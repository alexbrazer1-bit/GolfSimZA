using System.Collections.Generic;
using GolfSimZA.Courses;
using GolfSimZA.Physics;
using UnityEngine;

namespace GolfSimZA.Visual
{
    /// <summary>
    /// Out-of-bounds and penalty-area lines on the course, like the painted lines and stakes on a
    /// real course: OUT OF BOUNDS (course boundary and inner out-of-bounds areas) = white line with
    /// white stakes, PENALTY AREAS (water, hazards) = red line with red stakes, free-drop areas =
    /// blue line. Lines follow the ground. The same shapes are drawn on the hole map
    /// (<see cref="DrawOnMap"/>).
    /// </summary>
    public static class CourseBoundaryLines
    {
        public static readonly Color OutOfBoundsColor = new Color(1f, 1f, 1f, 0.95f);
        public static readonly Color PenaltyColor = new Color(0.92f, 0.12f, 0.10f, 0.95f);
        public static readonly Color FreeDropColor = new Color(0.25f, 0.55f, 1f, 0.95f);

        private const float SampleStep = 3f;     // metres between line points (the line follows the ground)
        private const float StakeSpacing = 35f;  // metres between stakes
        private const float LineWidth = 0.28f;

        private static GameObject root;
        private static Material lineMaterial;
        private static readonly Dictionary<Color, Material> stakeMaterials = new Dictionary<Color, Material>();

        /// <summary>Removes the lines (new course, back to the menu).</summary>
        public static void Clear()
        {
            if (root != null) Object.Destroy(root);
            root = null;
        }

        /// <summary>Builds the lines for a loaded course (call when the course terrain is ready).</summary>
        public static void Build(CourseDefinition course)
        {
            Clear();
            if (course == null) return;
            root = new GameObject("GolfSimZA_BoundaryLines");
            int count = 0;
            if (course.outOfBounds != null && course.outOfBounds.IsValid)
            {
                AddPolygon(course.outOfBounds.points, OutOfBoundsColor, "OutOfBounds");
                count++;
            }
            if (course.hazards != null)
            {
                foreach (HazardDefinition h in course.hazards)
                {
                    if (h == null || h.area == null || !h.area.IsValid) continue;
                    Color c = h.innerOutOfBounds ? OutOfBoundsColor : h.freeDrop ? FreeDropColor : PenaltyColor;
                    AddPolygon(h.area.points, c, h.innerOutOfBounds ? "InnerOutOfBounds" : h.freeDrop ? "FreeDrop" : "PenaltyArea");
                    count++;
                }
            }
            Debug.Log("[GolfSimZA] Course boundary lines: " + count + " area(s) marked (white = out of bounds, red = penalty area, blue = free drop).");
        }

        private static Material LineMaterial()
        {
            if (lineMaterial != null) return lineMaterial;
            Shader shader = Shader.Find("Sprites/Default");
            lineMaterial = new Material(shader != null ? shader : Shader.Find("Unlit/Color"));
            return lineMaterial;
        }

        private static void AddPolygon(Vector3[] corners, Color color, string name)
        {
            // Resample every few metres so the line hugs the ground over hills.
            var points = new List<Vector3>();
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 a = corners[i], b = corners[(i + 1) % corners.Length];
                a.y = 0f; b.y = 0f;
                float length = Vector3.Distance(a, b);
                int steps = Mathf.Max(1, Mathf.CeilToInt(length / SampleStep));
                for (int s = 0; s < steps; s++)
                {
                    Vector3 p = Vector3.Lerp(a, b, s / (float)steps);
                    p.y = GroundProbe.HeightAt(p) + 0.07f;
                    points.Add(p);
                }
            }
            if (points.Count < 2) return;
            points.Add(points[0]);

            // LineRenderers above ~ 16k points get slow to edit: split long boundaries into pieces.
            const int Piece = 2000;
            for (int start = 0; start < points.Count - 1; start += Piece - 1)
            {
                int n = Mathf.Min(Piece, points.Count - start);
                var go = new GameObject(name + "_Line");
                go.transform.SetParent(root.transform, false);
                go.layer = GroundProbe.IgnoreRaycastLayer;
                // Flat on the ground: the line's width lies in the horizontal plane.
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                LineRenderer line = go.AddComponent<LineRenderer>();
                line.material = LineMaterial();
                line.alignment = LineAlignment.TransformZ;
                line.useWorldSpace = true;
                line.positionCount = n;
                line.SetPositions(points.GetRange(start, n).ToArray());
                line.startColor = line.endColor = color;
                line.widthMultiplier = LineWidth;
                line.numCornerVertices = 2;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
            }

