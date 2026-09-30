using System;
using GolfSimZA.Core;
using UnityEngine;
using UnityEngine.UI;

namespace GolfSimZA.Visual
{
    /// <summary>
    /// SECOND SCREEN (Settings → GAME → SCREENS, or the picker at start-up): a full-screen page on
    /// the other monitor with the Garmin R10 state (Bluetooth link, ready, battery gauge, model) and
    /// the last shot, updated live while the ball flies, on two pages chosen with the tabs at the
    /// top: NUMBERS (16 data tiles) and VISUAL (club path / face, spin axis, launch and direction
    /// drawings). Built with a screen-space canvas on its own Unity display (the game's own HUD
    /// only draws on the main window).
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

        private GameObject numbersPage, visualPage;
        private Image numbersTab, visualTab;
        private Text numbersTabText, visualTabText;
        private Image batteryFill;
        private Text batteryText, deviceText;
        private Sprite circleSprite;

        // VISUAL page
        private readonly Text[] topValues = new Text[5];
        private RectTransform pathArrow, faceBar;
        private Text pathText, faceText, faceToPathText;
        private RectTransform spinAxis;
        private Text axisText, totalSpinText, sideSpinText, shapeText;
        private RectTransform launchLine, attackLine;
        private Text vlaText, aoaText, backSpinText, peakText;
        private RectTransform hlaLine, landingDot;
        private Text hlaText, offlineText, carryText, directionShape;

        private const float TopY = 310f;

        private void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            circleSprite = MakeCircleSprite();

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
            canvasGo.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            RectTransform root = canvasGo.GetComponent<RectTransform>();

            Panel(root, new Rect(0, 0, 1920, 1080), Background);

            // Title bar with the page tabs
            Panel(root, new Rect(0, 0, 1920, 96), new Color(0.03f, 0.30f, 0.21f, 1f));
            Label(root, new Rect(40, 0, 900, 96), "GOLFSIM ZA  •  SWING DATA", 44, Color.white, TextAnchor.MiddleLeft);
            numbersTab = TabButton(root, new Rect(1000, 18, 250, 60), "NUMBERS", 0, out numbersTabText);
            visualTab = TabButton(root, new Rect(1265, 18, 250, 60), "VISUAL", 1, out visualTabText);
            clock = Label(root, new Rect(1560, 0, 320, 96), "", 40, Color.white, TextAnchor.MiddleRight);

            // Garmin R10: link state, battery, model
            Panel(root, new Rect(40, 112, 1840, 120), TileColor);
            statusDot = Panel(root, new Rect(66, 140, 64, 64), Red);
            statusDot.sprite = circleSprite;
            statusLine = Label(root, new Rect(156, 118, 1180, 64), "", 42, Color.white, TextAnchor.MiddleLeft);
            statusDetail = Label(root, new Rect(156, 178, 1180, 44), "", 24, new Color(0.72f, 0.80f, 0.83f), TextAnchor.MiddleLeft);
            // Battery gauge: frame, fill (0-100 %), tip, percentage and device name
            Panel(root, new Rect(1400, 136, 180, 72), new Color(0.85f, 0.90f, 0.92f, 1f));
            Panel(root, new Rect(1406, 142, 168, 60), TileColor);
            Panel(root, new Rect(1580, 154, 14, 36), new Color(0.85f, 0.90f, 0.92f, 1f));
            batteryFill = Panel(root, new Rect(1410, 146, 160, 52), Green);
            batteryText = Label(root, new Rect(1606, 118, 260, 64), "", 44, Color.white, TextAnchor.MiddleLeft);
            deviceText = Label(root, new Rect(1400, 208, 470, 24), "", 18, new Color(0.72f, 0.80f, 0.83f), TextAnchor.MiddleLeft);

            // Player / club / shot
            playerLine = Label(root, new Rect(40, 242, 1840, 60), "", 36, GolfSimZA.UI.GolfSimTheme.Gold, TextAnchor.MiddleLeft);

