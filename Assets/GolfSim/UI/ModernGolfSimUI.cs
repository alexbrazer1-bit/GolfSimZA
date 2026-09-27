using System;
using GolfSimZA.Core;
using GolfSimZA.Courses;
using GolfSimZA.Physics;
using UnityEngine;

namespace GolfSimZA.UI
{
    public sealed class ModernGolfSimUI : MonoBehaviour
    {
        [SerializeField] private SimulatorController simulator;
        [SerializeField] private BallFlightSimulator flight;

        private GUIStyle panel, panelStrong, title, section, label, value, bigValue, button, center, tiny;
        private Texture2D darkTex, strongTex, accentTex, accentSoftTex, greenTex, whiteTex;
        private bool stylesReady;
        private bool isRange;
        private int lastShotCount;
        private string status = "READY";

        private void Awake()
        {
            simulator = simulator != null ? simulator : GetComponent<SimulatorController>();
            flight = flight != null ? flight : (simulator != null ? simulator.BallFlight : FindFirstObjectByType<BallFlightSimulator>());

            isRange = IsDrivingRange();
            RoundGameplayUI round = FindFirstObjectByType<RoundGameplayUI>();

            if (isRange)
            {
                // Driving range: this HUD replaces the round HUD.
                if (round != null) round.enabled = false;
                BuildDrivingRange();
            }
            else if (round != null)
            {
                // Playing a course: the round HUD runs the hole, scoring and course.
                // Previously this component disabled it, which stopped rounds progressing.
                enabled = false;
            }
        }

        private void Update()
        {
            if (!GameMenuOverlay.IsOpen && (flight == null || !flight.IsInFlight))
                ClubBar.HandleKeys();
            if (simulator == null) return;

            int count = simulator.History != null ? simulator.History.Count : 0;
            if (count > lastShotCount)
            {
                lastShotCount = count;
                status = "SHOT RECEIVED";
                ShotData shot = simulator.LastShot;
            }

            if (flight != null && flight.IsInFlight)
                status = "BALL IN FLIGHT";
            else if (status == "BALL IN FLIGHT")
                status = "SHOT COMPLETE";
        }

        private bool IsDrivingRange() => IsRangeSession();

