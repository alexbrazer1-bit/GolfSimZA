using System;
using GolfSimZA.Core;
using UnityEngine;

namespace GolfSimZA.Physics
{
    /// <summary>
    /// Ball flight, bounce and roll.
    ///
    /// * Fixed 100 Hz integration so the live flight matches the carry prediction.
    /// * Spin axis curves the ball (positive axis = curve right / fade, negative = draw).
    /// * When the launch monitor supplies a measured carry, the horizontal flight is
    ///   scaled so the ball lands at that carry on flat ground - no jump on landing.
    /// * Landing, bounce and roll follow the real ground (terrain or demo floor),
    ///   including slopes, and greens roll faster than fairways.
    /// * Putter shots skip the flight and roll along the ground.
    /// </summary>
    public sealed class BallFlightSimulator : MonoBehaviour
    {
        private const float Step = 0.01f;
        private const float BallRadius = 0.02135f;
        private const float BallMass = 0.04593f;
        private const float BallArea = Mathf.PI * BallRadius * BallRadius;
        private const float SeaLevelAirDensity = 1.225f;

        [Header("References")]
        [SerializeField] private Transform ball;

        [Header("Flight physics (aerodynamic model, fitted to tour launch data)")]
        [SerializeField] private float gravity = 9.81f;
        [SerializeField] private float dragBase = 0.2066f;
        [SerializeField] private float dragPerSpinFactor = 0.2679f;
        [SerializeField] private float liftScale = 0.3937f;
        [SerializeField] private float liftExponent = 0.3543f;
        [SerializeField] private float liftMaximum = 0.3223f;
        [SerializeField] private float spinDecayRate = 0.0519f;
        [SerializeField] private float metersToUnity = 1.0f;
        [SerializeField] private float maximumFlightTime = 15.0f;

        [Header("Ground physics")]
        [SerializeField] private float groundY = 0.0f;
        [SerializeField, Range(0.05f, 0.9f)] private float bounceRetention = 0.18f;
        [SerializeField, Range(0.5f, 1f)] private float horizontalBounceRetention = 0.90f;
        [Tooltip("Rolling resistance on fairway in m/s² (a ball rolling at 10 m/s stops after about 12 m).")]
        [SerializeField] private float fairwayRollDeceleration = 4.2f;
        [SerializeField] private float greenRollDeceleration = 0.65f;
        [SerializeField] private float fairwayPuttDeceleration = 1.6f;
        [SerializeField] private float maxRollMeters = 60.0f;
        [SerializeField] private float stopSpeed = 0.15f;
        [SerializeField] private float minBounceSpeed = 1.2f;
        [SerializeField] private float slopeRollFactor = 0.71f;
        [SerializeField] private float maximumRollTime = 25f;

        [Header("Spin / roll tuning")]
        [SerializeField] private float rollSpinDecayPerSecond = 0.65f;

        [Header("R10 measurement")]
        [SerializeField] private bool useMeasuredR10Carry = true;

        [Header("Hole")]
        [SerializeField] private float cupRadius = 0.054f;
        [SerializeField] private float maxCupEntrySpeed = 1.6f;

        private Vector3 velocity;
        private bool airborne;
        private bool rolling;
        private bool puttMode;
        private float currentSpinRpm;
        private float spinAxisRad;
        private float horizontalScale = 1f;
        private float landingRollDeceleration;
        private Vector3 launchPosition;
        private Vector3 landingPosition;
        private float maxHeight;
        private float carryMeters;
        private float totalMeters;
        private float flightTime;
        private float rollTime;
        private float stepAccumulator;
        private bool landedOnce;
        private ShotData activeShot;
        private readonly System.Collections.Generic.HashSet<int> treesHit = new System.Collections.Generic.HashSet<int>();

        private bool hasHole;
        private Vector3 holePosition;
        private float greenRadius = 14f;

        public bool IsInFlight => airborne || rolling;
        public bool IsAirborne => airborne;
        public float CarryMeters => carryMeters;
        public float TotalMeters => totalMeters;
        public float MaxHeightMeters => maxHeight;
        public float FlightTimeSeconds => flightTime;
        public bool WasHoled { get; private set; }
        /// <summary>Angle the ball came down at on its first landing (degrees).</summary>
        public float DescentAngleDeg { get; private set; }
        /// <summary>Distance right (+) or left (-) of the aim line where the ball stopped, metres.</summary>
        public float OfflineMeters { get; private set; }
        /// <summary>Straight-line aim direction for the last shot.</summary>
        public Vector3 AimDirection { get; private set; } = Vector3.forward;
        public Vector3 LandingPosition => landingPosition;
        public Vector3 LaunchPosition => launchPosition;
        public Transform Ball => ball;

