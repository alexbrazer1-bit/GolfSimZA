using UnityEngine;
using GolfSimZA.Physics;

namespace GolfSimZA.LaunchMonitors.Test
{
    public sealed class TestShotController : MonoBehaviour
    {
        private TestShotProvider provider;

        private void Awake()
        {
            provider = new TestShotProvider();
            provider.ShotReceived += OnShotReceived;
            provider.Start();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
                provider.EmitDriverShot();
        }

        private static void OnShotReceived(GolfSimZA.Core.ShotData shot)
        {
            float carry = BallFlightModel.ApproximateCarryMeters(shot);
            Debug.Log($"GolfSimZA test shot: ball={shot.BallSpeedMps:F1} m/s, launch={shot.LaunchAngleDeg:F1}°, approx carry={carry:F1} m");
        }

        private void OnDestroy()
        {
            if (provider == null) return;
            provider.ShotReceived -= OnShotReceived;
            provider.Dispose();
        }
    }
}
