using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace GolfSimZA.Courses
{
    /// <summary>
    /// The round in progress, saved after every completed hole so it can be resumed later
    /// ("RESUME ROUND" in Round Settings). One saved round is kept; finishing a round clears it.
    /// </summary>
    public sealed class RoundSave
    {
        private const string Key = "GolfSimZA.SavedRound";

        public string CourseKey;
        public string CourseName = "";
        public string[] Players = Array.Empty<string>();
        public int NextHole;
        public int RoundLength;
        public List<int[]> Scores = new List<int[]>();
        public int[] MulligansUsed = Array.Empty<int>();

        /// <summary>Key of the course currently selected in the session.</summary>
        public static string CurrentCourseKey => CourseSession.IsImportedCourse ? "id:" + CourseSession.CourseId : "name:" + CourseSession.CourseName;

        public static bool Exists => !string.IsNullOrEmpty(PlayerPrefs.GetString(Key, ""));

        public static RoundSave Load()
        {
            string text = PlayerPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(text)) return null;
            try
            {
                string[] lines = text.Split('\n');
                if (lines.Length < 6) return null;
                var save = new RoundSave
                {
                    CourseKey = lines[0],
                    Players = lines[1].Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries),
                    NextHole = int.Parse(lines[2], CultureInfo.InvariantCulture),
                    RoundLength = int.Parse(lines[3], CultureInfo.InvariantCulture),
                    MulligansUsed = ParseInts(lines[5]),
                    CourseName = lines.Length > 6 ? lines[6] : ""
                };
                foreach (string hole in lines[4].Split(new[] { ';' }, StringSplitOptions.None))
                    save.Scores.Add(string.IsNullOrEmpty(hole) ? null : ParseInts(hole));
                return save;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GolfSimZA] Saved round could not be read and was discarded: " + ex.Message);
                Clear();
                return null;
            }
        }

        /// <summary>The saved round when it belongs to the selected course and the same players; otherwise null.</summary>
        public static RoundSave LoadFor(string[] players)
        {
            RoundSave save = Load();
            if (save == null || save.CourseKey != CurrentCourseKey) return null;
            if (players == null || save.Players.Length != players.Length) return null;
            for (int i = 0; i < players.Length; i++)
                if (!string.Equals(save.Players[i], players[i], StringComparison.OrdinalIgnoreCase)) return null;
            return save;
        }

        public void Save()
        {
            var holes = new List<string>();
            foreach (int[] s in Scores) holes.Add(s == null ? "" : JoinInts(s));
            string text = string.Join("\n", new[]
            {
                CourseKey ?? "",
                string.Join("|", Players ?? Array.Empty<string>()),
                NextHole.ToString(CultureInfo.InvariantCulture),
                RoundLength.ToString(CultureInfo.InvariantCulture),
                string.Join(";", holes),
                JoinInts(MulligansUsed ?? Array.Empty<int>()),
                (CourseName ?? "").Replace("\n", " ")
            });
            PlayerPrefs.SetString(Key, text);
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        /// <summary>Short description for the Round Settings screen.</summary>
        public static string Describe()
        {
            RoundSave save = Load();
            if (save == null) return "No saved round";
            string course = !string.IsNullOrEmpty(save.CourseName) ? save.CourseName : "saved course";
            return "Saved: " + course + ", next hole " + (save.NextHole + 1) + " (" + string.Join(", ", save.Players) + ")";
        }

        private static int[] ParseInts(string text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<int>();
            string[] parts = text.Split(',');
            var values = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++) values[i] = int.Parse(parts[i], CultureInfo.InvariantCulture);
            return values;
        }

        private static string JoinInts(int[] values)
        {
            var parts = new string[values.Length];
            for (int i = 0; i < values.Length; i++) parts[i] = values[i].ToString(CultureInfo.InvariantCulture);
            return string.Join(",", parts);
        }
    }
}
