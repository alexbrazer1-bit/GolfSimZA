using GolfSimZA.Core;
using UnityEngine;

namespace GolfSimZA.Physics
{
    /// <summary>Draws the ball's flight path. Starts on launch and stops on first landing.</summary>
    public sealed class ShotTracer : MonoBehaviour
    {
        [SerializeField] private Transform ball;
        [SerializeField] private int maxPoints = 600;
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

            line.positionCount = 0;
            line.widthMultiplier = 0.06f;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.material = CreateTracerMaterial();
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

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
            if (flight != null) flight.Launched -= OnLaunched;
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
                tracking = false;
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
