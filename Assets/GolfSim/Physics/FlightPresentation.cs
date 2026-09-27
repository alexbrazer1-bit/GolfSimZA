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
            Vector3 lookTarget = ballPosition + aimDirection * addressLookAhead + Vector3.up * 1.5f;
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
                followCamera.farClipPlane = Mathf.Max(followCamera.farClipPlane, 4000f);
                homeCameraPosition = followCamera.transform.position;
                homeCameraRotation = followCamera.transform.rotation;
            }

            if (flight != null)
                flight.ShotCompleted += OnShotCompleted;
        }

        private void OnDestroy()
        {
            if (flight != null)
                flight.ShotCompleted -= OnShotCompleted;
        }

        private void Update()
        {
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
