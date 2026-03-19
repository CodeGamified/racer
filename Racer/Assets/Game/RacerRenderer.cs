// Copyright CodeGamified 2025-2026
// MIT License — Racer
using System.Collections.Generic;
using UnityEngine;
using CodeGamified.Quality;

namespace Racer.Game
{
    /// <summary>
    /// Visual renderer — top-down racing view.
    /// Track as a ribbon of flat quads, car as a colored cube, waypoint markers.
    /// Glow system: car point light + waypoint point light (pulsing) + HDR emission.
    /// </summary>
    public class RacerRenderer : MonoBehaviour, IQualityResponsive
    {
        private RacerTrack _track;
        private RacerCar _car;
        private RacerMatchManager _match;
        private bool _dirty = true;

        // ── Objects ──
        private readonly List<GameObject> _trackSegments = new();
        private readonly List<GameObject> _dynamicObjects = new();
        private GameObject _carObject;
        private Renderer _carRenderer;
        private GameObject _nextWpMarker;
        private Renderer _nextWpRenderer;

        // ── Glow system ──
        private Light _carLight;
        private const float CarLightBaseIntensity = 0.5f;
        private const float CarLightDecay = 3f;

        private Light _wpLight;
        private const float WpLightBaseIntensity = 0.4f;
        private const float WpLightRange = 1.5f;
        private float _wpPulsePhase;

        private readonly List<(Renderer renderer, Color baseColor)> _flashedRenderers = new();

        // ── Colors ──
        private static readonly Color RoadColor      = new(0.2f, 0.2f, 0.22f);
        private static readonly Color RoadEdgeColor  = new(0.7f, 0.7f, 0.3f);
        private static readonly Color OffRoadColor   = new(0.08f, 0.15f, 0.05f);
        public  static readonly Color CarColor       = new(0.1f, 0.5f, 0.95f);
        public  static readonly Color CarOffRoadColor = new(0.9f, 0.3f, 0.1f);
        public  static readonly Color WaypointColor  = new(0.2f, 0.9f, 0.3f, 0.5f);
        private static readonly Color FinishColor    = new(1f, 1f, 1f);

        public void Initialize(RacerTrack track, RacerCar car, RacerMatchManager match)
        {
            _track = track;
            _car = car;
            _match = match;
            BuildTrack();
            BuildCar();
            BuildWaypointMarker();
            QualityBridge.Register(this);
        }

        private void OnDisable() => QualityBridge.Unregister(this);
        public void OnQualityChanged(QualityTier tier) => _dirty = true;

        private void LateUpdate()
        {
            DecayCarLight();
            DecayWpLight();
            DecayFlashedRenderers();

            UpdateCar();
            UpdateWaypointMarker();
        }

        public void MarkDirty() => _dirty = true;

        // ═══════════════════════════════════════════════════════════════
        // BUILD TRACK — static geometry, created once
        // ═══════════════════════════════════════════════════════════════

        private void BuildTrack()
        {
            float halfWidth = _track.TrackWidth * 0.5f;
            float edgeExtra = 0.05f;
            int count = _track.WaypointCount;

            // Ground plane
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.parent = transform;
            float gx = (_track.TrackMinX + _track.TrackMaxX) * 0.5f;
            float gz = (_track.TrackMinZ + _track.TrackMaxZ) * 0.5f;
            float gw = _track.TrackMaxX - _track.TrackMinX + 6f;
            float gd = _track.TrackMaxZ - _track.TrackMinZ + 6f;
            ground.transform.localPosition = new Vector3(gx, -0.06f, gz);
            ground.transform.localScale = new Vector3(gw, 0.02f, gd);
            ground.GetComponent<Renderer>().material = CreateMat(OffRoadColor);
            ground.name = "Ground";
            _trackSegments.Add(ground);

            // Compute per-waypoint perpendicular (averaged from incoming/outgoing)
            var leftRoad  = new Vector3[count];
            var rightRoad = new Vector3[count];
            var leftEdge  = new Vector3[count];
            var rightEdge = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                int prev = (i - 1 + count) % count;
                int next = (i + 1) % count;
                var tangent = (_track.Waypoints[next] - _track.Waypoints[prev]).normalized;
                var perp = new Vector3(-tangent.z, 0f, tangent.x); // left-hand perp on XZ

                var wp = _track.Waypoints[i];
                leftRoad[i]  = wp - perp * halfWidth;
                rightRoad[i] = wp + perp * halfWidth;
                leftEdge[i]  = wp - perp * (halfWidth + edgeExtra);
                rightEdge[i] = wp + perp * (halfWidth + edgeExtra);
            }

