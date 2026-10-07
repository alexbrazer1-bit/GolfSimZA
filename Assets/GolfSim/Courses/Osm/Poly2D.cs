using System;
using System.Collections.Generic;
using UnityEngine;

namespace GolfSimZA.Courses.Osm
{
    /// <summary>2D polygon / polyline helpers (metres, x = east, y = north).</summary>
    public static class Poly2D
    {
        public static float SignedArea(Vector2[] p)
        {
            double a = 0;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++) a += (double)p[j].x * p[i].y - (double)p[i].x * p[j].y;
            return (float)(a * 0.5);
        }

        public static float Area(Vector2[] p) => Mathf.Abs(SignedArea(p));

        public static Vector2 Centroid(Vector2[] p)
        {
            double a = 0, cx = 0, cy = 0;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            {
                double cross = (double)p[j].x * p[i].y - (double)p[i].x * p[j].y;
                a += cross;
                cx += (p[j].x + p[i].x) * cross;
                cy += (p[j].y + p[i].y) * cross;
            }
            if (Math.Abs(a) < 1e-6)
            {
                Vector2 s = Vector2.zero;
                foreach (Vector2 v in p) s += v;
                return p.Length > 0 ? s / p.Length : Vector2.zero;
            }
            a *= 0.5;
            return new Vector2((float)(cx / (6 * a)), (float)(cy / (6 * a)));
        }

        public static bool Contains(Vector2[] p, Vector2 q)
        {
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            {
                if ((p[i].y > q.y) != (p[j].y > q.y) &&
                    q.x < (p[j].x - p[i].x) * (q.y - p[i].y) / (p[j].y - p[i].y + 1e-9f) + p[i].x)
                    inside = !inside;
            }
            return inside;
        }

