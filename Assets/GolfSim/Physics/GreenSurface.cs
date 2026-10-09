using System.Collections.Generic;
using UnityEngine;

namespace GolfSimZA.Physics
{
    /// <summary>
    /// What is under a point near the green? Looks at the surface under the point: a course object
    /// named / textured "Green", "Fringe" (collar, apron) or "Bunker" (sand, trap), terrain whose
    /// main ground texture there is one of those, or the surface map of a course GolfSimZA built.
    /// <see cref="At"/> answers only "green or not" (used by the ball physics).
    /// </summary>
    public static class GreenSurface
    {
        public enum Kind { Unknown, Green, NotGreen }

        /// <summary>The lie as far as putting is concerned.</summary>
        public enum Lie { Unknown, Green, Fringe, Bunker, Other }

        private static readonly RaycastHit[] Hits = new RaycastHit[8];
        private static readonly Dictionary<Collider, Lie> ColliderKinds = new Dictionary<Collider, Lie>();
        private static readonly Dictionary<TerrainData, Lie[]> TerrainLayerLies = new Dictionary<TerrainData, Lie[]>();

        /// <summary>Forget cached surfaces (a new course was loaded).</summary>
        public static void Reset()
        {
            ColliderKinds.Clear();
            TerrainLayerLies.Clear();
        }

        public static Kind At(Vector3 p)
        {
            Lie lie = LieAt(p);
            return lie == Lie.Unknown ? Kind.Unknown : lie == Lie.Green ? Kind.Green : Kind.NotGreen;
        }

        /// <summary>Green, fringe, bunker or something else under the point.</summary>
        public static Lie LieAt(Vector3 p)
        {
            // Courses GolfSimZA builds itself (OpenStreetMap) carry their own surface map.
            if (CourseSurfaceMap.Active)
            {
                int id = CourseSurfaceMap.At(p);
                if (id != CourseSurfaceMap.Unknown)
                    return id == CourseSurfaceMap.Green ? Lie.Green : id == CourseSurfaceMap.Fringe ? Lie.Fringe : id == CourseSurfaceMap.Bunker ? Lie.Bunker : Lie.Other;
            }
            int count = UnityEngine.Physics.RaycastNonAlloc(new Vector3(p.x, p.y + 30f, p.z), Vector3.down, Hits, 200f, ~(1 << GroundProbe.IgnoreRaycastLayer), QueryTriggerInteraction.Ignore);
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (Hits[i].distance >= bestDistance || IsMarker(Hits[i].collider)) continue;
                bestDistance = Hits[i].distance;
                best = i;
            }
            if (best < 0) return Lie.Unknown;
            RaycastHit hit = Hits[best];

            if (hit.collider is TerrainCollider)
            {
                Terrain terrain = hit.collider.GetComponent<Terrain>();
                return terrain != null ? TerrainLie(terrain, hit.point) : Lie.Other;
            }

            if (!ColliderKinds.TryGetValue(hit.collider, out Lie kind))
            {
                Renderer r = hit.collider.GetComponent<Renderer>();
                string name = hit.collider.gameObject.name + " " + (r != null && r.sharedMaterial != null ? r.sharedMaterial.name : "");
                kind = NameLie(name);
                ColliderKinds[hit.collider] = kind;
            }
            return kind;
        }

        /// <summary>A surface by its object / material / texture name.</summary>
        public static Lie NameLie(string raw)
        {
            string n = (raw ?? "").ToLowerInvariant();
            if (n.Contains("bunker") || n.Contains("sand") || n.Contains("trap")) return Lie.Bunker;
            if (n.Contains("fringe") || n.Contains("collar") || n.Contains("apron")) return Lie.Fringe;
            return NameIsGreen(n) ? Lie.Green : Lie.Other;
        }

        /// <summary>Flagsticks, cups, the ball and trees are not the surface.</summary>
        private static bool IsMarker(Collider c)
        {
            string n = c.gameObject.name.ToLowerInvariant();
            return n.Contains("pin") || n.Contains("flag") || n.Contains("cup") || n.Contains("ball") || n.Contains("tree") || n.Contains("marker");
        }

        public static bool NameIsGreen(string raw)
        {
            string n = (raw ?? "").ToLowerInvariant();
            if (!n.Contains("green")) return false;
            foreach (string not in new[] { "fringe", "collar", "apron", "bunker", "sand", "trap", "surround", "greenside", "semi", "rough", "fairway" })
                if (n.Contains(not)) return false;
            return true;
        }

        /// <summary>The terrain's main ground texture at the point: green, fringe, bunker sand or other.</summary>
        private static Lie TerrainLie(Terrain terrain, Vector3 point)
        {
            TerrainData data = terrain.terrainData;
            if (data == null || data.terrainLayers == null || data.terrainLayers.Length == 0) return Lie.Other;
            if (!TerrainLayerLies.TryGetValue(data, out Lie[] lies))
            {
                lies = new Lie[data.terrainLayers.Length];
                for (int i = 0; i < lies.Length; i++)
                {
                    TerrainLayer l = data.terrainLayers[i];
                    lies[i] = l == null ? Lie.Other : NameLie(l.name + " " + (l.diffuseTexture != null ? l.diffuseTexture.name : ""));
                }
                TerrainLayerLies[data] = lies;
            }
            bool any = false;
            foreach (Lie l in lies) any |= l != Lie.Other;
            if (!any) return Lie.Other;
            Vector3 local = point - terrain.transform.position;
            int x = Mathf.Clamp(Mathf.FloorToInt(local.x / data.size.x * data.alphamapWidth), 0, data.alphamapWidth - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(local.z / data.size.z * data.alphamapHeight), 0, data.alphamapHeight - 1);
            float[,,] w = data.GetAlphamaps(x, z, 1, 1);
            float green = 0f, fringe = 0f, bunker = 0f;
            for (int l = 0; l < w.GetLength(2) && l < lies.Length; l++)
            {
                if (lies[l] == Lie.Green) green += w[0, 0, l];
                else if (lies[l] == Lie.Fringe) fringe += w[0, 0, l];
                else if (lies[l] == Lie.Bunker) bunker += w[0, 0, l];
            }
            if (bunker >= 0.5f) return Lie.Bunker;
            if (green >= 0.5f) return Lie.Green;
            if (fringe >= 0.5f || fringe + green >= 0.5f) return Lie.Fringe;
            return Lie.Other;
        }
    }
}
