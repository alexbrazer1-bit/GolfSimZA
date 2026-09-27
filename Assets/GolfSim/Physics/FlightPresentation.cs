using GolfSimZA.Core;
using UnityEngine;

namespace GolfSimZA.Physics
{
    public sealed class FlightPresentation : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BallFlightSimulator flight;
        [SerializeField] private Transform ball;
        [SerializeField] private Camera followCamera;

        [Header("Camera")]
        [SerializeField] private bool followDuringFlight = true;
        [SerializeField] private float cameraHeight = 4.5f;
        [SerializeField] private float cameraDistance = 8.5f;
        [SerializeField] private float cameraFollowSpeed = 5f;
        [SerializeField] private float cameraLookHeight = 1.0f;
        [SerializeField] private float maximumFollowDistance = 300f;

        [Header("Landing marker")]
        [SerializeField] private float markerRadius = 1.5f;
        [SerializeField] private float markerHeight = 0.035f;

        private GameObject landingMarker;
        private Vector3 homeCameraPosition;
        private Quaternion homeCameraRotation;
        private Vector3 followDirection = Vector3.forward;
        private Vector3 homeAimDirection = Vector3.forward;
        private bool cameraMoved;

        [Header("Address view (behind the ball)")]
        [SerializeField] private float addressBackDistance = 6.5f;
        [SerializeField] private float addressHeight = 2.2f;
        [SerializeField] private float addressLookAhead = 60f;
        [SerializeField] private float addressLookHeight = 1.5f;

        /// <summary>Changes the behind-the-ball view (the range uses a higher view looking down the range).</summary>
        public void ConfigureAddressView(float height, float backDistance, float lookAhead, float lookHeight)
        {
            addressHeight = Mathf.Max(0.5f, height);
            addressBackDistance = Mathf.Max(1f, backDistance);
            addressLookAhead = Mathf.Max(5f, lookAhead);
            addressLookHeight = lookHeight;
        }

        /// <summary>
        /// Moves the resting camera behind the ball, looking along the aim line.
        /// Called by the round whenever the ball is placed or the aim changes.
        /// </summary>
        public void SetAddress(Vector3 ballPosition, Vector3 aimDirection, bool snap)
        {
            if (followCamera == null) return;
            aimDirection.y = 0f;
            if (aimDirection.sqrMagnitude < 0.0001f) aimDirection = Vector3.forward;
            aimDirection.Normalize();

            homeCameraPosition = ballPosition - aimDirection * addressBackDistance + Vector3.up * addressHeight;
            Vector3 lookTarget = ballPosition + aimDirection * addressLookAhead + Vector3.up * addressLookHeight;
            homeCameraRotation = Quaternion.LookRotation(lookTarget - homeCameraPosition, Vector3.up);
            followDirection = aimDirection;
            homeAimDirection = aimDirection;

            if (snap || flight == null || !flight.IsInFlight)
            {
                cameraMoved = !snap;
                if (snap)
                {
                    followCamera.transform.position = homeCameraPosition;
                    followCamera.transform.rotation = homeCameraRotation;
                }
            }
        }

        private Coroutine flyover;

        /// <summary>Flies the camera along the hole (tee → aim points → pin), then calls done.</summary>
        public void PlayFlyover(System.Collections.Generic.List<Vector3> path, System.Action done)
        {
            if (followCamera == null || path == null || path.Count < 2) return;
            if (flyover != null) StopCoroutine(flyover);
            flyover = StartCoroutine(FlyoverRoutine(path, done));
        }

        public bool IsFlyingOver => flyover != null;

