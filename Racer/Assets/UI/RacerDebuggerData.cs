// Copyright CodeGamified 2025-2026
// MIT License — Racer
using System.Collections.Generic;
using UnityEngine;
using CodeGamified.Engine;
using CodeGamified.Engine.Runtime;
using CodeGamified.TUI;
using Racer.Scripting;

namespace Racer.UI
{
    /// <summary>
    /// Adapts a RacerProgram into the engine's IDebuggerDataSource contract.
    /// </summary>
    public class RacerDebuggerData : IDebuggerDataSource
    {
        private readonly RacerProgram _program;
        private readonly string _label;

        public RacerDebuggerData(RacerProgram program, string label = null)
        {
            _program = program;
            _label = label;
        }

        public string ProgramName => _label ?? _program?.ProgramName ?? "RacerAI";
        public string[] SourceLines => _program?.Program?.SourceLines;
        public bool HasLiveProgram =>
            _program != null && _program.Executor != null && _program.Program != null
            && _program.Program.Instructions != null && _program.Program.Instructions.Length > 0;
        public int PC
        {
            get
            {
                var s = _program?.State;
                if (s == null) return 0;
                return s.LastExecutedPC >= 0 ? s.LastExecutedPC : s.PC;
            }
        }
        public long CycleCount => _program?.State?.CycleCount ?? 0;

        public string StatusString
        {
            get
            {
                if (_program == null || _program.Executor == null)
                    return TUIColors.Dimmed("NO PROGRAM");
                var state = _program.State;
                if (state == null) return TUIColors.Dimmed("NO STATE");
                int instCount = _program.Program?.Instructions?.Length ?? 0;
                return TUIColors.Fg(TUIColors.BrightGreen, $"TICK {instCount} inst");
            }
        }

        public List<string> BuildSourceLines(int pc, int scrollOffset, int maxRows)
        {
            var lines = new List<string>();
            var src = SourceLines;
            if (src == null) return lines;

            int activeLine = -1;
            int activeEnd = -1;
            bool isHalt = false;
            Instruction activeInst = default;
            if (HasLiveProgram && _program.Program.Instructions.Length > 0
                && pc < _program.Program.Instructions.Length)
            {
                activeInst = _program.Program.Instructions[pc];
                activeLine = activeInst.SourceLine - 1;
                isHalt = activeInst.Op == OpCode.HALT;
                if (activeLine >= 0)
                    activeEnd = SourceHighlight.GetContinuationEnd(src, activeLine);
            }

            if (scrollOffset == 0 && lines.Count < maxRows)
            {
                string whileLine = "while True:";
                if (isHalt)
                    lines.Add(TUIColors.Fg(TUIColors.BrightGreen, $"  {TUIGlyphs.ArrowR}   {whileLine}"));
                else
                    lines.Add($"  {TUIColors.Dimmed(TUIGlyphs.ArrowR)}   {SynthwaveHighlighter.Highlight(whileLine)}");
            }

            int tokenLine = -1;
            if (activeLine >= 0)
            {
                string token = SourceHighlight.GetSourceToken(activeInst);
                if (token != null)
                {
                    for (int k = activeLine; k <= activeEnd; k++)
                    {
                        if (src[k].IndexOf(token) >= 0) { tokenLine = k; break; }
                    }
                }
                if (tokenLine < 0) tokenLine = activeLine;
            }

            // Auto-scroll to keep active source line visible
            int focusLine = tokenLine >= 0 ? tokenLine : activeLine;
            if (focusLine >= 0 && src.Length > maxRows)
                scrollOffset = Mathf.Clamp(focusLine - maxRows / 3, 0, src.Length - maxRows);

            for (int i = scrollOffset; i < src.Length && lines.Count < maxRows; i++)
            {
                if (i == tokenLine)
                {
                    lines.Add(SourceHighlight.HighlightActiveLine(
                        src[i], $" {i + 1:D3}      ", activeInst));
                }
                else
                {
                    string num = TUIColors.Dimmed($"{i + 1:D3}");
                    lines.Add($" {num}      {SynthwaveHighlighter.Highlight(src[i])}");
                }
            }
            return lines;
        }

