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

        /// <summary>The receiver closed (leaving the play screen).</summary>
        public static void Closed()
        {
            Listening = false;
            BridgeConnected = false;
            ReadyKnown = false;
        }
    }
}
