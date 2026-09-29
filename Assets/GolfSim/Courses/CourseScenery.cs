using System.Collections.Generic;
using GolfSimZA.Core;
using GolfSimZA.Physics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfSimZA.Courses
{
    /// <summary>
    /// Makes imported courses and the range look more like a real course:
    ///  * terrain trees and grass drawn further away, finer terrain detail;
    ///  * GolfSim ZA trees planted in the rough between holes when a course has no (visible) trees
    ///    (Settings → VISUAL SETTINGS → COURSE LOOK → ADD TREES), never on fairways, greens,
    ///    tees, bunkers or water;
    ///  * clouds over a plain sky and the colour boost on the camera.
    /// </summary>
    public static class CourseScenery
    {
        /// <summary>Corridor kept clear around each hole's playing line (metres from the line).</summary>
        private const float ClearCorridor = 30f;

        /// <summary>A course with fewer trees than this gets GolfSim ZA trees (ADD TREES = courses without trees).</summary>
        public const int FewTrees = 40;

        public static string ImproveCourse(Scene scene, CourseDefinition course, GameObject host, Camera camera)
        {
            // 1. Terrain drawing: trees, grass and ground detail visible further away - scaled to how
            //    many trees the course has, so tree-heavy courses keep a smooth frame rate.
            int courseTrees = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Terrain t in root.GetComponentsInChildren<Terrain>(true))
                    if (t.terrainData != null) courseTrees += t.terrainData.treeInstanceCount;
            int quality = AppSettings.Current.graphicsQuality;
            float qualityScale = quality >= 3 ? 1f : quality == 2 ? 0.75f : quality == 1 ? 0.55f : 0.4f;
            float treeReach = (courseTrees > 12000 ? 750f : courseTrees > 6000 ? 1000f : courseTrees > 2500 ? 1500f : 2000f) * qualityScale;
            int fullTrees = Mathf.RoundToInt((courseTrees > 12000 ? 3500 : courseTrees > 6000 ? 5000 : 8000) * qualityScale);

            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Terrain t in root.GetComponentsInChildren<Terrain>(true))
                {
                    t.drawTreesAndFoliage = true;
                    t.drawInstanced = true;
                    // Course trees made for GSPro's tree add-on have no working far-away (billboard)
                    // version here - they vanish at the billboard distance - so they are drawn as real
                    // trees (GPU instanced) up to the tree distance, which fog softens.
                    t.treeDistance = treeReach;
                    t.treeBillboardDistance = treeReach;
                    t.treeCrossFadeLength = Mathf.Max(t.treeCrossFadeLength, 30f);
                    t.treeMaximumFullLODCount = Mathf.Max(t.treeMaximumFullLODCount, fullTrees);
                    t.treeLODBiasMultiplier = Mathf.Max(t.treeLODBiasMultiplier, 1.2f);
                    // Terrain grass / flowers: the course's own setting, at most 90 m (heavy on big courses).
                    t.detailObjectDistance = Mathf.Clamp(t.detailObjectDistance, 40f, quality >= 3 ? 90f : 60f);
                    t.basemapDistance = Mathf.Max(t.basemapDistance, 1500f);
                    t.heightmapPixelError = Mathf.Min(t.heightmapPixelError, quality >= 2 ? 4f : 8f);
                }

            // 1b. Course objects (bushes, trees, rocks placed as objects - some courses have tens of
            //     thousands): draw repeated ones together (GPU instancing) and let small plants skip
            //     casting shadows. Keeps big courses smooth.
            int instanced = 0, shadowless = 0;
            var seen = new HashSet<Material>();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    foreach (Material m in r.sharedMaterials)
                        if (m != null && seen.Add(m) && !m.enableInstancing && m.shader != null && m.shader.isSupported)
                        {
                            m.enableInstancing = true;
                            instanced++;
                        }
                    float size = r.bounds.size.magnitude;
                    string n = r.gameObject.name.ToLowerInvariant();
                    if (size < 4f && r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off &&
                        (n.Contains("bush") || n.Contains("grass") || n.Contains("flower") || n.Contains("plant") || n.Contains("fern") || n.Contains("weed") || n.Contains("shrub") || n.Contains("rock")))
                    {
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        shadowless++;
                    }
                }

            CourseSurvey.Stats stats = CourseSurvey.Inspect(scene);
            string result = $"course trees: {stats.TerrainTrees} terrain trees, {stats.TreeObjects} tree objects, trees drawn to {treeReach:0} m, {instanced} materials instanced, {shadowless} small plants without shadows";

            // 2. GolfSim ZA trees.
            int mode = AppSettings.Current.addTrees;
            bool plant = mode == 2 || (mode == 1 && stats.VisibleTrees < FewTrees);
            TreeField field = host.GetComponent<TreeField>();
            if (field == null) field = host.AddComponent<TreeField>();
            field.Clear();
            if (plant)
            {
                int planted = PlantCourse(field, course, AppSettings.Current.treeDensity, stats.Terrains > 0);
                result += $", {planted} GolfSim ZA trees planted";
            }
            field.Commit();

            // 3. Rough grass (GSPro's own grass add-on does not run outside GSPro).
            GolfSimZA.Visual.GrassField grass = GolfSimZA.Visual.GrassField.Ensure(host);
            grass.Exclude = CourseGrassExclusion(course);

            // 4. Sky, sun, shadows, picture.
            GolfSimZA.Visual.CloudDome.Ensure(camera);
            GolfSimZA.Visual.ColorGrade.Attach(camera);
            GolfSimZA.Visual.GraphicsQuality.ApplySceneLighting(camera);
            result += AppSettings.Current.roughGrass > 0 ? ", rough grass on" : ", rough grass off";
            return result;
        }

        /// <summary>Range: tree lines along both sides and a wood behind the range.</summary>
        public static void ImproveRange(GameObject host, Camera camera, float fairwayWidth)
        {
            TreeField field = host.GetComponent<TreeField>();
            if (field == null) field = host.AddComponent<TreeField>();
            field.Clear();
            if (AppSettings.Current.addTrees > 0)
            {
                var random = new System.Random(4242);
                float density = Mathf.Clamp(AppSettings.Current.treeDensity, 0.2f, 1f);
                float step = Mathf.Lerp(26f, 9f, density);
                for (float z = -30f; z < 430f; z += step)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float x = side * (fairwayWidth * 0.5f + 22f + (float)random.NextDouble() * 50f);
                        Plant(field, random, new Vector3(x, 0f, z + (float)(random.NextDouble() * 2 - 1) * step * 0.4f), 1.0f);
                    }
                for (float x = -260f; x <= 260f; x += step * 1.4f)
                    Plant(field, random, new Vector3(x, 0f, 440f + (float)random.NextDouble() * 70f), 1.2f);
            }
            field.Commit();

            // Rough grass beside the range fairway (never on the mown bands or the tee).
            float half = fairwayWidth * 0.5f + 1.5f;
            GolfSimZA.Visual.GrassField grass = GolfSimZA.Visual.GrassField.Ensure(host);
            grass.Exclude = p => Mathf.Abs(p.x) < half && p.z > -12f && p.z < RangeEnvironment.MaxTarget + 80f || (Mathf.Abs(p.x) < 4f && Mathf.Abs(p.z) < 4f);

            GolfSimZA.Visual.CloudDome.Ensure(camera);
            GolfSimZA.Visual.ColorGrade.Attach(camera);
            GolfSimZA.Visual.GraphicsQuality.ApplySceneLighting(camera);
        }

        /// <summary>Demo holes: grass off the fairway strip and green, sun and picture.</summary>
        public static void ImproveDemo(GameObject host, Camera camera)
        {
            GolfSimZA.Visual.GrassField.Ensure(host);
            GolfSimZA.Visual.CloudDome.Ensure(camera);
            GolfSimZA.Visual.ColorGrade.Attach(camera);
            GolfSimZA.Visual.GraphicsQuality.ApplySceneLighting(camera);
        }

        /// <summary>
        /// Safety margin for courses whose fairways are painted on the terrain with unnamed layers:
        /// no grass within 12 m of a hole's playing line, 20 m of a green or 8 m of a tee.
        /// </summary>
        private static System.Func<Vector3, bool> CourseGrassExclusion(CourseDefinition course)
        {
            var segments = new List<Vector3[]>();
            var greens = new List<Vector3>();
            var tees = new List<Vector3>();
            if (course != null && course.holes != null)
                foreach (HoleDefinition hole in course.holes)
                {
                    if (hole == null) continue;
                    Vector3 green = hole.GreenTarget;
                    greens.Add(green);
                    TeeDefinition back = null;
                    if (hole.tees != null)
                        foreach (TeeDefinition t in hole.tees)
                        {
                            tees.Add(t.position);
                            if (back == null || (t.position - green).sqrMagnitude > (back.position - green).sqrMagnitude) back = t;
                        }
                    var line = new List<Vector3>();
                    if (back != null) line.Add(back.position);
                    if (hole.aimPoints != null) line.AddRange(hole.aimPoints);
                    line.Add(green);
                    for (int i = 1; i < line.Count; i++) segments.Add(new[] { line[i - 1], line[i] });
                }
            return p =>
            {
                foreach (Vector3 g in greens) if (Flat(p - g) < 20f) return true;
                foreach (Vector3 t in tees) if (Flat(p - t) < 8f) return true;
                foreach (Vector3[] seg in segments) if (DistanceToSegment(p, seg[0], seg[1]) < 12f) return true;
                return false;
            };
        }

        // ------------------------------------------------------------ Planting

        private static int PlantCourse(TreeField field, CourseDefinition course, float density, bool terrainOnly)
        {
            if (course == null || course.holes == null || course.holes.Length == 0) return 0;
            density = Mathf.Clamp(density, 0.2f, 1f);
            var random = new System.Random(course.id != null ? course.id.GetHashCode() : 1);

            // Playing lines of every hole: each tee → aim points → green.
            var lines = new List<List<Vector3>>();
            var greens = new List<Vector3>();
            var teeSpots = new List<Vector3>();
            foreach (HoleDefinition hole in course.holes)
            {
                if (hole == null) continue;
                Vector3 green = hole.GreenTarget;
                greens.Add(green);
                var line = new List<Vector3>();
                TeeDefinition back = null;
                if (hole.tees != null)
                    foreach (TeeDefinition t in hole.tees)
                    {
                        teeSpots.Add(t.position);
                        if (back == null || (t.position - green).sqrMagnitude > (back.position - green).sqrMagnitude) back = t;
                    }
                if (back != null) line.Add(back.position);
                if (hole.aimPoints != null) line.AddRange(hole.aimPoints);
                line.Add(green);
                if (line.Count >= 2) lines.Add(line);
            }

            float step = Mathf.Lerp(34f, 12f, density);
            int planted = 0;
            foreach (List<Vector3> line in lines)
                for (int i = 1; i < line.Count; i++)
                {
                    Vector3 a = line[i - 1], b = line[i];
                    Vector3 dir = b - a;
                    dir.y = 0f;
                    float length = dir.magnitude;
                    if (length < 1f) continue;
                    dir /= length;
                    Vector3 side = new Vector3(dir.z, 0f, -dir.x);
                    for (float along = 0f; along < length; along += step)
                        for (int s = -1; s <= 1; s += 2)
                        {
                            if (random.NextDouble() > 0.85) continue; // natural gaps
                            float offset = ClearCorridor + 6f + (float)random.NextDouble() * 45f;
                            Vector3 p = a + dir * along + side * (s * offset) + new Vector3((float)(random.NextDouble() * 2 - 1) * 6f, 0f, (float)(random.NextDouble() * 2 - 1) * 6f);
                            if (!Allowed(p, lines, greens, teeSpots, course)) continue;
                            planted += Plant(field, random, p, 1f, terrainOnly);
                        }
                }
            return planted;
        }

        /// <summary>A small group of 1-4 trees around p (each checked for its own spot).</summary>
        private static int Plant(TreeField field, System.Random random, Vector3 p, float scale, bool terrainOnly = false)
        {
            int count = 1 + random.Next(4);
            int planted = 0;
            bool pines = random.NextDouble() < 0.5;
            for (int i = 0; i < count; i++)
            {
                Vector3 spot = p + new Vector3((float)(random.NextDouble() * 2 - 1) * 8f, 0f, (float)(random.NextDouble() * 2 - 1) * 8f);
                if (!GroundAt(spot, terrainOnly, out Vector3 ground)) continue;
                if (field.IsNear(ground, 4.5f)) continue;
                bool pine = random.NextDouble() < (pines ? 0.8 : 0.2);
                float height = (pine ? 13f + (float)random.NextDouble() * 12f : 9f + (float)random.NextDouble() * 8f) * scale;
                float width = pine ? 0.9f + (float)random.NextDouble() * 0.3f : 1.0f + (float)random.NextDouble() * 0.35f;
                field.Add(pine ? TreeField.Kind.Pine : TreeField.Kind.Broadleaf, ground, height, (float)random.NextDouble() * 360f, width, random.Next(3));
                planted++;
            }
            return planted;
        }

        private static bool GroundAt(Vector3 p, bool terrainOnly, out Vector3 ground)
        {
            ground = p;
            if (!UnityEngine.Physics.Raycast(new Vector3(p.x, p.y + 3000f, p.z), Vector3.down, out RaycastHit hit, 8000f, ~(1 << GroundProbe.IgnoreRaycastLayer), QueryTriggerInteraction.Ignore))
                return !terrainOnly; // flat range without colliders: stand on y = 0
            if (terrainOnly && !(hit.collider is TerrainCollider)) return false; // roofs, bridges, paths
            if (hit.normal.y < 0.8f) return false; // too steep
            ground = hit.point;
            return true;
        }

        private static bool Allowed(Vector3 p, List<List<Vector3>> lines, List<Vector3> greens, List<Vector3> tees, CourseDefinition course)
        {
            foreach (List<Vector3> line in lines)
                for (int i = 1; i < line.Count; i++)
                    if (DistanceToSegment(p, line[i - 1], line[i]) < ClearCorridor) return false;
            foreach (Vector3 g in greens) if (Flat(p - g) < 45f) return false;
            foreach (Vector3 t in tees) if (Flat(p - t) < 25f) return false;
            if (course.hazards != null)
                foreach (HazardDefinition h in course.hazards)
                    if (h != null && h.area != null && h.area.Contains(p)) return false;
            return true;
        }

        private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector2 P = new Vector2(p.x, p.z), A = new Vector2(a.x, a.z), B = new Vector2(b.x, b.z);
            Vector2 ab = B - A;
            float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(P - A, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(P, A + ab * t);
        }
    }
}
