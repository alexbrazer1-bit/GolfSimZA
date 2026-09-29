using System;
using GolfSimZA.Core;
using UnityEngine;
using UnityEngine.UI;

namespace GolfSimZA.Visual
{
    /// <summary>
    /// SECOND SCREEN (Settings → GAME → SCREENS, or the picker at start-up): a full-screen page on
    /// the other monitor with the Garmin R10 state (receiver, bridge, ready, ball detected, last
    /// packet) and the swing data of the last shot in big tiles, updated live while the ball flies.
    /// Built with a screen-space canvas on its own Unity display (the game's own HUD only draws on
    /// the main window).
    /// </summary>
    public sealed class SecondScreen : MonoBehaviour
    {
        private static SecondScreen instance;
        private int display = -1;
        private Camera cam;
        private Canvas canvas;
        private Font font;
        private Text clock, statusLine, statusDetail, playerLine, footer;
        private Image statusDot;
        private readonly Text[] values = new Text[16];
        private readonly Text[] captions = new Text[16];
        private float nextRefresh;

        private static readonly string[] Captions =
        {
            "BALL SPEED", "CLUB SPEED", "SMASH FACTOR", "LAUNCH ANGLE",
            "LAUNCH DIRECTION", "BACK SPIN", "SPIN AXIS", "SIDE SPIN",
            "CARRY", "TOTAL", "OFFLINE", "PEAK HEIGHT",
            "DESCENT ANGLE", "CLUB PATH", "FACE TO TARGET", "ATTACK ANGLE"
        };

        private static readonly Color Background = new Color(0.02f, 0.05f, 0.06f, 1f);
        private static readonly Color TileColor = new Color(0.06f, 0.12f, 0.14f, 1f);
        private static readonly Color Orange = new Color(1f, 0.55f, 0.18f);
        private static readonly Color Green = new Color(0.30f, 0.90f, 0.50f);
        private static readonly Color Amber = new Color(1f, 0.74f, 0.20f);
        private static readonly Color Red = new Color(0.95f, 0.32f, 0.28f);

        /// <summary>The Unity display the second screen is on (-1 = none).</summary>
        public static int ActiveDisplay => instance != null && instance.canvas != null && instance.canvas.enabled ? instance.display : -1;

        /// <summary>
        /// The Unity display to use: the one picked in Settings, else the first monitor that is
        /// not showing the game.
        /// </summary>
        public static int TargetDisplay()
        {
            AppSettings s = AppSettings.Current;
            int count = Display.displays.Length;
            if (count < 2) return -1;
            if (s.secondScreenDisplay >= 1 && s.secondScreenDisplay < count) return s.secondScreenDisplay;
            int game = GolfSimZA.UI.DisplaySetup.CurrentGameMonitor;
            for (int i = 1; i < count; i++)
                if (i != game) return i;
            return 1;
        }

        /// <summary>Opens, moves or hides the second screen to match the settings.</summary>
        public static void Apply()
        {
            AppSettings s = AppSettings.Current;
            int target = s.secondScreen ? TargetDisplay() : -1;
            if (target < 0)
            {
                if (instance != null) instance.Show(false);
                if (s.secondScreen) Debug.Log("[GolfSimZA] Second screen: only one monitor found.");
                return;
            }
            if (instance == null)
            {
                var go = new GameObject("GolfSimZA_SecondScreen");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<SecondScreen>();
                instance.Build();
            }
            instance.MoveTo(target);
            instance.Show(true);
        }

        private static bool quitWatchdog;

        /// <summary>
        /// With a second Unity display open the Windows player can keep that window (and the
        /// process) alive after quitting. Once the game has had a few seconds to shut down
        /// normally (R10 bridge stopped, settings saved), the process is ended for sure.
        /// </summary>
        private static void ArmQuitWatchdog()
        {
            if (quitWatchdog || Application.isEditor) return;
            quitWatchdog = true;
            Application.quitting += () =>
            {
                var t = new System.Threading.Thread(() =>
                {
                    System.Threading.Thread.Sleep(4000);
                    try { System.Diagnostics.Process.GetCurrentProcess().Kill(); } catch (Exception) { }
                }) { IsBackground = true };
                t.Start();
            };
        }

        private void Show(bool on)
        {
            if (canvas != null) canvas.enabled = on;
            if (cam != null) cam.enabled = display >= 0;
        }

