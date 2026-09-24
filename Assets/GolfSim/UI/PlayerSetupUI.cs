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
        private readonly Color navy = new Color(0.015f, 0.055f, 0.065f);
        private readonly Color panel = new Color(0.055f, 0.14f, 0.17f);
        private readonly Color panelDark = new Color(0.02f, 0.055f, 0.065f);
        private readonly Color panelLight = new Color(0.10f, 0.21f, 0.25f);
        private readonly Color blue = new Color(0.03f, 0.48f, 0.82f);
        private readonly Color blueBright = new Color(0.08f, 0.64f, 1f);
        private readonly Color orange = new Color(1f, 0.56f, 0.08f);
        private readonly Color muted = new Color(0.70f, 0.78f, 0.81f);
        private bool editingBag;
        private int selectedPlayerIndex = -1;
        private GolfBagProfile bagProfile;
        private Vector2 bagScroll;

        private void EnsureStyles()
        {
            if (stylesReady) return;
            titleStyle = MakeLabel(28, FontStyle.Bold, Color.white);
            subtitleStyle = MakeLabel(15, FontStyle.Normal, muted);
            labelStyle = MakeLabel(14, FontStyle.Bold, new Color(0.82f, 0.87f, 0.89f));
            fieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 16, fixedHeight = 42, padding = new RectOffset(12, 12, 8, 8) };
            buttonStyle = MakeButton(15, 44f, panel);
            tabStyle = MakeButton(14, 42f, panel);
            selectedTabStyle = MakeButton(14, 42f, Color.white, navy);
            cardStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(16, 16, 14, 14), normal = { background = MakeTexture(panel) } };
            bagHeaderStyle = MakeLabel(14, FontStyle.Bold, Color.white);
            bagFieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 13, fixedHeight = 34, padding = new RectOffset(8, 8, 6, 6) };
            bagButtonStyle = MakeButton(12, 34f, panelLight);
            bagActiveStyle = MakeButton(12, 34f, blue, Color.white);
            bagMutedStyle = MakeLabel(11, FontStyle.Normal, muted);
            clubBigStyle = MakeLabel(18, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            clubRangeStyle = MakeLabel(11, FontStyle.Bold, orange, TextAnchor.MiddleCenter);
            mapLabelStyle = MakeLabel(10, FontStyle.Bold, muted, TextAnchor.MiddleCenter);
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

        private GUIStyle MakeButton(int size, float height, Color background, Color text = default(Color))
        {
            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.fontSize = size;
            style.fontStyle = FontStyle.Bold;
            style.fixedHeight = height;
            style.normal.background = MakeTexture(background);
            style.hover.background = MakeTexture(blueBright);
            style.active.background = MakeTexture(blue);
            style.normal.textColor = text == default(Color) ? Color.white : text;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            return style;
        }

        private Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void OnGUI()
        {
            EnsureStyles();
            GUI.backgroundColor = navy;
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);
            if (editingBag) DrawBagEditor(); else DrawPlayerSetup();
        }

        private void DrawPlayerSetup()
        {
            float margin = Mathf.Max(28f, Screen.width * 0.035f);
            float width = Screen.width - margin * 2f;
            GUILayout.BeginArea(new Rect(margin, 22f, width, Screen.height - 44f));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("‹  BACK", buttonStyle, GUILayout.Width(110))) SceneManager.LoadScene("GolfSimZA_0_5_RoundSettings");
            GUILayout.FlexibleSpace();
            GUILayout.Label("GOLFSIM ZA", titleStyle, GUILayout.Width(190));
            GUILayout.FlexibleSpace();
            GUILayout.Label("PLAYERS    ⚙ SETTINGS", labelStyle, GUILayout.Width(210));
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label("PLAYERS", titleStyle, GUILayout.Width(210));
            GUILayout.FlexibleSpace();
            GUILayout.Label("01 COURSE", tabStyle, GUILayout.Width(130));
            GUILayout.Label("02 ROUND", tabStyle, GUILayout.Width(130));
            GUILayout.Label("03 PLAYERS", selectedTabStyle, GUILayout.Width(130));
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
                GUILayout.BeginHorizontal(selected ? selectedTabStyle : cardStyle);
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
            GUILayout.Label("GOLF BAG", titleStyle, GUILayout.Width(130));
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
            GUILayout.Label("ROUND SUMMARY", titleStyle);
            GUILayout.Space(10);
            Summary("COURSE", CourseSession.CourseName);
            Summary("TEE", CourseSession.TeeName);
            Summary("ROUND", CourseSession.RoundLength + " holes");
            Summary("MODE", CourseSession.GameMode);
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
            if (GUILayout.Button("START ROUND  →", buttonStyle, GUILayout.Height(54)))
            {
                string[] names = new string[players.Count];
                for (int i = 0; i < players.Count; i++) names[i] = string.IsNullOrWhiteSpace(players[i]) ? "Player " + (i + 1) : players[i].Trim();
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
            bagScroll = Vector2.zero;
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

        private void DrawClubLibraryPanel(float width)
        {
            GUILayout.BeginVertical(cardStyle, GUILayout.Width(width));
            GUILayout.BeginHorizontal();
            GUILayout.Label("SELECT A CLUB", titleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label("14 CLUBS", bagMutedStyle);
            GUILayout.EndHorizontal();
            GUILayout.Label("Mapped clubs show their carry–total range. Toggle a club to include or remove it from this player's bag.", bagMutedStyle);
            GUILayout.Space(8);

            bagScroll = GUILayout.BeginScrollView(bagScroll, GUILayout.Height(Screen.height - 155f));
            int[] rows = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };
            for (int r = 0; r < rows.Length; r++)
            {
                int i = rows[r];
                GUILayout.BeginHorizontal(cardStyle, GUILayout.Height(58f));
                bool inBag = bagProfile.InBag[i];
                if (GUILayout.Button(inBag ? "✓" : "+", inBag ? bagActiveStyle : bagButtonStyle, GUILayout.Width(42))) bagProfile.InBag[i] = !inBag;
                GUILayout.Label(ShortClubName(bagProfile.ClubNames[i]), clubBigStyle, GUILayout.Width(90));
                GUILayout.Label(bagProfile.Lofts[i].ToString("F1") + "°", labelStyle, GUILayout.Width(55));
                string range = bagProfile.CarryMeters[i] > 0f ? bagProfile.CarryMeters[i].ToString("F0") + " – " + bagProfile.TotalMeters[i].ToString("F0") + " m" : "NOT MAPPED";
                GUILayout.Label(range, clubRangeStyle, GUILayout.Width(125));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(3);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
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
