namespace GolfSimZA.Core
{
    /// <summary>
    /// Single source of truth for the GolfSimZA version. The release build copies
    /// this into PlayerSettings.bundleVersion and the installer, and the in-game
    /// updater compares it with the update folder manifest.
    /// Bump this before every "Build + Publish Update".
    /// </summary>
    public static class GolfSimVersion
    {
        public const string Version = "1.4.0";
        public const string ProductName = "GolfSimZA";
    }
}
