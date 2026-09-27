using GolfSimZA.Core;
using UnityEngine;

namespace GolfSimZA.Visual
{
    /// <summary>
    /// Soft white clouds over a plain (procedural) sky. The clouds are painted in code, lie on a
    /// sky dome that moves with the camera and fade out towards the horizon.
    /// Courses that bring their own sky picture keep it (their sky already has clouds).
    /// </summary>
    public sealed class CloudDome : MonoBehaviour
    {
        private const int Segments = 40;
        private const int Rings = 18;
        private static Texture2D cloudTexture;
        private MeshRenderer meshRenderer;
        private Camera cam;

        /// <summary>Adds clouds when the sky is the procedural one (range, demo holes, some courses).</summary>
        public static void Ensure(Camera camera)
        {
            if (camera == null) return;
            CloudDome existing = FindFirstObjectByType<CloudDome>();
            bool plainSky = RenderSettings.skybox == null || RenderSettings.skybox.shader == null || RenderSettings.skybox.shader.name.Contains("Procedural");
            if (!plainSky)
            {
                if (existing != null) existing.gameObject.SetActive(false);
                return;
            }
            if (existing == null)
            {
                var go = new GameObject("GolfSimZA_Clouds");
                existing = go.AddComponent<CloudDome>();
            }
            existing.cam = camera;
            existing.gameObject.SetActive(true);
        }

        private void Awake()
        {
            gameObject.layer = 2; // Ignore Raycast: not ground, not in the hole map
            var filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildDome();
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            Shader shader = Shader.Find("Sprites/Default");
            var material = new Material(shader) { mainTexture = CloudTexture(), renderQueue = 2990 };
            material.mainTexture.wrapMode = TextureWrapMode.Repeat;
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            AppSettings.Changed += ApplySetting;
            ApplySetting();
        }

        private void OnDestroy() => AppSettings.Changed -= ApplySetting;

        private void ApplySetting()
        {
            if (meshRenderer != null) meshRenderer.enabled = AppSettings.Current.clouds;
        }

        private void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            float radius = Mathf.Clamp(cam.farClipPlane * 0.85f, 400f, 2500f);
            transform.position = cam.transform.position;
            transform.localScale = Vector3.one * radius;
            // Clouds drift slowly with time.
            Material m = meshRenderer.sharedMaterial;
            m.mainTextureOffset = new Vector2(Time.time * 0.0015f, Time.time * 0.0006f);
        }

        /// <summary>Hemisphere, faces pointing inwards. UVs project the cloud texture onto a flat
        /// ceiling so clouds shrink in perspective towards the horizon.</summary>
        private static Mesh BuildDome()
        {
            var vertices = new Vector3[(Segments + 1) * (Rings + 1)];
            var uvs = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            for (int r = 0; r <= Rings; r++)
            {
                float elevation = Mathf.Lerp(-0.03f, Mathf.PI * 0.5f, r / (float)Rings);
                for (int s = 0; s <= Segments; s++)
                {
                    float azimuth = s / (float)Segments * Mathf.PI * 2f;
                    Vector3 d = new Vector3(Mathf.Cos(elevation) * Mathf.Cos(azimuth), Mathf.Sin(elevation), Mathf.Cos(elevation) * Mathf.Sin(azimuth));
                    int i = r * (Segments + 1) + s;
                    vertices[i] = d;
                    float h = Mathf.Max(0.06f, d.y);
                    uvs[i] = new Vector2(d.x / h, d.z / h) * 0.35f;
                    // Fade to nothing at the horizon.
                    colors[i] = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.30f, d.y)));
                }
            }
            var triangles = new System.Collections.Generic.List<int>();
            for (int r = 0; r < Rings; r++)
                for (int s = 0; s < Segments; s++)
                {
                    int a = r * (Segments + 1) + s, b = a + Segments + 1;
                    // Inward facing.
                    triangles.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
            var mesh = new Mesh { name = "GolfSimZA_CloudDome", vertices = vertices, uv = uvs, colors = colors };
            mesh.SetTriangles(triangles, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            return mesh;
        }

        /// <summary>Tileable fluffy clouds (several layers of noise), white with soft grey bases.</summary>
        private static Texture2D CloudTexture()
        {
            if (cloudTexture != null) return cloudTexture;
            const int size = 256;
            cloudTexture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "GolfSimZA_Clouds", wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    // Blend four shifted samples so the texture repeats without seams.
                    float n = Tile(u, v);
                    float cover = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.50f, 0.78f, n));
                    float shade = Mathf.Lerp(0.82f, 1f, Mathf.InverseLerp(0.55f, 0.9f, n));
                    pixels[y * size + x] = new Color(shade, shade, Mathf.Min(1f, shade + 0.03f), cover * 0.92f);
                }
            cloudTexture.SetPixels(pixels);
            cloudTexture.Apply(true);
            return cloudTexture;
        }

        private static float Tile(float u, float v)
        {
            return Fbm(u, v) * (1 - u) * (1 - v) + Fbm(u - 1, v) * u * (1 - v) + Fbm(u, v - 1) * (1 - u) * v + Fbm(u - 1, v - 1) * u * v;
        }

        private static float Fbm(float u, float v)
        {
            float sum = 0f, amp = 0.5f, freq = 4f;
            for (int o = 0; o < 5; o++)
            {
                sum += amp * Mathf.PerlinNoise(u * freq + 17.3f, v * freq + 5.1f);
                amp *= 0.5f;
                freq *= 2f;
            }
            return sum / 0.97f;
        }
    }
}