            // Stakes along the line.
            float walked = 0f, nextStake = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                float seg = Vector3.Distance(points[i - 1], points[i]);
                while (nextStake <= walked + seg)
                {
                    Vector3 p = Vector3.Lerp(points[i - 1], points[i], seg > 0.0001f ? (nextStake - walked) / seg : 0f);
                    Stake(p, color);
                    nextStake += StakeSpacing;
                }
                walked += seg;
            }
        }

        private static void Stake(Vector3 groundPoint, Color color)
        {
            GameObject stake = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stake.name = "Stake";
            Object.Destroy(stake.GetComponent<Collider>());
            stake.layer = GroundProbe.IgnoreRaycastLayer;
            stake.transform.SetParent(root.transform, false);
            stake.transform.position = new Vector3(groundPoint.x, groundPoint.y + 0.45f, groundPoint.z);
            stake.transform.localScale = new Vector3(0.06f, 0.45f, 0.06f);
            Renderer r = stake.GetComponent<Renderer>();
            if (!stakeMaterials.TryGetValue(color, out Material m) || m == null)
            {
                Shader shader = Shader.Find("Standard");
                m = shader != null ? new Material(shader) { color = new Color(color.r, color.g, color.b, 1f), enableInstancing = true } : r.sharedMaterial;
                stakeMaterials[color] = m;
            }
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------ Hole map

        /// <summary>
        /// Draws the boundary lines on a map (inside the current GUI clip). toMap turns a world
        /// point into a GUI point (already relative to the clip).
        /// </summary>
        public static void DrawOnMap(CourseDefinition course, System.Func<Vector3, Vector2> toMap, Rect clip, float width)
        {
            if (course == null || Event.current.type != EventType.Repaint) return;
            if (course.outOfBounds != null && course.outOfBounds.IsValid)
                MapPolygon(course.outOfBounds.points, toMap, clip, OutOfBoundsColor, width);
            if (course.hazards != null)
                foreach (HazardDefinition h in course.hazards)
                {
                    if (h == null || h.area == null || !h.area.IsValid) continue;
                    MapPolygon(h.area.points, toMap, clip, h.innerOutOfBounds ? OutOfBoundsColor : h.freeDrop ? FreeDropColor : PenaltyColor, width);
                }
            GUI.color = Color.white;
        }

        private static void MapPolygon(Vector3[] pts, System.Func<Vector3, Vector2> toMap, Rect clip, Color color, float width)
        {
            Vector2 prev = toMap(pts[pts.Length - 1]);
            for (int i = 0; i < pts.Length; i++)
            {
                Vector2 cur = toMap(pts[i]);
                // Only the part on the map picture is drawn. Dark edge under the coloured line so
                // white shows on a light map too.
                Line(prev, cur, width + 2f, new Color(0f, 0f, 0f, 0.55f), clip);
                Line(prev, cur, width, color, clip);
                prev = cur;
            }
        }

        /// <summary>
        /// A straight GUI line between two points, drawn as small axis-aligned squares so it is
        /// clipped correctly inside GUI.BeginClip areas (a rotated GUI matrix escapes the clip).
        /// With a limit rect, only the part inside it is drawn.
        /// </summary>
        public static void Line(Vector2 a, Vector2 b, float width, Color color, Rect? limit = null)
        {
            if (limit.HasValue && !ClipSegment(ref a, ref b, limit.Value)) return;
            Vector2 d = b - a;
            float length = d.magnitude;
            if (length < 0.5f) return;
            GUI.color = color;
            float half = width * 0.5f;
            if (Mathf.Abs(d.y) < 0.5f)
                GUI.DrawTexture(new Rect(Mathf.Min(a.x, b.x), a.y - half, Mathf.Abs(d.x), width), Texture2D.whiteTexture);
            else if (Mathf.Abs(d.x) < 0.5f)
                GUI.DrawTexture(new Rect(a.x - half, Mathf.Min(a.y, b.y), width, Mathf.Abs(d.y)), Texture2D.whiteTexture);
            else
            {
                float step = Mathf.Max(1f, width * 0.6f);
                int n = Mathf.Min(3000, Mathf.CeilToInt(length / step));
                for (int i = 0; i <= n; i++)
                {
                    Vector2 p = Vector2.Lerp(a, b, i / (float)n);
                    GUI.DrawTexture(new Rect(p.x - half, p.y - half, width, width), Texture2D.whiteTexture);
                }
            }
        }

        /// <summary>Cuts a segment to a rectangle (Liang-Barsky). False when it is entirely outside.</summary>
        private static bool ClipSegment(ref Vector2 a, ref Vector2 b, Rect r)
        {
            float t0 = 0f, t1 = 1f;
            Vector2 d = b - a;
            float[] p = { -d.x, d.x, -d.y, d.y };
            float[] q = { a.x - r.xMin, r.xMax - a.x, a.y - r.yMin, r.yMax - a.y };
            for (int i = 0; i < 4; i++)
            {
                if (Mathf.Abs(p[i]) < 1e-6f)
                {
                    if (q[i] < 0f) return false;
                    continue;
                }
                float t = q[i] / p[i];
                if (p[i] < 0f) { if (t > t1) return false; if (t > t0) t0 = t; }
                else { if (t < t0) return false; if (t < t1) t1 = t; }
            }
            Vector2 start = a + d * t0, end = a + d * t1;
            a = start;
            b = end;
            return true;
        }
    }
}
