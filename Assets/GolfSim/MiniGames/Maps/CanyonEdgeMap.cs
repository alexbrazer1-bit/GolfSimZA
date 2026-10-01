using System.Collections.Generic;
using GolfSimZA.Physics;
using UnityEngine;

namespace GolfSimZA.MiniGames
{
    /// <summary>
    /// EDGE KNOCKOUT's own map: a cliff-top in a red-rock canyon. The tee stands on the mainland
    /// rim; a grass tongue runs out from it and ends in a sheer drop (the edge). Far below is the
    /// canyon floor with a river, rock buttes stand in the canyon and a mesa wall closes it off on
    /// the far side. Everything you can land on is real ground (mesh colliders): a ball that runs
    /// over the edge or off the side of the tongue falls into the canyon.
    /// The plateau top is at y = 0 (the tee height), the canyon floor about 95 m below.
    /// </summary>
    public static class CanyonEdgeMap
    {
        public const float FloorY = -95f;
        /// <summary>Half width of the mowed playing strip (the knockout zone is ±30 m).</summary>
        public const float StripHalf = 30f;
        private const float MainlandFront = 10f;
        private const float Far = 1600f;
        /// <summary>Top of the far mesa (a little above the tee): its red cliff face fills the view beyond the edge.</summary>
        private const float FarTop = 8f;

        private static Material vertexLit, stripeMat, roughMat, waterMat, boulderMat;

        /// <summary>Half width of the grass tongue at z (wavy natural edges, always wider than the strip).</summary>
        public static float HalfWidth(float z, float seed) =>
            45f + 3f * Mathf.Sin(z * 0.045f + seed) + 1.5f * Mathf.Sin(z * 0.13f + seed * 2.3f);

        /// <summary>
        /// Height of the cliff-top at z: flat round the tee, then the tongue runs gently downhill
        /// (about 9 % of the edge distance in all) and levels out again at the lip. From the mat you
        /// can then see across the gap to the canyon walls and buttes beyond the edge.
        /// </summary>
        public static float TopY(float z, float edge)
        {
            if (z <= MainlandFront) return 0f;
            float t = Mathf.Clamp01((z - MainlandFront) / Mathf.Max(1f, edge - MainlandFront));
            return -edge * 0.09f * Mathf.SmoothStep(0f, 1f, t);
        }

