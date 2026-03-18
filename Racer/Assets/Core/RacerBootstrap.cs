// Copyright CodeGamified 2025-2026
// MIT License — Racer
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using CodeGamified.Camera;
using CodeGamified.Time;
using CodeGamified.Settings;
using CodeGamified.Quality;
using CodeGamified.Bootstrap;
using Racer.Game;
using Racer.Scripting;

namespace Racer.Core
{
    /// <summary>
    /// Bootstrap for Racer — code-controlled top-down racing.
    ///
    /// Architecture (same pattern as all CodeGamified games):
    ///   - Instantiate managers → wire cross-references → configure scene
    ///   - .engine submodule gives us TUI + Code Execution for free
    ///   - Player writes code to control throttle + steering
    ///   - Procedural oval track with off-road penalties
    ///   - "Unit test" your racing AI by watching it lap at 100x speed
    ///
    /// Track on XZ plane. Camera looks down from above.
    /// Attach to a GameObject. Press Play → Track + car appear.
    /// </summary>
    public class RacerBootstrap : GameBootstrap, IQualityResponsive
    {
        protected override string LogTag => "RACER";

        // =================================================================
        // INSPECTOR
        // =================================================================

        [Header("Track")]
        [Tooltip("Random seed for track generation (-1 = random)")]
        public int trackSeed = -1;

        [Header("Race")]
        [Tooltip("Laps to complete per race")]
        public int totalLaps = 3;

        [Tooltip("Auto-restart after finish")]
        public bool autoRestart = true;

        [Tooltip("Delay before restarting (sim-seconds)")]
        public float restartDelay = 2f;

        [Tooltip("Seconds off-road before auto-reset to track")]
        public float stuckTimeout = 10f;

        [Header("Time")]
        [Tooltip("Enable time scale modulation for fast testing")]
        public bool enableTimeScale = true;

        [Header("Scripting")]
        [Tooltip("Enable code execution (.engine)")]
        public bool enableScripting = true;

        [Header("Camera")]
        public bool configureCamera = true;

        // =================================================================
        // RUNTIME REFERENCES
        // =================================================================

        private RacerTrack _track;
        private RacerCar _car;
        private RacerMatchManager _match;
        private RacerRenderer _renderer;
        private RacerProgram _playerProgram;

        // Camera
        private CameraAmbientMotion _cameraSway;

        // Post-processing
        private Bloom _bloom;
        private Volume _postProcessVolume;

        // =================================================================
        // UPDATE
        // =================================================================

        private void Update()
        {
            UpdateBloomScale();
        }

        private void UpdateBloomScale()
        {
            if (_bloom == null || !_bloom.active) return;
            var cam = Camera.main;
            if (cam == null) return;
            float dist = Vector3.Distance(cam.transform.position, TrackCenter());
            float defaultDist = 6f;
            float scale = Mathf.Clamp01(defaultDist / Mathf.Max(dist, 0.01f));
            _bloom.intensity.value = Mathf.Lerp(0.5f, 1.0f, scale);
        }

        // =================================================================
        // BOOTSTRAP
        // =================================================================

        private void Start()
        {
            Log("🏎️ Racer Bootstrap starting...");

            SettingsBridge.Load();
            QualityBridge.SetTier((QualityTier)SettingsBridge.QualityLevel);
            QualityBridge.Register(this);
            Log($"Settings loaded (Quality={SettingsBridge.QualityLevel}, Font={SettingsBridge.FontSize}pt)");

            SetupSimulationTime();
            CreateTrack();
            CreateCar();
            CreateMatchManager();
            SetupCamera();
            CreateRenderer();
            CreateInputProvider();

            if (enableScripting) CreatePlayerProgram();

            WireEvents();
            StartCoroutine(RunBootSequence());
        }

        public void OnQualityChanged(QualityTier tier)
        {
            Log($"Quality changed → {tier}");
        }

        // =================================================================
        // SIMULATION TIME
        // =================================================================

        private void SetupSimulationTime()
        {
            EnsureSimulationTime<RacerSimulationTime>();
        }

        // =================================================================
        // TRACK + CAR
        // =================================================================

        private Vector3 TrackCenter()
        {
            if (_track == null) return Vector3.zero;
            return new Vector3(
                (_track.TrackMinX + _track.TrackMaxX) * 0.5f, 0f,
                (_track.TrackMinZ + _track.TrackMaxZ) * 0.5f);
        }

        private void CreateTrack()
        {
            var go = new GameObject("RacerTrack");
            _track = go.AddComponent<RacerTrack>();
            _track.Initialize(trackSeed);
            Log($"Created Track ({_track.WaypointCount} waypoints, width={_track.TrackWidth})");
        }

        private void CreateCar()
        {
            var go = new GameObject("RacerCar");
            _car = go.AddComponent<RacerCar>();
            // Initialize at first waypoint (actual placement deferred to StartMatch)
            var start = _track.GetWaypoint(0);
            var next = _track.GetWaypoint(1);
            float heading = Mathf.Atan2(next.x - start.x, next.z - start.z) * Mathf.Rad2Deg;
            _car.Initialize(_track, start, heading);
            Log("Created Car (physics on XZ plane)");
        }

        // =================================================================
        // CAMERA — top-down view of the track
        // =================================================================

