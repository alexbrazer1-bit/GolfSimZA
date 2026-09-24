using GolfSimZA.LaunchMonitors;
using GolfSimZA.Physics;
using UnityEngine;

namespace GolfSimZA.Core
{
    public sealed class SimulatorController : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour launchMonitorBehaviour;
        [SerializeField] private BallFlightSimulator ballFlightSimulator;
        [SerializeField] private int shotHistoryCapacity = 20;

        private ILaunchMonitorAdapter launchMonitor;
        private ShotData lastShot;
        private ShotHistory shotHistory;

        public ShotData LastShot => lastShot;
        public ShotHistory History => shotHistory;
        public string LaunchMonitorName => launchMonitor?.DeviceName ?? "Not configured";
        public bool IsLaunchMonitorConnected => launchMonitor != null && launchMonitor.IsConnected;

        private void Awake()
        {
            shotHistory = new ShotHistory(Mathf.Max(1, shotHistoryCapacity));
            launchMonitor = launchMonitorBehaviour as ILaunchMonitorAdapter;
            if (launchMonitor == null)
            {
                Debug.LogError("[GolfSimZA] Launch monitor component must implement ILaunchMonitorAdapter.");
                return;
            }

            launchMonitor.ShotReceived += OnShotReceived;
            launchMonitor.TryConnect();
        }

        private void OnDestroy()
        {
            if (launchMonitor == null)
                return;

            launchMonitor.ShotReceived -= OnShotReceived;
            launchMonitor.Disconnect();
        }

        private void OnShotReceived(ShotData shot)
        {
            if (!shot.IsValid)
                return;

            lastShot = shot;
            shotHistory.Add(shot);
            ballFlightSimulator?.Launch(shot);
            Debug.Log($"[GolfSimZA] Shot: {shot.ClubName}, ball {shot.BallSpeedMps:F1} m/s, launch {shot.LaunchAngleDeg:F1}°, spin {shot.BackSpinRpm:F0} rpm");
        }
    }
}
