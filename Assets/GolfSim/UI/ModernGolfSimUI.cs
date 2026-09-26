using System;
using GolfSimZA.Core;
using GolfSimZA.Courses;
using GolfSimZA.Physics;
using UnityEngine;

namespace GolfSimZA.UI
{
    /// <summary>
    /// Presentation layer for the simulator. It replaces the older fixed-layout
    /// RoundGameplayUI at runtime without changing the R10 data pipeline.
    /// </summary>
    public sealed class ModernGolfSimUI : MonoBehaviour
    {
        private SimulatorController simulator;
        private BallFlightSimulator flight;
        private RoundGameplayUI legacyUi;
        private GUIStyle panel, panelStrong, title, label, value, bigValue, button, center;
        private Texture2D darkTexture, strongTexture, accentTexture, greenTexture, whiteTexture;
        private bool stylesReady;
        private bool rangeBuilt;
        private bool isRange;
        private int lastShotCount;
        private string status = "READY";
        private float rangeDistance = 300f;

        private void Awake()
        {
            simulator = GetComponent<SimulatorController>();
            flight = simulator != null ? simulator.BallFlight : FindFirstObjectByType<BallFlightSimulator>();
            legacyUi = FindFirstObjectByType<RoundGameplayUI>();
            if (legacyUi != null) legacyUi.enabled = false;
            isRange = IsDrivingRange();
            if (isRange) BuildDrivingRange();
        }

        private void Update()
        {
            if (simulator == null) return;
            int count = simulator.History != null ? simulator.History.Count : 0;
            if (count > lastShotCount)
            {
                lastShotCount = count;
                status = "SHOT RECEIVED";
            }
            if (flight != null && flight.IsInFlight) status = "BALL IN FLIGHT";
            else if (status == "BALL IN FLIGHT") status = "SHOT COMPLETE";
        }

