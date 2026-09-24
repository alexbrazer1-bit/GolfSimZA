using System;
using GolfSimZA.Core;

namespace GolfSimZA.LaunchMonitors
{
    public interface ILaunchMonitorAdapter : IDisposable
    {
        string DisplayName { get; }
        bool IsConnected { get; }
        event Action<ShotData> ShotReceived;

        void Start();
        void Stop();
    }
}
