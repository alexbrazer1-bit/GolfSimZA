using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace GolfSimZA.Platform
{
    /// <summary>
    /// The real Windows monitor layout (which monitor is the Windows main display, device names,
    /// positions and the EDID names Unity shows) and switching the Windows main display.
    /// Unity cannot tell two monitors with the same resolution apart and can only open its second
    /// screen on a monitor that is NOT the Windows main display - so swapping the game and the
    /// swing-data screen means making the other monitor the main display (exactly what
    /// Windows Settings > Display > "Make this my main display" does) and restarting the game.
    /// Plain C# (no Unity types) so the same file is also used by SimLink's remote "swap screens".
    /// </summary>
    public static class WindowsDisplays
    {
        public sealed class Monitor
        {
            public string Device;        // \\.\DISPLAY1
            public string FriendlyName;  // DELL S2330MX (the name Unity reports)
            public int X, Y, Width, Height;
            public bool Primary;
            public override string ToString()
            {
                return Device + " '" + FriendlyName + "' " + Width + "x" + Height + " at " + X + "," + Y + (Primary ? " MAIN" : "");
            }
        }

        public static bool IsWindows
        {
            get { return Environment.OSVersion.Platform == PlatformID.Win32NT; }
        }

        /// <summary>All active monitors in Windows' order (the order Unity's display layout uses).</summary>
        public static List<Monitor> GetMonitors()
        {
            var list = new List<Monitor>();
            if (!IsWindows) return list;
            Dictionary<string, string> names = FriendlyNames();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate (IntPtr hMon, IntPtr hdc, ref RECT r, IntPtr data)
            {
                var mi = new MONITORINFOEX();
                mi.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
                if (GetMonitorInfo(hMon, ref mi))
                {
                    var m = new Monitor();
                    m.Device = mi.szDevice;
                    m.Primary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0;
                    // Physical position/size from the display settings (not DPI-scaled).
                    var dm = NewDevMode();
                    if (EnumDisplaySettings(mi.szDevice, ENUM_CURRENT_SETTINGS, ref dm))
                    {
                        m.X = dm.dmPositionX; m.Y = dm.dmPositionY; m.Width = dm.dmPelsWidth; m.Height = dm.dmPelsHeight;
                    }
                    else
                    {
                        m.X = mi.rcMonitor.left; m.Y = mi.rcMonitor.top; m.Width = mi.rcMonitor.right - mi.rcMonitor.left; m.Height = mi.rcMonitor.bottom - mi.rcMonitor.top;
                    }
                    string fn;
                    m.FriendlyName = names.TryGetValue(m.Device.ToUpperInvariant(), out fn) ? fn : "";
                    list.Add(m);
                }
                return true;
            }, IntPtr.Zero);
            return list;
        }

        /// <summary>
        /// For each monitor in Unity's list (by name and size, in order) the matching Windows monitor
        /// index, or -1. Names are used first; identical monitors are matched in order.
        /// </summary>
        public static int[] MatchUnityMonitors(IList<string> unityNames, IList<int> unityWidths, IList<int> unityHeights, List<Monitor> win)
        {
            int n = unityNames.Count;
            var map = new int[n];
            var used = new bool[win.Count];
            for (int i = 0; i < n; i++)
            {
                map[i] = -1;
                for (int w = 0; w < win.Count; w++)
                    if (!used[w] && !string.IsNullOrEmpty(win[w].FriendlyName) && string.Equals(win[w].FriendlyName.Trim(), (unityNames[i] ?? "").Trim(), StringComparison.OrdinalIgnoreCase)
                        && win[w].Width == unityWidths[i] && win[w].Height == unityHeights[i]) { map[i] = w; break; }
                if (map[i] >= 0) used[map[i]] = true;
            }
            // Anything not matched by name (identical monitors): Unity lists the Windows main
            // display first, then the others in Windows' order.
            for (int i = 0; i < n; i++)
            {
                if (map[i] >= 0) continue;
                int pick = -1;
                if (i == 0)
                    for (int w = 0; w < win.Count; w++) if (!used[w] && win[w].Primary) { pick = w; break; }
                if (pick < 0)
                    for (int w = 0; w < win.Count; w++) if (!used[w]) { pick = w; break; }
                map[i] = pick;
                if (pick >= 0) used[pick] = true;
            }
            return map;
        }

        /// <summary>
        /// Makes <paramref name="device"/> the Windows main display (moves every monitor so the new
        /// main one is at 0,0 - the layout stays the same). Returns null when done, else the reason.
        /// </summary>
        public static string SetPrimary(string device)
        {
            if (!IsWindows) return "Not Windows.";
            List<Monitor> mons = GetMonitors();
            Monitor target = null;
            foreach (Monitor m in mons) if (string.Equals(m.Device, device, StringComparison.OrdinalIgnoreCase)) target = m;
            if (target == null) return "Monitor " + device + " not found.";
            if (target.Primary) return null;
            int dx = target.X, dy = target.Y;
            // The new main display first, then the others, all staged; applied together at the end.
            var order = new List<Monitor>();
            order.Add(target);
            foreach (Monitor m in mons) if (m != target) order.Add(m);
            foreach (Monitor m in order)
            {
                var dm = NewDevMode();
                if (!EnumDisplaySettings(m.Device, ENUM_CURRENT_SETTINGS, ref dm)) return "Could not read the settings of " + m.Device + ".";
                dm.dmFields = DM_POSITION;
                dm.dmPositionX = m.X - dx;
                dm.dmPositionY = m.Y - dy;
                uint flags = CDS_UPDATEREGISTRY | CDS_NORESET;
                if (m == target) flags |= CDS_SET_PRIMARY;
                int r = ChangeDisplaySettingsEx(m.Device, ref dm, IntPtr.Zero, flags, IntPtr.Zero);
                if (r != DISP_CHANGE_SUCCESSFUL) return "Windows refused the change for " + m.Device + " (code " + r + ").";
            }
            int apply = ChangeDisplaySettingsEx(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
            if (apply != DISP_CHANGE_SUCCESSFUL) return "Windows could not apply the new main display (code " + apply + ").";
            return null;
        }

        /// <summary>Two monitors: makes the one that is not the main display the main display.</summary>
        public static string SwapPrimaryOfTwo()
        {
            List<Monitor> mons = GetMonitors();
            if (mons.Count < 2) return "Only one monitor found.";
            foreach (Monitor m in mons) if (!m.Primary) return SetPrimary(m.Device);
            return "No second monitor found.";
        }

        /// <summary>
        /// Brings a window to the front so the full-screen game covers the Windows taskbar (after a
        /// restart Windows does not give the new game window the focus by itself).
        /// </summary>
        public static string FocusWindow(IntPtr hwnd)
        {
            if (!IsWindows) return "Not Windows.";
            if (hwnd == IntPtr.Zero) hwnd = FindOwnWindowOnMainDisplay();
            if (hwnd == IntPtr.Zero) return "Game window not found.";
            // Windows only lets the program the user last used take the focus; a harmless ALT tap lifts that lock.
            keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
            bool ok = SetForegroundWindow(hwnd);
            keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            BringWindowToTop(hwnd);
            return ok ? null : "Windows did not bring the game to the front.";
        }

        /// <summary>A visible window of this program on the Windows main display (the game's own window).</summary>
        public static IntPtr FindOwnWindowOnMainDisplay()
        {
            uint pid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            IntPtr first = IntPtr.Zero, onMain = IntPtr.Zero;
            EnumWindows(delegate (IntPtr h, IntPtr data)
            {
                uint wpid;
                GetWindowThreadProcessId(h, out wpid);
                if (wpid != pid || !IsWindowVisible(h)) return true;
                if (first == IntPtr.Zero) first = h;
                var mi = new MONITORINFOEX();
                mi.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
                if (GetMonitorInfo(MonitorFromWindow(h, MONITOR_DEFAULTTONEAREST), ref mi) && (mi.dwFlags & MONITORINFOF_PRIMARY) != 0)
                {
                    onMain = h;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return onMain != IntPtr.Zero ? onMain : first;
        }

        public static string Describe()
        {
            var parts = new List<string>();
            foreach (Monitor m in GetMonitors()) parts.Add(m.ToString());
            return string.Join("; ", parts.ToArray());
        }

        // ------------------------------------------------------------------ EDID names (DisplayConfig API)

        private static Dictionary<string, string> FriendlyNames()
        {
            var result = new Dictionary<string, string>();
            try
            {
                uint pathCount, modeCount;
                if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out pathCount, out modeCount) != 0) return result;
                var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
                IntPtr modes = Marshal.AllocHGlobal((int)Math.Max(1, modeCount) * 64);
                try
                {
                    if (QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0) return result;
                }
                finally { Marshal.FreeHGlobal(modes); }
                for (int i = 0; i < pathCount; i++)
                {
                    var src = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
                    src.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
                    src.header.size = Marshal.SizeOf(typeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME));
                    src.header.adapterId = paths[i].sourceInfo.adapterId;
                    src.header.id = paths[i].sourceInfo.id;
                    if (DisplayConfigGetDeviceInfo(ref src) != 0) continue;
                    var tgt = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
                    tgt.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
                    tgt.header.size = Marshal.SizeOf(typeof(DISPLAYCONFIG_TARGET_DEVICE_NAME));
                    tgt.header.adapterId = paths[i].targetInfo.adapterId;
                    tgt.header.id = paths[i].targetInfo.id;
                    if (DisplayConfigGetDeviceInfo(ref tgt) != 0) continue;
                    if (!string.IsNullOrEmpty(src.viewGdiDeviceName))
                        result[src.viewGdiDeviceName.ToUpperInvariant()] = tgt.monitorFriendlyDeviceName ?? "";
                }
            }
            catch (Exception) { }
            return result;
        }

        // ------------------------------------------------------------------ Win32

        private const int ENUM_CURRENT_SETTINGS = -1;
        private const byte VK_MENU = 0x12;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const uint MONITORINFOF_PRIMARY = 1;
        private const uint DM_POSITION = 0x00000020;
        private const uint CDS_UPDATEREGISTRY = 0x00000001;
        private const uint CDS_SET_PRIMARY = 0x00000010;
        private const uint CDS_NORESET = 0x10000000;
        private const int DISP_CHANGE_SUCCESSFUL = 0;
        private const uint QDC_ONLY_ACTIVE_PATHS = 2;
        private const int DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1;
        private const int DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public ushort dmSpecVersion;
            public ushort dmDriverVersion;
            public ushort dmSize;
            public ushort dmDriverExtra;
            public uint dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public uint dmDisplayOrientation;
            public uint dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public ushort dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        private static DEVMODE NewDevMode()
        {
            var dm = new DEVMODE();
            dm.dmDeviceName = new string('\0', 32);
            dm.dmFormName = new string('\0', 32);
            dm.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));
            return dm;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID { public uint LowPart; public int HighPart; }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_SOURCE_INFO { public LUID adapterId; public uint id; public uint modeInfoIdx; public uint statusFlags; }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_RATIONAL { public uint Numerator; public uint Denominator; }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_TARGET_INFO
        {
            public LUID adapterId; public uint id; public uint modeInfoIdx; public uint outputTechnology; public uint rotation;
            public uint scaling; public DISPLAYCONFIG_RATIONAL refreshRate; public uint scanLineOrdering; public int targetAvailable; public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_INFO { public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo; public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo; public uint flags; }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_DEVICE_INFO_HEADER { public int type; public int size; public LUID adapterId; public uint id; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string viewGdiDeviceName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DISPLAYCONFIG_TARGET_DEVICE_NAME
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint flags;
            public uint outputTechnology;
            public ushort edidManufactureId;
            public ushort edidProductCodeId;
            public uint connectorInstance;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string monitorFriendlyDeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string monitorDevicePath;
        }

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data);
        private delegate bool WindowEnumProc(IntPtr hwnd, IntPtr data);

        [DllImport("user32.dll")] private static extern bool EnumWindows(WindowEnumProc proc, IntPtr data);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

        [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX info);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool EnumDisplaySettings(string device, int mode, ref DEVMODE dm);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int ChangeDisplaySettingsEx(string device, ref DEVMODE dm, IntPtr hwnd, uint flags, IntPtr param);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int ChangeDisplaySettingsEx(string device, IntPtr dm, IntPtr hwnd, uint flags, IntPtr param);
        [DllImport("user32.dll")] private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPaths, out uint numModes);
        [DllImport("user32.dll")] private static extern int QueryDisplayConfig(uint flags, ref uint numPaths, [Out] DISPLAYCONFIG_PATH_INFO[] paths, ref uint numModes, IntPtr modes, IntPtr topology);
        [DllImport("user32.dll")] private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME info);
        [DllImport("user32.dll")] private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME info);
    }
}