        private bool IsDrivingRange()
        {
            string name = CourseSession.CourseName ?? string.Empty;
            return name.IndexOf("range", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("practice", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;
            darkTexture = MakeTexture(new Color(0.025f, 0.055f, 0.065f, 0.94f));
            strongTexture = MakeTexture(new Color(0.008f, 0.025f, 0.032f, 0.97f));
            accentTexture = MakeTexture(new Color(0.03f, 0.55f, 0.86f, 1f));
            greenTexture = MakeTexture(new Color(0.08f, 0.38f, 0.16f, 1f));
            whiteTexture = MakeTexture(Color.white);
            panel = Panel(darkTexture);
            panelStrong = Panel(strongTexture);
            title = Label(14, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            label = Label(10, FontStyle.Normal, new Color(0.72f, 0.80f, 0.83f), TextAnchor.MiddleLeft);
            value = Label(14, FontStyle.Bold, Color.white, TextAnchor.MiddleRight);
            bigValue = Label(24, FontStyle.Bold, Color.white, TextAnchor.MiddleRight);
            button = Button(12);
            center = Label(11, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            stylesReady = true;
        }

        private GUIStyle Panel(Texture2D texture)
        {
            GUIStyle s = new GUIStyle(GUI.skin.box);
            s.normal.background = texture;
            s.border = new RectOffset(7, 7, 7, 7);
            s.padding = new RectOffset(10, 10, 7, 7);
            return s;
        }

        private GUIStyle Label(int size, FontStyle style, Color color, TextAnchor align)
        {
            GUIStyle s = new GUIStyle(GUI.skin.label);
            s.fontSize = size; s.fontStyle = style; s.alignment = align; s.normal.textColor = color;
            s.padding = new RectOffset(0, 0, 0, 0);
            return s;
        }

        private GUIStyle Button(int size)
        {
            GUIStyle s = new GUIStyle(GUI.skin.button);
            s.fontSize = size; s.fontStyle = FontStyle.Bold; s.alignment = TextAnchor.MiddleCenter;
            s.normal.textColor = Color.white; s.normal.background = accentTexture;
            s.hover.textColor = Color.white; s.hover.background = accentTexture;
            s.active.textColor = Color.white; s.active.background = accentTexture;
            return s;
        }

        private Texture2D MakeTexture(Color color)
        {
            Texture2D t = new Texture2D(1, 1);
            t.SetPixel(0, 0, color); t.Apply(); return t;
        }

        private void OnGUI()
        {
            EnsureStyles();
            ShotData shot = simulator != null ? simulator.LastShot : default(ShotData);
            bool hasShot = shot.IsValid;
            if (isRange) DrawRange(shot, hasShot); else DrawRound(shot, hasShot);
        }

        private void DrawRange(ShotData shot, bool hasShot)
        {
            float margin = 16f;
            float w = Screen.width - margin * 2f;
            GUI.Box(new Rect(margin, 10f, w, 48f), GUIContent.none, panelStrong);
            GUI.Label(new Rect(margin + 14f, 10f, 150f, 48f), "GOLFSIM ZA", title);
            GUI.Label(new Rect(margin + 160f, 10f, 260f, 48f), "DRIVING RANGE", title);
            GUI.Label(new Rect(margin + w - 180f, 10f, 165f, 48f), "R10  •  " + status, center);

            float cardW = Mathf.Clamp(Screen.width * 0.23f, 275f, 340f);
            DrawMetricsCard(16f, 72f, cardW, 300f, shot, hasShot);

            float targetW = Mathf.Clamp(Screen.width * 0.38f, 390f, 560f);
            float targetX = (Screen.width - targetW) * 0.5f;
            GUI.Box(new Rect(targetX, 72f, targetW, 126f), GUIContent.none, panel);
            GUI.Label(new Rect(targetX + 12f, 82f, targetW - 24f, 20f), "RANGE TARGETS", title);
            GUI.Label(new Rect(targetX + 12f, 113f, targetW - 24f, 28f), "50 m    100 m    150 m    200 m    250 m    300 m", center);
            GUI.Label(new Rect(targetX + 12f, 151f, targetW - 24f, 22f), hasShot ? "LAST CARRY  " + shot.CarryMeters.ToString("F1") + " m" : "HIT A BALL TO START", center);

            float resultW = Mathf.Clamp(Screen.width * 0.22f, 270f, 320f);
            float resultX = Screen.width - resultW - 16f;
            GUI.Box(new Rect(resultX, 72f, resultW, 300f), GUIContent.none, panel);
            GUI.Label(new Rect(resultX + 12f, 82f, resultW - 24f, 22f), "LAST SHOT", title);
            RangeResult(resultX, 112f, resultW, "CARRY", hasShot ? shot.CarryMeters.ToString("F1") + " m" : "—", true);
            RangeResult(resultX, 165f, resultW, "TOTAL", hasShot ? shot.TotalMeters.ToString("F1") + " m" : "—", false);
            RangeResult(resultX, 218f, resultW, "BALL SPEED", hasShot ? shot.BallSpeedKph.ToString("F1") + " km/h" : "—", false);
            RangeResult(resultX, 257f, resultW, "CLUB SPEED", hasShot && shot.HasClubData ? shot.ClubSpeedKph.ToString("F1") + " km/h" : "—", false);
            GUI.Label(new Rect(resultX + 12f, 294f, resultW - 24f, 22f), hasShot && shot.HasClubData && shot.ClubSpeedMps > 0.1f ? "SMASH  " + (shot.BallSpeedMps / shot.ClubSpeedMps).ToString("F2") : "SMASH  —", center);

            DrawClubBar();
        }

        private void DrawMetricsCard(float x, float y, float w, float h, ShotData shot, bool hasShot)
        {
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, panel);
            GUI.Label(new Rect(x + 12f, y + 10f, w - 24f, 22f), "SHOT DATA", title);
            float row = y + 43f;
            Metric("CLUB", hasShot ? shot.ClubName : "Driver", ref row, x, w);
            Metric("CLUB SPEED", hasShot && shot.HasClubData ? shot.ClubSpeedKph.ToString("F1") + " km/h" : "—", ref row, x, w);
            Metric("BALL SPEED", hasShot ? shot.BallSpeedKph.ToString("F1") + " km/h" : "—", ref row, x, w);
            Metric("SMASH", hasShot && shot.HasClubData && shot.ClubSpeedMps > 0.1f ? (shot.BallSpeedMps / shot.ClubSpeedMps).ToString("F2") : "—", ref row, x, w);
            Metric("LAUNCH", hasShot ? shot.LaunchAngleDeg.ToString("F1") + "°" : "—", ref row, x, w);
            Metric("HLA", hasShot ? shot.LaunchDirectionDeg.ToString("F1") + "°" : "—", ref row, x, w);
            Metric("BACKSPIN", hasShot ? shot.BackSpinRpm.ToString("F0") + " rpm" : "—", ref row, x, w);
            Metric("CARRY", hasShot ? shot.CarryMeters.ToString("F1") + " m" : "—", ref row, x, w);
            Metric("TOTAL", hasShot ? shot.TotalMeters.ToString("F1") + " m" : "—", ref row, x, w);
        }

        private void Metric(string name, string text, ref float y, float x, float w)
        {
            GUI.Label(new Rect(x + 12f, y, w * 0.48f, 21f), name, label);
            GUI.Label(new Rect(x + w * 0.42f, y, w * 0.53f, 21f), text, value);
            y += 27f;
        }

        private void RangeResult(float x, float y, float w, string name, string text, bool big)
        {
            GUI.Label(new Rect(x + 12f, y, w - 24f, 18f), name, label);
            GUI.Label(new Rect(x + 12f, y + 16f, w - 24f, big ? 32f : 24f), text, big ? bigValue : value);
        }

        private void DrawRound(ShotData shot, bool hasShot)
        {
            float margin = 16f;
            float w = Screen.width - margin * 2f;
            GUI.Box(new Rect(margin, 10f, w, 48f), GUIContent.none, panelStrong);
            GUI.Label(new Rect(margin + 14f, 10f, 150f, 48f), "GOLFSIM ZA", title);
            GUI.Label(new Rect(margin + 165f, 10f, w - 470f, 48f), CourseSession.CourseName, center);
            GUI.Label(new Rect(margin + w - 290f, 10f, 130f, 48f), "HOLE " + (CourseSession.RoundLength > 0 ? CourseSession.RoundLength : 1), center);
            GUI.Label(new Rect(margin + w - 155f, 10f, 140f, 48f), status, center);

            float cardW = Mathf.Clamp(Screen.width * 0.24f, 290f, 350f);
            DrawMetricsCard(16f, 72f, cardW, 300f, shot, hasShot);
            DrawClubBar();
        }

        private void DrawClubBar()
        {
            float width = Mathf.Clamp(Screen.width * 0.44f, 440f, 650f);
            float x = 16f, y = Screen.height - 66f;
            GUI.Box(new Rect(x, y, width, 50f), GUIContent.none, panelStrong);
            string[] clubs = { "Driver", "3W", "5i", "7i", "9i", "PW", "SW", "Putter" };
            float bw = (width - 52f) / clubs.Length;
            for (int i = 0; i < clubs.Length; i++)
            {
                Rect r = new Rect(x + 46f + i * bw, y + 8f, bw - 3f, 32f);
                if (GUI.Button(r, clubs[i], button)) { }
            }
            GUI.Label(new Rect(x + 8f, y + 8f, 35f, 32f), "CLUB", label);
        }

        private void BuildDrivingRange()
        {
            if (rangeBuilt) return;
            rangeBuilt = true;
            GameObject root = GameObject.Find("GolfSimZA_DrivingRange");
            if (root != null) return;
            root = new GameObject("GolfSimZA_DrivingRange");

            for (int i = 0; i < 7; i++)
            {
                float z = i * 50f;
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = "RangeTarget_" + z.ToString("0") + "m";
                marker.transform.SetParent(root.transform);
                marker.transform.position = new Vector3(0f, 0.02f, z);
                marker.transform.localScale = new Vector3(5f, 0.03f, 5f);
                Renderer r = marker.GetComponent<Renderer>();
                if (r != null)
                {
                    r.material = new Material(Shader.Find("Standard"));
                    r.material.color = i == 0 ? new Color(0.12f, 0.42f, 0.18f) : new Color(0.16f, 0.50f, 0.20f);
                }
                Collider c = marker.GetComponent<Collider>();
                if (c != null) Destroy(c);
            }

            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0f, 4.2f, -8f);
                cam.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.7f, 145f) - cam.transform.position, Vector3.up);
            }
        }
    }
}
