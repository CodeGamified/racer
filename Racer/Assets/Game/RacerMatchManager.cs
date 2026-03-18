// Copyright CodeGamified 2025-2026
// MIT License — Racer
using UnityEngine;
using CodeGamified.Time;

namespace Racer.Game
{
    /// <summary>
    /// Match manager — lap-based racing.
    ///
    /// Rules:
    ///   - Car races around a closed-loop track
    ///   - Complete N laps as fast as possible
    ///   - Waypoint progression: must pass each waypoint in order
    ///   - Off-road = heavy drag penalty (car slows dramatically)
    ///   - Game over when all laps completed; score = total time (lower better)
    ///   - Auto-restart after finish or when stuck too long
    ///
    /// Tick-based: code runs continuously, sets throttle/steering each tick.
    /// </summary>
    public class RacerMatchManager : MonoBehaviour
    {
        private RacerTrack _track;
        private RacerCar _car;

        // Config
        private int _totalLaps;
        private bool _autoRestart;
        private float _restartDelay;
        private float _stuckTimeout; // seconds off-road before auto-restart

        // State
        public int CurrentLap { get; private set; }
        public int TotalLaps => _totalLaps;
        public int NextWaypoint { get; private set; }
        public int WaypointsHit { get; private set; }
        public float RaceTime { get; private set; }
        public float BestLapTime { get; private set; }
        public float CurrentLapTime { get; private set; }
        public float BestRaceTime { get; private set; }
        public int RacesCompleted { get; private set; }

        public bool MatchInProgress { get; private set; }
        public bool RaceFinished { get; private set; }
        public bool GameOver { get; private set; }

        public float DistToTrack { get; private set; }
        public float AngleToNext { get; private set; }
        public float DistToNext { get; private set; }
        public float Progress { get; private set; } // 0..1 around current lap  

        // ── Events ──
        public System.Action OnMatchStarted;
        public System.Action OnLapCompleted;
        public System.Action OnRaceFinished;
        public System.Action OnBoardChanged;
        public System.Action OnWaypointHit;

        // Timing
        private float _offRoadTimer;

        public void Initialize(RacerTrack track, RacerCar car,
                               int totalLaps = 3, bool autoRestart = true,
                               float restartDelay = 2f, float stuckTimeout = 10f)
        {
            _track = track;
            _car = car;
            _totalLaps = totalLaps;
            _autoRestart = autoRestart;
            _restartDelay = restartDelay;
            _stuckTimeout = stuckTimeout;
            BestLapTime = float.MaxValue;
            BestRaceTime = float.MaxValue;
        }

        public void StartMatch()
        {
            // Place car at first waypoint, facing second
            var start = _track.GetWaypoint(0);
            var next = _track.GetWaypoint(1);
            float heading = Mathf.Atan2(next.x - start.x, next.z - start.z) * Mathf.Rad2Deg;
            _car.ResetTo(start, heading);

            CurrentLap = 1;
            NextWaypoint = 1;
            WaypointsHit = 0;
            RaceTime = 0f;
            CurrentLapTime = 0f;
            RaceFinished = false;
            GameOver = false;
            MatchInProgress = true;
            _offRoadTimer = 0f;

            UpdateDerivedState();

            OnMatchStarted?.Invoke();
            OnBoardChanged?.Invoke();
        }

        private void Update()
        {
            if (!MatchInProgress || RaceFinished) return;

            float timeScale = SimulationTime.Instance?.timeScale ?? 1f;
            if (SimulationTime.Instance != null && SimulationTime.Instance.isPaused) return;

            float dt = UnityEngine.Time.deltaTime * timeScale;
            if (dt <= 0f) return;

            // Step car physics
            _car.PhysicsStep(dt);

            // Update race timer
            RaceTime += dt;
            CurrentLapTime += dt;

            // Check waypoint progression
            CheckWaypoints();

            // Update derived state for bytecode queries
            UpdateDerivedState();

            // Off-road stuck detection
            if (!_car.IsOnRoad)
            {
                _offRoadTimer += dt;
                if (_offRoadTimer > _stuckTimeout)
                {
                    // Reset to nearest waypoint
                    int nearestWp = _track.ClosestWaypointIndex(new Vector3(_car.PosX, 0, _car.PosZ));
                    var wpPos = _track.GetWaypoint(nearestWp);
                    var nextWpPos = _track.GetWaypoint(nearestWp + 1);
                    float h = Mathf.Atan2(nextWpPos.x - wpPos.x, nextWpPos.z - wpPos.z) * Mathf.Rad2Deg;
                    _car.ResetTo(wpPos, h);
                    _offRoadTimer = 0f;
                }
            }
            else
            {
                _offRoadTimer = 0f;
            }

            OnBoardChanged?.Invoke();
        }

        private void CheckWaypoints()
        {
            var carPos = new Vector3(_car.PosX, 0f, _car.PosZ);
            var wpPos = _track.GetWaypoint(NextWaypoint);

            float dist = Vector3.Distance(
                new Vector3(carPos.x, 0, carPos.z),
                new Vector3(wpPos.x, 0, wpPos.z));

            // Hit waypoint within radius
            if (dist < _track.TrackWidth * 0.8f)
            {
                WaypointsHit++;
                NextWaypoint++;
                OnWaypointHit?.Invoke();

                // Lap completion check
                if (NextWaypoint >= _track.WaypointCount)
                {
                    NextWaypoint = 0;

                    if (CurrentLapTime < BestLapTime)
                        BestLapTime = CurrentLapTime;

                    OnLapCompleted?.Invoke();

                    if (CurrentLap >= _totalLaps)
                    {
                        // Race finished
                        RaceFinished = true;
                        MatchInProgress = false;

                        if (RaceTime < BestRaceTime)
                            BestRaceTime = RaceTime;
                        RacesCompleted++;

                        OnRaceFinished?.Invoke();
                        return;
                    }

                    CurrentLap++;
                    CurrentLapTime = 0f;
                }
            }
        }

        private void UpdateDerivedState()
        {
            var carPos = new Vector3(_car.PosX, 0f, _car.PosZ);
            DistToTrack = _track.DistanceToTrack(carPos);
            AngleToNext = _track.AngleToWaypoint(carPos, NextWaypoint);
            var nextWp = _track.GetWaypoint(NextWaypoint);
            DistToNext = Vector3.Distance(new Vector3(carPos.x, 0, carPos.z),
                                          new Vector3(nextWp.x, 0, nextWp.z));
            Progress = (float)(NextWaypoint % _track.WaypointCount) / _track.WaypointCount;
        }
    }
}
