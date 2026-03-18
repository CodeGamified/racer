// Copyright CodeGamified 2025-2026
// MIT License — Racer
using System.Collections.Generic;
using CodeGamified.Editor;

namespace Racer.Scripting
{
    /// <summary>
    /// Editor extension — tap-to-code metadata for Racer builtins.
    /// </summary>
    public class RacerEditorExtension : IEditorExtension
    {
        public List<EditorTypeInfo> GetAvailableTypes() => new();

        public List<EditorFuncInfo> GetAvailableFunctions() => new()
        {
            // Car state
            new EditorFuncInfo { Name = "get_pos_x",          Hint = "car X position",                ArgCount = 0 },
            new EditorFuncInfo { Name = "get_pos_z",          Hint = "car Z position",                ArgCount = 0 },
            new EditorFuncInfo { Name = "get_speed",          Hint = "current speed (units/sec)",     ArgCount = 0 },
            new EditorFuncInfo { Name = "get_heading",        Hint = "heading (deg, 0=+Z, 90=+X)",   ArgCount = 0 },
            new EditorFuncInfo { Name = "get_vel_x",          Hint = "velocity X component",         ArgCount = 0 },
            new EditorFuncInfo { Name = "get_vel_z",          Hint = "velocity Z component",         ArgCount = 0 },
            new EditorFuncInfo { Name = "get_on_road",        Hint = "1 if on road",                 ArgCount = 0 },

            // Track / waypoints
            new EditorFuncInfo { Name = "get_next_wp",        Hint = "next waypoint index",          ArgCount = 0 },
            new EditorFuncInfo { Name = "get_angle_to_next",  Hint = "angle (deg) to next wp",       ArgCount = 0 },
            new EditorFuncInfo { Name = "get_dist_to_next",   Hint = "distance to next wp",          ArgCount = 0 },
            new EditorFuncInfo { Name = "get_dist_to_track",  Hint = "dist from track center",       ArgCount = 0 },
            new EditorFuncInfo { Name = "get_track_width",    Hint = "road width",                   ArgCount = 0 },
            new EditorFuncInfo { Name = "get_wp_count",       Hint = "total waypoints",              ArgCount = 0 },
            new EditorFuncInfo { Name = "get_wp_x",           Hint = "waypoint i X position",        ArgCount = 1 },
            new EditorFuncInfo { Name = "get_wp_z",           Hint = "waypoint i Z position",        ArgCount = 1 },
            new EditorFuncInfo { Name = "get_progress",       Hint = "0-100 around lap",             ArgCount = 0 },

            // Race state
            new EditorFuncInfo { Name = "get_current_lap",    Hint = "current lap (1-based)",        ArgCount = 0 },
            new EditorFuncInfo { Name = "get_total_laps",     Hint = "laps to complete",             ArgCount = 0 },
            new EditorFuncInfo { Name = "get_race_time",      Hint = "total time × 100",             ArgCount = 0 },
            new EditorFuncInfo { Name = "get_lap_time",       Hint = "current lap time × 100",       ArgCount = 0 },
            new EditorFuncInfo { Name = "get_best_lap",       Hint = "best lap time × 100",          ArgCount = 0 },
            new EditorFuncInfo { Name = "get_best_race",      Hint = "best race time × 100",         ArgCount = 0 },
            new EditorFuncInfo { Name = "get_races_done",     Hint = "completed races",              ArgCount = 0 },
            new EditorFuncInfo { Name = "get_race_finished",  Hint = "1 if race done",               ArgCount = 0 },

            // Input
            new EditorFuncInfo { Name = "get_input",          Hint = "keyboard input",               ArgCount = 0 },

            // Utility
            new EditorFuncInfo { Name = "get_angle_diff",     Hint = "signed angle delta (a, b)",    ArgCount = 2 },

            // Commands
            new EditorFuncInfo { Name = "set_throttle",       Hint = "-1 (reverse) to 1 (full gas)", ArgCount = 1 },
            new EditorFuncInfo { Name = "set_steering",       Hint = "-1 (left) to 1 (right)",       ArgCount = 1 },
        };

        public List<EditorMethodInfo> GetMethodsForType(string typeName) => new();

        public List<string> GetVariableNameSuggestions() => new()
        {
            "heading", "target", "diff", "steer", "speed",
            "throttle", "dist", "lap", "on_road", "angle",
            "wp_x", "wp_z", "next_wp"
        };

        public List<string> GetStringLiteralSuggestions() => new();
    }
}
