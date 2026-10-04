using System;
using System.Collections.Generic;
using GolfSimZA.Physics;
using UnityEngine;

namespace GolfSimZA.MiniGames
{
    /// <summary>
    /// Building blocks for the mini games' own maps: real ground (height-field terrain with
    /// vertex colours and mesh colliders), water you can splash into, nets, signs with numbers,
    /// rocks, arches and stands. The tee is always at (0, 0, 0) on flat ground.
    /// </summary>
    public static class MapKit
    {
        private static Material vertexLit, water, netMat;
        private static Font font;

        public static Color Lin(Color c) => QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;

        /// <summary>Lit, vertex-coloured material with a fine grey detail texture (terrain, rock).</summary>
        public static Material VertexLit
        {
            get
            {
                if (vertexLit != null) return vertexLit;
                Shader vc = Resources.Load<Shader>("GolfSimZA_VertexColorLit");
                if (vc != null && vc.isSupported)
                {
                    vertexLit = new Material(vc) { mainTexture = Noise(Color.white, new Color(0.66f, 0.66f, 0.66f), 17, 128) };
                    vertexLit.SetFloat("_DetailScale", 1f);
                }
                else vertexLit = MiniGameWorld.Lit(new Color(0.4f, 0.55f, 0.3f), 0.05f);
                return vertexLit;
            }
        }

        public static Material Water
        {
            get
            {
                if (water != null) return water;
                water = new Material(Shader.Find("Standard")) { color = new Color(0.12f, 0.36f, 0.48f) };
                water.SetFloat("_Glossiness", 0.93f);
                water.SetFloat("_Metallic", 0.1f);
                return water;
            }
        }

        // ------------------------------------------------------------ Terrain

        /// <summary>
        /// Height-field ground from x0..x1, z0..z1 (cell size <paramref name="step"/>), cut into tiles so
        /// no mesh is too big. Every tile has a mesh collider: the ball lands, bounces and rolls on it.
        /// </summary>
        public static void Terrain(Transform root, string name, float x0, float x1, float z0, float z1, float step,
            Func<float, float, float> height, Func<float, float, float, Color> colour)
        {
            const int Tile = 100; // cells per tile side
            int nx = Mathf.CeilToInt((x1 - x0) / step), nz = Mathf.CeilToInt((z1 - z0) / step);
            for (int tz = 0; tz < nz; tz += Tile)
                for (int tx = 0; tx < nx; tx += Tile)
                {
                    int cx = Mathf.Min(Tile, nx - tx), cz = Mathf.Min(Tile, nz - tz);
                    var v = new List<Vector3>((cx + 1) * (cz + 1));
                    var c = new List<Color>(v.Capacity);
                    var uv = new List<Vector2>(v.Capacity);
                    for (int j = 0; j <= cz; j++)
                        for (int i = 0; i <= cx; i++)
                        {
                            float x = Mathf.Min(x1, x0 + (tx + i) * step), z = Mathf.Min(z1, z0 + (tz + j) * step);
                            float y = height(x, z);
                            v.Add(new Vector3(x, y, z));
                            c.Add(Lin(colour(x, z, y)));
                            uv.Add(new Vector2(x * 0.25f, z * 0.25f));
                        }
                    var t = new List<int>(cx * cz * 6);
                    int w = cx + 1;
                    for (int j = 0; j < cz; j++)
                        for (int i = 0; i < cx; i++)
                        {
                            int a = j * w + i, b = a + w;
                            t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                        }
                    Mesh m = Mesh(name, v, uv, c, t);
                    Surface(root, name, m, VertexLit, true);
                }
        }

        /// <summary>A flat water surface over the given rectangle (collider named Map_Water: a ball that reaches it splashes and stops).</summary>
        public static GameObject WaterSurface(Transform root, float level, float x0, float x1, float z0, float z1, Func<float, float, bool> inside = null, float step = 4f)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            int nx = Mathf.CeilToInt((x1 - x0) / step), nz = Mathf.CeilToInt((z1 - z0) / step);
            for (int j = 0; j <= nz; j++)
                for (int i = 0; i <= nx; i++)
                    v.Add(new Vector3(x0 + i * step, level, z0 + j * step));
            int w = nx + 1;
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    if (inside != null)
                    {
                        float cx = x0 + (i + 0.5f) * step, cz = z0 + (j + 0.5f) * step;
                        if (!inside(cx, cz)) continue;
                    }
                    int a = j * w + i, b = a + w;
                    t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            return Surface(root, "Map_Water", Mesh("Map_Water", v, null, null, t), Water, true);
        }

