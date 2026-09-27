#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace GolfSimZA.Editor
{
    /// <summary>
    /// One place that sets the build scene list. Every "Create ..." menu used to overwrite
    /// the list with a partial set (e.g. 1.0 dropped Round Settings and Players, so the
    /// menu flow failed in Play mode). Now every menu calls Apply(), which keeps the full
    /// flow in order: Course Selection (start) → Round Settings → Players → Play Round.
    /// </summary>
    public static class GolfSimBuildScenes
    {
        public const string CourseSelection = "Assets/Scenes/GolfSimZA_0_5_CourseSelection.unity";
        public const string RoundSettings = "Assets/Scenes/GolfSimZA_0_5_RoundSettings.unity";
        public const string Players = "Assets/Scenes/GolfSimZA_0_5_Players.unity";
        public const string PlayRound = "Assets/Scenes/GolfSimZA_0_6_PlayRound.unity";
        public const string PresentationRange = "Assets/Scenes/GolfSimZA_0_4_SimulatorPresentationRange.unity";

        public static readonly string[] Ordered = { CourseSelection, RoundSettings, Players, PlayRound, PresentationRange };
        public static readonly string[] Required = { CourseSelection, RoundSettings, Players, PlayRound };

        public static void Apply()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (string path in Ordered)
                if (File.Exists(path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        public static List<string> Missing()
        {
            var missing = new List<string>();
            foreach (string path in Required)
                if (!File.Exists(path)) missing.Add(path);
            return missing;
        }
    }
}
#endif