        /// <summary>True when the selected "course" is the practice range (range HUD instead of a round).</summary>
        public static bool IsRangeSession()
        {
            if (CourseSession.IsImportedCourse) return false;
            string name = CourseSession.CourseName ?? string.Empty;
            return name.IndexOf("range", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("practice", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;

            darkTex = MakeTexture(new Color(0.025f, 0.045f, 0.055f, 0.94f));
            strongTex = MakeTexture(new Color(0.008f, 0.016f, 0.021f, 0.97f));
            accentTex = MakeTexture(new Color(0.03f, 0.52f, 0.86f, 1f));
            accentSoftTex = MakeTexture(new Color(0.03f, 0.52f, 0.86f, 0.28f));
            greenTex = MakeTexture(new Color(0.16f, 0.62f, 0.28f, 1f));
            whiteTex = MakeTexture(new Color(1f, 1f, 1f, 0.96f));

            panel = Panel(darkTex, 8);
            panelStrong = Panel(strongTex, 8);
            title = Label(15, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            section = Label(12, FontStyle.Bold, new Color(0.70f, 0.82f, 0.86f), TextAnchor.MiddleLeft);
            label = Label(9, FontStyle.Normal, new Color(0.58f, 0.69f, 0.73f), TextAnchor.MiddleLeft);
            value = Label(13, FontStyle.Bold, Color.white, TextAnchor.MiddleRight);
            bigValue = Label(23, FontStyle.Bold, Color.white, TextAnchor.MiddleRight);
            button = Button(10);
            center = Label(11, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            tiny = Label(8, FontStyle.Bold, new Color(0.62f, 0.72f, 0.75f), TextAnchor.MiddleCenter);
            stylesReady = true;
        }

        private GUIStyle Panel(Texture2D texture, int border)
        {
            GUIStyle s = new GUIStyle(GUI.skin.box);
            s.normal.background = texture;
            s.border = new RectOffset(border, border, border, border);
            s.padding = new RectOffset(8, 8, 6, 6);
            return s;
        }

        private GUIStyle Label(int size, FontStyle style, Color color, TextAnchor align)
        {
            GUIStyle s = new GUIStyle(GUI.skin.label);
            s.fontSize = size;
            s.fontStyle = style;
            s.alignment = align;
            s.normal.textColor = color;
            s.padding = new RectOffset(0, 0, 0, 0);
            return s;
        }

        private GUIStyle Button(int size)
        {
            GUIStyle s = new GUIStyle(GUI.skin.button);
            s.fontSize = size;
            s.fontStyle = FontStyle.Bold;
            s.alignment = TextAnchor.MiddleCenter;
            s.normal.textColor = Color.white;
            s.normal.background = accentTex;
            s.hover.textColor = Color.white;
            s.hover.background = accentTex;
            s.active.textColor = Color.white;
            s.active.background = accentTex;
            s.border = new RectOffset(5, 5, 5, 5);
            return s;
        }

        private Texture2D MakeTexture(Color color)
        {
            Texture2D t = new Texture2D(1, 1);
            t.SetPixel(0, 0, color);
            t.Apply();
            return t;
        }

        private void OnGUI()
        {
            EnsureStyles();
            ShotData shot = simulator != null ? simulator.LastShot : default(ShotData);
            bool hasShot = shot.IsValid;

            if (isRange) DrawDrivingRangeHUD(shot, hasShot);
            else DrawRoundHUD(shot, hasShot);
        }

        private void DrawDrivingRangeHUD(ShotData shot, bool hasShot)
        {
            float margin = Mathf.Clamp(Screen.width * 0.018f, 10f, 20f);
            float gap = Mathf.Clamp(Screen.width * 0.012f, 8f, 14f);
            float contentW = Screen.width - margin * 2f;

            DrawHeader(margin, 10f, contentW, 48f, "DRIVING RANGE", "PRACTICE  •  " + status);

            float y = 68f;
            float h = Mathf.Min(286f, Mathf.Max(210f, Screen.height - 155f));
            float leftW = contentW * 0.29f;
            float centerW = contentW - leftW * 2f - gap * 2f;
            float rightW = leftW;

            DrawShotDataCard(margin, y, leftW, h, shot, hasShot);
            DrawRangeTargets(margin + leftW + gap, y, centerW, h, shot, hasShot);
            DrawLastShotCard(margin + leftW + gap + centerW + gap, y, rightW, h, shot, hasShot);

            ClubBar.Draw(new Rect(margin, Screen.height - ClubBar.Height - 8f, contentW, ClubBar.Height));
        }

        private void DrawRoundHUD(ShotData shot, bool hasShot)
        {
            float margin = Mathf.Clamp(Screen.width * 0.018f, 10f, 20f);
            float gap = Mathf.Clamp(Screen.width * 0.012f, 8f, 14f);
            float contentW = Screen.width - margin * 2f;

            string hole = "HOLE " + Mathf.Clamp(CourseSession.RoundLength, 1, 18);
            DrawHeader(margin, 10f, contentW, 48f, CourseSession.CourseName, hole + "  •  " + status);

            float y = 68f;
            float h = Mathf.Min(286f, Mathf.Max(210f, Screen.height - 155f));
            float leftW = contentW * 0.29f;
            DrawShotDataCard(margin, y, leftW, h, shot, hasShot);
            DrawRoundCenter(margin + leftW + gap, y, contentW - leftW - gap, h, shot, hasShot);

            ClubBar.Draw(new Rect(margin, Screen.height - ClubBar.Height - 8f, contentW, ClubBar.Height));
        }

        private void DrawHeader(float x, float y, float w, float h, string titleText, string rightText)
        {
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, panelStrong);
            GUI.Label(new Rect(x + 140f, y, 155f, h), "GOLFSIM ZA", title);
            GUI.Label(new Rect(x + 300f, y, Mathf.Max(100f, w - 485f), h), titleText, section);
            GUI.Label(new Rect(x + w - 175f, y, 163f, h), rightText, center);
        }

        private void DrawShotDataCard(float x, float y, float w, float h, ShotData shot, bool hasShot)
        {
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, panel);
            GUI.Label(new Rect(x + 10f, y + 8f, w - 20f, 20f), "SHOT DATA", title);
            GUI.Label(new Rect(x + 10f, y + 29f, w - 20f, 14f), hasShot ? "LATEST MEASURED SHOT" : "WAITING FOR SHOT", label);

            float row = y + 48f;
            Metric("CLUB", hasShot ? shot.ClubName + "  (next: " + ActiveClub.ShortName(ActiveClub.Resolve()) + ")" : ActiveClub.Resolve(), ref row, x, w);
            Metric("CLUB SPEED", hasShot && shot.HasClubData ? Mph(shot.ClubSpeedMps) : "—", ref row, x, w);
            Metric("BALL SPEED", hasShot ? Mph(shot.BallSpeedMps) : "—", ref row, x, w);
            Metric("SMASH", Smash(shot, hasShot), ref row, x, w);
            Metric("LAUNCH", hasShot ? shot.LaunchAngleDeg.ToString("F1") + "°" : "—", ref row, x, w);
            Metric("HLA", hasShot ? shot.LaunchDirectionDeg.ToString("F1") + "°" : "—", ref row, x, w);
            Metric("BACKSPIN", hasShot ? shot.BackSpinRpm.ToString("F0") + " rpm" : "—", ref row, x, w);
            Metric("CARRY", hasShot ? shot.CarryMeters.ToString("F1") + " m" : "—", ref row, x, w);
            Metric("TOTAL", hasShot ? shot.TotalMeters.ToString("F1") + " m" : "—", ref row, x, w);
        }

        private void Metric(string name, string text, ref float y, float x, float w)
        {
            const float rowH = 24f;
            GUI.Label(new Rect(x + 10f, y, w * 0.50f, rowH), name, label);
            GUI.Label(new Rect(x + w * 0.43f, y, w * 0.52f, rowH), text, value);
            y += rowH;
        }

        private void DrawRangeTargets(float x, float y, float w, float h, ShotData shot, bool hasShot)
        {
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, panel);
            GUI.Label(new Rect(x + 10f, y + 8f, w - 20f, 20f), "RANGE TARGETS", title);
            GUI.Label(new Rect(x + 10f, y + 29f, w - 20f, 14f), "DISTANCE BOARD", label);

            float cx = x + w * 0.5f;
            float top = y + 55f;
            float boardW = Mathf.Max(120f, w - 24f);
            float boardH = Mathf.Max(110f, h - 78f);
            float boardX = cx - boardW * 0.5f;
            GUI.Box(new Rect(boardX, top, boardW, boardH), GUIContent.none, panelStrong);

            int[] distances = { 50, 100, 150, 200, 250, 300 };
            for (int i = 0; i < distances.Length; i++)
            {
                float px = boardX + boardW * ((i + 1f) / 7f);
                GUI.DrawTexture(new Rect(px - 1f, top + 25f, 2f, Mathf.Max(40f, boardH - 50f)), accentSoftTex);
                GUI.Label(new Rect(px - 28f, top + 5f, 56f, 18f), distances[i] + " m", tiny);
            }

            GUI.DrawTexture(new Rect(boardX + boardW * 0.5f - 1f, top + 25f, 2f, Mathf.Max(40f, boardH - 50f)), greenTex);
            string result = hasShot
                ? "CARRY  " + shot.CarryMeters.ToString("F1") + " m  •  TOTAL  " + shot.TotalMeters.ToString("F1") + " m"
                : "HIT A BALL TO START";
            GUI.Label(new Rect(boardX + 8f, top + boardH - 27f, boardW - 16f, 20f), result, center);
            GUI.Label(new Rect(x + 12f, y + h - 36f, w - 24f, 22f), status, center);
        }

        private void DrawLastShotCard(float x, float y, float w, float h, ShotData shot, bool hasShot)
        {
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, panel);
            GUI.Label(new Rect(x + 10f, y + 8f, w - 20f, 20f), "LAST SHOT", title);
            GUI.Label(new Rect(x + 10f, y + 29f, w - 20f, 14f), hasShot ? "RESULT" : "NO SHOT YET", label);

            float row = y + 51f;
            ResultMetric("CARRY", hasShot ? shot.CarryMeters.ToString("F1") + " m" : "—", ref row, x, w, true);
            ResultMetric("TOTAL", hasShot ? shot.TotalMeters.ToString("F1") + " m" : "—", ref row, x, w, false);
            ResultMetric("BALL SPEED", hasShot ? Mph(shot.BallSpeedMps) : "—", ref row, x, w, false);
            ResultMetric("CLUB SPEED", hasShot && shot.HasClubData ? Mph(shot.ClubSpeedMps) : "—", ref row, x, w, false);
            ResultMetric("SMASH", Smash(shot, hasShot), ref row, x, w, false);
            ResultMetric("SPIN", hasShot ? shot.BackSpinRpm.ToString("F0") + " rpm" : "—", ref row, x, w, false);
        }

