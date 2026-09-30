using GolfSimZA.Core;
using UnityEngine;

namespace GolfSimZA.UI
{
    /// <summary>
    /// Small Garmin R10 badge for the play screens: a coloured dot (green = connected and ready,
    /// amber = connected / waiting, red = not connected), "R10" and a battery icon with the level.
    /// </summary>
    public static class R10Pill
    {
        public const float Width = 150f, Height = 38f;
        private static GUIStyle text, small;
        private static Texture2D dot;

        public static void Draw(float x, float y)
        {
            GolfSimTheme.Ensure();
            if (text == null)
            {
                text = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } };
                small = new GUIStyle(text) { fontSize = 12, alignment = TextAnchor.MiddleRight };
                dot = GolfSimTheme.Rounded(Color.white, 6);
            }
            Rect r = new Rect(x, y, Width, Height);
            GUI.Box(r, GUIContent.none, GolfSimTheme.Card);

            bool connected = LaunchMonitorStatus.Listening && LaunchMonitorStatus.BridgeConnected && (!LaunchMonitorStatus.R10Known || LaunchMonitorStatus.R10Connected);
            bool ready = connected && LaunchMonitorStatus.R10Known && LaunchMonitorStatus.Ready;
            Color c = ready ? GolfSimTheme.Good : connected ? GolfSimTheme.Warning : GolfSimTheme.Bad;
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x + 10f, r.y + 13f, 12f, 12f), dot);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 28f, r.y, 40f, r.height), "R10", text);

            // Battery icon: frame, tip and fill.
            int battery = LaunchMonitorStatus.R10Known ? LaunchMonitorStatus.Battery : -1;
            Rect b = new Rect(r.x + 66f, r.y + 12f, 30f, 14f);
            Color frame = new Color(1f, 1f, 1f, 0.85f);
            GUI.color = frame;
            GUI.DrawTexture(new Rect(b.x, b.y, b.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(b.x, b.yMax - 2f, b.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(b.x, b.y, 2f, b.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(b.xMax - 2f, b.y, 2f, b.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(b.xMax, b.y + 4f, 3f, b.height - 8f), Texture2D.whiteTexture);
            if (battery >= 0)
            {
                float level = battery / 100f;
                GUI.color = level > 0.5f ? GolfSimTheme.Good : level > 0.2f ? GolfSimTheme.Warning : GolfSimTheme.Bad;
                GUI.DrawTexture(new Rect(b.x + 3f, b.y + 3f, (b.width - 6f) * Mathf.Clamp(level, 0.05f, 1f), b.height - 6f), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 96f, r.y, r.width - 104f, r.height), battery >= 0 ? battery + "%" : "—", small);
        }
    }
}
