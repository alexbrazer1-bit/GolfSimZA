using UnityEngine;

namespace GolfSimZA.Core
{
    /// <summary>
    /// Live launch-monitor state for the second screen: is the Garmin R10 receiver listening, is
    /// the R10 bridge connected, is the R10 ready / does it see a ball, and the latest shot (updated
    /// while the ball flies and once more when it stops). Written by the R10 adapter and the
    /// simulator controller on the main thread.
    /// </summary>
    public static class LaunchMonitorStatus
    {
        public static string DeviceName = "Garmin Approach R10";
        /// <summary>The GolfSimZA receiver (TCP port 921) is open - only while a round or the range is running.</summary>
        public static bool Listening;
        public static int ListenPort = 921;
        /// <summary>The R10 bridge program is connected to the receiver.</summary>
        public static bool BridgeConnected;
        /// <summary>The R10 reports it is ready to take a shot (from the bridge's status packets).</summary>
        public static bool Ready;
        public static bool ReadyKnown;
        /// <summary>The R10 sees a ball on the tee.</summary>
        public static bool BallDetected;
        public static string LastPacket = "—";
        /// <summary>Time.realtimeSinceStartup of the last packet from the bridge (-1 = none yet).</summary>
        public static float LastPacketTime = -1f;

        public static ShotData LastShot;
        public static bool HasShot;
        public static int ShotCount;
        public static float LastShotTime = -1f;

        /// <summary>A new shot came in from the launch monitor (or a keyboard test shot).</summary>
        public static void RecordShot(ShotData shot)
        {
            LastShot = shot;
            HasShot = true;
            ShotCount++;
            LastShotTime = Time.realtimeSinceStartup;
        }

        /// <summary>The same shot with more values (carry / total while flying, apex, descent, offline at rest).</summary>
        public static void UpdateShot(ShotData shot)
        {
            if (!HasShot) return;
            LastShot = shot;
        }

        public static void Packet(string summary)
        {
            LastPacket = summary ?? "";
            LastPacketTime = Time.realtimeSinceStartup;
        }

        public static void SetReady(bool ready, bool ballDetected)
        {
            Ready = ready;
            BallDetected = ballDetected;
            ReadyKnown = true;
        }

        // ---------------- The R10 itself (sent by the GolfSimZA R10 bridge)
        /// <summary>The bridge reports the R10's own state (newer bridge).</summary>
        public static bool R10Known;
        /// <summary>Bluetooth link to the R10 is up.</summary>
        public static bool R10Connected;
        /// <summary>R10 battery 0-100 (-1 = not known).</summary>
        public static int Battery = -1;
        public static string R10State = "";
        public static string R10Name = "Approach R10";
        public static string R10Model = "";
        public static string R10Firmware = "";
        /// <summary>What the bridge is doing ("Connecting (turn the R10 on)", "Not paired" ...).</summary>
        public static string R10Message = "";
        public static float R10StatusTime = -1f;

        public static void SetR10(bool connected, int battery, bool ready, string state, string name, string model, string firmware, string message)
        {
            R10Known = true;
            R10Connected = connected;
            if (battery >= 0) Battery = Mathf.Clamp(battery, 0, 100);
            R10State = state ?? "";
            if (!string.IsNullOrEmpty(name)) R10Name = name;
            if (!string.IsNullOrEmpty(model)) R10Model = model;
            if (!string.IsNullOrEmpty(firmware)) R10Firmware = firmware;
            R10Message = message ?? "";
            R10StatusTime = Time.realtimeSinceStartup;
            SetReady(connected && ready, connected && ready);
        }

        /// <summary>Short state for the HUD pill and the second screen.</summary>
        public static string R10Summary()
        {
            if (!Listening) return "R10 • WAITING";
            if (!BridgeConnected) return "R10 BRIDGE OFF";
            if (!R10Known) return "R10 BRIDGE ON";
            if (!R10Connected) return "R10 NOT CONNECTED";
            return Ready ? "R10 READY" : "R10 CONNECTED";
        }

        /// <summary>The receiver closed (leaving the play screen).</summary>
        public static void Closed()
        {
            Listening = false;
            BridgeConnected = false;
            ReadyKnown = false;
            R10Known = false;
            R10Connected = false;
        }
    }
}