        /// <summary>Smooth 0..1 bump: 1 inside radius r, falling to 0 at r + fall.</summary>
        public static float Blend(float d, float r, float fall) => 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - r) / Mathf.Max(0.01f, fall)));

        public static float Dist(float x, float z, Vector3 c) => Mathf.Sqrt((x - c.x) * (x - c.x) + (z - c.z) * (z - c.z));

        /// <summary>Layered noise 0..1.</summary>
        public static float Noise2(float x, float z, float scale, float seed) =>
            Mathf.PerlinNoise(x * scale + seed, z * scale - seed) * 0.65f + Mathf.PerlinNoise(x * scale * 2.7f - seed, z * scale * 2.7f + seed) * 0.35f;

        // ------------------------------------------------------------ Things

        /// <summary>Rock: a lumpy squashed sphere (no collider).</summary>
        public static GameObject Rock(Transform root, Vector3 at, float size, Color colour, System.Random r)
        {
            GameObject g = MiniGameWorld.Shape(PrimitiveType.Sphere, root, new Vector3(at.x, MiniGameWorld.Ground(at) + size * 0.2f, at.z),
                new Vector3(size * (1f + (float)r.NextDouble() * 0.6f), size * (0.5f + (float)r.NextDouble() * 0.4f), size * (1f + (float)r.NextDouble() * 0.5f)), colour, "Rock", 0.1f);
            g.transform.rotation = Quaternion.Euler((float)r.NextDouble() * 20f, (float)r.NextDouble() * 360f, (float)r.NextDouble() * 20f);
            return g;
        }

        /// <summary>Tall pole with a see-through net panel to the next pole (range nets).</summary>
        public static void NetLine(Transform root, Vector3 from, Vector3 to, float height, float spacing)
        {
            if (netMat == null)
            {
                netMat = new Material(MiniGameWorld.VertexUnlit) { mainTexture = NetTexture(), color = new Color(1f, 1f, 1f, 0.55f) };
                netMat.mainTextureScale = Vector2.one;
            }
            Vector3 d = to - from;
            d.y = 0f;
            int n = Mathf.Max(1, Mathf.RoundToInt(d.magnitude / spacing));
            for (int i = 0; i <= n; i++)
            {
                Vector3 p = Vector3.Lerp(from, to, i / (float)n);
                p.y = MiniGameWorld.Ground(p);
                MiniGameWorld.Shape(PrimitiveType.Cylinder, root, p + Vector3.up * height * 0.5f, new Vector3(0.35f, height * 0.5f, 0.35f), new Color(0.72f, 0.74f, 0.76f), "NetPole", 0.6f);
                if (i == n) break;
                Vector3 q = Vector3.Lerp(from, to, (i + 1f) / n);
                q.y = MiniGameWorld.Ground(q);
                var v = new List<Vector3> { p + Vector3.up * 1f, q + Vector3.up * 1f, p + Vector3.up * height, q + Vector3.up * height };
                var uv = new List<Vector2> { new Vector2(0, 0), new Vector2(spacing / 2f, 0), new Vector2(0, height / 2f), new Vector2(spacing / 2f, height / 2f) };
                var col = new List<Color> { Color.white, Color.white, Color.white, Color.white };
                Mesh m = Mesh("Net", v, uv, col, new List<int> { 0, 2, 1, 1, 2, 3 });
                GameObject net = Surface(root, "Net", m, netMat, false);
                net.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>A board with big text on it (distance boards, banners). Faces the tee.</summary>
        public static GameObject Sign(Transform root, Vector3 at, string text, Color board, Color textColour, float height, float width, float lift = 0f, float lean = 12f)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Sign " + text);
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(at.x, MiniGameWorld.Ground(at) + lift, at.z);
            Vector3 look = -new Vector3(at.x, 0f, at.z);
            // Local +z points away from the tee, so the text (on the tee side) reads from the mat; the top leans back.
            go.transform.rotation = Quaternion.LookRotation(look.sqrMagnitude > 1f ? -look.normalized : Vector3.forward) * Quaternion.Euler(lean, 0f, 0f);
            GameObject b = MiniGameWorld.Shape(PrimitiveType.Cube, go.transform, new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, 0.3f), board, "Board", 0.3f);
            b.transform.localRotation = Quaternion.identity;
            if (font != null)
            {
                var tgo = new GameObject("Text");
                tgo.transform.SetParent(go.transform, false);
                tgo.transform.localPosition = new Vector3(0f, height * 0.5f, -0.17f);
                TextMesh tm = tgo.AddComponent<TextMesh>();
                tm.font = font;
                tm.text = text;
                tm.fontSize = 96;
                tm.characterSize = height * 0.075f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.fontStyle = FontStyle.Bold;
                tm.color = textColour;
                tgo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            return go;
        }

        /// <summary>Half-ring arch of tubes standing on the ground (decoration).</summary>
        public static void Arch(Transform root, Vector3 centre, float radius, float thick, Color colour, float yaw)
        {
            var go = new GameObject("Arch");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(centre.x, MiniGameWorld.Ground(centre), centre.z);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            const int seg = 18;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI, a1 = (i + 1) / (float)seg * Mathf.PI;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * radius, Mathf.Sin(a0) * radius, 0f);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius, 0f);
                Vector3 mid = (p0 + p1) * 0.5f;
                GameObject s = MiniGameWorld.Shape(PrimitiveType.Cylinder, go.transform, mid, new Vector3(thick, Vector3.Distance(p0, p1) * 0.55f, thick), colour, "ArchPart", 0.5f);
                s.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (p1 - p0).normalized);
            }
        }

        /// <summary>Stepped grandstand with coloured seat rows (decoration, faces the playing area).</summary>
        public static void Grandstand(Transform root, Vector3 at, float length, int rows, float yaw, System.Random r)
        {
            var go = new GameObject("Grandstand");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(at.x, MiniGameWorld.Ground(at), at.z);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Color concrete = new Color(0.62f, 0.62f, 0.64f);
            Color[] seats = { new Color(0.85f, 0.2f, 0.18f), new Color(0.15f, 0.4f, 0.85f), new Color(0.95f, 0.75f, 0.15f), new Color(0.2f, 0.7f, 0.35f) };
            for (int i = 0; i < rows; i++)
            {
                MiniGameWorld.Shape(PrimitiveType.Cube, go.transform, new Vector3(0f, 0.5f + i * 0.9f, i * 1.1f), new Vector3(length, 1f + i * 1.8f, 1.1f), concrete, "Step");
                MiniGameWorld.Shape(PrimitiveType.Cube, go.transform, new Vector3(0f, 1.15f + i * 1.8f, i * 1.1f - 0.2f), new Vector3(length - 0.4f, 0.3f, 0.5f), seats[i % seats.Length], "Seats", 0.4f);
            }
            MiniGameWorld.Shape(PrimitiveType.Cube, go.transform, new Vector3(0f, rows * 1.8f + 2.6f, rows * 1.1f * 0.5f), new Vector3(length + 1f, 0.25f, rows * 1.1f + 1.5f), new Color(0.95f, 0.95f, 0.95f), "Roof", 0.4f);
            for (int s = -1; s <= 1; s += 2)
                MiniGameWorld.Shape(PrimitiveType.Cylinder, go.transform, new Vector3(s * length * 0.5f, (rows * 1.8f + 2.6f) * 0.5f, rows * 1.1f), new Vector3(0.3f, (rows * 1.8f + 2.6f) * 0.5f, 0.3f), concrete, "Post");
        }

        /// <summary>Trees on the map wherever <paramref name="where"/> allows (they knock balls down like on a course).</summary>
        public static void Plant(TreeField field, int count, System.Random r, float x0, float x1, float z0, float z1, Func<Vector3, bool> where, TreeField.Kind kind, float h0, float h1, float width = 0.7f)
        {
            if (field == null) return;
            for (int i = 0; i < count; i++)
            {
                float x = Mathf.Lerp(x0, x1, (float)r.NextDouble()), z = Mathf.Lerp(z0, z1, (float)r.NextDouble());
                var p = new Vector3(x, 0f, z);
                if (!where(p) || field.IsNear(p, 4f)) continue;
                p.y = GroundProbe.HeightAt(new Vector3(x, 500f, z));
                field.Add(kind, p, Mathf.Lerp(h0, h1, (float)r.NextDouble()), (float)r.NextDouble() * 360f, width * (0.85f + (float)r.NextDouble() * 0.3f), r.Next(3));
            }
        }

        // ------------------------------------------------------------ Mesh helpers

        public static Mesh Mesh(string name, List<Vector3> v, List<Vector2> uv, List<Color> c, List<int> t)
        {
            var mesh = new Mesh { name = name, indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(v);
            if (uv != null) mesh.SetUVs(0, uv);
            if (c != null) mesh.SetColors(c);
            mesh.SetTriangles(t, 0);
            // Every flat piece faces up.
            int[] tri = mesh.triangles;
            for (int i = 0; i < tri.Length; i += 3)
            {
                Vector3 n = Vector3.Cross(v[tri[i + 1]] - v[tri[i]], v[tri[i + 2]] - v[tri[i]]);
                if (n.y < 0f && Mathf.Abs(n.y) > Mathf.Abs(n.x) * 0.2f && Mathf.Abs(n.y) > Mathf.Abs(n.z) * 0.2f) { int s = tri[i + 1]; tri[i + 1] = tri[i + 2]; tri[i + 2] = s; }
            }
            mesh.triangles = tri;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static GameObject Surface(Transform root, string name, Mesh mesh, Material material, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = true;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        /// <summary>A flat disc with a collider at the given height (rafts, platforms).</summary>
        public static GameObject Platform(Transform root, string name, Vector3 centre, float radius, float top, Color colour)
        {
            var v = new List<Vector3> { new Vector3(centre.x, top, centre.z) };
            var c = new List<Color> { Lin(colour) };
            var t = new List<int>();
            const int seg = 48;
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                v.Add(new Vector3(centre.x + Mathf.Cos(a) * radius, top, centre.z + Mathf.Sin(a) * radius));
                c.Add(Lin(colour));
                if (i > 0) t.AddRange(new[] { 0, i + 1, i });
            }
            GameObject g = Surface(root, name, Mesh(name, v, null, c, t), VertexLit, true);
            // Side skirt down into the water.
            GameObject side = MiniGameWorld.Shape(PrimitiveType.Cylinder, root, new Vector3(centre.x, top - 0.6f, centre.z), new Vector3(radius * 2f, 0.6f, radius * 2f), colour * 0.7f, name + "Side", 0.2f);
            side.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        public static Texture2D Noise(Color a, Color b, int seed, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4, name = "GolfSimZA_MapNoise" };
            var px = new Color[size * size];
            var rnd = new System.Random(seed);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = x / (float)size * Mathf.PI * 2f, fy = y / (float)size * Mathf.PI * 2f;
                    float n = 0.5f + 0.18f * Mathf.Sin(fx * 2f + seed) * Mathf.Cos(fy * 3f) + 0.14f * Mathf.Sin(fx * 5f + fy * 4f + seed)
                              + 0.1f * Mathf.Sin(fx * 11f - fy * 9f) + ((float)rnd.NextDouble() - 0.5f) * 0.22f;
                    px[y * size + x] = Color.Lerp(a, b, Mathf.Clamp01(n));
                }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        private static Texture2D NetTexture()
        {
            const int S = 32;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, name = "GolfSimZA_Net" };
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    bool line = x % 16 < 1 || y % 16 < 1;
                    px[y * S + x] = line ? new Color(0.15f, 0.15f, 0.15f, 0.8f) : new Color(1f, 1f, 1f, 0.06f);
                }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }
    }
}