        /// <summary>Builds the whole map under <paramref name="parent"/>; the cliff lip is at z = <paramref name="edge"/>.</summary>
        public static void Build(Transform parent, float edge, int seed)
        {
            EnsureMaterials();
            var random = new System.Random(seed);
            float s = seed * 0.37f;
            var root = new GameObject("CanyonEdgeMap").transform;
            root.SetParent(parent, false);

            // ---- Plateau top (y = 0): mowed strip, rough on the sides, mainland behind the tee.
            // The strip runs from behind the tee to the lip; the rough fills the rest of the tongue and the mainland.
            AddSurface(root, "Edge_Fairway", Grid(-StripHalf, StripHalf, 12, -60f, edge, Mathf.CeilToInt((edge + 60f) / 2f), (x, z) => TopY(z, edge), null), stripeMat, true);
            for (int side = -1; side <= 1; side += 2)
            {
                int sd = side;
                // Tongue rough: from the strip to the wavy outer edge.
                Mesh tongue = Strip(MainlandFront, edge, Mathf.CeilToInt((edge - MainlandFront) / 2f), z => sd < 0 ? -HalfWidth(z, s) : StripHalf, z => sd < 0 ? -StripHalf : HalfWidth(z, s), z => TopY(z, edge));
                AddSurface(root, side < 0 ? "Edge_RoughLeft" : "Edge_RoughRight", tongue, roughMat, true);
                // Mainland rough beside the tee strip.
                Mesh land = Grid(sd < 0 ? -Far : StripHalf, sd < 0 ? -StripHalf : Far, 40, -Far * 0.5f, MainlandFront, 40, (x, z) => 0f, null);
                AddSurface(root, side < 0 ? "Edge_GroundLeft" : "Edge_GroundRight", land, roughMat, true);
            }
            // Mainland behind the strip (the strip itself starts 60 m behind the tee).
            AddSurface(root, "Edge_GroundBack", Grid(-StripHalf, StripHalf, 6, -Far * 0.5f, -60f, 30, (x, z) => 0f, null), roughMat, true);

            // ---- Cliff faces round the rim (mainland front, tongue sides and the lip).
            var rim = new List<Vector3>();
            for (float x = -Far; x < -HalfWidth(MainlandFront, s); x += 20f) rim.Add(new Vector3(x, 0f, MainlandFront));
            for (float z = MainlandFront; z < edge; z += 2f) rim.Add(new Vector3(-HalfWidth(z, s), TopY(z, edge), z));
            for (float x = -HalfWidth(edge, s); x < HalfWidth(edge, s); x += 2f) rim.Add(new Vector3(x, TopY(edge, edge), edge));
            for (float z = edge; z > MainlandFront; z -= 2f) rim.Add(new Vector3(HalfWidth(z, s), TopY(z, edge), z));
            for (float x = HalfWidth(MainlandFront, s); x <= Far; x += 20f) rim.Add(new Vector3(x, 0f, MainlandFront));
            AddSurface(root, "Edge_CliffFace", CliffWall(rim, 0f, FloorY - 4f, false, seed, Vector3.zero), vertexLit, true);

            // ---- Canyon floor with a river across it.
            float riverZ(float x) => edge + 120f + 30f * Mathf.Sin(x * 0.011f + s) + 10f * Mathf.Sin(x * 0.031f);
            Mesh floor = Grid(-Far, Far, 80, MainlandFront - 40f, edge + 900f, 60, (x, z) =>
            {
                float h = FloorY + 5f * (Mathf.PerlinNoise(x * 0.01f + 3f, z * 0.01f + s) - 0.5f) + 2f * (Mathf.PerlinNoise(x * 0.05f, z * 0.05f) - 0.5f);
                float d = Mathf.Abs(z - riverZ(x));
                return h - 3.5f * Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((d - 6f) / 14f));
            }, (x, z, y) =>
            {
                float d = Mathf.Abs(z - riverZ(x));
                float n = Mathf.PerlinNoise(x * 0.03f + 7f, z * 0.03f);
                Color sand = Color.Lerp(new Color(0.72f, 0.46f, 0.30f), new Color(0.80f, 0.60f, 0.42f), n);
                Color bank = Color.Lerp(new Color(0.36f, 0.45f, 0.22f), sand, Mathf.Clamp01((d - 10f) / 25f));
                return d < 35f ? bank : sand;
            });
            AddSurface(root, "Edge_CanyonFloor", floor, vertexLit, true);

            // River water (no collider: a ball in the river sinks to the river bed).
            var wv = new List<Vector3>();
            var wt = new List<int>();
            for (float x = -Far; x <= Far; x += 10f)
            {
                float z = riverZ(x);
                wv.Add(new Vector3(x, FloorY - 1.2f, z - 13f));
                wv.Add(new Vector3(x, FloorY - 1.2f, z + 13f));
            }
            for (int i = 0; i + 3 < wv.Count; i += 2) wt.AddRange(new[] { i, i + 1, i + 2, i + 2, i + 1, i + 3 });
            AddSurface(root, "Edge_River", MakeMesh("Edge_River", wv, null, null, wt), waterMat, false);

            // ---- Mesa wall closing the canyon on the far side (below the tee, so from the mat you look across the gap at its red rock face).
            var far = new List<Vector3>();
            float farZ(float x) => edge + 250f + 35f * Mathf.Sin(x * 0.004f + s) + 18f * Mathf.Sin(x * 0.017f);
            for (float x = Far; x >= -Far; x -= 12f) far.Add(new Vector3(x, FarTop, farZ(x)));
            AddSurface(root, "Edge_FarWall", CliffWall(far, FarTop, FloorY - 6f, false, seed + 11, Vector3.zero, 0.62f), vertexLit, true);
            AddSurface(root, "Edge_FarMesaTop", Grid(-Far, Far, 140, 0f, 1f, 14, (x, z) => FarTop, (x, z, y) => new Color(0.62f, 0.50f, 0.34f), (x, t) => new Vector3(x, FarTop, Mathf.Lerp(farZ(x) - 0.5f, edge + 1400f, t))), vertexLit, true);

            // ---- Buttes standing in the canyon (you can land on top of them).
            for (int i = 0; i < 6; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var centre = new Vector3(side * (60f + (float)random.NextDouble() * 220f), 0f, edge + 60f + (float)random.NextDouble() * 130f);
                float radius = 14f + (float)random.NextDouble() * 22f;
                float top = -50f + (float)random.NextDouble() * 45f;
                Butte(root, centre, radius, top, seed + 20 + i);
            }

