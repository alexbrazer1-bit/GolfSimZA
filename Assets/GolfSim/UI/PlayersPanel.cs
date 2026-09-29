using System;
using GolfSimZA.Players;
using UnityEngine;

namespace GolfSimZA.UI
{
    /// <summary>
    /// Player list + details (name, hand, colour, playing, golf bag). Used on the home
    /// PLAYERS screen and in Settings → PLAYERS.
    /// </summary>
    public sealed class PlayersPanel
    {
        private PlayerProfile editing;
        private string newPlayerName = "";
        private Vector2 scroll;
        private readonly Action<string> openBag;
        private static Texture2D swatch;

        /// <param name="openBag">Opens Map My Bag for a player (null hides the button, e.g. during a round).</param>
        public PlayersPanel(Action<string> openBag)
        {
            this.openBag = openBag;
        }

        public void Draw(Rect r)
        {
            GolfSimTheme.Ensure();
            if (swatch == null) swatch = GolfSimTheme.Rounded(Color.white, 8);
            PlayerRoster roster = PlayerRoster.Current;
            if (editing == null || !roster.players.Contains(editing)) editing = roster.players.Count > 0 ? roster.players[0] : null;

            float listW = Mathf.Min(360f, r.width * 0.32f);
            Rect list = new Rect(r.x, r.y, listW, r.height);
            GUI.Box(list, GUIContent.none, GolfSimTheme.Card);

            float rowH = 48f;
            Rect view = new Rect(list.x + 8f, list.y + 8f, list.width - 16f, list.height - 120f);
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, view.width - 18f, roster.players.Count * (rowH + 4f)));
            for (int i = 0; i < roster.players.Count; i++)
            {
                PlayerProfile p = roster.players[i];
                Rect row = new Rect(0, i * (rowH + 4f), view.width - 18f, rowH);
                bool sel = p == editing;
                if (GUI.Button(new Rect(row.x, row.y, row.width - 50f, rowH), "     " + p.name.ToUpperInvariant() + (p.selected ? "   ●" : ""), sel ? GolfSimTheme.TabActive : GolfSimTheme.Button)) editing = p;
                GUI.color = PlayerRoster.Colors[roster.ColorIndex(p)];
                GUI.DrawTexture(new Rect(row.x + 12f, row.y + 16f, 16f, 16f), swatch);
                GUI.color = Color.white;
                GUI.enabled = roster.players.Count > 1;
                if (GUI.Button(new Rect(row.xMax - 44f, row.y + 4f, 40f, 40f), "✕", GolfSimTheme.SmallButton))
                {
                    roster.Remove(p);
                    if (editing == p) editing = roster.players[0];
                    GUI.enabled = true;
                    break;
                }
                GUI.enabled = true;
            }
            GUI.EndScrollView();

            newPlayerName = GUI.TextField(new Rect(list.x + 12f, list.yMax - 102f, list.width - 24f, 36f), newPlayerName ?? "", GolfSimTheme.TextField);
            if (string.IsNullOrEmpty(newPlayerName)) GUI.Label(new Rect(list.x + 24f, list.yMax - 94f, 200f, 20f), "New player name", GolfSimTheme.Subtitle);
            if (GUI.Button(new Rect(list.x + 12f, list.yMax - 56f, list.width - 24f, 44f), "+  ADD PLAYER", GolfSimTheme.AccentButton))
            {
                editing = roster.Add(newPlayerName);
                newPlayerName = "";
            }

            if (editing == null) return;
            Rect info = new Rect(list.xMax + 16f, list.y, r.width - listW - 16f, list.height);
            GUI.Box(info, GUIContent.none, GolfSimTheme.Card);
            float x = info.x + 24f, y = info.y + 18f, w = Mathf.Min(560f, info.width - 48f);

            GUI.Label(new Rect(x, y, w, 26f), "INFORMATION", GolfSimTheme.Heading);
            y += 36f;
            GUI.Label(new Rect(x, y, w, 18f), "PLAYER NAME", GolfSimTheme.Label);
            y += 22f;
            string renamed = GUI.TextField(new Rect(x, y, w, 36f), editing.name ?? "", GolfSimTheme.TextField);
            if (renamed != editing.name)
            {
                editing.name = PlayerRoster.Clean(renamed);
                roster.Save();
            }
            y += 48f;

            GUI.Label(new Rect(x, y, w, 18f), "LEFT / RIGHT HANDED", GolfSimTheme.Label);
            y += 22f;
            if (GUI.Button(new Rect(x, y, w * 0.49f, 40f), "LEFT", editing.leftHanded ? GolfSimTheme.TabActive : GolfSimTheme.Button)) { editing.leftHanded = true; roster.Save(); }
            if (GUI.Button(new Rect(x + w * 0.51f, y, w * 0.49f, 40f), "RIGHT", !editing.leftHanded ? GolfSimTheme.TabActive : GolfSimTheme.Button)) { editing.leftHanded = false; roster.Save(); }
            y += 52f;

            GUI.Label(new Rect(x, y, w, 18f), "COLOUR / TEAM  (shown with the player's name on the course)", GolfSimTheme.Label);
            y += 22f;
            int current = roster.ColorIndex(editing);
            float sw = Mathf.Min(64f, (w - 7 * 6f) / 8f);
            for (int c = 0; c < PlayerRoster.Colors.Length; c++)
            {
                Rect cell = new Rect(x + c * (sw + 6f), y, sw, 40f);
                if (GUI.Button(cell, GUIContent.none, c == current ? GolfSimTheme.TabActive : GolfSimTheme.Button)) { editing.color = c; roster.Save(); }
                GUI.color = PlayerRoster.Colors[c];
                GUI.DrawTexture(new Rect(cell.x + 6f, cell.y + 6f, cell.width - 12f, cell.height - 12f), swatch);
                GUI.color = Color.white;
            }
            y += 46f;
            GUI.Label(new Rect(x, y, w, 18f), PlayerRoster.ColorNames[current], new GUIStyle(GolfSimTheme.Label) { normal = { textColor = PlayerRoster.Colors[current] } });
            y += 28f;

            GUI.Label(new Rect(x, y, w, 18f), "RANGE & PRACTICE PLAYER", GolfSimTheme.Label);
            y += 22f;
            if (GUI.Button(new Rect(x, y, w * 0.49f, 40f), "PLAYING", editing.selected ? GolfSimTheme.TabActive : GolfSimTheme.Button))
            {
                if (!editing.selected && roster.Selected.Count >= PlayerRoster.MaxPlayersInRound) roster.Selected[roster.Selected.Count - 1].selected = false;
                editing.selected = true;
                roster.Save();
            }
            if (GUI.Button(new Rect(x + w * 0.51f, y, w * 0.49f, 40f), "NOT PLAYING", !editing.selected ? GolfSimTheme.TabActive : GolfSimTheme.Button)) { editing.selected = false; roster.Save(); }
            y += 52f;

            if (openBag != null)
            {
                GUI.Label(new Rect(x, y, w, 18f), "GOLF BAG", GolfSimTheme.Label);
                y += 22f;
                if (GUI.Button(new Rect(x, y, w, 42f), "EDIT GOLF BAG  &  MAP MY BAG  →", GolfSimTheme.AccentButton)) openBag(editing.name);
                y += 52f;
            }
            GUI.Label(new Rect(x, y, w, 60f), "Players marked PLAYING (●) are used on the driving range and in on-course practice. For a course round you choose the players in Round Settings. Each player's clubs and mapped distances are kept with their name.", GolfSimTheme.Subtitle);
        }
    }
}
