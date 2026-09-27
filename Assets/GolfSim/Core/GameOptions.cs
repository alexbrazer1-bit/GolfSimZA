using System;
using UnityEngine;

namespace GolfSimZA.Core
{
    public enum LightingPreset { Morning, Midday, Evening }

    /// <summary>In-game toggles and actions shared by the game menu and the HUDs.</summary>
    public static class GameOptions
    {
        public static bool ShowDataTiles = true;
        public static bool ShowFlag = true;
        public static bool PuttGrid;
        public static LightingPreset Lighting = LightingPreset.Midday;

        public static event Action FlyoverRequested;
        public static event Action ScorecardRequested;
        public static event Action MulliganRequested;
        public static event Action ShowFlagChanged;
        public static event Action PuttGridChanged;

        public static void RequestFlyover() => FlyoverRequested?.Invoke();
        public static void RequestScorecard() => ScorecardRequested?.Invoke();
        public static void RequestMulligan() => MulliganRequested?.Invoke();

        public static void ToggleFlag()
        {
            ShowFlag = !ShowFlag;
            ShowFlagChanged?.Invoke();
        }

        public static void TogglePuttGrid()
        {
            PuttGrid = !PuttGrid;
            PuttGridChanged?.Invoke();
        }

        /// <summary>Morning / midday / evening sun on every directional light in the loaded scenes.</summary>
        public static void CycleLighting()
        {
            Lighting = (LightingPreset)(((int)Lighting + 1) % 3);
            ApplyLighting();
        }

        public static void ApplyLighting()
        {
            float pitch, intensity;
            Color color;
            switch (Lighting)
            {
                case LightingPreset.Morning: pitch = 18f; intensity = 0.95f; color = new Color(1f, 0.90f, 0.78f); break;
                case LightingPreset.Evening: pitch = 12f; intensity = 0.85f; color = new Color(1f, 0.78f, 0.60f); break;
                default: pitch = 55f; intensity = 1.05f; color = new Color(1f, 0.98f, 0.94f); break;
            }
            foreach (Light light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional || !light.isActiveAndEnabled) continue;
                Vector3 e = light.transform.eulerAngles;
                light.transform.rotation = Quaternion.Euler(pitch, e.y, 0f);
                light.intensity = intensity;
                light.color = color;
            }
        }
    }

    /// <summary>Metric (km/h, metres) or imperial (mph, yards) display.</summary>
    public static class Units
    {
        public static bool Metric => AppSettings.Current.metricUnits;
        public static string SpeedUnit => Metric ? "KM/H" : "MPH";
        public static string DistanceUnit => Metric ? "m" : "yd";
        public static float Speed(float metersPerSecond) => Metric ? metersPerSecond * 3.6f : metersPerSecond * 2.2369363f;
        public static float Distance(float meters) => Metric ? meters : meters * 1.0936133f;
        public static string DistanceText(float meters, string format = "0") => Distance(meters).ToString(format) + " " + DistanceUnit;
    }
}