            // Build road edge ribbon mesh (slightly wider, sits below road)
            BuildRibbonMesh("TrackEdge", leftEdge, rightEdge, -0.035f, RoadEdgeColor);
            // Build road surface ribbon mesh
            BuildRibbonMesh("TrackRoad", leftRoad, rightRoad, -0.02f, RoadColor);

            // Start/finish line
            var wp0 = _track.GetWaypoint(0);
            var wp1 = _track.GetWaypoint(1);
            var startDir = (wp1 - wp0).normalized;
            var finishLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            finishLine.transform.parent = transform;
            finishLine.transform.localPosition = new Vector3(wp0.x, -0.01f, wp0.z);
            finishLine.transform.localScale = new Vector3(_track.TrackWidth * 1.2f, 0.01f, 0.08f);
            float finAngle = Mathf.Atan2(startDir.x, startDir.z) * Mathf.Rad2Deg;
            finishLine.transform.localRotation = Quaternion.Euler(0, finAngle, 0);
            finishLine.GetComponent<Renderer>().material = CreateMat(FinishColor);
            finishLine.name = "FinishLine";
            _trackSegments.Add(finishLine);
        }

        /// <summary>
        /// Creates a closed-loop ribbon mesh from left/right edge arrays.
        /// </summary>
        private void BuildRibbonMesh(string name, Vector3[] left, Vector3[] right, float y, Color color)
        {
            int count = left.Length;
            var verts = new Vector3[count * 2];
            var tris = new int[count * 6]; // 2 triangles per quad, 3 indices each

            for (int i = 0; i < count; i++)
            {
                verts[i * 2]     = new Vector3(left[i].x,  y, left[i].z);
                verts[i * 2 + 1] = new Vector3(right[i].x, y, right[i].z);
            }

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                int vi = i * 2;
                int vn = next * 2;
                int ti = i * 6;

                // Clockwise winding viewed from +Y (camera looks down)
                tris[ti]     = vi;
                tris[ti + 1] = vi + 1;
                tris[ti + 2] = vn;

                tris[ti + 3] = vi + 1;
                tris[ti + 4] = vn + 1;
                tris[ti + 5] = vn;
            }

            var mesh = new Mesh { vertices = verts, triangles = tris };
            mesh.RecalculateNormals();

