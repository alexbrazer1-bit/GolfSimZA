using System;
using GolfSimZA.Courses;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfSimZA.UI
{
    /// <summary>
    /// Step 2 of a Local Match: round settings. Three pages (Round setup, Course setup,
    /// Scoring &amp; assistance); every option here is used by the round.
    /// </summary>
    public sealed class RoundSettingsUI : MonoBehaviour
    {
        private enum Page { Round, Course, Scoring }

        // Tees come from the selected course (imported courses have their own tee boxes).
        private string[] tees = { "Red", "White", "Blue", "Black" };
        private readonly string[] gameModes = { "Stroke Play", "Match Play" };
        private readonly string[] pins = { "Easy", "Standard", "Tournament" };
        private readonly string[] gimmies = { "Off", "1 m", "2 m", "3 m" };
        private readonly string[] mulligans = { "Off", "1", "2", "3", "Unlimited" };
        private readonly string[] lengths = { "18 holes", "9 holes" };
        private static Texture2D background;

        private int selectedTee;
        private int selectedGameMode;
        private int selectedPins = 1;
        private int selectedGimmie = 1;
        private int selectedMulligan = 4;
        private int selectedLength;
        private bool resumeRound;
        private Page page = Page.Round;
        private int players;
        private GUIStyle valueStyle, arrowStyle, hintStyle;

        private void Start()
        {
            tees = CourseSession.AvailableTees;
            selectedTee = Mathf.Max(0, Array.IndexOf(tees, CourseSession.TeeName));
            selectedGameMode = Mathf.Max(0, Array.IndexOf(gameModes, CourseSession.GameMode));
            selectedPins = Mathf.Max(0, Array.IndexOf(pins, CourseSession.PinSetting));
            selectedGimmie = Mathf.Max(0, Array.IndexOf(gimmies, CourseSession.GimmieSetting));
            int m = Array.IndexOf(mulligans, CourseSession.MulliganSetting);
            selectedMulligan = m >= 0 ? m : 4;

            selectedLength = CourseSession.RoundLength <= 9 ? 1 : 0;
            resumeRound = false;
            players = GolfSimZA.Players.PlayerRoster.Current.Selected.Count;
        }

        private void EnsureStyles()
        {
            GolfSimTheme.Ensure();
            if (valueStyle != null) return;
            valueStyle = new GUIStyle(GolfSimTheme.Center) { fontSize = 18 };
            arrowStyle = new GUIStyle(GolfSimTheme.Button) { fontSize = 20, fixedHeight = 40 };
            hintStyle = new GUIStyle(GolfSimTheme.Subtitle) { fontSize = 12 };
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (background == null) background = GolfSimTheme.Tex(GolfSimTheme.Navy);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), background);

            float margin = Mathf.Clamp(Screen.width * 0.03f, 16f, 48f);
            float width = Screen.width - margin * 2f;

            // Top bar
            if (GUI.Button(new Rect(margin, 16f, 110f, 38f), "←  BACK", GolfSimTheme.TopBarButton)) CourseSelectionUI.OpenHome("LocalMatch");
            GolfSimTheme.DrawLogo(new Rect(Screen.width * 0.5f - 150f, 14f, 300f, 40f));
            if (GUI.Button(new Rect(Screen.width - margin - 250f, 16f, 120f, 38f), "PLAYERS", GolfSimTheme.TopBarButton)) CourseSelectionUI.OpenHome("Players");
            if (GUI.Button(new Rect(Screen.width - margin - 124f, 16f, 124f, 38f), "SETTINGS", GolfSimTheme.TopBarButton)) CourseSelectionUI.OpenHome("Settings");
            GUI.DrawTexture(new Rect(margin, 62f, width, 2f), GolfSimTheme.AccentTex);

            // Title + steps
            float y = 80f;
            GUI.Label(new Rect(margin, y, 400f, 40f), "ROUND SETTINGS", GolfSimTheme.Title);
            float stepW = Mathf.Min(140f, width * 0.14f);
            float sx = margin + width - stepW * 3f - 8f;
            if (GUI.Button(new Rect(sx, y, stepW, 38f), "01 COURSE", GolfSimTheme.Tab)) CourseSelectionUI.OpenHome("LocalMatch");
            GUI.Button(new Rect(sx + stepW + 4f, y, stepW, 38f), "02 ROUND", GolfSimTheme.TabActive);
            if (GUI.Button(new Rect(sx + (stepW + 4f) * 2f, y, stepW, 38f), "03 PLAYERS", GolfSimTheme.Tab)) Continue();
            y += 56f;

            // Side navigation
            float navW = Mathf.Clamp(width * 0.23f, 180f, 300f);
            string[] navNames = { "ROUND SETUP", "COURSE SETUP", "SCORING & ASSISTANCE" };
            for (int i = 0; i < navNames.Length; i++)
                if (GolfSimTheme.FitButton(new Rect(margin, y + i * 50f, navW, 44f), navNames[i], page == (Page)i ? GolfSimTheme.TabActive : GolfSimTheme.Button)) page = (Page)i;
            if (GolfSimTheme.FitButton(new Rect(margin, y + 170f, navW, 44f), "RECOMMENDED SETTINGS", GolfSimTheme.Button)) Recommended();

            // Content
            float cx = margin + navW + 24f;
            float cw = width - navW - 24f;
            float cardW = (cw - 14f) * 0.5f, cardH = 112f;
            GUI.Label(new Rect(cx, y - 4f, cw, 30f), navNames[(int)page], GolfSimTheme.Heading);
            GUI.Label(new Rect(cx, y + 24f, cw, 20f), CourseSession.CourseName + "  •  " + players + (players == 1 ? " player" : " players"), GolfSimTheme.Subtitle);
            float cy = y + 54f;

            switch (page)
            {
                case Page.Round:
                    SettingCard(new Rect(cx, cy, cardW, cardH), "GAME MODE", gameModes, ref selectedGameMode,
                        selectedGameMode == 1 ? (players >= 2 ? "Lowest score wins each hole" : "Needs 2+ players - plays as stroke play") : "Total strokes over the round");
                    SettingCard(new Rect(cx + cardW + 14f, cy, cardW, cardH), "TEE BOX", tees, ref selectedTee, "Hole lengths use these tees");
                    SettingCard(new Rect(cx, cy + cardH + 14f, cardW, cardH), "PINS", pins, ref selectedPins, "Pin position on every green");
                    break;
                case Page.Course:
                    SettingCard(new Rect(cx, cy, cardW, cardH), "ROUND LENGTH", lengths, ref selectedLength, "Front nine or the full course");
                    SettingCard(new Rect(cx + cardW + 14f, cy, cardW, cardH), "TEE BOX", tees, ref selectedTee, "Hole lengths use these tees");
                    ResumeCard(new Rect(cx, cy + cardH + 14f, cw, cardH));
                    break;
                case Page.Scoring:
                    SettingCard(new Rect(cx, cy, cardW, cardH), "GIMME / AUTO PUTT", gimmies, ref selectedGimmie, "Balls this close are holed (+1 stroke)");
                    SettingCard(new Rect(cx + cardW + 14f, cy, cardW, cardH), "MULLIGANS", mulligans, ref selectedMulligan, "Per player per round (Menu → MULLIGAN)");
                    break;
            }

            // Summary + continue
            string summary = gameModes[selectedGameMode] + "  •  " + tees[selectedTee] + " tees  •  " + pins[selectedPins] + " pins  •  " +
                             lengths[selectedLength] + "  •  gimme " + gimmies[selectedGimmie] + "  •  mulligans " + mulligans[selectedMulligan] + (resumeRound ? "  •  resume" : "");
            GUI.Label(new Rect(margin, Screen.height - 64f, width - 300f, 40f), summary, hintStyle);
            if (GUI.Button(new Rect(Screen.width - margin - 280f, Screen.height - 70f, 280f, 50f), "CONTINUE TO PLAYERS  →", new GUIStyle(GolfSimTheme.AccentButton) { fixedHeight = 50f, fontSize = 16 }))
                Continue();
        }

        private void Continue()
        {
            int holes = selectedLength == 0 ? 18 : 9; // a shorter course simply ends after its last hole
            CourseSession.SetRoundSettings(gameModes[selectedGameMode], tees[selectedTee], pins[selectedPins], gimmies[selectedGimmie], mulligans[selectedMulligan], resumeRound, holes);
            SceneManager.LoadScene("GolfSimZA_0_5_Players");
        }

        private void Recommended()
        {
            selectedGameMode = 0;
            selectedPins = 1;
            selectedGimmie = 1;
            selectedMulligan = 4;
            selectedLength = 0;
            resumeRound = false;
        }

        private void SettingCard(Rect r, string title, string[] values, ref int selected, string hint)
        {
            GUI.Box(r, GUIContent.none, GolfSimTheme.Card);
            GUI.Label(new Rect(r.x + 16f, r.y + 12f, r.width - 32f, 18f), title, GolfSimTheme.Label);
            float by = r.y + 38f;
            if (GUI.Button(new Rect(r.x + 14f, by, 48f, 40f), "‹", arrowStyle)) selected = (selected - 1 + values.Length) % values.Length;
            GUI.Label(new Rect(r.x + 66f, by, r.width - 132f, 40f), values[selected], valueStyle);
            if (GUI.Button(new Rect(r.xMax - 62f, by, 48f, 40f), "›", arrowStyle)) selected = (selected + 1) % values.Length;
            GUI.Label(new Rect(r.x + 16f, r.yMax - 28f, r.width - 32f, 18f), GolfSimTheme.Ellipsize(hint, hintStyle, r.width - 32f), hintStyle);
        }

        private void ResumeCard(Rect r)
        {
            GUI.Box(r, GUIContent.none, GolfSimTheme.Card);
            GUI.Label(new Rect(r.x + 16f, r.y + 12f, r.width - 32f, 18f), "RESUME ROUND", GolfSimTheme.Label);
            if (GUI.Button(new Rect(r.x + 14f, r.y + 38f, 110f, 40f), resumeRound ? "ON" : "OFF", resumeRound ? GolfSimTheme.AccentButton : GolfSimTheme.Button))
                resumeRound = !resumeRound;
            GUI.Label(new Rect(r.x + 140f, r.y + 42f, r.width - 160f, 36f),
                "Continue the saved round when the course and players match.  " + RoundSave.Describe(), new GUIStyle(GolfSimTheme.Body) { fontSize = 12 });
            if (RoundSave.Exists && GUI.Button(new Rect(r.xMax - 150f, r.yMax - 36f, 136f, 28f), "DELETE SAVED", GolfSimTheme.SmallButton))
                RoundSave.Clear();
        }
    }
}
