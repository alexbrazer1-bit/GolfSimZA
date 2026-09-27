using System;
using System.Collections.Generic;
using GolfSimZA.Core;
using GolfSimZA.Courses;
using GolfSimZA.Players;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfSimZA.UI
{
    /// <summary>
    /// ROUND SETTINGS for a Local Match: PLAYERS (up to 8 player tiles with tees and colour),
    /// MATCH SETTINGS (game mode, pins, tees, gimme, mulligans, resume) and HOLES
    /// (18 / front 9 / back 9). PLAY starts the round; HEAD TO THE RANGE goes to the range.
    /// </summary>
    public sealed class RoundSettingsUI : MonoBehaviour
    {
        private enum Tab { Players, Match, Holes }
        private static readonly string[] TabNames = { "PLAYERS", "MATCH SETTINGS", "HOLES" };

        private string[] tees = { "Red", "White", "Blue", "Black" };
        private readonly string[] gameModes = { "Stroke Play", "Match Play" };
        private readonly string[] pins = { "Easy", "Standard", "Tournament" };
        private readonly string[] gimmies = { "Off", "1 m", "2 m", "3 m" };
        private readonly string[] mulligans = { "Off", "1", "2", "3", "Unlimited" };

        private Tab tab = Tab.Players;
        private int selectedTee, selectedGameMode, selectedPins = 1, selectedGimmie = 1, selectedMulligan = 4;
        private int holesChoice; // 0 = all / 18, 1 = front 9, 2 = back 9
        private bool resumeRound;

        private readonly List<string> roundPlayers = new List<string>();
        private int pickerSlot = -2; // -2 closed, -1 add, >= 0 replace that slot
        private string newName = "";
        private string message = "";
        private CourseDefinition course;
        private Vector2 pickerScroll;

        private GUIStyle valueStyle, arrowStyle, hintStyle, nameStyle, plusStyle, cellStyle, cellHead;
        private static Texture2D background, swatch, white;

        private void Start()
        {
            tees = CourseSession.AvailableTees;
            selectedTee = Mathf.Max(0, Array.IndexOf(tees, CourseSession.TeeName));
            selectedGameMode = Mathf.Max(0, Array.IndexOf(gameModes, CourseSession.GameMode));
            selectedPins = Mathf.Max(0, Array.IndexOf(pins, CourseSession.PinSetting));
            selectedGimmie = Mathf.Max(0, Array.IndexOf(gimmies, CourseSession.GimmieSetting));
            int m = Array.IndexOf(mulligans, CourseSession.MulliganSetting);
            selectedMulligan = m >= 0 ? m : 4;
            holesChoice = CourseSession.RoundStartHole >= 9 ? 2 : CourseSession.RoundLength <= 9 && CourseSession.CourseHoles > 9 ? 1 : 0;

            course = CourseSession.IsImportedCourse ? CourseLibrary.Get(CourseSession.CourseId) : null;

            foreach (PlayerProfile p in PlayerRoster.Current.Selected) roundPlayers.Add(p.name);
            if (roundPlayers.Count == 0) roundPlayers.Add(PlayerRoster.Current.players[0].name);
        }

        private void EnsureStyles()
        {
            GolfSimTheme.Ensure();
            if (valueStyle != null) return;
            background = GolfSimTheme.Tex(GolfSimTheme.Navy);
            swatch = GolfSimTheme.Rounded(Color.white, 6);
            white = GolfSimTheme.Tex(Color.white);
            valueStyle = new GUIStyle(GolfSimTheme.Center) { fontSize = 15 };
            arrowStyle = new GUIStyle(GolfSimTheme.Button) { fontSize = 18, fixedHeight = 34 };
            hintStyle = new GUIStyle(GolfSimTheme.Subtitle) { fontSize = 12, wordWrap = true };
            nameStyle = new GUIStyle(GolfSimTheme.Heading) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            plusStyle = new GUIStyle(GolfSimTheme.Button) { fontSize = 44, fixedHeight = 0, normal = { textColor = new Color(1f, 1f, 1f, 0.75f) } };
            cellStyle = new GUIStyle(GolfSimTheme.Center) { fontSize = 13 };
            cellHead = new GUIStyle(GolfSimTheme.Label) { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
        }

        private void OnGUI()
        {
            EnsureStyles();
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), background);

            float margin = Mathf.Clamp(Screen.width * 0.03f, 16f, 48f);
            float width = Screen.width - margin * 2f;

            // Top bar
            if (GUI.Button(new Rect(margin, 16f, 110f, 38f), "←  BACK", GolfSimTheme.TopBarButton)) CourseSelectionUI.OpenHome("LocalMatch");
            GolfSimTheme.DrawLogo(new Rect(Screen.width * 0.5f - 150f, 14f, 300f, 40f));
            if (GUI.Button(new Rect(Screen.width - margin - 250f, 16f, 120f, 38f), "PLAYERS", GolfSimTheme.TopBarButton)) CourseSelectionUI.OpenHome("Players");
            if (GUI.Button(new Rect(Screen.width - margin - 124f, 16f, 124f, 38f), "SETTINGS", GolfSimTheme.TopBarButton)) CourseSelectionUI.OpenHome("Settings");
            GUI.DrawTexture(new Rect(margin, 62f, width, 2f), GolfSimTheme.AccentTex);

            float y = 78f;
            GUI.Label(new Rect(margin, y, 360f, 40f), "ROUND SETTINGS", GolfSimTheme.Title);
            float tabW = Mathf.Min(190f, (width - 380f) / 3f - 6f);
            float tx = margin + Mathf.Max(380f, (width - (tabW + 6f) * 3f) * 0.5f);
            for (int i = 0; i < TabNames.Length; i++)
                if (GolfSimTheme.FitButton(new Rect(tx + i * (tabW + 6f), y + 2f, tabW, 38f), TabNames[i], (int)tab == i ? GolfSimTheme.TabActive : GolfSimTheme.Tab))
                {
                    tab = (Tab)i;
                    pickerSlot = -2;
                }
            y += 50f;
            GUI.Label(new Rect(margin, y, width, 22f), CourseSession.CourseName + "  •  " + HolesText() + "  •  " + gameModes[selectedGameMode], GolfSimTheme.Subtitle);
            y += 30f;

            Rect body = new Rect(margin, y, width, Screen.height - y - 90f);
            switch (tab)
            {
                case Tab.Players: DrawPlayers(body); break;
                case Tab.Match: DrawMatch(body); break;
                case Tab.Holes: DrawHoles(body); break;
            }

            // Bottom bar
            float by = Screen.height - 70f;
            if (!string.IsNullOrEmpty(message)) GUI.Label(new Rect(margin, by - 26f, width, 22f), message, new GUIStyle(GolfSimTheme.Label) { normal = { textColor = GolfSimTheme.Warning } });
            if (GUI.Button(new Rect(margin, by, 230f, 50f), "CREATE NEW PLAYER  +", GolfSimTheme.Button)) { tab = Tab.Players; pickerSlot = -1; newName = ""; }
            if (GUI.Button(new Rect(Screen.width - margin - 560f, by, 250f, 50f), "HEAD TO THE RANGE", GolfSimTheme.Button)) HeadToRange();
            if (GolfSimTheme.FitButton(new Rect(Screen.width - margin - 300f, by, 300f, 50f), "PLAY " + CourseSession.CourseName.ToUpperInvariant() + "  ▶", new GUIStyle(GolfSimTheme.AccentButton) { fixedHeight = 50f, fontSize = 16 })) Play();

            if (pickerSlot > -2) DrawPicker();
        }

        // ------------------------------------------------------------ PLAYERS

        private void DrawPlayers(Rect r)
        {
            PlayerRoster roster = PlayerRoster.Current;
            const int cols = 4, rows = 2;
            float gap = 12f;
            float tw = (r.width - gap * (cols - 1)) / cols;
            float th = Mathf.Min(190f, (r.height - gap * (rows - 1)) / rows);
            for (int i = 0; i < cols * rows; i++)
            {
                Rect t = new Rect(r.x + (i % cols) * (tw + gap), r.y + (i / cols) * (th + gap), tw, th);
                if (i < roundPlayers.Count) DrawPlayerTile(t, i, roster);
                else if (i == roundPlayers.Count)
                {
                    if (GUI.Button(t, "+", plusStyle)) { pickerSlot = -1; newName = ""; }
                }
                else GUI.Box(t, GUIContent.none, GolfSimTheme.Card);
            }
            GUI.Label(new Rect(r.x, r.y + (th + gap) * 2f + 4f, r.width, 20f), "Up to " + PlayerRoster.MaxPlayersInRound + " players. Tap a name to swap the player. Colours show beside each name on the course.", hintStyle);
        }

        private void DrawPlayerTile(Rect t, int slot, PlayerRoster roster)
        {
            string name = roundPlayers[slot];
            PlayerProfile p = roster.Find(name);
            int colorIndex = p != null ? roster.ColorIndex(p) : 0;
            Color color = PlayerRoster.Colors[colorIndex];

            GUI.Box(t, GUIContent.none, GolfSimTheme.SelectedCard);
            GUI.color = color;
            GUI.DrawTexture(new Rect(t.x + 3f, t.y + 3f, t.width - 6f, 6f), white);
            GUI.color = Color.white;

            float x = t.x + 12f, w = t.width - 24f, y = t.y + 16f;
            if (GUI.Button(new Rect(x, y, w - 40f, 36f), "  " + name.ToUpperInvariant() + "  ▾", GolfSimTheme.Button)) { pickerSlot = slot; newName = ""; }
            GUI.enabled = roundPlayers.Count > 1;
            if (GUI.Button(new Rect(x + w - 36f, y, 36f, 36f), "✕", GolfSimTheme.SmallButton)) { roundPlayers.RemoveAt(slot); GUI.enabled = true; return; }
            GUI.enabled = true;
            y += 44f;

            // Tees: "MATCH TEES" or one of the course's tees for this player.
            string playerTee = p != null && Array.IndexOf(tees, p.tee) >= 0 ? p.tee : "";
            int teeIndex = playerTee == "" ? -1 : Array.IndexOf(tees, playerTee);
            string teeText = teeIndex < 0 ? "MATCH TEES (" + tees[selectedTee] + ")" : tees[teeIndex].ToUpperInvariant() + " TEES";
            if (Arrows(new Rect(x, y, w, 34f), teeText, out int dir) && p != null)
            {
                int next = teeIndex + dir;
                if (next < -1) next = tees.Length - 1;
                if (next >= tees.Length) next = -1;
                p.tee = next < 0 ? "" : tees[next];
                roster.Save();
            }
            y += 42f;

            // Colour / team
            if (Arrows(new Rect(x, y, w, 34f), "      " + PlayerRoster.ColorNames[colorIndex], out int cdir) && p != null)
            {
                p.color = (colorIndex + cdir + PlayerRoster.Colors.Length) % PlayerRoster.Colors.Length;
                roster.Save();
            }
            GUI.color = color;
            GUI.DrawTexture(new Rect(t.center.x - 56f, y + 9f, 16f, 16f), swatch);
            GUI.color = Color.white;
            y += 42f;

            bool left = p != null && p.leftHanded;
            if (y + 30f <= t.yMax && GUI.Button(new Rect(x, y, w, 30f), left ? "LEFT HANDED" : "RIGHT HANDED", GolfSimTheme.SmallButton) && p != null)
            {
                p.leftHanded = !p.leftHanded;
                roster.Save();
            }
        }

        private bool Arrows(Rect r, string text, out int direction)
        {
            direction = 0;
            if (GUI.Button(new Rect(r.x, r.y, 34f, r.height), "‹", arrowStyle)) direction = -1;
            GUI.Label(new Rect(r.x + 36f, r.y, r.width - 72f, r.height), GolfSimTheme.Ellipsize(text, valueStyle, r.width - 76f), valueStyle);
            if (GUI.Button(new Rect(r.xMax - 34f, r.y, 34f, r.height), "›", arrowStyle)) direction = 1;
            return direction != 0;
        }

        /// <summary>Pick a saved player for a slot, or create a new one.</summary>
        private void DrawPicker()
        {
            PlayerRoster roster = PlayerRoster.Current;
            float w = 460f, h = Mathf.Min(560f, Screen.height - 80f);
            Rect box = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);
            GUI.Box(box, GUIContent.none, GolfSimTheme.Overlay);
            GUI.Label(new Rect(box.x + 20f, box.y + 16f, w - 100f, 30f), pickerSlot < 0 ? "ADD A PLAYER" : "SWAP PLAYER", GolfSimTheme.Heading);
            if (GUI.Button(new Rect(box.xMax - 60f, box.y + 14f, 44f, 36f), "✕", GolfSimTheme.SmallButton)) { pickerSlot = -2; return; }

            var free = new List<PlayerProfile>();
            foreach (PlayerProfile p in roster.players)
                if (!roundPlayers.Exists(n => string.Equals(n, p.name, StringComparison.OrdinalIgnoreCase))) free.Add(p);

            Rect view = new Rect(box.x + 16f, box.y + 60f, w - 32f, h - 190f);
            pickerScroll = GUI.BeginScrollView(view, pickerScroll, new Rect(0, 0, view.width - 18f, Mathf.Max(view.height, free.Count * 48f)));
            for (int i = 0; i < free.Count; i++)
            {
                PlayerProfile p = free[i];
                if (GUI.Button(new Rect(0, i * 48f, view.width - 18f, 42f), "      " + p.name.ToUpperInvariant(), GolfSimTheme.Button)) { UsePlayer(p.name); return; }
                GUI.color = PlayerRoster.Colors[roster.ColorIndex(p)];
                GUI.DrawTexture(new Rect(14f, i * 48f + 13f, 16f, 16f), swatch);
                GUI.color = Color.white;
            }
            if (free.Count == 0) GUI.Label(new Rect(0, 0, view.width - 18f, 40f), "Every saved player is already in the round. Create a new player below.", hintStyle);
            GUI.EndScrollView();

            GUI.Label(new Rect(box.x + 20f, box.yMax - 124f, w - 40f, 20f), "CREATE NEW PLAYER", GolfSimTheme.Label);
            newName = GUI.TextField(new Rect(box.x + 20f, box.yMax - 100f, w - 40f, 36f), newName ?? "", GolfSimTheme.TextField);
            if (string.IsNullOrEmpty(newName)) GUI.Label(new Rect(box.x + 32f, box.yMax - 92f, 200f, 20f), "Name", GolfSimTheme.Subtitle);
            if (GUI.Button(new Rect(box.x + 20f, box.yMax - 56f, w - 40f, 42f), "CREATE & ADD", GolfSimTheme.AccentButton))
            {
                PlayerProfile created = roster.Add(newName);
                UsePlayer(created.name);
            }
        }

        private void UsePlayer(string name)
        {
            if (pickerSlot >= 0 && pickerSlot < roundPlayers.Count) roundPlayers[pickerSlot] = name;
            else if (roundPlayers.Count < PlayerRoster.MaxPlayersInRound) roundPlayers.Add(name);
            else message = "A round has up to " + PlayerRoster.MaxPlayersInRound + " players.";
            pickerSlot = -2;
        }

        // ------------------------------------------------------------ MATCH SETTINGS

        private void DrawMatch(Rect r)
        {
            float gap = 14f;
            float cw = (r.width - gap * 2f) / 3f, ch = 112f;
            SettingCard(new Rect(r.x, r.y, cw, ch), "GAME MODE", gameModes, ref selectedGameMode,
                selectedGameMode == 1 ? (roundPlayers.Count >= 2 ? "Lowest score wins each hole" : "Needs 2+ players - plays as stroke play") : "Total strokes over the round");
            SettingCard(new Rect(r.x + cw + gap, r.y, cw, ch), "MATCH TEES", tees, ref selectedTee, "Players on MATCH TEES use these");
            SettingCard(new Rect(r.x + (cw + gap) * 2f, r.y, cw, ch), "PINS", pins, ref selectedPins, "Pin position on every green");
            float y2 = r.y + ch + gap;
            SettingCard(new Rect(r.x, y2, cw, ch), "GIMME / AUTO PUTT", gimmies, ref selectedGimmie, "Balls this close are holed (+1 stroke)");
            SettingCard(new Rect(r.x + cw + gap, y2, cw, ch), "MULLIGANS", mulligans, ref selectedMulligan, "Per player per round (Menu → MULLIGAN)");
            ResumeCard(new Rect(r.x + (cw + gap) * 2f, y2, cw, ch));
            if (GUI.Button(new Rect(r.x, y2 + ch + gap, 260f, 42f), "RECOMMENDED SETTINGS", GolfSimTheme.Button))
            {
                selectedGameMode = 0; selectedPins = 1; selectedGimmie = 1; selectedMulligan = 4; resumeRound = false;
            }
            GUI.Label(new Rect(r.x, y2 + ch + gap + 52f, r.width, 40f), "Wind, green speed, fairway firmness, auto putt and player rotation are in SETTINGS → GAME.", hintStyle);
        }

        private void SettingCard(Rect r, string title, string[] values, ref int selected, string hint)
        {
            GUI.Box(r, GUIContent.none, GolfSimTheme.Card);
            GUI.Label(new Rect(r.x + 16f, r.y + 12f, r.width - 32f, 18f), title, GolfSimTheme.Label);
            if (Arrows(new Rect(r.x + 14f, r.y + 38f, r.width - 28f, 38f), values[Mathf.Clamp(selected, 0, values.Length - 1)], out int dir))
                selected = (selected + dir + values.Length) % values.Length;
            GUI.Label(new Rect(r.x + 16f, r.yMax - 28f, r.width - 32f, 18f), GolfSimTheme.Ellipsize(hint, hintStyle, r.width - 32f), hintStyle);
        }

        private void ResumeCard(Rect r)
        {
            GUI.Box(r, GUIContent.none, GolfSimTheme.Card);
            GUI.Label(new Rect(r.x + 16f, r.y + 12f, r.width - 32f, 18f), "RESUME ROUND", GolfSimTheme.Label);
            if (GUI.Button(new Rect(r.x + 14f, r.y + 38f, 90f, 38f), resumeRound ? "ON" : "OFF", resumeRound ? GolfSimTheme.AccentButton : GolfSimTheme.Button)) resumeRound = !resumeRound;
            if (RoundSave.Exists && GUI.Button(new Rect(r.xMax - 150f, r.y + 42f, 136f, 30f), "DELETE SAVED", GolfSimTheme.SmallButton)) RoundSave.Clear();
            GUI.Label(new Rect(r.x + 16f, r.yMax - 32f, r.width - 32f, 28f), GolfSimTheme.Ellipsize(RoundSave.Describe(), hintStyle, r.width - 32f), hintStyle);
        }

        // ------------------------------------------------------------ HOLES

        private string HolesText()
        {
            int holes = CourseSession.CourseHoles;
            if (holes <= 9) return holes + " holes";
            return holesChoice == 1 ? "Front 9" : holesChoice == 2 ? "Back 9" : holes + " holes";
        }

        private void DrawHoles(Rect r)
        {
            int courseHoles = CourseSession.CourseHoles;
            string[] options = courseHoles > 9 ? new[] { courseHoles + " HOLES", "FRONT 9", "BACK 9" } : new[] { courseHoles + " HOLES" };
            float bw = 200f;
            for (int i = 0; i < options.Length; i++)
                if (GUI.Button(new Rect(r.x + i * (bw + 8f), r.y, bw, 42f), options[i], holesChoice == i ? GolfSimTheme.TabActive : GolfSimTheme.Button)) holesChoice = i;
            if (holesChoice >= options.Length) holesChoice = 0;

            // Scorecard preview for the match tees.
            int first = holesChoice == 2 ? 9 : 0;
            int count = holesChoice == 0 ? courseHoles : Mathf.Min(9, courseHoles - first);
            float y = r.y + 60f;
            GUI.Label(new Rect(r.x, y, r.width, 22f), "HOLES  (" + tees[selectedTee] + " tees)", GolfSimTheme.Label);
            y += 28f;
            float cellW = Mathf.Clamp((r.width - 110f) / (count + 1), 34f, 70f);
            GUI.Label(new Rect(r.x, y, 110f, 24f), "HOLE", cellHead);
            GUI.Label(new Rect(r.x, y + 30f, 110f, 24f), "PAR", cellHead);
            GUI.Label(new Rect(r.x, y + 60f, 110f, 24f), Units.DistanceUnit.ToUpperInvariant(), cellHead);
            int parTotal = 0;
            float lengthTotal = 0f;
            for (int i = 0; i < count; i++)
            {
                int h = first + i;
                int par = course != null ? course.GetHole(h).par : DemoCourse.Par[Mathf.Clamp(h, 0, 17)];
                float length = course != null ? course.GetHole(h).LengthMeters(tees[selectedTee]) : DemoCourse.Length(h, tees[selectedTee]);
                parTotal += par;
                lengthTotal += length;
                float cx = r.x + 110f + i * cellW;
                GUI.Label(new Rect(cx, y, cellW, 24f), (h + 1).ToString(), cellHead);
                GUI.Label(new Rect(cx, y + 30f, cellW, 24f), par.ToString(), cellStyle);
                GUI.Label(new Rect(cx, y + 60f, cellW, 24f), Units.Distance(length).ToString("0"), cellStyle);
            }
            float tx = r.x + 110f + count * cellW;
            GUI.Label(new Rect(tx, y, cellW, 24f), "TOT", cellHead);
            GUI.Label(new Rect(tx, y + 30f, cellW, 24f), parTotal.ToString(), cellStyle);
            GUI.Label(new Rect(tx, y + 60f, cellW + 20f, 24f), Units.Distance(lengthTotal).ToString("0"), cellStyle);
        }

        // ------------------------------------------------------------ Start

        private bool Apply()
        {
            if (roundPlayers.Count == 0) { message = "Add at least one player."; return false; }
            int courseHoles = CourseSession.CourseHoles;
            int start = courseHoles > 9 && holesChoice == 2 ? 9 : 0;
            int length = courseHoles > 9 && holesChoice > 0 ? 9 : courseHoles;

            CourseSession.SetRoundSettings(gameModes[selectedGameMode], tees[selectedTee], pins[selectedPins], gimmies[selectedGimmie], mulligans[selectedMulligan], resumeRound, length);
            CourseSession.SetHoles(courseHoles, start, length);
            CourseSession.PracticeMode = false;

            // The round's players become the selected players (in this order).
            PlayerRoster roster = PlayerRoster.Current;
            foreach (PlayerProfile p in roster.players)
                p.selected = roundPlayers.Exists(n => string.Equals(n, p.name, StringComparison.OrdinalIgnoreCase));
            roster.Save();
            CourseSession.SetPlayers(roundPlayers.ToArray());
            return true;
        }

        private void Play()
        {
            if (Apply()) SceneManager.LoadScene("GolfSimZA_0_6_PlayRound");
        }

        private void HeadToRange()
        {
            if (!Apply()) return;
            CourseSession.SetSession("GolfSim ZA Practice Range", "Blue", 18);
            CourseSession.SetCourse("", null);
            CourseSession.SetHoles(18, 0, 18);
            SceneManager.LoadScene("GolfSimZA_0_6_PlayRound");
        }
    }
}
