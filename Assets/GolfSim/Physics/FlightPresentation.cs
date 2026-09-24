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

        [Header("Landing marker")]
        [SerializeField] private float markerRadius = 1.5f;
        [SerializeField] private float markerHeight = 0.035f;

        private GameObject landingMarker;
        private Vector3 homeCameraPosition;
        private Quaternion homeCameraRotation;
        private bool cameraMoved;

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
                Vector3 target = ball.position + Vector3.up * cameraHeight - followCamera.transform.forward * cameraDistance;
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
                }
            }
        }

        private void OnShotCompleted(ShotData shot, float carry, float total, float apex, float flightTime)
        {
            if (ball == null)
                return;

            CreateOrMoveLandingMarker(ball.position);
        }

        private void CreateOrMoveLandingMarker(Vector3 position)
        {
            if (landingMarker == null)
            {
                landingMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                landingMarker.name = "LastShot_LandingMarker";
                landingMarker.transform.localScale = new Vector3(markerRadius, markerHeight, markerRadius);

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

            landingMarker.transform.position = new Vector3(position.x, 0.02f, position.z);
            landingMarker.SetActive(true);
        }
    }
}