        private void ResultMetric(string name, string text, ref float y, float x, float w, bool big)
        {
            GUI.Label(new Rect(x + 10f, y, w - 20f, 15f), name, label);
            GUI.Label(new Rect(x + 10f, y + 12f, w - 20f, big ? 31f : 24f), text, big ? bigValue : value);
            y += big ? 53f : 42f;
        }

        private void DrawRoundCenter(float x, float y, float w, float h, ShotData shot, bool hasShot)
        {
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, panel);
            GUI.Label(new Rect(x + 12f, y + 8f, w - 24f, 20f), CourseSession.CourseName, title);
            GUI.Label(new Rect(x + 12f, y + 31f, w - 24f, 18f), "LIVE SHOT VIEW", label);
            GUI.Label(new Rect(x + 12f, y + 72f, w - 24f, 30f), hasShot ? "LAST CARRY  " + shot.CarryMeters.ToString("F1") + " m" : "READY", center);
            GUI.Label(new Rect(x + 12f, y + 110f, w - 24f, 18f), status, center);
        }

        private string Smash(ShotData shot, bool hasShot)
        {
            return hasShot && shot.HasClubData && shot.ClubSpeedMps > 0.1f
                ? (shot.BallSpeedMps / shot.ClubSpeedMps).ToString("F2")
                : "—";
        }

