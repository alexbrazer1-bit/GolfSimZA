using GolfSimZA.LaunchMonitors;
using GolfSimZA.Physics;
using GolfSimZA.UI;
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

        /// <summary>
        /// Every valid shot since the scene started. The HUDs use this to notice a new shot -
        /// History.Count stops growing once the history is full (a long round or range session).
        /// </summary>
        public int TotalShots { get; private set; }
        public BallFlightSimulator BallFlight => ballFlightSimulator;
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
            if (ballFlightSimulator != null)
                ballFlightSimulator.ShotCompleted += OnShotCompleted;

            EnsureModernPresentation();
            launchMonitor.TryConnect();
        }

        private void EnsureModernPresentation()
        {
            ModernGolfSimUI modern = GetComponent<ModernGolfSimUI>();
            if (modern == null)
                gameObject.AddComponent<ModernGolfSimUI>();
        }

        private void Update()
        {
            // Keep the HUD live while the ball is flying and rolling. Previously
            // LastShot stayed at its launch-time 0.0 m values until the final
            // ShotCompleted event, which made a valid shot appear to read 0.0 m.
            if (ballFlightSimulator == null || !lastShot.IsValid || !ballFlightSimulator.IsInFlight)
                return;

            lastShot.CarryMeters = ballFlightSimulator.CarryMeters;
            lastShot.TotalMeters = ballFlightSimulator.TotalMeters;
        }

        private void OnDestroy()
        {
            if (launchMonitor != null)
            {
                launchMonitor.ShotReceived -= OnShotReceived;
                launchMonitor.Disconnect();
            }

            if (ballFlightSimulator != null)
                ballFlightSimulator.ShotCompleted -= OnShotCompleted;
        }

        private void OnShotReceived(ShotData shot)
        {
            if (!shot.IsValid)
                return;

            if (ballFlightSimulator != null && ballFlightSimulator.IsInFlight)
                return;

            // Launch monitors like the R10 do not know which club was hit.
            if (string.IsNullOrWhiteSpace(shot.ClubName))
            {
                shot.ClubName = ActiveClub.Resolve();
                shot.ClubNumber = ActiveClub.SlotFor(shot.ClubName);
            }

            lastShot = shot;
            if (shot.IsValid) TotalShots++;
            shotHistory.Add(shot);
            ballFlightSimulator?.Launch(shot);
            Debug.Log($"[GolfSimZA] Shot: {shot.ClubName}, ball {shot.BallSpeedMps:F1} m/s, club {shot.ClubSpeedMps:F1} m/s, launch {shot.LaunchAngleDeg:F1}°, spin {shot.BackSpinRpm:F0} rpm, R10 carry {shot.CarryMeters:F1} m");
        }

        private void OnShotCompleted(ShotData shot, float carryMeters, float totalMeters, float maxHeightMeters, float flightTimeSeconds)
        {
            ShotData completed = shot;
            completed.CarryMeters = carryMeters;
            completed.TotalMeters = totalMeters;
            completed.PeakHeightMeters = maxHeightMeters;
            completed.IsComplete = true;
            if (ballFlightSimulator != null)
            {
                completed.DescentAngleDeg = ballFlightSimulator.DescentAngleDeg;
                completed.OfflineMeters = ballFlightSimulator.OfflineMeters;
            }
            lastShot = completed;
            shotHistory.ReplaceLast(completed);

            Debug.Log($"[GolfSimZA] Landing: carry {carryMeters:F1} m, total {totalMeters:F1} m, apex {maxHeightMeters:F1} m, flight {flightTimeSeconds:F2} s");
        }
    }
}
