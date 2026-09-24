using GolfSimZA.Core;
using GolfSimZA.LaunchMonitors;
using GolfSimZA.Physics;
using UnityEngine;

namespace GolfSimZA.Core
{
    public sealed class SimulatorController : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour launchMonitorBehaviour;
        [SerializeField] private BallFlightSimulator ballFlightSimulator;

        private ILaunchMonitorAdapter launchMonitor;
        private ShotData lastShot;

        public ShotData LastShot => lastShot;

        private void Awake()
        {
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
            lastShot = shot;
            ballFlightSimulator?.Launch(shot);
            Debug.Log($"[GolfSimZA] Shot: ball {shot.BallSpeedMps:F1} m/s, launch {shot.LaunchAngleDeg:F1}°, spin {shot.BackSpinRpm:F0} rpm");
        }
    }
}
