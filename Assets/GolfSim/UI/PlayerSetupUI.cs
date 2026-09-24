using System.Collections.Generic;
using GolfSimZA.Courses;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfSimZA.UI
{
    public sealed class PlayerSetupUI : MonoBehaviour
    {
        private readonly List<string> players = new List<string> { "Player 1" };
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle labelStyle;
        private GUIStyle fieldStyle;
        private GUIStyle buttonStyle;
        private GUIStyle tabStyle;
        private GUIStyle selectedTabStyle;
        private GUIStyle cardStyle;
        private bool stylesReady;
        private readonly Color navy = new Color(0.025f, 0.10f, 0.14f);
        private readonly Color panel = new Color(0.055f, 0.16f, 0.20f);

        private void EnsureStyles()
        {
            if (stylesReady) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            subtitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = new Color(0.70f, 0.78f, 0.81f) } };
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.80f, 0.85f, 0.87f) } };
            fieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 16, fixedHeight = 42, padding = new RectOffset(12, 12, 8, 8) };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold, fixedHeight = 44, margin = new RectOffset(4, 4, 4, 4) };
            tabStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold, fixedHeight = 42 };
            selectedTabStyle = new GUIStyle(tabStyle) { normal = { background = MakeTexture(Color.white), textColor = navy } };
            cardStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(16, 16, 14, 14), normal = { background = MakeTexture(panel) } };
            stylesReady = true;
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

            float margin = Mathf.Max(28f, Screen.width * 0.035f);
            float width = Screen.width - margin * 2f;
            GUILayout.BeginArea(new Rect(margin, 22f, width, Screen.height - 44f));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("‹  BACK", buttonStyle, GUILayout.Width(110)))
                SceneManager.LoadScene("GolfSimZA_0_5_RoundSettings");
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
            GUILayout.Label("Add up to four players for this round.", subtitleStyle);
            GUILayout.Space(10);

            for (int i = 0; i < players.Count; i++)
            {
                GUILayout.BeginHorizontal(cardStyle);
                GUILayout.Label((i + 1).ToString("00"), labelStyle, GUILayout.Width(42));
                players[i] = GUILayout.TextField(players[i], fieldStyle);
                if (players.Count > 1 && GUILayout.Button("REMOVE", buttonStyle, GUILayout.Width(100)))
                {
                    players.RemoveAt(i);
                    i--;
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(7);
            }

            if (players.Count < 4 && GUILayout.Button("＋  ADD PLAYER", buttonStyle, GUILayout.Width(190)))
                players.Add("Player " + (players.Count + 1));

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

        private void Summary(string key, string value)
        {
            GUILayout.Label(key, labelStyle);
            GUILayout.Label(value, subtitleStyle);
            GUILayout.Space(6);
        }
    }
}
