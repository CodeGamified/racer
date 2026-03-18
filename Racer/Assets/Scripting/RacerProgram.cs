// Copyright CodeGamified 2025-2026
// MIT License — Racer
using UnityEngine;
using CodeGamified.Engine;
using CodeGamified.Engine.Compiler;
using CodeGamified.Engine.Runtime;
using CodeGamified.Time;
using Racer.Game;

namespace Racer.Scripting
{
    /// <summary>
    /// RacerProgram — code-controlled racing AI.
    /// Subclasses ProgramBehaviour from .engine.
    ///
    /// EXECUTION MODEL (tick-based, deterministic):
    ///   - Script runs at 20 ops/sec sim-time
    ///   - Memory persists across ticks, PC resets on HALT
    ///   - Each tick: read sensors → set_throttle() + set_steering()
    ///
    /// The interesting AI surface:
    ///   - Waypoint following (steer toward next waypoint)
    ///   - Speed management (brake before sharp turns)
    ///   - Line optimization (cut corners, apex tracking)
    ///   - Look-ahead (query upcoming waypoints for path planning)
    ///   - PID steering (proportional-integral-derivative control)
    /// </summary>
    public class RacerProgram : ProgramBehaviour
    {
        private RacerMatchManager _match;
        private RacerCar _car;
        private RacerTrack _track;
        private RacerIOHandler _ioHandler;
        private RacerCompilerExtension _compilerExt;

        public const float OPS_PER_SECOND = 20f;
        private float _opAccumulator;

        private const string DEFAULT_CODE = @"# 🏎️ RACER — Write your driving AI!
# Your script runs at 20 ops/sec (sim-time).
# Each tick: read sensors → set throttle/steering.
#
# BUILTINS — Car State:
#   get_pos_x()             → car X position
#   get_pos_z()             → car Z position
#   get_speed()             → current speed (units/sec)
#   get_heading()           → car heading (degrees, 0=+Z)
#   get_vel_x()             → velocity X
#   get_vel_z()             → velocity Z
#   get_on_road()           → 1 if on road (off-road = heavy drag)
#
# BUILTINS — Track:
#   get_next_wp()           → index of next waypoint
#   get_angle_to_next()     → angle (deg) to next waypoint
#   get_dist_to_next()      → distance to next waypoint
#   get_dist_to_track()     → dist from track center
#   get_track_width()       → road width
#   get_wp_count()          → waypoints in track
#   get_wp_x(i)             → waypoint i X position
#   get_wp_z(i)             → waypoint i Z position
#   get_progress()          → 0-100 around current lap
#
# BUILTINS — Race:
#   get_current_lap()       → current lap number
#   get_total_laps()        → laps to complete
#   get_race_time()         → total time × 100
#   get_lap_time()          → current lap time × 100
#   get_best_lap()          → best lap time × 100
#   get_best_race()         → best race time × 100
#   get_races_done()        → completed races
#   get_race_finished()     → 1 if race done
#
# BUILTINS — Utility:
#   get_angle_diff(a, b)    → signed angle delta (-180..180)
#
# BUILTINS — Commands:
#   set_throttle(v)         → -1 (reverse) to 1 (full gas)
#   set_steering(v)         → -1 (left) to 1 (right)
#
# This starter steers toward the next waypoint:
heading = get_heading()
target = get_angle_to_next()
diff = get_angle_diff(heading, target)
steer = diff / 45
if steer > 1:
    steer = 1
if steer < -1:
    steer = -1
set_steering(steer)
speed = get_speed()
if speed < 4:
    set_throttle(1)
if speed >= 4:
    set_throttle(0)
";

        public string CurrentSourceCode => _sourceCode;
        public System.Action OnCodeChanged;

        public void Initialize(RacerMatchManager match, RacerCar car, RacerTrack track,
                               string initialCode = null, string programName = "RacerAI")
        {
            _match = match;
            _car = car;
            _track = track;
            _compilerExt = new RacerCompilerExtension();

            _programName = programName;
            _sourceCode = initialCode ?? DEFAULT_CODE;
            _autoRun = true;

            LoadAndRun(_sourceCode);
        }

        protected override void Update()
        {
            if (_executor == null || _program == null || _isPaused) return;
            if (_match == null || !_match.MatchInProgress || _match.RaceFinished) return;

            float timeScale = SimulationTime.Instance?.timeScale ?? 1f;
            if (SimulationTime.Instance != null && SimulationTime.Instance.isPaused) return;

            float simDelta = UnityEngine.Time.deltaTime * timeScale;
            _opAccumulator += simDelta * OPS_PER_SECOND;

            int opsToRun = (int)_opAccumulator;
            _opAccumulator -= opsToRun;

            for (int i = 0; i < opsToRun; i++)
            {
                if (_executor.State.IsHalted)
                {
                    _executor.State.PC = 0;
                    _executor.State.IsHalted = false;
                }
                _executor.ExecuteOne();
            }

            if (opsToRun > 0)
                ProcessEvents();
        }

        protected override IGameIOHandler CreateIOHandler()
        {
            _ioHandler = new RacerIOHandler(_match, _car, _track);
            return _ioHandler;
        }

        protected override CompiledProgram CompileSource(string source, string name)
        {
            return PythonCompiler.Compile(source, name, _compilerExt);
        }

        protected override void ProcessEvents()
        {
            if (_executor?.State == null) return;
            while (_executor.State.OutputEvents.Count > 0)
                _executor.State.OutputEvents.Dequeue();
        }

        public void UploadCode(string newSource)
        {
            _sourceCode = newSource;
            _opAccumulator = 0;
            LoadAndRun(_sourceCode);
            OnCodeChanged?.Invoke();
        }

        public void ResetExecution()
        {
            _opAccumulator = 0;
            LoadAndRun(_sourceCode);
        }
    }
}
