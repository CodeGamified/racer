// Copyright CodeGamified 2025-2026
// MIT License — Racer
using CodeGamified.Time;

namespace Racer.Core
{
    /// <summary>
    /// Simulation time for Racer — real-time racing, 100x fast-forward for AI testing.
    /// </summary>
    public class RacerSimulationTime : SimulationTime
    {
        protected override float MaxTimeScale => 100f;

        protected override void OnInitialize()
        {
            timeScalePresets = new[] { 0f, 0.25f, 0.5f, 1f, 2f, 5f, 10f, 50f, 100f };
            currentPresetIndex = 3; // 1x
        }

        public override string GetFormattedTime()
        {
            int m = (int)(simulationTime / 60.0);
            int s = (int)(simulationTime % 60.0);
            int ms = (int)((simulationTime % 1.0) * 100);
            return $"{m:D2}:{s:D2}.{ms:D2}";
        }
    }
}
