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
        /// <summary>A drop bigger than this in one roll step is a ledge: the ball falls off it.</summary>
        private const float LedgeDrop = 0.5f;
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
        [Tooltip("Vertical bounce (coefficient of restitution) of a slow impact; fast impacts bounce less.")]
        [SerializeField, Range(0.05f, 0.9f)] private float bounceRetention = 0.45f;
        [Tooltip("How much the restitution drops per m/s of impact speed into the ground (turf absorbs hard landings).")]
        [SerializeField] private float bounceSpeedLoss = 0.02f;
        [Tooltip("Turf friction on impact: slows the ball along the ground in proportion to how hard it lands.")]
        [SerializeField] private float turfFriction = 0.6f;
        [Tooltip("How strongly backspin checks the ball on landing (1 = rigid-ball physics).")]
        [SerializeField] private float spinCheck = 1.75f;
        [Tooltip("Rolling resistance on fairway at walking pace in m/s².")]
        [SerializeField] private float fairwayRollDeceleration = 2.6f;
        [Tooltip("Extra grass drag per m/s of roll speed (fast balls are slowed more: a natural, gradual run-out).")]
        [SerializeField] private float rollDragPerSpeed = 0.35f;
        [SerializeField] private float greenRollDeceleration = 0.65f;
        [SerializeField] private float fairwayPuttDeceleration = 1.6f;
        [SerializeField] private float maxRollMeters = 60.0f;
        [SerializeField] private float stopSpeed = 0.08f;
        [SerializeField] private float minBounceSpeed = 0.6f;
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
        /// <summary>The ball came to rest in water on a mini game map (splash).</summary>
        public bool InWater { get; private set; }
        /// <summary>Side slope under the ball at the last launch (% rise to the right of the aim line).</summary>
        public float LieSlopePercent { get; private set; }
        /// <summary>How far the sloped lie turned the last shot (degrees, + = right).</summary>
        public float LieShiftDeg { get; private set; }

        /// <summary>
        /// Side slope across the aim line in percent: how many metres the ground rises per 100 m
        /// going to the RIGHT of the aim line (+ = higher on the right).
        /// </summary>
        public static float SideSlopePercent(Vector3 groundNormal, float aimYawDeg)
        {
            Vector3 right = Quaternion.Euler(0f, aimYawDeg, 0f) * Vector3.right;
            float ny = Mathf.Max(0.2f, groundNormal.y);
            return -(groundNormal.x * right.x + groundNormal.z * right.z) / ny * 100f;
        }

        /// <summary>
        /// Start direction change from a side slope (degrees, + = right). A right-handed golfer
        /// stands on the left of the aim line, so ground rising to the right puts the ball above
        /// the feet; a left-hander stands on the right, so it is the other way round - and the
        /// same formula gives both: the slope tilts the club face, and a lofted face tilted
        /// points sideways by atan(tan(loft) x sin(slope)). Golfers allow for part of it with
        /// their stance, so 85 % of that is used.
        /// </summary>
        public static float LieShift(float sideSlopePercent, float loftDeg)
        {
            float slope = Mathf.Atan(sideSlopePercent / 100f);
            float loft = Mathf.Clamp(loftDeg, 6f, 64f) * Mathf.Deg2Rad;
            float shift = -Mathf.Atan(Mathf.Tan(loft) * Mathf.Sin(slope)) * Mathf.Rad2Deg * 0.85f;
            return Mathf.Clamp(shift, -20f, 20f);
        }

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
            // Ground model values (older scenes saved earlier tunings of these: always use the current model).
            stopSpeed = 0.08f;
            minBounceSpeed = 0.6f;
            maxRollMeters = 60f;
            bounceSpeedLoss = 0.02f;
            spinCheck = 1.75f;
            rollDragPerSpeed = 0.35f;
            switch (s.fairwayFirmness)
            {
                // Soft / medium / firm turf. Tuned so a medium fairway gives typical run-outs:
                // driver ~20-27 m, 3 wood ~19 m, 7 iron ~8 m, pitching wedge ~3 m, sand wedge ~2 m.
                case 0: fairwayRollDeceleration = 3.4f; fairwayPuttDeceleration = 2.0f; bounceRetention = 0.38f; turfFriction = 0.75f; break;
                case 2: fairwayRollDeceleration = 1.9f; fairwayPuttDeceleration = 1.3f; bounceRetention = 0.50f; turfFriction = 0.50f; break;
                default: fairwayRollDeceleration = 2.6f; fairwayPuttDeceleration = 1.6f; bounceRetention = 0.45f; turfFriction = 0.60f; break;
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
            interpolating = false;
            ball.gameObject.layer = GroundProbe.IgnoreRaycastLayer;
            airborne = false;
            rolling = false;
            velocity = Vector3.zero;
            WasHoled = false;
            InWater = false;
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
            if (interpolating) { ball.position = simCurrent; interpolating = false; }
            treesHit.Clear();
            WasHoled = false;
            InWater = false;
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
            // Sloped lie: a ball above or below the feet starts left or right (club face tilted by the slope).
            LieSlopePercent = 0f;
            LieShiftDeg = 0f;
            bool isPutt = ActiveClub.IsPutter(shot.ClubName) || shot.LaunchAngleDeg < 1.5f && shot.BallSpeedMps < 12f;
            if (!isPutt && AppSettings.Current.lieSlope && GroundProbe.TrySample(ball.position, out _, out Vector3 lieNormal))
            {
                float loft = shot.HasClubPath && shot.DynamicLoftDeg > 1f ? shot.DynamicLoftDeg : ActiveClub.LoftOf(string.IsNullOrEmpty(shot.ClubName) ? ActiveClub.Resolve() : shot.ClubName);
                LieSlopePercent = SideSlopePercent(lieNormal, AimYawDegrees);
                LieShiftDeg = LieShift(LieSlopePercent, loft);
                if (Mathf.Abs(LieShiftDeg) > 0.05f)
                {
                    shot.LaunchDirectionDeg += LieShiftDeg;
                    // The tilted face also adds draw / fade spin the same way.
                    shot.SpinAxisDeg += LieShiftDeg * 0.5f;
                    Debug.Log($"[GolfSimZA] Sloped lie: {LieSlopePercent:0.0}% side slope, loft {loft:0}° -> shot starts {Mathf.Abs(LieShiftDeg):0.0}° {(LieShiftDeg < 0f ? "left" : "right")}");
                }
            }
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

        /// <summary>True while any ball in the scene is flying or rolling (other systems go easy on the CPU then).</summary>
        public static bool AnyInFlight { get; private set; }
        /// <summary>Times a ball has hit a tree (leaves or trunk) / bounced off a solid obstacle (for the automatic test).</summary>
        public static int TreeHits { get; private set; }
        private static int treeLogs;
        public static int ObstacleHits { get; private set; }

        // Smooth motion: the physics runs in fixed 10 ms steps, but frames do not line up with them
        // (at 60 fps a frame holds one step, then two; at 144 fps often none), which made the ball
        // jump and stutter in the air. The ball is drawn between the last two physics positions.
        private Vector3 simPrevious, simCurrent;
        private bool interpolating;

        private void Update()
        {
            if (ball == null || !IsInFlight)
            {
                if (ball != null && interpolating) { ball.position = simCurrent; interpolating = false; }
                AnyInFlight = false;
                return;
            }
            AnyInFlight = true;

            // Back to the true physics position before stepping.
            if (interpolating) ball.position = simCurrent;
            else simPrevious = simCurrent = ball.position;

            stepAccumulator += Mathf.Min(Time.deltaTime, 0.1f);
            while (stepAccumulator >= Step && IsInFlight)
            {
                stepAccumulator -= Step;
                simPrevious = ball.position;
                if (airborne) StepAirborne(Step);
                else if (rolling) StepRoll(Step);
            }
            simCurrent = ball.position;

            if (IsInFlight)
            {
                // Show the ball part of the way into the next step (never across a cup drop or splash: those stop the shot).
                ball.position = Vector3.LerpUnclamped(simPrevious, simCurrent, Mathf.Clamp01(stepAccumulator / Step));
                interpolating = true;
            }
            else
            {
                interpolating = false;
                AnyInFlight = false;
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
            // Anything solid in the way this step (a hillside, cliff, rock, building, wall, tree
            // with a collider)? Before 1.8.5 only the ground straight below the ball was checked,
            // so a ball flew through mountains and trees.
            if (Sweep(ball.position, ball.position + move, out RaycastHit hit, true))
            {
                if (IsWater(hit.collider))
                {
                    ball.position = hit.point;
                    Splash(hit.point.y);
                    return;
                }
                ball.position = hit.point + hit.normal * (BallRadius * 1.05f);
                maxHeight = Mathf.Max(maxHeight, ball.position.y);
                if (hit.normal.y >= 0.5f)
                {
                    // Ground the ball can land on (a slope it flies into counts too).
                    totalMeters = HorizontalDistance(ball.position, launchPosition);
                    if (!landedOnce) carryMeters = totalMeters;
                    Impact(hit.normal);
                }
                else
                {
                    WallBounce(hit.normal);
                }
                return;
            }
            ball.position += move;

            // Trees: leaves slow the ball down, trunks knock it back.
            TreeField trees = TreeField.Active;
            if (trees != null && trees.Collide(ball.position, ref velocity, treesHit))
            {
                TreeHits++;
                horizontalScale = 1f;
                // A brush with the leaves keeps most of the spin; a branch or the trunk takes most of it.
                currentSpinRpm *= Mathf.Lerp(0.35f, 0.95f, trees.LastKeep);
                if (treeLogs++ < 40)
                    Debug.Log($"[GolfSimZA] Tree: ball went {(trees.LastDepth < 0.35f ? "through the outer leaves" : trees.LastDepth < 0.7f ? "through the branches" : trees.LastDepth >= 1f && trees.LastKeep <= 0.45f ? "into the trunk" : "into the heart of the tree")}, kept {trees.LastKeep * 100f:0}% of its speed.");
            }
            maxHeight = Mathf.Max(maxHeight, ball.position.y);
            totalMeters = HorizontalDistance(ball.position, launchPosition);
            if (!landedOnce) carryMeters = totalMeters;

            GroundProbe.TrySample(ball.position, out float groundHeight, out Vector3 normal);
            if (ball.position.y - BallRadius <= groundHeight && velocity.y < 0f)
            {
                if (GroundProbe.LastWasWater)
                {
                    Splash(groundHeight);
                    return;
                }
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

        // ---------------- Obstacles ----------------

        private static readonly RaycastHit[] SweepHits = new RaycastHit[16];
        private static readonly System.Collections.Generic.Dictionary<Collider, bool> solidCache = new System.Collections.Generic.Dictionary<Collider, bool>();
        private const int ObstacleMask = ~(1 << GroundProbe.IgnoreRaycastLayer);

        /// <summary>The nearest solid thing between two points (invisible helper colliders and the flagstick are ignored).</summary>
        private bool Sweep(Vector3 from, Vector3 to, out RaycastHit nearest) => Sweep(from, to, out nearest, false);

        /// <summary>
        /// The nearest solid thing between two points. In the air (inFlight) a course tree's own
        /// collider is left out: those colliders often wrap the whole crown, which made trees solid
        /// walls. The tree field handles trees instead (leaves slow the ball, trunks knock it back).
        /// </summary>
        private bool Sweep(Vector3 from, Vector3 to, out RaycastHit nearest, bool inFlight)
        {
            nearest = default;
            Vector3 d = to - from;
            float length = d.magnitude;
            if (length < 1e-5f) return false;
            int count = UnityEngine.Physics.RaycastNonAlloc(from, d / length, SweepHits, length + BallRadius, ObstacleMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = SweepHits[i];
                if (h.collider == null || h.distance >= best || !IsSolid(h.collider)) continue;
                if (inFlight && IsTreeHit(h)) continue;
                // The flagstick never knocks the ball away from the cup.
                if (hasHole && h.normal.y < 0.5f && HorizontalDistance(h.point, holePosition) < 0.6f) continue;
                best = h.distance;
                nearest = h;
                found = true;
            }
            return found;
        }

        /// <summary>
        /// Terrain, mini game map pieces and anything you can see. A collider with nothing drawn
        /// on it (course designers' invisible boundary walls and camera blockers) is not solid.
        /// </summary>
        private static bool IsSolid(Collider c)
        {
            if (c is TerrainCollider) return true;
            if (solidCache.TryGetValue(c, out bool solid)) return solid;
            solid = c.name.StartsWith("Map_", StringComparison.Ordinal)
                    || c.GetComponent<Renderer>() != null
                    || c.GetComponentInChildren<Renderer>() != null
                    || c.GetComponentInParent<LODGroup>() != null;
            if (solidCache.Count > 20000) solidCache.Clear();
            solidCache[c] = solid;
            return solid;
        }

        private static readonly System.Collections.Generic.Dictionary<Collider, bool> treeColliderCache = new System.Collections.Generic.Dictionary<Collider, bool>();

        /// <summary>
        /// A hit on a tree that the tree field already handles: a course tree object's collider
        /// (a single tree, not a forest band), or a terrain tree's collider (the hit is well above
        /// the terrain's ground there).
        /// </summary>
        private static bool IsTreeHit(RaycastHit h)
        {
            Collider c = h.collider;
            if (TreeField.Active == null) return false;
            if (c is TerrainCollider)
            {
                Terrain terrain = c.GetComponent<Terrain>();
                if (terrain == null || terrain.terrainData == null || terrain.terrainData.treeInstanceCount == 0) return false;
                float ground = terrain.SampleHeight(h.point) + terrain.transform.position.y;
                return h.point.y > ground + 0.4f && h.normal.y < 0.5f;
            }
            if (treeColliderCache.TryGetValue(c, out bool tree)) return tree;
            tree = false;
            for (Transform t = c.transform; t != null && !tree; t = t.parent)
                if (GolfSimZA.Courses.CourseSurvey.LooksLikeTree(t.name)) tree = true;
            if (!tree)
            {
                LODGroup lod = c.GetComponentInParent<LODGroup>();
                tree = lod != null && GolfSimZA.Courses.CourseSurvey.LooksLikeTree(lod.name);
            }
            // Only single trees (the tree field has them); a whole band of forest in one object stays solid.
            if (tree)
            {
                Bounds b = c.bounds;
                float radius = Mathf.Max(b.extents.x, b.extents.z);
                if (radius > 14f || radius > b.size.y * 1.3f) tree = false;
            }
            if (treeColliderCache.Count > 20000) treeColliderCache.Clear();
            treeColliderCache[c] = tree;
            return tree;
        }

        private static int rollBlockLogs;
        private static readonly string[] GroundWords = { "green", "fringe", "collar", "apron", "fairway", "rough", "tee", "bunker", "sand", "trap", "ground", "terrain", "course", "grass", "path", "cart", "road", "plane", "surface", "turf", "lip", "edge", "dirt", "mesh" };

        /// <summary>
        /// Something a ROLLING ball bounces off: a solid object standing at least 30 cm up off the
        /// ground (a tree, rock, wall, building, post). Ground pieces (green, fringe, fairway, bunker
        /// lips, paths ...) and low seams between them are rolled over.
        /// </summary>
        private static bool IsStandingObstacle(RaycastHit hit)
        {
            Collider c = hit.collider;
            if (c == null || c is TerrainCollider) return false;
            string n = c.name.ToLowerInvariant();
            foreach (string w in GroundWords) if (n.Contains(w)) return false;
            float ground = GroundProbe.HeightAt(hit.point + new Vector3(hit.normal.x, 0f, hit.normal.z) * 0.2f);
            return c.bounds.max.y - ground > 0.3f;
        }

        private static bool IsWater(Collider c) => c != null && c.name.StartsWith("Map_Water", StringComparison.Ordinal);

        /// <summary>A steep, hard surface (cliff, rock face, wall, trunk): the ball bounces off it and drops.</summary>
        private void WallBounce(Vector3 normal)
        {
            velocity = new Vector3(velocity.x * horizontalScale, velocity.y, velocity.z * horizontalScale);
            horizontalScale = 1f;
            float into = Vector3.Dot(velocity, normal);
            if (into < 0f)
            {
                Vector3 along = velocity - normal * into;
                velocity = along * 0.55f - normal * into * 0.35f;
            }
            currentSpinRpm *= 0.4f;
            airborne = true;
            rolling = false;
            ObstacleHits++;
            Debug.Log("[GolfSimZA] Ball hit an obstacle at " + ball.position.ToString("0.0") + " and bounced off.");
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

                // The run-out after landing starts at fairway pace; on the green it eases into green
                // speed as the ball slows (see StepRoll).
                landingRollDeceleration = fairwayRollDeceleration;
            }

            // Split velocity into the part into the ground and the part along it.
            float intoGround = Vector3.Dot(velocity, normal);
            Vector3 along = velocity - normal * intoGround;

            // Impact with the turf (rigid-ball bounce model):
            // * the bounce up gets less lively the harder the ball lands (turf absorbs it);
            // * friction takes speed off along the ground in proportion to the landing force,
            //   but the ball can never slow below its rolling speed - which backspin lowers,
            //   so a steep spinning wedge checks up and a shallow driver runs out.
            float normalSpeed = Mathf.Abs(intoGround);
            float restitution = Mathf.Clamp(bounceRetention - bounceSpeedLoss * normalSpeed, 0.08f, bounceRetention);
            float alongSpeed = along.magnitude;
            float spinSurfaceSpeed = BallRadius * currentSpinRpm * (2f * Mathf.PI / 60f);
            // Greens are soft and receptive: they grab a landing ball harder than a fairway.
            float friction = hasHole && IsOnGreen(ball.position) ? turfFriction * 1.4f : turfFriction;
            float afterFriction = alongSpeed - friction * (1f + restitution) * normalSpeed;
            float rollingLimit = 5f / 7f * alongSpeed - 2f / 7f * spinSurfaceSpeed * spinCheck;
            float newAlong = Mathf.Max(0f, Mathf.Max(afterFriction, rollingLimit));
            along = alongSpeed > 0.0001f ? along * (newAlong / alongSpeed) : Vector3.zero;
            float bounceSpeed = normalSpeed * restitution;
            currentSpinRpm *= 0.3f;

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

            // Rolling resistance plus grass drag that grows with speed: a fast ball is slowed
            // quickly, then it trickles out gently instead of stopping dead.
            float currentDeceleration = deceleration + (puttMode ? 0f : rollDragPerSpeed * speed);
            Vector3 direction = speed > 0.0001f ? horizontalVelocity / speed : Vector3.zero;

            horizontalVelocity += slopeAcceleration * dt;
            float newSpeed = Mathf.Max(0f, horizontalVelocity.magnitude - currentDeceleration * dt);
            horizontalVelocity = horizontalVelocity.sqrMagnitude > 0.000001f ? horizontalVelocity.normalized * newSpeed : direction * newSpeed;
            velocity = horizontalVelocity;

            Vector3 next = ball.position + velocity * dt;
            // Rolling into something steep and solid (a wall, rock, tree trunk, steep bank): it
            // bounces back instead of rolling through it.
            // (Not on the green, and never the ground the ball is rolling on - its own bumps and
            // seams are handled by the slope; only something standing up off the ground counts.)
            Collider rollingOn = GroundProbe.LastCollider;
            if (!(hasHole && IsOnGreen(ball.position))
                && Sweep(ball.position + Vector3.up * 0.03f, next + Vector3.up * 0.03f, out RaycastHit wall) && wall.normal.y < 0.6f && !IsWater(wall.collider)
                && wall.collider != rollingOn && IsStandingObstacle(wall))
            {
                if (rollBlockLogs++ < 30)
                    Debug.Log($"[GolfSimZA] Rolling ball stopped by '{wall.collider.name}' at {wall.point:0.00} (normal {wall.normal:0.00}).");
                Vector3 n = new Vector3(wall.normal.x, 0f, wall.normal.z).normalized;
                velocity = Vector3.Reflect(velocity, n) * 0.4f;
                return;
            }
            float nextHeight = GroundProbe.HeightAt(next);
            if (GroundProbe.LastWasWater)
            {
                // Rolled into the water.
                ball.position = new Vector3(next.x, ball.position.y, next.z);
                Splash(nextHeight);
                return;
            }
            if (ball.position.y - BallRadius - nextHeight > LedgeDrop)
            {
                // Rolled off a ledge (a cliff edge, a wall): the ball falls instead of jumping down.
                ball.position = new Vector3(next.x, ball.position.y, next.z);
                airborne = true;
                rolling = false;
                return;
            }
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

        /// <summary>The ball drops into water (mini game maps): it stops where it went in, just under the surface.</summary>
        private void Splash(float surface)
        {
            if (!landedOnce)
            {
                landedOnce = true;
                landingPosition = new Vector3(ball.position.x, surface, ball.position.z);
                carryMeters = HorizontalDistance(landingPosition, launchPosition);
                Landed?.Invoke(velocity.magnitude);
            }
            ball.position = new Vector3(ball.position.x, surface - BallRadius * 3f, ball.position.z);
            velocity = Vector3.zero;
            airborne = false;
            rolling = false;
            InWater = true;
            totalMeters = HorizontalDistance(ball.position, launchPosition);
            CompleteShot();
        }

        private float HorizontalDistance(Vector3 a, Vector3 b)
        {
            Vector3 delta = a - b;
            delta.y = 0f;
            return delta.magnitude / Mathf.Max(0.0001f, metersToUnity);
        }

        /// <summary>Steepest ground a ball can stay on (surface normal y; 0.7 is about 45 degrees).</summary>
        private const float SteepestRestNormalY = 0.7f;
        /// <summary>Flattest-enough ground to put a ball that slid off a steep face (about 30 degrees).</summary>
        private const float PlayableNormalY = 0.86f;

        /// <summary>
        /// A ball cannot come to rest on a face steeper than about 45 degrees (the foot of a wall, a
        /// cliff, a steep bank wedged against an object): in real life it slides or drops off it.
        /// It is moved to the nearest playable ground, preferring lower ground (where it would fall).
        /// </summary>
        private void SettleOffSteepGround()
        {
            if (!GroundProbe.TrySample(ball.position, out _, out Vector3 restNormal) || restNormal.y >= SteepestRestNormalY)
                return;
            Vector3 start = ball.position;
            float unit = Mathf.Max(0.0001f, metersToUnity);
            bool found = false;
            Vector3 best = start;
            float bestScore = float.MaxValue;
            for (float r = 0.5f; r <= 10f && !found; r += 0.5f)
            {
                int steps = Mathf.Clamp(Mathf.RoundToInt(r * 8f), 12, 48);
                for (int k = 0; k < steps; k++)
                {
                    float a = k * (Mathf.PI * 2f / steps);
                    Vector3 probe = start + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (r * unit);
                    probe.y = start.y + 30f * unit;
                    if (!GroundProbe.TrySample(probe, out float h, out Vector3 n) || n.y < PlayableNormalY || GroundProbe.LastWasWater)
                        continue;
                    if (!(GroundProbe.LastCollider is TerrainCollider) && GroundProbe.LastCollider != null && IsSolidObjectTop(GroundProbe.LastCollider))
                        continue;
                    // Nearest first; among equally near spots, the lower one (downhill, where it would drop).
                    float score = r + Mathf.Max(0f, h - start.y) / unit * 0.5f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = new Vector3(probe.x, h + BallRadius, probe.z);
                        found = true;
                    }
                }
            }
            if (!found)
                return;
            ball.position = best;
            totalMeters = HorizontalDistance(ball.position, launchPosition);
            Debug.Log($"[GolfSimZA] Ball could not stay on a steep face ({Mathf.Acos(Mathf.Clamp01(restNormal.y)) * Mathf.Rad2Deg:0} degrees) and dropped {HorizontalDistance(best, start):0.0} m to playable ground.");
        }

        /// <summary>The top of a building, rock or other standing object is not a place to play from.</summary>
        private static bool IsSolidObjectTop(Collider c)
        {
            string n = c.name.ToLowerInvariant();
            foreach (string w in GroundWords) if (n.Contains(w)) return false;
            return IsSolid(c);
        }

        private void CompleteShot()
        {
            if (!WasHoled && !InWater && !puttMode)
                SettleOffSteepGround();
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
