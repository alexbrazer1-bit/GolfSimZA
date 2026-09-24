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
        private GUIStyle bagHeaderStyle, bagFieldStyle, bagButtonStyle, bagActiveStyle, bagMutedStyle;
        private bool stylesReady;
        private readonly Color navy = new Color(0.025f, 0.10f, 0.14f);
        private readonly Color panel = new Color(0.055f, 0.16f, 0.20f);
        private readonly Color panelDark = new Color(0.025f, 0.075f, 0.10f);
        private readonly Color blue = new Color(0.03f, 0.48f, 0.82f);
        private readonly Color blueBright = new Color(0.08f, 0.64f, 1f);
        private readonly Color muted = new Color(0.70f, 0.78f, 0.81f);
        private bool editingBag;
        private int selectedPlayerIndex;
        private GolfBagProfile bagProfile;
        private Vector2 bagScroll;

        private void EnsureStyles()
        {
            if (stylesReady) return;
            titleStyle = MakeLabel(28, FontStyle.Bold, Color.white);
            subtitleStyle = MakeLabel(15, FontStyle.Normal, muted);
            labelStyle = MakeLabel(14, FontStyle.Bold, new Color(0.80f, 0.85f, 0.87f));
            fieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 16, fixedHeight = 42, padding = new RectOffset(12, 12, 8, 8) };
            buttonStyle = MakeButton(15, 44f, panel);
            tabStyle = MakeButton(14, 42f, panel);
            selectedTabStyle = MakeButton(14, 42f, Color.white, navy);
            cardStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(16, 16, 14, 14), normal = { background = MakeTexture(panel) } };
            bagHeaderStyle = MakeLabel(15, FontStyle.Bold, Color.white);
            bagFieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 13, fixedHeight = 34, padding = new RectOffset(8, 8, 6, 6) };
            bagButtonStyle = MakeButton(12, 34f, panel);
            bagActiveStyle = MakeButton(12, 34f, blue, Color.white);
            bagMutedStyle = MakeLabel(11, FontStyle.Normal, muted);
            stylesReady = true;
        }

        private GUIStyle MakeLabel(int size, FontStyle fontStyle, Color color)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = size;
            style.fontStyle = fontStyle;
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
            GUILayout.Label($"{CourseSession.CourseName}  •  {CourseSession.RoundLength} holes  •  {CourseSession.TeeName} tees", subtitleStyle);
            GUILayout.Space(18);

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("ADD PLAYERS", titleStyle);
            GUILayout.Label("Add up to four players. Each player has their own golf bag and distance map.", subtitleStyle);
            GUILayout.Space(10);

            for (int i = 0; i < players.Count; i++)
            {
                GUILayout.BeginHorizontal(cardStyle);
                GUILayout.Label((i + 1).ToString("00"), labelStyle, GUILayout.Width(42));
                players[i] = GUILayout.TextField(players[i], fieldStyle);
                if (GUILayout.Button("GOLF BAG", bagButtonStyle, GUILayout.Width(110))) { selectedPlayerIndex = i; OpenBagEditor(); }
                if (players.Count > 1 && GUILayout.Button("REMOVE", buttonStyle, GUILayout.Width(100))) { players.RemoveAt(i); i--; }
                GUILayout.EndHorizontal();
                GUILayout.Space(7);
            }

            if (players.Count < 4 && GUILayout.Button("＋  ADD PLAYER", buttonStyle, GUILayout.Width(190))) players.Add("Player " + (players.Count + 1));
            GUILayout.EndVertical();

            GUILayout.Space(22);
            GUILayout.BeginVertical(cardStyle, GUILayout.Width(width * 0.28f));
            GUILayout.Label("ROUND SUMMARY", titleStyle);
            GUILayout.Space(10);
            Summary("COURSE", CourseSession.CourseName);
            Summary("TEE", CourseSession.TeeName);
            Summary("ROUND", CourseSession.RoundLength + " holes");
            Summary("MODE", CourseSession.GameMode);
            Summary("PINS", CourseSession.PinSetting);
            Summary("AUTO PUTT", CourseSession.GimmieSetting);
            Summary("MULLIGANS", CourseSession.MulliganSetting);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Ready to play?", labelStyle);
            GUILayout.Space(8);
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
            if (selectedPlayerIndex < 0 || selectedPlayerIndex >= players.Count) selectedPlayerIndex = 0;
            string name = string.IsNullOrWhiteSpace(players[selectedPlayerIndex]) ? "Player " + (selectedPlayerIndex + 1) : players[selectedPlayerIndex].Trim();
            bagProfile = GolfBagProfile.Load(name);
            editingBag = true;
            bagScroll = Vector2.zero;
        }

        private void CloseBagEditor(bool save)
        {
            if (save && bagProfile != null)
            {
                string name = string.IsNullOrWhiteSpace(players[selectedPlayerIndex]) ? "Player " + (selectedPlayerIndex + 1) : players[selectedPlayerIndex].Trim();
                bagProfile.Save(name);
            }
            editingBag = false;
        }

        private void DrawBagEditor()
        {
            float margin = Mathf.Max(28f, Screen.width * 0.035f);
            float width = Screen.width - margin * 2f;
            string playerName = string.IsNullOrWhiteSpace(players[selectedPlayerIndex]) ? "Player " + (selectedPlayerIndex + 1) : players[selectedPlayerIndex].Trim();

            GUILayout.BeginArea(new Rect(margin, 20f, width, Screen.height - 40f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("‹  PLAYERS", buttonStyle, GUILayout.Width(130))) CloseBagEditor(true);
            GUILayout.FlexibleSpace();
            GUILayout.Label("GOLF BAG", titleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(playerName, labelStyle, GUILayout.Width(180));
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            GUILayout.BeginHorizontal(cardStyle);
            GUILayout.BeginVertical(GUILayout.Width(width * 0.68f));
            GUILayout.Label("PLAYER DISTANCE MAP", titleStyle);
            GUILayout.Label("TrackMan-style bag setup: map each club's typical carry and total distance. These values stay with this player.", subtitleStyle);
            GUILayout.Space(12);
            GUILayout.BeginHorizontal();
            GUILayout.Label("IN BAG", bagHeaderStyle, GUILayout.Width(70));
            GUILayout.Label("CLUB", bagHeaderStyle, GUILayout.Width(185));
            GUILayout.Label("LOFT", bagHeaderStyle, GUILayout.Width(80));
            GUILayout.Label("CARRY", bagHeaderStyle, GUILayout.Width(105));
            GUILayout.Label("TOTAL", bagHeaderStyle, GUILayout.Width(105));
            GUILayout.EndHorizontal();

            bagScroll = GUILayout.BeginScrollView(bagScroll, GUILayout.Height(Screen.height - 180f));
            for (int i = 0; i < GolfBagProfile.ClubCount; i++)
            {
                GUILayout.BeginHorizontal(cardStyle);
                bool inBag = bagProfile.InBag[i];
                if (GUILayout.Button(inBag ? "✓" : "", inBag ? bagActiveStyle : bagButtonStyle, GUILayout.Width(60))) bagProfile.InBag[i] = !inBag;
                bagProfile.ClubNames[i] = GUILayout.TextField(bagProfile.ClubNames[i], bagFieldStyle, GUILayout.Width(175));
                bagProfile.Lofts[i] = FloatField(bagProfile.Lofts[i], 80f);
                bagProfile.CarryMeters[i] = FloatField(bagProfile.CarryMeters[i], 105f);
                bagProfile.TotalMeters[i] = FloatField(bagProfile.TotalMeters[i], 105f);
                GUILayout.EndHorizontal();
                GUILayout.Space(4);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.Space(18);
            GUILayout.BeginVertical(cardStyle, GUILayout.Width(width * 0.27f));
            GUILayout.Label("DISTANCE SUMMARY", titleStyle);
            GUILayout.Space(10);
            Summary("PLAYER", playerName);
            Summary("CLUBS IN BAG", CountClubsInBag() + " / 14");
            Summary("LONGEST CARRY", LongestCarry().ToString("F0") + " m");
            Summary("LONGEST TOTAL", LongestTotal().ToString("F0") + " m");
            GUILayout.Space(8);
            GUILayout.Label("HOW IT WILL BE USED", bagHeaderStyle);
            GUILayout.Label("During a round, the club bar can use this player's mapped distances to show a realistic expected carry range before the shot.", bagMutedStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("SAVE GOLF BAG", bagActiveStyle, GUILayout.Height(48))) CloseBagEditor(true);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private float FloatField(float value, float width)
        {
            string text = GUILayout.TextField(value > 0f ? value.ToString("F0") : "", bagFieldStyle, GUILayout.Width(width));
            float parsed;
            return float.TryParse(text, out parsed) ? Mathf.Max(0f, parsed) : value;
        }

        private int CountClubsInBag()
        {
            int count = 0;
            for (int i = 0; i < GolfBagProfile.ClubCount; i++) if (bagProfile.InBag[i]) count++;
            return count;
        }

        private float LongestCarry()
        {
            float max = 0f;
            for (int i = 0; i < GolfBagProfile.ClubCount; i++) if (bagProfile.InBag[i]) max = Mathf.Max(max, bagProfile.CarryMeters[i]);
            return max;
        }

        private float LongestTotal()
        {
            float max = 0f;
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
