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
        private GameObject _nextWpMarker;

        // ── Colors ──
        private static readonly Color RoadColor      = new(0.2f, 0.2f, 0.22f);
        private static readonly Color RoadEdgeColor  = new(0.7f, 0.7f, 0.3f);
        private static readonly Color OffRoadColor   = new(0.08f, 0.15f, 0.05f);
        private static readonly Color CarColor       = new(0.1f, 0.5f, 0.95f);
        private static readonly Color CarOffRoadColor = new(0.9f, 0.3f, 0.1f);
        private static readonly Color WaypointColor  = new(0.2f, 0.9f, 0.3f, 0.5f);
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

            // Road segments
            for (int i = 0; i < _track.WaypointCount; i++)
            {
                int next = (i + 1) % _track.WaypointCount;
                var a = _track.Waypoints[i];
                var b = _track.Waypoints[next];

                var mid = (a + b) * 0.5f;
                var dir = b - a;
                float len = dir.magnitude;
                float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

                // Road surface
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.transform.parent = transform;
                seg.transform.localPosition = new Vector3(mid.x, -0.04f, mid.z);
                seg.transform.localScale = new Vector3(_track.TrackWidth, 0.02f, len);
                seg.transform.localRotation = Quaternion.Euler(0, angle, 0);
                seg.GetComponent<Renderer>().material = CreateMat(RoadColor);
                seg.name = $"Road_{i}";
                _trackSegments.Add(seg);

                // Road edge lines
                var edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                edge.transform.parent = transform;
                edge.transform.localPosition = new Vector3(mid.x, -0.035f, mid.z);
                edge.transform.localScale = new Vector3(_track.TrackWidth + 0.1f, 0.015f, len + 0.05f);
                edge.transform.localRotation = Quaternion.Euler(0, angle, 0);
                edge.GetComponent<Renderer>().material = CreateMat(RoadEdgeColor);
                edge.name = $"Edge_{i}";
                _trackSegments.Add(edge);
            }

            // Start/finish line
            var wp0 = _track.GetWaypoint(0);
            var wp1 = _track.GetWaypoint(1);
            var startDir = (wp1 - wp0).normalized;
            var finishLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            finishLine.transform.parent = transform;
            finishLine.transform.localPosition = new Vector3(wp0.x, -0.02f, wp0.z);
            finishLine.transform.localScale = new Vector3(_track.TrackWidth * 1.2f, 0.01f, 0.08f);
            float finAngle = Mathf.Atan2(startDir.x, startDir.z) * Mathf.Rad2Deg;
            finishLine.transform.localRotation = Quaternion.Euler(0, finAngle, 0);
            finishLine.GetComponent<Renderer>().material = CreateMat(FinishColor);
            finishLine.name = "FinishLine";
            _trackSegments.Add(finishLine);
        }

        // ═══════════════════════════════════════════════════════════════
        // BUILD CAR + WAYPOINT MARKER
        // ═══════════════════════════════════════════════════════════════

        private void BuildCar()
        {
            _carObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _carObject.transform.parent = transform;
            _carObject.transform.localScale = new Vector3(0.2f, 0.08f, 0.35f);
            _carObject.GetComponent<Renderer>().material = CreateMat(CarColor);
            _carObject.name = "Car";
        }

        private void BuildWaypointMarker()
        {
            _nextWpMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _nextWpMarker.transform.parent = transform;
            _nextWpMarker.transform.localScale = new Vector3(0.3f, 0.02f, 0.3f);
            _nextWpMarker.GetComponent<Renderer>().material = CreateMat(WaypointColor);
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

            // Tint red when off-road
            Color c = _car.IsOnRoad ? CarColor : CarOffRoadColor;
            _carObject.GetComponent<Renderer>().material.color = c;
        }

        private void UpdateWaypointMarker()
        {
            if (_nextWpMarker == null || _match == null || _track == null) return;

            var wp = _track.GetWaypoint(_match.NextWaypoint);
            _nextWpMarker.transform.localPosition = new Vector3(wp.x, 0.01f, wp.z);
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
