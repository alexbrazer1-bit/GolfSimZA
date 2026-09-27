using UnityEngine;

namespace GolfSimZA.Physics
{
    /// <summary>
    /// Ground markers on the play screen: the gimme circle around the hole and the aim
    /// indicator line from the ball (Settings → VISUAL SETTINGS → USER INTERFACE).
    /// </summary>
    public sealed class CourseMarkers : MonoBehaviour
    {
        private const int CircleSegments = 48;
        private const int AimSegments = 24;
        private LineRenderer circle, aim;

        public static readonly Color[] GimmeColors = { new Color(0.95f, 0.22f, 0.20f, 0.9f), new Color(0.25f, 0.60f, 1f, 0.9f), new Color(1f, 1f, 1f, 0.9f) };

        private void Awake()
        {
            circle = MakeLine("GolfSimZA_GimmeCircle", CircleSegments + 1, 0.05f);
            circle.loop = false;
            aim = MakeLine("GolfSimZA_AimIndicator", AimSegments, 0.10f);
            circle.gameObject.SetActive(false);
            aim.gameObject.SetActive(false);
        }

        private LineRenderer MakeLine(string name, int points, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.layer = GroundProbe.IgnoreRaycastLayer;
            LineRenderer line = go.AddComponent<LineRenderer>();
            Shader shader = Shader.Find("Sprites/Default");
            line.material = new Material(shader != null ? shader : Shader.Find("Unlit/Color"));
            line.positionCount = points;
            line.widthMultiplier = width;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCapVertices = 2;
            return line;
        }

        public void ShowGimme(Vector3 hole, float radius, Color color)
        {
            if (circle == null) return;
            if (radius <= 0.01f) { HideGimme(); return; }
            circle.gameObject.SetActive(true);
            circle.startColor = circle.endColor = color;
            for (int i = 0; i <= CircleSegments; i++)
            {
                float a = i / (float)CircleSegments * Mathf.PI * 2f;
                Vector3 p = hole + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                p.y = GroundProbe.HeightAt(p) + 0.02f;
                circle.SetPosition(i, p);
            }
        }

        public void HideGimme()
        {
            if (circle != null) circle.gameObject.SetActive(false);
        }

        /// <summary>Line along the aim direction, following the ground, fading out.</summary>
        public void ShowAim(Vector3 from, Vector3 direction, float length)
        {
            if (aim == null) return;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f || length < 1f) { HideAim(); return; }
            direction.Normalize();
            aim.gameObject.SetActive(true);
            aim.startColor = new Color(1f, 1f, 1f, 0.55f);
            aim.endColor = new Color(1f, 1f, 1f, 0f);
            // Thin near the ball, a little wider far away so it stays visible.
            aim.widthCurve = new AnimationCurve(new Keyframe(0f, 0.05f), new Keyframe(1f, Mathf.Clamp(length * 0.0015f, 0.08f, 0.35f)));
            aim.widthMultiplier = 1f;
            float start = Mathf.Min(3f, length * 0.2f);
            for (int i = 0; i < AimSegments; i++)
            {
                Vector3 p = from + direction * (start + (length - start) * i / (AimSegments - 1f));
                p.y = GroundProbe.HeightAt(p) + 0.05f;
                aim.SetPosition(i, p);
            }
        }

        public void HideAim()
        {
            if (aim != null) aim.gameObject.SetActive(false);
        }
    }
}
