using System.Collections.Generic;
using GolfSimZA.Physics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfSimZA.Courses
{
    /// <summary>
    /// Makes an imported course's own trees solid for the ball. Most GSPro course trees have no
    /// collider (GSPro handles them itself), so a ball flew straight through them. Every terrain
    /// tree and every tree object of the course is added to the ball's tree obstacles (trunk and
    /// crown sized from the tree's own model): leaves slow the ball and knock it about, a trunk
    /// bounces it back - the same as GolfSim ZA's own trees.
    /// </summary>
    public static class CourseObstacles
    {
        public static int AddCourseTrees(Scene scene, TreeField field)
        {
            if (field == null || !scene.IsValid()) return 0;
            int added = 0;
            var prototypeSize = new Dictionary<GameObject, Bounds>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                // 1. Terrain trees.
                foreach (Terrain t in root.GetComponentsInChildren<Terrain>(true))
                {
                    TerrainData d = t.terrainData;
                    if (d == null || d.treeInstanceCount == 0) continue;
                    TreePrototype[] protos = d.treePrototypes;
                    Vector3 origin = t.GetPosition();
                    foreach (TreeInstance tree in d.treeInstances)
                    {
                        if (tree.prototypeIndex < 0 || tree.prototypeIndex >= protos.Length) continue;
                        GameObject prefab = protos[tree.prototypeIndex].prefab;
                        if (prefab == null) continue;
                        if (!prototypeSize.TryGetValue(prefab, out Bounds b)) prototypeSize[prefab] = b = LocalBounds(prefab);
                        if (b.size.y < 0.5f) continue;
                        Vector3 pos = origin + Vector3.Scale(tree.position, d.size);
                        float h = b.max.y * tree.heightScale;
                        float r = Mathf.Max(b.extents.x, b.extents.z) * tree.widthScale * 0.85f;
                        field.AddObstacle(pos, h, r, CrownBottom(prefab.name, h));
                        added++;
                    }
                }

                // 2. Tree objects (one per tree: the object with the LOD group, else the top tree-named object).
                var done = new HashSet<Transform>();
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    Transform tree = TreeRoot(r.transform);
                    if (tree == null || !done.Add(tree)) continue;
                    Bounds wb = WorldBounds(tree);
                    if (wb.size.y < 1.2f || wb.size.y > 80f) continue;
                    float ground = GroundProbe.HeightAt(wb.center);
                    Vector3 basePos = new Vector3(wb.center.x, Mathf.Min(wb.min.y, ground + 0.5f), wb.center.z);
                    float h = wb.max.y - basePos.y;
                    float radius = Mathf.Max(wb.extents.x, wb.extents.z) * 0.85f;
                    // A single tree only: a whole band of forest or a long hedge in one object
                    // would turn a huge box into "leaves" (its own colliders, if any, still count).
                    if (radius > 12f || radius > h * 1.3f) continue;
                    field.AddObstacle(basePos, h, radius, CrownBottom(tree.name, h));
                    added++;
                }
            }
            return added;
        }

        /// <summary>Bushes and shrubs are leaves almost to the ground; trees have a bare trunk below the crown.</summary>
        private static float CrownBottom(string name, float height)
        {
            string n = (name ?? "").ToLowerInvariant();
            if (n.Contains("bush") || n.Contains("shrub") || n.Contains("hedge") || n.Contains("azalea")) return 0.1f;
            if (n.Contains("palm")) return height * 0.75f;
            if (n.Contains("pine") || n.Contains("conifer") || n.Contains("spruce") || n.Contains("fir") || n.Contains("cedar") || n.Contains("cypress")) return height * 0.2f;
            return height * 0.35f;
        }

        /// <summary>The object that is "the tree" for a renderer, or null when it is not part of a tree.</summary>
        private static Transform TreeRoot(Transform t)
        {
            LODGroup lod = t.GetComponentInParent<LODGroup>();
            Transform best = null;
            for (Transform p = t; p != null; p = p.parent)
            {
                if (CourseSurvey.LooksLikeTree(p.name)) best = p;
                else if (best != null) break;
            }
            if (lod != null && (best == null || lod.transform.IsChildOf(best) || best.IsChildOf(lod.transform)))
                return CourseSurvey.LooksLikeTree(lod.name) || best != null ? lod.transform : null;
            return best;
        }

        private static Bounds WorldBounds(Transform tree)
        {
            Renderer[] rs = tree.GetComponentsInChildren<Renderer>(true);
            Bounds b = new Bounds(tree.position, Vector3.zero);
            bool any = false;
            foreach (Renderer r in rs)
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        /// <summary>Size of a tree prefab (not placed in the world) from its meshes, relative to its root.</summary>
        private static Bounds LocalBounds(GameObject prefab)
        {
            Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix;
            Bounds b = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Matrix4x4 m = toRoot * mf.transform.localToWorldMatrix;
                Bounds mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }
    }
}