        /// <summary>Aim direction in degrees clockwise from world +Z (0 = straight down the range).</summary>
        public float AimYawDegrees { get; set; }

        /// <summary>Altitude of the course (or home range) in metres; thinner air flies further.</summary>
        public float AltitudeMeters { get; set; }

        private float AirDensity => SeaLevelAirDensity * Mathf.Exp(-Mathf.Max(-500f, AltitudeMeters) / 8434f);

        public event Action<ShotData> Launched;
        public event Action<ShotData, float, float, float, float> ShotCompleted;
        public event Action BallHoled;
        /// <summary>First time the ball touches the ground after a full shot (for the landing sound).</summary>
        public event Action<float> Landed;

        /// <summary>Stimpmeter reading → rolling deceleration (a ball released at 1.83 m/s rolls "stimp" feet).</summary>
        public static float StimpToDeceleration(float stimpFeet)
        {
            float distance = Mathf.Clamp(stimpFeet, 5f, 16f) * 0.3048f;
            return 1.83f * 1.83f / (2f * distance);
        }

        /// <summary>Applies the realism settings (green speed, fairway firmness).</summary>
        public void ApplyRealismSettings()
        {
            AppSettings s = AppSettings.Current;
            greenRollDeceleration = StimpToDeceleration(s.greenStimp);
            switch (s.fairwayFirmness)
            {
                case 0: fairwayRollDeceleration = 5.6f; fairwayPuttDeceleration = 2.0f; bounceRetention = 0.14f; break;
                case 2: fairwayRollDeceleration = 3.1f; fairwayPuttDeceleration = 1.3f; bounceRetention = 0.22f; break;
                default: fairwayRollDeceleration = 4.2f; fairwayPuttDeceleration = 1.6f; bounceRetention = 0.18f; break;
            }
        }

        private void Awake()
        {
            AltitudeMeters = AppSettings.Current.homeAltitudeMeters;
            ApplyRealismSettings();
            AppSettings.Changed += ApplyRealismSettings;

            // Sounds: strike on launch, thud on the first landing, rattle in the cup.
            Launched += shot => GolfSimAudio.PlayStrike(shot.BallSpeedMps, puttMode);
            Landed += GolfSimAudio.PlayLanding;
            BallHoled += GolfSimAudio.PlayCup;
            if (ball != null)
            {
                // Keep the ball out of ground ray casts.
                ball.gameObject.layer = GroundProbe.IgnoreRaycastLayer;
                GroundProbe.FallbackHeight = groundY;
            }
        }

        private void OnDestroy()
        {
            AppSettings.Changed -= ApplyRealismSettings;
        }

        /// <summary>Sets the current hole so greens roll faster and the ball can drop in the cup.</summary>
        /// <summary>
        /// True when this hole's green can be recognised from the course surface (then only the
        /// real green counts as "on the green"); otherwise a circle round the pin is used.
        /// </summary>
        public bool GreenFromSurface { get; private set; }

        public void SetHole(Vector3 pinPosition, float greenRadiusMeters)
        {
            hasHole = true;
            GreenFromSurface = GreenSurface.At(pinPosition) == GreenSurface.Kind.Green;
            holePosition = pinPosition;
            greenRadius = Mathf.Max(4f, greenRadiusMeters);
        }

        public void ClearHole() => hasHole = false;

        /// <summary>Places the ball at rest on the ground at the given point.</summary>
        public void PlaceBall(Vector3 worldPosition)
        {
            if (ball == null) return;
            ball.gameObject.layer = GroundProbe.IgnoreRaycastLayer;
            airborne = false;
            rolling = false;
            velocity = Vector3.zero;
            WasHoled = false;
            float h = GroundProbe.HeightAt(worldPosition);
            ball.position = new Vector3(worldPosition.x, h + BallRadius, worldPosition.z);
        }

        public bool IsOnGreen(Vector3 position)
        {
            if (!hasHole) return false;
            Vector3 d = position - holePosition;
            d.y = 0f;
            if (!GreenFromSurface) return d.magnitude <= greenRadius;
            // The real green: the surface under the ball, and near this hole's pin (another hole's green does not count).
            return d.magnitude <= greenRadius + 15f && GreenSurface.At(position) == GreenSurface.Kind.Green;
        }

