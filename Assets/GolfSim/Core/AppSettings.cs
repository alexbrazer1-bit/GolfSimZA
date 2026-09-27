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
        public bool IsFavourite(string courseKey) => favouriteCourses != null && favouriteCourses.Contains(courseKey);

        public void ToggleFavourite(string courseKey)
        {
            if (favouriteCourses == null) favouriteCourses = new System.Collections.Generic.List<string>();
            if (!favouriteCourses.Remove(courseKey)) favouriteCourses.Add(courseKey);
            Save();
        }

        public const string DefaultUpdateFolder = @"C:\Shared\GolfSimZA-Updates";

        public string updateFolder = DefaultUpdateFolder;
        public bool checkForUpdatesOnStart = true;
        public string lastCourseImportFolder = "";
        /// <summary>Altitude of the home simulator / driving range in metres (thinner air = longer carry).</summary>
        public float homeAltitudeMeters = 0f;
        /// <summary>true = km/h and metres (South Africa), false = mph and yards.</summary>
        public bool metricUnits = true;
        /// <summary>Course ids marked as favourites.</summary>
        public System.Collections.Generic.List<string> favouriteCourses = new System.Collections.Generic.List<string>();
        // Driving range layout
        public float rangeTargetMeters = 150f;
        public float rangeFairwayWidth = 55f;
        public float rangeGreenWidth = 12f;
        public bool rangeRandomizer;

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
                        if (loaded.favouriteCourses == null) loaded.favouriteCourses = new System.Collections.Generic.List<string>();
                        if (loaded.rangeTargetMeters < 20f) loaded.rangeTargetMeters = 150f;
                        if (loaded.rangeFairwayWidth < 10f) loaded.rangeFairwayWidth = 55f;
                        if (loaded.rangeGreenWidth < 3f) loaded.rangeGreenWidth = 12f;
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
