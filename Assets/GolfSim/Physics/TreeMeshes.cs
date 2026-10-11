using System;
using System.Collections.Generic;
using UnityEngine;

namespace GolfSimZA.Physics
{
    /// <summary>
    /// The GolfSim ZA tree models, built in code: leaf-cluster cards around an inner crown,
    /// trunks and branches with bark, palms with leafy fronds, pines with drooping needle
    /// whorls, bushes and reeds. Every model has a near version (full detail) and a far version
    /// (a few large cards). All models share one texture atlas drawn here (leaves, needles,
    /// palm leaflets, reed blades, dense leaves, bark). Models are 1 unit tall, about 1 unit wide.
    /// Submesh 0 = bark, submesh 1 = leaves. Vertex colour: light / shade of each part
    /// (rgb) and how loose it is in the wind (a).
    /// </summary>
    public static class TreeMeshes
    {
        public const int Size = 512;

        // Atlas regions (u0, v0, u1, v1).
        private static readonly Rect LeafCluster = R(0, 0, 256, 256);
        private static readonly Rect Needles = R(256, 0, 256, 256);
        private static readonly Rect Frond = R(0, 256, 256, 256);
        private static readonly Rect Reeds = R(256, 256, 128, 256);
        private static readonly Rect DenseLeaves = R(384, 256, 128, 128);
        private static readonly Rect Bark = R(384, 384, 128, 128);

        private static Rect R(float x, float y, float w, float h)
        {
            const float inset = 3f;
            return new Rect((x + inset) / Size, (y + inset) / Size, (w - inset * 2f) / Size, (h - inset * 2f) / Size);
        }

        private static Texture2D atlas;

        // ------------------------------------------------------------------ texture

        /// <summary>The shared tree texture (built once).</summary>
        public static Texture2D Atlas()
        {
            if (atlas != null) return atlas;
            var px = new Color[Size * Size];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.72f, 0.74f, 0.66f, 0f); // no dark fringes
            var rnd = new System.Random(4711);
            DrawLeafCluster(px, rnd, 0, 0, 256);
            DrawNeedles(px, rnd, 256, 0, 256);
            DrawFrond(px, rnd, 0, 256, 256);
            DrawReeds(px, rnd, 256, 256, 128, 256);
            DrawDense(px, rnd, 384, 256, 128);
            DrawBark(px, rnd, 384, 384, 128);

            atlas = new Texture2D(Size, Size, TextureFormat.RGBA32, true, false)
            {
                name = "GolfSimZA_TreeAtlas", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4,
                hideFlags = HideFlags.DontSave
            };
            // Mip levels made here so the leaves keep their coverage far away (averaged alpha
            // would make alpha cut-out trees thin out and vanish in the distance).
            Color[] level = px;
            int size = Size;
            for (int mip = 0; mip < atlas.mipmapCount; mip++)
            {
                if (mip > 0)
                {
                    int half = Mathf.Max(1, size / 2);
                    var next = new Color[half * half];
                    for (int y = 0; y < half; y++)
                        for (int x = 0; x < half; x++)
                        {
                            Color a = level[(y * 2) * size + x * 2], b = level[(y * 2) * size + Mathf.Min(size - 1, x * 2 + 1)];
                            Color c = level[Mathf.Min(size - 1, y * 2 + 1) * size + x * 2], d = level[Mathf.Min(size - 1, y * 2 + 1) * size + Mathf.Min(size - 1, x * 2 + 1)];
                            float wsum = a.a + b.a + c.a + d.a;
                            Color rgb = wsum > 0.001f ? (a * a.a + b * b.a + c * c.a + d * d.a) / wsum : (a + b + c + d) * 0.25f;
                            rgb.a = (a.a + b.a + c.a + d.a) * 0.25f;
                            next[y * half + x] = rgb;
                        }
                    level = next;
                    size = half;
                }
                var boosted = new Color[level.Length];
                float boost = 1f + 0.32f * mip;
                for (int i = 0; i < level.Length; i++) { Color c = level[i]; c.a = Mathf.Min(1f, c.a * boost); boosted[i] = c; }
                atlas.SetPixels(boosted, mip);
            }
            atlas.Apply(false, true);
            return atlas;
        }

