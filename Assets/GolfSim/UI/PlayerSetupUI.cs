using System.Collections.Generic;
using GolfSimZA.Courses;
using GolfSimZA.Players;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfSimZA.UI
{
    public sealed class PlayerSetupUI : MonoBehaviour
    {
        private readonly List<string> players = new List<string> { "Player 1" };
        private GUIStyle titleStyle, subtitleStyle, labelStyle, fieldStyle, buttonStyle, tabStyle, selectedTabStyle, cardStyle;
        private GUIStyle bagHeaderStyle, bagFieldStyle, bagButtonStyle, bagActiveStyle, bagMutedStyle, clubBigStyle, clubRangeStyle, mapLabelStyle;
        private bool stylesReady;
        private GUIStyle selectedCardStyle, startStyle;
        private static Texture2D background;
        private readonly Color muted = new Color(0.70f, 0.78f, 0.81f);
        private bool editingBag;
        private int selectedPlayerIndex = -1;
        private GolfBagProfile bagProfile;

        private bool returnHome;

        private void Start()
        {
            // Players come from the home screen's roster / the current round.
            players.Clear();
            foreach (string name in (CourseSession.PlayerNames ?? "Player 1").Split(new[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries))
                players.Add(name.Trim());
            if (players.Count == 0) players.Add("Player 1");

            returnHome = PlayerPrefs.GetInt("GolfSimZA.BagReturnHome", 0) == 1;
            PlayerPrefs.DeleteKey("GolfSimZA.BagReturnHome");

            string openFor = PlayerPrefs.GetString("GolfSimZA.OpenBagFor", "");
            PlayerPrefs.DeleteKey("GolfSimZA.OpenBagFor");
            if (!string.IsNullOrWhiteSpace(openFor))
            {
                int index = players.FindIndex(p => string.Equals(p, openFor, System.StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                {
                    players.Insert(0, openFor);
                    if (players.Count > 4) players.RemoveAt(players.Count - 1);
                    index = 0;
                }
                selectedPlayerIndex = index;
                OpenBagEditor();
            }
        }

        private void GoBack()
        {
            SceneManager.LoadScene(returnHome ? "GolfSimZA_0_5_CourseSelection" : "GolfSimZA_0_5_RoundSettings");
        }

        private void EnsureStyles()
        {
            GolfSimTheme.Ensure();
            if (stylesReady && titleStyle != null) return;
            titleStyle = new GUIStyle(GolfSimTheme.Title) { fontSize = 26 };
            subtitleStyle = new GUIStyle(GolfSimTheme.Subtitle) { wordWrap = false };
            labelStyle = GolfSimTheme.Label;
            fieldStyle = new GUIStyle(GolfSimTheme.TextField) { fixedHeight = 38 };
            buttonStyle = new GUIStyle(GolfSimTheme.Button) { fixedHeight = 44 };
            tabStyle = GolfSimTheme.Tab;
            selectedTabStyle = GolfSimTheme.TabActive;
            cardStyle = new GUIStyle(GolfSimTheme.Card) { padding = new RectOffset(16, 16, 14, 14) };
            selectedCardStyle = new GUIStyle(GolfSimTheme.SelectedCard) { padding = new RectOffset(16, 16, 14, 14) };
            bagHeaderStyle = MakeLabel(14, FontStyle.Bold, Color.white);
            bagFieldStyle = new GUIStyle(GolfSimTheme.TextField) { fontSize = 13, fixedHeight = 34 };
            bagButtonStyle = new GUIStyle(GolfSimTheme.SmallButton) { fixedHeight = 34 };
            bagActiveStyle = new GUIStyle(GolfSimTheme.AccentButton) { fixedHeight = 34, fontSize = 12 };
            bagMutedStyle = MakeLabel(11, FontStyle.Normal, muted);
            clubBigStyle = MakeLabel(18, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            clubRangeStyle = MakeLabel(11, FontStyle.Bold, GolfSimTheme.Gold, TextAnchor.MiddleCenter);
            mapLabelStyle = MakeLabel(10, FontStyle.Bold, muted, TextAnchor.MiddleCenter);
            startStyle = new GUIStyle(GolfSimTheme.AccentButton) { fixedHeight = 52, fontSize = 17 };
            background = GolfSimTheme.Tex(GolfSimTheme.Navy);
            stylesReady = true;
        }

        private GUIStyle MakeLabel(int size, FontStyle fontStyle, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = size;
            style.fontStyle = fontStyle;
            style.alignment = alignment;
            style.normal.textColor = color;
            return style;
        }


        private void OnGUI()
        {
            EnsureStyles();
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), background);
            if (editingBag) DrawBagEditor(); else DrawPlayerSetup();
        }

        private void DrawPlayerSetup()
        {
            float margin = Mathf.Max(28f, Screen.width * 0.035f);
            float width = Screen.width - margin * 2f;
            GUILayout.BeginArea(new Rect(margin, 22f, width, Screen.height - 44f));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("←  BACK", GolfSimTheme.TopBarButton, GUILayout.Width(110))) GoBack();
            GUILayout.FlexibleSpace();
            GolfSimTheme.DrawLogo(GUILayoutUtility.GetRect(300f, 40f, GUILayout.Width(300f)));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("PLAYERS", GolfSimTheme.TopBarButton, GUILayout.Width(110))) CourseSelectionUI.OpenHome("Players");
            if (GUILayout.Button("SETTINGS", GolfSimTheme.TopBarButton, GUILayout.Width(110))) CourseSelectionUI.OpenHome("Settings");
            GUILayout.EndHorizontal();
            GUI.DrawTexture(GUILayoutUtility.GetRect(width, 2f), GolfSimTheme.AccentTex);
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label("PLAYERS", titleStyle, GUILayout.Width(210));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("01 COURSE", tabStyle, GUILayout.Width(130))) CourseSelectionUI.OpenHome("LocalMatch");
            if (GUILayout.Button("02 ROUND", tabStyle, GUILayout.Width(130))) SceneManager.LoadScene("GolfSimZA_0_5_RoundSettings");
            GUILayout.Button("03 PLAYERS", selectedTabStyle, GUILayout.Width(130));
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            GUILayout.Label(CourseSession.CourseName + "  •  " + CourseSession.RoundLength + " holes  •  " + CourseSession.TeeName + " tees", subtitleStyle);
            GUILayout.Space(18);

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(width * 0.68f));
            GUILayout.Label("SELECT PLAYERS", titleStyle);
            GUILayout.Label("Select a player first. Their personal golf bag and distance map will then become available.", subtitleStyle);
            GUILayout.Space(10);

            for (int i = 0; i < players.Count; i++)
            {
                bool selected = selectedPlayerIndex == i;
                GUILayout.BeginHorizontal(selected ? selectedCardStyle : cardStyle);
                if (GUILayout.Button(selected ? "✓" : "○", selected ? bagActiveStyle : bagButtonStyle, GUILayout.Width(48))) selectedPlayerIndex = i;
                GUILayout.BeginVertical();
                players[i] = GUILayout.TextField(players[i], fieldStyle);
                GUILayout.Label(selected ? "PLAYER SELECTED  •  MAP MY BAG AVAILABLE" : "Select this player to manage their golf bag", bagMutedStyle);
                GUILayout.EndVertical();
                if (players.Count > 1 && GUILayout.Button("REMOVE", buttonStyle, GUILayout.Width(100)))
                {
                    players.RemoveAt(i);
                    if (selectedPlayerIndex == i) selectedPlayerIndex = -1;
                    else if (selectedPlayerIndex > i) selectedPlayerIndex--;
                    i--;
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(7);
            }

            GUILayout.BeginHorizontal();
            if (players.Count < 4 && GUILayout.Button("＋  ADD PLAYER", buttonStyle, GUILayout.Width(190))) players.Add("Player " + (players.Count + 1));
            GUILayout.FlexibleSpace();
            GUI.enabled = selectedPlayerIndex >= 0 && selectedPlayerIndex < players.Count;
            if (GUILayout.Button("MAP MY BAG  →", selectedPlayerIndex >= 0 ? bagActiveStyle : buttonStyle, GUILayout.Width(190))) OpenBagEditor();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(12);
            GUILayout.BeginHorizontal(cardStyle);
            GUILayout.Label("GOLF BAG", GolfSimTheme.Heading, GUILayout.Width(130));
            if (selectedPlayerIndex >= 0 && selectedPlayerIndex < players.Count)
            {
                string name = string.IsNullOrWhiteSpace(players[selectedPlayerIndex]) ? "Player " + (selectedPlayerIndex + 1) : players[selectedPlayerIndex].Trim();
                GUILayout.Label(name + " is selected", labelStyle);
            }
            else
            {
                GUILayout.Label("Select a player above to map their clubs and distances.", subtitleStyle);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(22);
            GUILayout.BeginVertical(cardStyle, GUILayout.Width(width * 0.28f));
            GUILayout.Label("ROUND SUMMARY", GolfSimTheme.Heading);
            GUILayout.Space(10);
            Summary("COURSE", CourseSession.CourseName);
            Summary("TEE", CourseSession.TeeName);
            Summary("ROUND", CourseSession.RoundLength + " holes");
            Summary("MODE", CourseSession.GameMode + (CourseSession.GameMode == "Match Play" && players.Count < 2 ? " (needs 2 players)" : ""));
            Summary("GIMME / MULLIGANS", CourseSession.GimmieSetting + "  /  " + CourseSession.MulliganSetting);
            Summary("PLAYERS", players.Count.ToString());
            GUILayout.Space(12);
            GUILayout.Label("PLAYER", labelStyle);
            if (selectedPlayerIndex >= 0 && selectedPlayerIndex < players.Count)
            {
                string name = string.IsNullOrWhiteSpace(players[selectedPlayerIndex]) ? "Player " + (selectedPlayerIndex + 1) : players[selectedPlayerIndex].Trim();
                GUILayout.Label(name, clubBigStyle);
                GUILayout.Label("READY TO MAP BAG", clubRangeStyle);
            }
            else GUILayout.Label("NONE SELECTED", subtitleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("START ROUND  →", startStyle))
            {
                string[] names = new string[players.Count];
                for (int i = 0; i < players.Count; i++) names[i] = string.IsNullOrWhiteSpace(players[i]) ? "Player " + (i + 1) : players[i].Trim();
                CourseSession.SetPlayers(names);
                // Keep the home screen's player list in step with players added here.
                bool changed = false;
                foreach (string n in names)
                    if (PlayerRoster.Current.Find(n) == null) { PlayerRoster.Current.players.Add(new PlayerProfile { name = n, selected = false }); changed = true; }
                if (changed) PlayerRoster.Current.Save();
                CourseSession.SetPlayers(names);
                SceneManager.LoadScene("GolfSimZA_0_6_PlayRound");
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void OpenBagEditor()
        {
            if (selectedPlayerIndex < 0 || selectedPlayerIndex >= players.Count) return;
            string name = string.IsNullOrWhiteSpace(players[selectedPlayerIndex]) ? "Player " + (selectedPlayerIndex + 1) : players[selectedPlayerIndex].Trim();
            bagProfile = GolfBagProfile.Load(name);
            editingBag = true;
        }

        private void CloseBagEditor(bool save)
        {
            if (save && bagProfile != null && selectedPlayerIndex >= 0 && selectedPlayerIndex < players.Count)
            {
                string name = string.IsNullOrWhiteSpace(players[selectedPlayerIndex]) ? "Player " + (selectedPlayerIndex + 1) : players[selectedPlayerIndex].Trim();
                bagProfile.Save(name);
            }
            editingBag = false;
        }

        private void DrawBagEditor()
        {
            float margin = Mathf.Max(24f, Screen.width * 0.028f);
            float width = Screen.width - margin * 2f;
            string playerName = string.IsNullOrWhiteSpace(players[selectedPlayerIndex]) ? "Player " + (selectedPlayerIndex + 1) : players[selectedPlayerIndex].Trim();

            GUILayout.BeginArea(new Rect(margin, 20f, width, Screen.height - 40f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("‹  PLAYERS", buttonStyle, GUILayout.Width(130))) CloseBagEditor(true);
            GUILayout.FlexibleSpace();
            GUILayout.Label("MAP MY BAG", titleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(playerName, labelStyle, GUILayout.Width(180));
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            GUILayout.Label("TRACKMAN-STYLE CLUB DISTANCE MAPPING  •  " + CourseSession.CourseName, subtitleStyle);
            GUILayout.Space(12);

            GUILayout.BeginHorizontal();
            DrawClubLibraryPanel(width * 0.47f);
            GUILayout.Space(14);
            DrawBagMapPanel(width * 0.51f);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        /// <summary>
        /// Every club in the library in a two-column grid that always fits the screen (no scrolling):
        /// tick to put it in the bag (up to 18), MAP to hit six shots with it.
        /// </summary>
        private void DrawClubLibraryPanel(float width)
        {
            GUILayout.BeginVertical(cardStyle, GUILayout.Width(width));
            GUILayout.BeginHorizontal();
            GUILayout.Label("SELECT A CLUB", GolfSimTheme.Heading);
            GUILayout.FlexibleSpace();
            GUILayout.Label(CountClubsInBag() + " / " + GolfBagProfile.MaxBagClubs + " IN BAG", bagMutedStyle);
            GUILayout.EndHorizontal();
            GUILayout.Label("Tick a club to put it in this player's bag. MAP hits six shots to learn its carry and total.", bagMutedStyle);
            GUILayout.Space(6);

            Rect area = GUILayoutUtility.GetRect(width - 40f, Screen.height - 190f);
            int count = GolfBagProfile.ClubCount;
            int columns = 2;
            int rows = Mathf.CeilToInt(count / (float)columns);
            float gap = 4f;
            float rowH = Mathf.Max(24f, (area.height - gap * (rows - 1)) / rows);
            float colW = (area.width - gap) / columns;
            var nameStyle = new GUIStyle(clubBigStyle) { fontSize = rowH >= 40f ? 16 : 13, alignment = TextAnchor.MiddleLeft };
            var small = new GUIStyle(bagMutedStyle) { alignment = TextAnchor.MiddleLeft };
            var range = new GUIStyle(clubRangeStyle) { alignment = TextAnchor.MiddleLeft };
            var tick = new GUIStyle(bagButtonStyle) { fixedHeight = 0 };
            var tickOn = new GUIStyle(bagActiveStyle) { fixedHeight = 0 };
            int inBagCount = CountClubsInBag();
            string playerName = CurrentPlayerName();

            for (int i = 0; i < count; i++)
            {
                int col = i / rows, row = i % rows;
                Rect cell = new Rect(area.x + col * (colW + gap), area.y + row * (rowH + gap), colW, rowH);
                bool inBag = bagProfile.InBag[i];
                GUI.Box(cell, GUIContent.none, inBag ? GolfSimTheme.SelectedCard : GolfSimTheme.Card);

                float bh = Mathf.Min(34f, rowH - 6f);
                bool canAdd = inBag || inBagCount < GolfBagProfile.MaxBagClubs;
                GUI.enabled = canAdd;
                if (GUI.Button(new Rect(cell.x + 6f, cell.y + (rowH - bh) * 0.5f, bh, bh), inBag ? "✓" : "+", inBag ? tickOn : tick))
                {
                    bagProfile.InBag[i] = !inBag;
                    bagProfile.Save(playerName);
                }
                GUI.enabled = true;

                float x = cell.x + bh + 14f;
                float nameW = Mathf.Min(130f, colW * 0.36f);
                GUI.Label(new Rect(x, cell.y, nameW, rowH), bagProfile.ClubNames[i], nameStyle);
                x += nameW;
                GUI.Label(new Rect(x, cell.y, 48f, rowH), bagProfile.Lofts[i].ToString("0.#") + "°", small);
                x += 50f;
                float mapW = inBag ? 58f : 0f;
                string mapped = bagProfile.CarryMeters[i] > 0f ? bagProfile.CarryMeters[i].ToString("F0") + "–" + bagProfile.TotalMeters[i].ToString("F0") + " m" : (inBag ? "NOT MAPPED" : "");
                GUI.Label(new Rect(x, cell.y, cell.xMax - x - mapW - 8f, rowH), mapped, range);
                if (inBag && GUI.Button(new Rect(cell.xMax - mapW - 6f, cell.y + (rowH - bh) * 0.5f, mapW, bh), "MAP", tickOn))
                {
                    bagProfile.Save(playerName);
                    PlayerPrefs.SetInt("GolfSimZA.MapMode", 1);
                    PlayerPrefs.SetString("GolfSimZA.MapPlayer", playerName);
                    PlayerPrefs.SetInt("GolfSimZA.MapClubIndex", i);
                    PlayerPrefs.Save();
                    SceneManager.LoadScene("GolfSimZA_0_6_PlayRound");
                }
            }
            GUILayout.EndVertical();
        }

        private string CurrentPlayerName()
        {
            if (selectedPlayerIndex < 0 || selectedPlayerIndex >= players.Count) return "Player 1";
            return string.IsNullOrWhiteSpace(players[selectedPlayerIndex]) ? "Player " + (selectedPlayerIndex + 1) : players[selectedPlayerIndex].Trim();
        }

        private void DrawBagMapPanel(float width)
        {
            GUILayout.BeginVertical(cardStyle, GUILayout.Width(width));
            GUILayout.BeginHorizontal();
            GUILayout.Label("DISTANCE MAP", titleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label("CARRY • TOTAL", bagMutedStyle);
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            float maxDistance = Mathf.Max(200f, LongestTotal() + 20f);
            float chartHeight = Mathf.Min(500f, Screen.height - 245f);
            Rect chart = GUILayoutUtility.GetRect(width - 30f, chartHeight);
            GUI.Box(chart, GUIContent.none, cardStyle);

            GUI.Label(new Rect(chart.x + 10f, chart.y + 6f, chart.width - 20f, 18f), "PLAYER DISTANCE PROFILE", mapLabelStyle);
            DrawDistanceGrid(chart, maxDistance);

            int visible = 0;
            for (int i = 0; i < GolfBagProfile.ClubCount; i++)
            {
                if (!bagProfile.InBag[i] || bagProfile.TotalMeters[i] <= 0f) continue;
                float y = chart.yMax - 30f - (bagProfile.TotalMeters[i] / maxDistance) * (chart.height - 65f);
                float x = chart.x + 40f + visible * Mathf.Max(28f, (chart.width - 80f) / Mathf.Max(1, CountMappedInBag()));
                GUI.Label(new Rect(x - 24f, y - 18f, 48f, 18f), ShortClubName(bagProfile.ClubNames[i]), mapLabelStyle);
                GUI.Label(new Rect(x - 28f, y, 56f, 18f), bagProfile.TotalMeters[i].ToString("F0") + "m", clubRangeStyle);
                visible++;
            }

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Mapped", bagMutedStyle, GUILayout.Width(70));
            GUILayout.Label(CountMappedInBag() + " / " + CountClubsInBag() + " clubs", labelStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("SAVE BAG", bagActiveStyle, GUILayout.Width(130))) CloseBagEditor(true);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawDistanceGrid(Rect chart, float maxDistance)
        {
            int lines = 5;
            for (int i = 1; i <= lines; i++)
            {
                float value = maxDistance * i / lines;
                float y = chart.yMax - 30f - (value / maxDistance) * (chart.height - 65f);
                GUI.Label(new Rect(chart.x + 4f, y - 8f, 34f, 16f), value.ToString("F0"), mapLabelStyle);
                GUI.Box(new Rect(chart.x + 38f, y, chart.width - 48f, 1f), GUIContent.none);
            }
        }

        private string ShortClubName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Club";
            string n = name.Trim();
            if (n == "Driver") return "Dr";
            if (n == "3 Wood") return "3W";
            if (n == "5 Wood") return "5W";
            if (n == "4 Hybrid") return "4H";
            if (n == "Pitching Wedge") return "PW";
            if (n == "Gap Wedge") return "GW";
            if (n == "Sand Wedge") return "SW";
            if (n == "Lob Wedge") return "LW";
            if (n == "Putter") return "PT";
            return n.Replace(" Iron", "i");
        }

        private int CountClubsInBag()
        {
            int count = 0;
            if (bagProfile == null) return count;
            for (int i = 0; i < GolfBagProfile.ClubCount; i++) if (bagProfile.InBag[i]) count++;
            return count;
        }

        private int CountMappedInBag()
        {
            int count = 0;
            if (bagProfile == null) return count;
            for (int i = 0; i < GolfBagProfile.ClubCount; i++) if (bagProfile.InBag[i] && bagProfile.TotalMeters[i] > 0f) count++;
            return count;
        }

        private float LongestTotal()
        {
            float max = 0f;
            if (bagProfile == null) return max;
            for (int i = 0; i < GolfBagProfile.ClubCount; i++) if (bagProfile.InBag[i]) max = Mathf.Max(max, bagProfile.TotalMeters[i]);
            return max;
        }

        private void Summary(string key, string value)
        {
            GUILayout.Label(key, labelStyle);
            GUILayout.Label(value, subtitleStyle);
            GUILayout.Space(6);
        }
    }
}
