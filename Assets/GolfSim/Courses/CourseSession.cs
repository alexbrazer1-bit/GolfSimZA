using UnityEngine;

namespace GolfSimZA.Courses
{
    public static class CourseSession
    {
        private const string CourseKey = "GolfSimZA.CourseName";
        private const string TeeKey = "GolfSimZA.TeeName";
        private const string RoundKey = "GolfSimZA.RoundLength";
        private const string ModeKey = "GolfSimZA.GameMode";
        private const string PinsKey = "GolfSimZA.PinSetting";
        private const string GimmieKey = "GolfSimZA.GimmieSetting";
        private const string MulliganKey = "GolfSimZA.MulliganSetting";
        private const string ResumeKey = "GolfSimZA.ResumeRound";
        private const string PlayersKey = "GolfSimZA.PlayerNames";
        private const string CourseIdKey = "GolfSimZA.CourseId";
        private const string CourseTeesKey = "GolfSimZA.CourseTees";
        private const string PracticeKey = "GolfSimZA.PracticeMode";
        private const string StartHoleKey = "GolfSimZA.RoundStartHole";
        private const string CourseHolesKey = "GolfSimZA.CourseHoles";
        private const string TeamsKey = "GolfSimZA.Teams";

        // The round screen reads these settings many times every frame. PlayerPrefs is the Windows
        // registry, so each read is slow; this class is the only one that writes these keys, so the
        // values are kept in memory and the registry is only read once per key and written on change.
        private static readonly System.Collections.Generic.Dictionary<string, int> IntCache = new System.Collections.Generic.Dictionary<string, int>();
        private static readonly System.Collections.Generic.Dictionary<string, string> StringCache = new System.Collections.Generic.Dictionary<string, string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearCache()
        {
            IntCache.Clear();
            StringCache.Clear();
        }

        private static int GetInt(string key, int fallback)
        {
            if (!IntCache.TryGetValue(key, out int value))
            {
                value = PlayerPrefs.GetInt(key, fallback);
                IntCache[key] = value; // a missing key keeps its fallback until it is set
            }
            return value;
        }

        private static string GetString(string key, string fallback)
        {
            if (!StringCache.TryGetValue(key, out string value))
            {
                value = PlayerPrefs.GetString(key, fallback);
                StringCache[key] = value;
            }
            return value;
        }

        private static void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            IntCache[key] = value;
        }

        private static void SetString(string key, string value)
        {
            value = value ?? "";
            PlayerPrefs.SetString(key, value);
            StringCache[key] = value;
        }

        private static void DeleteKey(string key)
        {
            PlayerPrefs.DeleteKey(key);
            IntCache.Remove(key);
            StringCache.Remove(key);
        }

        /// <summary>A round is for up to this many players (every screen and the round itself enforce it).</summary>
        public const int MaxPlayers = 8;

        /// <summary>Team (0 = A .. 3 = D) of each round player, in the order of PlayerNames. Empty when not set.</summary>
        public static int[] Teams
        {
            get
            {
                string saved = GetString(TeamsKey, "");
                if (string.IsNullOrEmpty(saved)) return new int[0];
                string[] parts = saved.Split('|');
                var teams = new int[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                    teams[i] = int.TryParse(parts[i], out int t) ? Mathf.Clamp(t, 0, GameFormats.MaxTeams - 1) : 0;
                return teams;
            }
            set
            {
                if (value == null || value.Length == 0) { DeleteKey(TeamsKey); PlayerPrefs.Save(); return; }
                var parts = new string[value.Length];
                for (int i = 0; i < value.Length; i++) parts[i] = Mathf.Clamp(value[i], 0, GameFormats.MaxTeams - 1).ToString();
                SetString(TeamsKey, string.Join("|", parts));
                PlayerPrefs.Save();
            }
        }

        /// <summary>First hole of the round, 0-based (9 = back nine).</summary>
        public static int RoundStartHole => Mathf.Max(0, GetInt(StartHoleKey, 0));

        /// <summary>Number of holes the selected course has.</summary>
        public static int CourseHoles => Mathf.Clamp(GetInt(CourseHolesKey, 18), 1, 18);

        public static void SetHoles(int courseHoles, int startHole, int roundLength)
        {
            SetInt(CourseHolesKey, Mathf.Clamp(courseHoles, 1, 18));
            SetInt(StartHoleKey, Mathf.Max(0, startHole));
            SetInt(RoundKey, Mathf.Clamp(roundLength, 1, 18));
            PlayerPrefs.Save();
        }

        /// <summary>On-course practice: pick any hole, no scoring.</summary>
        public static bool PracticeMode
        {
            get => GetInt(PracticeKey, 0) == 1;
            set { SetInt(PracticeKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Tees used by the built-in demo courses.</summary>
        public static readonly string[] DemoTees = { "Red", "White", "Blue", "Black" };

        public static string CourseName => GetString(CourseKey, "GolfSim ZA Practice Range");
        public static string TeeName => GetString(TeeKey, "Blue");
        public static int RoundLength => GetInt(RoundKey, 18);
        public static string GameMode => GetString(ModeKey, "Stroke Play");
        public static string PinSetting => GetString(PinsKey, "Standard");
        public static string GimmieSetting => GetString(GimmieKey, "1 m");
        public static string MulliganSetting => GetString(MulliganKey, "Unlimited");
        public static bool ResumeRound => GetInt(ResumeKey, 0) == 1;
        public static string PlayerNames => GetString(PlayersKey, "Player 1");

        /// <summary>Id of an imported course in the CourseLibrary, or empty for a demo course / range.</summary>
        public static string CourseId => GetString(CourseIdKey, "");
        public static bool IsImportedCourse => !string.IsNullOrEmpty(CourseId);

        /// <summary>Tee names available on the selected course.</summary>
        public static string[] AvailableTees
        {
            get
            {
                string saved = GetString(CourseTeesKey, "");
                string[] tees = saved.Split(new[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries);
                return tees.Length > 0 ? tees : DemoTees;
            }
        }

        public static void SetCourse(string courseId, string[] availableTees)
        {
            SetString(CourseIdKey, courseId ?? "");
            SetString(CourseTeesKey, availableTees != null && availableTees.Length > 0 ? string.Join("|", availableTees) : "");
            PlayerPrefs.Save();
        }

        public static void SetSession(string courseName, string teeName, int roundLength)
        {
            SetString(CourseKey, courseName);
            SetString(TeeKey, teeName);
            SetInt(RoundKey, roundLength);
            PlayerPrefs.Save();
        }

        public static void SetRoundSettings(string gameMode, string teeName, string pins, string gimmie, string mulligan, bool resumeRound, int holes)
        {
            SetString(ModeKey, gameMode);
            SetString(TeeKey, teeName);
            SetString(PinsKey, pins);
            SetString(GimmieKey, gimmie);
            SetString(MulliganKey, mulligan);
            SetInt(ResumeKey, resumeRound ? 1 : 0);
            SetInt(RoundKey, holes);
            PlayerPrefs.Save();
        }

        public static void SetPlayers(string[] names)
        {
            if (names != null && names.Length > MaxPlayers) System.Array.Resize(ref names, MaxPlayers);
            SetString(PlayersKey, string.Join("|", names ?? new string[0]));
            PlayerPrefs.Save();
        }
    }
}