        private static void Put(Color[] px, int x, int y, Color c, float coverage)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size || coverage <= 0f) return;
            int i = y * Size + x;
            Color old = px[i];
            float a = Mathf.Clamp01(coverage);
            Color mixed = Color.Lerp(old.a > 0f ? old : c, c, a);
            mixed.a = Mathf.Max(old.a, a);
            px[i] = mixed;
        }

        /// <summary>A leaf: an ellipse with a darker middle vein, inside the box [bx, bx+bw).</summary>
        private static void Leaf(Color[] px, float cx, float cy, float length, float width, float angle, Color colour, int bx, int by, int bw, int bh)
        {
            float ca = Mathf.Cos(angle), sa = Mathf.Sin(angle);
            int r = Mathf.CeilToInt(length * 0.5f) + 1;
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                {
                    int px0 = Mathf.RoundToInt(cx) + x, py0 = Mathf.RoundToInt(cy) + y;
                    if (px0 < bx || py0 < by || px0 >= bx + bw || py0 >= by + bh) continue;
                    float dx = px0 - cx, dy = py0 - cy;
                    float u = (dx * ca + dy * sa) / (length * 0.5f);
                    float v = (-dx * sa + dy * ca) / (width * 0.5f);
                    float e = u * u + v * v;
                    if (e > 1.15f) continue;
                    float cover = Mathf.Clamp01((1.15f - e) * 4f);
                    float shade = 1f - 0.18f * Mathf.Exp(-v * v * 30f) * (1f - Mathf.Abs(u)) + 0.08f * u;
                    Color c = colour * shade;
                    c.a = 1f;
                    Put(px, px0, py0, c, cover);
                }
        }

        private static Color LeafTone(System.Random rnd, float lo, float hi)
        {
            float v = Mathf.Lerp(lo, hi, (float)rnd.NextDouble());
            float warm = (float)rnd.NextDouble() * 0.08f;
            return new Color(v * (0.92f + warm), v, v * (0.84f - warm), 1f);
        }

        private static void DrawLeafCluster(Color[] px, System.Random rnd, int bx, int by, int s)
        {
            float cx = bx + s * 0.5f, cy = by + s * 0.5f;
            // A few twigs under the leaves.
            for (int t = 0; t < 7; t++)
            {
                float a = (float)(rnd.NextDouble() * Math.PI * 2);
                for (float d = 0; d < s * 0.42f; d += 0.5f)
                    Put(px, Mathf.RoundToInt(cx + Mathf.Cos(a) * d), Mathf.RoundToInt(cy + Mathf.Sin(a) * d), new Color(0.32f, 0.27f, 0.20f, 1f), 0.9f);
            }
            for (int i = 0; i < 230; i++)
            {
                // Denser in the middle, ragged edge.
                float d = s * 0.46f * Mathf.Sqrt((float)rnd.NextDouble()) * (0.75f + 0.25f * (float)rnd.NextDouble());
                float a = (float)(rnd.NextDouble() * Math.PI * 2);
                float len = s * (0.07f + 0.04f * (float)rnd.NextDouble());
                Leaf(px, cx + Mathf.Cos(a) * d, cy + Mathf.Sin(a) * d, len, len * 0.48f, (float)(rnd.NextDouble() * Math.PI), LeafTone(rnd, 0.55f, 1f), bx, by, s, s);
            }
        }

        private static void DrawNeedles(Color[] px, System.Random rnd, int bx, int by, int s)
        {
            // A branch along the card (u = along the branch) with needle tufts down both sides.
            float y0 = by + s * 0.5f;
            for (int x = bx + 4; x < bx + s - 6; x++)
                for (int w = -2; w <= 2; w++) Put(px, x, Mathf.RoundToInt(y0 + w), new Color(0.34f, 0.26f, 0.18f, 1f), 1f);
            for (int i = 0; i < 520; i++)
            {
                float along = bx + 6 + (float)rnd.NextDouble() * (s - 14);
                float t = (along - bx) / s;
                float reach = s * (0.44f - 0.28f * t) * (0.6f + 0.4f * (float)rnd.NextDouble());
                float side = rnd.NextDouble() < 0.5 ? -1f : 1f;
                float angle = side * (0.55f + 0.5f * (float)rnd.NextDouble());
                Color c = LeafTone(rnd, 0.5f, 0.95f);
                for (float d = 0; d < reach; d += 0.6f)
                {
                    int x = Mathf.RoundToInt(along + Mathf.Cos(angle) * d * 0.55f);
                    int y = Mathf.RoundToInt(y0 + Mathf.Sin(angle) * d);
                    if (x < bx || x >= bx + s || y < by || y >= by + s) break;
                    Put(px, x, y, c, 1f);
                    if (rnd.NextDouble() < 0.4) Put(px, x + 1, y, c, 0.6f);
                }
            }
        }

        private static void DrawFrond(Color[] px, System.Random rnd, int bx, int by, int s)
        {
            float y0 = by + s * 0.5f;
            for (int x = bx + 2; x < bx + s - 2; x++)
                for (int w = -2; w <= 2; w++) Put(px, x, Mathf.RoundToInt(y0 + w), new Color(0.55f, 0.52f, 0.34f, 1f), 1f);
            for (float along = bx + 8; along < bx + s - 6; along += 3.2f)
            {
                float t = (along - bx) / s;
                float length = s * 0.47f * Mathf.Sin(Mathf.Lerp(0.15f, 1f, t) * Mathf.PI * 0.95f) * (0.85f + 0.15f * (float)rnd.NextDouble());
                for (int side = -1; side <= 1; side += 2)
                {
                    float angle = side * (1.05f + 0.15f * (float)rnd.NextDouble());
                    Color c = LeafTone(rnd, 0.6f, 1f);
                    for (float d = 0; d < length; d += 0.7f)
                    {
                        float w = 2.2f * (1f - d / length) + 0.6f;
                        int x = Mathf.RoundToInt(along + Mathf.Cos(angle) * d * 0.5f + d * 0.25f);
                        int y = Mathf.RoundToInt(y0 + Mathf.Sin(angle) * d);
                        for (int k = -1; k <= 1; k++)
                            if (x + k >= bx && x + k < bx + s && y >= by && y < by + s) Put(px, x + k, y, c * (k == 0 ? 1f : 0.85f), k == 0 ? 1f : Mathf.Clamp01(w - 1f));
                    }
                }
            }
        }

        private static void DrawReeds(Color[] px, System.Random rnd, int bx, int by, int w, int h)
        {
            for (int b = 0; b < 16; b++)
            {
                float x0 = bx + 8 + (float)rnd.NextDouble() * (w - 16);
                float top = by + h * (0.65f + 0.33f * (float)rnd.NextDouble());
                float lean = ((float)rnd.NextDouble() * 2f - 1f) * 18f;
                Color c = LeafTone(rnd, 0.6f, 1f);
                for (float y = by + 2; y < top; y += 0.5f)
                {
                    float t = (y - by) / (top - by);
                    float x = x0 + lean * t * t;
                    float half = 2.4f * (1f - t) + 0.5f;
                    for (int k = -3; k <= 3; k++)
                        if (Mathf.Abs(k) <= half) Put(px, Mathf.RoundToInt(x + k), Mathf.RoundToInt(y), c * (1f - 0.05f * Mathf.Abs(k)), 1f);
                }
                // Some carry a brown seed head.
                if (rnd.NextDouble() < 0.35)
                    for (float y = top - 26; y < top - 6; y += 0.5f)
                        for (int k = -3; k <= 3; k++) Put(px, Mathf.RoundToInt(x0 + lean * 0.8f + k), Mathf.RoundToInt(y), new Color(0.45f, 0.33f, 0.22f, 1f), 1f);
            }
        }

        private static void DrawDense(Color[] px, System.Random rnd, int bx, int by, int s)
        {
            for (int y = by; y < by + s; y++)
                for (int x = bx; x < bx + s; x++) px[y * Size + x] = new Color(0.42f, 0.44f, 0.36f, 1f);
            for (int i = 0; i < 420; i++)
            {
                float len = s * (0.09f + 0.05f * (float)rnd.NextDouble());
                Leaf(px, bx + (float)rnd.NextDouble() * s, by + (float)rnd.NextDouble() * s, len, len * 0.5f, (float)(rnd.NextDouble() * Math.PI), LeafTone(rnd, 0.45f, 0.85f), bx, by, s, s);
            }
        }

        private static void DrawBark(Color[] px, System.Random rnd, int bx, int by, int s)
        {
            var streak = new float[s];
            for (int x = 0; x < s; x++) streak[x] = (float)rnd.NextDouble();
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float v = 0.55f + 0.25f * Mathf.Lerp(streak[x], streak[(x + 1) % s], 0.5f) + 0.1f * Mathf.Sin(y * 0.35f + streak[x] * 6f);
                    if (rnd.NextDouble() < 0.02) v *= 0.7f;
                    px[(by + y) * Size + bx + x] = new Color(v * 1.0f, v * 0.88f, v * 0.74f, 1f);
                }
        }

        // ------------------------------------------------------------------ meshes

        public enum Model { Broadleaf, Spreading, Flowering, Pine, Palm, Bush, Reed }
        public const int ModelCount = 7;

        /// <summary>Near (index 0) and far (index 1) mesh of a model.</summary>
        public static Mesh[] Build(Model model)
        {
            switch (model)
            {
                case Model.Pine: return new[] { Pine(false), Pine(true) };
                case Model.Palm: return new[] { Palm(false), Palm(true) };
                case Model.Bush: return new[] { Crowned(Model.Bush, false), Crowned(Model.Bush, true) };
                case Model.Reed: return new[] { Reed(false), Reed(true) };
                default: return new[] { Crowned(model, false), Crowned(model, true) };
            }
        }

        private struct Lobe { public Vector3 C, R; }

        /// <summary>Broadleaf, spreading and flowering trees and bushes: trunk, branches, crown lobes of leaf cards.</summary>
        private static Mesh Crowned(Model model, bool far)
        {
            int seed = model == Model.Spreading ? 23 : model == Model.Flowering ? 31 : model == Model.Bush ? 41 : 11;
            var b = new Builder(new System.Random(seed));
            var lobes = new List<Lobe>();
            float trunkTop;
            if (model == Model.Spreading)
            {
                trunkTop = 0.5f;
                lobes.Add(new Lobe { C = new Vector3(0f, 0.74f, 0f), R = new Vector3(0.36f, 0.15f, 0.36f) });
                for (int i = 0; i < 5; i++)
                {
                    float a = i / 5f * Mathf.PI * 2f + 0.4f;
                    lobes.Add(new Lobe { C = new Vector3(Mathf.Cos(a) * 0.3f, 0.70f + 0.04f * (i % 2), Mathf.Sin(a) * 0.3f), R = new Vector3(0.22f, 0.13f, 0.22f) });
                }
            }
            else if (model == Model.Bush)
            {
                trunkTop = 0.15f;
                lobes.Add(new Lobe { C = new Vector3(0f, 0.48f, 0f), R = new Vector3(0.42f, 0.40f, 0.42f) });
                lobes.Add(new Lobe { C = new Vector3(0.18f, 0.40f, 0.12f), R = new Vector3(0.30f, 0.30f, 0.30f) });
                lobes.Add(new Lobe { C = new Vector3(-0.16f, 0.38f, -0.14f), R = new Vector3(0.30f, 0.28f, 0.30f) });
            }
            else
            {
                trunkTop = 0.45f;
                lobes.Add(new Lobe { C = new Vector3(0f, 0.68f, 0f), R = new Vector3(0.30f, 0.25f, 0.30f) });
                lobes.Add(new Lobe { C = new Vector3(0.18f, 0.58f, 0.06f), R = new Vector3(0.22f, 0.19f, 0.22f) });
                lobes.Add(new Lobe { C = new Vector3(-0.16f, 0.60f, -0.10f), R = new Vector3(0.22f, 0.20f, 0.22f) });
                lobes.Add(new Lobe { C = new Vector3(0.04f, 0.82f, 0.10f), R = new Vector3(0.20f, 0.17f, 0.20f) });
                lobes.Add(new Lobe { C = new Vector3(-0.06f, 0.55f, 0.20f), R = new Vector3(0.19f, 0.17f, 0.19f) });
                lobes.Add(new Lobe { C = new Vector3(0.10f, 0.52f, -0.20f), R = new Vector3(0.18f, 0.16f, 0.18f) });
            }

            // Trunk and the main branches into the crown.
            if (model != Model.Bush)
            {
                b.Tube(0, Vector3.zero, new Vector3(0.01f, trunkTop, 0f), 0.045f, 0.032f, far ? 5 : 8, new Color(0.9f, 0.9f, 0.9f, 0f));
                if (!far)
                    for (int i = 1; i < lobes.Count; i++)
                    {
                        Vector3 end = lobes[i].C * 0.85f + Vector3.up * lobes[i].C.y * 0.12f;
                        b.Tube(0, new Vector3(0.01f, trunkTop * 0.9f, 0f), end, 0.026f, 0.012f, 6, new Color(0.8f, 0.8f, 0.8f, 0.2f));
                    }
            }
            Vector3 crownCentre = Vector3.zero;
            foreach (Lobe l in lobes) crownCentre += l.C;
            crownCentre /= lobes.Count;
            float crownTop = 0f, crownBottom = 1f;
            foreach (Lobe l in lobes) { crownTop = Mathf.Max(crownTop, l.C.y + l.R.y); crownBottom = Mathf.Min(crownBottom, l.C.y - l.R.y); }

            // Inner crowns (dense leaves) so the crown never looks hollow.
            foreach (Lobe l in lobes)
                b.Ellipsoid(1, l.C, l.R * (far ? 0.72f : 0.6f), far ? 8 : 12, DenseLeaves, crownCentre, crownBottom, crownTop, 0.55f, model == Model.Flowering ? new Color(0.42f, 0.95f, 0.45f, 1f) : Color.white);

            // Leaf cards on the lobes.
            int perLobe = far ? 7 : (model == Model.Bush ? 32 : 30);
            float cardSize = far ? 0.32f : (model == Model.Bush ? 0.25f : 0.21f);
            foreach (Lobe l in lobes)
            {
                int n = Mathf.RoundToInt(perLobe * (l.R.x / 0.25f));
                for (int i = 0; i < n; i++)
                {
                    Vector3 dir = b.OnSphere();
                    if (dir.y < -0.5f) dir.y *= 0.5f; // fewer under the crown
                    Vector3 p = l.C + Vector3.Scale(dir, l.R) * (0.8f + 0.25f * b.Next());
                    Color tone = model == Model.Flowering && b.Next() < 0.6f ? new Color(0.42f, 0.95f, 0.45f, 1f) : Color.white;
                    b.LeafCard(1, p, crownCentre, cardSize * (0.85f + 0.3f * b.Next()), LeafCluster, crownBottom, crownTop, tone);
                }
            }
            return b.ToMesh("GolfSimZA_" + model + (far ? "_Far" : ""), 1.5f);
        }

        private static Mesh Pine(bool far)
        {
            var b = new Builder(new System.Random(7));
            b.Tube(0, Vector3.zero, new Vector3(0f, 0.95f, 0f), 0.035f, 0.01f, far ? 5 : 8, new Color(0.85f, 0.85f, 0.85f, 0f));
            // Dark inner cone.
            b.ConeShell(1, 0.16f, 1.0f, 0.2f, far ? 7 : 10, DenseLeaves);
            int whorls = far ? 4 : 9;
            for (int w = 0; w < whorls; w++)
            {
                float t = w / (float)(whorls - 1);
                float y = Mathf.Lerp(0.22f, 0.95f, t);
                float radius = 0.31f * Mathf.Pow(1f - t, 0.85f) + 0.05f;
                int branches = far ? 5 : 8;
                for (int i = 0; i < branches; i++)
                {
                    float a = (i + 0.5f * (w % 2)) / branches * Mathf.PI * 2f + b.Next() * 0.4f;
                    Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    b.Branch(1, new Vector3(0f, y, 0f), dir, radius * (0.85f + 0.3f * b.Next()), 0.32f + 0.12f * b.Next(), radius * (far ? 0.75f : 0.55f), Needles, t);
                }
            }
            return b.ToMesh(far ? "GolfSimZA_Pine_Far" : "GolfSimZA_Pine", 1.2f);
        }

        private static Mesh Palm(bool far)
        {
            var b = new Builder(new System.Random(17));
            int rings = far ? 5 : 12;
            for (int r = 0; r < rings; r++)
            {
                float t0 = r / (float)rings, t1 = (r + 1) / (float)rings;
                Vector3 c0 = new Vector3(0.06f * t0 * t0, t0 * 0.9f, 0f), c1 = new Vector3(0.06f * t1 * t1, t1 * 0.9f, 0f);
                float shade = r % 2 == 0 ? 1f : 0.85f;
                b.Tube(0, c0, c1, Mathf.Lerp(0.032f, 0.02f, t0) * (r % 2 == 0 ? 1.08f : 1f), Mathf.Lerp(0.032f, 0.02f, t1), far ? 5 : 8, new Color(shade * 1.25f, shade * 1.2f, shade * 1.1f, 0f));
            }
            Vector3 top = new Vector3(0.06f, 0.9f, 0f);
            int fronds = far ? 8 : 15;
            for (int f = 0; f < fronds; f++)
            {
                float yaw = f / (float)fronds * Mathf.PI * 2f + b.Next() * 0.3f;
                bool dead = !far && f % 5 == 4;
                float lift = dead ? -0.25f : (f % 3 == 0 ? 0.16f : 0.07f);
                b.FrondStrip(1, top, new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)), dead ? 0.3f : 0.42f, lift, 0.11f, far ? 3 : 8, Frond,
                    dead ? new Color(1.25f, 0.95f, 0.55f, 0.6f) : new Color(1f, 1f, 1f, 1f));
            }
            return b.ToMesh(far ? "GolfSimZA_Palm_Far" : "GolfSimZA_Palm", 1.4f);
        }

        private static Mesh Reed(bool far)
        {
            var b = new Builder(new System.Random(53));
            int blades = far ? 5 : 12;
            for (int i = 0; i < blades; i++)
            {
                float a = b.Next() * Mathf.PI * 2f;
                Vector3 at = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.12f * b.Next();
                float yaw = b.Next() * Mathf.PI;
                Vector3 right = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * 0.28f;
                Vector3 up = new Vector3(at.x * 0.6f, 1f, at.z * 0.6f) * (0.75f + 0.25f * b.Next());
                b.Quad(1, at - right * 0.5f, right, up, Reeds, Vector3.up, new Color(0.85f + 0.2f * b.Next(), 0.9f + 0.15f * b.Next(), 0.85f, 0.9f));
            }
            return b.ToMesh(far ? "GolfSimZA_Reed_Far" : "GolfSimZA_Reed", 1.1f);
        }

        // ------------------------------------------------------------------ builder

        private sealed class Builder
        {
            private readonly List<Vector3> v = new List<Vector3>();
            private readonly List<Vector3> n = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<Color> col = new List<Color>();
            private readonly List<int>[] tri = { new List<int>(), new List<int>() };
            private readonly System.Random rnd;

            public Builder(System.Random rnd) { this.rnd = rnd; }

            public float Next() => (float)rnd.NextDouble();

            public Vector3 OnSphere()
            {
                float z = Next() * 2f - 1f, a = Next() * Mathf.PI * 2f, r = Mathf.Sqrt(1f - z * z);
                return new Vector3(r * Mathf.Cos(a), z, r * Mathf.Sin(a));
            }

            private static Vector2 In(Rect r, float u, float w) => new Vector2(r.x + r.width * Mathf.Clamp01(u), r.y + r.height * Mathf.Clamp01(w));

            /// <summary>Light at a point of the crown: brighter on top and outside, darker low and inside.</summary>
            private static float CrownLight(Vector3 p, Vector3 centre, float bottom, float top, float outward)
            {
                float height = Mathf.InverseLerp(bottom, top, p.y);
                return Mathf.Lerp(0.55f, 1.05f, 0.55f * height + 0.45f * outward);
            }

            public void Tube(int sub, Vector3 a, Vector3 b, float ra, float rb, int segments, Color colour)
            {
                Vector3 axis = (b - a).normalized;
                Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                Vector3 side2 = Vector3.Cross(axis, side);
                int start = v.Count;
                float length = (b - a).magnitude;
                for (int i = 0; i <= segments; i++)
                {
                    float ang = i / (float)segments * Mathf.PI * 2f;
                    Vector3 dir = side * Mathf.Cos(ang) + side2 * Mathf.Sin(ang);
                    v.Add(a + dir * ra); n.Add(dir); uv.Add(In(Bark, i / (float)segments, 0f)); col.Add(colour * 0.8f);
                    v.Add(b + dir * rb); n.Add(dir); uv.Add(In(Bark, i / (float)segments, Mathf.Min(1f, length * 2f))); col.Add(colour);
                }
                for (int i = 0; i < segments; i++)
                {
                    int k = start + i * 2;
                    tri[sub].AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 });
                }
            }

            public void Quad(int sub, Vector3 corner, Vector3 right, Vector3 up, Rect region, Vector3 normal, Color colour)
            {
                int s = v.Count;
                v.Add(corner); v.Add(corner + right); v.Add(corner + right + up); v.Add(corner + up);
                for (int i = 0; i < 4; i++) { n.Add(normal); col.Add(colour); }
                uv.Add(In(region, 0, 0)); uv.Add(In(region, 1, 0)); uv.Add(In(region, 1, 1)); uv.Add(In(region, 0, 1));
                tri[sub].AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
            }

            /// <summary>A leaf-cluster card at p, shaded as part of a round crown (normal = outward).</summary>
            public void LeafCard(int sub, Vector3 p, Vector3 centre, float size, Rect region, float bottom, float top, Color tone)
            {
                Vector3 outward = (p - centre);
                outward.y *= 0.7f;
                outward = outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector3.up;
                Vector3 plane = (outward + OnSphere() * 0.9f).normalized;
                Vector3 right = Vector3.Cross(plane, OnSphere()).normalized * size;
                Vector3 up = Vector3.Cross(right.normalized, plane).normalized * size;
                Vector3 shading = (outward + Vector3.up * 0.25f).normalized;
                float light = CrownLight(p, centre, bottom, top, Mathf.Clamp01(Vector3.Dot((p - centre).normalized, outward))) * (0.92f + 0.16f * Next());
                // tone: white = the material colour; flowering trees pass a green tone for their
                // leaf cards (the material colour is the flower colour).
                Color colour = new Color(light * tone.r, light * tone.g, light * tone.b, 1f);
                int s = v.Count;
                Add(sub, p - right * 0.5f - up * 0.5f, shading, In(region, 0, 0), colour);
                Add(sub, p + right * 0.5f - up * 0.5f, shading, In(region, 1, 0), colour);
                Add(sub, p + right * 0.5f + up * 0.5f, shading, In(region, 1, 1), colour);
                Add(sub, p - right * 0.5f + up * 0.5f, shading, In(region, 0, 1), colour);
                tri[sub].AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
            }

            private void Add(int sub, Vector3 p, Vector3 normal, Vector2 t, Color c) { v.Add(p); n.Add(normal); uv.Add(t); col.Add(c); }

            /// <summary>Closed ellipsoid of dense leaves (the inside of a crown), darker than the cards.</summary>
            public void Ellipsoid(int sub, Vector3 c, Vector3 r, int segments, Rect region, Vector3 crownCentre, float bottom, float top, float darkness, Color tone)
            {
                int rings = Mathf.Max(4, segments / 2 + 1);
                int start = v.Count;
                for (int i = 0; i <= rings; i++)
                {
                    float lat = i / (float)rings * Mathf.PI;
                    for (int j = 0; j <= segments; j++)
                    {
                        float lon = j / (float)segments * Mathf.PI * 2f;
                        Vector3 dir = new Vector3(Mathf.Sin(lat) * Mathf.Cos(lon), Mathf.Cos(lat), Mathf.Sin(lat) * Mathf.Sin(lon));
                        float hash = Mathf.Abs(Mathf.Sin(i * 12.9898f + (j % segments) * 78.233f) * 43758.5453f) % 1f;
                        float bump = (i == 0 || i == rings) ? 1f : 0.95f + 0.1f * hash; // same at the seam, no crack
                        Vector3 p = c + Vector3.Scale(dir, r) * bump;
                        float light = CrownLight(p, crownCentre, bottom, top, 0.3f) * darkness;
                        v.Add(p); n.Add((dir + Vector3.up * 0.2f).normalized);
                        uv.Add(In(region, j / (float)segments, i / (float)rings));
                        col.Add(new Color(light * tone.r, light * tone.g, light * tone.b, 0.4f));
                    }
                }
                for (int i = 0; i < rings; i++)
                    for (int j = 0; j < segments; j++)
                    {
                        int a = start + i * (segments + 1) + j, b = a + segments + 1;
                        tri[sub].AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                    }
            }

            public void ConeShell(int sub, float y0, float y1, float radius, int segments, Rect region)
            {
                int start = v.Count;
                for (int j = 0; j <= segments; j++)
                {
                    float a = j / (float)segments * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    v.Add(dir * radius + Vector3.up * y0); n.Add((dir + Vector3.up * 0.3f).normalized); uv.Add(In(region, j / (float)segments, 0)); col.Add(new Color(0.35f, 0.35f, 0.35f, 0.3f));
                    v.Add(Vector3.up * y1); n.Add(Vector3.up); uv.Add(In(region, j / (float)segments, 1)); col.Add(new Color(0.6f, 0.6f, 0.6f, 0.3f));
                }
                for (int j = 0; j < segments; j++)
                {
                    int k = start + j * 2;
                    tri[sub].AddRange(new[] { k, k + 1, k + 2, k + 2, k + 1, k + 3 });
                }
            }

            /// <summary>A pine branch: a drooping card of needles from the trunk outwards.</summary>
            public void Branch(int sub, Vector3 root, Vector3 dir, float length, float droop, float width, Rect region, float heightT)
            {
                Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
                Vector3 tip = root + dir * length - Vector3.up * length * droop;
                Vector3 mid = root + dir * length * 0.5f - Vector3.up * length * droop * 0.25f;
                float light = Mathf.Lerp(0.6f, 1.05f, heightT) * (0.9f + 0.2f * Next());
                var c = new Color(light, light, light, 1f);
                Vector3 normal = (dir + Vector3.up * 0.6f).normalized;
                int s = v.Count;
                Add(sub, root - side * width * 0.3f, normal, In(region, 0, 0.2f), c * 0.7f);
                Add(sub, root + side * width * 0.3f, normal, In(region, 0, 0.8f), c * 0.7f);
                Add(sub, mid - side * width * 0.55f, normal, In(region, 0.5f, 0), c);
                Add(sub, mid + side * width * 0.55f, normal, In(region, 0.5f, 1), c);
                Add(sub, tip - side * width * 0.3f, normal, In(region, 1, 0.1f), c);
                Add(sub, tip + side * width * 0.3f, normal, In(region, 1, 0.9f), c);
                tri[sub].AddRange(new[] { s, s + 2, s + 1, s + 1, s + 2, s + 3, s + 2, s + 4, s + 3, s + 3, s + 4, s + 5 });
            }

            /// <summary>A palm frond: an arching strip with the leaflet texture.</summary>
            public void FrondStrip(int sub, Vector3 root, Vector3 dir, float length, float lift, float width, int steps, Rect region, Color colour)
            {
                Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
                int start = v.Count;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float y = lift * Mathf.Sin(t * Mathf.PI * 0.6f) - 0.30f * length * t * t;
                    Vector3 c = root + dir * (length * t) + Vector3.up * y;
                    // Leaflets droop either side of the midrib.
                    Vector3 droop = Vector3.down * width * 0.35f;
                    Vector3 normal = (Vector3.up + dir * 0.2f).normalized;
                    float light = Mathf.Lerp(0.75f, 1.05f, t);
                    Color cc = new Color(colour.r * light, colour.g * light, colour.b * light, colour.a);
                    Add(sub, c - side * width + droop, normal, In(region, t, 0), cc);
                    Add(sub, c, normal, In(region, t, 0.5f), cc);
                    Add(sub, c + side * width + droop, normal, In(region, t, 1), cc);
                }
                for (int i = 0; i < steps; i++)
                {
                    int k = start + i * 3;
                    tri[sub].AddRange(new[] { k, k + 1, k + 3, k + 1, k + 4, k + 3, k + 1, k + 2, k + 4, k + 2, k + 5, k + 4 });
                }
            }

            public Mesh ToMesh(string name, float boundsSize)
            {
                var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
                if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(v);
                mesh.SetNormals(n);
                mesh.SetUVs(0, uv);
                mesh.SetColors(col);
                mesh.subMeshCount = 2;
                mesh.SetTriangles(tri[0], 0);
                mesh.SetTriangles(tri[1], 1);
                // Instances are scaled up a lot; keep culling generous.
                mesh.bounds = new Bounds(new Vector3(0f, 0.5f, 0f), Vector3.one * boundsSize);
                return mesh;
            }
        }
    }
}
