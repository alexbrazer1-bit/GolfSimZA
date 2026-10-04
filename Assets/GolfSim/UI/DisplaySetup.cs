using System.Collections.Generic;
using GolfSimZA.Core;
using UnityEngine;

namespace GolfSimZA.UI
{
    /// <summary>
    /// SCREENS: which monitor the game runs on and the optional second screen (Garmin R10 status
    /// and swing data). With two or more monitors a picker shows when the game starts (Settings →
    /// GAME → SCREENS turns it off). The choice is saved: the game moves its window to the chosen
    /// monitor at once and opens there on the next start.
    /// </summary>
    public sealed class DisplaySetup : MonoBehaviour
    {
        private static readonly List<DisplayInfo> layout = new List<DisplayInfo>();
        private static DisplaySetup instance;
        private bool showPicker;
        private int pickGame, pickSecond;
        private bool pickAsk;
        private GUIStyle cardStyle, cardActive;
        private static Texture2D dim;

        /// <summary>Monitors Windows reports (name, size), in its order: display 1, display 2 ...</summary>
        public static IReadOnlyList<DisplayInfo> Monitors
        {
            get
            {
                layout.Clear();
                try { Screen.GetDisplayLayout(layout); } catch (System.Exception) { }
                return layout;
            }
        }

        public static int MonitorCount => Mathf.Max(Monitors.Count, Display.displays.Length);

        /// <summary>Index (in <see cref="Monitors"/>) of the monitor the game window is on now.</summary>
        public static int CurrentGameMonitor
        {
            get
            {
                IReadOnlyList<DisplayInfo> list = Monitors;
                DisplayInfo main = Screen.mainWindowDisplayInfo;
                for (int i = 0; i < list.Count; i++)
                    if (Same(list[i], main)) return i;
                return 0;
            }
        }

        /// <summary>
        /// Which monitor (index in <see cref="Monitors"/>) each Unity display is on. Unity display 0
        /// is the Windows main display; the others follow in Windows' order. Matched by resolution
        /// (and order when two monitors have the same size).
        /// </summary>
        public static int[] UnityDisplayMonitors()
        {
            IReadOnlyList<DisplayInfo> list = Monitors;
            int n = Display.displays.Length;
            var map = new int[n];
            var used = new bool[Mathf.Max(1, list.Count)];
            for (int u = 0; u < n; u++)
            {
                map[u] = -1;
                int w = Display.displays[u].systemWidth, h = Display.displays[u].systemHeight;
                for (int m = 0; m < list.Count; m++)
                    if (!used[m] && list[m].width == w && list[m].height == h) { map[u] = m; break; }
                if (map[u] < 0)
                    for (int m = 0; m < list.Count; m++)
                        if (!used[m]) { map[u] = m; break; }
                if (map[u] >= 0) used[map[u]] = true;
                else map[u] = Mathf.Min(u, Mathf.Max(0, list.Count - 1));
            }
            return map;
        }

        /// <summary>The Windows main display (index in <see cref="Monitors"/>).</summary>
        public static int PrimaryMonitor
        {
            get
            {
                int[] map = UnityDisplayMonitors();
                return map.Length > 0 ? Mathf.Max(0, map[0]) : 0;
            }
        }

        private static bool Same(DisplayInfo a, DisplayInfo b) =>
            a.name == b.name && a.width == b.width && a.height == b.height && a.workArea.x == b.workArea.x && a.workArea.y == b.workArea.y;

        public static string MonitorLabel(int i)
        {
            IReadOnlyList<DisplayInfo> list = Monitors;
            if (i < 0 || i >= list.Count) return "DISPLAY " + (i + 1);
            DisplayInfo d = list[i];
            string name = string.IsNullOrWhiteSpace(d.name) ? "" : "  •  " + d.name;
            return "DISPLAY " + (i + 1) + "  (" + d.width + " × " + d.height + ")" + name;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject("GolfSimZA_DisplaySetup");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<DisplaySetup>();
        }

        private void Start()
        {
            AppSettings s = AppSettings.Current;
            int monitors = Monitors.Count;
            Debug.Log("[GolfSimZA] Screens: " + monitors + " monitor(s), Unity displays " + Display.displays.Length + ", game window on display " + (CurrentGameMonitor + 1) + DescribeLayout());
            if (monitors > 1 && s.askDisplayOnLaunch && !Application.isEditor)
            {
                showPicker = true;
                pickGame = s.gameDisplay >= 0 && s.gameDisplay < monitors ? s.gameDisplay : CurrentGameMonitor;
                pickSecond = s.secondScreen ? 1 : 0;
                pickAsk = true;
            }
            else
            {
                Apply(false);
            }
        }

