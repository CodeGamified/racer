// Copyright CodeGamified 2025-2026
// MIT License — Racer
using System.Collections.Generic;
using CodeGamified.Engine;
using CodeGamified.Engine.Compiler;

namespace Racer.Scripting
{
    /// <summary>
    /// Racer opcodes mapped to CUSTOM_0..CUSTOM_N.
    /// 28 opcodes: 24 queries + 2 commands + 2 utility.
    ///
    /// AI surface: steering + throttle control, waypoint navigation,
    /// speed management (brake before turns), path planning.
    ///
    /// Angles in degrees. Positions in world units.
    /// </summary>
    public static class RacerOps
    {
        // ── Car state → R0 ──
        public const int GET_POS_X           = 0;   // car X position
        public const int GET_POS_Z           = 1;   // car Z position
        public const int GET_SPEED           = 2;   // current speed (units/sec)
        public const int GET_HEADING         = 3;   // car heading (degrees, 0=+Z, 90=+X)
        public const int GET_VEL_X           = 4;   // velocity X component
        public const int GET_VEL_Z           = 5;   // velocity Z component
        public const int GET_ON_ROAD         = 6;   // 1 if on road, 0 if off-road

        // ── Track/waypoint queries → R0 ──
        public const int GET_NEXT_WP         = 7;   // index of next waypoint to hit
        public const int GET_ANGLE_TO_NEXT   = 8;   // angle (deg) from car to next waypoint
        public const int GET_DIST_TO_NEXT    = 9;   // distance to next waypoint
        public const int GET_DIST_TO_TRACK   = 10;  // distance from car to track center
        public const int GET_TRACK_WIDTH     = 11;  // road width
        public const int GET_WP_COUNT        = 12;  // total waypoints in track loop
        public const int GET_WP_X            = 13;  // R0=index → waypoint X
        public const int GET_WP_Z            = 14;  // R0=index → waypoint Z
        public const int GET_PROGRESS        = 15;  // 0..100 around current lap (percent)

        // ── Race state → R0 ──
        public const int GET_CURRENT_LAP     = 16;
        public const int GET_TOTAL_LAPS      = 17;
        public const int GET_RACE_TIME       = 18;  // total time (seconds × 100 for precision)
        public const int GET_LAP_TIME        = 19;  // current lap time × 100
        public const int GET_BEST_LAP        = 20;  // best lap time × 100
        public const int GET_BEST_RACE       = 21;  // best race time × 100
        public const int GET_RACES_DONE      = 22;  // races completed
        public const int GET_RACE_FINISHED   = 23;  // 1 if finished

        // ── Input ──
        public const int GET_INPUT           = 24;

        // ── Utility ──
        public const int GET_ANGLE_DIFF      = 25;  // R0=heading, R1=target → signed delta

        // ── Commands ──
        public const int SET_THROTTLE        = 26;  // R0 = -1..1
        public const int SET_STEERING        = 27;  // R0 = -1..1

        public const int COUNT = 28;
    }

    /// <summary>
    /// Compiler extension — one case per builtin function.
    /// </summary>
    public class RacerCompilerExtension : ICompilerExtension
    {
        public void RegisterBuiltins(CompilerContext ctx) { }

