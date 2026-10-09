using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfSimZA.Physics
{
    /// <summary>
    /// Is a collider water? A ball that reaches water goes in (splash) instead of bouncing or
    /// rolling on it. Mini game maps and courses GolfSimZA builds name their water "Map_Water...";
    /// imported (GSPro) courses use lakes, ponds and rivers with water materials and shaders
    /// (AQUAS, Lux Water, OPCD water, NatureManufacture rivers, GolfSimZA water) whose flat water
    /// plane often has a solid collider - so the ball skipped over it like glass.
    /// <see cref="Prepare"/> also gives water planes without a collider one, so the ball meets the
    /// surface instead of dropping through to the lake bed.
    /// </summary>
    public static class WaterSurface
    {
        private static readonly Dictionary<Collider, bool> Cache = new Dictionary<Collider, bool>();

        private static readonly string[] WaterWords = { "water", "lake", "pond", "river", "creek", "stream", "aquas", "ocean", "lagoon", "dam " };
        private static readonly string[] NotWater = { "fall", "splash", "fountain", "spray", "particle", "edge", "bank", "shore", "foam", "bed", "bottom", "wall", "rock", "stone", "bridge", "pipe", "sprinkler", "hazard_stake", "stake" };

        /// <summary>Forget cached colliders (a new course was loaded).</summary>
        public static void Reset() => Cache.Clear();

        public static bool IsWater(Collider c)
        {
            if (c == null || c is TerrainCollider) return false;
            if (c.name.StartsWith("Map_Water", StringComparison.Ordinal)) return true;
            if (Cache.TryGetValue(c, out bool water)) return water;
            water = false;
            Renderer r = c.GetComponent<Renderer>();
            if (r != null && r.sharedMaterial != null && LooksLikeWater(r.sharedMaterial.name + " " + (r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "")))
                water = true;
            else if (LooksLikeWater(c.name) || (c.transform.parent != null && LooksLikeWater(c.transform.parent.name) && IsFlat(c.bounds)))
                water = IsFlat(c.bounds);
            if (Cache.Count > 20000) Cache.Clear();
            Cache[c] = water;
            return water;
        }

        public static bool LooksLikeWater(string raw)
        {
            string n = (raw ?? "").ToLowerInvariant();
            bool any = false;
            foreach (string w in WaterWords) if (n.Contains(w)) { any = true; break; }
            if (!any) return false;
            foreach (string not in NotWater) if (n.Contains(not)) return false;
            return true;
        }

        /// <summary>A water surface is wide and flat (not a waterfall, fountain or wall).</summary>
        private static bool IsFlat(Bounds b) => b.size.y < Mathf.Max(3f, 0.25f * Mathf.Max(b.size.x, b.size.z)) && Mathf.Max(b.size.x, b.size.z) > 2f;

        /// <summary>
        /// Gives every water surface of a course a collider (a mesh collider on the water plane when it
        /// has none), so the ball meets the water. Returns the number of water surfaces found and how
        /// many colliders were added.
        /// </summary>
        public static string Prepare(Scene scene)
        {
            Reset();
            int found = 0, added = 0;
            if (!scene.IsValid()) return "no course scene";
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Material m = r.sharedMaterial;
                    string names = r.name + " " + (m != null ? m.name + " " + (m.shader != null ? m.shader.name : "") : "");
                    if (!LooksLikeWater(names) || !IsFlat(r.bounds)) continue;
                    found++;
                    if (r.GetComponent<Collider>() != null) continue;
                    MeshFilter f = r.GetComponent<MeshFilter>();
                    if (f == null || f.sharedMesh == null) continue;
                    try
                    {
                        var collider = r.gameObject.AddComponent<MeshCollider>();
                        collider.sharedMesh = f.sharedMesh;
                        Cache[collider] = true;
                        added++;
                    }
                    catch (Exception)
                    {
                        // A mesh that cannot be a collider (not readable): the ball uses the lake bed there.
                    }
                }
            return found + " water surfaces, " + added + " given a collider";
        }
    }
}
