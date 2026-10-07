using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfSimZA.Physics
{
    /// <summary>
    /// GolfSim ZA trees: broadleaf, spreading and flowering trees, pines, palms, bushes and reeds
    /// built in code (no downloaded models, see <see cref="TreeMeshes"/>), drawn with GPU
    /// instancing - full detail near the camera, a lighter model further away - so thousands of
    /// trees stay cheap. The ball collides with the trees (not with reeds): a ball that flies into
    /// the leaves is slowed down and knocked about, a ball that hits a trunk bounces back.
    /// </summary>
    public sealed class TreeField : MonoBehaviour
    {
        public enum Kind { Pine, Broadleaf, Palm, Bush, Spreading, Flowering, Reed }
        private const int Kinds = 7;
        private const int Variants = 3;

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

        // Instanced drawing: per (kind, colour variant) every instance; per detail level the
        // matrices in lists of at most 1023 (one instanced draw each), regrouped as the camera moves.
        private struct Instance
        {
            public Matrix4x4 Matrix;
            public Vector3 Position;
        }

        private readonly List<Instance>[,] instances = new List<Instance>[Kinds, Variants];
        private readonly List<List<Matrix4x4>>[,,] drawLists = new List<List<Matrix4x4>>[Kinds, Variants, 2];
        private static Mesh[,] meshes;          // [kind, detail level]
        private static Material barkMaterial;
        private static Material[,] foliageMaterials;
        private Vector3 groupedFrom = new Vector3(float.MaxValue, 0f, 0f);
        private bool dirty = true;

        /// <summary>Trees further than this from the camera are drawn with the lighter model.</summary>
        private static float FarDistance => GolfSimZA.Core.AppSettings.Current.graphicsQuality >= 2 ? 260f : 170f;

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
                for (int v = 0; v < Variants; v++)
                {
                    instances[k, v]?.Clear();
                    for (int lod = 0; lod < 2; lod++) drawLists[k, v, lod]?.Clear();
                }
            dirty = true;
        }

        /// <summary>Adds a tree (or reed clump) standing on the ground at basePosition.</summary>
        public void Add(Kind kind, Vector3 basePosition, float height, float yawDeg, float widthScale, int colourVariant)
        {
            EnsureAssets();
            int k = (int)kind, v = Mathf.Clamp(colourVariant, 0, Variants - 1);
            if (instances[k, v] == null) instances[k, v] = new List<Instance>();
            float width = height * widthScale;
            // Models are built 1 unit tall; sink the base a little so slopes show no gap.
            Matrix4x4 m = Matrix4x4.TRS(basePosition - Vector3.up * (kind == Kind.Reed ? 0.15f : 0.3f), Quaternion.Euler(0f, yawDeg, 0f), new Vector3(width, height, width));
            instances[k, v].Add(new Instance { Matrix = m, Position = basePosition });
            dirty = true;
            if (kind == Kind.Reed) return; // reeds do not stop the ball

            var info = new TreeInfo { Base = basePosition, Height = height };
            switch (kind)
            {
                case Kind.Palm:
                    info.TrunkRadius = 0.025f * width + 0.1f;
                    info.CanopyCentre = basePosition + Vector3.up * height * 0.88f;
                    info.CanopyRadius = 0.36f * width;
                    info.CanopyHalfHeight = height * 0.12f;
                    break;
                case Kind.Pine:
                    info.TrunkRadius = 0.035f * width + 0.1f;
                    info.CanopyCentre = basePosition + Vector3.up * height * 0.58f;
                    info.CanopyRadius = 0.3f * width;
                    info.CanopyHalfHeight = height * 0.4f;
                    break;
                case Kind.Spreading:
                    info.TrunkRadius = 0.045f * width + 0.1f;
                    info.CanopyCentre = basePosition + Vector3.up * height * 0.72f;
                    info.CanopyRadius = 0.5f * width;
                    info.CanopyHalfHeight = height * 0.18f;
                    break;
                case Kind.Bush:
                    info.TrunkRadius = 0.1f;
                    info.CanopyCentre = basePosition + Vector3.up * height * 0.45f;
                    info.CanopyRadius = 0.45f * width;
                    info.CanopyHalfHeight = height * 0.42f;
                    break;
                default: // broadleaf, flowering
                    info.TrunkRadius = 0.045f * width + 0.1f;
                    info.CanopyCentre = basePosition + Vector3.up * height * 0.66f;
                    info.CanopyRadius = 0.38f * width;
                    info.CanopyHalfHeight = height * 0.28f;
                    break;
            }
            int index = trees.Count;
            trees.Add(info);
            Vector2Int c = CellOf(basePosition);
            if (!grid.TryGetValue(c, out List<int> list)) grid[c] = list = new List<int>();
            list.Add(index);
        }

        /// <summary>
        /// A tree that is drawn by something else (a course's own trees): only its trunk and crown
        /// are added, so the ball hits it like a GolfSim ZA tree.
        /// </summary>
        public void AddObstacle(Vector3 basePosition, float height, float crownRadius, float crownBottom)
        {
            if (height < 1f || crownRadius < 0.3f) return;
            crownBottom = Mathf.Clamp(crownBottom, 0f, height * 0.85f);
            var info = new TreeInfo
            {
                Base = basePosition,
                Height = height,
                TrunkRadius = Mathf.Clamp(crownRadius * 0.08f, 0.12f, 0.6f),
                CanopyCentre = basePosition + Vector3.up * (crownBottom + height) * 0.5f,
                CanopyRadius = crownRadius,
                CanopyHalfHeight = Mathf.Max(0.5f, (height - crownBottom) * 0.5f)
            };
            int index = trees.Count;
            trees.Add(info);
            Vector2Int c = CellOf(basePosition);
            if (!grid.TryGetValue(c, out List<int> list)) grid[c] = list = new List<int>();
            list.Add(index);
        }

        /// <summary>
        /// The nearest tree in front of a point (within maxAngle of dir, between minDist and
        /// maxDist away): its base and crown centre height. For the automatic test.
        /// </summary>
        public bool FindTreeAhead(Vector3 from, Vector3 dir, float minDist, float maxDist, float maxAngle, out Vector3 basePos, out float crownCentreY)
        {
            basePos = Vector3.zero;
            crownCentreY = 0f;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return false;
            dir.Normalize();
            float best = float.MaxValue;
            foreach (TreeInfo t in trees)
            {
                Vector3 d = t.Base - from;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist < minDist || dist > maxDist || t.Height < 6f) continue;
                if (Vector3.Angle(dir, d) > maxAngle) continue;
                if (dist < best) { best = dist; basePos = t.Base; crownCentreY = t.CanopyCentre.y; }
            }
            return best < float.MaxValue;
        }

        /// <summary>Call after adding trees (the drawing lists are rebuilt on the next frame).</summary>
        public void Commit() => dirty = true;

        /// <summary>Number of reed clumps and trees drawn (for reports).</summary>
        public int DrawnCount
        {
            get
            {
                int n = 0;
                foreach (List<Instance> list in instances) if (list != null) n += list.Count;
                return n;
            }
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null || meshes == null) return;
            Vector3 camPos = cam.transform.position;
            float far = FarDistance;
            Vector3 moved = camPos - groupedFrom;
            moved.y = 0f;
            if (dirty || moved.sqrMagnitude > 12f * 12f)
            {
                Regroup(camPos, far);
                groupedFrom = camPos;
                dirty = false;
            }
            for (int k = 0; k < Kinds; k++)
                for (int v = 0; v < Variants; v++)
                    for (int lod = 0; lod < 2; lod++)
                    {
                        List<List<Matrix4x4>> lists = drawLists[k, v, lod];
                        if (lists == null) continue;
                        Mesh mesh = meshes[k, lod];
                        bool bark = mesh.GetSubMesh(0).indexCount > 0;
                        ShadowCastingMode shadows = lod == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                        foreach (List<Matrix4x4> batch in lists)
                        {
                            if (batch.Count == 0) continue;
                            if (bark) Graphics.DrawMeshInstanced(mesh, 0, barkMaterial, batch, null, shadows, true);
                            Graphics.DrawMeshInstanced(mesh, 1, foliageMaterials[k, v], batch, null, shadows, true);
                        }
                    }
        }

        /// <summary>Sorts every instance into the near (full detail) or far (light model) lists.</summary>
        private void Regroup(Vector3 camPos, float far)
        {
            float far2 = far * far;
            for (int k = 0; k < Kinds; k++)
                for (int v = 0; v < Variants; v++)
                {
                    for (int lod = 0; lod < 2; lod++)
                    {
                        if (drawLists[k, v, lod] == null) drawLists[k, v, lod] = new List<List<Matrix4x4>>();
                        foreach (List<Matrix4x4> l in drawLists[k, v, lod]) l.Clear();
                    }
                    List<Instance> all = instances[k, v];
                    if (all == null) continue;
                    var used = new int[2];
                    foreach (Instance i in all)
                    {
                        float dx = i.Position.x - camPos.x, dz = i.Position.z - camPos.z;
                        int lod = dx * dx + dz * dz < far2 ? 0 : 1;
                        List<List<Matrix4x4>> lists = drawLists[k, v, lod];
                        int slot = used[lod] / 1023;
                        while (lists.Count <= slot) lists.Add(new List<Matrix4x4>(1023));
                        lists[slot].Add(i.Matrix);
                        used[lod]++;
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

        private static TreeMeshes.Model ModelOf(Kind kind)
        {
            switch (kind)
            {
                case Kind.Pine: return TreeMeshes.Model.Pine;
                case Kind.Palm: return TreeMeshes.Model.Palm;
                case Kind.Bush: return TreeMeshes.Model.Bush;
                case Kind.Spreading: return TreeMeshes.Model.Spreading;
                case Kind.Flowering: return TreeMeshes.Model.Flowering;
                case Kind.Reed: return TreeMeshes.Model.Reed;
                default: return TreeMeshes.Model.Broadleaf;
            }
        }

        private static void EnsureAssets()
        {
            if (meshes != null) return;
            meshes = new Mesh[Kinds, 2];
            for (int k = 0; k < Kinds; k++)
            {
                Mesh[] built = TreeMeshes.Build(ModelOf((Kind)k));
                meshes[k, 0] = built[0];
                meshes[k, 1] = built[1];
            }
            Texture2D atlas = TreeMeshes.Atlas();
            barkMaterial = MakeMaterial(atlas, new Color(0.55f, 0.47f, 0.40f), 0.5f, 0.02f);
            foliageMaterials = new Material[Kinds, Variants];
            // Colour variants per kind (yellow-green, mid green, dark green; flowers: pink, coral, yellow).
            Color[][] tints =
            {
                new[] { new Color(0.17f, 0.36f, 0.19f), new Color(0.20f, 0.40f, 0.20f), new Color(0.14f, 0.31f, 0.18f) },   // pine
                new[] { new Color(0.42f, 0.62f, 0.22f), new Color(0.31f, 0.53f, 0.19f), new Color(0.24f, 0.43f, 0.17f) },   // broadleaf
                new[] { new Color(0.38f, 0.58f, 0.22f), new Color(0.44f, 0.62f, 0.24f), new Color(0.32f, 0.50f, 0.19f) },   // palm
                new[] { new Color(0.36f, 0.52f, 0.20f), new Color(0.44f, 0.56f, 0.24f), new Color(0.28f, 0.44f, 0.18f) },   // bush
                new[] { new Color(0.36f, 0.56f, 0.21f), new Color(0.46f, 0.62f, 0.24f), new Color(0.28f, 0.47f, 0.18f) },   // spreading
                new[] { new Color(0.95f, 0.50f, 0.62f), new Color(0.95f, 0.42f, 0.22f), new Color(0.78f, 0.78f, 0.26f) },   // flowering
                new[] { new Color(0.55f, 0.62f, 0.32f), new Color(0.64f, 0.66f, 0.36f), new Color(0.47f, 0.57f, 0.28f) },   // reed
            };
            float[] wind = { 0.03f, 0.05f, 0.08f, 0.04f, 0.04f, 0.05f, 0.14f };
            for (int k = 0; k < Kinds; k++)
                for (int v = 0; v < Variants; v++)
                    foliageMaterials[k, v] = MakeMaterial(atlas, tints[k][v], 0.45f, wind[k]);
        }

        private static Material MakeMaterial(Texture2D atlas, Color tint, float cutoff, float wind)
        {
            Shader shader = Resources.Load<Shader>("GolfSimZA_Foliage");
            if (shader == null || !shader.isSupported) shader = Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse");
            var m = new Material(shader) { color = tint, enableInstancing = true, hideFlags = HideFlags.DontSave };
            m.mainTexture = atlas;
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", cutoff);
            if (m.HasProperty("_WindStrength")) m.SetFloat("_WindStrength", wind);
            return m;
        }
    }
}