            var go = new GameObject(name);
            go.transform.parent = transform;
            go.AddComponent<MeshFilter>().mesh = mesh;
            go.AddComponent<MeshRenderer>().material = CreateMat(color);
            _trackSegments.Add(go);
        }

        // ═══════════════════════════════════════════════════════════════
        // BUILD CAR + WAYPOINT MARKER
        // ═══════════════════════════════════════════════════════════════

        private void BuildCar()
        {
            _carObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _carObject.transform.parent = transform;
            _carObject.transform.localScale = new Vector3(0.2f, 0.08f, 0.35f);
            _carRenderer = _carObject.GetComponent<Renderer>();
            _carRenderer.material = CreateMat(CarColor);
            _carObject.name = "Car";
        }

        private void BuildWaypointMarker()
        {
            _nextWpMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _nextWpMarker.transform.parent = transform;
            _nextWpMarker.transform.localScale = new Vector3(0.3f, 0.02f, 0.3f);
            _nextWpRenderer = _nextWpMarker.GetComponent<Renderer>();
            _nextWpRenderer.material = CreateMat(WaypointColor);
            _nextWpMarker.name = "NextWaypoint";
        }

        // ═══════════════════════════════════════════════════════════════
        // UPDATE — real-time car position
        // ═══════════════════════════════════════════════════════════════

        private void UpdateCar()
        {
            if (_carObject == null || _car == null) return;

            _carObject.transform.localPosition = new Vector3(_car.PosX, 0.04f, _car.PosZ);
            _carObject.transform.localRotation = Quaternion.Euler(0, _car.Heading, 0);

            // Tint red when off-road, blue when on-road — with HDR emission
            Color c = _car.IsOnRoad ? CarColor : CarOffRoadColor;
            float speedBoost = 1f + Mathf.Clamp01(Mathf.Abs(_car.Speed) / RacerCar.MaxSpeed) * 1.5f;
            Color hdr = new Color(c.r * speedBoost, c.g * speedBoost, c.b * speedBoost);
            SetHDRColorMat(_carRenderer.material, hdr);
        }

        private void UpdateWaypointMarker()
        {
            if (_nextWpMarker == null || _match == null || _track == null) return;

            var wp = _track.GetWaypoint(_match.NextWaypoint);
            _nextWpMarker.transform.localPosition = new Vector3(wp.x, 0.01f, wp.z);

            // Waypoint always glows with HDR emission
            Color wpHDR = new Color(WaypointColor.r * 2f, WaypointColor.g * 2f, WaypointColor.b * 2f);
            SetHDRColorMat(_nextWpRenderer.material, wpHDR);
        }

        // ═══════════════════════════════════════════════════════════════
        // GLOW / FLASH API — called by RacerBootstrap event wiring
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Create a point light on the car.</summary>
        public void CreateCarLight()
        {
            if (_carLight != null) return;
            var go = new GameObject("CarGlow");
            go.transform.SetParent(transform, false);
            _carLight = go.AddComponent<Light>();
            _carLight.type = LightType.Point;
            _carLight.range = 2f;
            _carLight.intensity = CarLightBaseIntensity;
            _carLight.color = CarColor;
            _carLight.shadows = LightShadows.None;
        }

        /// <summary>Create a pulsing point light on the next waypoint.</summary>
        public void CreateWaypointLight()
        {
            if (_wpLight != null) return;
            var go = new GameObject("WpGlow");
            go.transform.SetParent(transform, false);
            _wpLight = go.AddComponent<Light>();
            _wpLight.type = LightType.Point;
            _wpLight.range = WpLightRange;
            _wpLight.intensity = WpLightBaseIntensity;
            _wpLight.color = new Color(WaypointColor.r, WaypointColor.g, WaypointColor.b);
            _wpLight.shadows = LightShadows.None;
        }

        /// <summary>Flash the car light to a high intensity + color.</summary>
        public void FlashCarLight(float intensity, Color color)
        {
            if (_carLight == null) return;
            _carLight.intensity = intensity;
            _carLight.color = color;
            _carLight.range = 2f + intensity * 0.3f;
        }

        /// <summary>Flash the waypoint light.</summary>
        public void FlashWpLight(float intensity, Color color)
        {
            if (_wpLight == null) return;
            _wpLight.intensity = intensity;
            _wpLight.color = color;
            _wpLight.range = WpLightRange + intensity * 0.3f;
        }

        /// <summary>Flash the car cube with HDR color.</summary>
        public void FlashCarColor(Color hdrColor)
        {
            if (_carRenderer == null) return;
            FlashRenderer(_carObject, hdrColor, CarColor);
        }

        /// <summary>Flash the waypoint marker with HDR color.</summary>
        public void FlashWaypointColor(Color hdrColor)
        {
            if (_nextWpRenderer == null) return;
            Color baseCol = new Color(WaypointColor.r, WaypointColor.g, WaypointColor.b);
            FlashRenderer(_nextWpMarker, hdrColor, baseCol);
        }

        // ═══════════════════════════════════════════════════════════════
        // DECAY — runs every LateUpdate
        // ═══════════════════════════════════════════════════════════════

        private void DecayCarLight()
        {
            if (_carLight == null) return;
            float decay = Mathf.Clamp01(CarLightDecay * Time.unscaledDeltaTime);
            _carLight.intensity = Mathf.Lerp(_carLight.intensity, CarLightBaseIntensity, decay);
            _carLight.color = Color.Lerp(_carLight.color, CarColor, decay);
            _carLight.range = Mathf.Lerp(_carLight.range, 2f, decay);

            if (_car != null)
                _carLight.transform.localPosition = new Vector3(_car.PosX, 0.15f, _car.PosZ);
        }

        private void DecayWpLight()
        {
            if (_wpLight == null || _track == null || _match == null) return;

            // Gentle pulse
            _wpPulsePhase += Time.unscaledDeltaTime * 3f;
            float pulse = WpLightBaseIntensity + Mathf.Sin(_wpPulsePhase) * 0.15f;

            float decay = Mathf.Clamp01(CarLightDecay * Time.unscaledDeltaTime);
            _wpLight.intensity = Mathf.Lerp(_wpLight.intensity, pulse, decay);
            Color baseWp = new Color(WaypointColor.r, WaypointColor.g, WaypointColor.b);
            _wpLight.color = Color.Lerp(_wpLight.color, baseWp, decay);
            _wpLight.range = Mathf.Lerp(_wpLight.range, WpLightRange, decay);

            var wp = _track.GetWaypoint(_match.NextWaypoint);
            _wpLight.transform.localPosition = new Vector3(wp.x, 0.2f, wp.z);
        }

        private void DecayFlashedRenderers()
        {
            float decay = Mathf.Clamp01(CarLightDecay * Time.unscaledDeltaTime);
            for (int i = _flashedRenderers.Count - 1; i >= 0; i--)
            {
                var (fr, baseCol) = _flashedRenderers[i];
                if (fr == null) { _flashedRenderers.RemoveAt(i); continue; }
                var mat = fr.material;
                Color current = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.color;
                Color next = Color.Lerp(current, baseCol, decay);
                SetHDRColorMat(mat, next);
                if (Mathf.Abs(next.r - baseCol.r) + Mathf.Abs(next.g - baseCol.g) + Mathf.Abs(next.b - baseCol.b) < 0.03f)
                {
                    SetHDRColorMat(mat, baseCol);
                    _flashedRenderers.RemoveAt(i);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // HDR HELPERS
        // ═══════════════════════════════════════════════════════════════

        private void FlashRenderer(GameObject go, Color hdrColor, Color baseColor)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            int idx = _flashedRenderers.FindIndex(e => e.renderer == r);
            Color origColor = baseColor;
            if (idx >= 0)
            {
                origColor = _flashedRenderers[idx].baseColor;
                _flashedRenderers.RemoveAt(idx);
            }
            _flashedRenderers.Add((r, origColor));
            SetHDRColorMat(r.material, hdrColor);
        }

        private static void SetHDRColorMat(Material mat, Color color)
        {
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else
                mat.color = color;

            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color);
            }
        }

        // ═══════════════════════════════════════════════════════════════

        private static Material CreateMat(Color c)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = c;
            if (c.a < 1f)
            {
                mat.SetFloat("_Surface", 1);
                mat.SetFloat("_Blend", 0);
                mat.SetFloat("_DstBlend", 10);
                mat.SetFloat("_ZWrite", 0);
                mat.renderQueue = 3000;
            }
            return mat;
        }
    }
}
