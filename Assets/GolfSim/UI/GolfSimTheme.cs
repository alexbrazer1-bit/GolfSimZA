using UnityEngine;

namespace GolfSimZA.UI
{
    /// <summary>
    /// GolfSim ZA look: dark slate glass panels with rounded corners, "veld green" accent
    /// and "Highveld gold" highlights. All IMGUI styles are created here (use from OnGUI).
    /// </summary>
    public static class GolfSimTheme
    {
        public static readonly Color Navy = new Color(0.035f, 0.075f, 0.095f);
        public static readonly Color Panel = new Color(0.055f, 0.105f, 0.130f, 0.90f);
        public static readonly Color PanelLight = new Color(0.10f, 0.18f, 0.21f, 0.94f);
        public static readonly Color PanelDark = new Color(0.02f, 0.05f, 0.065f, 0.92f);
        public static readonly Color Accent = new Color(0.11f, 0.72f, 0.48f);      // veld green
        public static readonly Color AccentHover = new Color(0.18f, 0.82f, 0.57f);
        public static readonly Color Gold = new Color(0.96f, 0.72f, 0.23f);        // highveld gold
        public static readonly Color Good = new Color(0.35f, 0.85f, 0.55f);
        public static readonly Color Warning = new Color(1.00f, 0.72f, 0.20f);
        public static readonly Color Bad = new Color(0.95f, 0.38f, 0.33f);
        public static readonly Color Muted = new Color(0.66f, 0.76f, 0.80f);
        public static readonly Color Text = new Color(0.93f, 0.96f, 0.97f);

        public static GUIStyle Title, Heading, Subtitle, Label, Body, Button, AccentButton, SmallButton, GhostButton,
            Card, SelectedCard, TextField, Overlay, Good_, Warning_, Bad_, Logo, LogoGold, TileValue, TileLabel,
            TopBarButton, BigTile, Tab, TabActive, Center;
        public static Texture2D White, AccentTex, GoldTex, PanelTex, DarkTex;
        private static bool ready;

