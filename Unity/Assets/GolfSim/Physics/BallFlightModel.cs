using UnityEngine;
using GolfSimZA.Core;

namespace GolfSimZA.Physics
{
    public static class BallFlightModel
    {
        // First-pass flight model. This is deliberately deterministic and will
        // later be replaced/enhanced with aerodynamic drag, lift, spin decay,
        // wind and terrain interaction.
        public static Vector3 InitialVelocity(ShotData shot)
        {
            float launch = shot.LaunchAngleDeg * Mathf.Deg2Rad;
            float direction = shot.LaunchDirectionDeg * Mathf.Deg2Rad;
            float horizontal = shot.BallSpeedMps * Mathf.Cos(launch);
            float vertical = shot.BallSpeedMps * Mathf.Sin(launch);

            return new Vector3(
                horizontal * Mathf.Sin(direction),
                vertical,
                horizontal * Mathf.Cos(direction));
        }

        public static float ApproximateCarryMeters(ShotData shot)
        {
            const float gravity = 9.80665f;
            float angle = shot.LaunchAngleDeg * Mathf.Deg2Rad;
            float velocity = shot.BallSpeedMps;
            return (velocity * velocity * Mathf.Sin(2f * angle)) / gravity;
        }
    }
}