            // ---- Boulders on the rough and the mainland (no colliders - they are only scenery).
            for (int i = 0; i < 40; i++)
            {
                float z = Mathf.Lerp(-50f, edge - 8f, (float)random.NextDouble());
                float sideSign = random.NextDouble() < 0.5 ? -1f : 1f;
                float inner = StripHalf + 4f, outer = z < MainlandFront ? 140f : HalfWidth(z, s) - 3f;
                if (outer <= inner) continue;
                float x = sideSign * Mathf.Lerp(inner, outer, (float)random.NextDouble());
                float size = 0.6f + (float)random.NextDouble() * 1.8f;
                GameObject b = MiniGameWorld.Shape(PrimitiveType.Sphere, root, new Vector3(x, TopY(z, edge) + size * 0.25f, z), new Vector3(size * 1.4f, size * 0.8f, size * 1.2f), Color.white, "Boulder");
                b.GetComponent<Renderer>().sharedMaterial = boulderMat;
                b.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            }
        }

        /// <summary>Pines on the mainland either side of the tee (they knock balls down like on a course).</summary>
        public static void PlantTrees(TreeField field, float edge, int seed)
        {
            if (field == null) return;
            field.Clear();
            var random = new System.Random(seed);
            for (int i = 0; i < 160; i++)
            {
                float side = random.NextDouble() < 0.5 ? -1f : 1f;
                float x = side * (StripHalf + 22f + (float)random.NextDouble() * 280f);
                float z = -10f - (float)random.NextDouble() * 260f;
                if (Mathf.Abs(x) < 70f && z > -40f) continue; // keep the tee area open
                Vector3 p = new Vector3(x, 0f, z);
                if (field.IsNear(p, 5f)) continue;
                float h = 10f + (float)random.NextDouble() * 12f;
                field.Add(TreeField.Kind.Pine, p, h, (float)random.NextDouble() * 360f, 0.55f + (float)random.NextDouble() * 0.25f, random.Next(3));
            }
            field.Commit();
        }

        // ------------------------------------------------------------ Pieces

        private static void Butte(Transform root, Vector3 centre, float radius, float top, int seed)
        {
            var ring = new List<Vector3>();
            for (int i = 0; i < 40; i++)
            {
                float a = -i / 40f * Mathf.PI * 2f; // clockwise from above: faces outward
                float r = radius * (0.85f + 0.3f * Mathf.PerlinNoise(i * 0.35f + seed, seed * 0.7f));
                ring.Add(new Vector3(centre.x + Mathf.Cos(a) * r, top, centre.z + Mathf.Sin(a) * r));
            }
            ring.Add(ring[0]);
            AddSurface(root, "Edge_Butte", CliffWall(ring, top, FloorY - 6f, true, seed, centre, 0.8f), vertexLit, true);
            // Flat cap (landable).
            var v = new List<Vector3> { new Vector3(centre.x, top, centre.z) };
            var c = new List<Color> { Lin(new Color(0.60f, 0.48f, 0.32f)) };
            var t = new List<int>();
            for (int i = 0; i < ring.Count; i++)
            {
                v.Add(new Vector3(ring[i].x, top, ring[i].z));
                c.Add(Lin(new Color(0.58f, 0.46f, 0.30f)));
                if (i > 0) t.AddRange(new[] { 0, i, i + 1 });
            }
            Mesh cap = MakeMesh("Edge_ButteTop", v, null, c, t);
            FixWinding(cap, Vector3.up);
            AddSurface(root, "Edge_ButteTop", cap, vertexLit, true);
        }

