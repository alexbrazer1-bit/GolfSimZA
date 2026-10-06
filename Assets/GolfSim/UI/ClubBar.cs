using GolfSimZA.Core;
using GolfSimZA.Players;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GolfSimZA.UI
{
    /// <summary>
    /// Club selector, bottom-left: a CLUB tile (current club) and a HAND tile (right / left
    /// handed). Clicking the club tile opens the grid of every club in the player's bag (with its
    /// loft) directly to the RIGHT of the HAND tile, along the bottom of the screen, so it never
    /// covers the hole map above the selector. Before every shot the game can suggest the club for
    /// the distance (<see cref="Suggest"/>): it is selected and marked NEXT on the club tile.
    /// Keys: 1-9 / 0 first ten clubs, Q / E previous / next club.
    /// </summary>
    public static class ClubBar
    {
        private static GUIStyle tile, tileValue, tileLabel, clubButton, clubActive, clubLoft, panel;
        private static bool open;
        private static GUIStyle nextLabel;

        public const float Height = 64f;
        public static bool IsOpen => open;

        /// <summary>Club suggested for the next shot (null = none).</summary>
        public static string Suggested { get; private set; }
        private static float suggestedAt = -10f;

        /// <summary>Selects the club for the next shot and marks it NEXT on the club tile; closes the club grid.</summary>
        public static void Suggest(string clubName)
        {
            if (string.IsNullOrWhiteSpace(clubName)) return;
            ActiveClub.Select(clubName);
            Suggested = clubName;
            suggestedAt = Time.unscaledTime;
            open = false;
        }

        /// <summary>Best club for a distance (metres) from the player's bag: the shortest club that gets there.</summary>
        public static string ClubForDistance(float meters, bool onGreen)
        {
            var bag = ActiveClub.Bag;
            // No putting in GolfSim ZA: short shots get the most lofted club (a chip).
            string best = null, longest = null, shortest = null;
            float shortestCarry = float.MaxValue;
            float bestCarry = float.MaxValue, longestCarry = 0f;
            foreach (ActiveClub.BagClub c in bag)
            {
                if (ActiveClub.IsPutter(c.Name)) continue;
                float carry = ActiveClub.ExpectedCarry(c.Name);
                // Allow a little run-out after the carry.
                float reach = carry * 1.04f;
                if (carry > longestCarry) { longestCarry = carry; longest = c.Name; }
                if (carry < shortestCarry) { shortestCarry = carry; shortest = c.Name; }
                if (reach >= meters && carry < bestCarry) { bestCarry = carry; best = c.Name; }
            }
            if (meters < 12f && shortest != null) return shortest;
            return best ?? longest ?? "Driver";
        }

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
            bool isSuggested = Suggested != null && string.Equals(Suggested, current, System.StringComparison.OrdinalIgnoreCase);
            if (isSuggested)
            {
                // The suggested club for this shot: gold NEXT CLUB caption (pulses for a few seconds after it changes).
                float pulse = Time.unscaledTime - suggestedAt < 4f ? 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) : 1f;
                Color gold = GolfSimTheme.Gold;
                if (nextLabel == null) nextLabel = new GUIStyle(tileLabel);
                nextLabel.normal.textColor = new Color(gold.r, gold.g, gold.b, pulse);
                GUI.Label(new Rect(clubRect.x, clubRect.yMax - 24f, clubRect.width, 16f), "NEXT CLUB  " + (open ? "◀" : "▶"), nextLabel);
            }
            else GUI.Label(new Rect(clubRect.x, clubRect.yMax - 24f, clubRect.width, 16f), "CLUB  " + (open ? "◀" : "▶"), tileLabel);

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

            // The club grid sits directly right of the HAND tile, bottom-aligned with it (two rows).
            const float cell = 58f;
            int rows = bag.Count > 8 ? 2 : 1;
            int columns = Mathf.CeilToInt(bag.Count / (float)rows);
            float panelW = columns * cell + 16f, panelH = rows * cell + 16f;
            float px = handRect.xMax + 8f;
            if (px + panelW > Screen.width - 8f) px = Mathf.Max(8f, Screen.width - 8f - panelW);
            Rect panelRect = new Rect(px, bottom - panelH, panelW, panelH);
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
