using System.Collections.Generic;
using GolfSimZA.Core;
using UnityEngine;

namespace GolfSimZA.Physics
{
    /// <summary>
    /// The GolfSim ZA driving range, in the style of a tour practice ground: a long fairway with
    /// lengthwise mowing stripes and natural curved edges, darker rough beside it, sandy desert
    /// scrub beyond, low foothills and a mountain range on the horizon, a target green with flag
    /// and a dashed white line from the mat to the target. Rebuilt whenever the range settings
    /// change. The ground plane is the only collider (the ball lands and rolls on it).
    /// </summary>
    public sealed class RangeEnvironment : MonoBehaviour
    {
        public const float MinTarget = 20f;
        public const float MaxTarget = 350f;
        private const float RangeStart = -24f;
        private const float RangeEnd = 480f;
        private const float Step = 2f;

        private readonly List<GameObject> built = new List<GameObject>();
        private readonly List<GameObject> targetParts = new List<GameObject>();
        private static Material fairwayMat, roughMat, groundMat, greenMat, fringeMat, lineMat, flagMat, poleMat, teeMat, mountainMat;
        private LineRenderer aimLine;
        private float seedLeft = 1.3f, seedRight = 4.1f;

        public Vector3 TeePosition => Vector3.zero;
        public Vector3 TargetPosition { get; private set; }
        public float TargetDistance => TargetPosition.magnitude;
        public float FairwayWidth { get; private set; }
        public float GreenWidth { get; private set; }

        /// <summary>Distance lines along the range, in metres from the tee (for on-screen labels).</summary>
        public readonly List<float> MarkerDistances = new List<float>();

        /// <summary>0 = fairway, 1 = rough, 2 = desert scrub (outside the rough).</summary>
        public int SurfaceAt(Vector3 p)
        {
            if (p.z < RangeStart || p.z > RangeEnd) return 2;
            float left = LeftEdge(p.z), right = RightEdge(p.z);
            if (p.x >= -left && p.x <= right) return 0;
            float rough = RoughWidth(p.z);
            if (p.x >= -left - rough && p.x <= right + rough) return 1;
            return 2;
        }