        public static Vector2 ClosestOnSegment(Vector2 a, Vector2 b, Vector2 q)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return a + ab * t;
        }

        /// <summary>Distance from q to the polygon's edge (positive outside and inside alike).</summary>
        public static float EdgeDistance(Vector2[] p, Vector2 q)
        {
            float best = float.MaxValue;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                best = Mathf.Min(best, (ClosestOnSegment(p[j], p[i], q) - q).sqrMagnitude);
            return Mathf.Sqrt(best);
        }

        /// <summary>Signed distance: negative inside the polygon.</summary>
        public static float SignedDistance(Vector2[] p, Vector2 q) => Contains(p, q) ? -EdgeDistance(p, q) : EdgeDistance(p, q);

        public static float Distance(Vector2[] polygon, Vector2 q) => Contains(polygon, q) ? 0f : EdgeDistance(polygon, q);

        public static Vector2 ClosestOnEdge(Vector2[] p, Vector2 q)
        {
            float best = float.MaxValue;
            Vector2 result = q;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            {
                Vector2 c = ClosestOnSegment(p[j], p[i], q);
                float d = (c - q).sqrMagnitude;
                if (d < best) { best = d; result = c; }
            }
            return result;
        }

        public static float LineLength(Vector2[] line)
        {
            float l = 0f;
            for (int i = 1; i < line.Length; i++) l += Vector2.Distance(line[i - 1], line[i]);
            return l;
        }

        /// <summary>Point at a distance along a polyline (clamped to the ends).</summary>
        public static Vector2 PointAlong(Vector2[] line, float distance)
        {
            if (line.Length == 0) return Vector2.zero;
            if (distance <= 0f) return line[0];
            for (int i = 1; i < line.Length; i++)
            {
                float seg = Vector2.Distance(line[i - 1], line[i]);
                if (distance <= seg) return Vector2.Lerp(line[i - 1], line[i], seg > 0f ? distance / seg : 0f);
                distance -= seg;
            }
            return line[line.Length - 1];
        }

        public static float LineDistance(Vector2[] line, Vector2 q)
        {
            float best = float.MaxValue;
            for (int i = 1; i < line.Length; i++) best = Mathf.Min(best, (ClosestOnSegment(line[i - 1], line[i], q) - q).sqrMagnitude);
            return line.Length == 1 ? Vector2.Distance(line[0], q) : Mathf.Sqrt(best);
        }

        public static void Bounds(Vector2[] p, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector2 v in p) { min = Vector2.Min(min, v); max = Vector2.Max(max, v); }
        }

        /// <summary>Ear-clipping triangulation of a simple polygon. Returns triangle indices into p.</summary>
        public static List<int> Triangulate(Vector2[] p)
        {
            var result = new List<int>();
            int n = p.Length;
            if (n < 3) return result;
            var idx = new List<int>(n);
            bool ccw = SignedArea(p) > 0f;
            for (int i = 0; i < n; i++) idx.Add(ccw ? i : n - 1 - i);

            int guard = 0;
            while (idx.Count > 3 && guard++ < n * n)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int a = idx[(i + idx.Count - 1) % idx.Count], b = idx[i], c = idx[(i + 1) % idx.Count];
                    Vector2 pa = p[a], pb = p[b], pc = p[c];
                    float cross = (pb.x - pa.x) * (pc.y - pa.y) - (pb.y - pa.y) * (pc.x - pa.x);
                    if (cross <= 1e-7f) continue; // reflex or flat
                    bool empty = true;
                    for (int k = 0; k < idx.Count; k++)
                    {
                        int v = idx[k];
                        if (v == a || v == b || v == c) continue;
                        if (InTriangle(p[v], pa, pb, pc)) { empty = false; break; }
                    }
                    if (!empty) continue;
                    result.Add(a); result.Add(b); result.Add(c);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped)
                {
                    // Self-touching or messy polygon: fan the rest so nothing is left open.
                    for (int i = 1; i + 1 < idx.Count; i++) { result.Add(idx[0]); result.Add(idx[i]); result.Add(idx[i + 1]); }
                    return result;
                }
            }
            if (idx.Count == 3) { result.Add(idx[0]); result.Add(idx[1]); result.Add(idx[2]); }
            return result;
        }

        private static bool InTriangle(Vector2 q, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (q.x - b.x) * (a.y - b.y) - (a.x - b.x) * (q.y - b.y);
            float d2 = (q.x - c.x) * (b.y - c.y) - (b.x - c.x) * (q.y - c.y);
            float d3 = (q.x - a.x) * (c.y - a.y) - (c.x - a.x) * (q.y - a.y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0;
            bool pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        /// <summary>
        /// Fills a polygon into a grid (scanline, cell centres). cellSize metres, grid origin at
        /// gridMin. Calls fill(x, y) for every cell whose centre is inside.
        /// </summary>
        public static void Rasterize(Vector2[] p, Vector2 gridMin, float cellSize, int width, int height, Action<int, int> fill)
        {
            if (p.Length < 3) return;
            Bounds(p, out Vector2 min, out Vector2 max);
            int y0 = Mathf.Max(0, Mathf.FloorToInt((min.y - gridMin.y) / cellSize));
            int y1 = Mathf.Min(height - 1, Mathf.CeilToInt((max.y - gridMin.y) / cellSize));
            var xs = new List<float>();
            for (int gy = y0; gy <= y1; gy++)
            {
                float wy = gridMin.y + (gy + 0.5f) * cellSize;
                xs.Clear();
                for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                {
                    Vector2 a = p[j], b = p[i];
                    if ((a.y > wy) != (b.y > wy))
                        xs.Add(a.x + (wy - a.y) / (b.y - a.y) * (b.x - a.x));
                }
                xs.Sort();
                for (int k = 0; k + 1 < xs.Count; k += 2)
                {
                    int x0 = Mathf.Max(0, Mathf.CeilToInt((xs[k] - gridMin.x) / cellSize - 0.5f));
                    int x1 = Mathf.Min(width - 1, Mathf.FloorToInt((xs[k + 1] - gridMin.x) / cellSize - 0.5f));
                    for (int gx = x0; gx <= x1; gx++) fill(gx, gy);
                }
            }
        }

        /// <summary>Cells within halfWidth of a polyline.</summary>
        public static void RasterizeLine(Vector2[] line, float halfWidth, Vector2 gridMin, float cellSize, int width, int height, Action<int, int> fill)
        {
            for (int i = 1; i < line.Length; i++)
            {
                Vector2 a = line[i - 1], b = line[i];
                Vector2 min = Vector2.Min(a, b) - Vector2.one * halfWidth, max = Vector2.Max(a, b) + Vector2.one * halfWidth;
                int x0 = Mathf.Max(0, Mathf.FloorToInt((min.x - gridMin.x) / cellSize));
                int x1 = Mathf.Min(width - 1, Mathf.CeilToInt((max.x - gridMin.x) / cellSize));
                int y0 = Mathf.Max(0, Mathf.FloorToInt((min.y - gridMin.y) / cellSize));
                int y1 = Mathf.Min(height - 1, Mathf.CeilToInt((max.y - gridMin.y) / cellSize));
                for (int gy = y0; gy <= y1; gy++)
                    for (int gx = x0; gx <= x1; gx++)
                    {
                        Vector2 c = new Vector2(gridMin.x + (gx + 0.5f) * cellSize, gridMin.y + (gy + 0.5f) * cellSize);
                        if ((ClosestOnSegment(a, b, c) - c).sqrMagnitude <= halfWidth * halfWidth) fill(gx, gy);
                    }
            }
        }
    }
}
