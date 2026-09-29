using System.Collections.Generic;
using UnityEngine;

namespace GolfSimZA.Physics
{
    /// <summary>
    /// Is a point on the putting green itself? Looks at the surface under the point: a course
    /// object named / textured "Green" (not the fringe, collar, apron or a greenside bunker), or
    /// terrain whose main ground texture there is a green texture.
    /// </summary>
    public static class GreenSurface
    {
        public enum Kind { Unknown, Green, NotGreen }

        private static readonly RaycastHit[] Hits = new RaycastHit[8];
        private static readonly Dictionary<Collider, Kind> ColliderKinds = new Dictionary<Collider, Kind>();
        private static readonly Dictionary<TerrainData, bool[]> TerrainGreenLayers = new Dictionary<TerrainData, bool[]>();

        /// <summary>Forget cached surfaces (a new course was loaded).</summary>
        public static void Reset()
        {
            ColliderKinds.Clear();
            TerrainGreenLayers.Clear();
        }

        public static Kind At(Vector3 p)
        {
            int count = UnityEngine.Physics.RaycastNonAlloc(new Vector3(p.x, p.y + 30f, p.z), Vector3.down, Hits, 200f, ~(1 << GroundProbe.IgnoreRaycastLayer), QueryTriggerInteraction.Ignore);
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (Hits[i].distance >= bestDistance || IsMarker(Hits[i].collider)) continue;
                bestDistance = Hits[i].distance;
                best = i;
            }
            if (best < 0) return Kind.Unknown;
            RaycastHit hit = Hits[best];

            if (hit.collider is TerrainCollider)
            {
                Terrain terrain = hit.collider.GetComponent<Terrain>();
                return terrain != null && TerrainIsGreen(terrain, hit.point) ? Kind.Green : Kind.NotGreen;
            }

            if (!ColliderKinds.TryGetValue(hit.collider, out Kind kind))
            {
                Renderer r = hit.collider.GetComponent<Renderer>();
                string name = hit.collider.gameObject.name + " " + (r != null && r.sharedMaterial != null ? r.sharedMaterial.name : "");
                kind = NameIsGreen(name) ? Kind.Green : Kind.NotGreen;
                ColliderKinds[hit.collider] = kind;
            }
            return kind;
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

        private static bool TerrainIsGreen(Terrain terrain, Vector3 point)
        {
            TerrainData data = terrain.terrainData;
            if (data == null || data.terrainLayers == null || data.terrainLayers.Length == 0) return false;
            if (!TerrainGreenLayers.TryGetValue(data, out bool[] green))
            {
                green = new bool[data.terrainLayers.Length];
                for (int i = 0; i < green.Length; i++)
                {
                    TerrainLayer l = data.terrainLayers[i];
                    green[i] = l != null && NameIsGreen(l.name + " " + (l.diffuseTexture != null ? l.diffuseTexture.name : ""));
                }
                TerrainGreenLayers[data] = green;
            }
            bool any = false;
            foreach (bool g in green) any |= g;
            if (!any) return false;
            Vector3 local = point - terrain.transform.position;
            int x = Mathf.Clamp(Mathf.FloorToInt(local.x / data.size.x * data.alphamapWidth), 0, data.alphamapWidth - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(local.z / data.size.z * data.alphamapHeight), 0, data.alphamapHeight - 1);
            float[,,] w = data.GetAlphamaps(x, z, 1, 1);
            float greenWeight = 0f;
            for (int l = 0; l < w.GetLength(2) && l < green.Length; l++) if (green[l]) greenWeight += w[0, 0, l];
            return greenWeight >= 0.5f;
        }
    }
}