        private System.Collections.IEnumerator FlyoverRoutine(System.Collections.Generic.List<Vector3> path, System.Action done)
        {
            float total = 0f;
            for (int i = 1; i < path.Count; i++) total += Vector3.Distance(path[i - 1], path[i]);
            AppSettings settings = AppSettings.Current;
            float metersPerSecond = Mathf.Lerp(25f, 90f, Mathf.Clamp01(settings.flyoverSpeed));
            float heightAbove = Mathf.Lerp(12f, 60f, Mathf.Clamp01(settings.flyoverHeight));
            float duration = Mathf.Clamp(total / metersPerSecond, 3f, 16f);
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float along = Mathf.SmoothStep(0f, 1f, t / duration) * total;
                Vector3 point = PointAlong(path, along, out Vector3 forward);
                Vector3 camPos = point - forward * (heightAbove * 0.8f) + Vector3.up * heightAbove;
                float ground = GroundProbe.HeightAt(camPos);
                camPos.y = Mathf.Max(camPos.y, ground + heightAbove * 0.6f);
                followCamera.transform.position = Vector3.Lerp(followCamera.transform.position, camPos, 1f - Mathf.Exp(-4f * Time.deltaTime));
                Vector3 look = PointAlong(path, Mathf.Min(total, along + 60f), out _);
                followCamera.transform.rotation = Quaternion.Slerp(followCamera.transform.rotation, Quaternion.LookRotation(look - followCamera.transform.position, Vector3.up), 1f - Mathf.Exp(-4f * Time.deltaTime));
                yield return null;
            }
            flyover = null;
            cameraMoved = true;
            done?.Invoke();
        }

        private static Vector3 PointAlong(System.Collections.Generic.List<Vector3> path, float distance, out Vector3 forward)
        {
            forward = Vector3.forward;
            for (int i = 1; i < path.Count; i++)
            {
                float segment = Vector3.Distance(path[i - 1], path[i]);
                Vector3 dir = path[i] - path[i - 1];
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f) forward = dir.normalized;
                if (distance <= segment || i == path.Count - 1)
                    return Vector3.Lerp(path[i - 1], path[i], segment > 0.001f ? Mathf.Clamp01(distance / segment) : 1f);
                distance -= segment;
            }
            return path[path.Count - 1];
        }

        public void HideLandingMarker()
        {
            if (landingMarker != null) landingMarker.SetActive(false);
        }

        private void Awake()
        {
            if (flight == null)
                flight = GetComponent<BallFlightSimulator>();

            if (ball == null && flight != null)
            {
                Transform candidate = transform.Find("GolfBall");
                if (candidate != null)
                    ball = candidate;
            }

            if (followCamera == null)
                followCamera = Camera.main;

            if (followCamera != null)
            {
                homeCameraPosition = followCamera.transform.position;
                homeCameraRotation = followCamera.transform.rotation;
            }

            if (flight != null)
                flight.ShotCompleted += OnShotCompleted;

            ApplyVisualSettings();
            AppSettings.Changed += ApplyVisualSettings;
        }

        private void OnDestroy()
        {
            AppSettings.Changed -= ApplyVisualSettings;
            if (flight != null)
                flight.ShotCompleted -= OnShotCompleted;
        }

        /// <summary>Settings → VISUAL SETTINGS → CAMERA OPTIONS (follow camera, delay, fog, draw distance).</summary>
        public void ApplyVisualSettings()
        {
            AppSettings s = AppSettings.Current;
            followDuringFlight = s.followCamera;
            // Delay 0 = tight (fast follow), 1 = lazy (slow follow).
            cameraFollowSpeed = Mathf.Lerp(9f, 1.5f, Mathf.Clamp01(s.followDelay));
            if (followCamera != null)
                followCamera.farClipPlane = Mathf.Clamp(s.drawDistanceMeters, 500f, 15000f);
            ApplyFog();
        }

        /// <summary>Gradient fog on/off; the fog starts later with a longer draw distance.</summary>
        public static void ApplyFog()
        {
            AppSettings s = AppSettings.Current;
            if (!s.gradientFog)
            {
                RenderSettings.fog = false;
                return;
            }
            float draw = Mathf.Clamp(s.drawDistanceMeters, 500f, 15000f);
            // A course with its own fog keeps it; otherwise use the GolfSim ZA haze.
            if (RenderSettings.fog && RenderSettings.fogMode != FogMode.Linear) return;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = Mathf.Min(450f, draw * 0.25f);
            RenderSettings.fogEndDistance = Mathf.Max(RenderSettings.fogStartDistance + 200f, Mathf.Min(1600f, draw * 0.9f));
        }

        private void Update()
        {
            if (flyover != null) return;
            if (!followDuringFlight || flight == null || ball == null || followCamera == null)
                return;

            if (flight.IsInFlight)
            {
                cameraMoved = true;

                // Keep the camera on the range instead of chasing the ball's changing
                // forward direction. This prevents the camera from pitching into the sky.
                Vector3 flatBall = new Vector3(ball.position.x, 0f, ball.position.z);
                Vector3 flatHome = new Vector3(homeCameraPosition.x, 0f, homeCameraPosition.z);
                Vector3 travel = flatBall - flatHome;
                float travelDistance = Mathf.Min(travel.magnitude, maximumFollowDistance);

                if (travel.sqrMagnitude > 0.01f)
                    followDirection = travel.normalized;

                Vector3 desiredCenter = flatHome + followDirection * travelDistance;
                desiredCenter.y = ball.position.y;

                Vector3 target = desiredCenter + Vector3.up * cameraHeight - followDirection * cameraDistance;
                float groundUnderCamera = GroundProbe.HeightAt(target);
                if (target.y < groundUnderCamera + 1.5f) target.y = groundUnderCamera + 1.5f;
                followCamera.transform.position = Vector3.Lerp(
                    followCamera.transform.position,
                    target,
                    1f - Mathf.Exp(-cameraFollowSpeed * Time.deltaTime));

                Vector3 lookTarget = ball.position + Vector3.up * cameraLookHeight;
                Quaternion lookRotation = Quaternion.LookRotation(lookTarget - followCamera.transform.position, Vector3.up);
                followCamera.transform.rotation = Quaternion.Slerp(
                    followCamera.transform.rotation,
                    lookRotation,
                    1f - Mathf.Exp(-cameraFollowSpeed * Time.deltaTime));
            }
            else if (cameraMoved)
            {
                followCamera.transform.position = Vector3.Lerp(
                    followCamera.transform.position,
                    homeCameraPosition,
                    1f - Mathf.Exp(-2.5f * Time.deltaTime));
                followCamera.transform.rotation = Quaternion.Slerp(
                    followCamera.transform.rotation,
                    homeCameraRotation,
                    1f - Mathf.Exp(-2.5f * Time.deltaTime));

                if (Vector3.Distance(followCamera.transform.position, homeCameraPosition) < 0.05f)
                {
                    followCamera.transform.position = homeCameraPosition;
                    followCamera.transform.rotation = homeCameraRotation;
                    cameraMoved = false;
                    followDirection = homeAimDirection;
                }
            }
        }

        private void OnShotCompleted(ShotData shot, float carry, float total, float apex, float flightTime)
        {
            if (ball == null)
                return;

            CreateOrMoveLandingMarker(flight != null ? flight.LandingPosition : ball.position);
        }

        private void CreateOrMoveLandingMarker(Vector3 position)
        {
            if (landingMarker == null)
            {
                landingMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                landingMarker.name = "LastShot_LandingMarker";
                landingMarker.transform.localScale = new Vector3(markerRadius, markerHeight, markerRadius);
                landingMarker.layer = GroundProbe.IgnoreRaycastLayer;
                Collider markerCollider = landingMarker.GetComponent<Collider>();
                if (markerCollider != null) Destroy(markerCollider);

                Renderer renderer = landingMarker.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (shader == null)
                        shader = Shader.Find("Unlit/Color");
                    if (shader != null)
                    {
                        Material material = new Material(shader);
                        material.color = new Color(1f, 0.8f, 0.1f, 0.65f);
                        renderer.sharedMaterial = material;
                    }
                }
            }

            landingMarker.transform.position = new Vector3(position.x, GroundProbe.HeightAt(position) + 0.02f, position.z);
            landingMarker.SetActive(true);
        }
    }
}
