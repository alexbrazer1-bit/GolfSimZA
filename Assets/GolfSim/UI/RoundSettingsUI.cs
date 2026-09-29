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
    /// ROUND SETTINGS for a Local Match: PLAYERS (up to 8 player tiles with tees, colour and -
    /// in team formats - the team), MATCH SETTINGS (format with its full definition, pins, tees,
    /// gimme, mulligans, auto putt with the 1-putt / 2-putt circles, resume) and HOLES
    /// (18 / front 9 / back 9). Players are chosen here for every course (nothing is carried over
    /// from the home screen). PLAY starts the round; HEAD TO THE RANGE goes to the range.
    /// </summary>
    public sealed class RoundSettingsUI : MonoBehaviour
    {
        private enum Tab { Players, Match, Holes }
        private static readonly string[] TabNames = { "PLAYERS", "MATCH SETTINGS", "HOLES" };

        private string[] tees = { "Red", "White", "Blue", "Black" };
        private readonly string[] gameModes = GameFormats.Names;
        private readonly string[] pins = { "Easy", "Standard", "Tournament" };
        private readonly string[] gimmies = { "Off", "1 m", "2 m", "3 m" };
        private readonly string[] mulligans = { "Off", "1", "2", "3", "Unlimited" };

        private Tab tab = Tab.Players;
        private int selectedTee, selectedGameMode, selectedPins = 1, selectedGimmie = 1, selectedMulligan = 4;
        private int holesChoice; // 0 = all / 18, 1 = front 9, 2 = back 9
        private bool resumeRound;

        private readonly List<string> roundPlayers = new List<string>();
        /// <summary>Team (0 = A .. 3 = D) of each round player (team formats).</summary>
        private readonly List<int> roundTeams = new List<int>();
        private Vector2 matchScroll;
        private GUIStyle defStyle, defHead;
        private GameFormatInfo Format => GameFormats.All[Mathf.Clamp(selectedGameMode, 0, GameFormats.All.Length - 1)];
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
            selectedGameMode = GameFormats.IndexOf(CourseSession.GameMode);
            selectedPins = Mathf.Max(0, Array.IndexOf(pins, CourseSession.PinSetting));
            selectedGimmie = Mathf.Max(0, Array.IndexOf(gimmies, CourseSession.GimmieSetting));
            int m = Array.IndexOf(mulligans, CourseSession.MulliganSetting);
            selectedMulligan = m >= 0 ? m : 4;
            holesChoice = CourseSession.RoundStartHole >= 9 ? 2 : CourseSession.RoundLength <= 9 && CourseSession.CourseHoles > 9 ? 1 : 0;

            course = CourseSession.IsImportedCourse ? CourseLibrary.Get(CourseSession.CourseId) : null;

            // Only the players already chosen for this course (empty when a course was just picked).
            int[] savedTeams = CourseSession.Teams;
            string[] names = (CourseSession.PlayerNames ?? "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < names.Length && roundPlayers.Count < PlayerRoster.MaxPlayersInRound; i++)
            {
                if (PlayerRoster.Current.Find(names[i]) == null) continue;
                roundPlayers.Add(PlayerRoster.Current.Find(names[i]).name);
                roundTeams.Add(i < savedTeams.Length ? savedTeams[i] : GameFormats.DefaultTeams(i + 1)[i]);
            }
            if (roundPlayers.Count == 0) message = "Add the players for this round (tap +).";
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
            defStyle = new GUIStyle(GolfSimTheme.Body) { wordWrap = true, fontSize = 14 };
            defHead = new GUIStyle(GolfSimTheme.Label) { fontSize = 12, normal = { textColor = GolfSimTheme.Gold } };
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
            GUI.Label(new Rect(margin, y, width, 22f), CourseSession.CourseName + "  •  " + HolesText() + "  •  " + gameModes[selectedGameMode] + "  •  " + roundPlayers.Count + (roundPlayers.Count == 1 ? " player" : " players"), GolfSimTheme.Subtitle);
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
            float th = Mathf.Min(Format.Team ? 232f : 190f, (r.height - gap * (rows - 1)) / rows);
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
            string teamHint = Format.Team ? "  " + Format.Name + ": teams of " + (Format.MinTeamSize == Format.MaxTeamSize ? Format.MinTeamSize.ToString() : Format.MinTeamSize + "-" + Format.MaxTeamSize) + " - set each player's TEAM on the tile." : "";
            GUI.Label(new Rect(r.x, r.y + (th + gap) * 2f + 4f, r.width - 190f, 36f), "Up to " + PlayerRoster.MaxPlayersInRound + " players. Tap a name to swap the player. Colours show beside each name on the course." + teamHint, hintStyle);
            if (Format.Team && GUI.Button(new Rect(r.xMax - 180f, r.y + (th + gap) * 2f + 2f, 180f, 34f), "AUTO TEAMS", GolfSimTheme.SmallButton))
            {
                int[] t = GameFormats.DefaultTeams(roundPlayers.Count, Mathf.Max(2, Format.MinTeamSize));
                for (int i = 0; i < roundTeams.Count; i++) roundTeams[i] = t[i];
                SaveDraft();
            }
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
            if (GUI.Button(new Rect(x + w - 36f, y, 36f, 36f), "✕", GolfSimTheme.SmallButton)) { roundPlayers.RemoveAt(slot); roundTeams.RemoveAt(slot); SaveDraft(); return; }
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

            // Team (team formats only).
            if (Format.Team)
            {
                int team = roundTeams[slot];
                if (Arrows(new Rect(x, y, w, 34f), "TEAM " + GameFormats.TeamLetters[team], out int tdir))
                {
                    roundTeams[slot] = (team + tdir + GameFormats.MaxTeams) % GameFormats.MaxTeams;
                    SaveDraft();
                }
                GUI.color = GameFormats.TeamColors[roundTeams[slot]];
                GUI.DrawTexture(new Rect(t.center.x - 50f, y + 9f, 16f, 16f), swatch);
                GUI.color = Color.white;
                y += 42f;
            }

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
            if (roundPlayers.Exists(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase))) { pickerSlot = -2; return; }
            if (pickerSlot >= 0 && pickerSlot < roundPlayers.Count) roundPlayers[pickerSlot] = name;
            else if (roundPlayers.Count < PlayerRoster.MaxPlayersInRound)
            {
                roundPlayers.Add(name);
                roundTeams.Add(NextTeam());
                message = "";
            }
            else message = "A round has up to " + PlayerRoster.MaxPlayersInRound + " players.";
            SaveDraft();
            pickerSlot = -2;
        }

        // ------------------------------------------------------------ MATCH SETTINGS

        private void DrawMatch(Rect r)
        {
            float gap = 14f;
            float innerW = r.width - 20f;
            float cw = (innerW - gap * 2f) / 3f, ch = 112f;
            float defH = 200f, puttH = 150f;
            float contentH = ch + gap + defH + gap + ch + gap + puttH + gap + 110f;
            matchScroll = GUI.BeginScrollView(r, matchScroll, new Rect(0, 0, innerW, contentH));
            float y = 0f;

            // Row 1: format, match tees, pins.
            int before = selectedGameMode;
            SettingCard(new Rect(0, y, cw, ch), "FORMAT", gameModes, ref selectedGameMode, Format.Short);
            if (before != selectedGameMode && Format.Team && !GameFormats.All[before].Team)
            {
                int[] t = GameFormats.DefaultTeams(roundPlayers.Count, Mathf.Max(2, Format.MinTeamSize));
                for (int i = 0; i < roundTeams.Count; i++) roundTeams[i] = t[i];
            }
            SettingCard(new Rect(cw + gap, y, cw, ch), "MATCH TEES", tees, ref selectedTee, "Players on MATCH TEES use these");
            SettingCard(new Rect((cw + gap) * 2f, y, cw, ch), "PINS", pins, ref selectedPins, "Pin position on every green");
            y += ch + gap;

            // Row 2: the chosen format explained.
            DrawDefinition(new Rect(0, y, innerW, defH));
            y += defH + gap;

            // Row 3: gimme, mulligans, resume.
            SettingCard(new Rect(0, y, cw, ch), "GIMME", gimmies, ref selectedGimmie, "Balls this close are holed (+1 stroke)");
            SettingCard(new Rect(cw + gap, y, cw, ch), "MULLIGANS", mulligans, ref selectedMulligan, "Per player per round (Menu → MULLIGAN)");
            ResumeCard(new Rect((cw + gap) * 2f, y, cw, ch));
            y += ch + gap;

            // Row 4: auto putt and the putt circles.
            DrawAutoPutt(new Rect(0, y, innerW, puttH));
            y += puttH + gap;

            if (GUI.Button(new Rect(0, y, 260f, 42f), "RECOMMENDED SETTINGS", GolfSimTheme.Button))
            {
                selectedGameMode = 0; selectedPins = 1; selectedGimmie = 1; selectedMulligan = 4; resumeRound = false;
            }
            GUI.Label(new Rect(0, y + 52f, innerW, 40f), "Wind, green speed, fairway firmness and player rotation are in SETTINGS → GAME.", hintStyle);
            GUI.EndScrollView();
        }

        /// <summary>How the selected format works, how it is scored and why to play it.</summary>
        private void DrawDefinition(Rect r)
        {
            GameFormatInfo f = Format;
            GUI.Box(r, GUIContent.none, GolfSimTheme.Card);
            float x = r.x + 18f, w = r.width - 36f, y = r.y + 12f;
            GUI.Label(new Rect(x, y, w, 26f), f.Name.ToUpperInvariant() + (f.Team ? "   •   TEAM FORMAT" : "   •   INDIVIDUAL"), GolfSimTheme.Heading);
            y += 30f;
            float colW = (w - 24f) / 3f;
            string[] heads = { "HOW IT WORKS", "SCORING", "WHY PLAY IT" };
            string[] texts = { f.HowItWorks, f.Scoring, f.WhyPlay };
            for (int i = 0; i < 3; i++)
            {
                float cx = x + i * (colW + 12f);
                GUI.Label(new Rect(cx, y, colW, 18f), heads[i], defHead);
                GUI.Label(new Rect(cx, y + 20f, colW, r.yMax - y - 26f), texts[i], defStyle);
            }
            string problem = GameFormats.Problem(f, roundPlayers.Count, roundTeams.ToArray());
            if (problem != null)
                GUI.Label(new Rect(x, r.yMax - 24f, w, 20f), "⚠ " + problem, new GUIStyle(hintStyle) { normal = { textColor = GolfSimTheme.Warning } });
        }

        /// <summary>AUTO PUTT: on / off, the 1-putt and 2-putt circle sizes and what happens outside them.</summary>
        private void DrawAutoPutt(Rect r)
        {
            AppSettings s = AppSettings.Current;
            GUI.Box(r, GUIContent.none, GolfSimTheme.Card);
            float x = r.x + 16f, y = r.y + 12f;
            GUI.Label(new Rect(x, y, 400f, 18f), "AUTO PUTT", GolfSimTheme.Label);
            GUI.Label(new Rect(x + 120f, y, r.width - 150f, 18f), "A ball that stops on the green is putted for you: inside the gold circle 1 putt, inside the white circle 2 putts.", hintStyle);
            y += 28f;
            bool changed = false;
            if (GUI.Button(new Rect(x, y, 90f, 38f), s.autoPutt ? "ON" : "OFF", s.autoPutt ? GolfSimTheme.AccentButton : GolfSimTheme.Button)) { s.autoPutt = !s.autoPutt; changed = true; }
            float col = (r.width - 140f) / 3f;
            float cx = x + 110f;
            GUI.enabled = s.autoPutt;
            GUI.Label(new Rect(cx, y - 2f, col - 12f, 16f), "1 PUTT CIRCLE", new GUIStyle(hintStyle) { normal = { textColor = GolfSimTheme.Gold } });
            if (Arrows(new Rect(cx, y + 14f, col - 12f, 34f), Units.DistanceText(s.autoPuttOneMeters, "0.0"), out int d1))
            {
                s.autoPuttOneMeters = Mathf.Clamp(s.autoPuttOneMeters + d1 * 0.5f, 0.5f, 10f);
                if (s.autoPuttTwoMeters < s.autoPuttOneMeters + 0.5f) s.autoPuttTwoMeters = s.autoPuttOneMeters + 0.5f;
                changed = true;
            }
            cx += col;
            GUI.Label(new Rect(cx, y - 2f, col - 12f, 16f), "2 PUTT CIRCLE", hintStyle);
            if (Arrows(new Rect(cx, y + 14f, col - 12f, 34f), Units.DistanceText(s.autoPuttTwoMeters, "0.0"), out int d2))
            {
                s.autoPuttTwoMeters = Mathf.Clamp(s.autoPuttTwoMeters + d2 * 0.5f, s.autoPuttOneMeters + 0.5f, 30f);
                changed = true;
            }
            cx += col;
            GUI.Label(new Rect(cx, y - 2f, col - 12f, 16f), "OUTSIDE THE 2 PUTT CIRCLE", hintStyle);
            string[] beyond = { "3 PUTTS", "PUTT IT MYSELF" };
            if (Arrows(new Rect(cx, y + 14f, col - 12f, 34f), beyond[Mathf.Clamp(s.autoPuttBeyond, 0, 1)], out int d3)) { s.autoPuttBeyond = s.autoPuttBeyond == 0 ? 1 : 0; changed = true; }
            y += 58f;
            if (GUI.Button(new Rect(x + 110f, y, 260f, 34f), s.showPuttCircles ? "CIRCLES ON THE GREEN: SHOWN" : "CIRCLES ON THE GREEN: HIDDEN", GolfSimTheme.SmallButton)) { s.showPuttCircles = !s.showPuttCircles; changed = true; }
            GUI.enabled = true;
            GUI.Label(new Rect(x + 390f, y + 6f, r.width - 420f, 30f), "Also in SETTINGS → GAME and in the game MENU → AUTO PUTT.", hintStyle);
            if (changed) s.Save();
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

        private bool Apply(bool forRange = false)
        {
            if (roundPlayers.Count == 0) { message = "Add at least one player."; tab = Tab.Players; return false; }
            if (roundPlayers.Count > PlayerRoster.MaxPlayersInRound) { message = "A round has up to " + PlayerRoster.MaxPlayersInRound + " players."; return false; }
            string problem = GameFormats.Problem(Format, roundPlayers.Count, roundTeams.ToArray());
            if (problem != null && !forRange) { message = problem; return false; }
            int courseHoles = CourseSession.CourseHoles;
            int start = courseHoles > 9 && holesChoice == 2 ? 9 : 0;
            int length = courseHoles > 9 && holesChoice > 0 ? 9 : courseHoles;

            CourseSession.SetRoundSettings(gameModes[selectedGameMode], tees[selectedTee], pins[selectedPins], gimmies[selectedGimmie], mulligans[selectedMulligan], resumeRound, length);
            CourseSession.SetHoles(courseHoles, start, length);
            CourseSession.PracticeMode = false;

            CourseSession.SetPlayers(roundPlayers.ToArray());
            CourseSession.Teams = Format.Team ? CompactTeams() : null;
            return true;
        }

        /// <summary>Keeps the chosen players and teams while the player visits other screens (PLAYERS, SETTINGS).</summary>
        private void SaveDraft()
        {
            CourseSession.SetPlayers(roundPlayers.ToArray());
            CourseSession.Teams = roundTeams.ToArray();
        }

        /// <summary>Team for a newly added player: the first team that still has room.</summary>
        private int NextTeam()
        {
            int size = Mathf.Max(2, Format.MinTeamSize);
            var counts = new int[GameFormats.MaxTeams];
            foreach (int t in roundTeams) counts[t]++;
            for (int t = 0; t < GameFormats.MaxTeams; t++) if (counts[t] < size) return t;
            return GameFormats.MaxTeams - 1;
        }

        /// <summary>Teams renumbered so the used teams are A, B, C ... in order (no gaps).</summary>
        private int[] CompactTeams()
        {
            var map = new int[GameFormats.MaxTeams];
            for (int i = 0; i < map.Length; i++) map[i] = -1;
            int next = 0;
            var used = new bool[GameFormats.MaxTeams];
            foreach (int t in roundTeams) used[t] = true;
            for (int t = 0; t < GameFormats.MaxTeams; t++) if (used[t]) map[t] = next++;
            var result = new int[roundTeams.Count];
            for (int i = 0; i < result.Length; i++) result[i] = map[roundTeams[i]];
            return result;
        }

        private void Play()
        {
            if (Apply()) SceneManager.LoadScene("GolfSimZA_0_6_PlayRound");
        }

        private void HeadToRange()
        {
            if (!Apply(true)) return;
            CourseSession.SetSession("GolfSim ZA Practice Range", "Blue", 18);
            CourseSession.SetCourse("", null);
            CourseSession.SetHoles(18, 0, 18);
            SceneManager.LoadScene("GolfSimZA_0_6_PlayRound");
        }
    }
}
