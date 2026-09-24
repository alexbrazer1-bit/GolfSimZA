using System;
using GolfSimZA.Core;

namespace GolfSimZA.LaunchMonitors.Test
{
    public sealed class TestShotProvider : ILaunchMonitorAdapter
    {
        public string DisplayName => "Development Test Shot";
        public bool IsConnected { get; private set; }
        public event Action<ShotData> ShotReceived;

        public void Start() => IsConnected = true;

        public void Stop() => IsConnected = false;

        public void EmitDriverShot()
        {
            if (!IsConnected) return;
            ShotReceived?.Invoke(ShotData.CreateTestDriverShot());
        }

        public void Dispose() => Stop();
    }
}
