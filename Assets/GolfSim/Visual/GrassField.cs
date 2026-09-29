using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using GolfSimZA.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace GolfSimZA.Visual
{
    /// <summary>
    /// ROUGH GRASS (Settings → VISUAL SETTINGS → COURSE LOOK): 3D grass tufts in the rough around
    /// the camera, like GSPro's course grass (whose grass add-on does not run outside GSPro).
    /// Grass grows only on the course ground (terrain or rough objects) - never on fairways,
    /// greens, tees, bunkers, paths or water - is tinted to the colour of the ground under it,
    /// sways in the wind and fades out at the grass distance. Built in 8 m squares around the
    /// camera a few at a time, so it follows the camera during flyovers and ball flights.
    /// </summary>
    public sealed class GrassField : MonoBehaviour
    {
        private const float Chunk = 8f;
        private const float NearLod = 32f;

        /// <summary>Extra "no grass here" rule (hole corridors, range fairway). Null = none.</summary>
        public Func<Vector3, bool> Exclude;

        /// <summary>Colliders whose object name contains one of these may carry grass (besides terrains).</summary>
        public string[] AllowObjects = { "rough", "grass", "courseground", "range_ground", "ground", "terrain" };

        private static readonly string[] NoGrass =
        {
            "fairway", "green", "fringe", "collar", "apron", "tee", "bunker", "sand", "trap", "path", "cart",
            "road", "water", "lake", "pond", "river", "creek", "gravel", "concrete", "asphalt", "tarmac", "dirt",
            "rock", "stone", "mulch", "bark", "wood", "deck", "bridge", "building", "house", "roof", "parking",
            "pavement", "brick", "tile", "cliff", "mud", "snow", "beach"
        };

        private sealed class ChunkData
        {
            public Mesh Mesh;
            public int Lod;
        }

        private readonly Dictionary<Vector2Int, ChunkData> chunks = new Dictionary<Vector2Int, ChunkData>();
        private readonly List<Vector2Int> wanted = new List<Vector2Int>();
        private readonly List<Vector2Int> drop = new List<Vector2Int>();
        private readonly Dictionary<Terrain, TerrainInfo> terrains = new Dictionary<Terrain, TerrainInfo>();
        private readonly Dictionary<Texture, Color> textureColours = new Dictionary<Texture, Color>();
        private readonly Dictionary<Collider, bool> colliderAllowed = new Dictionary<Collider, bool>();
        private static Material material;
        private static Texture2D bladeTexture;
        private float lastDensity = -1f, lastDistance = -1f;
        private bool reported;
        private int tuftsBuilt, rejectedSurface, rejectedExclude;
        private readonly Dictionary<string, int> hitNames = new Dictionary<string, int>();

        private sealed class TerrainInfo
        {
            public bool[] Allowed;
            public Color[] Colour;
            public string Names;
        }

        public static GrassField Ensure(GameObject host)
        {
            GrassField field = host.GetComponent<GrassField>();
            if (field == null) field = host.AddComponent<GrassField>();
            field.Clear();
            return field;
        }

        public void Clear()
        {
            foreach (ChunkData c in chunks.Values) if (c.Mesh != null) Destroy(c.Mesh);
            chunks.Clear();
            terrains.Clear();
            colliderAllowed.Clear();
            hitNames.Clear();
            reported = false;
            tuftsBuilt = rejectedSurface = rejectedExclude = 0;
        }

        private void OnDisable() => Clear();

        private void LateUpdate()
        {
            AppSettings s = AppSettings.Current;
            if (s.roughGrass == 0)
            {
                if (chunks.Count > 0) Clear();
                return;
            }
            Camera cam = Camera.main;
            if (cam == null) return;
            if (!EnsureMaterial()) return;

            float density = Mathf.Clamp(s.grassDensity, 0.2f, 1f);
            float distance = Mathf.Clamp(s.grassDistance, 30f, 150f);
            if (!Mathf.Approximately(density, lastDensity) || !Mathf.Approximately(distance, lastDistance))
            {
                foreach (ChunkData c in chunks.Values) if (c.Mesh != null) Destroy(c.Mesh);
                chunks.Clear();
                lastDensity = density;
                lastDistance = distance;
            }
            material.SetFloat("_FadeStart", distance * 0.72f);
            material.SetFloat("_FadeEnd", distance);
            material.SetFloat("_WindStrength", 0.10f + Mathf.Clamp(Wind.SpeedMps, 0f, 12f) * 0.015f);

            Vector3 camPos = cam.transform.position;
            Vector2Int centre = new Vector2Int(Mathf.FloorToInt(camPos.x / Chunk), Mathf.FloorToInt(camPos.z / Chunk));
            int reach = Mathf.CeilToInt(distance / Chunk) + 1;

            // Drop squares that are out of range.
            drop.Clear();
            foreach (KeyValuePair<Vector2Int, ChunkData> kv in chunks)
                if (FlatDistance(kv.Key, camPos) > distance + Chunk * 2f) drop.Add(kv.Key);
            foreach (Vector2Int k in drop)
            {
                if (chunks[k].Mesh != null) Destroy(chunks[k].Mesh);
                chunks.Remove(k);
            }

            // Missing (or wrong detail level) squares, nearest first.
            wanted.Clear();
            for (int x = -reach; x <= reach; x++)
                for (int z = -reach; z <= reach; z++)
                {
                    var key = new Vector2Int(centre.x + x, centre.y + z);
                    float d = FlatDistance(key, camPos);
                    if (d > distance + Chunk) continue;
                    int lod = d < NearLod ? 0 : 1;
                    if (chunks.TryGetValue(key, out ChunkData existing) && existing.Lod <= lod) continue;
                    wanted.Add(key);
                }
            wanted.Sort((a, b) => FlatDistance(a, camPos).CompareTo(FlatDistance(b, camPos)));

            var watch = Stopwatch.StartNew();
            foreach (Vector2Int key in wanted)
            {
                if (watch.Elapsed.TotalMilliseconds > 3.0) break;
                int lod = FlatDistance(key, camPos) < NearLod ? 0 : 1;
                if (chunks.TryGetValue(key, out ChunkData old) && old.Mesh != null) Destroy(old.Mesh);
                chunks[key] = new ChunkData { Mesh = Build(key, density * (lod == 0 ? 1f : 0.45f), lod), Lod = lod };
            }

            if (!reported && wanted.Count == 0 && chunks.Count > 0) Report();

            Matrix4x4 identity = Matrix4x4.identity;
            foreach (ChunkData c in chunks.Values)
                if (c.Mesh != null)
                    Graphics.DrawMesh(c.Mesh, identity, material, 0, null, 0, null, ShadowCastingMode.Off, true);
        }

        private static float FlatDistance(Vector2Int key, Vector3 p)
        {
            float cx = (key.x + 0.5f) * Chunk - p.x, cz = (key.y + 0.5f) * Chunk - p.z;
            return Mathf.Sqrt(cx * cx + cz * cz);
        }

        // ------------------------------------------------------------ Building

        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<Vector2> uv2s = new List<Vector2>();
        private readonly List<Color> colours = new List<Color>();
        private readonly List<int> triangles = new List<int>();

        private Mesh Build(Vector2Int key, float density, int lod)
        {
            vertices.Clear(); uvs.Clear(); uv2s.Clear(); colours.Clear(); triangles.Clear();
            var random = new System.Random(key.x * 73856093 ^ key.y * 19349663);
            // Tufts per square metre: thick rough ~1.6 at full density.
            float perSquareMetre = 3.2f * density;
            int count = Mathf.RoundToInt(perSquareMetre * Chunk * Chunk);
            int mask = ~(1 << GolfSimZA.Physics.GroundProbe.IgnoreRaycastLayer);
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;

            for (int i = 0; i < count; i++)
            {
                float x = (key.x + (float)random.NextDouble()) * Chunk;
                float z = (key.y + (float)random.NextDouble()) * Chunk;
                double sizeRoll = random.NextDouble(), yawRoll = random.NextDouble(), colourRoll = random.NextDouble(), variant = random.NextDouble();
                if (!UnityEngine.Physics.Raycast(new Vector3(x, 5000f, z), Vector3.down, out RaycastHit hit, 10000f, mask, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y < 0.55f) continue;
                if (!SurfaceAllows(hit, out Color groundColour, out float heightScale))
                {
                    rejectedSurface++;
                    continue;
                }
                // The safety corridor is only for ground whose rough is not marked as such
                // (terrain paint, unnamed objects); course objects named "...Rough" always get grass.
                bool namedRough = !(hit.collider is TerrainCollider) && hit.collider.gameObject.name.IndexOf("rough", StringComparison.OrdinalIgnoreCase) >= 0;
                if (Exclude != null && !namedRough && Exclude(hit.point))
                {
                    rejectedExclude++;
                    continue;
                }

                // Size: mostly ankle-high rough, a few taller clumps. Far squares get bigger, fewer tufts.
                float height = Mathf.Lerp(0.13f, 0.30f, (float)(sizeRoll * sizeRoll)) * heightScale * (lod == 0 ? 1f : 1.2f);
                float width = height * (1.4f + (float)variant * 0.5f) * (lod == 0 ? 1f : 1.3f);

                // Colour: GolfSim ZA rough green blended with the ground colour, with natural variation.
                Color baseColour = RoughColour(colourRoll);
                Color c = groundColour.a > 0f ? Color.Lerp(baseColour, groundColour, 0.8f) : baseColour;
                // Small random brightness change per tuft so the rough is not one flat colour.
                c *= 0.88f + (float)variant * 0.2f;
                c.a = 1f;
                if (linear) c = c.linear;
                AddTuft(hit.point, height, width, (float)yawRoll * 180f, variant < 0.5 ? 0f : 0.5f, c, (float)variant * 6.28f);
                tuftsBuilt++;
            }

            if (vertices.Count == 0) return null;
            var mesh = new Mesh { name = "GolfSimZA_Grass_" + key, indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, uv2s);
            mesh.SetColors(colours);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            Bounds b = mesh.bounds;
            b.Expand(new Vector3(1f, 1f, 1f));
            mesh.bounds = b;
            mesh.UploadMeshData(true);
            return mesh;
        }

        private static Color RoughColour(double roll)
        {
            if (roll < 0.40) return new Color(0.24f, 0.36f, 0.12f);
            if (roll < 0.72) return new Color(0.20f, 0.32f, 0.10f);
            if (roll < 0.95) return new Color(0.28f, 0.40f, 0.14f);
            return new Color(0.38f, 0.41f, 0.18f); // a few drier tufts
        }

        /// <summary>Three crossed quads (a star seen from above).</summary>
        private void AddTuft(Vector3 p, float height, float width, float yaw, float u0, Color colour, float phase)
        {
            for (int q = 0; q < 3; q++)
            {
                float a = (yaw + q * 60f) * Mathf.Deg2Rad;
                Vector3 half = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (width * 0.5f);
                Vector3 bottom = p - Vector3.up * 0.03f;
                Vector3 top = bottom + Vector3.up * height;
                int start = vertices.Count;
                vertices.Add(bottom - half); vertices.Add(bottom + half); vertices.Add(top + half); vertices.Add(top - half);
                uvs.Add(new Vector2(u0, 0f)); uvs.Add(new Vector2(u0 + 0.5f, 0f)); uvs.Add(new Vector2(u0 + 0.5f, 1f)); uvs.Add(new Vector2(u0, 1f));
                var info = new Vector2(height, phase);
                uv2s.Add(info); uv2s.Add(info); uv2s.Add(info); uv2s.Add(info);
                colours.Add(colour); colours.Add(colour); colours.Add(colour); colours.Add(colour);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
            }
        }

        // ------------------------------------------------------------ Where grass grows

        private bool SurfaceAllows(RaycastHit hit, out Color groundColour, out float heightScale)
        {
            groundColour = new Color(0f, 0f, 0f, 0f);
            heightScale = 1f;
            Collider col = hit.collider;
            if (col is TerrainCollider tc)
            {
                Count("terrain:" + col.name);
                Terrain terrain = tc.GetComponent<Terrain>();
                if (terrain == null || terrain.terrainData == null) return true;
                TerrainInfo info = InfoFor(terrain);
                if (info.Allowed == null || info.Allowed.Length == 0) return true;
                TerrainData data = terrain.terrainData;
                Vector3 local = hit.point - terrain.transform.position;
                int ax = Mathf.Clamp(Mathf.FloorToInt(local.x / data.size.x * data.alphamapWidth), 0, data.alphamapWidth - 1);
                int az = Mathf.Clamp(Mathf.FloorToInt(local.z / data.size.z * data.alphamapHeight), 0, data.alphamapHeight - 1);
                float[,,] w = data.GetAlphamaps(ax, az, 1, 1);
                int best = 0;
                float bestWeight = -1f, blocked = 0f;
                for (int l = 0; l < w.GetLength(2) && l < info.Allowed.Length; l++)
                {
                    if (w[0, 0, l] > bestWeight) { bestWeight = w[0, 0, l]; best = l; }
                    if (!info.Allowed[l]) blocked += w[0, 0, l];
                }
                if (blocked > 0.35f) return false;
                groundColour = info.Colour[best];
                return true;
            }

            Count("object:" + col.name);
            string lower = col.gameObject.name.ToLowerInvariant();
            // Semi-rough / first cut beside the fairway: shorter grass.
            if (lower.Contains("deeprough") || lower.Contains("deep_rough") || lower.Contains("fescue")) heightScale = 1.35f;
            if (lower.Contains("semi") || lower.Contains("firstcut") || lower.Contains("first_cut") || lower.Contains("intermediate")) heightScale = 0.55f;
            if (!colliderAllowed.TryGetValue(col, out bool allowed))
            {
                string n = col.gameObject.name.ToLowerInvariant();
                Renderer r = col.GetComponent<Renderer>();
                string m = r != null && r.sharedMaterial != null ? r.sharedMaterial.name.ToLowerInvariant() : "";
                allowed = false;
                foreach (string a in AllowObjects)
                    if (n.Contains(a) || m.Contains(a)) { allowed = true; break; }
                if (allowed && (Blocked(n) || Blocked(m))) allowed = false;
                colliderAllowed[col] = allowed;
            }
            if (allowed)
            {
                Renderer r = col.GetComponent<Renderer>();
                if (r != null && r.sharedMaterial != null)
                {
                    Material mat = r.sharedMaterial;
                    Color tint = mat.HasProperty("_Color") ? mat.color : Color.white;
                    Texture tex = mat.HasProperty("_MainTex") ? mat.mainTexture : null;
                    Color avg = tex != null ? AverageColour(tex) : new Color(0f, 0f, 0f, 0f);
                    if (avg.a > 0f) groundColour = Tame(avg * tint);
                }
            }
            return allowed;
        }

        private void Count(string name)
        {
            if (reported) return;
            hitNames.TryGetValue(name, out int n);
            hitNames[name] = n + 1;
        }

        /// <summary>
        /// True for fairway / green / bunker / path ... names. "Rough" always allows grass, and a
        /// generic grass texture called e.g. "GrassGreen01" is not taken for a putting green.
        /// </summary>
        private static bool Blocked(string name)
        {
            if (name.Contains("rough") && !name.Contains("sand") && !name.Contains("bunker") && !name.Contains("water")) return false;
            bool grassWord = name.Contains("grass");
            foreach (string b in NoGrass)
            {
                if (!name.Contains(b)) continue;
                if (grassWord && b == "green" && !name.Contains("putting")) continue;
                return true;
            }
            return false;
        }

        private TerrainInfo InfoFor(Terrain terrain)
        {
            if (terrains.TryGetValue(terrain, out TerrainInfo info)) return info;
            info = new TerrainInfo();
            TerrainLayer[] layers = terrain.terrainData.terrainLayers;
            var names = new StringBuilder();
            if (layers != null)
            {
                info.Allowed = new bool[layers.Length];
                info.Colour = new Color[layers.Length];
                for (int i = 0; i < layers.Length; i++)
                {
                    TerrainLayer layer = layers[i];
                    string n = layer == null ? "" : (layer.name + " " + (layer.diffuseTexture != null ? layer.diffuseTexture.name : "")).ToLowerInvariant();
                    info.Allowed[i] = layer != null && !Blocked(n);
                    Color avg = layer != null && layer.diffuseTexture != null ? AverageColour(layer.diffuseTexture) : new Color(0f, 0f, 0f, 0f);
                    if (avg.a > 0f && layer != null)
                    {
                        Vector4 remap = layer.diffuseRemapMax;
                        if (remap.x > 0f || remap.y > 0f || remap.z > 0f) avg = new Color(avg.r * remap.x, avg.g * remap.y, avg.b * remap.z, 1f);
                        avg = Tame(avg);
                    }
                    info.Colour[i] = avg;
                    names.Append(i).Append('=').Append(layer != null ? layer.name : "(none)").Append(info.Allowed[i] ? " [grass]" : " [no grass]").Append("; ");
                }
            }
            info.Names = names.ToString();
            terrains[terrain] = info;
            return info;
        }

        /// <summary>Ground colours of dirt / brown textures are pulled back towards green.</summary>
        private static Color Tame(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            if (h < 0.12f || h > 0.45f) h = Mathf.Lerp(h, 0.24f, 0.7f);
            h = Mathf.Clamp(h, 0.14f, 0.36f);
            s = Mathf.Clamp(s * 1.1f, 0.45f, 0.80f);
            v = Mathf.Clamp(v * 1.1f, 0.30f, 0.54f);
            Color o = Color.HSVToRGB(h, s, v);
            o.a = 1f;
            return o;
        }

        /// <summary>Average colour of a texture (works for course textures that cannot be read directly).</summary>
        private Color AverageColour(Texture texture)
        {
            if (textureColours.TryGetValue(texture, out Color cached)) return cached;
            Color result = new Color(0f, 0f, 0f, 0f);
            RenderTexture previous = RenderTexture.active;
            RenderTexture rt = RenderTexture.GetTemporary(8, 8, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            try
            {
                Graphics.Blit(texture, rt);
                RenderTexture.active = rt;
                var read = new Texture2D(8, 8, TextureFormat.RGBA32, false, false);
                read.ReadPixels(new Rect(0, 0, 8, 8), 0, 0, false);
                read.Apply(false);
                Color sum = Color.black;
                foreach (Color p in read.GetPixels()) sum += p;
                Destroy(read);
                result = sum / 64f;
                result.a = 1f;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GolfSimZA] Grass colour from " + texture.name + " failed: " + ex.Message);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
            textureColours[texture] = result;
            return result;
        }

        private void Report()
        {
            reported = true;
            var sb = new StringBuilder("[GolfSimZA] Rough grass: ");
            sb.Append(tuftsBuilt).Append(" tufts in ").Append(chunks.Count).Append(" squares; skipped ").Append(rejectedSurface)
              .Append(" (fairway/green/bunker/path/water/objects) and ").Append(rejectedExclude).Append(" (hole corridor). Ground: ");
            foreach (KeyValuePair<Terrain, TerrainInfo> t in terrains) sb.Append(t.Key.name).Append(" {").Append(t.Value.Names).Append("} ");
            sb.Append(" Hits: ");
            int shown = 0;
            foreach (KeyValuePair<string, int> kv in hitNames)
            {
                if (shown++ > 25) break;
                sb.Append(kv.Key).Append(" x").Append(kv.Value).Append(", ");
            }
            Debug.Log(sb.ToString());
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(Application.persistentDataPath, "grass-report.txt"), sb.ToString()); }
            catch (Exception) { /* report only */ }
        }

        // ------------------------------------------------------------ Assets

        private static bool EnsureMaterial()
        {
            if (material != null) return true;
            Shader shader = Resources.Load<Shader>("GolfSimZA_Grass");
            if (shader == null || !shader.isSupported) shader = Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse");
            if (shader == null) return false;
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, mainTexture = BladeTexture() };
            if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 0.45f);
            return true;
        }

        /// <summary>Two tufts of blades side by side (u 0..0.5 and 0.5..1), drawn in code.</summary>
        private static Texture2D BladeTexture()
        {
            if (bladeTexture != null) return bladeTexture;
            const int W = 256, H = 128;
            var pixels = new Color[W * H];
            var random = new System.Random(99);
            for (int half = 0; half < 2; half++)
            {
                int blades = 22;
                for (int b = 0; b < blades; b++)
                {
                    float baseX = half * 128 + 8 + (float)random.NextDouble() * 112f;
                    float top = 0.45f + (float)random.NextDouble() * 0.55f;
                    float lean = ((float)random.NextDouble() * 2f - 1f) * 26f;
                    float widthPx = 2.2f + (float)random.NextDouble() * 2.6f;
                    float shade = 0.75f + (float)random.NextDouble() * 0.25f;
                    for (int y = 0; y < H; y++)
                    {
                        float v = y / (float)(H - 1);
                        if (v > top) break;
                        float along = v / top;
                        float cx = baseX + lean * along * along;
                        float halfWidth = widthPx * (1f - along) + 0.35f;
                        for (int x = Mathf.FloorToInt(cx - halfWidth - 1); x <= Mathf.CeilToInt(cx + halfWidth + 1); x++)
                        {
                            if (x < half * 128 || x >= half * 128 + 128) continue;
                            float cover = Mathf.Clamp01(halfWidth + 0.5f - Mathf.Abs(x + 0.5f - cx));
                            if (cover <= 0f) continue;
                            // Darker at the base, lighter yellow-green towards the tip; a centre vein.
                            float light = Mathf.Lerp(0.70f, 1.05f, along) * shade * (1f - 0.10f * Mathf.Clamp01(1f - Mathf.Abs(x + 0.5f - cx)));
                            var col = new Color(light * Mathf.Lerp(0.92f, 1.0f, along), light, light * Mathf.Lerp(0.9f, 0.82f, along), cover);
                            int idx = y * W + x;
                            if (col.a >= pixels[idx].a) pixels[idx] = col;
                        }
                    }
                }
            }
            bladeTexture = new Texture2D(W, H, TextureFormat.RGBA32, true, false)
            {
                name = "GolfSimZA_GrassBlades",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
                hideFlags = HideFlags.HideAndDontSave
            };
            // Mip levels keep their coverage so thin blades do not vanish in the distance.
            int w = W, h = H;
            Color[] level = pixels;
            for (int mip = 0; mip < bladeTexture.mipmapCount; mip++)
            {
                if (mip > 0)
                {
                    int nw = Mathf.Max(1, w / 2), nh = Mathf.Max(1, h / 2);
                    var next = new Color[nw * nh];
                    for (int y = 0; y < nh; y++)
                        for (int x = 0; x < nw; x++)
                        {
                            Color sum = Color.clear;
                            float alpha = 0f;
                            for (int dy = 0; dy < 2; dy++)
                                for (int dx = 0; dx < 2; dx++)
                                {
                                    Color p = level[Mathf.Min(h - 1, y * 2 + dy) * w + Mathf.Min(w - 1, x * 2 + dx)];
                                    sum += new Color(p.r * p.a, p.g * p.a, p.b * p.a, 0f);
                                    alpha += p.a;
                                }
                            Color o = alpha > 0.001f ? new Color(sum.r / alpha, sum.g / alpha, sum.b / alpha, 0f) : new Color(0.8f, 0.8f, 0.7f, 0f);
                            o.a = Mathf.Clamp01(alpha * 0.25f * 1.5f);
                            next[y * nw + x] = o;
                        }
                    w = nw; h = nh; level = next;
                }
                bladeTexture.SetPixels(level, mip);
            }
            bladeTexture.Apply(false, true);
            return bladeTexture;
        }
    }
}
