// Copyright CodeGamified 2025-2026
// MIT License — Racer
using CodeGamified.Engine;
using CodeGamified.Time;
using Racer.Game;
using UnityEngine;

namespace Racer.Scripting
{
    /// <summary>
    /// IO handler — executes custom opcodes at runtime.
    /// Reads game state from RacerMatchManager/RacerCar/RacerTrack.
    /// </summary>
    public class RacerIOHandler : IGameIOHandler
    {
        private readonly RacerMatchManager _match;
        private readonly RacerCar _car;
        private readonly RacerTrack _track;

        public RacerIOHandler(RacerMatchManager match, RacerCar car, RacerTrack track)
        {
            _match = match;
            _car = car;
            _track = track;
        }

        public bool PreExecute(Instruction inst, MachineState state) => true;
        public float GetTimeScale() => SimulationTime.Instance?.timeScale ?? 1f;
        public double GetSimulationTime() => SimulationTime.Instance?.simulationTime ?? 0.0;

        public void ExecuteIO(Instruction inst, MachineState state)
        {
            int op = (int)inst.Op - (int)OpCode.CUSTOM_0;

            switch (op)
            {
                // ── Car state ──
                case RacerOps.GET_POS_X:
                    state.SetRegister(0, _car.PosX);
                    break;
                case RacerOps.GET_POS_Z:
                    state.SetRegister(0, _car.PosZ);
                    break;
                case RacerOps.GET_SPEED:
                    state.SetRegister(0, _car.Speed);
                    break;
                case RacerOps.GET_HEADING:
                    state.SetRegister(0, _car.Heading);
                    break;
                case RacerOps.GET_VEL_X:
                    state.SetRegister(0, _car.VelX);
                    break;
                case RacerOps.GET_VEL_Z:
                    state.SetRegister(0, _car.VelZ);
                    break;
                case RacerOps.GET_ON_ROAD:
                    state.SetRegister(0, _car.IsOnRoad ? 1f : 0f);
                    break;

                // ── Track/waypoint ──
                case RacerOps.GET_NEXT_WP:
                    state.SetRegister(0, _match.NextWaypoint);
                    break;
                case RacerOps.GET_ANGLE_TO_NEXT:
                    state.SetRegister(0, _match.AngleToNext);
                    break;
                case RacerOps.GET_DIST_TO_NEXT:
                    state.SetRegister(0, _match.DistToNext);
                    break;
                case RacerOps.GET_DIST_TO_TRACK:
                    state.SetRegister(0, _match.DistToTrack);
                    break;
                case RacerOps.GET_TRACK_WIDTH:
                    state.SetRegister(0, _track.TrackWidth);
                    break;
                case RacerOps.GET_WP_COUNT:
                    state.SetRegister(0, _track.WaypointCount);
                    break;
                case RacerOps.GET_WP_X:
                {
                    int idx = (int)state.GetRegister(0);
                    state.SetRegister(0, _track.GetWaypoint(idx).x);
                    break;
                }
                case RacerOps.GET_WP_Z:
                {
                    int idx = (int)state.GetRegister(0);
                    state.SetRegister(0, _track.GetWaypoint(idx).z);
                    break;
                }
                case RacerOps.GET_PROGRESS:
                    state.SetRegister(0, _match.Progress * 100f);
                    break;

                // ── Race state ──
                case RacerOps.GET_CURRENT_LAP:
                    state.SetRegister(0, _match.CurrentLap);
                    break;
                case RacerOps.GET_TOTAL_LAPS:
                    state.SetRegister(0, _match.TotalLaps);
                    break;
                case RacerOps.GET_RACE_TIME:
                    state.SetRegister(0, _match.RaceTime * 100f);
                    break;
                case RacerOps.GET_LAP_TIME:
                    state.SetRegister(0, _match.CurrentLapTime * 100f);
                    break;
                case RacerOps.GET_BEST_LAP:
                    state.SetRegister(0, _match.BestLapTime < float.MaxValue ? _match.BestLapTime * 100f : 0f);
                    break;
                case RacerOps.GET_BEST_RACE:
                    state.SetRegister(0, _match.BestRaceTime < float.MaxValue ? _match.BestRaceTime * 100f : 0f);
                    break;
                case RacerOps.GET_RACES_DONE:
                    state.SetRegister(0, _match.RacesCompleted);
                    break;
                case RacerOps.GET_RACE_FINISHED:
                    state.SetRegister(0, _match.RaceFinished ? 1f : 0f);
                    break;

                // ── Input ──
                case RacerOps.GET_INPUT:
                    state.SetRegister(0, RacerInputProvider.Instance != null
                        ? RacerInputProvider.Instance.CurrentInput : 0f);
                    break;

                // ── Utility ──
                case RacerOps.GET_ANGLE_DIFF:
                {
                    float from = state.GetRegister(0);
                    float to = state.GetRegister(1);
                    float diff = to - from;
                    // Normalize to -180..180
                    while (diff > 180f) diff -= 360f;
                    while (diff < -180f) diff += 360f;
                    state.SetRegister(0, diff);
                    break;
                }

                // ── Commands ──
                case RacerOps.SET_THROTTLE:
                {
                    float v = Mathf.Clamp(state.GetRegister(0), -1f, 1f);
                    _car.Throttle = v;
                    state.SetRegister(0, 1f);
                    break;
                }
                case RacerOps.SET_STEERING:
                {
                    float v = Mathf.Clamp(state.GetRegister(0), -1f, 1f);
                    _car.Steering = v;
                    state.SetRegister(0, 1f);
                    break;
                }
            }
        }
    }
}