        public bool TryCompileCall(string functionName, List<AstNodes.ExprNode> args,
                                   CompilerContext ctx, int sourceLine)
        {
            switch (functionName)
            {
                // ── Car state (no args) ──
                case "get_pos_x":
                    Emit(ctx, RacerOps.GET_POS_X, sourceLine, "get_pos_x → R0");
                    return true;
                case "get_pos_z":
                    Emit(ctx, RacerOps.GET_POS_Z, sourceLine, "get_pos_z → R0");
                    return true;
                case "get_speed":
                    Emit(ctx, RacerOps.GET_SPEED, sourceLine, "get_speed → R0");
                    return true;
                case "get_heading":
                    Emit(ctx, RacerOps.GET_HEADING, sourceLine, "get_heading → R0");
                    return true;
                case "get_vel_x":
                    Emit(ctx, RacerOps.GET_VEL_X, sourceLine, "get_vel_x → R0");
                    return true;
                case "get_vel_z":
                    Emit(ctx, RacerOps.GET_VEL_Z, sourceLine, "get_vel_z → R0");
                    return true;
                case "get_on_road":
                    Emit(ctx, RacerOps.GET_ON_ROAD, sourceLine, "get_on_road → R0");
                    return true;

                // ── Track/waypoint (no args unless noted) ──
                case "get_next_wp":
                    Emit(ctx, RacerOps.GET_NEXT_WP, sourceLine, "get_next_wp → R0");
                    return true;
                case "get_angle_to_next":
                    Emit(ctx, RacerOps.GET_ANGLE_TO_NEXT, sourceLine, "get_angle_to_next → R0");
                    return true;
                case "get_dist_to_next":
                    Emit(ctx, RacerOps.GET_DIST_TO_NEXT, sourceLine, "get_dist_to_next → R0");
                    return true;
                case "get_dist_to_track":
                    Emit(ctx, RacerOps.GET_DIST_TO_TRACK, sourceLine, "get_dist_to_track → R0");
                    return true;
                case "get_track_width":
                    Emit(ctx, RacerOps.GET_TRACK_WIDTH, sourceLine, "get_track_width → R0");
                    return true;
                case "get_wp_count":
                    Emit(ctx, RacerOps.GET_WP_COUNT, sourceLine, "get_wp_count → R0");
                    return true;
                case "get_wp_x":
                    CompileOneArg(args, ctx);
                    Emit(ctx, RacerOps.GET_WP_X, sourceLine, "get_wp_x(R0=idx) → R0");
                    return true;
                case "get_wp_z":
                    CompileOneArg(args, ctx);
                    Emit(ctx, RacerOps.GET_WP_Z, sourceLine, "get_wp_z(R0=idx) → R0");
                    return true;
                case "get_progress":
                    Emit(ctx, RacerOps.GET_PROGRESS, sourceLine, "get_progress → R0");
                    return true;

                // ── Race state (no args) ──
                case "get_current_lap":
                    Emit(ctx, RacerOps.GET_CURRENT_LAP, sourceLine, "get_current_lap → R0");
                    return true;
                case "get_total_laps":
                    Emit(ctx, RacerOps.GET_TOTAL_LAPS, sourceLine, "get_total_laps → R0");
                    return true;
                case "get_race_time":
                    Emit(ctx, RacerOps.GET_RACE_TIME, sourceLine, "get_race_time → R0");
                    return true;
                case "get_lap_time":
                    Emit(ctx, RacerOps.GET_LAP_TIME, sourceLine, "get_lap_time → R0");
                    return true;
                case "get_best_lap":
                    Emit(ctx, RacerOps.GET_BEST_LAP, sourceLine, "get_best_lap → R0");
                    return true;
                case "get_best_race":
                    Emit(ctx, RacerOps.GET_BEST_RACE, sourceLine, "get_best_race → R0");
                    return true;
                case "get_races_done":
                    Emit(ctx, RacerOps.GET_RACES_DONE, sourceLine, "get_races_done → R0");
                    return true;
                case "get_race_finished":
                    Emit(ctx, RacerOps.GET_RACE_FINISHED, sourceLine, "get_race_finished → R0");
                    return true;

                // ── Input ──
                case "get_input":
                    Emit(ctx, RacerOps.GET_INPUT, sourceLine, "get_input → R0");
                    return true;

                // ── Utility (2 args) ──
                case "get_angle_diff":
                    CompileTwoArgs(args, ctx);
                    Emit(ctx, RacerOps.GET_ANGLE_DIFF, sourceLine, "get_angle_diff(R0=heading,R1=target) → R0");
                    return true;

                // ── Commands (1 arg) ──
                case "set_throttle":
                    CompileOneArg(args, ctx);
                    Emit(ctx, RacerOps.SET_THROTTLE, sourceLine, "set_throttle(R0)");
                    return true;
                case "set_steering":
                    CompileOneArg(args, ctx);
                    Emit(ctx, RacerOps.SET_STEERING, sourceLine, "set_steering(R0)");
                    return true;

                default: return false;
            }
        }

        public bool TryCompileMethodCall(string typeName, string methodName,
                                         List<AstNodes.ExprNode> args,
                                         CompilerContext ctx, int sourceLine) => false;

        public bool TryCompileObjectDecl(string typeName, string varName,
                                         List<AstNodes.ExprNode> args,
                                         CompilerContext ctx, int sourceLine) => false;

        // ─── helpers ───

        private static void CompileOneArg(List<AstNodes.ExprNode> args, CompilerContext ctx)
        {
            if (args != null && args.Count > 0)
                args[0].Compile(ctx);
        }

        private static void CompileTwoArgs(List<AstNodes.ExprNode> args, CompilerContext ctx)
        {
            if (args != null && args.Count >= 2)
            {
                args[0].Compile(ctx);              // arg0 → R0
                ctx.Emit(OpCode.PUSH, 0);          // save R0
                args[1].Compile(ctx);              // arg1 → R0
                ctx.Emit(OpCode.MOV, 1, 0);        // R0 → R1
                ctx.Emit(OpCode.POP, 0);           // restore arg0 → R0
            }
        }

        private static void Emit(CompilerContext ctx, int opIndex, int sourceLine, string comment)
        {
            ctx.Emit(OpCode.CUSTOM_0 + opIndex, 0, 0, 0, sourceLine, comment);
        }
    }
}