        /// <summary>
        /// A rock wall hanging from a rim line down to <paramref name="bottom"/>: rows every 3 m,
        /// jagged ledges, coloured in canyon strata. Faces away from the land (or away from
        /// <paramref name="centre"/> for a closed butte ring).
        /// </summary>
        private static Mesh CliffWall(List<Vector3> rim, float top, float bottom, bool closedAroundCentre, int seed, Vector3 centre, float shade = 1f)
        {
            int rows = Mathf.Max(2, Mathf.CeilToInt((top - bottom) / 3f));
            var v = new List<Vector3>();
            var c = new List<Color>();
            var uv = new List<Vector2>();
            var outward = new List<Vector3>();
            float along = 0f;
            for (int i = 0; i < rim.Count; i++)
            {
                Vector3 prev = rim[Mathf.Max(0, i - 1)], next = rim[Mathf.Min(rim.Count - 1, i + 1)];
                Vector3 dir = next - prev;
                dir.y = 0f;
                // Rim lines are listed with the land on the right: outward is to the left.
                Vector3 n = new Vector3(-dir.z, 0f, dir.x).normalized;
                if (closedAroundCentre)
                {
                    Vector3 away = rim[i] - centre;
                    away.y = 0f;
                    n = away.normalized;
                }
                outward.Add(n);
                if (i > 0) along += Vector3.Distance(rim[i], rim[i - 1]);
                for (int k = 0; k <= rows; k++)
                {
                    float y = Mathf.Lerp(rim[i].y, bottom, k / (float)rows);
                    float depth = rim[i].y - y;
                    // Ledges: the wall steps out a little at the hard layers and slumps into a talus slope at the bottom.
                    float jag = k == 0 ? 0f : (Mathf.PerlinNoise(along * 0.08f + seed, y * 0.11f) - 0.5f) * 3.2f;
                    float step = Mathf.SmoothStep(0f, 2.5f, Mathf.Clamp01((depth - 28f) / 4f)) + Mathf.SmoothStep(0f, 3f, Mathf.Clamp01((depth - 62f) / 4f));
                    float talus = Mathf.Pow(Mathf.Clamp01((depth - (rim[i].y - bottom - 18f)) / 18f), 1.6f) * 22f;
                    v.Add(rim[i] + n * (jag + step + talus) + Vector3.up * (y - rim[i].y));
                    c.Add(Lin(Strata(y, along, seed) * shade));
                    uv.Add(new Vector2(along * 0.1f, y * 0.1f));
                }
            }
            var t = new List<int>();
            int w = rows + 1;
            for (int i = 0; i + 1 < rim.Count; i++)
                for (int k = 0; k < rows; k++)
                {
                    int a = i * w + k, b = (i + 1) * w + k;
                    // Wind each quad so it faces outward.
                    Vector3 pa = v[a], pb = v[b], pc = v[a + 1];
                    Vector3 normal = Vector3.Cross(pb - pa, pc - pa);
                    // (a, b, a+1) faces along "normal"; keep that order when it points outward, else reverse it.
                    if (Vector3.Dot(normal, outward[i]) >= 0f) t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                    else t.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
                }
            return MakeMesh("Edge_Wall", v, uv, c, t);
        }