        private void MoveTo(int target)
        {
            if (target < 0 || target >= Display.displays.Length) return;
            if (!Display.displays[target].active)
            {
                ArmQuitWatchdog();
                Display.displays[target].Activate();
                Debug.Log("[GolfSimZA] Second screen opened on Unity display " + target + " (" + Display.displays[target].systemWidth + "x" + Display.displays[target].systemHeight + ")");
            }
            display = target;
            cam.targetDisplay = target;
            canvas.targetDisplay = target;
        }

        // ------------------------------------------------------------ Build the page

        private void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            cam = gameObject.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
            cam.cullingMask = 0;
            cam.depth = -100;
            cam.allowHDR = false;
            cam.allowMSAA = false;

            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            RectTransform root = canvasGo.GetComponent<RectTransform>();

            Panel(root, new Rect(0, 0, 1920, 1080), Background);

            // Title bar
            Panel(root, new Rect(0, 0, 1920, 96), new Color(0.03f, 0.30f, 0.21f, 1f));
            Label(root, new Rect(40, 0, 1200, 96), "GOLFSIM ZA  •  SWING DATA", 44, Color.white, TextAnchor.MiddleLeft);
            clock = Label(root, new Rect(1400, 0, 480, 96), "", 40, Color.white, TextAnchor.MiddleRight);

            // Launch monitor status
            Panel(root, new Rect(40, 120, 1840, 150), TileColor);
            statusDot = Panel(root, new Rect(70, 160, 70, 70), Red);
            statusLine = Label(root, new Rect(170, 130, 1680, 76), "", 46, Color.white, TextAnchor.MiddleLeft);
            statusDetail = Label(root, new Rect(170, 200, 1680, 56), "", 26, new Color(0.72f, 0.80f, 0.83f), TextAnchor.MiddleLeft);

            // Player / club / shot
            playerLine = Label(root, new Rect(40, 285, 1840, 70), "", 38, GolfSimZA.UI.GolfSimTheme.Gold, TextAnchor.MiddleLeft);

            // 16 data tiles: 4 x 4
            const float left = 40f, top = 370f, gap = 20f;
            float tw = (1840f - 3f * gap) / 4f, th = (1080f - top - 90f - 3f * gap) / 4f;
            for (int i = 0; i < 16; i++)
            {
                int row = i / 4, col = i % 4;
                var r = new Rect(left + col * (tw + gap), top + row * (th + gap), tw, th);
                Panel(root, r, TileColor);
                captions[i] = Label(root, new Rect(r.x, r.y + 10f, r.width, 40f), Captions[i], 26, Orange, TextAnchor.MiddleCenter);
                values[i] = Label(root, new Rect(r.x, r.y + 44f, r.width, r.height - 54f), "—", 64, Color.white, TextAnchor.MiddleCenter);
            }
            footer = Label(root, new Rect(40, 1080 - 70, 1840, 50), "", 24, new Color(0.62f, 0.70f, 0.74f), TextAnchor.MiddleLeft);
        }