        public void Launch(ShotData shot)
        {
            if (!shot.IsValid || ball == null)
                return;

            // Offsets from Settings → VISUAL SETTINGS → OFFSET (launch monitor not square to the screen).
            shot.LaunchDirectionDeg += AppSettings.Current.aimOffsetDeg;
            shot.LaunchAngleDeg += AppSettings.Current.launchAngleOffsetDeg;
            activeShot = shot;
            treesHit.Clear();
            WasHoled = false;
            stepAccumulator = 0f;
            rollTime = 0f;
            flightTime = 0f;
            landedOnce = false;
            carryMeters = 0f;
            totalMeters = 0f;

            float scale = Mathf.Max(0.0001f, metersToUnity);
            float speed = Mathf.Max(0f, shot.BallSpeedMps) * scale;
            AimDirection = Quaternion.Euler(0f, AimYawDegrees, 0f) * Vector3.forward;
            DescentAngleDeg = 0f;
            OfflineMeters = 0f;
            float yaw = (AimYawDegrees + shot.LaunchDirectionDeg) * Mathf.Deg2Rad;
            Vector3 horizontal = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));

            float groundHeight = GroundProbe.HeightAt(ball.position);
            ball.position = new Vector3(ball.position.x, Mathf.Max(ball.position.y, groundHeight + BallRadius), ball.position.z);
            launchPosition = ball.position;
            landingPosition = ball.position;
            maxHeight = ball.position.y;

            puttMode = ActiveClub.IsPutter(shot.ClubName) || shot.LaunchAngleDeg < 1.5f && shot.BallSpeedMps < 12f;
            if (puttMode)
            {
                velocity = horizontal * speed;
                currentSpinRpm = 0f;
                spinAxisRad = 0f;
                horizontalScale = 1f;
                airborne = false;
                rolling = true;
                landingPosition = launchPosition;
                landingRollDeceleration = IsOnGreen(launchPosition) ? greenRollDeceleration : fairwayPuttDeceleration;
                Launched?.Invoke(shot);
                return;
            }

            float elevation = Mathf.Clamp(shot.LaunchAngleDeg, -10f, 70f) * Mathf.Deg2Rad;
            velocity = horizontal * (speed * Mathf.Cos(elevation));
            velocity.y = speed * Mathf.Sin(elevation);
            currentSpinRpm = Mathf.Max(0f, shot.BackSpinRpm);
            spinAxisRad = Mathf.Clamp(shot.SpinAxisDeg, -60f, 60f) * Mathf.Deg2Rad;
            landingRollDeceleration = fairwayRollDeceleration;

            horizontalScale = 1f;
            if (useMeasuredR10Carry && shot.CarryMeters > 0.5f && Wind.SpeedMps < 0.1f)
            {
                float predicted = PredictFlatCarry(velocity, currentSpinRpm, spinAxisRad);
                if (predicted > 0.5f)
                    horizontalScale = Mathf.Clamp(shot.CarryMeters / predicted, 0.5f, 2.0f);
            }

