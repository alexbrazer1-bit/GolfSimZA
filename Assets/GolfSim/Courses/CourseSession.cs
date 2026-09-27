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

        /// <summary>Tees used by the built-in demo courses.</summary>
        public static readonly string[] DemoTees = { "Red", "White", "Blue", "Black" };

        public static string CourseName => PlayerPrefs.GetString(CourseKey, "GolfSim ZA Practice Range");
        public static string TeeName => PlayerPrefs.GetString(TeeKey, "Blue");
        public static int RoundLength => PlayerPrefs.GetInt(RoundKey, 18);
        public static string GameMode => PlayerPrefs.GetString(ModeKey, "Stroke Play");
        public static string PinSetting => PlayerPrefs.GetString(PinsKey, "Standard");
        public static string GimmieSetting => PlayerPrefs.GetString(GimmieKey, "1 m");
        public static string MulliganSetting => PlayerPrefs.GetString(MulliganKey, "Off");
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
            PlayerPrefs.SetString(PlayersKey, string.Join("|", names));
            PlayerPrefs.Save();
        }
    }
}
