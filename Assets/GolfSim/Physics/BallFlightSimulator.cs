using GolfSimZA.Core;
using UnityEngine;

namespace GolfSimZA.Physics
{
    public sealed class BallFlightSimulator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform ball;

        [Header("Prototype physics")]
        [SerializeField] private float gravity = 9.81f;
        [SerializeField] private float drag = 0.0009f;
        [SerializeField] private float metersToUnity = 1.0f;
        [SerializeField] private float groundY = 0.0f;
        [SerializeField] private float bounceRetention = 0.35f;
        [SerializeField] private float rollDeceleration = 2.2f;

        private Vector3 velocity;
        private bool airborne;
        private bool rolling;

        public void Launch(ShotData shot)
        {
            if (!shot.IsValid || ball == null)
                return;

            float speed = shot.BallSpeedMps * metersToUnity;
            float elevation = shot.LaunchAngleDeg * Mathf.Deg2Rad;
            float azimuth = shot.LaunchDirectionDeg * Mathf.Deg2Rad;

            Vector3 horizontal = new Vector3(Mathf.Sin(azimuth), 0f, Mathf.Cos(azimuth));
            velocity = horizontal * (speed * Mathf.Cos(elevation));
            velocity.y = speed * Mathf.Sin(elevation);

            ball.position = new Vector3(ball.position.x, Mathf.Max(ball.position.y, groundY + 0.03f), ball.position.z);
            airborne = true;
            rolling = false;
        }

        private void Update()
        {
            if (ball == null)
                return;

            if (airborne)
            {
                velocity += Vector3.down * gravity * Time.deltaTime;
                velocity *= Mathf.Clamp01(1f - drag * velocity.magnitude * Time.deltaTime);
                ball.position += velocity * Time.deltaTime;

                if (ball.position.y <= groundY)
                {
                    ball.position = new Vector3(ball.position.x, groundY, ball.position.z);
                    velocity.y = -velocity.y * bounceRetention;
                    velocity.x *= 0.86f;
                    velocity.z *= 0.86f;

                    if (Mathf.Abs(velocity.y) < 1.5f)
                    {
                        airborne = false;
                        rolling = velocity.magnitude > 0.15f;
                    }
                }
            }
            else if (rolling)
            {
                float speed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
                if (speed <= 0.01f)
                {
                    velocity = Vector3.zero;
                    rolling = false;
                    return;
                }

                float newSpeed = Mathf.Max(0f, speed - rollDeceleration * Time.deltaTime);
                Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z).normalized;
                velocity = horizontal * newSpeed;
                ball.position += velocity * Time.deltaTime;
            }
        }
    }
}
