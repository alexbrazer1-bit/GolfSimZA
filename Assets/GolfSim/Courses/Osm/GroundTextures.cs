using UnityEngine;

namespace GolfSimZA.Courses.Osm
{
    /// <summary>
    /// Surface pictures for built courses, made in code (no image files): rough, fairway, green,
    /// tee, bunker sand, cart path, fringe and natural veld grass, as one texture array, plus a
    /// large-scale variation texture. Made once and kept.
    /// </summary>
    public static class GroundTextures
    {
        private const int Size = 256;
        private static Texture2DArray layers;
        private static Texture2D macro;

        // Base colour, colour of the variation, fine speckle strength, blade streaks.
        private static readonly Color32[] Base =
        {
            new Color32(62, 92, 30, 255),    // rough
            new Color32(78, 126, 40, 255),   // fairway
            new Color32(82, 140, 48, 255),   // green
            new Color32(80, 128, 42, 255),   // tee
            new Color32(206, 186, 140, 255), // bunker sand
            new Color32(112, 108, 98, 255),  // path
            new Color32(72, 116, 36, 255),   // fringe
            new Color32(112, 106, 60, 255),  // natural veld
        };
        private static readonly Color32[] Alt =
        {
            new Color32(84, 100, 36, 255),
            new Color32(66, 110, 32, 255),
            new Color32(70, 126, 40, 255),
            new Color32(68, 112, 34, 255),
            new Color32(186, 164, 120, 255),
            new Color32(94, 90, 82, 255),
            new Color32(62, 102, 30, 255),
            new Color32(84, 94, 42, 255),
        };
        private static readonly float[] Speckle = { 0.22f, 0.12f, 0.06f, 0.10f, 0.12f, 0.18f, 0.10f, 0.26f };
        private static readonly float[] Blades = { 0.6f, 0.25f, 0.1f, 0.25f, 0f, 0f, 0.35f, 0.8f };

        public static Texture2DArray Layers
        {
            get
            {
                if (layers == null) layers = MakeLayers();
                return layers;
            }
        }

        public static Texture2D Macro
        {
            get
            {
                if (macro == null) macro = MakeMacro();
                return macro;
            }
        }

        private static Texture2DArray MakeLayers()
        {
            var array = new Texture2DArray(Size, Size, OsmSurface.Count, TextureFormat.RGBA32, true, false)
            {
                name = "GolfSimZA_GroundLayers",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8,
                hideFlags = HideFlags.DontSave
            };
            var px = new Color32[Size * Size];
            var random = new System.Random(1977);
            for (int layer = 0; layer < OsmSurface.Count; layer++)
            {
                Color a = Base[layer], b = Alt[layer];
                int seed = layer * 131;
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        // Tileable patchy variation (three octaves).
                        float v = 0.55f * Periodic(x, y, 8, seed) + 0.3f * Periodic(x, y, 16, seed + 7) + 0.15f * Periodic(x, y, 32, seed + 13);
                        Color c = Color.Lerp(a, b, Mathf.SmoothStep(0.25f, 0.75f, v));
                        // Grass blades: short streaks.
                        if (Blades[layer] > 0f)
                        {
                            float streak = Periodic(x, y * 4 % Size, 128, seed + 29);
                            c *= 1f + (streak - 0.5f) * 0.18f * Blades[layer];
                        }
                        // Fine speckle (sand grains, gravel, individual grass tips).
                        float s = (float)random.NextDouble() - 0.5f;
                        c *= 1f + s * Speckle[layer] * 2f;
                        c.a = 1f;
                        px[y * Size + x] = c;
                    }
                array.SetPixels32(px, layer, 0);
            }
            array.Apply(true, true);
            return array;
        }

        private static Texture2D MakeMacro()
        {
            var tex = new Texture2D(Size, Size, TextureFormat.R8, true, true)
            {
                name = "GolfSimZA_GroundMacro",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.DontSave
            };
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float v = 0.5f * Periodic(x, y, 4, 501) + 0.3f * Periodic(x, y, 8, 502) + 0.2f * Periodic(x, y, 16, 503);
                    byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
                    px[y * Size + x] = new Color32(b, b, b, 255);
                }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        /// <summary>Value noise that repeats every Size pixels: cells = lattice cells across the texture.</summary>
        private static float Periodic(int x, int y, int cells, int seed)
        {
            float fx = x * cells / (float)Size, fy = y * cells / (float)Size;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            int x1 = (x0 + 1) % cells, y1 = (y0 + 1) % cells;
            x0 %= cells; y0 %= cells;
            float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed), c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }
    }
}
