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

        /// <summary>A round is for up to this many players (every screen and the round itself enforce it).</summary>
        public const int MaxPlayers = 8;

        /// <summary>Team (0 = A .. 3 = D) of each round player, in the order of PlayerNames. Empty when not set.</summary>
        public static int[] Teams
        {
            get
            {
                string saved = PlayerPrefs.GetString(TeamsKey, "");
                if (string.IsNullOrEmpty(saved)) return new int[0];
                string[] parts = saved.Split('|');
                var teams = new int[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                    teams[i] = int.TryParse(parts[i], out int t) ? Mathf.Clamp(t, 0, GameFormats.MaxTeams - 1) : 0;
                return teams;
            }
            set
            {
                if (value == null || value.Length == 0) { PlayerPrefs.DeleteKey(TeamsKey); PlayerPrefs.Save(); return; }
                var parts = new string[value.Length];
                for (int i = 0; i < value.Length; i++) parts[i] = Mathf.Clamp(value[i], 0, GameFormats.MaxTeams - 1).ToString();
                PlayerPrefs.SetString(TeamsKey, string.Join("|", parts));
                PlayerPrefs.Save();
            }
        }

        /// <summary>First hole of the round, 0-based (9 = back nine).</summary>
        public static int RoundStartHole => Mathf.Max(0, PlayerPrefs.GetInt(StartHoleKey, 0));

        /// <summary>Number of holes the selected course has.</summary>
        public static int CourseHoles => Mathf.Clamp(PlayerPrefs.GetInt(CourseHolesKey, 18), 1, 18);

        public static void SetHoles(int courseHoles, int startHole, int roundLength)
        {
            PlayerPrefs.SetInt(CourseHolesKey, Mathf.Clamp(courseHoles, 1, 18));
            PlayerPrefs.SetInt(StartHoleKey, Mathf.Max(0, startHole));
            PlayerPrefs.SetInt(RoundKey, Mathf.Clamp(roundLength, 1, 18));
            PlayerPrefs.Save();
        }

        /// <summary>On-course practice: pick any hole, no scoring.</summary>
        public static bool PracticeMode
        {
            get => PlayerPrefs.GetInt(PracticeKey, 0) == 1;
            set { PlayerPrefs.SetInt(PracticeKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Tees used by the built-in demo courses.</summary>
        public static readonly string[] DemoTees = { "Red", "White", "Blue", "Black" };

        public static string CourseName => PlayerPrefs.GetString(CourseKey, "GolfSim ZA Practice Range");
        public static string TeeName => PlayerPrefs.GetString(TeeKey, "Blue");
        public static int RoundLength => PlayerPrefs.GetInt(RoundKey, 18);
        public static string GameMode => PlayerPrefs.GetString(ModeKey, "Stroke Play");
        public static string PinSetting => PlayerPrefs.GetString(PinsKey, "Standard");
        public static string GimmieSetting => PlayerPrefs.GetString(GimmieKey, "1 m");
        public static string MulliganSetting => PlayerPrefs.GetString(MulliganKey, "Unlimited");
        public static bool ResumeRound => PlayerPrefs.GetInt(ResumeKey, 0) == 1;
        public static string PlayerNames => PlayerPrefs.GetString(PlayersKey, "Player 1");

        /// <summary>Id of an imported course in the CourseLibrary, or empty for a demo course / range.</summary>
        public static string CourseId => PlayerPrefs.GetString(CourseIdKey, "");
        public static bool IsImportedCourse => !string.IsNullOrEmpty(CourseId);

        /// <summary>Tee names available on the selected course.</summary>
        public static string[] AvailableTees
        {
            get
            {
                string saved = PlayerPrefs.GetString(CourseTeesKey, "");
                string[] tees = saved.Split(new[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries);
                return tees.Length > 0 ? tees : DemoTees;
            }
        }

        public static void SetCourse(string courseId, string[] availableTees)
        {
            PlayerPrefs.SetString(CourseIdKey, courseId ?? "");
            PlayerPrefs.SetString(CourseTeesKey, availableTees != null && availableTees.Length > 0 ? string.Join("|", availableTees) : "");
            PlayerPrefs.Save();
        }

        public static void SetSession(string courseName, string teeName, int roundLength)
        {
            PlayerPrefs.SetString(CourseKey, courseName);
            PlayerPrefs.SetString(TeeKey, teeName);
            PlayerPrefs.SetInt(RoundKey, roundLength);
            PlayerPrefs.Save();
        }

        public static void SetRoundSettings(string gameMode, string teeName, string pins, string gimmie, string mulligan, bool resumeRound, int holes)
        {
            PlayerPrefs.SetString(ModeKey, gameMode);
            PlayerPrefs.SetString(TeeKey, teeName);
            PlayerPrefs.SetString(PinsKey, pins);
            PlayerPrefs.SetString(GimmieKey, gimmie);
            PlayerPrefs.SetString(MulliganKey, mulligan);
            PlayerPrefs.SetInt(ResumeKey, resumeRound ? 1 : 0);
            PlayerPrefs.SetInt(RoundKey, holes);
            PlayerPrefs.Save();
        }

        public static void SetPlayers(string[] names)
        {
            if (names != null && names.Length > MaxPlayers) System.Array.Resize(ref names, MaxPlayers);
            PlayerPrefs.SetString(PlayersKey, string.Join("|", names ?? new string[0]));
            PlayerPrefs.Save();
        }
    }
}
