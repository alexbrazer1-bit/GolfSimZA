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


        private void Start()
        {
            // MAP MY BAG: every player in the roster can map their own bag.
            players.Clear();
            foreach (PlayerProfile p in PlayerRoster.Current.players) players.Add(p.name);
            if (players.Count == 0) players.Add("Player 1");

            PlayerPrefs.DeleteKey("GolfSimZA.BagReturnHome");
            // Opened for a player (home / players page) or coming back from mapping a club.
            string openFor = PlayerPrefs.GetString("GolfSimZA.OpenBagFor", "");
            PlayerPrefs.DeleteKey("GolfSimZA.OpenBagFor");
            string mapped = PlayerPrefs.GetString("GolfSimZA.MapReturnPlayer", "");
            PlayerPrefs.DeleteKey("GolfSimZA.MapReturnPlayer");
            PlayerPrefs.Save();
            string target = !string.IsNullOrWhiteSpace(openFor) ? openFor : mapped;
            if (!string.IsNullOrWhiteSpace(target)) SelectPlayer(target);
        }

        /// <summary>Opens the bag of this player (added to the roster when new).</summary>
        private void SelectPlayer(string name)
        {
            name = PlayerRoster.Clean(name);
            if (string.IsNullOrWhiteSpace(name)) return;
            if (editingBag) CloseBagEditor(true);
            int index = players.FindIndex(p => string.Equals(p, name, System.StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                PlayerProfile created = PlayerRoster.Current.Find(name) ?? PlayerRoster.Current.Add(name);
                players.Add(created.name);
                index = players.Count - 1;
            }
            selectedPlayerIndex = index;
            OpenBagEditor();
        }

        private void GoBack()
        {
            if (editingBag) CloseBagEditor(true);
            SceneManager.LoadScene("GolfSimZA_0_5_CourseSelection");
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

        private string newPlayerName = "";
        private readonly Dictionary<string, Vector2Int> bagSummaries = new Dictionary<string, Vector2Int>();
        private Vector2 chooseScroll;

        /// <summary>MAP MY BAG: pick whose bag to map (or create a new player).</summary>
        private void DrawPlayerSetup()
        {
            float margin = Mathf.Max(28f, Screen.width * 0.035f);
            float width = Screen.width - margin * 2f;
            if (GUI.Button(new Rect(margin, 22f, 110f, 38f), "←  BACK", GolfSimTheme.TopBarButton)) GoBack();
            GolfSimTheme.DrawLogo(new Rect(Screen.width * 0.5f - 150f, 20f, 300f, 40f));
            GUI.DrawTexture(new Rect(margin, 68f, width, 2f), GolfSimTheme.AccentTex);
            GUI.Label(new Rect(margin, 84f, width, 40f), "MAP MY BAG  •  CHOOSE A PLAYER", titleStyle);
            GUI.Label(new Rect(margin, 124f, width, 22f), "Every player has their own golf bag and mapped distances. Pick the player whose bag you want to set up.", subtitleStyle);

            PlayerRoster roster = PlayerRoster.Current;
            const int cols = 4;
            float gap = 12f, tileH = 86f;
            float tileW = (width - gap * (cols - 1)) / cols;
            int rows = Mathf.CeilToInt(roster.players.Count / (float)cols);
            Rect view = new Rect(margin, 160f, width, Screen.height - 160f - 150f);
            Rect content = new Rect(0, 0, width - 20f, Mathf.Max(view.height, rows * (tileH + gap)));
            chooseScroll = GUI.BeginScrollView(view, chooseScroll, content);
            tileW = (content.width - gap * (cols - 1)) / cols;
            for (int i = 0; i < roster.players.Count; i++)
            {
                PlayerProfile p = roster.players[i];
                Rect t = new Rect((i % cols) * (tileW + gap), (i / cols) * (tileH + gap), tileW, tileH);
                if (GUI.Button(t, GUIContent.none, GolfSimTheme.Card)) { SelectPlayer(p.name); GUI.EndScrollView(); return; }
                GUI.color = PlayerRoster.Colors[roster.ColorIndex(p)];
                GUI.DrawTexture(new Rect(t.x + 3f, t.y + 3f, 8f, t.height - 6f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(t.x + 22f, t.y + 12f, t.width - 30f, 28f), p.name.ToUpperInvariant(), GolfSimTheme.Heading);
                if (!bagSummaries.TryGetValue(p.name, out Vector2Int counts))
                {
                    GolfBagProfile bag = GolfBagProfile.Load(p.name);
                    for (int c = 0; c < GolfBagProfile.ClubCount; c++)
                        if (bag.InBag[c]) { counts.x++; if (bag.CarryMeters[c] > 0f) counts.y++; }
                    bagSummaries[p.name] = counts;
                }
                int inBag = counts.x, mapped = counts.y;
                GUI.Label(new Rect(t.x + 22f, t.y + 46f, t.width - 30f, 22f), inBag + " clubs in bag  •  " + mapped + " mapped", subtitleStyle);
            }
            GUI.EndScrollView();

            float y = Screen.height - 130f;
            GUI.Label(new Rect(margin, y, 400f, 20f), "NEW PLAYER", labelStyle);
            newPlayerName = GUI.TextField(new Rect(margin, y + 24f, Mathf.Min(420f, width * 0.5f), 40f), newPlayerName ?? "", GolfSimTheme.TextField);
            if (GUI.Button(new Rect(margin + Mathf.Min(420f, width * 0.5f) + 12f, y + 24f, 260f, 40f), "CREATE & MAP BAG  →", GolfSimTheme.AccentButton)
                && !string.IsNullOrWhiteSpace(newPlayerName))
            {
                SelectPlayer(newPlayerName);
                newPlayerName = "";
            }
        }

        private void SwitchPlayer(int direction)
        {
            if (players.Count == 0) return;
            CloseBagEditor(true);
            selectedPlayerIndex = ((selectedPlayerIndex + direction) % players.Count + players.Count) % players.Count;
            OpenBagEditor();
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
            bagSummaries.Clear();
        }

        private void DrawBagEditor()
        {
            float margin = Mathf.Max(24f, Screen.width * 0.028f);
            float width = Screen.width - margin * 2f;
            string playerName = string.IsNullOrWhiteSpace(players[selectedPlayerIndex]) ? "Player " + (selectedPlayerIndex + 1) : players[selectedPlayerIndex].Trim();

            GUILayout.BeginArea(new Rect(margin, 20f, width, Screen.height - 40f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("←  HOME", buttonStyle, GUILayout.Width(120))) { GoBack(); GUILayout.EndHorizontal(); GUILayout.EndArea(); return; }
            if (GUILayout.Button("CHOOSE PLAYER", buttonStyle, GUILayout.Width(170))) { CloseBagEditor(true); GUILayout.EndHorizontal(); GUILayout.EndArea(); return; }
            GUILayout.FlexibleSpace();
            GUILayout.Label("MAP MY BAG", titleStyle);
            GUILayout.FlexibleSpace();
            // Switch straight to another player's bag.
            if (GUILayout.Button("‹", buttonStyle, GUILayout.Width(44))) { SwitchPlayer(-1); GUILayout.EndHorizontal(); GUILayout.EndArea(); return; }
            GUILayout.Label(playerName.ToUpperInvariant(), new GUIStyle(clubBigStyle) { normal = { textColor = PlayerRoster.ColorFor(playerName) } }, GUILayout.Width(200), GUILayout.Height(44));
            if (GUILayout.Button("›", buttonStyle, GUILayout.Width(44))) { SwitchPlayer(1); GUILayout.EndHorizontal(); GUILayout.EndArea(); return; }
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
    }
}