        public static void Ensure()
        {
            if (ready && Title != null && White != null) return;

            White = Tex(Color.white);
            AccentTex = Tex(Accent);
            GoldTex = Tex(Gold);
            PanelTex = Rounded(Panel, 10);
            DarkTex = Rounded(PanelDark, 10);

            Title = Lbl(30, FontStyle.Bold, Text);
            Heading = Lbl(18, FontStyle.Bold, Text);
            Subtitle = new GUIStyle(Lbl(14, FontStyle.Normal, Muted)) { wordWrap = true };
            Label = Lbl(13, FontStyle.Bold, new Color(0.78f, 0.86f, 0.89f));
            Body = new GUIStyle(Lbl(13, FontStyle.Normal, new Color(0.86f, 0.90f, 0.92f))) { wordWrap = true };
            Center = new GUIStyle(Lbl(14, FontStyle.Bold, Text)) { alignment = TextAnchor.MiddleCenter };
            Logo = Lbl(30, FontStyle.Bold, Text);
            LogoGold = Lbl(30, FontStyle.Bold, Gold);
            TileValue = new GUIStyle(Lbl(24, FontStyle.Bold, Text)) { alignment = TextAnchor.MiddleCenter };
            TileLabel = new GUIStyle(Lbl(10, FontStyle.Bold, Muted)) { alignment = TextAnchor.UpperCenter };

            Button = MakeButton(Rounded(new Color(0.12f, 0.20f, 0.24f, 0.95f), 8), Rounded(new Color(0.17f, 0.28f, 0.33f, 1f), 8), 14, 40);
            AccentButton = MakeButton(Rounded(Accent, 8), Rounded(AccentHover, 8), 14, 40);
            SmallButton = MakeButton(Rounded(new Color(0.12f, 0.20f, 0.24f, 0.95f), 6), Rounded(new Color(0.17f, 0.28f, 0.33f, 1f), 6), 12, 30);
            GhostButton = MakeButton(Tex(new Color(0, 0, 0, 0)), Rounded(new Color(1f, 1f, 1f, 0.08f), 6), 14, 36);
            TopBarButton = new GUIStyle(GhostButton) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            Tab = MakeButton(Tex(new Color(0, 0, 0, 0)), Rounded(new Color(1f, 1f, 1f, 0.06f), 6), 14, 38);
            TabActive = MakeButton(Rounded(Accent, 6), Rounded(AccentHover, 6), 14, 38);

            BigTile = new GUIStyle(GUI.skin.button)
            {
                fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerCenter,
                padding = new RectOffset(8, 8, 8, 18), border = new RectOffset(12, 12, 12, 12),
                normal = { background = Rounded(new Color(0.03f, 0.08f, 0.10f, 0.70f), 12, new Color(1f, 1f, 1f, 0.85f)), textColor = Color.white },
                hover = { background = Rounded(new Color(0.08f, 0.30f, 0.22f, 0.80f), 12, Gold), textColor = Gold },
                active = { background = Rounded(new Color(0.08f, 0.30f, 0.22f, 0.90f), 12, Gold), textColor = Gold }
            };

            Card = new GUIStyle(GUI.skin.box) { padding = new RectOffset(16, 16, 12, 12), margin = new RectOffset(0, 0, 4, 4), border = new RectOffset(10, 10, 10, 10), normal = { background = PanelTex } };
            SelectedCard = new GUIStyle(Card) { normal = { background = Rounded(PanelLight, 10, Accent) } };
            TextField = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 15, fixedHeight = 36, padding = new RectOffset(10, 10, 9, 8), border = new RectOffset(8, 8, 8, 8),
                normal = { background = Rounded(new Color(0.02f, 0.05f, 0.06f, 0.95f), 8, new Color(1f, 1f, 1f, 0.55f)), textColor = Color.white },
                focused = { background = Rounded(new Color(0.02f, 0.05f, 0.06f, 0.95f), 8, Accent), textColor = Color.white },
                hover = { background = Rounded(new Color(0.02f, 0.05f, 0.06f, 0.95f), 8, Color.white), textColor = Color.white }
            };
            Overlay = new GUIStyle(GUI.skin.box) { padding = new RectOffset(24, 24, 20, 20), border = new RectOffset(12, 12, 12, 12), normal = { background = Rounded(new Color(0.03f, 0.07f, 0.09f, 0.97f), 12) } };
            Good_ = new GUIStyle(Body) { fontStyle = FontStyle.Bold, normal = { textColor = Good } };
            Warning_ = new GUIStyle(Body) { fontStyle = FontStyle.Bold, normal = { textColor = Warning } };
            Bad_ = new GUIStyle(Body) { fontStyle = FontStyle.Bold, normal = { textColor = Bad } };
            ready = true;
        }

