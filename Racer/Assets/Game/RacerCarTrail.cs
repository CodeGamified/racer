// Copyright CodeGamified 2025-2026
// MIT License — Racer
using System.Collections.Generic;
using UnityEngine;
using CodeGamified.Quality;

namespace Racer.Game
{
    /// <summary>
    /// Procedural car trail on XZ plane.
    ///  • Low/Med/High: ring buffer of spheres that fade out behind the car.
    ///  • Ultra: LineRenderer segments with HDR glow.
    /// </summary>
    public class RacerCarTrail : MonoBehaviour, IQualityResponsive
    {
        private int _trailLength;
        private const float TRAIL_INTERVAL = 0.02f;
        private const int ULTRA_THRESHOLD = 1000;

        // Ring buffer mode (Low/Med/High)
        private Transform[] _trailParts;
        private Renderer[] _trailRenderers;
        private int _writeIndex;

        // Line mode (Ultra)
        private List<LineRenderer> _lineSegments;
        private List<List<Vector3>> _segmentPoints;
        private bool _lineMode;
        private Color _currentLineColor;
        private Material _lineMaterial;

        // Shared
        private float _nextSpawnTime;
        private RacerCar _car;
        private Material _trailMaterial;
        private Color _trailBaseColor;
        private static readonly Color DefaultTrailHDR = new Color(0.3f, 1.5f, 3f); // bright blue

        // Speed-bucket coloring (5 bands: 0-20%, 20-40%, 40-60%, 60-80%, 80-100%)
        private int _currentBucket = -1;
        private static readonly Color[] SpeedBucketColors = new Color[]
        {
            new Color(0.2f, 0.3f, 1.5f),   // 0-20%   blue   (crawling)
            new Color(0.2f, 1.2f, 1.5f),   // 20-40%  cyan   (cruising)
            new Color(0.3f, 1.8f, 0.4f),   // 40-60%  green  (moderate)
            new Color(2.0f, 1.8f, 0.2f),   // 60-80%  yellow (fast)
            new Color(2.5f, 0.4f, 0.1f),   // 80-100% red    (near max)
        };

        public void Initialize(RacerCar car, Color trailColor)
        {
            _car = car;
            _trailBaseColor = trailColor;
            _trailLength = QualityHints.TrailSegments(QualityBridge.CurrentTier);
            Build();
        }

        private void OnEnable()  => QualityBridge.Register(this);
        private void OnDisable() => QualityBridge.Unregister(this);

        public void OnQualityChanged(QualityTier tier)
        {
            int newLength = QualityHints.TrailSegments(tier);
            if (newLength == _trailLength) return;
            _trailLength = newLength;
            Cleanup();
            Build();
        }

        private void Build()
        {
            _lineMode = _trailLength >= ULTRA_THRESHOLD;
            if (_lineMode) BuildLineMode();
            else BuildSphereMode();
        }

        private void BuildLineMode()
        {
            _lineSegments = new List<LineRenderer>(16);
            _segmentPoints = new List<List<Vector3>>(16);
            _currentLineColor = DefaultTrailHDR;

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Universal Render Pipeline/Unlit");
            _lineMaterial = new Material(shader);
            _lineMaterial.SetFloat("_Surface", 0);
            _lineMaterial.SetColor("_BaseColor", Color.white);

            StartNewSegment(DefaultTrailHDR);
        }

        private void BuildSphereMode()
        {
            _trailParts = new Transform[_trailLength];
            _trailRenderers = new Renderer[_trailLength];

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Unlit/Color");

            _trailMaterial = new Material(shader);
            Color halfAlpha = new Color(_trailBaseColor.r, _trailBaseColor.g, _trailBaseColor.b, 0.5f);
            if (_trailMaterial.HasProperty("_BaseColor"))
                _trailMaterial.SetColor("_BaseColor", halfAlpha);
            else
                _trailMaterial.color = halfAlpha;

            for (int i = 0; i < _trailLength; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"Trail_{i}";
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * 0.06f;
                go.SetActive(false);

                var collider = go.GetComponent<Collider>();
                if (collider != null) Destroy(collider);

                var r = go.GetComponent<Renderer>();
                r.material = new Material(_trailMaterial);

                _trailParts[i] = go.transform;
                _trailRenderers[i] = r;
            }
        }

        private void Cleanup()
        {
            if (_trailParts != null)
            {
                for (int i = 0; i < _trailParts.Length; i++)
                    if (_trailParts[i] != null)
                        Destroy(_trailParts[i].gameObject);
                _trailParts = null;
                _trailRenderers = null;
            }
            ClearLineSegments();
            _writeIndex = 0;
        }

        private void ClearLineSegments()
        {
            if (_lineSegments != null)
            {
                for (int i = 0; i < _lineSegments.Count; i++)
                    if (_lineSegments[i] != null)
                        Destroy(_lineSegments[i].gameObject);
                _lineSegments.Clear();
            }
            _segmentPoints?.Clear();
        }