        private static string DescribeLayout()
        {
            var parts = new List<string>();
            IReadOnlyList<DisplayInfo> list = Monitors;
            for (int i = 0; i < list.Count; i++)
                parts.Add((i + 1) + "=" + list[i].name + " " + list[i].width + "x" + list[i].height + " at " + list[i].workArea.x + "," + list[i].workArea.y);
            var unity = new List<string>();
            for (int i = 0; i < Display.displays.Length; i++)
                unity.Add(i + "=" + Display.displays[i].systemWidth + "x" + Display.displays[i].systemHeight + (Display.displays[i].active ? " active" : ""));
            return "  |  monitors: " + string.Join("; ", parts) + "  |  unity displays: " + string.Join("; ", unity);
        }

        /// <summary>
        /// The monitor the game really uses. With the second screen on it is always display 1 (the
        /// Windows main display): Unity can only open extra screens on the other monitors, never on
        /// the main one, so the game has to stay on the main display for the second screen to work.
        /// </summary>
        public static int EffectiveGameDisplay(AppSettings s)
        {
            int count = Monitors.Count;
            if (count < 2) return 0;
            // The main display - Windows' own one, which is not always first in the monitor list.
            if (s.secondScreen) return PrimaryMonitor;
            return s.gameDisplay >= 0 && s.gameDisplay < count ? s.gameDisplay : CurrentGameMonitor;
        }

        /// <summary>Moves the game to the chosen monitor (full screen at that monitor's size) and opens / closes the second screen.</summary>
        public static void Apply(bool fromSettings)
        {
            AppSettings s = AppSettings.Current;
            IReadOnlyList<DisplayInfo> list = Monitors;
            if (Application.isEditor || list.Count == 0)
            {
                GolfSimZA.Visual.SecondScreen.Apply();
                return;
            }
            int game = EffectiveGameDisplay(s);
            // Opens on this monitor next time too (the Windows player reads this at start-up).
            PlayerPrefs.SetInt("UnitySelectMonitor", game);
            PlayerPrefs.Save();
            DisplayInfo target = list[Mathf.Clamp(game, 0, list.Count - 1)];
            if (game != CurrentGameMonitor)
            {
                AsyncOperation move = Screen.MoveMainWindowTo(target, Vector2Int.zero);
                if (move != null)
                {
                    move.completed += _ =>
                    {
                        FullScreenOn(target);
                        Debug.Log("[GolfSimZA] Game moved to display " + (game + 1) + DescribeLayout());
                        GolfSimZA.Visual.SecondScreen.Apply();
                    };
                    return;
                }
            }
            FullScreenOn(target);
            GolfSimZA.Visual.SecondScreen.Apply();
        }

        /// <summary>Full screen (borderless) at the monitor's own resolution - a 1920×1080 window on a smaller monitor would hang over the edge.</summary>
        private static void FullScreenOn(DisplayInfo monitor)
        {
            if (monitor.width <= 0 || monitor.height <= 0) return;
            if (Screen.fullScreenMode == FullScreenMode.FullScreenWindow && Screen.width == monitor.width && Screen.height == monitor.height) return;
            Screen.SetResolution(monitor.width, monitor.height, FullScreenMode.FullScreenWindow);
        }

        private string layoutSignature;
        private float nextLayoutCheck;

        /// <summary>
        /// Watches the monitors: when the game's monitor or the Windows main display changes (or a
        /// monitor is added / removed), the game and the second screen are placed again so the
        /// club data is always on a monitor the game is not using.
        /// </summary>
        private void Update()
        {
            if (showPicker || Time.unscaledTime < nextLayoutCheck) return;
            nextLayoutCheck = Time.unscaledTime + 2f;
            string sig = LayoutSignature();
            if (layoutSignature == null) { layoutSignature = sig; return; }
            if (sig == layoutSignature) return;
            layoutSignature = sig;
            Debug.Log("[GolfSimZA] Screens changed - placing the game and the second screen again." + DescribeLayout());
            Apply(false);
        }

