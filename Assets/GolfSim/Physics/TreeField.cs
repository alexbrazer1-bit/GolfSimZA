using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfSimZA.Physics
{
    /// <summary>
    /// GolfSim ZA trees: pines and broadleaf trees built in code (no downloaded models), drawn
    /// with GPU instancing so hundreds of trees stay cheap. The ball collides with them: a ball
    /// that flies into the leaves is slowed down and knocked about, a ball that hits a trunk
    /// bounces back.
    /// </summary>
    public sealed class TreeField : MonoBehaviour
    {
        public enum Kind { Pine, Broadleaf, Palm, Bush }
        private const int Kinds = 4;

        private struct TreeInfo
        {
            public Vector3 Base;
            public float Height;
            public float TrunkRadius;
            public Vector3 CanopyCentre;
            public float CanopyRadius;
            public float CanopyHalfHeight;
        }

        private static TreeField active;
        private readonly List<TreeInfo> trees = new List<TreeInfo>();
        private readonly Dictionary<Vector2Int, List<int>> grid = new Dictionary<Vector2Int, List<int>>();
        private const float Cell = 20f;

        // Instanced drawing: per (kind, colour variant) a list of matrices in batches of 1023.
        private readonly List<Matrix4x4>[,] matrices = new List<Matrix4x4>[Kinds, 3];
        private readonly List<Matrix4x4[]>[,] batches = new List<Matrix4x4[]>[Kinds, 3];
        private static Mesh pineMesh, broadMesh, palmMesh;
        private static Material palmTrunkMaterial;
        private static Material trunkMaterial;
        private static Material[,] foliageMaterials;

        public int Count => trees.Count;

        /// <summary>The tree field of the current scene (for ball collisions), or null.</summary>
        public static TreeField Active => active != null && active.isActiveAndEnabled ? active : null;

        private void OnEnable() => active = this;
        private void OnDisable() { if (active == this) active = null; }

        public void Clear()
        {
            trees.Clear();
            grid.Clear();
            for (int k = 0; k < Kinds; k++)
                for (int v = 0; v < 3; v++)
                {
                    matrices[k, v]?.Clear();
                    batches[k, v]?.Clear();
                }
        }

        /// <summary>Adds a tree standing on the ground at basePosition.</summary>
        public void Add(Kind kind, Vector3 basePosition, float height, float yawDeg, float widthScale, int colourVariant)
        {
            EnsureAssets();
            int k = (int)kind, v = Mathf.Clamp(colourVariant, 0, 2);
            if (matrices[k, v] == null) matrices[k, v] = new List<Matrix4x4>();
            float width = height * widthScale;
            // Meshes are built 1 unit tall; sink the base a little so slopes show no gap.
            Matrix4x4 m = Matrix4x4.TRS(basePosition - Vector3.up * 0.3f, Quaternion.Euler(0f, yawDeg, 0f), new Vector3(width, height, width));
            matrices[k, v].Add(m);

            var info = new TreeInfo { Base = basePosition, Height = height };
            if (kind == Kind.Palm)
            {
                info.TrunkRadius = 0.025f * width + 0.1f;
                info.CanopyCentre = basePosition + Vector3.up * height * 0.9f;
                info.CanopyRadius = 0.3f * width;
                info.CanopyHalfHeight = height * 0.1f;
            }
            else if (kind == Kind.Pine)
            {
                info.TrunkRadius = 0.035f * width + 0.1f;
                info.CanopyCentre = basePosition + Vector3.up * height * 0.55f;
                info.CanopyRadius = 0.26f * width;
                info.CanopyHalfHeight = height * 0.42f;
            }
            else
            {
                info.TrunkRadius = 0.04f * width + 0.1f;
                info.CanopyCentre = basePosition + Vector3.up * height * 0.66f;
                info.CanopyRadius = 0.38f * width;
                info.CanopyHalfHeight = height * 0.30f;
            }
            int index = trees.Count;
            trees.Add(info);
            Vector2Int c = CellOf(basePosition);
            if (!grid.TryGetValue(c, out List<int> list)) grid[c] = list = new List<int>();
            list.Add(index);
        }

        /// <summary>Call after adding trees: packs the matrices for drawing.</summary>
        public void Commit()
        {
            for (int k = 0; k < Kinds; k++)
                for (int v = 0; v < 3; v++)
                {
                    if (batches[k, v] == null) batches[k, v] = new List<Matrix4x4[]>();
                    batches[k, v].Clear();
                    List<Matrix4x4> all = matrices[k, v];
                    if (all == null) continue;
                    for (int i = 0; i < all.Count; i += 1023)
                        batches[k, v].Add(all.GetRange(i, Mathf.Min(1023, all.Count - i)).ToArray());
                }
        }

        private void LateUpdate()
        {
            if (trees.Count == 0) return;
            for (int k = 0; k < Kinds; k++)
            {
                Mesh mesh = k == 0 ? pineMesh : k == 2 ? palmMesh : broadMesh;
                Material trunk = k == 2 ? palmTrunkMaterial : trunkMaterial;
                for (int v = 0; v < 3; v++)
                {
                    List<Matrix4x4[]> list = batches[k, v];
                    if (list == null) continue;
                    foreach (Matrix4x4[] batch in list)
                    {
                        Graphics.DrawMeshInstanced(mesh, 0, trunk, batch, batch.Length, null, ShadowCastingMode.On, true);
                        Graphics.DrawMeshInstanced(mesh, 1, foliageMaterials[k, v], batch, batch.Length, null, ShadowCastingMode.On, true);
                    }
                }
            }
        }

        private static Vector2Int CellOf(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.z / Cell));

        /// <summary>True when there is a tree trunk or crown within radius of the point (for planting).</summary>
        public bool IsNear(Vector3 p, float radius)
        {
            Vector2Int c = CellOf(p);
            int reach = Mathf.CeilToInt(radius / Cell);
            for (int x = -reach; x <= reach; x++)
                for (int z = -reach; z <= reach; z++)
                    if (grid.TryGetValue(new Vector2Int(c.x + x, c.y + z), out List<int> list))
                        foreach (int i in list)
                        {
                            Vector3 d = trees[i].Base - p;
                            d.y = 0f;
                            if (d.magnitude < radius) return true;
                        }
            return false;
        }

        /// <summary>
        /// Ball against trees. Returns true and changes the velocity when the ball is inside a crown
        /// or hits a trunk. hitTrees stops the same tree from slowing the ball on every step.
        /// </summary>
        public bool Collide(Vector3 ballPosition, ref Vector3 velocity, HashSet<int> hitTrees)
        {
            Vector2Int c = CellOf(ballPosition);
            for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                {
                    if (!grid.TryGetValue(new Vector2Int(c.x + x, c.y + z), out List<int> list)) continue;
                    foreach (int i in list)
                    {
                        TreeInfo t = trees[i];
                        float up = ballPosition.y - t.Base.y;
                        if (up < 0f || up > t.Height) continue;
                        Vector3 flat = ballPosition - t.Base;
                        flat.y = 0f;

                        // Trunk (lower part): bounce back off the bark.
                        if (up < t.Height * 0.5f && flat.magnitude < t.TrunkRadius && hitTrees.Add(-(i + 1)))
                        {
                            Vector3 n = flat.sqrMagnitude > 0.0001f ? flat.normalized : -new Vector3(velocity.x, 0f, velocity.z).normalized;
                            Vector3 h = new Vector3(velocity.x, 0f, velocity.z);
                            float into = Vector3.Dot(h, n);
                            if (into < 0f) h -= 2f * into * n;
                            velocity = new Vector3(h.x * 0.45f, velocity.y * 0.6f, h.z * 0.45f);
                            return true;
                        }

                        // Crown: an ellipsoid of leaves and branches.
                        if (hitTrees.Contains(i)) continue;
                        Vector3 d = ballPosition - t.CanopyCentre;
                        float e = (d.x * d.x + d.z * d.z) / (t.CanopyRadius * t.CanopyRadius) + d.y * d.y / (t.CanopyHalfHeight * t.CanopyHalfHeight);
                        if (e > 1f) continue;
                        hitTrees.Add(i);
                        // Deeper hits lose more speed; the ball is knocked in a random direction.
                        float keep = Mathf.Lerp(0.15f, 0.55f, e) * Random.Range(0.8f, 1.1f);
                        Vector3 random = Random.insideUnitSphere * velocity.magnitude * 0.35f;
                        velocity = velocity * keep + random * keep;
                        if (velocity.y > 3f) velocity.y = 3f;
                        return true;
                    }
                }
            return false;
        }

        // ------------------------------------------------------------ Assets

        private static void EnsureAssets()
        {
            if (pineMesh != null) return;
            pineMesh = BuildPine();
            broadMesh = BuildBroadleaf();
            palmMesh = BuildPalm();
            trunkMaterial = MakeMaterial(new Color(0.30f, 0.22f, 0.15f));
            palmTrunkMaterial = MakeMaterial(new Color(0.46f, 0.38f, 0.29f));
            foliageMaterials = new Material[Kinds, 3];
            Color[] palms = { new Color(0.24f, 0.40f, 0.14f), new Color(0.30f, 0.45f, 0.16f), new Color(0.21f, 0.36f, 0.13f) };
            Color[] bushes = { new Color(0.34f, 0.38f, 0.20f), new Color(0.40f, 0.42f, 0.24f), new Color(0.28f, 0.34f, 0.17f) };
            Color[] pines = { new Color(0.10f, 0.26f, 0.12f), new Color(0.12f, 0.30f, 0.14f), new Color(0.09f, 0.23f, 0.13f) };
            Color[] broad = { new Color(0.22f, 0.42f, 0.14f), new Color(0.27f, 0.47f, 0.16f), new Color(0.19f, 0.38f, 0.13f) };
            for (int v = 0; v < 3; v++)
            {
                foliageMaterials[0, v] = MakeMaterial(pines[v]);
                foliageMaterials[1, v] = MakeMaterial(broad[v]);
                foliageMaterials[2, v] = MakeMaterial(palms[v]);
                foliageMaterials[3, v] = MakeMaterial(bushes[v]);
            }
        }

        private static Material MakeMaterial(Color color)
        {
            Shader shader = Shader.Find("Standard");
            var m = new Material(shader) { color = color, enableInstancing = true };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.08f);
            return m;
        }

        private static Mesh BuildPine()
        {
            var b = new MeshBuilder(new System.Random(7));
            b.Cylinder(0, 0.035f, 0.025f, 0f, 0.35f, 7);
            // Stacked cones, widest at the bottom.
            float[] bottoms = { 0.14f, 0.32f, 0.50f, 0.68f };
            float[] radii = { 0.32f, 0.27f, 0.20f, 0.13f };
            float[] tops = { 0.52f, 0.68f, 0.84f, 1.0f };
            for (int i = 0; i < bottoms.Length; i++) b.Cone(1, radii[i], bottoms[i], tops[i], 11, 0.05f);
            return b.ToMesh("GolfSimZA_Pine");
        }

        private static Mesh BuildBroadleaf()
        {
            var b = new MeshBuilder(new System.Random(11));
            b.Cylinder(0, 0.045f, 0.03f, 0f, 0.55f, 7);
            b.Blob(1, new Vector3(0f, 0.66f, 0f), new Vector3(0.30f, 0.24f, 0.30f), 0.12f);
            b.Blob(1, new Vector3(0.17f, 0.58f, 0.05f), new Vector3(0.22f, 0.18f, 0.22f), 0.14f);
            b.Blob(1, new Vector3(-0.15f, 0.60f, -0.09f), new Vector3(0.22f, 0.19f, 0.22f), 0.14f);
            b.Blob(1, new Vector3(0.02f, 0.80f, 0.10f), new Vector3(0.21f, 0.17f, 0.21f), 0.14f);
            b.Blob(1, new Vector3(-0.06f, 0.56f, 0.19f), new Vector3(0.19f, 0.16f, 0.19f), 0.14f);
            return b.ToMesh("GolfSimZA_Broadleaf");
        }

        /// <summary>
        /// Palm: a slightly curved, ringed trunk and a crown of arching fronds (both sides drawn).
        /// Built 1 unit tall; the fronds reach about 0.36 of the width scale.
        /// </summary>
        private static Mesh BuildPalm()
        {
            var b = new MeshBuilder(new System.Random(17));
            const int rings = 10;
            Vector3 prev = Vector3.zero;
            for (int r = 0; r < rings; r++)
            {
                float t0 = r / (float)rings, t1 = (r + 1) / (float)rings;
                Vector3 c0 = new Vector3(0.06f * t0 * t0, t0 * 0.92f, 0f), c1 = new Vector3(0.06f * t1 * t1, t1 * 0.92f, 0f);
                b.Segment(0, c0, c1, Mathf.Lerp(0.03f, 0.018f, t0) * (r % 2 == 0 ? 1.08f : 1f), Mathf.Lerp(0.03f, 0.018f, t1), 7);
            }
            Vector3 top = new Vector3(0.06f, 0.92f, 0f);
            for (int f = 0; f < 13; f++)
            {
                float yaw = f / 13f * Mathf.PI * 2f + (f % 2) * 0.2f;
                float lift = f % 3 == 0 ? 0.10f : 0.05f;
                b.Frond(1, top, new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)), 0.36f, lift, 0.05f);
            }
            return b.ToMesh("GolfSimZA_Palm");
        }

        /// <summary>Tiny mesh builder with two submeshes (0 = trunk, 1 = leaves).</summary>
        private sealed class MeshBuilder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<int>[] triangles = { new List<int>(), new List<int>() };
            private readonly System.Random random;

            public MeshBuilder(System.Random random) { this.random = random; }

            private float Jitter(float amount) => (float)(random.NextDouble() * 2.0 - 1.0) * amount;

            public void Cylinder(int sub, float r0, float r1, float y0, float y1, int segments)
            {
                int start = vertices.Count;
                for (int i = 0; i <= segments; i++)
                {
                    float a = i / (float)segments * Mathf.PI * 2f;
                    Vector3 n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    vertices.Add(n * r0 + Vector3.up * y0); normals.Add(n);
                    vertices.Add(n * r1 + Vector3.up * y1); normals.Add(n);
                }
                for (int i = 0; i < segments; i++)
                {
                    int a = start + i * 2;
                    triangles[sub].AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
                }
            }

            public void Cone(int sub, float radius, float y0, float y1, int segments, float jitter)
            {
                int start = vertices.Count;
                float slope = radius / (y1 - y0);
                for (int i = 0; i < segments; i++)
                {
                    float a = i / (float)segments * Mathf.PI * 2f;
                    float r = radius * (1f + Jitter(0.12f));
                    Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    vertices.Add(dir * r + Vector3.up * (y0 + Jitter(jitter)));
                    normals.Add((dir + Vector3.up * slope).normalized);
                }
                int tip = vertices.Count;
                vertices.Add(Vector3.up * y1); normals.Add(Vector3.up);
                int centre = vertices.Count;
                vertices.Add(Vector3.up * (y0 + 0.02f)); normals.Add(Vector3.down);
                for (int i = 0; i < segments; i++)
                {
                    int a = start + i, bIndex = start + (i + 1) % segments;
                    triangles[sub].AddRange(new[] { a, tip, bIndex });
                    triangles[sub].AddRange(new[] { a, bIndex, centre });
                }
            }

            public void Blob(int sub, Vector3 centre, Vector3 radius, float jitter)
            {
                const int rings = 6, segments = 10;
                int start = vertices.Count;
                for (int r = 0; r <= rings; r++)
                {
                    float v = r / (float)rings * Mathf.PI;
                    for (int s = 0; s <= segments; s++)
                    {
                        float u = s / (float)segments * Mathf.PI * 2f;
                        Vector3 n = new Vector3(Mathf.Sin(v) * Mathf.Cos(u), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Sin(u));
                        float bump = (r == 0 || r == rings || s == segments) ? 1f : 1f + Jitter(jitter);
                        vertices.Add(centre + Vector3.Scale(n, radius) * bump);
                        normals.Add(n);
                    }
                }
                for (int r = 0; r < rings; r++)
                    for (int s = 0; s < segments; s++)
                    {
                        int a = start + r * (segments + 1) + s;
                        int bIndex = a + segments + 1;
                        triangles[sub].AddRange(new[] { a, a + 1, bIndex, a + 1, bIndex + 1, bIndex });
                    }
            }

            /// <summary>Tapered tube between two points (palm trunk rings).</summary>
            public void Segment(int sub, Vector3 a, Vector3 b, float ra, float rb, int segments)
            {
                int start = vertices.Count;
                for (int i = 0; i <= segments; i++)
                {
                    float ang = i / (float)segments * Mathf.PI * 2f;
                    Vector3 n = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    vertices.Add(a + n * ra); normals.Add(n);
                    vertices.Add(b + n * rb); normals.Add(n);
                }
                for (int i = 0; i < segments; i++)
                {
                    int k = start + i * 2;
                    triangles[sub].AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 });
                }
            }

            /// <summary>A frond: a strip that rises a little then arches down, drawn on both sides.</summary>
            public void Frond(int sub, Vector3 root, Vector3 dir, float length, float lift, float width)
            {
                const int steps = 7;
                Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
                int start = vertices.Count;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float y = lift * Mathf.Sin(t * Mathf.PI * 0.6f) - 0.22f * length * t * t;
                    Vector3 c = root + dir * (length * t) + Vector3.up * y;
                    float w = width * Mathf.Sin(Mathf.Lerp(0.25f, 1f, t) * Mathf.PI) * (1f + Jitter(0.15f));
                    vertices.Add(c - side * w); normals.Add(Vector3.up);
                    vertices.Add(c + side * w); normals.Add(Vector3.up);
                }
                for (int i = 0; i < steps; i++)
                {
                    int k = start + i * 2;
                    triangles[sub].AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 });
                    triangles[sub].AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 });
                }
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.subMeshCount = 2;
                mesh.SetTriangles(triangles[0], 0);
                mesh.SetTriangles(triangles[1], 1);
                mesh.RecalculateBounds();
                // Instances are scaled up a lot; keep culling generous.
                mesh.bounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(1.2f, 1.2f, 1.2f));
                return mesh;
            }
        }
    }
}
