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
        /// <summary>COURSE LOOK report: every shader each course uses, what it needs, lighting and missing GSPro scripts.</summary>
        public static string LookReportPath => Path.Combine(Application.persistentDataPath, "course-look.txt");

        /// <summary>Set by the editor test menu: run the check as soon as Play mode starts, then stop Play mode.</summary>
        public const string RunOnStartKey = "GolfSimZA.CourseSurveyOnStart";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (PlayerPrefs.GetInt(RunOnStartKey, 0) != 1) return;
            PlayerPrefs.DeleteKey(RunOnStartKey);
            PlayerPrefs.Save();
            Begin().stopPlayModeWhenDone = true;
        }

        private bool stopPlayModeWhenDone;

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

        // ------------------------------------------------------------ Course look

        /// <summary>One shader across all courses: how many courses and materials use it, and what it reads.</summary>
        private sealed class ShaderUse
        {
            public readonly HashSet<string> Courses = new HashSet<string>();
            public int Materials;
            public bool Supported;
            public readonly SortedSet<string> Textures = new SortedSet<string>();
            public readonly SortedSet<string> Keywords = new SortedSet<string>();
            public readonly List<string> Examples = new List<string>();
        }

        private readonly Dictionary<string, ShaderUse> shaderUses = new Dictionary<string, ShaderUse>();

        /// <summary>The course's look: shaders (as built, before GolfSim ZA repairs them), terrain, grass, water, lighting, GSPro scripts.</summary>
        private string InspectLook(Scene scene, string courseName)
        {
            var sb = new StringBuilder();
            var perShader = new SortedDictionary<string, int>();
            var done = new HashSet<Material>();
            int missingScripts = 0, reflectionProbes = 0, lights = 0, lodGroups = 0, particleSystems = 0, colliders = 0, meshColliders = 0, treeColliders = 0;
            var componentTypes = new SortedDictionary<string, int>();

            void Add(Material m, string where)
            {
                if (m == null || !done.Add(m)) return;
                string name = m.shader != null ? m.shader.name : "(none)";
                perShader[name] = perShader.TryGetValue(name, out int c) ? c + 1 : 1;
                if (!shaderUses.TryGetValue(name, out ShaderUse use)) shaderUses[name] = use = new ShaderUse();
                use.Courses.Add(courseName);
                use.Materials++;
                use.Supported = m.shader != null && m.shader.isSupported;
                try { foreach (string t in m.GetTexturePropertyNames()) if (m.GetTexture(t) != null) use.Textures.Add(t); } catch (Exception) { }
                foreach (string k in m.shaderKeywords) use.Keywords.Add(k);
                if (use.Examples.Count < 4) use.Examples.Add(courseName + ": " + m.name + " (" + where + ")");
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Component c in root.GetComponentsInChildren<Component>(true))
                {
                    if (c == null) { missingScripts++; continue; }
                    if (c is MonoBehaviour)
                    {
                        string tn = c.GetType().FullName;
                        componentTypes[tn] = componentTypes.TryGetValue(tn, out int n) ? n + 1 : 1;
                    }
                    if (c is ReflectionProbe) reflectionProbes++;
                    if (c is Light) lights++;
                    if (c is LODGroup) lodGroups++;
                    if (c is ParticleSystem) particleSystems++;
                    if (c is Collider col)
                    {
                        colliders++;
                        if (col is MeshCollider) meshColliders++;
                        if (LooksLikeTree(col.gameObject.name) || (col.transform.parent != null && LooksLikeTree(col.transform.parent.name))) treeColliders++;
                    }
                }
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                    foreach (Material m in r.sharedMaterials) Add(m, LooksLikeTree(r.gameObject.name) ? "tree object" : r.gameObject.name.ToLowerInvariant().Contains("water") ? "water" : "object");

                foreach (Terrain t in root.GetComponentsInChildren<Terrain>(true))
                {
                    TerrainData d = t.terrainData;
                    Add(t.materialTemplate, "terrain");
                    sb.AppendLine(string.Format("    terrain {0}: {1:0}x{2:0} m, heights {3}, layers {4}, detail layers {5}, trees {6} ({7} kinds), material {8}, draw trees {9}, collider {10}",
                        t.name, d != null ? d.size.x : 0f, d != null ? d.size.z : 0f, d != null ? d.heightmapResolution : 0, d != null && d.terrainLayers != null ? d.terrainLayers.Length : 0,
                        d != null ? d.detailPrototypes.Length : 0, d != null ? d.treeInstanceCount : 0, d != null ? d.treePrototypes.Length : 0,
                        t.materialTemplate != null && t.materialTemplate.shader != null ? t.materialTemplate.shader.name : "(Unity default)", t.drawTreesAndFoliage,
                        t.GetComponent<TerrainCollider>() != null));
                    if (d == null) continue;
                    if (d.terrainLayers != null)
                        foreach (TerrainLayer l in d.terrainLayers)
                            if (l != null) sb.AppendLine(string.Format("      layer {0}: diffuse {1}, normal {2}, tile {3:0.#} m", l.name, l.diffuseTexture != null ? l.diffuseTexture.name + " " + l.diffuseTexture.width + "px" : "-", l.normalMapTexture != null ? "yes" : "no", l.tileSize.x));
                    foreach (DetailPrototype p in d.detailPrototypes)
                        sb.AppendLine("      grass: " + (p.usePrototypeMesh ? "mesh " + (p.prototype != null ? p.prototype.name : "?") : "texture " + (p.prototypeTexture != null ? p.prototypeTexture.name : "?")) + ", " + p.renderMode);
                    foreach (TreePrototype p in d.treePrototypes)
                    {
                        if (p == null || p.prefab == null) continue;
                        bool hasCollider = p.prefab.GetComponentInChildren<Collider>(true) != null;
                        if (hasCollider) treeColliders++;
                        foreach (Renderer r in p.prefab.GetComponentsInChildren<Renderer>(true))
                            foreach (Material m in r.sharedMaterials) Add(m, "terrain tree " + p.prefab.name);
                    }
                }
            }

            // Lighting of the course scene (it is the active scene while it is played).
            Scene previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            sb.AppendLine(string.Format("    lighting: sky {0}, ambient {1}, fog {2} {3}, lightmaps {4}, reflection probes {5}, lights {6}",
                RenderSettings.skybox != null && RenderSettings.skybox.shader != null ? RenderSettings.skybox.shader.name : "none", RenderSettings.ambientMode,
                RenderSettings.fog ? "on" : "off", RenderSettings.fog ? RenderSettings.fogMode.ToString() : "", LightmapSettings.lightmaps.Length, reflectionProbes, lights));
            if (RenderSettings.skybox != null) Add(RenderSettings.skybox, "sky");
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);

            sb.AppendLine(string.Format("    objects: colliders {0} (mesh {1}, on trees {2}), LOD groups {3}, particle systems {4}, missing GSPro scripts {5}", colliders, meshColliders, treeColliders, lodGroups, particleSystems, missingScripts));
            if (componentTypes.Count > 0) sb.AppendLine("    scripts that still work: " + string.Join(", ", Pairs(componentTypes)));
            sb.AppendLine("    shaders: " + string.Join(", ", Pairs(perShader)));
            return sb.ToString();
        }

        private static IEnumerable<string> Pairs(IDictionary<string, int> d)
        {
            foreach (KeyValuePair<string, int> kv in d) yield return kv.Key + " x" + kv.Value;
        }

        private string ShaderSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("SHADERS USED BY THE COURSES (most courses first). 'runs here' = this Unity can draw it as it is.");
            var list = new List<KeyValuePair<string, ShaderUse>>(shaderUses);
            list.Sort((a, b) => b.Value.Courses.Count != a.Value.Courses.Count ? b.Value.Courses.Count.CompareTo(a.Value.Courses.Count) : b.Value.Materials.CompareTo(a.Value.Materials));
            foreach (KeyValuePair<string, ShaderUse> kv in list)
            {
                ShaderUse u = kv.Value;
                string builtin = ShaderRepair.IsBuiltinName(kv.Key) ? "Unity built-in" : "custom";
                sb.AppendLine(string.Format("  {0}  |  {1} courses, {2} materials  |  {3}, {4}  |  replaced by: {5}", kv.Key, u.Courses.Count, u.Materials, builtin, u.Supported ? "runs here" : "DOES NOT RUN HERE", ShaderRepair.ReplacementFor(kv.Key)));
                if (u.Textures.Count > 0) sb.AppendLine("      textures: " + string.Join(", ", u.Textures));
                if (u.Keywords.Count > 0) sb.AppendLine("      keywords: " + string.Join(", ", u.Keywords));
                foreach (string e in u.Examples) sb.AppendLine("      e.g. " + e);
            }
            return sb.ToString();
        }

        private IEnumerator Run()
        {
            var look = new StringBuilder();
            look.AppendLine("GolfSim ZA course look check  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "  (Unity " + Application.unityVersion + ")");
            look.AppendLine();
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
                if (course.IsOpenStreetMap)
                {
                    report.AppendLine(course.name + ":  built from OpenStreetMap (GolfSimZA ground, no course file to check)");
                    Done++;
                    continue;
                }
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
                    look.AppendLine(course.name + "  (built with Unity " + (string.IsNullOrEmpty(course.bundleUnityVersion) ? "?" : course.bundleUnityVersion) + ")");
                    try { look.Append(InspectLook(scene, course.name)); }
                    catch (Exception ex) { look.AppendLine("    look check failed: " + ex.Message); }
                    look.AppendLine();
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
            try { File.WriteAllText(LookReportPath, ShaderSummary() + "\n\nPER COURSE\n\n" + look); }
            catch (Exception ex) { Summary += " (look report not saved: " + ex.Message + ")"; }
            Debug.Log("[GolfSimZA] Course check: " + Summary + "\n" + report);
            Current = "";
            Finished = true;
            if (stopPlayModeWhenDone)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }
        }
    }
}
