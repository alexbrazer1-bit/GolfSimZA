using GolfSimZA.Core;
using GolfSimZA.Players;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GolfSimZA.UI
{
    /// <summary>
    /// Club selector, bottom-left: a CLUB tile (current club) and a HAND tile (right / left
    /// handed). Clicking the club tile opens a drop-up grid of every club in the player's bag
    /// with its loft. Keys: 1-9 / 0 first ten clubs, Q / E previous / next club.
    /// </summary>
    public static class ClubBar
    {
        private static GUIStyle tile, tileValue, tileLabel, clubButton, clubActive, clubLoft, panel;
        private static bool open;

        public const float Height = 64f;
        public static bool IsOpen => open;

        /// <summary>Call from Update (not OnGUI).</summary>
        public static void HandleKeys()
        {
            Keyboard k = Keyboard.current;
            if (k == null) return;

            Key[] numberKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0 };
            for (int i = 0; i < numberKeys.Length; i++)
                if (k[numberKeys[i]].wasPressedThisFrame && i < ActiveClub.Bag.Count)
                    ActiveClub.SelectBagIndex(i);
            if (k.qKey.wasPressedThisFrame || k.pageUpKey.wasPressedThisFrame) ActiveClub.Cycle(-1);
            if (k.eKey.wasPressedThisFrame || k.pageDownKey.wasPressedThisFrame) ActiveClub.Cycle(1);
            if (k.cKey.wasPressedThisFrame) open = !open;
        }

        private static void Ensure()
        {
            GolfSimTheme.Ensure();
            if (tile != null) return;
            tile = new GUIStyle(GUI.skin.button)
            {
                border = new RectOffset(8, 8, 8, 8),
                normal = { background = GolfSimTheme.Rounded(new Color(0.03f, 0.07f, 0.09f, 0.88f), 8) },
                hover = { background = GolfSimTheme.Rounded(new Color(0.06f, 0.16f, 0.14f, 0.95f), 8, GolfSimTheme.Accent) },
                active = { background = GolfSimTheme.Rounded(new Color(0.06f, 0.16f, 0.14f, 0.95f), 8, GolfSimTheme.Gold) }
            };
            tileValue = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            tileLabel = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = GolfSimTheme.Muted } };
            panel = new GUIStyle(GUI.skin.box) { border = new RectOffset(10, 10, 10, 10), normal = { background = GolfSimTheme.Rounded(new Color(0.06f, 0.11f, 0.14f, 0.95f), 10) } };
            clubButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter, padding = new RectOffset(2, 2, 8, 2), border = new RectOffset(6, 6, 6, 6),
                normal = { background = GolfSimTheme.Tex(new Color(0, 0, 0, 0)), textColor = Color.white },
                hover = { background = GolfSimTheme.Rounded(new Color(1f, 1f, 1f, 0.10f), 6), textColor = Color.white },
                active = { background = GolfSimTheme.Rounded(GolfSimTheme.Accent, 6), textColor = Color.white }
            };
            clubActive = new GUIStyle(clubButton) { normal = { background = GolfSimTheme.Rounded(GolfSimTheme.Accent, 6), textColor = Color.white } };
            clubLoft = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.LowerCenter, normal = { textColor = new Color(0.80f, 0.88f, 0.90f) } };
        }

        /// <summary>Draws the tiles with their bottom-left corner at (left, bottom).</summary>
        public static void Draw(float left, float bottom)
        {
            Ensure();
            var bag = ActiveClub.Bag;
            string current = ActiveClub.Resolve();

            Rect clubRect = new Rect(left, bottom - Height, 96f, Height);
            if (GUI.Button(clubRect, GUIContent.none, tile)) open = !open;
            GUI.Label(new Rect(clubRect.x, clubRect.y + 6f, clubRect.width, 30f), ShortOrName(current), tileValue);
            GUI.Label(new Rect(clubRect.x, clubRect.yMax - 24f, clubRect.width, 16f), "CLUB  " + (open ? "▼" : "▲"), tileLabel);

            bool left_ = PlayerRoster.Current.IsLeftHanded(ActiveClub.Player);
            Rect handRect = new Rect(left + 100f, bottom - Height, 70f, Height);
            if (GUI.Button(handRect, GUIContent.none, tile))
            {
                PlayerProfile p = PlayerRoster.Current.Find(ActiveClub.Player);
                if (p != null)
                {
                    p.leftHanded = !p.leftHanded;
                    PlayerRoster.Current.Save();
                }
            }
            GUI.Label(new Rect(handRect.x, handRect.y + 6f, handRect.width, 30f), left_ ? "LH" : "RH", tileValue);
            GUI.Label(new Rect(handRect.x, handRect.yMax - 24f, handRect.width, 16f), "HAND", tileLabel);

            if (!open) return;

            const int columns = 7;
            const float cell = 62f;
            int rows = Mathf.CeilToInt(bag.Count / (float)columns);
            Rect panelRect = new Rect(left, bottom - Height - 8f - rows * cell - 16f, columns * cell + 16f, rows * cell + 16f);
            GUI.Box(panelRect, GUIContent.none, panel);
            for (int i = 0; i < bag.Count; i++)
            {
                int r = i / columns, c = i % columns;
                Rect b = new Rect(panelRect.x + 8f + c * cell, panelRect.y + 8f + r * cell, cell - 4f, cell - 4f);
                bool selected = string.Equals(bag[i].Name, current, System.StringComparison.OrdinalIgnoreCase);
                if (GUI.Button(b, bag[i].Short, selected ? clubActive : clubButton))
                {
                    ActiveClub.Select(bag[i].Name);
                    open = false;
                }
                GUI.Label(new Rect(b.x, b.y, b.width, b.height - 5f), bag[i].Loft.ToString("0.#") + "°", clubLoft);
            }
        }

        public static void Close() => open = false;

        private static string ShortOrName(string club)
        {
            return club != null && club.Length <= 7 ? club : ActiveClub.ShortName(club);
        }
    }
}
