using System.Collections.Generic;
using UnityEngine;

namespace GolfSimZA.UI
{
    /// <summary>
    /// Small pictures of golf clubs for the club tile and the club grid: driver, fairway wood,
    /// hybrid, iron and wedge. Drawn once in code (no image files needed) and cached; the club
    /// face opens with the club's loft so a wedge looks different from a long iron.
    /// </summary>
    public static class ClubIcons
    {
        public enum Kind { Driver, Wood, Hybrid, Iron, Wedge }

        private const int Size = 96;
        private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        /// <summary>The kind of club from its name (and loft when the name does not say).</summary>
        public static Kind KindOf(string clubName, float loft)
        {
            string n = (clubName ?? "").ToLowerInvariant();
            if (n.Contains("driver") && !n.Contains("iron")) return Kind.Driver;
            if (n.Contains("wood")) return Kind.Wood;
            if (n.Contains("hybrid") || n.Contains("rescue") || n.Contains("utility")) return Kind.Hybrid;
            if (n.Contains("wedge") || n.Contains("chipper") || n.Contains("bump")) return Kind.Wedge;
            if (n.Contains("iron")) return Kind.Iron;
            if (loft < 13f) return Kind.Driver;
            if (loft < 19f) return Kind.Wood;
            if (loft < 24f) return Kind.Hybrid;
            return loft < 44f ? Kind.Iron : Kind.Wedge;
        }

        /// <summary>Picture of the club (cached per kind and loft step).</summary>
        public static Texture2D For(string clubName, float loft)
        {
            Kind kind = KindOf(clubName, loft);
            int loftStep = Mathf.RoundToInt(Mathf.Clamp(loft, 6f, 64f) / 4f);
            string key = kind + "_" + loftStep;
            if (cache.TryGetValue(key, out Texture2D tex) && tex != null) return tex;
            tex = Paint(kind, loftStep * 4f);
            cache[key] = tex;
            return tex;
        }

        // ------------------------------------------------------------------ painting

        private static Texture2D Paint(Kind kind, float loft)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "GolfSimZA_ClubIcon_" + kind,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color[Size * Size];

            // Picture coordinates: x right, y DOWN (0..96). The shaft runs from the grip at the
            // top right down to the hosel; the head sits bottom left, face towards the viewer.
            Vector2 gripTop = new Vector2(86f, 4f), gripEnd = new Vector2(75f, 26f), hosel = new Vector2(56f, 66f);

            Color grip = new Color(0.08f, 0.08f, 0.09f, 1f);
            Color shaft = new Color(0.78f, 0.80f, 0.83f, 1f);
            Color steel = new Color(0.80f, 0.83f, 0.86f, 1f);
            Color steelDark = new Color(0.52f, 0.56f, 0.60f, 1f);
            Color woodHead = new Color(0.13f, 0.14f, 0.16f, 1f);
            Color woodRim = new Color(0.55f, 0.58f, 0.62f, 1f);
            Color accent = GolfSimTheme.Accent;

            // Face tilt grows with loft (a wedge leans back more than a 4 iron).
            float tilt = Mathf.Lerp(-4f, -16f, Mathf.InverseLerp(10f, 60f, loft)) * Mathf.Deg2Rad;

