using System;
using System.IO;
using UnityEngine;

namespace GolfSimZA.Core
{
    /// <summary>
    /// Machine-local settings stored as JSON in Application.persistentDataPath.
    /// Survives updates and reinstalls because the installer never touches that folder.
    /// </summary>
    [Serializable]
    public sealed class AppSettings
    {
        public const string DefaultUpdateFolder = @"C:\Shared\GolfSimZA-Updates";

        public string updateFolder = DefaultUpdateFolder;
        public bool checkForUpdatesOnStart = true;
        public string lastCourseImportFolder = "";
        /// <summary>Altitude of the home simulator / driving range in metres (thinner air = longer carry).</summary>
        public float homeAltitudeMeters = 0f;

        private static AppSettings current;

        public static string FilePath => Path.Combine(Application.persistentDataPath, "settings.json");

        public static AppSettings Current
        {
            get
            {
                if (current == null) current = Load();
                return current;
            }
        }

        private static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    AppSettings loaded = JsonUtility.FromJson<AppSettings>(File.ReadAllText(FilePath));
                    if (loaded != null)
                    {
                        if (string.IsNullOrWhiteSpace(loaded.updateFolder)) loaded.updateFolder = DefaultUpdateFolder;
                        if (loaded.lastCourseImportFolder == null) loaded.lastCourseImportFolder = "";
                        return loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GolfSimZA] Could not read settings, using defaults: " + ex.Message);
            }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(this, true));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(temp, FilePath);
            }
            catch (Exception ex)
            {
                Debug.LogError("[GolfSimZA] Could not save settings: " + ex.Message);
            }
        }
    }
}