        private static string LayoutSignature()
        {
            var sb = new System.Text.StringBuilder();
            IReadOnlyList<DisplayInfo> list = Monitors;
            sb.Append(list.Count).Append('|').Append(CurrentGameMonitor).Append('|').Append(PrimaryMonitor);
            foreach (DisplayInfo d in list) sb.Append('|').Append(d.name).Append(d.width).Append('x').Append(d.height);
            return sb.ToString();
        }

        private void OnGUI()
        {
            if (!showPicker) return;
            GolfSimTheme.Ensure();
            GUI.depth = -6000;
            if (dim == null) dim = GolfSimTheme.Tex(new Color(0.01f, 0.03f, 0.04f, 0.94f));
            if (cardStyle == null)
            {
                cardStyle = new GUIStyle(GolfSimTheme.Button) { fontSize = 16, wordWrap = true, alignment = TextAnchor.MiddleCenter };
                cardActive = new GUIStyle(GolfSimTheme.AccentButton) { fontSize = 16, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            }
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), dim);

            int monitors = Monitors.Count;
            float w = Mathf.Min(760f, Screen.width - 40f);
            float h = 500f;
            Rect panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.Box(panel, GUIContent.none, GolfSimTheme.Overlay);
            float x = panel.x + 28f, cw = w - 56f, y = panel.y + 22f;
            GUI.Label(new Rect(x, y, cw, 36f), "CHOOSE YOUR SCREENS", new GUIStyle(GolfSimTheme.Title) { alignment = TextAnchor.MiddleCenter, fontSize = 26 });
            y += 50f;

            GUI.Label(new Rect(x, y, cw, 22f), "PLAY THE GAME ON", GolfSimTheme.Label);
            y += 26f;
            float bw = (cw - 10f * (monitors - 1)) / monitors;
            for (int i = 0; i < monitors; i++)
                if (GUI.Button(new Rect(x + i * (bw + 10f), y, bw, 64f), MonitorLabel(i), pickGame == i ? cardActive : cardStyle)) pickGame = i;
            y += 80f;

            GUI.Label(new Rect(x, y, cw, 22f), "SECOND SCREEN  (Garmin R10 status and swing data on the other monitor)", GolfSimTheme.Label);
            y += 26f;
            if (GUI.Button(new Rect(x, y, cw * 0.5f - 5f, 48f), "OFF", pickSecond == 0 ? cardActive : cardStyle)) pickSecond = 0;
            if (GUI.Button(new Rect(x + cw * 0.5f + 5f, y, cw * 0.5f - 5f, 48f), "ON  •  " + OtherLabel(pickSecond == 1 ? PrimaryMonitor : pickGame), pickSecond == 1 ? cardActive : cardStyle)) pickSecond = 1;
            y += 54f;
            if (pickSecond == 1 && pickGame != PrimaryMonitor)
                GUI.Label(new Rect(x, y, cw, 36f), "With the second screen on, the game plays on DISPLAY " + (PrimaryMonitor + 1) + " (the Windows main display) and the swing data goes on the other screen. To swap them, make the other monitor the main display in Windows Settings → Display.",
                    new GUIStyle(GolfSimTheme.Label) { wordWrap = true, fontSize = 11, normal = { textColor = GolfSimTheme.Gold } });
            y += 40f;

            if (GUI.Button(new Rect(x, y, cw, 36f), (pickAsk ? "☑" : "☐") + "  ASK EVERY TIME THE GAME STARTS  (change later in Settings → GAME → SCREENS)", GolfSimTheme.GhostButton)) pickAsk = !pickAsk;
            y += 52f;

            if (GUI.Button(new Rect(x, y, cw, 54f), "START", new GUIStyle(GolfSimTheme.AccentButton) { fontSize = 20 }))
            {
                AppSettings s = AppSettings.Current;
                s.gameDisplay = pickGame;
                s.secondScreen = pickSecond == 1;
                s.askDisplayOnLaunch = pickAsk;
                s.Save();
                showPicker = false;
                Apply(false);
            }

            if (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp || Event.current.type == EventType.KeyDown)
                Event.current.Use();
        }

        private static string OtherLabel(int game)
        {
            int other = game == 0 ? 1 : 0;
            return "DISPLAY " + (other + 1);
        }

        /// <summary>Monitor (index in <see cref="Monitors"/>) a Unity display is on.</summary>
        public static int MonitorOfUnityDisplay(int unityDisplay)
        {
            int[] map = UnityDisplayMonitors();
            return unityDisplay >= 0 && unityDisplay < map.Length ? map[unityDisplay] : unityDisplay;
        }
    }
}