        public List<string> BuildMachineLines(int pc, int maxRows)
        {
            var lines = new List<string>();
            if (!HasLiveProgram) return lines;

            var instructions = _program.Program.Instructions;
            int total = instructions.Length;

            int offset = 0;
            if (total > maxRows)
                offset = Mathf.Clamp(pc - maxRows / 3, 0, total - maxRows);
            int visibleCount = Mathf.Min(maxRows, total);

            for (int j = 0; j < visibleCount; j++)
            {
                int i = offset + j;
                var inst = instructions[i];
                bool isPC = (i == pc);
                string asm = inst.ToAssembly(FormatRacerOp);
                if (isPC)
                {
                    lines.Add(TUIColors.Fg(TUIColors.BrightGreen, $" {i:X3}  {asm}"));
                }
                else
                {
                    string addr = TUIColors.Dimmed($"{i:X3}");
                    lines.Add($" {addr}  {SynthwaveHighlighter.HighlightAsm(asm)}");
                }
            }
            return lines;
        }

        public List<string> BuildStateLines()
        {
            if (!HasLiveProgram) return new List<string>();
            var s = _program.State;
            int displayPC = s.LastExecutedPC >= 0 ? s.LastExecutedPC : s.PC;
            return TUIWidgets.BuildStateLines(
                s.Registers, s.LastRegisterModified,
                s.Flags, displayPC, s.Stack.Count,
                s.NameToAddress, s.Memory);
        }

        static string FormatRacerOp(Instruction inst)
        {
            int id = (int)inst.Op - (int)OpCode.CUSTOM_0;
            return id switch
            {
                RacerOps.GET_POS_X         => "INP R0, POS.X",
                RacerOps.GET_POS_Z         => "INP R0, POS.Z",
                RacerOps.GET_SPEED         => "INP R0, SPEED",
                RacerOps.GET_HEADING       => "INP R0, HEAD",
                RacerOps.GET_VEL_X         => "INP R0, VEL.X",
                RacerOps.GET_VEL_Z         => "INP R0, VEL.Z",
                RacerOps.GET_ON_ROAD       => "INP R0, ROAD",
                RacerOps.GET_NEXT_WP       => "INP R0, NX.WP",
                RacerOps.GET_ANGLE_TO_NEXT => "INP R0, ANG.N",
                RacerOps.GET_DIST_TO_NEXT  => "INP R0, DST.N",
                RacerOps.GET_DIST_TO_TRACK => "INP R0, DST.T",
                RacerOps.GET_TRACK_WIDTH   => "INP R0, TRK.W",
                RacerOps.GET_WP_COUNT      => "INP R0, WP.CT",
                RacerOps.GET_WP_X          => "INP R0, WP.X",
                RacerOps.GET_WP_Z          => "INP R0, WP.Z",
                RacerOps.GET_PROGRESS      => "INP R0, PROG",
                RacerOps.GET_CURRENT_LAP   => "INP R0, LAP",
                RacerOps.GET_TOTAL_LAPS    => "INP R0, LAPS",
                RacerOps.GET_RACE_TIME     => "INP R0, R.TIM",
                RacerOps.GET_LAP_TIME      => "INP R0, L.TIM",
                RacerOps.GET_BEST_LAP      => "INP R0, B.LAP",
                RacerOps.GET_BEST_RACE     => "INP R0, B.RAC",
                RacerOps.GET_RACES_DONE    => "INP R0, RACES",
                RacerOps.GET_RACE_FINISHED => "INP R0, FIN",
                RacerOps.GET_INPUT         => "INP R0, INPUT",
                RacerOps.GET_ANGLE_DIFF    => "INP R0, A.DIF",
                RacerOps.SET_THROTTLE      => "OUT THRTL, R0",
                RacerOps.SET_STEERING      => "OUT STEER, R0",
                _                          => $"IO.{id,2} {inst.Arg0}, {inst.Arg1}"
            };
        }
    }
}
