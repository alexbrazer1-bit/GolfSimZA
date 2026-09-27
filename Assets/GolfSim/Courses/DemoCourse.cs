namespace GolfSimZA.Courses
{
    /// <summary>The built-in demo holes (used when no imported course is selected).</summary>
    public static class DemoCourse
    {
        public static readonly int[] Par = { 4, 4, 5, 3, 4, 4, 3, 5, 4, 4, 5, 4, 3, 4, 5, 4, 3, 5 };
        public static readonly int[] DistanceMeters = { 360, 385, 470, 165, 395, 410, 175, 505, 375, 390, 495, 400, 175, 420, 510, 405, 180, 525 };

        public static float TeeMultiplier(string tee)
        {
            switch (tee)
            {
                case "Red": return 0.86f;
                case "White": return 0.93f;
                case "Black": return 1.07f;
                default: return 1f;
            }
        }

        public static float Length(int hole, string tee)
        {
            int i = hole < 0 ? 0 : hole >= DistanceMeters.Length ? DistanceMeters.Length - 1 : hole;
            return DistanceMeters[i] * TeeMultiplier(tee);
        }
    }
}
