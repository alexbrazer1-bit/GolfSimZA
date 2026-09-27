using GolfSimZA.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GolfSimZA.UI
{
    /// <summary>
    /// Club selector showing every club in the player's bag with its loft.
    /// Click a club, press 1-9 / 0 for the first ten clubs, or Q / E (Page Up / Page Down)
    /// for the previous / next club. Used by the round HUD and the range HUD.
    /// </summary>
    public static class ClubBar
    {
        private static GUIStyle panel, button, active, loft, label;
        private static Texture2D underline;

        public const float Height = 62f;

        /// <summary>Call from Update (not OnGUI).</summary>
        public static void HandleKeys()
        {
            Keyboard k = Keyboard.current;
            if (k == null) return;

            Key[] numberKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0 };
            for (int i = 0; i < numberKeys.Length; i++)
            {
                if (k[numberKeys[i]].wasPressedThisFrame && i < ActiveClub.Bag.Count)
                    ActiveClub.SelectBagIndex(i);
            }
            if (k.qKey.wasPressedThisFrame || k.pageUpKey.wasPressedThisFrame) ActiveClub.Cycle(-1);
            if (k.eKey.wasPressedThisFrame || k.pageDownKey.wasPressedThisFrame) ActiveClub.Cycle(1);
        }

        private static void Ensure()
        {
            if (panel != null) return;
            panel = new GUIStyle(GUI.skin.box) { normal = { background = GolfSimTheme.Tex(new Color(0.005f, 0.025f, 0.03f, 0.92f)) }, padding = new RectOffset(6, 6, 4, 4) };
            button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter, padding = new RectOffset(2, 2, 6, 2),
                normal = { background = GolfSimTheme.Tex(new Color(0.06f, 0.16f, 0.20f, 1f)), textColor = Color.white },
                hover = { background = GolfSimTheme.Tex(new Color(0.10f, 0.26f, 0.32f, 1f)), textColor = Color.white },
                active = { background = GolfSimTheme.Tex(GolfSimTheme.Accent), textColor = Color.white }
            };
            active = new GUIStyle(button) { normal = { background = GolfSimTheme.Tex(GolfSimTheme.Accent), textColor = Color.white }, hover = { background = GolfSimTheme.Tex(new Color(0.16f, 0.68f, 1f)), textColor = Color.white } };
            loft = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.LowerCenter, normal = { textColor = new Color(0.78f, 0.86f, 0.90f) } };
            label = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft, wordWrap = true, normal = { textColor = new Color(0.70f, 0.80f, 0.84f) } };
            underline = GolfSimTheme.Tex(new Color(1f, 0.82f, 0.2f, 1f));
        }

        public static void Draw(Rect area)
        {
            Ensure();
            GUI.Box(area, GUIContent.none, panel);

            var bag = ActiveClub.Bag;
            string current = ActiveClub.Resolve();
            GUI.Label(new Rect(area.x + 8f, area.y + 6f, 64f, area.height - 10f), "CLUB\n" + current + "\nQ / E", label);

            float left = area.x + 76f;
            float width = area.width - 82f;
            int count = Mathf.Max(1, bag.Count);
            float w = Mathf.Min(64f, width / count);
            for (int i = 0; i < bag.Count; i++)
            {
                Rect r = new Rect(left + i * w, area.y + 5f, w - 3f, area.height - 10f);
                bool selected = string.Equals(bag[i].Name, current, System.StringComparison.OrdinalIgnoreCase);
                if (GUI.Button(r, bag[i].Short, selected ? active : button))
                    ActiveClub.Select(bag[i].Name);
                GUI.Label(new Rect(r.x, r.y, r.width, r.height - 3f), bag[i].Loft.ToString("0.#") + "°", loft);
                if (selected) GUI.DrawTexture(new Rect(r.x + 3f, r.yMax - 3f, r.width - 6f, 3f), underline);
            }
        }
    }
}
