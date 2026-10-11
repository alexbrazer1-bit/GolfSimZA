using System;
using GolfSimZA.Core;
using GolfSimZA.LaunchMonitors;
using UnityEngine;

namespace GolfSimZA.LaunchMonitors.GarminR10
{
    /// <summary>
    /// The play scene's connection to the Garmin Approach R10.
    ///
    /// The TCP server the R10 bridge talks to is <see cref="R10Link"/>, which lives for the whole
    /// app. This component only forwards its shots while the scene is open, so loading or leaving
    /// a scene (round, range, mini game, home screen) never drops the bridge connection.
    /// </summary>
    public sealed class GarminR10Adapter : MonoBehaviour, ILaunchMonitorAdapter
    {
        [SerializeField] private int listenPort = 921;
        [Tooltip("Off = only programs on this PC can connect (recommended). On = any device on the network can send shots.")]
        [SerializeField] private bool allowNetworkConnections = false;
        [SerializeField] private bool autoStart = true;
        [SerializeField] private bool sendReadyHeartbeat = true;
        [SerializeField] private float heartbeatIntervalSeconds = 2f;

        private R10Link link;

        public string DeviceName => "Garmin Approach R10";
        public bool IsConnected => link != null && link.IsConnected;
        public bool IsListening => link != null && link.IsListening;
        public int ListenPort => listenPort;
        public string LastPacketSummary => link != null ? link.LastPacketSummary : "None";
        public event Action<ShotData> ShotReceived;

        private void Awake()
        {
            if (autoStart)
                TryConnect();
        }

        public bool TryConnect()
        {
            if (link == null)
            {
                link = R10Link.Get(listenPort, allowNetworkConnections, sendReadyHeartbeat, heartbeatIntervalSeconds);
                if (link == null) return false;
                link.ShotReceived += Forward;
            }
            return link.TryConnect();
        }

        private void Forward(ShotData shot) => ShotReceived?.Invoke(shot);

        /// <summary>Stops forwarding shots to this scene. The app's R10 server stays open for the next scene.</summary>
        public void Disconnect()
        {
            if (link != null) link.ShotReceived -= Forward;
            link = null;
        }

        private void OnDestroy() => Disconnect();
    }
}
