using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GolfSimZA.Physics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfSimZA.Courses.Osm
{
    /// <summary>
    /// Makes the 3D course for an OpenStreetMap course from its saved files: the ground (a terrain
    /// painted with the surfaces), lakes and streams, buildings, and the list of trees to plant.
    /// The ball's surface checks use the saved surface map (<see cref="CourseSurfaceMap"/>).
    /// </summary>
    public sealed class OsmCourseScene
    {
        public const string TerrainName = "GolfSimZA_CourseGround";

        public Scene Scene { get; private set; }
        public string Error { get; private set; }
        public string Summary { get; private set; } = "";

        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private OsmCourseFeatures features;
        private OsmHeightField heights;
        private OsmSurfaceGrid surface;
        private Terrain terrain;

        private struct TreeSpot
        {
            public Vector3 Base;
            public TreeField.Kind Kind;
            public float Height;
            public float Width;
            public float Yaw;
            public int Colour;
        }

        private readonly List<TreeSpot> treeSpots = new List<TreeSpot>();

        /// <summary>Builds the course scene. progress receives 0..1.</summary>
        public IEnumerator Build(CourseDefinition course, Action<string, float> progress)
        {
            string folder = CourseLibrary.DataFolderFor(course);
            progress?.Invoke("Reading the course...", 0.05f);

            // 1. Files and the splat maps (worker thread).
            byte[] splat0 = null, splat1 = null, groundNormals = null;
            float[,] normalized = null;
            float minH = 0f, maxH = 0f;
            Task load = Task.Run(() =>
            {
                heights = OsmHeightField.Load(Path.Combine(folder, OsmCourseBuilder.HeightsFile));
                surface = OsmSurfaceGrid.Load(Path.Combine(folder, OsmCourseBuilder.SurfaceFile));
                features = JsonUtility.FromJson<OsmCourseFeatures>(File.ReadAllText(Path.Combine(folder, OsmCourseBuilder.FeaturesFile)));
                MakeSplats(surface, out splat0, out splat1);
                groundNormals = MakeGroundNormals(heights);
                minH = heights.MinHeight - 2f;
                maxH = heights.MaxHeight + 2f;
                int res = heights.Resolution;
                normalized = new float[res, res];
                float range = Mathf.Max(1f, maxH - minH);
                for (int z = 0; z < res; z++)
                    for (int x = 0; x < res; x++)
                        normalized[z, x] = (heights.Heights[z * res + x] - minH) / range;
            });
            while (!load.IsCompleted) yield return null;
            if (load.IsFaulted)
            {
                Error = "The course files could not be read: " + load.Exception?.GetBaseException().Message + ". Build the course again.";
                yield break;
            }

            Scene = SceneManager.CreateScene("GolfSimZA_OSM_" + course.id);

            // 2. Ground.
            progress?.Invoke("Making the ground...", 0.35f);
            yield return null;
            var data = new TerrainData { heightmapResolution = heights.Resolution, name = "GolfSimZA_CourseGround" };
            data.size = new Vector3(heights.Size, Mathf.Max(1f, maxH - minH), heights.Size);
            data.SetHeights(0, 0, normalized);
            owned.Add(data);
            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = TerrainName;
            SceneManager.MoveGameObjectToScene(terrainObject, Scene);
            terrainObject.transform.position = new Vector3(heights.OriginX, minH, heights.OriginZ);
            terrain = terrainObject.GetComponent<Terrain>();
            terrain.drawInstanced = false;
            terrain.heightmapPixelError = GolfSimZA.Core.AppSettings.Current.graphicsQuality >= 2 ? 2f : 4f;
            terrain.basemapDistance = 20000f;
            terrain.drawTreesAndFoliage = false;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            // The terrain is used for the ball (exact heights); the ground is drawn with GolfSimZA's
            // own meshes and surface material (Unity's terrain drawing needs terrain shaders).
            terrain.drawHeightmap = false;
            Material ground = GroundMaterial(splat0, splat1, surface.Cells, course);
            ground.SetTexture("_GroundNormal", GroundNormalTexture(groundNormals, heights.Resolution));
            progress?.Invoke("Making the ground...", 0.45f);
            yield return null;
            int chunks = 0;
            foreach (bool _ in MakeGroundMeshes(ground)) { chunks++; if (chunks % 24 == 0) yield return null; }
            MakeSurroundings(ground);
            progress?.Invoke("Making the ground...", 0.55f);
            yield return null;

            TerrainCollider collider = terrainObject.GetComponent<TerrainCollider>();
            OsmSurfaceGrid grid = surface;
            CourseSurfaceMap.Set(world =>
            {
                var p = new Vector2(world.x, world.z);
                if (p.x < grid.OriginX || p.y < grid.OriginZ || p.x >= grid.OriginX + grid.Size || p.y >= grid.OriginZ + grid.Size) return CourseSurfaceMap.Unknown;
                return grid.At(p);
            }, collider);
            GreenSurface.Reset();

            // 3. Water.
            progress?.Invoke("Filling the lakes...", 0.65f);
            yield return null;
            int lakes = MakeWater();

            // 4. Buildings.
            progress?.Invoke("Putting up the buildings...", 0.75f);
            yield return null;
            int buildings = MakeBuildings();

            // 5. Trees (planted by the loader into the tree field).
            progress?.Invoke("Planting trees...", 0.85f);
            yield return null;
            treeDensity = Mathf.Clamp(GolfSimZA.Core.AppSettings.Current.treeDensity, 0.2f, 1f); // read here: settings are not for worker threads
            Task plan = Task.Run(PlanTrees);
            while (!plan.IsCompleted) yield return null;
            if (plan.IsFaulted) Debug.LogWarning("[GolfSimZA] Tree planting failed: " + plan.Exception?.GetBaseException());

            Summary = $"ground {heights.Size:0} m ({heights.Resolution - 1} cells), {lakes} water areas, {buildings} buildings, {mappedTrees} mapped trees, {treeSpots.Count - mappedTrees - reeds} trees and bushes in the rough, {reeds} reed clumps at the water";
        }

        /// <summary>Adds the mapped trees, woods and scrub to the tree field (after the course look step cleared it).</summary>
        public int PlantTrees(TreeField field)
        {
            foreach (TreeSpot t in treeSpots) field.Add(t.Kind, t.Base, t.Height, t.Yaw, t.Width, t.Colour);
            return treeSpots.Count;
        }

        /// <summary>Releases the generated ground, textures and meshes.</summary>
        public void Dispose()
        {
            CourseSurfaceMap.Clear();
            foreach (UnityEngine.Object o in owned)
                if (o != null) UnityEngine.Object.Destroy(o);
            owned.Clear();
        }

        // ---------------------------------------------------------------- ground meshes

        /// <summary>Ground meshes in 64 x 64 quad chunks, one quad per two height cells.</summary>
        private IEnumerable<bool> MakeGroundMeshes(Material material)
        {
            int res = heights.Resolution;
            const int Step = 2, Quads = 64;
            int points = (res - 1) / Step + 1; // grid points along a side
            float spacing = heights.Spacing * Step;
            for (int cz = 0; cz < points - 1; cz += Quads)
                for (int cx = 0; cx < points - 1; cx += Quads)
                {
                    int nx = Mathf.Min(Quads, points - 1 - cx), nz = Mathf.Min(Quads, points - 1 - cz);
                    var verts = new Vector3[(nx + 1) * (nz + 1)];
                    var normals = new Vector3[verts.Length];
                    var uvs = new Vector2[verts.Length];
                    var tangents = new Vector4[verts.Length];
                    var colours = new Color32[verts.Length];
                    for (int z = 0; z <= nz; z++)
                        for (int x = 0; x <= nx; x++)
                        {
                            int hx = (cx + x) * Step, hz = (cz + z) * Step;
                            float h = heights.At(hx, hz);
                            int i = z * (nx + 1) + x;
                            verts[i] = new Vector3(heights.OriginX + hx * heights.Spacing, h, heights.OriginZ + hz * heights.Spacing);
                            // Lighting uses the full-detail shape texture (_GroundNormal): the mesh
                            // normal is straight up with tangent +x so that texture holds world normals.
                            normals[i] = Vector3.up;
                            tangents[i] = new Vector4(1f, 0f, 0f, -1f);
                            colours[i] = new Color32(255, 255, 255, 255);
                            uvs[i] = new Vector2(hx / (float)(res - 1), hz / (float)(res - 1));
                        }
                    var tris = new int[nx * nz * 6];
                    int t = 0;
                    for (int z = 0; z < nz; z++)
                        for (int x = 0; x < nx; x++)
                        {
                            int a = z * (nx + 1) + x, b = a + 1, c = a + nx + 1, d = c + 1;
                            // Clockwise seen from above (front face up in Unity).
                            tris[t++] = a; tris[t++] = c; tris[t++] = b;
                            tris[t++] = b; tris[t++] = c; tris[t++] = d;
                        }
                    var mesh = new Mesh { name = "Ground" + cx + "_" + cz };
                    mesh.vertices = verts;
                    mesh.normals = normals;
                    mesh.tangents = tangents;
                    mesh.colors32 = colours;
                    mesh.uv = uvs;
                    mesh.triangles = tris;
                    mesh.RecalculateBounds();
                    owned.Add(mesh);
                    var go = new GameObject("GolfSimZA_Ground_" + cx + "_" + cz);
                    SceneManager.MoveGameObjectToScene(go, Scene);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = material;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveShadows = true;
                    yield return true;
                }
        }

        /// <summary>Flat land to the horizon around the built square, so there is no edge to see.</summary>
        private void MakeSurroundings(Material material)
        {
            int res = heights.Resolution;
            float edge = float.MaxValue;
            for (int i = 0; i < res; i += 8)
            {
                edge = Mathf.Min(edge, heights.At(i, 0), heights.At(i, res - 1));
                edge = Mathf.Min(edge, heights.At(0, i), heights.At(res - 1, i));
            }
            float y = edge - 0.6f, far = 12000f;
            float x0 = heights.OriginX, z0 = heights.OriginZ, x1 = x0 + heights.Size, z1 = z0 + heights.Size;
            // A ring of four quads around the square (uv at the square's edge = its natural ground).
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int s = verts.Count;
                foreach (Vector3 v in new[] { a, b, c, d })
                {
                    verts.Add(v);
                    uvs.Add(new Vector2(Mathf.Clamp01((v.x - x0) / heights.Size) * 0.98f + 0.01f, Mathf.Clamp01((v.z - z0) / heights.Size) * 0.98f + 0.01f));
                }
                tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
                tris.Add(s); tris.Add(s + 2); tris.Add(s + 3);
            }
            Vector3 P(float x, float z) => new Vector3(x, y, z);
            Quad(P(x0 - far, z1), P(x0 - far, z1 + far), P(x1 + far, z1 + far), P(x1 + far, z1));   // north
            Quad(P(x0 - far, z0 - far), P(x0 - far, z0), P(x1 + far, z0), P(x1 + far, z0 - far));   // south
            Quad(P(x0 - far, z0), P(x0 - far, z1), P(x0, z1), P(x0, z0));                           // west
            Quad(P(x1, z0), P(x1, z1), P(x1 + far, z1), P(x1 + far, z0));                           // east
            var mesh = new Mesh { name = "Surroundings" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            var up = new List<Vector3>();
            var tangents = new List<Vector4>();
            var flat = new List<Color32>();
            for (int i = 0; i < verts.Count; i++) { up.Add(Vector3.up); tangents.Add(new Vector4(1f, 0f, 0f, -1f)); flat.Add(new Color32(0, 0, 0, 255)); }
            mesh.SetNormals(up);
            mesh.SetTangents(tangents);
            mesh.SetColors(flat); // flat land: not the detailed ground shape
            mesh.RecalculateBounds();
            owned.Add(mesh);
            var go = new GameObject("GolfSimZA_Surroundings");
            SceneManager.MoveGameObjectToScene(go, Scene);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ---------------------------------------------------------------- ground material

        private Material GroundMaterial(byte[] splat0, byte[] splat1, int cells, CourseDefinition course)
        {
            Shader shader = Resources.Load<Shader>("GolfSimZA_CourseGround");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogWarning("[GolfSimZA] Course ground shader missing - plain ground used.");
                shader = Shader.Find("Standard");
            }
            var material = new Material(shader) { name = "GolfSimZA_CourseGround" };
            Debug.Log("[GolfSimZA] Course ground material: " + shader.name + (shader.isSupported ? " (supported)" : " (NOT supported)") +
                      ", texture arrays " + SystemInfo.supports2DArrayTextures);
            owned.Add(material);
            Texture2D s0 = SplatTexture(splat0, cells, "GolfSimZA_Splat0");
            Texture2D s1 = SplatTexture(splat1, cells, "GolfSimZA_Splat1");
            material.SetTexture("_Splat0", s0);
            material.SetTexture("_Splat1", s1);
            material.SetTexture("_Layers", GroundTextures.Layers);
            material.SetTexture("_Macro", GroundTextures.Macro);

            // Mowing stripes run along the course's main playing direction.
            Vector2 dir = Vector2.zero;
            if (course.holes != null)
                foreach (HoleDefinition h in course.holes)
                {
                    TeeDefinition tee = h.tees != null && h.tees.Length > 0 ? h.tees[0] : null;
                    if (tee == null) continue;
                    Vector2 d = new Vector2(h.GreenTarget.x - tee.position.x, h.GreenTarget.z - tee.position.z);
                    if (d.sqrMagnitude < 1f) continue;
                    d.Normalize();
                    if (Vector2.Dot(d, dir) < 0f) d = -d; // stripes have no front or back
                    dir += d;
                }
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.up;
            material.SetVector("_StripeDir", new Vector4(dir.normalized.x, dir.normalized.y, 0f, 0f));
            return material;
        }

        /// <summary>
        /// The ground's shape at full detail (one normal per height point) as RGBA bytes: world
        /// normal x, z, y mapped to 0..255 (the layout the ground shader expects). Worker thread.
        /// </summary>
        private static byte[] MakeGroundNormals(OsmHeightField h)
        {
            int res = h.Resolution;
            float twoSpacing = 2f * h.Spacing;
            var bytes = new byte[res * res * 4];
            Parallel.For(0, res, z =>
            {
                for (int x = 0; x < res; x++)
                {
                    float dx = h.At(Math.Min(res - 1, x + 1), z) - h.At(Math.Max(0, x - 1), z);
                    float dz = h.At(x, Math.Min(res - 1, z + 1)) - h.At(x, Math.Max(0, z - 1));
                    Vector3 n = new Vector3(-dx, twoSpacing, -dz).normalized;
                    int i = (z * res + x) * 4;
                    bytes[i] = (byte)Mathf.Clamp(Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f), 0, 255);
                    bytes[i + 1] = (byte)Mathf.Clamp(Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f), 0, 255);
                    bytes[i + 2] = (byte)Mathf.Clamp(Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f), 0, 255);
                    bytes[i + 3] = 255;
                }
            });
            return bytes;
        }

        private Texture2D GroundNormalTexture(byte[] rgba, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = "GolfSimZA_GroundShape",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
                hideFlags = HideFlags.DontSave
            };
            tex.SetPixelData(rgba, 0);
            tex.Apply(true, true);
            owned.Add(tex);
            return tex;
        }

        private Texture2D SplatTexture(byte[] rgba, int size, string name)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            tex.LoadRawTextureData(rgba);
            tex.Apply(false, true);
            owned.Add(tex);
            return tex;
        }

        /// <summary>Two RGBA weight maps (surfaces 0-3 and 4-7), softened a little at the edges.</summary>
        private static void MakeSplats(OsmSurfaceGrid s, out byte[] splat0, out byte[] splat1)
        {
            int n = s.Cells;
            var weights = new byte[OsmSurface.Count][];
            for (int k = 0; k < OsmSurface.Count; k++) weights[k] = new byte[n * n];
            for (int i = 0; i < s.Ids.Length; i++) weights[Mathf.Min(s.Ids[i], (byte)7)][i] = 255;
            // 3x3 blur per surface (separable) so edges blend over about a metre.
            Parallel.For(0, OsmSurface.Count, k =>
            {
                byte[] w = weights[k];
                var tmp = new byte[w.Length];
                for (int z = 0; z < n; z++)
                {
                    int row = z * n;
                    for (int x = 0; x < n; x++)
                    {
                        int a = w[row + Math.Max(0, x - 1)], b = w[row + x], c = w[row + Math.Min(n - 1, x + 1)];
                        tmp[row + x] = (byte)((a + 2 * b + c) >> 2);
                    }
                }
                for (int z = 0; z < n; z++)
                    for (int x = 0; x < n; x++)
                    {
                        int a = tmp[Math.Max(0, z - 1) * n + x], b = tmp[z * n + x], c = tmp[Math.Min(n - 1, z + 1) * n + x];
                        w[z * n + x] = (byte)((a + 2 * b + c) >> 2);
                    }
            });
            splat0 = new byte[n * n * 4];
            splat1 = new byte[n * n * 4];
            for (int i = 0; i < n * n; i++)
            {
                int j = i * 4;
                splat0[j] = weights[0][i]; splat0[j + 1] = weights[1][i]; splat0[j + 2] = weights[2][i]; splat0[j + 3] = weights[3][i];
                splat1[j] = weights[4][i]; splat1[j + 1] = weights[5][i]; splat1[j + 2] = weights[6][i]; splat1[j + 3] = weights[7][i];
            }
        }

        // ---------------------------------------------------------------- water

        private int MakeWater()
        {
            Shader waterShader = Resources.Load<Shader>("GolfSimZA_CourseWater");
            if (waterShader == null || !waterShader.isSupported) waterShader = Resources.Load<Shader>("GolfSimZA_Water");
            if (waterShader == null) return 0;
            var material = new Material(waterShader) { name = "GolfSimZA_CourseWater", enableInstancing = true };
            material.SetTexture("_BumpMap", ShaderRepair.RippleTexture());
            owned.Add(material);
            int count = 0;
            foreach (OsmShape w in features.water)
            {
                if (w.pts.Length < 3 || Poly2D.Area(w.pts) < 8f) continue;
                bool stream = w.value == OsmCourseBuilder.StreamLevel;
                List<int> tris = Poly2D.Triangulate(w.pts);
                if (tris.Count < 3) continue;
                var verts = new Vector3[w.pts.Length];
                var uvs = new Vector2[w.pts.Length];
                for (int i = 0; i < w.pts.Length; i++)
                {
                    float y = stream ? heights.Sample(w.pts[i]) - 0.45f : w.value;
                    verts[i] = new Vector3(w.pts[i].x, y, w.pts[i].y);
                    uvs[i] = w.pts[i] / 10f;
                }
                // Triangles face up (clockwise seen from above in Unity).
                for (int t = 0; t < tris.Count; t += 3)
                {
                    Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                    if (Vector3.Cross(b - a, c - a).y < 0f) { int tmp = tris[t + 1]; tris[t + 1] = tris[t + 2]; tris[t + 2] = tmp; }
                }
                var mesh = new Mesh { name = "Water" + w.osmId, indexFormat = verts.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
                mesh.vertices = verts;
                mesh.uv = uvs;
                mesh.triangles = tris.ToArray();
                var normals = new Vector3[verts.Length];
                for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
                mesh.normals = normals;
                mesh.RecalculateBounds();
                owned.Add(mesh);
                // "Map_Water" makes the ball splash and stop when it reaches the water.
                var go = new GameObject("Map_Water_" + count);
                SceneManager.MoveGameObjectToScene(go, Scene);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
                count++;
            }
            return count;
        }

        // ---------------------------------------------------------------- buildings

        private int MakeBuildings()
        {
            Shader shader = Resources.Load<Shader>("GolfSimZA_VertexColorLit");
            if (shader == null || features.buildings.Count == 0) return 0;
            var material = new Material(shader) { name = "GolfSimZA_Buildings" };
            owned.Add(material);
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            int built = 0, part = 0;
            var random = new System.Random(7);
            foreach (OsmShape b in features.buildings)
            {
                if (b.pts.Length < 3 || Poly2D.Area(b.pts) < 6f) continue;
                float ground = float.MaxValue;
                foreach (Vector2 p in b.pts) ground = Mathf.Min(ground, heights.Sample(p));
                float bottom = ground - 0.5f, top = ground + Mathf.Max(2.5f, b.value);
                Color wall = Color.Lerp(new Color(0.86f, 0.82f, 0.74f), new Color(0.78f, 0.74f, 0.70f), (float)random.NextDouble());
                Color roof = random.NextDouble() < 0.6 ? new Color(0.48f, 0.30f, 0.24f) : new Color(0.40f, 0.42f, 0.44f);
                bool ccw = Poly2D.SignedArea(b.pts) > 0f;
                for (int i = 0; i < b.pts.Length; i++)
                {
                    Vector2 a2 = b.pts[i], c2 = b.pts[(i + 1) % b.pts.Length];
                    if (!ccw) { Vector2 t = a2; a2 = c2; c2 = t; }
                    int start = verts.Count;
                    verts.Add(new Vector3(a2.x, bottom, a2.y)); verts.Add(new Vector3(c2.x, bottom, c2.y));
                    verts.Add(new Vector3(c2.x, top, c2.y)); verts.Add(new Vector3(a2.x, top, a2.y));
                    Color shade = wall * (0.85f + 0.15f * Mathf.Abs(Vector2.Dot((c2 - a2).normalized, new Vector2(0.6f, 0.8f))));
                    shade.a = 1f;
                    cols.Add(shade); cols.Add(shade); cols.Add(shade); cols.Add(shade);
                    // Outward-facing quad (walls of a counter-clockwise outline face outward when wound this way).
                    tris.Add(start); tris.Add(start + 2); tris.Add(start + 1);
                    tris.Add(start); tris.Add(start + 3); tris.Add(start + 2);
                }
                List<int> roofTris = Poly2D.Triangulate(b.pts);
                int roofStart = verts.Count;
                foreach (Vector2 p in b.pts) { verts.Add(new Vector3(p.x, top, p.y)); cols.Add(roof); }
                for (int t = 0; t < roofTris.Count; t += 3)
                {
                    Vector3 a = verts[roofStart + roofTris[t]], bb = verts[roofStart + roofTris[t + 1]], c = verts[roofStart + roofTris[t + 2]];
                    bool up = Vector3.Cross(bb - a, c - a).y > 0f;
                    tris.Add(roofStart + roofTris[t]);
                    tris.Add(roofStart + (up ? roofTris[t + 1] : roofTris[t + 2]));
                    tris.Add(roofStart + (up ? roofTris[t + 2] : roofTris[t + 1]));
                }
                built++;
                if (verts.Count > 60000) { FlushBuildings(material, verts, cols, tris, part++); }
            }
            if (verts.Count > 0) FlushBuildings(material, verts, cols, tris, part);
            return built;
        }

        private void FlushBuildings(Material material, List<Vector3> verts, List<Color> cols, List<int> tris, int part)
        {
            var mesh = new Mesh { name = "Buildings" + part, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            var uv = new Vector2[verts.Count];
            for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(verts[i].x + verts[i].z, verts[i].y) * 0.5f;
            mesh.uv = uv;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            owned.Add(mesh);
            var go = new GameObject("Buildings_" + part);
            SceneManager.MoveGameObjectToScene(go, Scene);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            verts.Clear(); cols.Clear(); tris.Clear();
        }

        // ---------------------------------------------------------------- trees

        private int mappedTrees, reeds;
        private float treeDensity = 0.6f;

        /// <summary>
        /// Plans the trees (worker thread): the mapped trees, woods and scrub, then - like the course
        /// planners' drawings - belts of trees between the holes with single trees and bushes closer
        /// to the fairways, palms near the water, and reed clumps along the banks. The playing lines
        /// stay clear. Uses 2 m grids of distances (to the playing surfaces, the water, the course).
        /// </summary>
        private void PlanTrees()
        {
            var random = new System.Random(features.courseOsmId.GetHashCode());
            float density = treeDensity;
            int maxTrees = Mathf.RoundToInt(Mathf.Lerp(5000f, 11000f, density));
            const int MaxReeds = 6000;

            // ---- 2 m grids over the ground square
            const float cell = 2f;
            int n = Mathf.CeilToInt(heights.Size / cell);
            var gridMin = new Vector2(heights.OriginX, heights.OriginZ);
            var play = new bool[n * n];      // fairway, green, tee, fringe, bunker
            var blocked = new bool[n * n];   // paths, buildings, water
            var water = new bool[n * n];
            var course = new bool[n * n];
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    byte id = surface.At(gridMin + new Vector2((x + 0.5f) * cell, (z + 0.5f) * cell));
                    play[z * n + x] = id == OsmSurface.Fairway || id == OsmSurface.Green || id == OsmSurface.Tee || id == OsmSurface.Fringe || id == OsmSurface.Bunker;
                    blocked[z * n + x] = id == OsmSurface.Path;
                }
            foreach (OsmShape w in features.water)
                Poly2D.Rasterize(w.pts, gridMin, cell, n, n, (x, z) => { water[z * n + x] = true; blocked[z * n + x] = true; });
            foreach (OsmShape b in features.buildings)
                Poly2D.Rasterize(Grow(b.pts, 3f), gridMin, cell, n, n, (x, z) => blocked[z * n + x] = true);
            foreach (OsmShape p in features.paths)
                if (p.pts.Length >= 2) Poly2D.RasterizeLine(p.pts, Mathf.Max(1.5f, p.value * 0.5f + 0.5f), gridMin, cell, n, n, (x, z) => blocked[z * n + x] = true);
            bool hasBoundary = false;
            foreach (OsmShape b in features.boundary)
                if (b.pts.Length >= 3) { hasBoundary = true; Poly2D.Rasterize(b.pts, gridMin, cell, n, n, (x, z) => course[z * n + x] = true); }
            float[] toPlay = Distances(play, n, cell);
            float[] toWater = Distances(water, n, cell);
            float[] toCourse = hasBoundary ? Distances(course, n, cell) : null;
            int Index(Vector2 p)
            {
                int x = Mathf.FloorToInt((p.x - gridMin.x) / cell), z = Mathf.FloorToInt((p.y - gridMin.y) / cell);
                return x < 0 || z < 0 || x >= n || z >= n ? -1 : z * n + x;
            }
            // Planted near the course: inside its boundary or just outside it (mapped woods can run for kilometres).
            bool NearCourse(int i) => i >= 0 && (toCourse != null ? toCourse[i] < 50f : toPlay[i] < 150f);

            // Spatial hash so new trees keep clear of each other and of the mapped ones.
            var taken = new HashSet<long>();
            long Key(Vector2 p) => ((long)Mathf.FloorToInt(p.x / 4f) << 32) ^ (uint)Mathf.FloorToInt(p.y / 4f);
            bool Free(Vector2 p)
            {
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                        if (taken.Contains(((long)(Mathf.FloorToInt(p.x / 4f) + dx) << 32) ^ (uint)(Mathf.FloorToInt(p.y / 4f) + dz))) return false;
                return true;
            }
            float R(float a, float b) => a + (float)random.NextDouble() * (b - a);

            void Add(Vector2 p, TreeField.Kind kind)
            {
                float h, w;
                switch (kind)
                {
                    case TreeField.Kind.Pine: h = R(13f, 24f); w = R(0.75f, 1.0f); break;
                    case TreeField.Kind.Spreading: h = R(8f, 13f); w = R(1.4f, 1.8f); break;
                    case TreeField.Kind.Flowering: h = R(6f, 10f); w = R(1.0f, 1.3f); break;
                    case TreeField.Kind.Palm: h = R(8f, 15f); w = R(0.8f, 1.0f); break;
                    case TreeField.Kind.Bush: h = R(1.6f, 3.4f); w = R(1.3f, 1.8f); break;
                    case TreeField.Kind.Reed: h = R(1.3f, 2.4f); w = R(1.6f, 2.4f); break;
                    default: h = R(9f, 17f); w = R(0.95f, 1.3f); break;
                }
                treeSpots.Add(new TreeSpot { Base = new Vector3(p.x, heights.Sample(p), p.y), Kind = kind, Height = h, Width = w, Yaw = R(0f, 360f), Colour = random.Next(3) });
                if (kind != TreeField.Kind.Reed) taken.Add(Key(p));
            }

            var lines = new List<Vector2[]>();
            foreach (OsmShape h in features.holes) if (h.pts.Length >= 2) lines.Add(h.pts);
            float FromLine(Vector2 p)
            {
                float best = float.MaxValue;
                foreach (Vector2[] line in lines) best = Mathf.Min(best, Poly2D.LineDistance(line, p));
                return best;
            }

            // ---- 1. Mapped trees, tree rows, woods and scrub
            void PlantMapped(Vector2 p, bool bush, bool exact)
            {
                if (treeSpots.Count >= maxTrees) return;
                int i = Index(p);
                if (i < 0 || water[i]) return;
                byte id = surface.At(p);
                if (id == OsmSurface.Green || id == OsmSurface.Tee || id == OsmSurface.Bunker) return;
                if (!exact && (play[i] || blocked[i] || !NearCourse(i))) return;
                // A mapped tree inside a fairway outline right on the playing line: left out (the
                // fairway outlines often run over the trees between two holes; those stay).
                if (play[i] && FromLine(p) < 18f) return;
                double r = random.NextDouble();
                TreeField.Kind kind = bush ? TreeField.Kind.Bush
                    : toWater[i] < 25f && r < 0.3 ? TreeField.Kind.Palm
                    : r < 0.3 ? TreeField.Kind.Pine : r < 0.42 ? TreeField.Kind.Spreading : TreeField.Kind.Broadleaf;
                Add(p, kind);
            }
            foreach (Vector2 t in features.trees) PlantMapped(t, false, true);
            foreach (OsmShape row in features.treeRows)
            {
                float length = Poly2D.LineLength(row.pts);
                for (float d = 0f; d <= length; d += 8f) PlantMapped(Poly2D.PointAlong(row.pts, d), false, true);
            }
            float woodStep = Mathf.Lerp(14f, 8f, density);
            foreach (OsmShape wood in features.woods) Scatter(wood.pts, woodStep, random, p => { if (Free(p)) PlantMapped(p, random.NextDouble() < 0.12, false); });
            float scrubStep = Mathf.Lerp(12f, 7f, density);
            foreach (OsmShape sc in features.scrub) Scatter(sc.pts, scrubStep, random, p => { if (Free(p)) PlantMapped(p, random.NextDouble() < 0.75, false); });
            mappedTrees = treeSpots.Count;

            // ---- 2. Tree belts between the holes (the playing lines stay clear)
            float step = Mathf.Lerp(11f, 7f, density);
            for (float y = gridMin.y + step * 0.5f; y < gridMin.y + heights.Size; y += step)
                for (float x = gridMin.x + step * 0.5f; x < gridMin.x + heights.Size; x += step)
                {
                    if (treeSpots.Count >= maxTrees) break;
                    var p = new Vector2(x + R(-0.45f, 0.45f) * step, y + R(-0.45f, 0.45f) * step);
                    int i = Index(p);
                    if (i < 0 || play[i] || blocked[i] || !NearCourse(i)) continue;
                    float fromPlay = toPlay[i];
                    if (fromPlay < 4f) continue; // a strip of rough along every fairway
                    if (FromLine(p) < 20f) continue; // the playing line itself
                    bool edge = fromPlay < 14f;
                    if (random.NextDouble() > (edge ? 0.22 : 0.85)) continue;
                    if (!Free(p)) continue;
                    double r = random.NextDouble();
                    bool nearWater = toWater[i] < 22f;
                    TreeField.Kind kind;
                    if (nearWater && r < 0.30) kind = TreeField.Kind.Palm;
                    else if (edge) kind = r < 0.35 ? TreeField.Kind.Bush : r < 0.47 ? TreeField.Kind.Flowering : r < 0.80 ? TreeField.Kind.Broadleaf : r < 0.92 ? TreeField.Kind.Spreading : TreeField.Kind.Pine;
                    else kind = r < 0.13 ? TreeField.Kind.Bush : r < 0.18 ? TreeField.Kind.Flowering : r < 0.60 ? TreeField.Kind.Broadleaf : r < 0.72 ? TreeField.Kind.Spreading : r < 0.96 ? TreeField.Kind.Pine : TreeField.Kind.Palm;
                    Add(p, kind);
                }

            // ---- 3. Reeds along the banks of lakes, ponds and streams
            int treesBefore = treeSpots.Count;
            foreach (OsmShape w in features.water)
            {
                if (w.pts.Length < 3) continue;
                for (int k = 0; k < w.pts.Length && treeSpots.Count - treesBefore < MaxReeds; k++)
                {
                    Vector2 a = w.pts[k], b = w.pts[(k + 1) % w.pts.Length];
                    float length = (b - a).magnitude;
                    if (length < 0.01f) continue;
                    Vector2 along = (b - a) / length;
                    Vector2 normal = new Vector2(along.y, -along.x);
                    for (float d = 0f; d < length; d += 1.4f)
                    {
                        if (random.NextDouble() > 0.55) continue;
                        Vector2 at = a + along * d;
                        // Outward = away from the water.
                        Vector2 outward = Poly2D.Contains(w.pts, at + normal * 0.8f) ? -normal : normal;
                        Vector2 p = at + outward * R(-0.6f, 2.2f);
                        int i = Index(p);
                        if (i < 0 || !NearCourse(i) || toPlay[i] < 2f) continue;
                        byte id = surface.At(p);
                        if (id == OsmSurface.Green || id == OsmSurface.Tee || id == OsmSurface.Fairway || id == OsmSurface.Bunker || id == OsmSurface.Path) continue;
                        Add(p, TreeField.Kind.Reed);
                    }
                }
            }
            reeds = treeSpots.Count - treesBefore;
        }

        /// <summary>A polygon pushed out by m metres from its centre (rough, for keeping clear of buildings).</summary>
        private static Vector2[] Grow(Vector2[] poly, float m)
        {
            if (poly.Length < 3) return poly;
            Vector2 c = Poly2D.Centroid(poly);
            var grown = new Vector2[poly.Length];
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 d = poly[i] - c;
                grown[i] = poly[i] + (d.sqrMagnitude > 1e-6f ? d.normalized * m : Vector2.zero);
            }
            return grown;
        }

        /// <summary>Distance (m) from every cell to the nearest set cell (two-pass chamfer).</summary>
        private static float[] Distances(bool[] set, int n, float cell)
        {
            const float Far = 1e7f, Diagonal = 1.41421356f;
            var d = new float[n * n];
            for (int i = 0; i < d.Length; i++) d[i] = set[i] ? 0f : Far;
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    int i = z * n + x;
                    float v = d[i];
                    if (v == 0f) continue;
                    if (x > 0) v = Mathf.Min(v, d[i - 1] + 1f);
                    if (z > 0)
                    {
                        v = Mathf.Min(v, d[i - n] + 1f);
                        if (x > 0) v = Mathf.Min(v, d[i - n - 1] + Diagonal);
                        if (x < n - 1) v = Mathf.Min(v, d[i - n + 1] + Diagonal);
                    }
                    d[i] = v;
                }
            for (int z = n - 1; z >= 0; z--)
                for (int x = n - 1; x >= 0; x--)
                {
                    int i = z * n + x;
                    float v = d[i];
                    if (v == 0f) continue;
                    if (x < n - 1) v = Mathf.Min(v, d[i + 1] + 1f);
                    if (z < n - 1)
                    {
                        v = Mathf.Min(v, d[i + n] + 1f);
                        if (x < n - 1) v = Mathf.Min(v, d[i + n + 1] + Diagonal);
                        if (x > 0) v = Mathf.Min(v, d[i + n - 1] + Diagonal);
                    }
                    d[i] = v;
                }
            for (int i = 0; i < d.Length; i++) d[i] *= cell;
            return d;
        }

        private static void Scatter(Vector2[] poly, float step, System.Random random, Action<Vector2> plant)
        {
            if (poly.Length < 3) return;
            Poly2D.Bounds(poly, out Vector2 min, out Vector2 max);
            for (float y = min.y + step * 0.5f; y < max.y; y += step)
                for (float x = min.x + step * 0.5f; x < max.x; x += step)
                {
                    var p = new Vector2(x + (float)(random.NextDouble() * 2 - 1) * step * 0.45f, y + (float)(random.NextDouble() * 2 - 1) * step * 0.45f);
                    if (Poly2D.Contains(poly, p)) plant(p);
                }
        }
    }
}
