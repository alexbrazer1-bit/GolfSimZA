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

        // ---------------- GAME
        /// <summary>Hole map on the right (true) or the left (false) of the play screen.</summary>
        public bool miniMapRight = true;
        /// <summary>0 = classic (furthest from the hole plays), 1 = play out hole, 2 = putt out.</summary>
        public int rotationStyle;
        /// <summary>Auto putt: a ball that stops on the green is holed in 1 or 2 putts inside these distances.</summary>
        public bool autoPutt;
        public float autoPuttOneMeters = 3.0f;
        public float autoPuttTwoMeters = 12.0f;

        // ---------------- REALISM
        /// <summary>0 off, 1 light, 2 moderate, 3 strong, 4 custom.</summary>
        public int windMode;
        public float windCustomMps = 3f;
        /// <summary>Direction the custom wind blows FROM, degrees clockwise from the tee-to-green line.</summary>
        public float windCustomFromDeg = 180f;
        /// <summary>Green speed (stimpmeter, feet).</summary>
        public float greenStimp = 10f;
        /// <summary>0 soft, 1 normal, 2 firm fairways.</summary>
        public int fairwayFirmness = 1;

        // ---------------- SOUNDS (0..1)
        public float volumeGolf = 0.8f;
        public float volumeCourse = 0.7f;
        public float volumeMenu = 0.6f;
        public float volumeMaster = 0.9f;
        public bool ballReadySound = true;

        // ---------------- VISUAL: user interface
        public bool showGimmeCircle = true;
        /// <summary>0 red, 1 blue, 2 white.</summary>
        public int gimmeCircleColor = 1;
        public bool showAimIndicator = true;
        public bool showBallReady = true;
        public bool hideUiOnShot;
        public bool showDistanceBanner = true;
        public bool hideBannerOnGreen;
        /// <summary>Seconds the scorecard shows after each hole (0 = off).</summary>
        public int scorecardAfterHoleSeconds = 5;
        public bool showUpNextMessage = true;
        /// <summary>0 show, 1 hide, 2 auto hide (hidden while the ball flies).</summary>
        public int clubSelectorMode;
        /// <summary>Shot data tiles shown on the play screen, in order (ids from ShotDataTiles).</summary>
        public System.Collections.Generic.List<string> dataTiles = new System.Collections.Generic.List<string>();

        // ---------------- VISUAL: camera
        public bool followCamera = true;
        /// <summary>0 = camera sticks to the ball, 1 = lazy follow.</summary>
        public float followDelay = 0.4f;
        public bool autoFlyover;
        /// <summary>0 slow .. 1 fast.</summary>
        public float flyoverSpeed = 0.4f;
        /// <summary>0 low .. 1 high.</summary>
        public float flyoverHeight = 0.6f;
        /// <summary>0 red, 1 blue, 2 green, 3 yellow, 4 gold, 5 none.</summary>
        public int ballTrailColor = 4;
        public bool ballTrailThick;
        public bool gradientFog = true;
        public float drawDistanceMeters = 5000f;

        // ---------------- VISUAL: course look
        /// <summary>0 off, 1 only courses without trees, 2 every course.</summary>
        public int addTrees = 1;
        /// <summary>0.2 (few) .. 1 (many) GolfSim ZA trees.</summary>
        public float treeDensity = 0.6f;
        /// <summary>0 off, 1 natural, 2 lush, 3 vivid colour grading.</summary>
        public int colourBoost = 2;
        public bool clouds = true;
        /// <summary>0 low, 1 medium, 2 high, 3 ultra: shadows, texture filtering, edge smoothing.</summary>
        public int graphicsQuality = 3;
        /// <summary>GolfSim ZA 3D grass in the rough (0 off, 1 on).</summary>
        public int roughGrass = 1;
        /// <summary>0.2 (thin) .. 1 (thick) rough grass.</summary>
        public float grassDensity = 0.7f;
        /// <summary>How far away the rough grass is drawn (metres).</summary>
        public float grassDistance = 70f;
        /// <summary>Filmic tone mapping + sunlight like a photo (HDR).</summary>
        public bool photoLighting = true;
        /// <summary>Crisper picture (mild sharpening) and a soft darkened edge.</summary>
        public bool sharpen = true;

        // ---------------- OFFSET
        /// <summary>Added to the launch direction of every shot (corrects a launch monitor that is not square to the screen).</summary>
        public float aimOffsetDeg;
        /// <summary>Added to the launch angle of every shot.</summary>
        public float launchAngleOffsetDeg;

        private static AppSettings current;

        /// <summary>Raised after every save so open screens apply the new settings immediately.</summary>
        public static event Action Changed;

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
                        if (loaded.dataTiles == null) loaded.dataTiles = new System.Collections.Generic.List<string>();
                        if (loaded.greenStimp < 5f) loaded.greenStimp = 10f;
                        if (loaded.drawDistanceMeters < 500f) loaded.drawDistanceMeters = 5000f;
                        if (loaded.grassDistance < 20f) loaded.grassDistance = 70f;
                        if (loaded.grassDensity < 0.1f) loaded.grassDensity = 0.7f;
                        if (loaded.autoPuttOneMeters <= 0f) loaded.autoPuttOneMeters = 3f;
                        if (loaded.autoPuttTwoMeters <= loaded.autoPuttOneMeters) loaded.autoPuttTwoMeters = Mathf.Max(12f, loaded.autoPuttOneMeters + 1f);
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
            try { Changed?.Invoke(); }
            catch (Exception ex) { Debug.LogError("[GolfSimZA] Applying settings failed: " + ex); }
        }
    }
}