        private string Mph(float mps)
        {
            return (mps * 2.2369363f).ToString("F1") + " mph";
        }

        private void BuildDrivingRange()
        {
            GameObject oldGround = GameObject.Find("CourseGround");
            if (oldGround != null) oldGround.SetActive(false);
            GameObject oldGreen = GameObject.Find("HoleGreen");
            if (oldGreen != null) oldGreen.SetActive(false);
            GameObject oldPin = GameObject.Find("HolePin");
            if (oldPin != null) oldPin.SetActive(false);

            GameObject root = GameObject.Find("GolfSimZA_DrivingRange");
            if (root == null) root = new GameObject("GolfSimZA_DrivingRange");

            if (root.transform.Find("RangeGround") == null)
            {
                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "RangeGround";
                ground.transform.SetParent(root.transform);
                ground.transform.position = Vector3.zero;
                ground.transform.localScale = new Vector3(14f, 1f, 34f);
                ground.GetComponent<Renderer>().material = MaterialFor(new Color(0.12f, 0.42f, 0.18f));
            }

            Material targetMaterial = MaterialFor(new Color(0.18f, 0.60f, 0.26f));
            Material markerMaterial = MaterialFor(new Color(0.86f, 0.87f, 0.85f));

            for (int i = 1; i <= 6; i++)
            {
                float z = i * 50f;
                string targetName = "RangeTarget_" + z.ToString("0") + "m";
                if (root.transform.Find(targetName) == null)
                {
                    GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    ring.name = targetName;
                    ring.transform.SetParent(root.transform);
                    ring.transform.position = new Vector3(0f, 0.025f, z);
                    ring.transform.localScale = new Vector3(5f, 0.04f, 5f);
                    ring.GetComponent<Renderer>().material = targetMaterial;
                    Collider c = ring.GetComponent<Collider>();
                    if (c != null) Destroy(c);
                }

                string markerName = "DistanceMarker_" + z.ToString("0") + "m";
                if (root.transform.Find(markerName) == null)
                {
                    GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    marker.name = markerName;
                    marker.transform.SetParent(root.transform);
                    marker.transform.position = new Vector3(7.5f, 0.03f, z);
                    marker.transform.localScale = new Vector3(0.25f, 0.05f, 3.5f);
                    marker.GetComponent<Renderer>().material = markerMaterial;
                    Collider c = marker.GetComponent<Collider>();
                    if (c != null) Destroy(c);
                }
            }

            if (root.transform.Find("RangeTeeBox") == null)
            {
                GameObject tee = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tee.name = "RangeTeeBox";
                tee.transform.SetParent(root.transform);
                tee.transform.position = new Vector3(0f, 0.06f, -3f);
                tee.transform.localScale = new Vector3(6f, 0.12f, 4f);
                tee.GetComponent<Renderer>().material = MaterialFor(new Color(0.05f, 0.22f, 0.10f));
            }

            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0f, 5.2f, -9f);
                cam.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1.0f, 150f) - cam.transform.position, Vector3.up);
                cam.fieldOfView = 55f;
            }
        }

        private Material MaterialFor(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material m = new Material(shader);
            m.color = color;
            return m;
        }
    }
}
