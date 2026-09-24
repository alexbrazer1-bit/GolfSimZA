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
        [SerializeField] private float maximumFlightTime = 12.0f;

        [Header("Ground physics")]
        [SerializeField] private float groundY = 0.0f;
        [SerializeField, Range(0.05f, 0.9f)] private float bounceRetention = 0.28f;
        [SerializeField, Range(0.5f, 1f)] private float horizontalBounceRetention = 0.86f;
        [SerializeField] private float rollDeceleration = 12.0f;
        [SerializeField] private float maxRollMeters = 35.0f;
        [SerializeField] private float stopSpeed = 0.15f;

        private Vector3 velocity;
        private bool airborne;
        private bool rolling;
        private bool hasLanded;
        private float currentSpinRpm;
        private Vector3 launchPosition;
        private Vector3 landingPosition;
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
            float scale = Mathf.Max(0.0001f, metersToUnity);
            float speed = Mathf.Max(0f, shot.BallSpeedMps) * scale;
            float elevation = Mathf.Clamp(shot.LaunchAngleDeg, -10f, 70f) * Mathf.Deg2Rad;
            float azimuth = shot.LaunchDirectionDeg * Mathf.Deg2Rad;

            Vector3 horizontal = new Vector3(Mathf.Sin(azimuth), 0f, Mathf.Cos(azimuth));
            velocity = horizontal * (speed * Mathf.Cos(elevation));
            velocity.y = speed * Mathf.Sin(elevation);

            currentSpinRpm = Mathf.Max(0f, shot.BackSpinRpm);
            ball.position = new Vector3(ball.position.x, Mathf.Max(ball.position.y, groundY + 0.03f), ball.position.z);
            launchPosition = ball.position;
            landingPosition = ball.position;
            maxHeight = ball.position.y;
            carryMeters = 0f;
            totalMeters = 0f;
            flightTime = 0f;
            airborne = true;
            rolling = false;
            hasLanded = false;
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
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            flightTime += dt;

            Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
            float horizontalSpeed = horizontalVelocity.magnitude;
            Vector3 dragForce = -velocity * (airDrag * velocity.magnitude);
            float lift = currentSpinRpm * horizontalSpeed * spinLift;

            velocity += (Vector3.down * gravity + dragForce + Vector3.up * lift) * dt;
            currentSpinRpm = Mathf.MoveTowards(currentSpinRpm, 0f, currentSpinRpm * spinDecayPerSecond * dt);
            ball.position += velocity * dt;
            maxHeight = Mathf.Max(maxHeight, ball.position.y);
            totalMeters = HorizontalDistanceFromLaunch();

            // First ground contact is the carry distance.
            if (ball.position.y <= groundY)
            {
                LandBall();
                return;
            }

            // Safety fallback so a bad scene surface can never leave the camera
            // following an airborne ball forever.
            if (flightTime >= maximumFlightTime)
            {
                ball.position = new Vector3(ball.position.x, groundY, ball.position.z);
                LandBall();
            }
        }

        private void LandBall()
        {
            if (hasLanded)
                return;

            hasLanded = true;
            ball.position = new Vector3(ball.position.x, groundY, ball.position.z);
            landingPosition = ball.position;
            carryMeters = HorizontalDistanceFromLaunch();
            totalMeters = carryMeters;

            float verticalImpactSpeed = Mathf.Abs(velocity.y);
            velocity.y = -verticalImpactSpeed * bounceRetention;
            velocity.x *= horizontalBounceRetention;
            velocity.z *= horizontalBounceRetention;

            airborne = false;
            rolling = new Vector3(velocity.x, 0f, velocity.z).magnitude > stopSpeed;

            if (!rolling)
                CompleteShot();
        }

        private void SimulateRoll()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
            float speed = horizontalVelocity.magnitude;
            float rollDistance = HorizontalDistanceFromLanding();

            // Stop the ball naturally once it has slowed down, and also prevent
            // an exaggerated roll from carrying the ball hundreds of metres.
            if (speed <= stopSpeed || rollDistance >= maxRollMeters)
            {
                velocity = Vector3.zero;
                ball.position = new Vector3(ball.position.x, groundY, ball.position.z);
                totalMeters = HorizontalDistanceFromLaunch();
                rolling = false;
                CompleteShot();
                return;
            }

            float newSpeed = Mathf.Max(0f, speed - rollDeceleration * dt);
            velocity = horizontalVelocity.normalized * newSpeed;
            ball.position += velocity * dt;
            ball.position = new Vector3(ball.position.x, groundY, ball.position.z);
            totalMeters = HorizontalDistanceFromLaunch();
        }

        private float HorizontalDistanceFromLaunch()
        {
            Vector3 delta = ball.position - launchPosition;
            delta.y = 0f;
            return delta.magnitude / Mathf.Max(0.0001f, metersToUnity);
        }

        private float HorizontalDistanceFromLanding()
        {
            Vector3 delta = ball.position - landingPosition;
            delta.y = 0f;
            return delta.magnitude / Mathf.Max(0.0001f, metersToUnity);
        }

        private void CompleteShot()
        {
            totalMeters = Mathf.Max(totalMeters, HorizontalDistanceFromLaunch());
            carryMeters = Mathf.Clamp(carryMeters, 0f, totalMeters);

            ShotCompleted?.Invoke(
                activeShot,
                carryMeters,
                totalMeters,
                maxHeight / Mathf.Max(0.0001f, metersToUnity),
                flightTime);
        }
    }
}
