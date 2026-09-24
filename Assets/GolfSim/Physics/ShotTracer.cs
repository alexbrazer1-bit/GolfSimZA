using UnityEngine;

namespace GolfSimZA.Physics
{
    public sealed class ShotTracer : MonoBehaviour
    {
        [SerializeField] private Transform ball;
        [SerializeField] private int maxPoints = 180;
        [SerializeField] private float minPointDistance = 0.08f;

        private LineRenderer line;
        private Vector3 lastPoint;
        private bool tracking;

        private void Awake()
        {
            line = gameObject.GetComponent<LineRenderer>();
            if (line == null)
                line = gameObject.AddComponent<LineRenderer>();

            line.positionCount = 0;
            line.widthMultiplier = 0.035f;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.material = CreateTracerMaterial();
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        private void Update()
        {
            if (ball == null || line == null)
                return;

            Vector3 point = ball.position;

            if (!tracking)
            {
                if (point.y > 0.08f)
                    Begin(point);
                return;
            }

            if (Vector3.Distance(point, lastPoint) >= minPointDistance && line.positionCount < maxPoints)
            {
                line.positionCount++;
                line.SetPosition(line.positionCount - 1, point);
                lastPoint = point;
            }

            if (point.y <= 0.06f && line.positionCount > 2)
                tracking = false;
        }

        private void Begin(Vector3 point)
        {
            tracking = true;
            line.positionCount = 1;
            line.SetPosition(0, point);
            lastPoint = point;
        }

        private Material CreateTracerMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            Material material = new Material(shader);
            material.color = new Color(1f, 0.75f, 0.08f, 1f);
            return material;
        }
    }
}
