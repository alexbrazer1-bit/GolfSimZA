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
        [SerializeField] private float airDrag = 0.00055f;
        [SerializeField] private float spinLift = 0.000015f;
        [SerializeField] private float spinDecayPerSecond = 0.08f;
        [SerializeField] private float metersToUnity = 1.0f;
        [SerializeField] private float maximumFlightTime = 12.0f;

        [Header("Ground physics")]
        [SerializeField] private float groundY = 0.0f;
        [SerializeField, Range(0.05f, 0.9f)] private float bounceRetention = 0.18f;
        [SerializeField, Range(0.5f, 1f)] private float horizontalBounceRetention = 0.90f;
        [SerializeField] private float rollDeceleration = 7.5f;
        [SerializeField] private float maxRollMeters = 35.0f;
        [SerializeField] private float stopSpeed = 0.15f;

        [Header("Spin / roll tuning")]
        [SerializeField] private float referenceSpinRpm = 2400.0f;
        [SerializeField] private float spinRollInfluence = 0.45f;
        [SerializeField] private float rollSpinDecayPerSecond = 0.65f;

        [Header("R10 measurement")]
        [SerializeField] private bool useMeasuredR10Carry = true;
        [SerializeField] private bool useMeasuredR10CarryAsTotalWhenNoRoll = true;

        private Vector3 velocity;
        private bool airborne;
        private bool rolling;
        private bool hasLanded;
        private float currentSpinRpm;
        private float landingRollDeceleration;
        private Vector3 launchPosition;
        private Vector3 landingPosition;
        private float maxHeight;
        private float carryMeters;
        private float totalMeters;
        private float flightTime;
        private float measuredCarryMeters;
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
            measuredCarryMeters = useMeasuredR10Carry && shot.CarryMeters > 0.1f ? shot.CarryMeters : 0f;

            float scale = Mathf.Max(0.0001f, metersToUnity);
            float speed = Mathf.Max(0f, shot.BallSpeedMps) * scale;
            float elevation = Mathf.Clamp(shot.LaunchAngleDeg, -10f, 70f) * Mathf.Deg2Rad;
            float azimuth = shot.LaunchDirectionDeg * Mathf.Deg2Rad;

            Vector3 horizontal = new Vector3(Mathf.Sin(azimuth), 0f, Mathf.Cos(azimuth));
            velocity = horizontal * (speed * Mathf.Cos(elevation));
            velocity.y = speed * Mathf.Sin(elevation);

            currentSpinRpm = Mathf.Max(0f, shot.BackSpinRpm);
            landingRollDeceleration = rollDeceleration;
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

            if (ball.position.y <= groundY)
            {
                LandBall();
                return;
            }

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

            // Garmin R10 carry is the authoritative measured carry. The old
            // simulator replaced it with a simplified aerodynamic estimate,
            // which caused large discrepancies between the R10 and GolfSimZA.
            // We retain the visual flight but report the measured carry when it
            // is available, then add a controlled ground-roll estimate.
            float visualCarry = HorizontalDistanceFromLaunch();
            carryMeters = measuredCarryMeters > 0.1f ? measuredCarryMeters : visualCarry;

            if (measuredCarryMeters > 0.1f && visualCarry > 0.1f)
            {
                Vector3 direction = ball.position - launchPosition;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    direction.Normalize();
                    ball.position = launchPosition + direction * measuredCarryMeters * metersToUnity;
                    ball.position = new Vector3(ball.position.x, groundY, ball.position.z);
                }
            }

            landingPosition = ball.position;

            float normalizedSpin = Mathf.Clamp01(currentSpinRpm / Mathf.Max(1f, referenceSpinRpm));
            float spinFriction = Mathf.Lerp(1f - spinRollInfluence, 1f + spinRollInfluence, normalizedSpin);
            landingRollDeceleration = rollDeceleration * spinFriction;

            float verticalImpactSpeed = Mathf.Abs(velocity.y);
            velocity.y = -verticalImpactSpeed * bounceRetention;

            float horizontalRetention = Mathf.Lerp(
                horizontalBounceRetention,
                horizontalBounceRetention * 0.88f,
                normalizedSpin);
            velocity.x *= horizontalRetention;
            velocity.z *= horizontalRetention;

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

            if (speed <= stopSpeed || rollDistance >= maxRollMeters)
            {
                velocity = Vector3.zero;
                ball.position = new Vector3(ball.position.x, groundY, ball.position.z);
                totalMeters = HorizontalDistanceFromLaunch();
                rolling = false;
                CompleteShot();
                return;
            }

            float spinFactor = Mathf.Clamp01(currentSpinRpm / Mathf.Max(1f, referenceSpinRpm));
            float currentDeceleration = landingRollDeceleration * Mathf.Lerp(1f, 1.12f, spinFactor);
            float newSpeed = Mathf.Max(0f, speed - currentDeceleration * dt);
            velocity = horizontalVelocity.normalized * newSpeed;
            ball.position += velocity * dt;
            ball.position = new Vector3(ball.position.x, groundY, ball.position.z);
            totalMeters = HorizontalDistanceFromLaunch();

            currentSpinRpm = Mathf.MoveTowards(currentSpinRpm, 0f,
                Mathf.Max(1f, currentSpinRpm) * rollSpinDecayPerSecond * dt);
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
            if (measuredCarryMeters > 0.1f)
                carryMeters = measuredCarryMeters;

            totalMeters = Mathf.Max(totalMeters, carryMeters);
            if (useMeasuredR10CarryAsTotalWhenNoRoll && totalMeters <= carryMeters + 0.01f)
                totalMeters = carryMeters;

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
