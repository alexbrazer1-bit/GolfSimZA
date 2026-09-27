using UnityEngine;

namespace GolfSimZA.UI
{
    /// <summary>Shared menu colours and IMGUI styles (must be used from OnGUI).</summary>
    public static class GolfSimTheme
    {
        public static readonly Color Navy = new Color(0.025f, 0.10f, 0.14f);
        public static readonly Color Panel = new Color(0.055f, 0.16f, 0.20f);
        public static readonly Color PanelLight = new Color(0.10f, 0.22f, 0.27f);
        public static readonly Color Accent = new Color(0.10f, 0.60f, 0.92f);
        public static readonly Color Good = new Color(0.30f, 0.80f, 0.45f);
        public static readonly Color Warning = new Color(1.00f, 0.72f, 0.20f);
        public static readonly Color Bad = new Color(0.95f, 0.35f, 0.30f);
        public static readonly Color Muted = new Color(0.68f, 0.77f, 0.80f);

        public static GUIStyle Title, Heading, Subtitle, Label, Body, Button, AccentButton, SmallButton, Card, SelectedCard, TextField, Overlay, Good_, Warning_, Bad_;
        private static bool ready;

        public static void Ensure()
        {
            if (ready && Title != null) return;
            Title = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            Heading = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            Subtitle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, normal = { textColor = Muted } };
            Label = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.80f, 0.86f, 0.88f) } };
            Body = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, normal = { textColor = new Color(0.86f, 0.90f, 0.92f) } };
            Button = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold, fixedHeight = 40, margin = new RectOffset(4, 4, 4, 4) };
            AccentButton = new GUIStyle(Button) { normal = { background = Tex(Accent), textColor = Color.white }, hover = { background = Tex(new Color(0.16f, 0.68f, 1f)), textColor = Color.white } };
            SmallButton = new GUIStyle(Button) { fontSize = 12, fixedHeight = 30 };
            Card = new GUIStyle(GUI.skin.box) { padding = new RectOffset(16, 16, 12, 12), margin = new RectOffset(0, 0, 4, 4), normal = { background = Tex(Panel) } };
            SelectedCard = new GUIStyle(Card) { normal = { background = Tex(PanelLight) } };
            TextField = new GUIStyle(GUI.skin.textField) { fontSize = 14, fixedHeight = 34, padding = new RectOffset(8, 8, 8, 8) };
            Overlay = new GUIStyle(GUI.skin.box) { padding = new RectOffset(24, 24, 20, 20), normal = { background = Tex(new Color(0.02f, 0.07f, 0.10f, 0.98f)) } };
            Good_ = new GUIStyle(Body) { fontStyle = FontStyle.Bold, normal = { textColor = Good } };
            Warning_ = new GUIStyle(Body) { fontStyle = FontStyle.Bold, normal = { textColor = Warning } };
            Bad_ = new GUIStyle(Body) { fontStyle = FontStyle.Bold, normal = { textColor = Bad } };
            ready = true;
        }

        public static Texture2D Tex(Color color)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
