using GolfSimZA.Core;
using UnityEngine;

namespace GolfSimZA.Physics
{
    /// <summary>
    /// Draws the ball's flight path. Starts on launch, follows the ball through the whole flight and
    /// stops where the ball first lands. The line is widened with its distance from the camera so
    /// the whole trace stays visible up to the landing spot (a fixed 6 cm line vanishes after ~60 m).
    /// </summary>
    public sealed class ShotTracer : MonoBehaviour
    {
        [SerializeField] private Transform ball;
        [SerializeField] private int maxPoints = 2000;
        [SerializeField] private float minPointDistance = 0.25f;

        private LineRenderer line;
        private BallFlightSimulator flight;
        private Vector3 lastPoint;
        private bool tracking;

        private void Awake()
        {
            line = gameObject.GetComponent<LineRenderer>();
            if (line == null)
                line = gameObject.AddComponent<LineRenderer>();

            // Scenes saved an old limit of 600 points (150 m of flight at 25 cm spacing), which cut
            // long shots' traces off in mid-air. Enough points for any shot, all the way down.
            maxPoints = Mathf.Max(maxPoints, 4000);
            minPointDistance = Mathf.Clamp(minPointDistance, 0.1f, 0.5f);
            line.positionCount = 0;
            line.widthMultiplier = 0.06f;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.material = CreateTracerMaterial();
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            ApplyTrailSettings();
            AppSettings.Changed += ApplyTrailSettings;

            flight = GetComponent<BallFlightSimulator>();
            if (flight == null) flight = FindFirstObjectByType<BallFlightSimulator>();
            if (flight != null)
            {
                flight.Launched += OnLaunched;
                if (ball == null) ball = flight.Ball;
            }
        }

        private void OnDestroy()
        {
            AppSettings.Changed -= ApplyTrailSettings;
            if (flight != null) flight.Launched -= OnLaunched;
        }

        public static readonly string[] ColorNames = { "RED", "BLUE", "GREEN", "YELLOW", "GOLD", "NONE" };
        public static readonly Color[] Colors =
        {
            new Color(0.93f, 0.20f, 0.18f), new Color(0.22f, 0.55f, 0.98f), new Color(0.20f, 0.85f, 0.45f),
            new Color(1f, 0.92f, 0.20f), new Color(1f, 0.75f, 0.08f), Color.clear
        };

        /// <summary>Settings → CAMERA OPTIONS → BALL TRAIL colour and size.</summary>
        private void ApplyTrailSettings()
        {
            if (line == null) return;
            AppSettings s = AppSettings.Current;
            int index = Mathf.Clamp(s.ballTrailColor, 0, Colors.Length - 1);
            line.enabled = index != Colors.Length - 1;
            if (line.material != null) line.material.color = Colors[index];
            line.startColor = line.endColor = Colors[index];
            baseWidth = s.ballTrailThick ? 0.14f : 0.06f;
            line.widthMultiplier = 1f;
            UpdateWidth();
        }

        private float baseWidth = 0.06f;
        private readonly Keyframe[] widthKeys = new Keyframe[24];
        private AnimationCurve widthCurve;

        /// <summary>
        /// Each part of the trace gets a width that looks the same on screen (about 2.5 px at
        /// 1080p, 5 px thick) whatever its distance from the camera, never thinner than the base width.
        /// </summary>
        private void UpdateWidth()
        {
            if (line == null) return;
            int n = line.positionCount;
            Camera cam = Camera.main;
            if (n < 2 || cam == null)
            {
                line.widthCurve = AnimationCurve.Constant(0f, 1f, baseWidth);
                return;
            }
            float pixelAngle = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 2f / Mathf.Max(200f, Screen.height);
            float pixels = baseWidth > 0.1f ? 5f : 2.6f;
            int keys = Mathf.Min(widthKeys.Length, n);
            Vector3 camPos = cam.transform.position;
            for (int k = 0; k < keys; k++)
            {
                float t = keys == 1 ? 0f : k / (float)(keys - 1);
                int i = Mathf.Clamp(Mathf.RoundToInt(t * (n - 1)), 0, n - 1);
                float distance = Vector3.Distance(camPos, line.GetPosition(i));
                widthKeys[k] = new Keyframe(t, Mathf.Max(baseWidth, distance * pixelAngle * pixels));
            }
            if (widthCurve == null) widthCurve = new AnimationCurve();
            if (widthCurve.length != keys)
            {
                var exact = new Keyframe[keys];
                System.Array.Copy(widthKeys, exact, keys);
                widthCurve.keys = exact;
            }
            else
            {
                for (int k = 0; k < keys; k++) widthCurve.MoveKey(k, widthKeys[k]);
            }
            line.widthCurve = widthCurve;
        }

        private void OnLaunched(ShotData shot)
        {
            if (ball == null) return;
            tracking = true;
            line.positionCount = 1;
            line.SetPosition(0, ball.position);
            lastPoint = ball.position;
        }

        public void Clear()
        {
            tracking = false;
            if (line != null) line.positionCount = 0;
        }

        private void LateUpdate()
        {
            if (line != null && line.positionCount > 1) UpdateWidth();
            if (!tracking || ball == null || line == null || flight == null)
                return;

            Vector3 point = ball.position;
            if (Vector3.Distance(point, lastPoint) >= minPointDistance && line.positionCount < maxPoints)
            {
                line.positionCount++;
                line.SetPosition(line.positionCount - 1, point);
                lastPoint = point;
            }

            if (!flight.IsAirborne)
            {
                // Finish exactly at the landing spot.
                if (line.positionCount < maxPoints && Vector3.Distance(point, lastPoint) > 0.01f)
                {
                    line.positionCount++;
                    line.SetPosition(line.positionCount - 1, point);
                }
                tracking = false;
            }
        }

        private Material CreateTracerMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            Material material = new Material(shader);
            material.color = new Color(1f, 0.75f, 0.08f, 1f);
            return material;
        }
    }
}
