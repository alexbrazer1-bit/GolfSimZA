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
        public static void CacheBuiltinShaders()
        {
            foreach (string name in FallbackShaders)
            {
                if (Builtins.TryGetValue(name, out Shader cached) && cached != null) continue;
                Shader shader = Shader.Find(name);
                if (shader != null && shader.isSupported) Builtins[name] = shader;
            }
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

            // 1. Same shader name compiled for this Unity version (Standard, Legacy, Nature...)
            Shader same = Builtin(name);
            if (same != null)
            {
                Swap(material, same);
                report.SameShader++;
                return;
            }

            // 2. The course's own custom shader happens to work in this Unity - keep it.
            if (current != null && current.isSupported && !IsErrorShader(current) && HasPasses(current))
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
            string n = (original + " " + material.name).ToLowerInvariant();
            if (n.Contains("terrain")) return "Nature/Terrain/Standard";
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