        /// <summary>Canyon rock layers by height: pale caprock, salmon and red bands, brown at the bottom.</summary>
        private static Color Strata(float y, float along, int seed)
        {
            float wobble = (Mathf.PerlinNoise(along * 0.02f + seed, 0.5f) - 0.5f) * 5f;
            float h = y + wobble;
            Color[] bands =
            {
                new Color(0.76f, 0.40f, 0.24f), new Color(0.84f, 0.62f, 0.44f), new Color(0.64f, 0.30f, 0.18f),
                new Color(0.78f, 0.48f, 0.32f), new Color(0.56f, 0.27f, 0.18f), new Color(0.72f, 0.42f, 0.28f),
                new Color(0.60f, 0.34f, 0.24f), new Color(0.50f, 0.32f, 0.24f)
            };
            float t = Mathf.Repeat(-h / 12.5f, bands.Length);
            int i = Mathf.FloorToInt(t);
            Color a = bands[i % bands.Length], b = bands[(i + 1) % bands.Length];
            float f = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - i - 0.75f) / 0.25f));
            float grain = 0.92f + 0.16f * Mathf.PerlinNoise(along * 0.3f, y * 0.4f + seed);
            return Color.Lerp(a, b, f) * grain;
        }

        /// <summary>A flat-ish grid x0..x1 by z0..z1 (heights and colours from functions; or positions from <paramref name="place"/>).</summary>
        private static Mesh Grid(float x0, float x1, int nx, float z0, float z1, int nz, System.Func<float, float, float> height,
            System.Func<float, float, float, Color> colour, System.Func<float, float, Vector3> place = null)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var c = colour != null ? new List<Color>() : null;
            var t = new List<int>();
            for (int j = 0; j <= nz; j++)
                for (int i = 0; i <= nx; i++)
                {
                    float x = Mathf.Lerp(x0, x1, i / (float)nx), zt = j / (float)nz, z = Mathf.Lerp(z0, z1, zt);
                    Vector3 p = place != null ? place(x, zt) : new Vector3(x, height(x, z), z);
                    v.Add(p);
                    uv.Add(new Vector2(p.x, p.z));
                    c?.Add(Lin(colour(p.x, p.z, p.y)));
                }
            int w = nx + 1;
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    int a = j * w + i, b = a + w;
                    t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            Mesh m = MakeMesh("Edge_Grid", v, uv, c, t);
            FixWinding(m, Vector3.up);
            return m;
        }

        /// <summary>Flat strip from z0 to z1 between two edge functions (y = 0).</summary>
        private static Mesh Strip(float z0, float z1, int rows, System.Func<float, float> left, System.Func<float, float> right, System.Func<float, float> height)
        {
            const int across = 6;
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int j = 0; j <= rows; j++)
            {
                float z = Mathf.Lerp(z0, z1, j / (float)rows);
                for (int i = 0; i <= across; i++)
                {
                    float x = Mathf.Lerp(left(z), right(z), i / (float)across);
                    v.Add(new Vector3(x, height(z), z));
                    uv.Add(new Vector2(x, z));
                }
            }
            int w = across + 1;
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < across; i++)
                {
                    int a = j * w + i, b = a + w;
                    t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            Mesh m = MakeMesh("Edge_Strip", v, uv, null, t);
            FixWinding(m, Vector3.up);
            return m;
        }

        /// <summary>Flips the triangles if the mesh faces away from <paramref name="up"/> (so it is never seen from the wrong side).</summary>
        private static void FixWinding(Mesh m, Vector3 up)
        {
            int[] t = m.triangles;
            Vector3[] v = m.vertices;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 n = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (Vector3.Dot(n, up) < 0f) { int s = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = s; }
            }
            m.triangles = t;
            m.RecalculateNormals();
            m.RecalculateBounds();
        }

        private static Mesh MakeMesh(string name, List<Vector3> v, List<Vector2> uv, List<Color> c, List<int> t)
        {
            var mesh = new Mesh { name = name, indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(v);
            if (uv != null) mesh.SetUVs(0, uv);
            if (c != null) mesh.SetColors(c);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static GameObject AddSurface(Transform root, string name, Mesh mesh, Material material, bool collider)
        {
            mesh.name = name;
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

        private static Color Lin(Color c) => QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;

        // ------------------------------------------------------------ Materials

        private static void EnsureMaterials()
        {
            if (vertexLit != null) return;
            Shader vc = Resources.Load<Shader>("GolfSimZA_VertexColorLit");
            if (vc != null && vc.isSupported)
            {
                vertexLit = new Material(vc) { mainTexture = Noise(Color.white, new Color(0.62f, 0.62f, 0.62f), 31, 128) };
                vertexLit.SetFloat("_DetailScale", 1f);
            }
            else vertexLit = MiniGameWorld.Lit(new Color(0.72f, 0.48f, 0.34f), 0.05f);

            stripeMat = new Material(Shader.Find("Standard")) { mainTexture = Stripes(new Color(0.40f, 0.60f, 0.24f), new Color(0.31f, 0.49f, 0.18f)), mainTextureScale = new Vector2(1f / 16f, 1f / 40f) };
            stripeMat.SetFloat("_Glossiness", 0.08f);
            roughMat = new Material(Shader.Find("Standard")) { mainTexture = Noise(new Color(0.30f, 0.43f, 0.17f), new Color(0.40f, 0.46f, 0.22f), 13, 128), mainTextureScale = new Vector2(1f / 9f, 1f / 9f) };
            roughMat.SetFloat("_Glossiness", 0.02f);
            waterMat = new Material(Shader.Find("Standard")) { color = new Color(0.16f, 0.42f, 0.46f) };
            waterMat.SetFloat("_Glossiness", 0.9f);
            boulderMat = new Material(Shader.Find("Standard")) { mainTexture = Noise(new Color(0.62f, 0.44f, 0.32f), new Color(0.48f, 0.36f, 0.28f), 41, 64) };
            boulderMat.SetFloat("_Glossiness", 0.1f);
        }

        private static Texture2D Stripes(Color light, Color dark)
        {
            const int W = 64, H = 64;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8, name = "GolfSimZA_EdgeStripes" };
            var px = new Color[W * H];
            var rnd = new System.Random(5);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W;
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Abs(u - 0.5f) - 0.22f) / 0.06f));
                    px[y * W + x] = Color.Lerp(dark, light, k) * (0.94f + (float)rnd.NextDouble() * 0.12f);
                }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        private static Texture2D Noise(Color a, Color b, int seed, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4, name = "GolfSimZA_EdgeNoise" };
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
    }
}
