using GolfSimZA.Core;
using UnityEngine;

namespace GolfSimZA.UI
{
    /// <summary>
    /// Distances on the enlarged (3×) map, for rounds, the range and mini games:
    /// rings every 50 m / yd round the ball with their numbers, the aim point with "ball → aim" and
    /// "aim → pin / target", and under the mouse a cross with its distance from the ball and to the
    /// pin. All numbers are white tags with dark text so they read on grass, sand and water.
    /// Call between GUI.BeginClip(area) / GUI.EndClip() is NOT needed: positions are in screen GUI space.
    /// </summary>
    public static class MapDistances
    {
        private static GUIStyle tag;
        private static Texture2D fill, edge, white, dot;

        private static void Ensure()
        {
            if (tag != null) return;
            GolfSimTheme.Ensure();
            tag = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow, normal = { textColor = new Color(0.08f, 0.09f, 0.10f) } };
            fill = GolfSimTheme.Tex(new Color(0.98f, 0.98f, 0.97f, 0.95f));
            edge = GolfSimTheme.Tex(new Color(0f, 0f, 0f, 0.4f));
            white = GolfSimTheme.Tex(Color.white);
            dot = GolfSimTheme.Rounded(Color.white, 16);
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>A white number tag centred on p (kept inside the area). Accent = coloured bar on the left.</summary>
        public static void Tag(Rect area, Vector2 p, string text, Color? accent = null, int size = 13)
        {
            Ensure();
            tag.fontSize = size;
            Vector2 s = tag.CalcSize(new GUIContent(text));
            float bar = accent.HasValue ? 5f : 0f;
            float w = s.x + 12f + bar, h = s.y + 4f;
            float x = Mathf.Clamp(p.x - w * 0.5f, area.x + 2f, area.xMax - w - 2f);
            float y = Mathf.Clamp(p.y - h * 0.5f, area.y + 2f, area.yMax - h - 2f);
            var r = new Rect(Mathf.Round(x), Mathf.Round(y), w, h);
            GUI.DrawTexture(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), edge);
            GUI.DrawTexture(r, fill);
            if (accent.HasValue)
            {
                GUI.color = accent.Value;
                GUI.DrawTexture(new Rect(r.x, r.y, bar, r.height), white);
                GUI.color = Color.white;
            }
            GUI.Label(new Rect(r.x + bar, r.y, r.width - bar, r.height), text, tag);
        }

        /// <summary>
        /// Draws the distance overlay on a big map.
        /// <paramref name="toMap"/> world → GUI point; <paramref name="fromMap"/> GUI point → world (false when off the map).
        /// </summary>
        public static void Draw(Rect area, System.Func<Vector3, Vector2> toMap, FromMap fromMap, Vector3 ball, Vector3? aim, Vector3? target, string targetName, bool rings = true)
        {
            Ensure();
            float unit = Units.Metric ? 1f : 0.9144f;
            string u = " " + Units.DistanceUnit;

            // Rings every 50 round the ball, numbered at the top of each ring.
            Vector2 b = toMap(ball);
            float perMetre = Vector2.Distance(b, toMap(ball + Vector3.right * 10f)) / 10f;
            if (rings && perMetre > 0.0001f)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.55f);
                for (int k = 1; k <= 10; k++)
                {
                    float r = k * 50f * unit * perMetre;
                    if (r > area.height * 1.2f) break;
                    for (float a = 0f; a < 360f; a += Mathf.Clamp(360f / (r * 0.6f), 1.2f, 6f))
                    {
                        Vector2 p = b + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * r;
                        if (area.Contains(p)) GUI.DrawTexture(new Rect(p.x - 1f, p.y - 1f, 2f, 2f), white);
                    }
                }
                GUI.color = Color.white;
                for (int k = 1; k <= 10; k++)
                {
                    float r = k * 50f * unit * perMetre;
                    Vector2 p = b + new Vector2(0f, -r);
                    if (!area.Contains(p)) continue;
                    Tag(area, p, (k * 50).ToString(), null, 11);
                }
            }

            // Aim point: ball → aim and aim → pin / target.
            if (aim.HasValue)
            {
                Vector2 a = toMap(aim.Value);
                Tag(area, a + new Vector2(0f, -20f), "AIM " + Units.Distance(Flat(ball, aim.Value)).ToString("0") + u, GolfSimTheme.Gold, 13);
                if (target.HasValue && Flat(aim.Value, target.Value) > 1f)
                {
                    Vector2 t = toMap(target.Value);
                    Tag(area, Vector2.Lerp(a, t, 0.5f), Units.Distance(Flat(aim.Value, target.Value)).ToString("0") + u + " TO " + targetName, null, 12);
                }
            }
            if (target.HasValue)
                Tag(area, toMap(target.Value) + new Vector2(0f, 18f), targetName + " " + Units.Distance(Flat(ball, target.Value)).ToString("0") + u, new Color(0.92f, 0.2f, 0.15f), 12);

            // Under the mouse: a cross and its distances.
            Vector2 m = Event.current.mousePosition;
            if (area.Contains(m) && fromMap != null && fromMap(m, out Vector3 world))
            {
                GUI.color = new Color(1f, 1f, 1f, 0.9f);
                GUI.DrawTexture(new Rect(m.x - 9f, m.y - 1f, 18f, 2f), white);
                GUI.DrawTexture(new Rect(m.x - 1f, m.y - 9f, 2f, 18f), white);
                GUI.color = Color.white;
                string text = Units.Distance(Flat(ball, world)).ToString("0") + u;
                if (target.HasValue) text += "  •  " + targetName + " " + Units.Distance(Flat(world, target.Value)).ToString("0") + u;
                Tag(area, m + new Vector2(0f, 24f), text, new Color(0.25f, 0.62f, 1f), 13);
            }
        }

        public delegate bool FromMap(Vector2 guiPoint, out Vector3 world);
    }
}