        // Fairway edges: narrower near the mat, widening down the range, gently wavy on each side.
        private float Widen(float z) => Mathf.Lerp(0.62f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-10f, 140f, z)));
        public float LeftEdge(float z) => FairwayWidth * 0.5f * Widen(z) + 3.2f * Mathf.Sin(z * 0.019f + seedLeft) + 1.6f * Mathf.Sin(z * 0.057f + seedLeft * 2f);
        public float RightEdge(float z) => FairwayWidth * 0.5f * Widen(z) + 3.2f * Mathf.Sin(z * 0.021f + seedRight) + 1.6f * Mathf.Sin(z * 0.049f + seedRight * 2f);
        private static float RoughWidth(float z) => 16f + 5f * Mathf.Sin(z * 0.013f + 0.7f) + 3f * Mathf.Sin(z * 0.041f);

        public void Build(float targetMeters, float fairwayWidth, float greenWidth)
        {
            EnsureMaterials();
            foreach (GameObject go in built) if (go != null) Destroy(go);
            built.Clear();
            MarkerDistances.Clear();

            FairwayWidth = Mathf.Clamp(fairwayWidth, 15f, 120f);
            GreenWidth = Mathf.Clamp(greenWidth, 4f, 40f);

            // Ground (the only collider): dusty desert scrub.
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Range_Ground";
            ground.transform.SetParent(transform, false);
            ground.transform.localScale = new Vector3(400f, 1f, 400f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;
            built.Add(ground);

            // Rough strips, then the striped fairway on top.
            AddMesh("Range_Rough", BuildRough(), roughMat, 0.012f);
            AddMesh("Range_Fairway", BuildFairway(), fairwayMat, 0.024f);

            // Tee mat.
            GameObject mat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mat.name = "Range_TeeMat";
            mat.transform.SetParent(transform, false);
            mat.transform.localPosition = new Vector3(0f, 0.02f, 0.2f);
            mat.transform.localScale = new Vector3(1.5f, 0.03f, 1.5f);
            Decorate(mat, teeMat);

            for (float d = 50f; d <= MaxTarget + 1f; d += 50f) MarkerDistances.Add(d);
            if (AppSettings.Current.rangeDistanceLines)
                for (float d = 50f; d <= MaxTarget + 1f; d += 50f)
                {
                    GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    line.name = "Range_Line_" + d;
                    line.transform.SetParent(transform, false);
                    float w = LeftEdge(d) + RightEdge(d);
                    line.transform.localPosition = new Vector3((RightEdge(d) - LeftEdge(d)) * 0.5f, 0.03f, d);
                    line.transform.localScale = new Vector3(w, 0.004f, 0.25f);
                    Decorate(line, lineMat);
                }

            // Foothills and mountains on the horizon.
            AddMesh("Range_Foothills", BuildMountains(950f, 1500f, 35f, 120f, 17, true), mountainMat, 0f);
            AddMesh("Range_Mountains", BuildMountains(2300f, 3600f, 260f, 620f, 5, false), mountainMat, 0f);

            // Dashed aim line from the mat to the target.
            GameObject lineObject = new GameObject("Range_AimLine");
            lineObject.transform.SetParent(transform, false);
            lineObject.layer = GroundProbe.IgnoreRaycastLayer;
            aimLine = lineObject.AddComponent<LineRenderer>();
            Shader sprite = Shader.Find("Sprites/Default");
            aimLine.material = new Material(sprite != null ? sprite : Shader.Find("Unlit/Transparent")) { mainTexture = DashTexture() };
            aimLine.textureMode = LineTextureMode.Tile;
            aimLine.startColor = new Color(1f, 1f, 1f, 0.9f);
            aimLine.endColor = new Color(1f, 1f, 1f, 0.75f);
            aimLine.startWidth = 0.07f;
            aimLine.endWidth = 0.28f;
            aimLine.alignment = LineAlignment.View;
            aimLine.positionCount = 2;
            aimLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            built.Add(lineObject);

            SetTarget(new Vector3(0f, 0f, Mathf.Clamp(targetMeters, MinTarget, MaxTarget)));
            ApplySky();
        }

        /// <summary>Moves the target green and flag (used by the randomizer).</summary>
        public void SetTarget(Vector3 position)
        {
            foreach (GameObject go in targetParts) if (go != null) Destroy(go);
            targetParts.Clear();
            position.y = 0f;
            TargetPosition = position;

            float r = GreenWidth * 0.5f;
            targetParts.Add(AddMesh("Range_GreenFringe", Disc(r + 1.4f, 48), fringeMat, 0.03f, position));
            targetParts.Add(AddMesh("Range_Green", Disc(r, 48), greenMat, 0.036f, position));
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Range_FlagPole";
            pole.transform.SetParent(transform, false);
            pole.transform.localPosition = position + Vector3.up * 1.2f;
            pole.transform.localScale = new Vector3(0.04f, 1.2f, 0.04f);
            Decorate(pole, poleMat);
            targetParts.Add(pole);
            GameObject flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flag.name = "Range_Flag";
            flag.transform.SetParent(transform, false);
            flag.transform.localPosition = position + new Vector3(0.34f, 2.18f, 0f);
            flag.transform.localScale = new Vector3(0.64f, 0.4f, 0.015f);
            Decorate(flag, flagMat);
            targetParts.Add(flag);

            if (aimLine != null)
            {
                aimLine.SetPosition(0, TeePosition + new Vector3(0f, 0.05f, 1.2f));
                aimLine.SetPosition(1, position + Vector3.up * 0.05f);
                // One dash every ~2.5 m.
                aimLine.material.mainTextureScale = new Vector2(Mathf.Max(1f, position.magnitude / 2.5f), 1f);
            }
        }

        /// <summary>Shows or hides the target green, flag and aim line (mini games draw their own targets).</summary>
        public void SetTargetVisible(bool visible)
        {
            foreach (GameObject go in targetParts) if (go != null) go.SetActive(visible);
            if (aimLine != null) aimLine.enabled = visible;
        }

        public void RandomTarget(float maxDistance)
        {
            float distance = Random.Range(Mathf.Max(MinTarget, 40f), Mathf.Clamp(maxDistance, 60f, MaxTarget));
            float side = Random.Range(-FairwayWidth * 0.3f, FairwayWidth * 0.3f);
            SetTarget(new Vector3(side, 0f, distance));
        }

        // ------------------------------------------------------------ Meshes

        private GameObject AddMesh(string name, Mesh mesh, Material material, float lift, Vector3 offset = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = offset + Vector3.up * lift;
            // Default layer (no collider): shows on the top-down range map.
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            built.Add(go);
            return go;
        }

        private void Decorate(GameObject go, Material material)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            built.Add(go);
        }

        /// <summary>Fairway strip with UVs in metres (u across, v along) for the stripe texture.</summary>
        private Mesh BuildFairway()
        {
            const int across = 16;
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            int rows = 0;
            for (float z = RangeStart; z <= RangeEnd; z += Step, rows++)
            {
                float l = -LeftEdge(z), r = RightEdge(z);
                // Rounded ends of the fairway.
                float endShape = z < RangeStart + 12f ? Mathf.Sqrt(Mathf.Clamp01((z - RangeStart) / 12f)) : z > RangeEnd - 30f ? Mathf.Sqrt(Mathf.Clamp01((RangeEnd - z) / 30f)) : 1f;
                float mid = (l + r) * 0.5f, half = (r - l) * 0.5f * Mathf.Max(0.05f, endShape);
                for (int i = 0; i <= across; i++)
                {
                    float x = mid - half + 2f * half * i / across;
                    v.Add(new Vector3(x, 0f, z));
                    uv.Add(new Vector2(x, z));
                }
            }
            Grid(t, rows, across);
            return ToMesh("GolfSimZA_RangeFairway", v, uv, t, null);
        }

        private Mesh BuildRough()
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int side = -1; side <= 1; side += 2)
            {
                int start = v.Count, rows = 0;
                for (float z = RangeStart - 20f; z <= RangeEnd + 40f; z += Step, rows++)
                {
                    float edge = side < 0 ? -LeftEdge(Mathf.Clamp(z, RangeStart, RangeEnd)) : RightEdge(Mathf.Clamp(z, RangeStart, RangeEnd));
                    float outer = edge + side * RoughWidth(z);
                    float inner = edge - side * 2f; // tuck under the fairway edge
                    float a = side < 0 ? outer : inner, b = side < 0 ? inner : outer;
                    for (int i = 0; i <= 4; i++)
                    {
                        float x = Mathf.Lerp(a, b, i / 4f);
                        v.Add(new Vector3(x, 0f, z));
                        uv.Add(new Vector2(x, z));
                    }
                }
                var local = new List<int>();
                Grid(local, rows, 4);
                foreach (int i in local) t.Add(i + start);
            }
            // Rough across the far end behind the fairway.
            {
                int start = v.Count;
                for (int j = 0; j <= 1; j++)
                    for (int i = 0; i <= 10; i++)
                    {
                        float x = Mathf.Lerp(-LeftEdge(RangeEnd) - 20f, RightEdge(RangeEnd) + 20f, i / 10f);
                        float z = j == 0 ? RangeEnd - 40f : RangeEnd + 40f;
                        v.Add(new Vector3(x, 0f, z));
                        uv.Add(new Vector2(x, z));
                    }
                for (int i = 0; i < 10; i++)
                {
                    int a = start + i, b = start + 11 + i;
                    t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            }
            return ToMesh("GolfSimZA_RangeRough", v, uv, t, null);
        }

        private static void Grid(List<int> t, int rows, int across)
        {
            int w = across + 1;
            for (int r = 0; r < rows - 1; r++)
                for (int i = 0; i < across; i++)
                {
                    int a = r * w + i, b = a + w;
                    t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
        }

        private static Mesh Disc(float radius, int segments)
        {
            var v = new List<Vector3> { Vector3.zero };
            var uv = new List<Vector2> { Vector2.zero };
            var t = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                v.Add(p);
                uv.Add(new Vector2(p.x, p.z));
                if (i > 0) t.AddRange(new[] { 0, i + 1, i });
            }
            return ToMesh("GolfSimZA_Disc", v, uv, t, null);
        }

        /// <summary>
        /// A ring of mountains around the far end of the range (a polar height field): ridged noise
        /// peaks, sandy-brown slopes with a little green scrub low down (foothills) or light rock high up.
        /// </summary>
        private static Mesh BuildMountains(float near, float far, float minPeak, float maxPeak, int seed, bool foothills)
        {
            const int angles = 140, radials = 14;
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var c = new List<Color>();
            var t = new List<int>();
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            float a0 = -105f * Mathf.Deg2Rad, a1 = 105f * Mathf.Deg2Rad;
            for (int j = 0; j <= radials; j++)
            {
                float rt = j / (float)radials;
                float radius = Mathf.Lerp(near, far, rt);
                for (int i = 0; i <= angles; i++)
                {
                    float at = i / (float)angles;
                    float a = Mathf.Lerp(a0, a1, at);
                    float nx = at * 16f + seed, ny = rt * 3.0f + seed * 0.37f;
                    // Ridged multi-octave noise: sharp crests, valleys and gullies.
                    float h = 0f, amp = 0.55f, freq = 1f;
                    for (int o = 0; o < 4; o++)
                    {
                        float r = 1f - Mathf.Abs(Mathf.PerlinNoise(nx * freq, ny * freq) * 2f - 1f);
                        h += r * r * amp;
                        amp *= 0.5f;
                        freq *= 2.1f;
                    }
                    h = Mathf.Clamp01(h * 1.15f);
                    float envelope = Mathf.Sin(rt * Mathf.PI) * (0.35f + 0.65f * Mathf.PerlinNoise(at * 4f + seed, 7.1f));
                    float height = Mathf.Lerp(minPeak, maxPeak, h) * envelope;
                    if (j == 0 || j == radials) height = -15f;
                    var p = new Vector3(Mathf.Sin(a) * radius, height, Mathf.Cos(a) * radius + 200f);
                    v.Add(p);
                    uv.Add(new Vector2(p.x * 0.01f, p.z * 0.01f));

                    float k = Mathf.InverseLerp(0f, maxPeak, height);
                    Color col = foothills
                        ? Color.Lerp(new Color(0.40f, 0.42f, 0.28f), new Color(0.58f, 0.50f, 0.38f), k + Mathf.PerlinNoise(nx * 4f, ny * 4f) * 0.3f)
                        : Color.Lerp(new Color(0.66f, 0.48f, 0.32f), new Color(0.86f, 0.74f, 0.60f), Mathf.Clamp01(k * 1.2f + Mathf.PerlinNoise(nx * 5f, ny * 3f) * 0.3f - 0.15f));
                    c.Add(linear ? col.linear : col);
                }
            }
            int w = angles + 1;
            for (int j = 0; j < radials; j++)
                for (int i = 0; i < angles; i++)
                {
                    int a = j * w + i, b = a + w;
                    t.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
            return ToMesh(foothills ? "GolfSimZA_Foothills" : "GolfSimZA_Mountains", v, uv, t, c);
        }

        private static Mesh ToMesh(string name, List<Vector3> v, List<Vector2> uv, List<int> t, List<Color> colors)
        {
            var mesh = new Mesh { name = name, indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            if (colors != null) mesh.SetColors(colors);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------ Materials and textures

        private static void EnsureMaterials()
        {
            if (fairwayMat != null) return;
            // Fairway: lengthwise mowing stripes (8 m wide) with a soft crosswise variation.
            fairwayMat = Mat(Color.white, 0.08f, StripeTexture(new Color(0.38f, 0.56f, 0.22f), new Color(0.30f, 0.47f, 0.17f)), new Vector2(1f / 16f, 1f / 40f));
            roughMat = Mat(Color.white, 0.02f, NoiseTexture(new Color(0.27f, 0.40f, 0.15f), new Color(0.34f, 0.44f, 0.19f), 11), new Vector2(1f / 9f, 1f / 9f));
            groundMat = Mat(Color.white, 0.0f, NoiseTexture(new Color(0.56f, 0.49f, 0.34f), new Color(0.44f, 0.43f, 0.27f), 23), new Vector2(40f, 40f));
            greenMat = Mat(new Color(0.40f, 0.66f, 0.30f), 0.25f, null, Vector2.one);
            fringeMat = Mat(new Color(0.35f, 0.57f, 0.24f), 0.15f, null, Vector2.one);
            lineMat = Mat(new Color(0.95f, 0.96f, 0.95f, 1f), 0.1f, null, Vector2.one);
            flagMat = Mat(new Color(0.92f, 0.15f, 0.12f), 0.2f, null, Vector2.one);
            poleMat = Mat(new Color(0.96f, 0.96f, 0.96f), 0.4f, null, Vector2.one);
            teeMat = Mat(new Color(0.10f, 0.30f, 0.16f), 0.05f, null, Vector2.one);
            Shader vc = Resources.Load<Shader>("GolfSimZA_VertexColorLit");
            mountainMat = vc != null && vc.isSupported ? new Material(vc) : Mat(new Color(0.60f, 0.52f, 0.42f), 0f, null, Vector2.one);
            if (vc != null && vc.isSupported)
            {
                mountainMat.mainTexture = NoiseTexture(Color.white, new Color(0.6f, 0.6f, 0.6f), 5);
                mountainMat.SetFloat("_DetailScale", 3f);
            }
        }

        private static Material Mat(Color color, float smoothness, Texture2D texture, Vector2 tiling)
        {
            var m = new Material(Shader.Find("Standard")) { color = color };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (texture != null)
            {
                m.mainTexture = texture;
                m.mainTextureScale = tiling;
            }
            return m;
        }

        /// <summary>Two stripes (light / dark) across u, with a little grain.</summary>
        private static Texture2D StripeTexture(Color light, Color dark)
        {
            const int W = 64, H = 64;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8, name = "GolfSimZA_RangeStripes" };
            var px = new Color[W * H];
            var rnd = new System.Random(3);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W;
                    // Soft edge between the two stripes.
                    float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Abs(u - 0.5f) - 0.22f) / 0.06f));
                    Color c = Color.Lerp(dark, light, s);
                    float grain = 0.94f + (float)rnd.NextDouble() * 0.12f;
                    float wave = 1f + 0.035f * Mathf.Sin(y / (float)H * Mathf.PI * 2f);
                    px[y * W + x] = c * grain * wave;
                }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        private static Texture2D NoiseTexture(Color a, Color b, int seed)
        {
            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4, name = "GolfSimZA_RangeNoise" };
            var px = new Color[S * S];
            var rnd = new System.Random(seed);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    // Tileable noise from sines of several frequencies plus grain.
                    float fx = x / (float)S * Mathf.PI * 2f, fy = y / (float)S * Mathf.PI * 2f;
                    float n = 0.5f + 0.18f * Mathf.Sin(fx * 2f + seed) * Mathf.Cos(fy * 3f) + 0.14f * Mathf.Sin(fx * 5f + fy * 4f + seed)
                              + 0.1f * Mathf.Sin(fx * 11f - fy * 9f) + ((float)rnd.NextDouble() - 0.5f) * 0.22f;
                    px[y * S + x] = Color.Lerp(a, b, Mathf.Clamp01(n));
                }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        private static Texture2D dash;
        private static Texture2D DashTexture()
        {
            if (dash != null) return dash;
            dash = new Texture2D(32, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "GolfSimZA_Dash" };
            var px = new Color[32 * 4];
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 32; x++)
                    px[y * 32 + x] = x < 18 ? Color.white : new Color(1f, 1f, 1f, 0f);
            dash.SetPixels(px);
            dash.Apply();
            return dash;
        }

        private static void ApplySky()
        {
            Shader sky = Shader.Find("Skybox/Procedural");
            if (sky != null && (RenderSettings.skybox == null || RenderSettings.skybox.shader != sky))
            {
                var material = new Material(sky);
                if (material.HasProperty("_AtmosphereThickness")) material.SetFloat("_AtmosphereThickness", 0.75f);
                if (material.HasProperty("_Exposure")) material.SetFloat("_Exposure", 1.2f);
                if (material.HasProperty("_SkyTint")) material.SetColor("_SkyTint", new Color(0.45f, 0.55f, 0.75f));
                RenderSettings.skybox = material;
            }
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.farClipPlane = Mathf.Max(6000f, AppSettings.Current.drawDistanceMeters);
            }
            // Light dry-air haze: the mountains fade a little, like a desert practice ground.
            RenderSettings.fogColor = new Color(0.72f, 0.78f, 0.86f);
            FlightPresentation.ApplyFog();
            if (RenderSettings.fog)
            {
                // Range: haze starts past the target and the mountains stay visible through it.
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = 1200f;
                RenderSettings.fogEndDistance = 14000f;
            }
            // Sun from behind the golfer and a little to the right: the mountains are lit, not a silhouette.
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) l.transform.rotation = Quaternion.Euler(l.transform.eulerAngles.x, 28f, 0f);
            GameOptions.ApplyLighting();
        }
    }
}
