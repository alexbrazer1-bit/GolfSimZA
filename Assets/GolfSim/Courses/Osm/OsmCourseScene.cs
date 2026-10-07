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
            byte[] splat0 = null, splat1 = null;
            float[,] normalized = null;
            float minH = 0f, maxH = 0f;
            Task load = Task.Run(() =>
            {
                heights = OsmHeightField.Load(Path.Combine(folder, OsmCourseBuilder.HeightsFile));
                surface = OsmSurfaceGrid.Load(Path.Combine(folder, OsmCourseBuilder.SurfaceFile));
                features = JsonUtility.FromJson<OsmCourseFeatures>(File.ReadAllText(Path.Combine(folder, OsmCourseBuilder.FeaturesFile)));
                MakeSplats(surface, out splat0, out splat1);
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
            PlanTrees();

            Summary = $"ground {heights.Size:0} m ({heights.Resolution - 1} cells), {lakes} water areas, {buildings} buildings, {treeSpots.Count} mapped trees";
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
                    for (int z = 0; z <= nz; z++)
                        for (int x = 0; x <= nx; x++)
                        {
                            int hx = (cx + x) * Step, hz = (cz + z) * Step;
                            float h = heights.At(hx, hz);
                            int i = z * (nx + 1) + x;
                            verts[i] = new Vector3(heights.OriginX + hx * heights.Spacing, h, heights.OriginZ + hz * heights.Spacing);
                            float dx = heights.At(hx + Step, hz) - heights.At(hx - Step, hz);
                            float dz = heights.At(hx, hz + Step) - heights.At(hx, hz - Step);
                            normals[i] = new Vector3(-dx, 2f * spacing, -dz).normalized;
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
            mesh.RecalculateNormals();
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
            Shader waterShader = Resources.Load<Shader>("GolfSimZA_Water");
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

        private void PlanTrees()
        {
            var random = new System.Random(features.courseOsmId.GetHashCode());
            float density = Mathf.Clamp(GolfSimZA.Core.AppSettings.Current.treeDensity, 0.2f, 1f);
            const int MaxTrees = 9000;
            // Woods and scrub only near the course (mapped woods can run for kilometres).
            Vector2[] near = null;
            foreach (OsmShape b in features.boundary)
                if (b.pts.Length >= 3 && (near == null || Poly2D.Area(b.pts) > Poly2D.Area(near))) near = b.pts;
            bool NearCourse(Vector2 p) => near == null || Poly2D.Distance(near, p) < 120f;

            void Plant(Vector2 p, bool bush, bool mappedTree)
            {
                if (treeSpots.Count >= MaxTrees) return;
                byte id = surface.At(p);
                if (!mappedTree && id != OsmSurface.Rough && id != OsmSurface.Native) return;
                if (!mappedTree && !NearCourse(p)) return;
                if (mappedTree && (id == OsmSurface.Green || id == OsmSurface.Tee || id == OsmSurface.Bunker)) return;
                float ground = heights.Sample(p);
                if (bush)
                {
                    treeSpots.Add(new TreeSpot
                    {
                        Base = new Vector3(p.x, ground, p.y), Kind = TreeField.Kind.Bush,
                        Height = 1.6f + (float)random.NextDouble() * 1.6f, Width = 1.4f + (float)random.NextDouble() * 0.5f,
                        Yaw = (float)random.NextDouble() * 360f, Colour = random.Next(3)
                    });
                    return;
                }
                bool pine = random.NextDouble() < 0.3;
                treeSpots.Add(new TreeSpot
                {
                    Base = new Vector3(p.x, ground, p.y),
                    Kind = pine ? TreeField.Kind.Pine : TreeField.Kind.Broadleaf,
                    Height = pine ? 13f + (float)random.NextDouble() * 9f : 8f + (float)random.NextDouble() * 8f,
                    Width = pine ? 0.9f + (float)random.NextDouble() * 0.3f : 1.0f + (float)random.NextDouble() * 0.35f,
                    Yaw = (float)random.NextDouble() * 360f,
                    Colour = random.Next(3)
                });
            }

            foreach (Vector2 t in features.trees) Plant(t, false, true);
            foreach (OsmShape row in features.treeRows)
            {
                float length = Poly2D.LineLength(row.pts);
                for (float d = 0f; d <= length; d += 8f) Plant(Poly2D.PointAlong(row.pts, d), false, true);
            }
            float woodStep = Mathf.Lerp(16f, 9f, density);
            foreach (OsmShape wood in features.woods) Scatter(wood.pts, woodStep, random, p => Plant(p, random.NextDouble() < 0.12, false));
            float scrubStep = Mathf.Lerp(12f, 7f, density);
            foreach (OsmShape s in features.scrub) Scatter(s.pts, scrubStep, random, p => Plant(p, random.NextDouble() < 0.75, false));
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