            airborne = true;
            rolling = false;
            Launched?.Invoke(shot);
        }

        private void Update()
        {
            if (ball == null || !IsInFlight)
                return;

            stepAccumulator += Mathf.Min(Time.deltaTime, 0.1f);
            while (stepAccumulator >= Step && IsInFlight)
            {
                stepAccumulator -= Step;
                if (airborne) StepAirborne(Step);
                else if (rolling) StepRoll(Step);
            }
        }

        // ---------------- Flight ----------------

        /// <summary>
        /// One integration step. Spin is in rpm. Drag and lift coefficients depend on the
        /// spin factor S = r*omega/v. Lift acts at right angles to the velocity, tilted
        /// sideways by the spin axis (positive axis curves the ball right).
        /// </summary>
        private void Accelerate(ref Vector3 v, ref float spinRpm, float axis, float dt, float heightAboveGround)
        {
            // Aerodynamics use the air speed relative to the ball (wind from Settings → REALISM).
            Vector3 air = v - Wind.At(heightAboveGround);
            float speed = air.magnitude;
            if (speed < 0.01f)
            {
                v += Vector3.down * gravity * dt;
                return;
            }

            float omega = spinRpm * (2f * Mathf.PI / 60f);
            float spinFactor = BallRadius * omega / speed;
            float cd = dragBase + dragPerSpinFactor * spinFactor;
            float cl = spinFactor > 0f ? Mathf.Min(liftMaximum, liftScale * Mathf.Pow(spinFactor, liftExponent)) : 0f;
            float k = 0.5f * AirDensity * BallArea / BallMass;

            Vector3 direction = air / speed;
            Vector3 horizontal = new Vector3(air.x, 0f, air.z);
            Vector3 right = horizontal.sqrMagnitude > 0.000001f ? Vector3.Cross(Vector3.up, horizontal.normalized) : Vector3.right;
            Vector3 up = Vector3.Cross(direction, right);
            if (up.y < 0f) up = -up;
            Vector3 liftDirection = up * Mathf.Cos(axis) + right * Mathf.Sin(axis);

            Vector3 acceleration = -k * cd * speed * air + k * cl * speed * speed * liftDirection + Vector3.down * gravity;
            v += acceleration * dt;
            spinRpm *= Mathf.Exp(-spinDecayRate * dt);
        }

        private float PredictFlatCarry(Vector3 v, float spin, float axis)
        {
            Vector3 p = Vector3.zero;
            float t = 0f;
            while (t < maximumFlightTime)
            {
                Accelerate(ref v, ref spin, axis, Step, Mathf.Max(0f, p.y));
                p += v * Step;
                t += Step;
                if (p.y <= 0f && t > 0.05f) break;
            }
            p.y = 0f;
            return p.magnitude / Mathf.Max(0.0001f, metersToUnity);
        }

        private void StepAirborne(float dt)
        {
            flightTime += dt;
            Accelerate(ref velocity, ref currentSpinRpm, spinAxisRad, dt, ball.position.y - GroundProbe.HeightAt(ball.position));

            Vector3 move = new Vector3(velocity.x * horizontalScale, velocity.y, velocity.z * horizontalScale) * dt;
            ball.position += move;

            // Trees: leaves slow the ball down, trunks knock it back.
            TreeField trees = TreeField.Active;
            if (trees != null && trees.Collide(ball.position, ref velocity, treesHit))
            {
                horizontalScale = 1f;
                currentSpinRpm *= 0.5f;
            }
            maxHeight = Mathf.Max(maxHeight, ball.position.y);
            totalMeters = HorizontalDistance(ball.position, launchPosition);
            if (!landedOnce) carryMeters = totalMeters;

            GroundProbe.TrySample(ball.position, out float groundHeight, out Vector3 normal);
            if (ball.position.y - BallRadius <= groundHeight && velocity.y < 0f)
            {
                ball.position = new Vector3(ball.position.x, groundHeight + BallRadius, ball.position.z);
                Impact(normal);
                return;
            }

            if (flightTime >= maximumFlightTime)
            {
                ball.position = new Vector3(ball.position.x, groundHeight + BallRadius, ball.position.z);
                Impact(normal);
            }
        }

        private void Impact(Vector3 normal)
        {
            // The horizontal scale only shapes the flight; after landing use real motion.
            velocity = new Vector3(velocity.x * horizontalScale, velocity.y, velocity.z * horizontalScale);
            horizontalScale = 1f;

            if (!landedOnce)
            {
                float horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;
                DescentAngleDeg = Mathf.Atan2(Mathf.Max(0f, -velocity.y), Mathf.Max(0.001f, horizontalSpeed)) * Mathf.Rad2Deg;
                landedOnce = true;
                landingPosition = ball.position;
                Landed?.Invoke(velocity.magnitude);
                carryMeters = HorizontalDistance(landingPosition, launchPosition);

                bool onGreen = IsOnGreen(landingPosition);
                // Greens are smoother than fairways, so the ball releases a little more.
                landingRollDeceleration = onGreen ? fairwayRollDeceleration * 0.8f : fairwayRollDeceleration;
            }

            // Split velocity into the part into the ground and the part along it.
            float intoGround = Vector3.Dot(velocity, normal);
            Vector3 along = velocity - normal * intoGround;

            // How much forward speed survives the bounce: steep landings and backspin
            // "check" the ball (a wedge stops quickly, a driver runs out).
            float speedNow = velocity.magnitude;
            float steepness = speedNow > 0.01f ? Mathf.Clamp01(Mathf.Abs(intoGround) / speedNow) : 0f;
            float alongRetention = Mathf.Clamp(0.95f - 0.55f * steepness - currentSpinRpm / 14000f, 0.12f, horizontalBounceRetention);

            along *= alongRetention;
            float bounceSpeed = Mathf.Abs(intoGround) * bounceRetention;
            currentSpinRpm *= 0.6f;

            if (bounceSpeed > minBounceSpeed)
            {
                velocity = along + normal * bounceSpeed;
                airborne = true;
                rolling = false;
                return;
            }

            velocity = new Vector3(along.x, 0f, along.z);
            airborne = false;
            rolling = velocity.magnitude > stopSpeed;
            if (!rolling)
                CompleteShot();
        }

        // ---------------- Roll ----------------

        private void StepRoll(float dt)
        {
            rollTime += dt;
            GroundProbe.TrySample(ball.position, out float groundHeight, out Vector3 normal);

            Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
            float speed = horizontalVelocity.magnitude;

            float deceleration = landingRollDeceleration;
            if (hasHole && IsOnGreen(ball.position))
            {
                // Putts roll on green speed. A full shot keeps its landing "check" while it
                // is fast, then releases onto green speed as it slows down.
                deceleration = puttMode
                    ? greenRollDeceleration
                    : speed > 2.5f ? deceleration : Mathf.Lerp(greenRollDeceleration * 1.5f, deceleration, speed / 2.5f);
            }

            // Gravity along the slope (a rolling ball feels about 5/7 of it).
            Vector3 slopeAcceleration = new Vector3(normal.x, 0f, normal.z) * (gravity * normal.y * slopeRollFactor);

            float rolledSoFar = HorizontalDistance(ball.position, landingPosition);
            bool slopeHolds = slopeAcceleration.magnitude <= deceleration * 0.9f;
            if ((speed <= stopSpeed && slopeHolds) || (!puttMode && rolledSoFar >= maxRollMeters) || rollTime >= maximumRollTime)
            {
                velocity = Vector3.zero;
                ball.position = new Vector3(ball.position.x, groundHeight + BallRadius, ball.position.z);
                totalMeters = HorizontalDistance(ball.position, launchPosition);
                rolling = false;
                CompleteShot();
                return;
            }

            float currentDeceleration = deceleration;
            Vector3 direction = speed > 0.0001f ? horizontalVelocity / speed : Vector3.zero;

            horizontalVelocity += slopeAcceleration * dt;
            float newSpeed = Mathf.Max(0f, horizontalVelocity.magnitude - currentDeceleration * dt);
            horizontalVelocity = horizontalVelocity.sqrMagnitude > 0.000001f ? horizontalVelocity.normalized * newSpeed : direction * newSpeed;
            velocity = horizontalVelocity;

            Vector3 next = ball.position + velocity * dt;
            float nextHeight = GroundProbe.HeightAt(next);
            ball.position = new Vector3(next.x, nextHeight + BallRadius, next.z);
            totalMeters = HorizontalDistance(ball.position, launchPosition);
            currentSpinRpm = Mathf.MoveTowards(currentSpinRpm, 0f, Mathf.Max(1f, currentSpinRpm) * rollSpinDecayPerSecond * dt);

            if (hasHole && HorizontalDistance(ball.position, holePosition) <= cupRadius && newSpeed <= maxCupEntrySpeed)
            {
                ball.position = new Vector3(holePosition.x, nextHeight - 0.06f, holePosition.z);
                velocity = Vector3.zero;
                rolling = false;
                WasHoled = true;
                totalMeters = HorizontalDistance(ball.position, launchPosition);
                CompleteShot();
                BallHoled?.Invoke();
            }
        }

        private float HorizontalDistance(Vector3 a, Vector3 b)
        {
            Vector3 delta = a - b;
            delta.y = 0f;
            return delta.magnitude / Mathf.Max(0.0001f, metersToUnity);
        }

        private void CompleteShot()
        {
            Vector3 fromLaunch = ball.position - launchPosition;
            fromLaunch.y = 0f;
            Vector3 right = Vector3.Cross(Vector3.up, AimDirection);
            OfflineMeters = Vector3.Dot(fromLaunch, right) / Mathf.Max(0.0001f, metersToUnity);

            if (puttMode)
                carryMeters = 0f;

            totalMeters = Mathf.Max(totalMeters, carryMeters);
            carryMeters = Mathf.Clamp(carryMeters, 0f, totalMeters);

            ShotCompleted?.Invoke(
                activeShot,
                carryMeters,
                totalMeters,
                (maxHeight - launchPosition.y) / Mathf.Max(0.0001f, metersToUnity),
                flightTime);
        }
    }
}
