using System;
using GolfSimZA.Core;
using UnityEngine;

namespace GolfSimZA.Physics
{
    public sealed class BallFlightSimulator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform ball;

        [Header("Flight physics")]
        [SerializeField] private float gravity = 9.81f;
        [SerializeField] private float airDrag = 0.00075f;
        [SerializeField] private float spinLift = 0.000018f;
        [SerializeField] private float spinDecayPerSecond = 0.08f;
        [SerializeField] private float metersToUnity = 1.0f;

        [Header("Ground physics")]
        [SerializeField] private float groundY = 0.0f;
        [SerializeField, Range(0.05f, 0.9f)] private float bounceRetention = 0.28f;
        [SerializeField, Range(0.5f, 1f)] private float horizontalBounceRetention = 0.86f;
        [SerializeField] private float rollDeceleration = 2.2f;
        [SerializeField] private float stopSpeed = 0.15f;

        private Vector3 velocity;
        private bool airborne;
        private bool rolling;
        private float currentSpinRpm;
        private float launchZ;
        private float maxHeight;
        private float carryMeters;
        private float totalMeters;
        private float flightTime;
        private ShotData activeShot;

        public bool IsInFlight => airborne || rolling;
        public float CarryMeters => carryMeters;
        public float TotalMeters => totalMeters;
        public float MaxHeightMeters => maxHeight;
        public float FlightTimeSeconds => flightTime;
        public event Action<ShotData, float, float, float, float> ShotCompleted;

        public void Launch(ShotData shot)
        {
            if (!shot.IsValid || ball == null)
                return;

            activeShot = shot;
            float speed = Mathf.Max(0f, shot.BallSpeedMps) * metersToUnity;
            float elevation = Mathf.Clamp(shot.LaunchAngleDeg, -10f, 70f) * Mathf.Deg2Rad;
            float azimuth = shot.LaunchDirectionDeg * Mathf.Deg2Rad;

            Vector3 horizontal = new Vector3(Mathf.Sin(azimuth), 0f, Mathf.Cos(azimuth));
            velocity = horizontal * (speed * Mathf.Cos(elevation));
            velocity.y = speed * Mathf.Sin(elevation);

            currentSpinRpm = Mathf.Max(0f, shot.BackSpinRpm);
            launchZ = ball.position.z;
            maxHeight = ball.position.y;
            carryMeters = 0f;
            totalMeters = 0f;
            flightTime = 0f;

            ball.position = new Vector3(ball.position.x, Mathf.Max(ball.position.y, groundY + 0.03f), ball.position.z);
            airborne = true;
            rolling = false;
        }

        private void Update()
        {
            if (ball == null || !IsInFlight)
                return;

            if (airborne)
                SimulateAirborne();
            else if (rolling)
                SimulateRoll();
        }

        private void SimulateAirborne()
        {
            float dt = Time.deltaTime;
            flightTime += dt;

            Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
            float horizontalSpeed = horizontalVelocity.magnitude;

            // Approximate aerodynamic drag and Magnus lift. This is deliberately
            // lightweight so the prototype remains deterministic and responsive.
            Vector3 dragForce = -velocity * (airDrag * velocity.magnitude);
            Vector3 liftDirection = horizontalSpeed > 0.01f
                ? Vector3.Cross(Vector3.up, horizontalVelocity.normalized)
                : Vector3.zero;
            float lift = currentSpinRpm * horizontalSpeed * spinLift;

            velocity += (Vector3.down * gravity + dragForce + Vector3.up * lift) * dt;
            currentSpinRpm = Mathf.MoveTowards(currentSpinRpm, 0f, currentSpinRpm * spinDecayPerSecond * dt);
            ball.position += velocity * dt;
            maxHeight = Mathf.Max(maxHeight, ball.position.y);

            if (ball.position.y <= groundY)
            {
                ball.position = new Vector3(ball.position.x, groundY, ball.position.z);
                carryMeters = HorizontalDistanceFromLaunch();

                velocity.y = -velocity.y * bounceRetention;
                velocity.x *= horizontalBounceRetention;
                velocity.z *= horizontalBounceRetention;

                if (Mathf.Abs(velocity.y) < 1.0f)
                {
                    airborne = false;
                    rolling = new Vector3(velocity.x, 0f, velocity.z).magnitude > stopSpeed;
                    if (!rolling)
                        CompleteShot();
                }
            }
        }

        private void SimulateRoll()
        {
            float dt = Time.deltaTime;
            Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
            float speed = horizontalVelocity.magnitude;

            if (speed <= stopSpeed)
            {
                velocity = Vector3.zero;
                rolling = false;
                CompleteShot();
                return;
            }

            float newSpeed = Mathf.Max(0f, speed - rollDeceleration * dt);
            velocity = horizontalVelocity.normalized * newSpeed;
            ball.position += velocity * dt;
            totalMeters = HorizontalDistanceFromLaunch();
        }

        private float HorizontalDistanceFromLaunch()
        {
            Vector3 delta = ball.position - new Vector3(ball.position.x, ball.position.y, launchZ);
            return Mathf.Abs(delta.z) / Mathf.Max(0.0001f, metersToUnity);
        }

        private void CompleteShot()
        {
            totalMeters = HorizontalDistanceFromLaunch();
            carryMeters = Mathf.Max(carryMeters, totalMeters);
            ShotCompleted?.Invoke(activeShot, carryMeters, totalMeters, maxHeight / Mathf.Max(0.0001f, metersToUnity), flightTime);
        }
    }
}