            for (int py = 0; py < Size; py++)
            {
                for (int px = 0; px < Size; px++)
                {
                    Vector2 p = new Vector2(px + 0.5f, py + 0.5f);
                    Color c = new Color(0f, 0f, 0f, 0f);

                    // Shaft and grip.
                    c = Over(c, shaft, Coverage(SegmentDistance(p, gripEnd, hosel) - 1.3f));
                    c = Over(c, grip, Coverage(SegmentDistance(p, gripTop, gripEnd) - 3.2f));

                    switch (kind)
                    {
                        case Kind.Driver:
                        case Kind.Wood:
                        case Kind.Hybrid:
                        {
                            float rx = kind == Kind.Driver ? 27f : kind == Kind.Wood ? 21f : 17f;
                            float ry = kind == Kind.Driver ? 15f : kind == Kind.Wood ? 11.5f : 10f;
                            Vector2 centre = new Vector2(hosel.x - rx + 3f, hosel.y + ry * 0.55f);
                            // Hosel neck.
                            c = Over(c, woodHead, Coverage(SegmentDistance(p, hosel + new Vector2(0f, -5f), hosel + new Vector2(-2f, 3f)) - 2.6f));
                            float d = EllipseDistance(p - centre, rx, ry);
                            c = Over(c, woodRim, Coverage(d));
                            c = Over(c, woodHead, Coverage(d + 1.6f));
                            // Face: the lower part of the head, a lighter band with grooves.
                            float face = Mathf.Max(d + 3.5f, (centre.y + ry * 0.05f) - p.y);
                            c = Over(c, new Color(0.30f, 0.32f, 0.36f, 1f), Coverage(face));
                            // Crown shine and a coloured alignment mark.
                            float shine = EllipseDistance(p - (centre + new Vector2(-rx * 0.25f, -ry * 0.45f)), rx * 0.45f, ry * 0.18f);
                            c = Over(c, new Color(1f, 1f, 1f, 0.22f), Coverage(shine));
                            c = Over(c, accent, Coverage(EllipseDistance(p - (centre + new Vector2(0f, -ry * 0.62f)), 2.2f, 1.6f)));
                            break;
                        }
                        default:
                        {
                            bool wedge = kind == Kind.Wedge;
                            float halfW = wedge ? 19f : 21f, halfH = wedge ? 11f : 8.5f;
                            Vector2 centre = new Vector2(hosel.x - halfW + 2f, hosel.y + halfH * 0.9f);
                            Vector2 local = Rotate(p - centre, -tilt);
                            // Hosel.
                            c = Over(c, steelDark, Coverage(SegmentDistance(p, hosel + new Vector2(0f, -6f), hosel + new Vector2(-1f, 4f)) - 2.4f));
                            // Blade: rounded box, toe a bit taller than the heel.
                            float toeLift = Mathf.InverseLerp(halfW, -halfW, local.x) * (wedge ? 4f : 3f);
                            Vector2 q = new Vector2(local.x, local.y + toeLift * 0.5f);
                            float d = RoundedBoxDistance(q, new Vector2(halfW, halfH + toeLift * 0.5f), wedge ? 7f : 4.5f);
                            c = Over(c, steelDark, Coverage(d));
                            c = Over(c, steel, Coverage(d + 1.4f));
                            // Grooves on the face.
                            if (d < -2.5f)
                            {
                                float groove = Mathf.Repeat(local.y + 40f, wedge ? 3.4f : 4f);
                                if (groove < 0.9f && local.x > -halfW + 5f && local.x < halfW - 4f)
                                    c = Over(c, new Color(0.45f, 0.48f, 0.52f, 1f), 0.9f);
                            }
                            // Sole line.
                            float sole = Mathf.Abs(local.y - (halfH - 1.5f)) - 0.8f;
                            if (d < -0.5f) c = Over(c, new Color(0.38f, 0.41f, 0.45f, 1f), Coverage(sole));
                            break;
                        }
                    }
                    pixels[(Size - 1 - py) * Size + px] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private static float Coverage(float signedDistance) => Mathf.Clamp01(0.5f - signedDistance);

        private static Color Over(Color under, Color top, float amount)
        {
            float a = top.a * amount;
            if (a <= 0f) return under;
            float outA = a + under.a * (1f - a);
            if (outA <= 0f) return new Color(0f, 0f, 0f, 0f);
            Color rgb = (top * a + under * under.a * (1f - a)) / outA;
            rgb.a = outA;
            return rgb;
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Approximate signed distance to an ellipse (pixels, negative inside).</summary>
        private static float EllipseDistance(Vector2 p, float rx, float ry)
        {
            float k = Mathf.Sqrt((p.x * p.x) / (rx * rx) + (p.y * p.y) / (ry * ry));
            if (k < 0.0001f) return -Mathf.Min(rx, ry);
            Vector2 grad = new Vector2(p.x / (rx * rx), p.y / (ry * ry));
            return (k * k - 1f) / (2f * Mathf.Max(0.0001f, grad.magnitude));
        }

        private static float RoundedBoxDistance(Vector2 p, Vector2 halfSize, float radius)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - halfSize + new Vector2(radius, radius);
            Vector2 outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
            return outside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        private static Vector2 Rotate(Vector2 v, float radians)
        {
            float s = Mathf.Sin(radians), c = Mathf.Cos(radians);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
