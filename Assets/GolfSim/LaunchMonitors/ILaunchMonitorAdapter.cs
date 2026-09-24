using System;
using GolfSimZA.Core;

namespace GolfSimZA.LaunchMonitors
{
    public interface ILaunchMonitorAdapter
    {
        string DeviceName { get; }
        bool IsConnected { get; }
        event Action<ShotData> ShotReceived;
        bool TryConnect();
        void Disconnect();
    }
}
