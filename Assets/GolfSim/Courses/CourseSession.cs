using UnityEngine;

namespace GolfSimZA.Courses
{
    public static class CourseSession
    {
        private const string CourseKey = "GolfSimZA.CourseName";
        private const string TeeKey = "GolfSimZA.TeeName";
        private const string RoundKey = "GolfSimZA.RoundLength";

        public static string CourseName => PlayerPrefs.GetString(CourseKey, "GolfSim ZA Practice Range");
        public static string TeeName => PlayerPrefs.GetString(TeeKey, "Blue");
        public static int RoundLength => PlayerPrefs.GetInt(RoundKey, 18);

        public static void SetSession(string courseName, string teeName, int roundLength)
        {
            PlayerPrefs.SetString(CourseKey, courseName);
            PlayerPrefs.SetString(TeeKey, teeName);
            PlayerPrefs.SetInt(RoundKey, roundLength);
            PlayerPrefs.Save();
        }
    }
}
