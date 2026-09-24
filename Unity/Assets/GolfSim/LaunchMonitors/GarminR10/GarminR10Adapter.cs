using System;
using GolfSimZA.Core;

namespace GolfSimZA.LaunchMonitors.GarminR10
{
    /// <summary>
    /// Boundary for Garmin Approach R10 integration.
    ///
    /// The R10 is intentionally isolated from the simulator core so that its
    /// transport/protocol implementation can be tested without changing ball
    /// physics or course code. Do not add proprietary Garmin SDK material here.
    /// </summary>
    public sealed class GarminR10Adapter : ILaunchMonitorAdapter
    {
        public string DisplayName => "Garmin Approach R10";
        public bool IsConnected { get; private set; }
        public event Action<ShotData> ShotReceived;

        public void Start()
        {
            // TODO: Implement the supported R10 PC transport after validating
            // the applicable Garmin/third-party integration requirements.
            IsConnected = false;
        }

        public void Stop() => IsConnected = false;

        internal void RaiseShot(ShotData shot) => ShotReceived?.Invoke(shot);

        public void Dispose() => Stop();
    }
}