        private void SetupCamera()
        {
            if (!configureCamera) return;

            var cam = EnsureCamera();

            cam.orthographic = false;
            cam.fieldOfView = 60f;
            var center = TrackCenter();
            float trackSpan = Mathf.Max(
                _track.TrackMaxX - _track.TrackMinX,
                _track.TrackMaxZ - _track.TrackMinZ);
            float camHeight = trackSpan * 0.7f + 2f;
            cam.transform.position = center + new Vector3(0f, camHeight, -1f);
            cam.transform.LookAt(center, Vector3.up);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.01f, 0.02f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;

            // Ambient sway
            _cameraSway = cam.gameObject.AddComponent<CameraAmbientMotion>();
            _cameraSway.lookAtTarget = center;

            // Post-processing: bloom
            var camData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (camData == null)
                camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;

            var volumeGO = new GameObject("PostProcessVolume");
            _postProcessVolume = volumeGO.AddComponent<Volume>();
            _postProcessVolume.isGlobal = true;
            _postProcessVolume.priority = 1;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _bloom = profile.Add<Bloom>();
            _bloom.threshold.overrideState = true;
            _bloom.threshold.value = 0.8f;
            _bloom.intensity.overrideState = true;
            _bloom.intensity.value = 1.0f;
            _bloom.scatter.overrideState = true;
            _bloom.scatter.value = 0.5f;
            _bloom.clamp.overrideState = true;
            _bloom.clamp.value = 20f;
            _bloom.highQualityFiltering.overrideState = true;
            _bloom.highQualityFiltering.value = true;
            _postProcessVolume.profile = profile;

            Log($"Camera: perspective, FOV=60, height={camHeight:F1}, top-down track view + sway + bloom");
        }

        // =================================================================
        // MATCH MANAGER
        // =================================================================

        private void CreateMatchManager()
        {
            var go = new GameObject("MatchManager");
            _match = go.AddComponent<RacerMatchManager>();
            _match.Initialize(_track, _car, totalLaps, autoRestart, restartDelay, stuckTimeout);
            Log($"Created MatchManager ({totalLaps} laps)");
        }

        // =================================================================
        // RENDERER
        // =================================================================

        private void CreateRenderer()
        {
            var go = new GameObject("RacerRenderer");
            _renderer = go.AddComponent<RacerRenderer>();
            _renderer.Initialize(_track, _car, _match);
            Log("Created Renderer (road + car + waypoint markers)");
        }

        // =================================================================
        // INPUT PROVIDER
        // =================================================================

        private void CreateInputProvider()
        {
            var go = new GameObject("InputProvider");
            go.AddComponent<RacerInputProvider>();
            Log("Created RacerInputProvider (WASD / Arrow Keys)");
        }

        // =================================================================
        // PLAYER SCRIPTING
        // =================================================================

        private void CreatePlayerProgram()
        {
            var go = new GameObject("PlayerProgram");
            _playerProgram = go.AddComponent<RacerProgram>();
            _playerProgram.Initialize(_match, _car, _track);
            Log("Created PlayerProgram (code-controlled racing AI)");
        }

        // =================================================================
        // EVENT WIRING
        // =================================================================

        private void WireEvents()
        {
            if (SimulationTime.Instance != null)
            {
                SimulationTime.Instance.OnTimeScaleChanged += s => Log($"Time scale → {s:F0}x");
                SimulationTime.Instance.OnPausedChanged += p => Log(p ? "⏸ PAUSED" : "▶ RESUMED");
            }

            if (_match != null)
            {
                _match.OnMatchStarted += () =>
                {
                    Log($"RACE STARTED — {totalLaps} laps, GO!");
                    _renderer?.MarkDirty();
                };

                _match.OnWaypointHit += () =>
                {
                    _renderer?.MarkDirty();
                };

                _match.OnLapCompleted += () =>
                {
                    string lapTime = FormatTime(_match.CurrentLapTime);
                    string bestLap = _match.BestLapTime < float.MaxValue
                        ? FormatTime(_match.BestLapTime) : "--:--.--";
                    Log($"LAP {_match.CurrentLap - 1} complete — {lapTime} │ Best: {bestLap}");
                    _renderer?.MarkDirty();
                };

                _match.OnRaceFinished += () =>
                {
                    string total = FormatTime(_match.RaceTime);
                    string best = _match.BestRaceTime < float.MaxValue
                        ? FormatTime(_match.BestRaceTime) : "--:--.--";
                    Log($"🏁 RACE FINISHED — Time: {total} │ Best: {best} │ Races: {_match.RacesCompleted}");
                    if (autoRestart)
                        StartCoroutine(RestartAfterDelay());
                };

                _match.OnBoardChanged += () => _renderer?.MarkDirty();
            }
        }

        private static string FormatTime(float seconds)
        {
            int m = (int)(seconds / 60f);
            int s = (int)(seconds % 60f);
            int ms = (int)((seconds % 1f) * 100f);
            return $"{m:D2}:{s:D2}.{ms:D2}";
        }

        // =================================================================
        // BOOT SEQUENCE
        // =================================================================

        private IEnumerator RunBootSequence()
        {
            yield return null;
            yield return null;

            LogDivider();
            Log("🏎️ RACER — Code Your Racing AI");
            LogDivider();
            LogStatus("TRACK", $"{_track.WaypointCount} waypoints, width={_track.TrackWidth}");
            LogStatus("LAPS", $"{totalLaps}");
            LogEnabled("SCRIPTING", enableScripting);
            LogEnabled("TIME SCALE", enableTimeScale);
            LogEnabled("AUTO RESTART", autoRestart);
            LogDivider();

            _match.StartMatch();
            Log("Race started — GO!");
        }

        private IEnumerator RestartAfterDelay()
        {
            float waited = 0f;
            while (waited < restartDelay)
            {
                if (SimulationTime.Instance != null && !SimulationTime.Instance.isPaused)
                    waited += Time.deltaTime * (SimulationTime.Instance?.timeScale ?? 1f);
                yield return null;
            }

            _match.StartMatch();
            _playerProgram?.ResetExecution();
            Log("Race restarted");
        }

        private void OnDestroy()
        {
            QualityBridge.Unregister(this);
        }
    }
}
