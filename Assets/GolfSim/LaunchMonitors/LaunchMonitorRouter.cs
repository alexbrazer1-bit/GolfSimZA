using System;
using GolfSimZA.Core;
using UnityEngine;

namespace GolfSimZA.LaunchMonitors
{
    /// <summary>
    /// Single adapter exposed to the simulator controller. Real R10 shots take
    /// priority; the development provider remains available for local testing.
    /// </summary>
    public sealed class LaunchMonitorRouter : MonoBehaviour, ILaunchMonitorAdapter
    {
        [SerializeField] private MonoBehaviour realMonitorBehaviour;
        [SerializeField] private MonoBehaviour testMonitorBehaviour;
        [SerializeField] private bool allowKeyboardTestShots = true;

        private ILaunchMonitorAdapter realMonitor;
        private ILaunchMonitorAdapter testMonitor;

        public string DeviceName => realMonitor?.DeviceName ?? testMonitor?.DeviceName ?? "Launch Monitor";
        public bool IsConnected => (realMonitor != null && realMonitor.IsConnected) || (allowKeyboardTestShots && testMonitor != null && testMonitor.IsConnected);
        public event Action<ShotData> ShotReceived;

        private void Awake()
        {
            realMonitor = realMonitorBehaviour as ILaunchMonitorAdapter;
            testMonitor = testMonitorBehaviour as ILaunchMonitorAdapter;

            if (realMonitor != null) realMonitor.ShotReceived += OnRealShot;
            if (testMonitor != null) testMonitor.ShotReceived += OnTestShot;
        }

        public bool TryConnect()
        {
            bool connected = false;
            if (realMonitor != null) connected |= realMonitor.TryConnect();
            if (testMonitor != null) connected |= testMonitor.TryConnect();
            return connected;
        }

        public void Disconnect()
        {
            if (realMonitor != null) realMonitor.Disconnect();
            if (testMonitor != null) testMonitor.Disconnect();
        }

        private void OnRealShot(ShotData shot)
        {
            ShotReceived?.Invoke(shot);
        }

        private void OnTestShot(ShotData shot)
        {
            if (realMonitor != null && realMonitor.IsConnected && !allowKeyboardTestShots)
                return;
            ShotReceived?.Invoke(shot);
        }

        private void OnDestroy()
        {
            if (realMonitor != null) realMonitor.ShotReceived -= OnRealShot;
            if (testMonitor != null) testMonitor.ShotReceived -= OnTestShot;
        }
    }
}
