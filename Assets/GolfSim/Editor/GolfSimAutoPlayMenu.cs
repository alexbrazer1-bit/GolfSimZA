using System;
using GolfSimZA.Core;
using GolfSimZA.Courses;
using GolfSimZA.Players;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GolfSimZA.EditorTools
{
    /// <summary>
    /// GolfSimZA → Test: plays a whole round automatically in the editor (see RoundAutoTester) and
    /// writes autoplay-report.txt next to settings.json.
    /// </summary>
    [InitializeOnLoad]
    internal static class GolfSimAutoPlayMenu
    {
        private const string QueueKey = "GolfSimZA.AutoPlayQueue";

        static GolfSimAutoPlayMenu()
        {
            // "Run all": when one round ends (Play mode stops), start the next one.
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode) return;
                string queue = EditorPrefs.GetString(QueueKey, "");
                if (string.IsNullOrEmpty(queue)) return;
                string[] items = queue.Split(';');
                EditorPrefs.SetString(QueueKey, string.Join(";", items, 1, items.Length - 1));
                EditorApplication.delayCall += () => RunItem(items[0]);
            };
        }

        [MenuItem("GolfSimZA/Test/Auto-play ALL five test rounds (one after the other)")]
        private static void RunAll()
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.persistentDataPath, "autoplay-all.txt"), "");
            EditorPrefs.SetString(QueueKey, "Greenville,Scramble,4,0-0-1-1;Auburn,Greensomes,4,0-0-1-1;Aliante,Shamble,4,0-0-1-1;Apostle,Stableford,3,");
            RunItem("A-Ga-Ming,Stroke Play,2,");
        }

        private static void RunItem(string item)
        {
            string[] f = item.Split(',');
            int[] teams = null;
            if (f.Length > 3 && f[3].Length > 0) teams = Array.ConvertAll(f[3].Split('-'), int.Parse);
            Run(f[0], f[1], int.Parse(f[2]), teams);
        }

        [MenuItem("GolfSimZA/Test/Auto-play 18 holes - A-Ga-Ming, 2 players, Stroke Play")]
        private static void StrokeAGaMing() => Run("A-Ga-Ming", "Stroke Play", 2, null);

        [MenuItem("GolfSimZA/Test/Auto-play 18 holes - 3s Greenville, 4 players, Scramble")]
        private static void Scramble() => Run("Greenville", "Scramble", 4, new[] { 0, 0, 1, 1 });

        [MenuItem("GolfSimZA/Test/Auto-play 18 holes - Auburn, 4 players, Greensomes")]
        private static void Greensomes() => Run("Auburn", "Greensomes", 4, new[] { 0, 0, 1, 1 });

        [MenuItem("GolfSimZA/Test/Auto-play 18 holes - Aliante, 4 players, Shamble")]
        private static void Shamble() => Run("Aliante", "Shamble", 4, new[] { 0, 0, 1, 1 });

        [MenuItem("GolfSimZA/Test/Auto-play 18 holes - Apostle Highland, 3 players, Stableford")]
        private static void Stableford() => Run("Apostle", "Stableford", 3, null);

        /// <summary>Also callable from the command line / other tools: course name part, format, player count, teams.</summary>
        public static void Run(string coursePart, string format, int playerCount, int[] teams)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[GolfSimZA] Stop Play mode first."); return; }
            CourseLibrary.Reload();
            CourseDefinition course = null;
            foreach (CourseDefinition c in CourseLibrary.All)
                if (c.name.IndexOf(coursePart, StringComparison.OrdinalIgnoreCase) >= 0) { course = c; break; }
            if (course == null) { Debug.LogError("[GolfSimZA] Auto-play: no imported course matching '" + coursePart + "'."); return; }

            PlayerRoster roster = PlayerRoster.Current;
            while (roster.players.Count < playerCount) roster.Add("Tester " + (roster.players.Count + 1));
            var names = new string[playerCount];
            for (int i = 0; i < playerCount; i++) names[i] = roster.players[i].name;

            string[] tees = course.tees != null && course.tees.Length > 0 ? course.tees : CourseSession.DemoTees;
            string tee = tees.Length > 2 ? tees[tees.Length / 2] : tees[0];
            int holes = course.HoleCount;
            CourseSession.PracticeMode = false;
            CourseSession.SetSession(course.name, tee, holes);
            CourseSession.SetCourse(course.id, tees);
            CourseSession.SetRoundSettings(format, tee, "Standard", "1 m", "Unlimited", false, holes);
            CourseSession.SetHoles(holes, 0, holes);
            CourseSession.SetPlayers(names);
            CourseSession.Teams = teams;

            AppSettings s = AppSettings.Current;
            s.autoPutt = true;
            s.Save();

            PlayerPrefs.SetInt("GolfSimZA.AutoPlay", 1);
            PlayerPrefs.Save();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(GolfSimZA.Editor.GolfSimBuildScenes.PlayRound);
                EditorApplication.isPlaying = true;
            }
        }
    }
}
