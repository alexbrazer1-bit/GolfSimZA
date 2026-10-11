using System.Collections.Generic;
using GolfSimZA.Core;
using GolfSimZA.Platform;
using UnityEngine;

namespace GolfSimZA.UI
{
    /// <summary>
    /// SCREENS: which monitor the game runs on and the optional second screen (Garmin R10 status
    /// and swing data). With two or more monitors a picker shows when the game starts (Settings →
    /// GAME → SCREENS turns it off). The choice is saved: the game moves its window to the chosen
    /// monitor at once and opens there on the next start.
    /// Unity can only open the second screen on a monitor that is NOT the Windows main display, so
    /// with the second screen on the game always plays on the Windows main display. SWAP SCREENS
    /// (and picking the other monitor) makes that monitor the Windows main display and restarts the
    /// game. Monitors are identified through Windows by their real names - two monitors with the
    /// same resolution can no longer be mixed up.
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
        private const string SkipPickerKey = "GolfSimZA.SkipScreenPickerOnce";
        private static string startPrimaryDevice;
        private static bool restarting;
        private static string swapMessage;

        /// <summary>Last swap error (shown in the picker / settings), or null.</summary>
        public static string SwapMessage => swapMessage;

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
                var list = new List<DisplayInfo>(Monitors);
                DisplayInfo main = Screen.mainWindowDisplayInfo;
                int found = -1, count = 0;
                for (int i = 0; i < list.Count; i++)
                    if (Same(list[i], main)) { if (found < 0) found = i; count++; }
                // Two identical monitors: assume the Windows main display (where the game normally is).
                if (count > 1) { int p = PrimaryMonitor; if (p >= 0 && p < list.Count && Same(list[p], main)) return p; }
                return found >= 0 ? found : 0;
            }
        }

        // ------------------------------------------------------------ Windows monitor mapping

        private static List<WindowsDisplays.Monitor> winMonitors;
        private static int[] winMap;

        /// <summary>Reads the Windows layout and matches it to Unity's monitor list (by EDID name, then order).</summary>
        private static bool RefreshWindows()
        {
            winMonitors = null;
            winMap = null;
            if (Application.isEditor || !WindowsDisplays.IsWindows) return false;
            try
            {
                var list = new List<DisplayInfo>(Monitors);
                List<WindowsDisplays.Monitor> win = WindowsDisplays.GetMonitors();
                if (list.Count == 0 || win.Count == 0) return false;
                var names = new List<string>();
                var widths = new List<int>();
                var heights = new List<int>();
                foreach (DisplayInfo d in list) { names.Add(d.name); widths.Add(d.width); heights.Add(d.height); }
                winMonitors = win;
                winMap = WindowsDisplays.MatchUnityMonitors(names, widths, heights, win);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[GolfSimZA] Could not read the Windows monitor layout: " + e.Message);
                return false;
            }
        }

        /// <summary>The Windows monitor behind a monitor in <see cref="Monitors"/>, or null.</summary>
        public static WindowsDisplays.Monitor WindowsMonitor(int index)
        {
            if (!RefreshWindows() || index < 0 || index >= winMap.Length || winMap[index] < 0) return null;
            return winMonitors[winMap[index]];
        }

        private static string CurrentPrimaryDevice()
        {
            if (Application.isEditor || !WindowsDisplays.IsWindows) return null;
            try
            {
                foreach (WindowsDisplays.Monitor m in WindowsDisplays.GetMonitors()) if (m.Primary) return m.Device;
            }
            catch (System.Exception) { }
            return null;
        }

        /// <summary>
        /// Which monitor (index in <see cref="Monitors"/>) each Unity display is on. Unity display 0
        /// is the Windows main display; the others follow in Windows' order.
        /// </summary>
        public static int[] UnityDisplayMonitors()
        {
            int n = Display.displays.Length;
            int count = Monitors.Count;
            int primary = PrimaryMonitor;
            var others = new List<int>();
            if (RefreshWindows())
            {
                for (int w = 0; w < winMonitors.Count; w++)
                    for (int i = 0; i < winMap.Length; i++)
                        if (winMap[i] == w && i != primary) others.Add(i);
            }
            else
            {
                for (int i = 0; i < count; i++) if (i != primary) others.Add(i);
            }
            var map = new int[n];
            for (int u = 0; u < n; u++)
                map[u] = u == 0 ? primary : (u - 1 < others.Count ? others[u - 1] : Mathf.Min(u, Mathf.Max(0, count - 1)));
            return map;
        }

        /// <summary>The Windows main display (index in <see cref="Monitors"/>).</summary>
        public static int PrimaryMonitor
        {
            get
            {
                if (RefreshWindows())
                    for (int i = 0; i < winMap.Length; i++)
                        if (winMap[i] >= 0 && winMonitors[winMap[i]].Primary) return i;
                // Without the Windows layout: Unity lists the Windows main display first.
                return 0;
            }
        }

        /// <summary>True when the screens can be swapped (Windows player, two or more monitors).</summary>
        public static bool CanSwap => !Application.isEditor && WindowsDisplays.IsWindows && Monitors.Count >= 2;

        /// <summary>
        /// Makes a monitor (index in <see cref="Monitors"/>) the Windows main display and restarts the
        /// game, which then plays there with the second screen on another monitor. Returns null when
        /// the restart has started, else the reason it could not.
        /// </summary>
        public static string MakeMainDisplayAndRestart(int index, string why)
        {
            if (!CanSwap) return "Swapping needs the Windows game and two monitors.";
            WindowsDisplays.Monitor m = WindowsMonitor(index);
            if (m == null) return "Could not identify DISPLAY " + (index + 1) + ".";
            if (!m.Primary)
            {
                string err = WindowsDisplays.SetPrimary(m.Device);
                if (err != null)
                {
                    swapMessage = err;
                    Debug.LogWarning("[GolfSimZA] " + why + ": " + err);
                    return err;
                }
            }
            swapMessage = null;
            startPrimaryDevice = m.Device;
            Debug.Log("[GolfSimZA] " + why + ": " + m.Device + " (" + m.FriendlyName + ") is now the Windows main display - restarting the game.  windows: " + WindowsDisplays.Describe());
            RestartGame();
            return null;
        }

        /// <summary>
        /// SWAP SCREENS: the game and the swing data change monitors. The monitor showing the swing
        /// data (or the first other monitor) becomes the Windows main display and the game restarts.
        /// </summary>
        public static string SwapScreens()
        {
            int count = Monitors.Count;
            if (count < 2) return "Only one monitor found.";
            int primary = PrimaryMonitor;
            int target = -1;
            int data = GolfSimZA.Visual.SecondScreen.ActiveDisplay;
            if (data >= 0) target = MonitorOfUnityDisplay(data);
            if (target < 0 || target == primary)
                for (int i = 0; i < count; i++) if (i != primary) { target = i; break; }
            AppSettings s = AppSettings.Current;
            s.gameDisplay = -1;
            s.Save();
            return MakeMainDisplayAndRestart(target, "Swap screens");
        }

        /// <summary>Starts a new copy of the game (after this one has closed) and quits this one.</summary>
        public static void RestartGame()
        {
            if (restarting) return;
            restarting = true;
            PlayerPrefs.SetInt(SkipPickerKey, 1);
            // Let the new start open on the Windows main display (Unity's monitor numbers can change).
            PlayerPrefs.DeleteKey("UnitySelectMonitor");
            PlayerPrefs.Save();
            try
            {
                string exe = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c ping -n 5 127.0.0.1 >nul & start \"\" \"" + exe + "\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[GolfSimZA] Could not start the game again automatically: " + e.Message);
            }
            Application.Quit();
        }

        // workArea is relative to each monitor (0,0 for both on the simulator PC), so it cannot tell monitors apart.
        private static bool Same(DisplayInfo a, DisplayInfo b) =>
            a.name == b.name && a.width == b.width && a.height == b.height;

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
            startPrimaryDevice = CurrentPrimaryDevice();
            bool skipPicker = PlayerPrefs.GetInt(SkipPickerKey, 0) == 1;
            if (skipPicker) { PlayerPrefs.DeleteKey(SkipPickerKey); PlayerPrefs.Save(); }
            Debug.Log("[GolfSimZA] Screens: " + monitors + " monitor(s), Unity displays " + Display.displays.Length + ", game window on display " + (CurrentGameMonitor + 1) + ", Windows main display = display " + (PrimaryMonitor + 1) + DescribeLayout());
            if (monitors > 1 && s.askDisplayOnLaunch && !Application.isEditor && !skipPicker)
            {
                showPicker = true;
                pickGame = s.gameDisplay >= 0 && s.gameDisplay < monitors ? s.gameDisplay : CurrentGameMonitor;
                pickSecond = s.secondScreen ? 1 : 0;
                pickAsk = true;
                PlaceForPicker(s.secondScreen ? PrimaryMonitor : pickGame);
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
            string win = Application.isEditor ? "" : "  |  windows: " + WindowsDisplays.Describe();
            return "  |  monitors: " + string.Join("; ", parts) + "  |  unity displays: " + string.Join("; ", unity) + win;
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
                        FocusGameSoon();
                    };
                    return;
                }
            }
            FullScreenOn(target);
            GolfSimZA.Visual.SecondScreen.Apply();
            FocusGameSoon();
        }

        /// <summary>The picker shows full screen on the monitor the game will most likely use (not a small window).</summary>
        private static void PlaceForPicker(int monitor)
        {
            if (Application.isEditor || instance == null) return;
            instance.StartCoroutine(instance.PlacePickerRoutine(monitor));
        }

        // Unity ignores window moves in the very first frame, so wait a moment, move, wait until done, then go full screen.
        private System.Collections.IEnumerator PlacePickerRoutine(int monitor)
        {
            yield return null;
            yield return new WaitForSecondsRealtime(0.3f);
            var list = new List<DisplayInfo>(Monitors);
            if (list.Count == 0) yield break;
            monitor = Mathf.Clamp(monitor, 0, list.Count - 1);
            DisplayInfo target = list[monitor];
            if (monitor != CurrentGameMonitor)
            {
                AsyncOperation move = Screen.MoveMainWindowTo(target, Vector2Int.zero);
                float until = Time.realtimeSinceStartup + 3f;
                while (move != null && !move.isDone && Time.realtimeSinceStartup < until) yield return null;
            }
            FullScreenOn(target);
            yield return new WaitForSecondsRealtime(0.5f);
            Debug.Log("[GolfSimZA] Screens picker on display " + (CurrentGameMonitor + 1) + " (wanted " + (monitor + 1) + "), " + Screen.fullScreenMode + " " + Screen.width + "x" + Screen.height);
            FocusGameSoon();
        }

        /// <summary>
        /// Brings the game window to the front a moment after it has been placed, so it covers the
        /// Windows taskbar (needed after a restart or when started by SimLink).
        /// </summary>
        public static void FocusGameSoon()
        {
            if (instance == null || Application.isEditor || !WindowsDisplays.IsWindows) return;
            instance.StartCoroutine(instance.FocusRoutine());
        }

        private System.Collections.IEnumerator FocusRoutine()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            string err = null;
            try { err = WindowsDisplays.FocusWindow(System.IntPtr.Zero); }
            catch (System.Exception e) { err = e.Message; }
            if (err != null) Debug.Log("[GolfSimZA] Bring game to front: " + err);
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
            if (restarting || Time.unscaledTime < nextLayoutCheck) return;
            nextLayoutCheck = Time.unscaledTime + 2f;
            // The Windows main display was changed (Windows Settings, SimLink ...) while the second
            // screen is open: Unity cannot move its second window, so restart to place both again.
            string primaryNow = CurrentPrimaryDevice();
            if (AppSettings.Current.secondScreen && startPrimaryDevice != null && primaryNow != null && primaryNow != startPrimaryDevice && Monitors.Count > 1)
            {
                Debug.Log("[GolfSimZA] Windows main display changed (" + startPrimaryDevice + " -> " + primaryNow + ") - restarting so the game and the swing data swap screens." + DescribeLayout());
                startPrimaryDevice = primaryNow;
                RestartGame();
                return;
            }
            if (showPicker) return;
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
            if (GUI.Button(new Rect(x + cw * 0.5f + 5f, y, cw * 0.5f - 5f, 48f), "ON  •  " + OtherLabel(pickGame), pickSecond == 1 ? cardActive : cardStyle)) pickSecond = 1;
            y += 54f;
            string note = null;
            if (swapMessage != null) note = "Could not swap the screens: " + swapMessage;
            else if (pickSecond == 1 && pickGame != PrimaryMonitor && CanSwap)
                note = "START makes DISPLAY " + (pickGame + 1) + " the Windows main display and restarts the game (about 10 s) - then the game plays there and the swing data goes on the other screen.";
            if (note != null)
                GUI.Label(new Rect(x, y, cw, 36f), note, new GUIStyle(GolfSimTheme.Label) { wordWrap = true, fontSize = 11, normal = { textColor = GolfSimTheme.Gold } });
            y += 40f;

            if (GUI.Button(new Rect(x, y, cw, 36f), (pickAsk ? "☑" : "☐") + "  ASK EVERY TIME THE GAME STARTS  (change later in Settings → GAME → SCREENS)", GolfSimTheme.GhostButton)) pickAsk = !pickAsk;
            y += 52f;

            if (GUI.Button(new Rect(x, y, cw, 54f), "START", new GUIStyle(GolfSimTheme.AccentButton) { fontSize = 20 }))
            {
                AppSettings s = AppSettings.Current;
                s.gameDisplay = pickGame;
                s.secondScreen = pickSecond == 1;
                s.askDisplayOnLaunch = pickAsk;
                if (pickSecond == 1 && pickGame != PrimaryMonitor && CanSwap)
                {
                    s.gameDisplay = -1;
                    s.Save();
                    if (MakeMainDisplayAndRestart(pickGame, "Screens picker") == null) { showPicker = false; return; }
                }
                else
                {
                    s.Save();
                }
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

        /// <summary>Called by Settings: swaps the screens and returns the error (null = restarting).</summary>
        public static string SwapFromSettings() => SwapScreens();

        /// <summary>Monitor (index in <see cref="Monitors"/>) a Unity display is on.</summary>
        public static int MonitorOfUnityDisplay(int unityDisplay)
        {
            int[] map = UnityDisplayMonitors();
            return unityDisplay >= 0 && unityDisplay < map.Length ? map[unityDisplay] : unityDisplay;
        }
    }
}
