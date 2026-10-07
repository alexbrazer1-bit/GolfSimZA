using System.Collections.Generic;
using Unity.Profiling;

namespace GolfSimZA
{
    /// <summary>
    /// Named profiler sections for the on-screen interface (shown in Unity's Profiler and read by the
    /// automatic round test), so the cost of each panel can be measured. Names start "GolfSimZA.".
    /// </summary>
    public static class GuiMarkers
    {
        public const string Prefix = "GolfSimZA.";
        private static readonly Dictionary<string, ProfilerMarker> Markers = new Dictionary<string, ProfilerMarker>();

        /// <summary>Every section the interface uses (created up front so a test can record them from the start).</summary>
        public static readonly string[] Names =
        {
            "RoundUI.GroundLabels", "RoundUI.DistanceBanner", "RoundUI.PlayerBanner", "RoundUI.CenterTop", "RoundUI.HoleCard",
            "RoundUI.ShotDataTiles", "RoundUI.MiniMap", "RoundUI.ClubBar", "RoundUI.ShotCompletion", "RoundUI.BallReady",
            "RoundUI.Banner", "RoundUI.ExpandedMap", "RoundUI.Scorecard", "GameMenu.OnGUI", "SimulatorHUD.OnGUI"
        };

        static GuiMarkers()
        {
            foreach (string n in Names) Get(n);
        }

        public static ProfilerMarker Get(string name)
        {
            if (!Markers.TryGetValue(name, out ProfilerMarker m))
            {
                m = new ProfilerMarker(ProfilerCategory.Gui, Prefix + name);
                Markers[name] = m;
            }
            return m;
        }
    }
}