        private static GUIStyle Lbl(int size, FontStyle style, Color color)
        {
            return new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = style, normal = { textColor = color }, padding = new RectOffset(0, 0, 0, 0) };
        }

        private static GUIStyle MakeButton(Texture2D normal, Texture2D hover, int size, float height)
        {
            return new GUIStyle(GUI.skin.button)
            {
                fontSize = size, fontStyle = FontStyle.Bold, fixedHeight = height, alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(4, 4, 4, 4), border = new RectOffset(8, 8, 8, 8),
                normal = { background = normal, textColor = Text },
                hover = { background = hover, textColor = Color.white },
                active = { background = hover, textColor = Gold },
                focused = { background = normal, textColor = Text }
            };
        }

        /// <summary>Draws the "GOLFSIM ZA" word mark centred in the rect.</summary>
        public static void DrawLogo(Rect r)
        {
            Ensure();
            var golf = new GUIContent("GOLFSIM ");
            var za = new GUIContent("ZA");
            Vector2 a = Logo.CalcSize(golf);
            Vector2 b = LogoGold.CalcSize(za);
            float x = r.center.x - (a.x + b.x) * 0.5f;
            float y = r.center.y - a.y * 0.5f;
            GUI.Label(new Rect(x, y, a.x, a.y), golf, Logo);
            GUI.Label(new Rect(x + a.x, y, b.x, b.y), za, LogoGold);
            GUI.DrawTexture(new Rect(x, y + a.y + 1f, a.x + b.x, 3f), AccentTex);
        }

        /// <summary>Button whose text shrinks (down to 11 px) so it always fits inside the rect.</summary>
        public static bool FitButton(Rect r, string text, GUIStyle style)
        {
            int original = style.fontSize;
            var content = new GUIContent(text);
            while (style.fontSize > 11 && style.CalcSize(content).x > r.width - 6f) style.fontSize--;
            bool clicked = GUI.Button(r, content, style);
            style.fontSize = original;
            return clicked;
        }

        /// <summary>Shortens text with "…" so it fits the given width in the style.</summary>
        public static string Ellipsize(string text, GUIStyle style, float width)
        {
            if (string.IsNullOrEmpty(text) || style.CalcSize(new GUIContent(text)).x <= width) return text ?? string.Empty;
            int length = text.Length;
            while (length > 1 && style.CalcSize(new GUIContent(text.Substring(0, length) + "…")).x > width) length--;
            return text.Substring(0, length).TrimEnd() + "…";
        }

        /// <summary>Small painted driving-range picture (sky, hills, mown stripes) for tiles.</summary>
        public static Texture2D RangePicture()
        {
            if (rangePicture != null) return rangePicture;
            const int w = 160, h = 100;
            rangePicture = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[w * h];
            float horizon = h * 0.58f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color c;
                    float hill = horizon + 6f * Mathf.Sin(x * 0.06f) + 4f * Mathf.Sin(x * 0.17f + 1f);
                    if (y > hill)
                    {
                        float t = (y - horizon) / (h - horizon);
                        c = Color.Lerp(new Color(0.55f, 0.74f, 0.92f), new Color(0.30f, 0.55f, 0.85f), t);
                    }
                    else if (y > horizon - 1f)
                    {
                        c = new Color(0.27f, 0.42f, 0.28f);
                    }
                    else
                    {
                        // Perspective stripes: stripe index from distance to the horizon.
                        float depth = 1f / Mathf.Max(0.02f, (horizon - y) / horizon);
                        bool light = Mathf.FloorToInt(depth * 1.6f) % 2 == 0;
                        float centre = Mathf.Abs(x - w * 0.5f) / (w * 0.5f);
                        float fairway = Mathf.Lerp(0.12f, 0.9f, (horizon - y) / horizon);
                        c = centre < fairway
                            ? (light ? new Color(0.36f, 0.66f, 0.26f) : new Color(0.29f, 0.57f, 0.21f))
                            : new Color(0.20f, 0.42f, 0.16f);
                    }
                    pixels[y * w + x] = c;
                }
            }
            // Flag near the horizon.
            for (int y = (int)horizon - 4; y < horizon + 8; y++) pixels[y * w + w / 2] = Color.white;
            for (int y = (int)horizon + 4; y < horizon + 8; y++)
                for (int x = w / 2 + 1; x < w / 2 + 6; x++) pixels[y * w + x] = new Color(0.9f, 0.15f, 0.12f);
            rangePicture.SetPixels(pixels);
            rangePicture.Apply();
            return rangePicture;
        }
        private static Texture2D rangePicture;

        public static Texture2D Tex(Color color)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        /// <summary>Rounded-rectangle texture for 9-slicing (style.border = radius).</summary>
        public static Texture2D Rounded(Color fill, int radius, Color? outline = null)
        {
            int size = radius * 2 + 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                    float dy = Mathf.Max(0f, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    Color c = fill;
                    if (outline.HasValue)
                    {
                        float edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));
                        bool inCorner = dx > 0f && dy > 0f;
                        bool onBorder = inCorner ? distance > radius - 2.2f : edge < 2f;
                        if (onBorder) c = outline.Value;
                    }
                    c.a *= alpha;
                    pixels[y * size + x] = c;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
