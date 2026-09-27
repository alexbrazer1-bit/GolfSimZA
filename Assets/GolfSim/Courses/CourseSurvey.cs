using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfSimZA.Courses
{
    /// <summary>
    /// CHECK ALL COURSES (Settings → GAME → SYSTEM): opens every imported course one after the
    /// other and writes a report of what each one contains - terrains, terrain trees, tree and
    /// bush objects, grass details, water, shaders that could not be repaired. The report is
    /// saved as course-report.txt next to settings.json and shown on screen.
    /// </summary>
    public sealed class CourseSurvey : MonoBehaviour
    {
        public static CourseSurvey Running { get; private set; }
        public static string LastReportPath => Path.Combine(Application.persistentDataPath, "course-report.txt");

        public int Done { get; private set; }
        public int Total { get; private set; }
        public string Current { get; private set; } = "";
        public bool Finished { get; private set; }
        public string Summary { get; private set; } = "";

        public static CourseSurvey Begin()
        {
            if (Running != null && !Running.Finished) return Running;
            var go = new GameObject("GolfSimZA_CourseSurvey");
            DontDestroyOnLoad(go);
            Running = go.AddComponent<CourseSurvey>();
            Running.StartCoroutine(Running.Run());
            return Running;
        }

        /// <summary>What a loaded course scene contains.</summary>
        public struct Stats
        {
            public int Terrains, TerrainTrees, TreePrototypes, DetailLayers, TerrainLayers;
            public int TreeObjects, Renderers, BrokenShaders, WaterObjects;
            public bool TreesHidden;
            public float TreeDistance;
            public string Prototypes;
            /// <summary>
            /// Trees GolfSim ZA will show: terrain trees (hidden ones are switched on) plus tree
            /// objects (each tree usually has about three detail levels, so objects / 3).
            /// </summary>
            public int VisibleTrees => TerrainTrees + TreeObjects / 3;
        }

        private static readonly string[] TreeWords = { "tree", "pine", "oak", "palm", "birch", "maple", "fir", "spruce", "willow", "conifer", "cedar", "cypress", "bush", "shrub", "hedge", "forest", "speedtree", "foliage" };

        public static bool LooksLikeTree(string name)
        {
            string n = (name ?? "").ToLowerInvariant();
            foreach (string w in TreeWords) if (n.Contains(w)) return true;
            return false;
        }

        public static Stats Inspect(Scene scene)
        {
            var s = new Stats();
            var protoNames = new List<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Terrain t in root.GetComponentsInChildren<Terrain>(true))
                {
                    s.Terrains++;
                    TerrainData d = t.terrainData;
                    if (d == null) continue;
                    s.TerrainTrees += d.treeInstanceCount;
                    s.TreePrototypes += d.treePrototypes.Length;
                    s.DetailLayers += d.detailPrototypes.Length;
                    s.TerrainLayers += d.terrainLayers != null ? d.terrainLayers.Length : 0;
                    if (!t.drawTreesAndFoliage && d.treeInstanceCount > 0) s.TreesHidden = true;
                    s.TreeDistance = Mathf.Max(s.TreeDistance, t.treeDistance);
                    foreach (TreePrototype p in d.treePrototypes)
                        if (p != null && p.prefab != null && protoNames.Count < 8) protoNames.Add(p.prefab.name);
                }
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    s.Renderers++;
                    string n = r.gameObject.name;
                    if (LooksLikeTree(n) || (r.transform.parent != null && LooksLikeTree(r.transform.parent.name))) s.TreeObjects++;
                    if (n.ToLowerInvariant().Contains("water")) s.WaterObjects++;
                    foreach (Material m in r.sharedMaterials)
                        if (m != null && (m.shader == null || !m.shader.isSupported || m.shader.name.Contains("InternalError"))) { s.BrokenShaders++; break; }
                }
            }
            s.Prototypes = string.Join(", ", protoNames);
            return s;
        }

        private IEnumerator Run()
        {
            var report = new StringBuilder();
            report.AppendLine("GolfSim ZA course check  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            report.AppendLine();
            IReadOnlyList<CourseDefinition> courses = CourseLibrary.All;
            Total = courses.Count;
            int withTrees = 0, withoutTrees = 0, failed = 0;
            ShaderRepair.CacheBuiltinShaders();

            foreach (CourseDefinition course in courses)
            {
                Current = course.name;
                if (!CourseLibrary.IsAvailable(course))
                {
                    report.AppendLine(course.name + ":  FILE MISSING (" + course.bundleFile + ")");
                    failed++;
                    Done++;
                    continue;
                }

                AssetBundleCreateRequest br = AssetBundle.LoadFromFileAsync(course.bundleFile);
                yield return br;
                AssetBundle bundle = br.assetBundle;
                string[] scenes = bundle != null ? bundle.GetAllScenePaths() : null;
                if (bundle == null || scenes == null || scenes.Length == 0)
                {
                    report.AppendLine(course.name + ":  COULD NOT OPEN");
                    if (bundle != null) bundle.Unload(true);
                    failed++;
                    Done++;
                    continue;
                }

                AsyncOperation load = SceneManager.LoadSceneAsync(scenes[0], LoadSceneMode.Additive);
                yield return load;
                Scene scene = SceneManager.GetSceneByPath(scenes[0]);
                if (!scene.IsValid()) scene = SceneManager.GetSceneByName(Path.GetFileNameWithoutExtension(scenes[0]));

                if (scene.IsValid() && scene.isLoaded)
                {
                    // Keep the course from drawing or making sound while it is checked.
                    foreach (GameObject root in scene.GetRootGameObjects()) root.SetActive(false);
                    Stats st = Inspect(scene);
                    bool trees = st.VisibleTrees >= CourseScenery.FewTrees;
                    if (trees) withTrees++; else withoutTrees++;
                    report.AppendLine(string.Format("{0}:  {1}  |  terrains {2}, terrain trees {3} ({4} kinds{5}), tree/bush objects {6}, grass layers {7}, ground textures {8}, water objects {9}, objects {10}, unfixable shaders {11}{12}",
                        course.name, trees ? "HAS TREES" : st.VisibleTrees > 0 ? "FEW TREES (about " + st.VisibleTrees + ")" : "NO TREES", st.Terrains, st.TerrainTrees, st.TreePrototypes, st.TreesHidden ? ", hidden by the course file - GolfSim ZA shows them" : "",
                        st.TreeObjects, st.DetailLayers, st.TerrainLayers, st.WaterObjects, st.Renderers, st.BrokenShaders,
                        string.IsNullOrEmpty(st.Prototypes) ? "" : "  |  tree kinds: " + st.Prototypes));
                    yield return SceneManager.UnloadSceneAsync(scene);
                }
                else
                {
                    report.AppendLine(course.name + ":  SCENE DID NOT LOAD");
                    failed++;
                }
                bundle.Unload(true);
                yield return Resources.UnloadUnusedAssets();
                Done++;
            }

            Summary = $"{Total} courses: {withTrees} with trees, {withoutTrees} with no or few trees (GolfSim ZA adds trees), {failed} could not be checked.";
            report.AppendLine();
            report.AppendLine(Summary);
            report.AppendLine("Courses without trees get GolfSim ZA trees in the rough when Settings → VISUAL SETTINGS → COURSE LOOK → ADD TREES is on.");
            try { File.WriteAllText(LastReportPath, report.ToString()); }
            catch (Exception ex) { Summary += " (report not saved: " + ex.Message + ")"; }
            Debug.Log("[GolfSimZA] Course check: " + Summary + "\n" + report);
            Current = "";
            Finished = true;
        }
    }
}
