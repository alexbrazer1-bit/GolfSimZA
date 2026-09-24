using System;
using GolfSimZA.Core;
using GolfSimZA.LaunchMonitors;
using UnityEngine;

namespace GolfSimZA.LaunchMonitors.GarminR10
{
    /// <summary>
    /// Boundary for Garmin Approach R10 support.
    ///
    /// This class intentionally does not implement or reproduce Garmin's
    /// proprietary transport/protocol. The transport can be plugged in once
    /// an approved/documented integration path is selected.
    /// </summary>
    public sealed class GarminR10Adapter : ILaunchMonitorAdapter
    {
        public string DeviceName => "Garmin Approach R10";
        public bool IsConnected { get; private set; }

        public event Action<ShotData> ShotReceived;

        public bool TryConnect()
        {
            IsConnected = false;
            Debug.Log("[GolfSimZA] Garmin R10 adapter is ready, but no proprietary transport is enabled yet.");
            return false;
        }

        public void Disconnect()
        {
            IsConnected = false;
        }

        // Called by the approved transport layer when a valid R10 shot is received.
        public void PublishShot(ShotData shot)
        {
            if (!shot.IsValid)
                return;

            ShotReceived?.Invoke(shot);
        }
    }
}
