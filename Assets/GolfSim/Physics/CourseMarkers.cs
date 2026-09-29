using System.Collections.Generic;
using UnityEngine;

namespace GolfSimZA.Physics
{
    /// <summary>
    /// Ground markers on the play screen: the gimme circle around the hole, the auto putt
    /// 1-putt (gold) and 2-putt (white) circles on the green, and the aim indicator line
    /// from the ball (Settings → VISUAL SETTINGS → USER INTERFACE, Settings → GAME → AUTO PUTT).
    /// </summary>
    public sealed class CourseMarkers : MonoBehaviour
    {
        private const int CircleSegments = 48;
        private const int AimSegments = 24;
        private LineRenderer circle, aim, onePutt, twoPutt;
        private Vector3 puttCentre;

        public static readonly Color OnePuttColor = new Color(1f, 0.80f, 0.20f, 0.95f);
        public static readonly Color TwoPuttColor = new Color(1f, 1f, 1f, 0.85f);

        /// <summary>True while the putt circles are drawn (the HUD labels them).</summary>
        public bool PuttCirclesVisible => puttVisible;

        public static readonly Color[] GimmeColors = { new Color(0.95f, 0.22f, 0.20f, 0.9f), new Color(0.25f, 0.60f, 1f, 0.9f), new Color(1f, 1f, 1f, 0.9f) };

        private void Awake()
        {
            circle = MakeLine("GolfSimZA_GimmeCircle", CircleSegments + 1, 0.05f);
            circle.loop = false;
            aim = MakeLine("GolfSimZA_AimIndicator", AimSegments, 0.10f);
            onePutt = MakeLine("GolfSimZA_OnePuttCircle", CircleSegments * 2 + 1, 0.06f);
            twoPutt = MakeLine("GolfSimZA_TwoPuttCircle", CircleSegments * 2 + 1, 0.06f);
            circle.gameObject.SetActive(false);
            aim.gameObject.SetActive(false);
            onePutt.gameObject.SetActive(false);
            twoPutt.gameObject.SetActive(false);
        }

        /// <summary>Circles stay visible from the fairway: thicker the further the camera is.</summary>
        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            float d = Vector3.Distance(cam.transform.position, puttCentre);
            float width = Mathf.Clamp(d * 0.0022f, 0.05f, 0.7f);
            foreach (LineRenderer l in onePuttParts) if (l != null && l.gameObject.activeSelf) l.widthMultiplier = width;
            foreach (LineRenderer l in twoPuttParts) if (l != null && l.gameObject.activeSelf) l.widthMultiplier = width;
            if (circle != null && circle.gameObject.activeSelf) circle.widthMultiplier = Mathf.Clamp(d * 0.0018f, 0.05f, 0.5f);
        }

        /// <summary>
        /// Auto putt circles around the hole (radii in metres). With onGreen given, only the parts
        /// of each circle that lie on the putting green are drawn (the circles never leave the green).
        /// </summary>
        public void ShowPuttCircles(Vector3 hole, float oneRadius, float twoRadius, System.Func<Vector3, bool> onGreen = null)
        {
            if (onePutt == null) return;
            bool changed = (hole - puttCentre).sqrMagnitude > 0.0001f || !puttVisible
                           || Mathf.Abs(lastOne - oneRadius) > 0.001f || Mathf.Abs(lastTwo - twoRadius) > 0.001f;
            puttCentre = hole;
            puttVisible = true;
            if (!changed) return;
            lastOne = oneRadius;
            lastTwo = twoRadius;
            DrawClipped(onePuttParts, onePutt, hole, oneRadius, OnePuttColor, onGreen);
            DrawClipped(twoPuttParts, twoPutt, hole, twoRadius, TwoPuttColor, onGreen);
        }

        private bool puttVisible;
        private readonly List<LineRenderer> onePuttParts = new List<LineRenderer>();
        private readonly List<LineRenderer> twoPuttParts = new List<LineRenderer>();
        private float lastOne = -1f, lastTwo = -1f;

        /// <summary>Right-hand point of a circle that lies on the green (for its label), if any.</summary>
        public bool LabelPoint(Vector3 hole, float radius, Vector3 right, System.Func<Vector3, bool> onGreen, out Vector3 point)
        {
            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f * (i % 2 == 0 ? 1f : -1f) * 0.5f;
                point = hole + Quaternion.Euler(0f, a, 0f) * right * radius;
                if (onGreen == null || onGreen(point)) return true;
            }
            point = hole;
            return false;
        }

        /// <summary>A circle split into the arcs that are on the green (one line per arc).</summary>
        private void DrawClipped(List<LineRenderer> parts, LineRenderer first, Vector3 centre, float radius, Color color, System.Func<Vector3, bool> onGreen)
        {
            if (parts.Count == 0) parts.Add(first);
            const int n = CircleSegments * 2;
            var points = new Vector3[n];
            var on = new bool[n];
            bool all = true;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                Vector3 p = centre + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                p.y = GroundProbe.HeightAt(p) + 0.03f;
                points[i] = p;
                on[i] = onGreen == null || onGreen(p);
                all &= on[i];
            }

            var runs = new List<List<Vector3>>();
            if (all)
            {
                var loop = new List<Vector3>(points) { points[0] };
                runs.Add(loop);
            }
            else
            {
                // Start just after an off-green point so every arc is continuous.
                int start = 0;
                for (int i = 0; i < n; i++) if (!on[i]) { start = i; break; }
                List<Vector3> run = null;
                for (int k = 1; k <= n; k++)
                {
                    int i = (start + k) % n;
                    if (on[i])
                    {
                        if (run == null) { run = new List<Vector3>(); runs.Add(run); }
                        run.Add(points[i]);
                    }
                    else run = null;
                }
            }

            while (parts.Count < runs.Count) parts.Add(MakeLine("GolfSimZA_PuttArc", 2, 0.06f));
            for (int r = 0; r < parts.Count; r++)
            {
                LineRenderer line = parts[r];
                bool show = r < runs.Count && runs[r].Count >= 2;
                line.gameObject.SetActive(show);
                if (!show) continue;
                line.loop = false;
                line.startColor = line.endColor = color;
                line.positionCount = runs[r].Count;
                line.SetPositions(runs[r].ToArray());
            }
        }

        public void HidePuttCircles()
        {
            puttVisible = false;
            foreach (LineRenderer l in onePuttParts) if (l != null) l.gameObject.SetActive(false);
            foreach (LineRenderer l in twoPuttParts) if (l != null) l.gameObject.SetActive(false);
            if (onePutt != null) onePutt.gameObject.SetActive(false);
            if (twoPutt != null) twoPutt.gameObject.SetActive(false);
        }

        private static void DrawCircle(LineRenderer line, Vector3 centre, float radius, Color color, float lift)
        {
            line.startColor = line.endColor = color;
            int n = line.positionCount;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)(n - 1) * Mathf.PI * 2f;
                Vector3 p = centre + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                p.y = GroundProbe.HeightAt(p) + lift;
                line.SetPosition(i, p);
            }
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
            puttCentre = hole;
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