        private void Update()
        {
            if (_car == null) return;

            // Check for speed-bucket change → new line segment
            int bucket = GetSpeedBucket();
            if (bucket != _currentBucket)
            {
                _currentBucket = bucket;
                Color col = SpeedBucketColors[bucket];
                if (_lineMode)
                    StartNewSegment(col);
                else
                    _trailBaseColor = col;
            }

            if (Time.time >= _nextSpawnTime)
            {
                _nextSpawnTime = Time.time + TRAIL_INTERVAL;
                if (_lineMode) AppendLinePoint();
                else SpawnSpherePoint();
            }

            if (!_lineMode)
                UpdateSphereFade();
        }

        private int GetSpeedBucket()
        {
            float frac = Mathf.Clamp01(Mathf.Abs(_car.Speed) / RacerCar.MaxSpeed);
            int bucket = Mathf.FloorToInt(frac * 5f);
            return Mathf.Clamp(bucket, 0, SpeedBucketColors.Length - 1);
        }

        private Vector3 CarWorldPos()
        {
            return new Vector3(_car.PosX, 0.02f, _car.PosZ);
        }

        // ── Sphere mode ──────────────────────────────────────────

        private void SpawnSpherePoint()
        {
            if (_trailParts == null || Mathf.Abs(_car.Speed) < 0.1f) return;

            var pos = CarWorldPos();
            var part = _trailParts[_writeIndex];
            part.position = pos;
            part.localScale = Vector3.one * 0.06f;
            part.gameObject.SetActive(true);

            Color hdr = SpeedBucketColors[_currentBucket >= 0 ? _currentBucket : 0];
            SetHDRColorMat(_trailRenderers[_writeIndex].material, hdr);

            _writeIndex = (_writeIndex + 1) % _trailLength;
        }

        private void UpdateSphereFade()
        {
            if (_trailParts == null) return;
            float fadeRate = 2f * Time.deltaTime;
            for (int i = 0; i < _trailLength; i++)
            {
                if (!_trailParts[i].gameObject.activeSelf) continue;

                float s = _trailParts[i].localScale.x;
                s -= fadeRate * 0.03f;
                if (s <= 0.005f)
                {
                    _trailParts[i].gameObject.SetActive(false);
                    continue;
                }
                _trailParts[i].localScale = Vector3.one * s;

                var mat = _trailRenderers[i].material;
                Color c = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.color;
                c *= (1f - fadeRate);
                SetHDRColorMat(mat, c);
            }
        }

        // ── Line mode ────────────────────────────────────────────

        private void AppendLinePoint()
        {
            if (Mathf.Abs(_car.Speed) < 0.1f) return;

            var pos = CarWorldPos();
            if (_lineSegments == null || _lineSegments.Count == 0) return;
            var points = _segmentPoints[_segmentPoints.Count - 1];
            var lr = _lineSegments[_lineSegments.Count - 1];

            if (points.Count > 0 &&
                Vector3.SqrMagnitude(pos - points[points.Count - 1]) < 0.0001f)
                return;

            points.Add(pos);
            lr.positionCount = points.Count;
            lr.SetPosition(points.Count - 1, pos);
        }

        private void StartNewSegment(Color hdrColor)
        {
            if (_lineSegments == null) return;

            var go = new GameObject($"TrailSeg_{_lineSegments.Count}");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = new Material(_lineMaterial);
            lr.startColor = hdrColor;
            lr.endColor = hdrColor * 0.3f;
            lr.startWidth = 0.06f;
            lr.endWidth = 0.02f;
            lr.numCapVertices = 2;
            lr.positionCount = 0;
            lr.useWorldSpace = true;

            if (lr.material.HasProperty("_EmissionColor"))
            {
                lr.material.EnableKeyword("_EMISSION");
                lr.material.SetColor("_EmissionColor", hdrColor);
            }

            _lineSegments.Add(lr);
            _segmentPoints.Add(new List<Vector3>(256));
        }

        /// <summary>Set trail color (e.g., change on off-road).</summary>
        public void SetColor(Color hdrColor)
        {
            if (_lineMode)
            {
                if (_currentLineColor != hdrColor)
                {
                    _currentLineColor = hdrColor;
                    StartNewSegment(hdrColor);
                }
            }
            else
            {
                _trailBaseColor = hdrColor;
            }
        }

        /// <summary>Clear all trail visuals (on race restart).</summary>
        public void ClearLine()
        {
            if (_lineMode)
            {
                ClearLineSegments();
                StartNewSegment(_currentLineColor);
            }
            else
            {
                if (_trailParts != null)
                    for (int i = 0; i < _trailLength; i++)
                        _trailParts[i].gameObject.SetActive(false);
                _writeIndex = 0;
            }
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
    }
}