            numbersPage = Page(root, "Numbers");
            visualPage = Page(root, "Visual");
            BuildNumbers(numbersPage.GetComponent<RectTransform>());
            BuildVisual(visualPage.GetComponent<RectTransform>());

            footer = Label(root, new Rect(40, 1080 - 58, 1840, 44), "", 22, new Color(0.62f, 0.70f, 0.74f), TextAnchor.MiddleLeft);
            ShowPage(CurrentPage());
        }

        private GameObject Page(RectTransform root, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(root, false);
            Place(go.GetComponent<RectTransform>(), new Rect(0, 0, 1920, 1080));
            return go;
        }

        /// <summary>NUMBERS page: 16 data tiles, 4 x 4.</summary>
        private void BuildNumbers(RectTransform page)
        {
            const float left = 40f, gap = 18f;
            float tw = (1840f - 3f * gap) / 4f, th = (1080f - TopY - 70f - 3f * gap) / 4f;
            for (int i = 0; i < 16; i++)
            {
                int row = i / 4, col = i % 4;
                var r = new Rect(left + col * (tw + gap), TopY + row * (th + gap), tw, th);
                Panel(page, r, TileColor);
                captions[i] = Label(page, new Rect(r.x, r.y + 8f, r.width, 38f), Captions[i], 26, Orange, TextAnchor.MiddleCenter);
                values[i] = Label(page, new Rect(r.x, r.y + 40f, r.width, r.height - 48f), "—", 62, Color.white, TextAnchor.MiddleCenter);
            }
        }

        /// <summary>
        /// VISUAL page: the key numbers across the top and four drawings - club path and face (seen
        /// from above), the ball's spin axis, launch and attack angle (seen from the side) and the
        /// start direction with the landing spot. Small angles are drawn bigger so they can be seen;
        /// the true values are written next to each drawing.
        /// </summary>
        private void BuildVisual(RectTransform page)
        {
            string[] top = { "BALL SPEED", "CLUB SPEED", "SMASH FACTOR", "CARRY", "TOTAL" };
            float w = (1840f - 4f * 16f) / 5f;
            for (int i = 0; i < 5; i++)
            {
                var r = new Rect(40f + i * (w + 16f), TopY, w, 118f);
                Panel(page, r, TileColor);
                Label(page, new Rect(r.x, r.y + 6f, r.width, 32f), top[i], 24, Orange, TextAnchor.MiddleCenter);
                topValues[i] = Label(page, new Rect(r.x, r.y + 34f, r.width, 80f), "—", 58, Color.white, TextAnchor.MiddleCenter);
            }

            float py = TopY + 134f, ph = (1080f - py - 70f - 16f) / 2f, pw = (1840f - 16f) / 2f;
            Rect a = new Rect(40f, py, pw, ph), b = new Rect(56f + pw, py, pw, ph);
            Rect c = new Rect(40f, py + ph + 16f, pw, ph), d = new Rect(56f + pw, py + ph + 16f, pw, ph);

            // A: club path and face (top view, target straight up)
            RectTransform box = Diagram(page, a, "CLUB PATH & FACE", out RectTransform areaA);
            Line(areaA, new Vector2(0f, -100f), new Vector2(0f, 100f), 3f, new Color(1f, 1f, 1f, 0.35f));
            pathArrow = Line(areaA, Vector2.zero, Vector2.up, 8f, Orange);
            faceBar = Line(areaA, Vector2.zero, Vector2.right, 12f, new Color(0.35f, 0.70f, 1f));
            Dot(areaA, Vector2.zero, 26f, Color.white);
            pathText = Label(box, TextRect(a, 0), "", 34, Orange, TextAnchor.MiddleLeft);
            faceText = Label(box, TextRect(a, 1), "", 34, new Color(0.35f, 0.70f, 1f), TextAnchor.MiddleLeft);
            faceToPathText = Label(box, TextRect(a, 2), "", 34, Color.white, TextAnchor.MiddleLeft);

            // B: spin axis (ball seen from behind)
            box = Diagram(page, b, "SPIN AXIS", out RectTransform areaB);
            Dot(areaB, Vector2.zero, 190f, Color.white);
            spinAxis = Line(areaB, new Vector2(0f, -120f), new Vector2(0f, 120f), 6f, Red);
            axisText = Label(box, TextRect(b, 0), "", 34, Red, TextAnchor.MiddleLeft);
            totalSpinText = Label(box, TextRect(b, 1), "", 34, Color.white, TextAnchor.MiddleLeft);
            sideSpinText = Label(box, TextRect(b, 2), "", 34, Color.white, TextAnchor.MiddleLeft);
            shapeText = Label(box, TextRect(b, 3), "", 34, GolfSimZA.UI.GolfSimTheme.Gold, TextAnchor.MiddleLeft);

            // C: launch and attack angle (side view, target to the right)
            box = Diagram(page, c, "LAUNCH (SIDE VIEW)", out RectTransform areaC);
            Line(areaC, new Vector2(-120f, -60f), new Vector2(120f, -60f), 3f, new Color(0.40f, 0.80f, 0.45f, 0.9f));
            launchLine = Line(areaC, new Vector2(-40f, -52f), new Vector2(120f, -52f), 7f, Color.white);
            attackLine = Line(areaC, new Vector2(-120f, -52f), new Vector2(-40f, -52f), 7f, Orange);
            Dot(areaC, new Vector2(-40f, -48f), 20f, Color.white);
            vlaText = Label(box, TextRect(c, 0), "", 34, Color.white, TextAnchor.MiddleLeft);
            aoaText = Label(box, TextRect(c, 1), "", 34, Orange, TextAnchor.MiddleLeft);
            backSpinText = Label(box, TextRect(c, 2), "", 34, Color.white, TextAnchor.MiddleLeft);
            peakText = Label(box, TextRect(c, 3), "", 34, Color.white, TextAnchor.MiddleLeft);

            // D: start direction and landing (top view, target straight up)
            box = Diagram(page, d, "DIRECTION (TOP VIEW)", out RectTransform areaD);
            Line(areaD, new Vector2(0f, -110f), new Vector2(0f, 110f), 3f, new Color(1f, 1f, 1f, 0.35f));
            hlaLine = Line(areaD, new Vector2(0f, -100f), new Vector2(0f, 100f), 6f, Color.white);
            Dot(areaD, new Vector2(0f, -100f), 18f, Color.white);
            landingDot = Dot(areaD, new Vector2(0f, 100f), 28f, Green).rectTransform;
            hlaText = Label(box, TextRect(d, 0), "", 34, Color.white, TextAnchor.MiddleLeft);
            offlineText = Label(box, TextRect(d, 1), "", 34, Green, TextAnchor.MiddleLeft);
            carryText = Label(box, TextRect(d, 2), "", 34, Color.white, TextAnchor.MiddleLeft);
            directionShape = Label(box, TextRect(d, 3), "", 34, GolfSimZA.UI.GolfSimTheme.Gold, TextAnchor.MiddleLeft);
        }

        /// <summary>A drawing panel: title, a square drawing area on the left (returned, centre = 0,0, ±130 units).</summary>
        private RectTransform Diagram(RectTransform page, Rect r, string title, out RectTransform area)
        {
            Panel(page, r, TileColor);
            Label(page, new Rect(r.x + 20f, r.y + 8f, r.width - 40f, 36f), title, 26, Orange, TextAnchor.MiddleLeft);
            var go = new GameObject("Area", typeof(RectTransform));
            go.transform.SetParent(page, false);
            area = go.GetComponent<RectTransform>();
            float size = Mathf.Min(r.height - 50f, 280f);
            Rect ar = new Rect(r.x + 30f, r.y + 44f, size, size);
            // Fixed 280 x 280 unit drawing space, scaled to fit the area.
            Place(area, ar);
            var inner = new GameObject("Inner", typeof(RectTransform));
            inner.transform.SetParent(area, false);
            RectTransform ir = inner.GetComponent<RectTransform>();
            ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 0.5f);
            ir.sizeDelta = new Vector2(280f, 280f);
            ir.localScale = Vector3.one * (size / 280f);
            area = ir;
            return page;
        }

        /// <summary>Text line <paramref name="row"/> right of a panel's drawing.</summary>
        private static Rect TextRect(Rect panel, int row)
        {
            float x = panel.x + 30f + Mathf.Min(panel.height - 50f, 280f) + 30f;
            return new Rect(x, panel.y + 52f + row * 54f, panel.xMax - x - 20f, 50f);
        }

        /// <summary>A straight line (thin image) from a to b inside a drawing area.</summary>
        private static RectTransform Line(RectTransform parent, Vector2 a, Vector2 b, float thickness, Color color)
        {
            var go = new GameObject("Line", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            SetLine(rt, a, b, thickness);
            return rt;
        }

        private static void SetLine(RectTransform rt, Vector2 a, Vector2 b, float thickness)
        {
            Vector2 d = b - a;
            rt.anchoredPosition = a;
            rt.sizeDelta = new Vector2(Mathf.Max(1f, d.magnitude), thickness);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        private Image Dot(RectTransform parent, Vector2 centre, float size, Color color)
        {
            var go = new GameObject("Dot", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.sprite = circleSprite;
            img.color = color;
            img.raycastTarget = false;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = centre;
            return img;
        }

        private static Sprite MakeCircleSprite()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            float r = n * 0.5f - 1f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - n * 0.5f) * (x + 0.5f - n * 0.5f) + (y + 0.5f - n * 0.5f) * (y + 0.5f - n * 0.5f));
                    byte alpha = (byte)(Mathf.Clamp01(r - d + 0.5f) * 255f);
                    px[y * n + x] = new Color32(255, 255, 255, alpha);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>A clickable tab in the title bar (NUMBERS / VISUAL).</summary>
        private Image TabButton(RectTransform parent, Rect r, string text, int page, out Text label)
        {
            Image img = Panel(parent, r, new Color(0.02f, 0.18f, 0.13f, 1f));
            img.raycastTarget = true;
            Button b = img.gameObject.AddComponent<Button>();
            b.onClick.AddListener(() =>
            {
                AppSettings s = AppSettings.Current;
                s.secondScreenView = page;
                s.Save();
                ShowPage(page);
            });
            label = Label(img.rectTransform, new Rect(0, 0, 1920, 1080), text, 30, Color.white, TextAnchor.MiddleCenter);
            return img;
        }

        /// <summary>Clicks on the second screen's tabs need an event system (the game itself uses IMGUI).</summary>
        private static void EnsureEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null) return;
            var go = new GameObject("GolfSimZA_EventSystem");
            DontDestroyOnLoad(go);
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        private int shownPage = -1;

        /// <summary>The page to show now: the chosen one, or (BOTH) switching every 10 s.</summary>
        private static int CurrentPage()
        {
            int view = AppSettings.Current.secondScreenView;
            if (view == 2) return ((int)(Time.realtimeSinceStartup / 10f)) % 2;
            return Mathf.Clamp(view, 0, 1);
        }

        private void ShowPage(int page)
        {
            shownPage = page;
            numbersPage.SetActive(page == 0);
            visualPage.SetActive(page == 1);
            Color on = GolfSimZA.UI.GolfSimTheme.Accent, off = new Color(0.02f, 0.18f, 0.13f, 1f);
            numbersTab.color = page == 0 ? on : off;
            visualTab.color = page == 1 ? on : off;
        }

        /// <summary>A coloured block at a 1920×1080 position (top-left origin).</summary>
        private Image Panel(RectTransform parent, Rect r, Color color)
        {
            var go = new GameObject("Panel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            Place(go.GetComponent<RectTransform>(), r);
            return img;
        }

        private Text Label(RectTransform parent, Rect r, string text, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform));
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

            int page = CurrentPage();
            if (page != shownPage) ShowPage(page);
            clock.text = DateTime.Now.ToString("HH:mm");
            UpdateStatus();

            ShotData s = LaunchMonitorStatus.LastShot;
            bool has = LaunchMonitorStatus.HasShot;
            playerLine.text = "PLAYER  " + (ActiveClub.Player ?? "").ToUpperInvariant() + "     CLUB  " + (has && !string.IsNullOrEmpty(s.ClubName) ? s.ClubName : ActiveClub.Resolve()).ToUpperInvariant()
                              + "     SHOTS  " + LaunchMonitorStatus.ShotCount
                              + (has && LaunchMonitorStatus.LastShotTime >= 0f ? "     LAST SHOT " + Ago(Time.realtimeSinceStartup - LaunchMonitorStatus.LastShotTime) : "");

            if (page == 0) UpdateNumbers(s, has);
            else UpdateVisual(s, has);

            footer.text = "Click NUMBERS / VISUAL at the top to change the page  •  Settings → GAME → SCREENS to move or switch the second screen off";
        }

        /// <summary>R10 state: dot colour, words, battery gauge.</summary>
        private void UpdateStatus()
        {
            float since = LaunchMonitorStatus.LastPacketTime < 0f ? -1f : Time.realtimeSinceStartup - LaunchMonitorStatus.LastPacketTime;
            string state, detail;
            Color dot;
            if (!LaunchMonitorStatus.Listening)
            {
                state = "GARMIN R10  •  WAITING  •  OPEN THE RANGE OR A ROUND";
                detail = "The R10 connects when the driving range or a round is open.";
                dot = Amber;
            }
            else if (!LaunchMonitorStatus.BridgeConnected)
            {
                state = "GARMIN R10  •  BRIDGE NOT RUNNING";
                detail = "GolfSimZA starts the R10 bridge by itself - wait a few seconds.";
                dot = Red;
            }
            else if (LaunchMonitorStatus.R10Known && !LaunchMonitorStatus.R10Connected)
            {
                state = "GARMIN R10  •  NOT CONNECTED";
                detail = string.IsNullOrEmpty(LaunchMonitorStatus.R10Message) ? "Turn the R10 on (Bluetooth)." : LaunchMonitorStatus.R10Message;
                dot = Red;
            }
            else if (LaunchMonitorStatus.R10Known)
            {
                bool ready = LaunchMonitorStatus.Ready;
                state = "GARMIN R10  •  CONNECTED  •  " + (ready ? "READY - HIT AWAY" : "NOT READY");
                detail = (string.IsNullOrEmpty(LaunchMonitorStatus.R10State) ? "" : "State: " + LaunchMonitorStatus.R10State + "   •   ")
                         + "last message " + (since < 0f ? "none yet" : since < 1.5f ? "just now" : Mathf.RoundToInt(since) + " s ago");
                dot = ready ? Green : Amber;
            }
            else
            {
                state = "GARMIN R10  •  BRIDGE CONNECTED";
                detail = "Last message " + (since < 0f ? "none yet" : since < 1.5f ? "just now" : Mathf.RoundToInt(since) + " s ago") + (string.IsNullOrEmpty(LaunchMonitorStatus.LastPacket) ? "" : ":  " + LaunchMonitorStatus.LastPacket);
                dot = Amber;
            }
            statusLine.text = state;
            statusDetail.text = detail;
            statusDot.color = dot;

            // Battery
            int battery = LaunchMonitorStatus.R10Known ? LaunchMonitorStatus.Battery : -1;
            bool show = battery >= 0;
            float level = show ? battery / 100f : 0f;
            batteryFill.gameObject.SetActive(show);
            batteryFill.color = level > 0.5f ? Green : level > 0.2f ? Amber : Red;
            RectTransform fr = batteryFill.rectTransform;
            float x0 = 1410f / 1920f, x1 = (1410f + 160f * Mathf.Clamp01(Mathf.Max(level, 0.03f))) / 1920f;
            fr.anchorMin = new Vector2(x0, fr.anchorMin.y);
            fr.anchorMax = new Vector2(x1, fr.anchorMax.y);
            batteryText.text = show ? battery + "%" : "—";
            batteryText.color = show && level <= 0.2f ? Red : Color.white;
            string model = LaunchMonitorStatus.R10Model;
            deviceText.text = "BATTERY" + (string.IsNullOrEmpty(model) ? "" : "   •   " + model + (string.IsNullOrEmpty(LaunchMonitorStatus.R10Firmware) ? "" : "  fw " + LaunchMonitorStatus.R10Firmware));
        }

        private void UpdateNumbers(ShotData s, bool has)
        {
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
        }

        /// <summary>Drawn angles are exaggerated (x3, at most 45°) so a 2° path still shows.</summary>
        private static float Big(float degrees) => Mathf.Clamp(degrees * 3f, -45f, 45f);

        /// <summary>Unit vector for an angle measured from "straight up" (+ = to the right).</summary>
        private static Vector2 Up(float degrees) => new Vector2(Mathf.Sin(degrees * Mathf.Deg2Rad), Mathf.Cos(degrees * Mathf.Deg2Rad));

        private void UpdateVisual(ShotData s, bool has)
        {
            string speed = Units.SpeedUnit.ToLowerInvariant();
            string dist = Units.DistanceUnit;
            topValues[0].text = has ? Units.Speed(s.BallSpeedMps).ToString("0.0") + " " + speed : "—";
            topValues[1].text = has && s.ClubSpeedMps > 0.1f ? Units.Speed(s.ClubSpeedMps).ToString("0.0") + " " + speed : "—";
            topValues[2].text = has && s.ClubSpeedMps > 0.1f ? (s.BallSpeedMps / s.ClubSpeedMps).ToString("0.00") : "—";
            topValues[3].text = has ? Units.Distance(s.CarryMeters).ToString("0.0") + " " + dist : "—";
            topValues[4].text = has ? Units.Distance(s.TotalMeters).ToString("0.0") + " " + dist : "—";

            // A: club path (orange arrow through the ball) and the face (blue bar square to where it points).
            bool club = has && (s.HasClubPath || Mathf.Abs(s.FaceToTargetDeg) > 0.001f);
            float path = club ? s.ClubPathDeg : 0f, face = club ? s.FaceToTargetDeg : 0f;
            Vector2 pd = Up(Big(path));
            SetLine(pathArrow, -pd * 110f, pd * 110f, 8f);
            Vector2 fd = Up(Big(face));
            Vector2 faceAcross = new Vector2(fd.y, -fd.x);
            SetLine(faceBar, -faceAcross * 70f + fd * 18f, faceAcross * 70f + fd * 18f, 12f);
            pathArrow.gameObject.SetActive(club);
            faceBar.gameObject.SetActive(club);
            float faceToPath = face - path;
            pathText.text = club ? "PATH  " + Mathf.Abs(path).ToString("0.0") + "°  " + (Mathf.Abs(path) < 0.05f ? "SQUARE" : path > 0f ? "IN-TO-OUT" : "OUT-TO-IN") : "PATH  —";
            faceText.text = club ? "FACE  " + Mathf.Abs(face).ToString("0.0") + "°  " + (Mathf.Abs(face) < 0.05f ? "SQUARE" : face > 0f ? "OPEN" : "CLOSED") + " TO TARGET" : "FACE  —";
            faceToPathText.text = club ? "FACE TO PATH  " + Mathf.Abs(faceToPath).ToString("0.0") + "°  " + (Mathf.Abs(faceToPath) < 0.05f ? "SQUARE" : faceToPath > 0f ? "OPEN" : "CLOSED") : "No club data (R10 needs the club head in view)";

            // B: spin axis - the ball seen from behind, axis tilted (+ = right = fade / slice spin).
            float axis = has ? s.SpinAxisDeg : 0f;
            Vector2 ad = Up(Big(axis));
            SetLine(spinAxis, -ad * 125f, ad * 125f, 6f);
            float total = has ? Mathf.Sqrt(s.BackSpinRpm * s.BackSpinRpm + s.SideSpinRpm * s.SideSpinRpm) : 0f;
            axisText.text = has ? "SPIN AXIS  " + Side(axis, "°") : "SPIN AXIS  —";
            totalSpinText.text = has ? "TOTAL SPIN  " + total.ToString("0") + " rpm" : "TOTAL SPIN  —";
            sideSpinText.text = has ? "SIDE SPIN  " + Side(s.SideSpinRpm, " rpm", "0") : "SIDE SPIN  —";
            shapeText.text = has ? ShotShape(s.LaunchDirectionDeg, axis) : "";

            // C: launch (white, rising to the right) and attack angle (orange, club coming in).
            float vla = has ? s.LaunchAngleDeg : 0f;
            float aoa = has && s.HasClubData ? s.AttackAngleDeg : 0f;
            Vector2 ball = new Vector2(-40f, -52f);
            float vlaDraw = Mathf.Clamp(vla * 1.6f, 0f, 75f) * Mathf.Deg2Rad;
            SetLine(launchLine, ball, ball + new Vector2(Mathf.Cos(vlaDraw), Mathf.Sin(vlaDraw)) * 170f, 7f);
            float aoaDraw = Mathf.Clamp(aoa * 3f, -40f, 40f) * Mathf.Deg2Rad;
            SetLine(attackLine, ball - new Vector2(Mathf.Cos(aoaDraw), Mathf.Sin(aoaDraw)) * 90f, ball, 7f);
            attackLine.gameObject.SetActive(has && s.HasClubData);
            vlaText.text = has ? "LAUNCH ANGLE  " + vla.ToString("0.0") + "°" : "LAUNCH ANGLE  —";
            aoaText.text = has && s.HasClubData ? "ATTACK ANGLE  " + (aoa > 0f ? "+" : "") + aoa.ToString("0.0") + "°  " + (aoa >= 0f ? "UP" : "DOWN") : "ATTACK ANGLE  —";
            backSpinText.text = has ? "BACK SPIN  " + s.BackSpinRpm.ToString("0") + " rpm" : "BACK SPIN  —";
            peakText.text = has && s.IsComplete ? "PEAK HEIGHT  " + Units.Distance(s.PeakHeightMeters).ToString("0.0") + " " + dist : "PEAK HEIGHT  —";

            // D: start direction (white line from the tee) and where the ball landed (green dot).
            float hla = has ? s.LaunchDirectionDeg : 0f;
            Vector2 tee = new Vector2(0f, -100f);
            SetLine(hlaLine, tee, tee + Up(Big(hla)) * 200f, 6f);
            float offline = has && s.IsComplete ? s.OfflineMeters : 0f;
            float along = Mathf.Max(20f, has ? s.CarryMeters : 200f);
            float landAngle = Mathf.Atan2(offline, along) * Mathf.Rad2Deg;
            landingDot.anchoredPosition = tee + Up(Big(landAngle)) * 200f;
            landingDot.gameObject.SetActive(has && s.IsComplete);
            hlaText.text = has ? "START DIRECTION  " + Side(hla, "°") : "START DIRECTION  —";
            offlineText.text = has && s.IsComplete ? "OFFLINE  " + Side(Units.Distance(offline), " " + dist) : "OFFLINE  —";
            carryText.text = has ? "CARRY  " + Units.Distance(s.CarryMeters).ToString("0.0") + " " + dist + "   TOTAL  " + Units.Distance(s.TotalMeters).ToString("0.0") + " " + dist : "CARRY  —";
            directionShape.text = has ? ShotShape(hla, axis) : "";
        }

        /// <summary>Shot shape from start direction and spin axis (right-handed view): PUSH FADE, STRAIGHT DRAW ...</summary>
        private static string ShotShape(float startDeg, float axisDeg)
        {
            string start = startDeg > 2f ? "PUSH" : startDeg < -2f ? "PULL" : "STRAIGHT";
            string curve = axisDeg > 12f ? "SLICE" : axisDeg > 3f ? "FADE" : axisDeg < -12f ? "HOOK" : axisDeg < -3f ? "DRAW" : "";
            if (curve == "") return start == "STRAIGHT" ? "SHOT SHAPE  STRAIGHT" : "SHOT SHAPE  " + start;
            return "SHOT SHAPE  " + (start == "STRAIGHT" ? curve : start + " " + curve);
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
