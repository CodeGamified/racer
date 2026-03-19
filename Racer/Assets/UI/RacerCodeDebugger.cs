// Copyright CodeGamified 2025-2026
// MIT License — Racer
using CodeGamified.TUI;
using Racer.Scripting;

namespace Racer.UI
{
    /// <summary>
    /// Thin adapter — wires a RacerProgram into the engine's CodeDebuggerWindow
    /// via RacerDebuggerData (IDebuggerDataSource).
    /// </summary>
    public class RacerCodeDebugger : CodeDebuggerWindow
    {
        protected override void Awake()
        {
            base.Awake();
            windowTitle = "CODE";
        }

        public void Bind(RacerProgram program)
        {
            SetDataSource(new RacerDebuggerData(program));
        }
    }
}
