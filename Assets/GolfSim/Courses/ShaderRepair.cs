using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfSimZA.Courses
{
    /// <summary>
    /// Course files built with an older Unity contain shaders this Unity version cannot
    /// run (they render pink). This swaps every material to a current built-in shader of
    /// the same name, or to the closest built-in shader, keeping textures and colours.
    /// All fallback shaders are added to "Always Included Shaders" by the release build.
    /// </summary>
    public static class ShaderRepair
    {
        public static readonly string[] FallbackShaders =
        {
            "Standard",
            "Standard (Specular setup)",
            "Nature/Terrain/Standard",
            "Nature/Terrain/Diffuse",
            "Nature/Terrain/Specular",
            "Nature/SpeedTree",
            "Nature/SpeedTree Billboard",
            "Nature/SpeedTree8",
            "Nature/Tree Soft Occlusion Bark",
            "Nature/Tree Soft Occlusion Leaves",
            "Legacy Shaders/Diffuse",
            "Legacy Shaders/Specular",
            "Legacy Shaders/Bumped Diffuse",
            "Legacy Shaders/Decal",
            "Legacy Shaders/Reflective/Specular",
            "Legacy Shaders/Reflective/VertexLit",
            "Legacy Shaders/Transparent/Diffuse",
            "Legacy Shaders/Transparent/Cutout/Diffuse",
            "Legacy Shaders/Transparent/Cutout/VertexLit",
            "Skybox/Procedural",
            "Skybox/6 Sided",
            "Skybox/Cubemap",
            "Unlit/Texture",
            "Unlit/Color",
            "Unlit/Transparent Cutout",
            "Sprites/Default"
        };

        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BumpMap = Shader.PropertyToID("_BumpMap");
        private static readonly int Cutoff = Shader.PropertyToID("_Cutoff");
        private static readonly int Glossiness = Shader.PropertyToID("_Glossiness");

        private static readonly Dictionary<string, Shader> Builtins = new Dictionary<string, Shader>(StringComparer.Ordinal);

        /// <summary>
        /// Must run BEFORE a course file is loaded: once the course's own (old) shaders are
        /// in memory, Shader.Find could return those instead of this Unity's versions.
        /// </summary>
        private static Shader leavesShader, waterShader;
        private static Texture2D rippleTexture;

        public static void CacheBuiltinShaders()
        {
            if (leavesShader == null) leavesShader = Resources.Load<Shader>("GolfSimZA_TreeLeaves");
            if (waterShader == null) waterShader = Resources.Load<Shader>("GolfSimZA_Water");
            foreach (string name in FallbackShaders)
            {
                if (Builtins.TryGetValue(name, out Shader cached) && cached != null) continue;
                Shader shader = Shader.Find(name);
                if (shader != null && shader.isSupported) Builtins[name] = shader;
            }
        }

        /// <summary>True when the shader name is one of Unity's own built-in shaders GolfSim ZA can use.</summary>
        public static bool IsBuiltinName(string name) => Array.IndexOf(FallbackShaders, name) >= 0;

        /// <summary>What a course shader of this name is drawn with in GolfSim ZA (for the course look report).</summary>
        public static string ReplacementFor(string shaderName)
        {
            if (IsBuiltinName(shaderName)) return "same shader (this Unity's version)";
            if (NeedsGolfSimReplacement(shaderName)) return PickFallbackName(shaderName, null);
            return "kept if it runs, else " + PickFallbackName(shaderName, null);
        }

        private static Shader Builtin(string name)
        {
            return !string.IsNullOrEmpty(name) && Builtins.TryGetValue(name, out Shader shader) && shader != null ? shader : null;
        }

        public struct Report
        {
            public int Materials;
            public int SameShader;
            public int Fallback;
            public int Failed;
            public override string ToString() => $"{Materials} materials: {SameShader} updated, {Fallback} replaced with a similar shader, {Failed} could not be fixed";
        }

        public static Report Repair(Scene scene)
        {
            var report = new Report();
            var done = new HashSet<Material>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                    foreach (Material material in renderer.sharedMaterials)
                        Fix(material, done, ref report);

                foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
                {
                    if (terrain.materialTemplate != null)
                        Fix(terrain.materialTemplate, done, ref report, "Nature/Terrain/Standard");

                    TerrainData data = terrain.terrainData;
                    if (data == null) continue;
                    foreach (TreePrototype tree in data.treePrototypes)
                    {
                        if (tree == null || tree.prefab == null) continue;
                        foreach (Renderer renderer in tree.prefab.GetComponentsInChildren<Renderer>(true))
                            foreach (Material material in renderer.sharedMaterials)
                                Fix(material, done, ref report);
                    }
                }
            }

            if (RenderSettings.skybox != null)
                Fix(RenderSettings.skybox, done, ref report, "Skybox/Procedural");

            return report;
        }

        private static void Fix(Material material, HashSet<Material> done, ref Report report, string preferred = null)
        {
            if (material == null || !done.Add(material)) return;
            report.Materials++;

            Shader current = material.shader;
            string name = current != null ? current.name : "";

            // 0. Water and tree leaves get GolfSim ZA's own shaders (the course's ones need the
            //    reflection / vegetation scripts of the program the course was made for).
            if (IsWaterShader(name) && waterShader != null && waterShader.isSupported)
            {
                SwapToWater(material);
                report.Fallback++;
                return;
            }
            if (NeedsGolfSimReplacement(name) && IsLeaf(name, material) && leavesShader != null && leavesShader.isSupported)
            {
                Swap(material, leavesShader);
                if (material.HasProperty("_Translucency")) material.SetFloat("_Translucency", 0.8f);
                report.Fallback++;
                return;
            }

            // 1. Same shader name compiled for this Unity version (Standard, Legacy, Nature...)
            Shader same = Builtin(name);
            if (same != null)
            {
                Swap(material, same);
                report.SameShader++;
                return;
            }

            // 2. The course's own custom shader happens to work in this Unity - keep it.
            //    (Not for GSPro's tree/vegetation shaders: they compile but draw nothing without
            //    GSPro's own wind and vegetation scripts, which leaves courses without trees.)
            if (current != null && current.isSupported && !IsErrorShader(current) && HasPasses(current) && !NeedsGolfSimReplacement(name))
            {
                report.SameShader++;
                return;
            }

            // 3. Closest built-in shader.
            Shader fallback = Builtin(preferred ?? PickFallbackName(name, material)) ?? Builtin("Standard");
            if (fallback != null)
            {
                Swap(material, fallback);
                report.Fallback++;
                return;
            }

            report.Failed++;
        }

        private static readonly string[] ReplaceShaders = { "arboreum", "vegetationstudio", "vegetation studio", "awesometechnologies", "ctree", "nature manufacture", "naturemanufacture", "treeleaves", "treebark", "cti ", "mtree" };

        /// <summary>Course water shaders (AQUAS, Lux Water, OPCD water, NatureManufacture rivers ...).</summary>
        public static bool IsWaterShader(string shaderName)
        {
            string n = (shaderName ?? "").ToLowerInvariant();
            return n.Contains("water") || n.Contains("aquas");
        }

        /// <summary>Leaves, fronds and leaf cards (not bark or branches).</summary>
        private static bool IsLeaf(string shaderName, Material material)
        {
            string n = (shaderName + " " + (material != null ? material.name : "")).ToLowerInvariant();
            if (n.Contains("bark") || n.Contains("trunk")) return false;
            return n.Contains("leaf") || n.Contains("leaves") || n.Contains("frond") || n.Contains("foliage") || n.Contains("needle")
                   || n.Contains("billboard") || n.Contains("cross model") || n.Contains("ivy") || n.Contains("crown");
        }

        private static readonly string[] NormalNames = { "_BumpMap", "_NormalTexture", "_Normal", "_NormalMap", "_WaveMap", "_WavesNormal", "_DetailNormalMap" };

        /// <summary>Course water drawn with the GolfSim ZA water shader, keeping its own ripple texture when it has one.</summary>
        private static void SwapToWater(Material material)
        {
            Texture ripples = null;
            foreach (string p in NormalNames)
                if (material.HasProperty(p) && material.GetTexture(p) != null) { ripples = material.GetTexture(p); break; }
            Color tint = new Color(0.06f, 0.20f, 0.24f, 1f);
            if (material.HasProperty(ColorId))
            {
                Color c = material.GetColor(ColorId);
                // A real water colour (not plain white / black) is kept, a little darker.
                if (c.maxColorComponent < 0.95f && c.maxColorComponent > 0.05f) tint = Color.Lerp(tint, c * 0.6f, 0.5f);
            }
            material.shader = waterShader;
            material.SetColor(ColorId, new Color(tint.r, tint.g, tint.b, 1f));
            material.SetTexture(BumpMap, ripples != null ? ripples : RippleTexture());
            material.renderQueue = -1;
        }

        /// <summary>A tiling ripple normal map made in code (for water that has none).</summary>
        public static Texture2D RippleTexture()
        {
            if (rippleTexture != null) return rippleTexture;
            const int S = 256;
            var heights = new float[S * S];
            var rnd = new System.Random(77);
            var waves = new Vector4[12];
            for (int i = 0; i < waves.Length; i++)
            {
                // Integer frequencies so the texture tiles seamlessly.
                waves[i] = new Vector4(rnd.Next(-6, 7), rnd.Next(-6, 7), (float)rnd.NextDouble() * 6.28f, 1f / (1 + i * 0.5f));
                if (waves[i].x == 0 && waves[i].y == 0) waves[i].x = 1 + i % 3;
            }
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float h = 0f;
                    foreach (Vector4 w in waves)
                        h += Mathf.Sin((w.x * x + w.y * y) / S * 6.2832f + w.z) * w.w;
                    heights[y * S + x] = h;
                }
            rippleTexture = new Texture2D(S, S, TextureFormat.RGBA32, true, true) { name = "GolfSimZA_Ripples", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = heights[y * S + (x + 1) % S] - heights[y * S + (x + S - 1) % S];
                    float dy = heights[((y + 1) % S) * S + x] - heights[((y + S - 1) % S) * S + x];
                    Vector3 n = new Vector3(-dx * 0.6f, -dy * 0.6f, 1f).normalized;
                    // UnpackNormal reads x as red x alpha and y as green: x in red with alpha 1.
                    byte nx = (byte)Mathf.Clamp((n.x * 0.5f + 0.5f) * 255f, 0, 255);
                    byte ny = (byte)Mathf.Clamp((n.y * 0.5f + 0.5f) * 255f, 0, 255);
                    byte nz = (byte)Mathf.Clamp((n.z * 0.5f + 0.5f) * 255f, 0, 255);
                    px[y * S + x] = new Color32(nx, ny, nz, 255);
                }
            rippleTexture.SetPixels32(px);
            rippleTexture.Apply(true);
            return rippleTexture;
        }

        /// <summary>Custom tree / vegetation shaders that only work inside GSPro.</summary>
        public static bool NeedsGolfSimReplacement(string shaderName)
        {
            string n = (shaderName ?? "").ToLowerInvariant().Replace("/", " ");
            foreach (string k in ReplaceShaders) if (n.Contains(k)) return true;
            return false;
        }

        private static bool HasPasses(Shader shader)
        {
            try { return shader.passCount > 0; } catch { return true; }
        }

        private static bool IsErrorShader(Shader shader)
        {
            return shader.name.IndexOf("InternalErrorShader", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string PickFallbackName(string original, Material material)
        {
            string n = (original + " " + (material != null ? material.name : "")).ToLowerInvariant();
            if (n.Contains("terrain")) return "Nature/Terrain/Standard";
            // Tree trunks and branches are solid; leaves (below) are cut out.
            if ((n.Contains("bark") || n.Contains("trunk") || n.Contains("branch_") || n.Contains("stem")) && !n.Contains("leaf") && !n.Contains("leaves")) return "Standard";
            if (n.Contains("skybox") || n.Contains("sky")) return "Skybox/Procedural";
            if (n.Contains("grass") || n.Contains("leaf") || n.Contains("leaves") || n.Contains("foliage") ||
                n.Contains("cutout") || n.Contains("billboard") || n.Contains("tree") || n.Contains("arboreum") ||
                n.Contains("speedtree") || n.Contains("bush") || n.Contains("plant") || n.Contains("fence"))
                return "Legacy Shaders/Transparent/Cutout/Diffuse";
            if (n.Contains("unlit")) return "Unlit/Texture";
            if (n.Contains("decal")) return "Legacy Shaders/Transparent/Diffuse";
            return "Standard";
        }

        private static void Swap(Material material, Shader shader)
        {
            Texture mainTexture = material.HasProperty(MainTex) ? material.GetTexture(MainTex) : null;
            Vector2 scale = material.HasProperty(MainTex) ? material.GetTextureScale(MainTex) : Vector2.one;
            Vector2 offset = material.HasProperty(MainTex) ? material.GetTextureOffset(MainTex) : Vector2.zero;
            Color color = material.HasProperty(ColorId) ? material.GetColor(ColorId) : Color.white;
            Texture normal = material.HasProperty(BumpMap) ? material.GetTexture(BumpMap) : null;
            bool isWater = (material.name + " " + (material.shader != null ? material.shader.name : "")).IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0;

            if (material.shader != shader)
                material.shader = shader;

            if (material.HasProperty(MainTex))
            {
                if (mainTexture != null) material.SetTexture(MainTex, mainTexture);
                material.SetTextureScale(MainTex, scale);
                material.SetTextureOffset(MainTex, offset);
            }
            if (material.HasProperty(ColorId)) material.SetColor(ColorId, color);
            if (normal != null && material.HasProperty(BumpMap))
            {
                material.SetTexture(BumpMap, normal);
                material.EnableKeyword("_NORMALMAP");
            }
            if (material.HasProperty(Cutoff) && material.GetFloat(Cutoff) <= 0f) material.SetFloat(Cutoff, 0.5f);
            if (isWater && material.HasProperty(Glossiness))
            {
                material.SetFloat(Glossiness, 0.92f);
                if (mainTexture == null && material.HasProperty(ColorId))
                    material.SetColor(ColorId, new Color(0.10f, 0.28f, 0.34f, 1f));
            }
        }
    }
}