        /// <summary>A coloured block at a 1920×1080 position (top-left origin).</summary>
        private Image Panel(RectTransform parent, Rect r, Color color)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            Place(go.GetComponent<RectTransform>(), r);
            return img;
        }

        private Text Label(RectTransform parent, Rect r, string text, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            Text t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.color = color;
            t.alignment = anchor;
            t.text = text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.Max(10, size / 2);
            t.resizeTextMaxSize = size;
            Place(go.GetComponent<RectTransform>(), r);
            return t;
        }

        /// <summary>Anchors a rect in 1920×1080 canvas units (top-left origin, stretches with the screen).</summary>
        private static void Place(RectTransform rt, Rect r)
        {
            rt.anchorMin = new Vector2(r.x / 1920f, 1f - (r.y + r.height) / 1080f);
            rt.anchorMax = new Vector2((r.x + r.width) / 1920f, 1f - r.y / 1080f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ------------------------------------------------------------ Live values

        private void Update()
        {
            if (canvas == null || !canvas.enabled || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.1f;

            clock.text = DateTime.Now.ToString("HH:mm");

            // Status: green = R10 connected and ready, amber = bridge connected / waiting, red = no bridge.
            float since = LaunchMonitorStatus.LastPacketTime < 0f ? -1f : Time.realtimeSinceStartup - LaunchMonitorStatus.LastPacketTime;
            string state;
            Color dot;
            if (!LaunchMonitorStatus.Listening)
            {
                state = "GARMIN R10  •  WAITING  •  OPEN THE RANGE OR A ROUND";
                dot = Amber;
            }
            else if (!LaunchMonitorStatus.BridgeConnected)
            {
                state = "GARMIN R10  •  NOT CONNECTED  •  START THE R10 BRIDGE";
                dot = Red;
            }
            else if (LaunchMonitorStatus.ReadyKnown && !LaunchMonitorStatus.Ready)
            {
                state = "GARMIN R10  •  CONNECTED  •  NOT READY";
                dot = Amber;
            }
            else
            {
                state = "GARMIN R10  •  CONNECTED  •  READY" + (LaunchMonitorStatus.ReadyKnown && LaunchMonitorStatus.BallDetected ? "  •  BALL DETECTED" : "");
                dot = Green;
            }
            statusLine.text = state;
            statusDot.color = dot;
            statusDetail.text = "Receiver " + (LaunchMonitorStatus.Listening ? "listening on port " + LaunchMonitorStatus.ListenPort : "closed")
                                + "   •   last message " + (since < 0f ? "none yet" : since < 1.5f ? "just now" : Mathf.RoundToInt(since) + " s ago")
                                + (string.IsNullOrEmpty(LaunchMonitorStatus.LastPacket) ? "" : ":  " + LaunchMonitorStatus.LastPacket);

            ShotData s = LaunchMonitorStatus.LastShot;
            bool has = LaunchMonitorStatus.HasShot;
            playerLine.text = "PLAYER  " + (ActiveClub.Player ?? "").ToUpperInvariant() + "     CLUB  " + (has && !string.IsNullOrEmpty(s.ClubName) ? s.ClubName : ActiveClub.Resolve()).ToUpperInvariant()
                              + "     SHOTS  " + LaunchMonitorStatus.ShotCount
                              + (has && LaunchMonitorStatus.LastShotTime >= 0f ? "     LAST SHOT " + Ago(Time.realtimeSinceStartup - LaunchMonitorStatus.LastShotTime) : "");

            string speed = Units.SpeedUnit.ToLowerInvariant();
            string dist = Units.DistanceUnit;
            values[0].text = has ? Units.Speed(s.BallSpeedMps).ToString("0.0") + " " + speed : "—";
            values[1].text = has && s.ClubSpeedMps > 0.1f ? Units.Speed(s.ClubSpeedMps).ToString("0.0") + " " + speed : "—";
            values[2].text = has && s.ClubSpeedMps > 0.1f ? (s.BallSpeedMps / s.ClubSpeedMps).ToString("0.00") : "—";
            values[3].text = has ? s.LaunchAngleDeg.ToString("0.0") + "°" : "—";
            values[4].text = has ? Side(s.LaunchDirectionDeg, "°") : "—";
            values[5].text = has ? s.BackSpinRpm.ToString("0") + " rpm" : "—";
            values[6].text = has ? Side(s.SpinAxisDeg, "°") : "—";
            values[7].text = has && Mathf.Abs(s.SideSpinRpm) > 0.5f ? Side(s.SideSpinRpm, " rpm", "0") : "—";
            values[8].text = has ? Units.Distance(s.CarryMeters).ToString("0.0") + " " + dist : "—";
            values[9].text = has ? Units.Distance(s.TotalMeters).ToString("0.0") + " " + dist : "—";
            values[10].text = has && s.IsComplete ? Side(Units.Distance(s.OfflineMeters), " " + dist) : "—";
            values[11].text = has && s.IsComplete ? Units.Distance(s.PeakHeightMeters).ToString("0.0") + " " + dist : "—";
            values[12].text = has && s.IsComplete ? s.DescentAngleDeg.ToString("0.0") + "°" : "—";
            values[13].text = has && s.HasClubPath ? Side(s.ClubPathDeg, "°") : "—";
            values[14].text = has && s.HasClubData && Mathf.Abs(s.FaceToTargetDeg) > 0.001f ? Side(s.FaceToTargetDeg, "°") : "—";
            values[15].text = has && s.HasClubData && Mathf.Abs(s.AttackAngleDeg) > 0.001f ? s.AttackAngleDeg.ToString("0.0") + "°" : "—";

            footer.text = "Second screen  •  Settings → GAME → SCREENS to move or switch it off";
        }

        private static string Ago(float seconds)
        {
            if (seconds < 60f) return Mathf.RoundToInt(seconds) + " s AGO";
            return Mathf.RoundToInt(seconds / 60f) + " MIN AGO";
        }

        /// <summary>"3.2° R" / "1.5° L" (positive = right).</summary>
        private static string Side(float value, string unit, string format = "0.0")
        {
            if (Mathf.Abs(value) < 0.05f) return "0" + unit;
            return Mathf.Abs(value).ToString(format) + unit + (value > 0f ? " R" : " L");
        }
    }
}
