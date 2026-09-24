using GolfSimZA.Courses;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfSimZA.UI
{
    public sealed class RoundSettingsUI : MonoBehaviour
    {
        private readonly string[] tees = { "Red", "White", "Blue", "Black" };
        private readonly string[] gameModes = { "Stroke Play", "Match Play" };
        private readonly string[] pins = { "Easy", "Standard", "Tournament" };
        private readonly string[] gimmies = { "Off", "3 m", "6 m" };
        private readonly string[] mulligans = { "Off", "1", "3" };

        private int selectedTee;
        private int selectedGameMode;
        private int selectedPins = 1;
        private int selectedGimmie = 2;
        private int selectedMulligan;
        private bool resumeRound;
        private int roundLength;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle labelStyle;
        private GUIStyle valueStyle;
        private GUIStyle buttonStyle;
        private GUIStyle tabStyle;
        private GUIStyle selectedTabStyle;
        private GUIStyle cardStyle;
        private bool stylesReady;

        private readonly Color navy = new Color(0.025f, 0.10f, 0.14f);
        private readonly Color panel = new Color(0.055f, 0.16f, 0.20f);
        private readonly Color panelLight = new Color(0.10f, 0.22f, 0.27f);
        private readonly Color accent = new Color(0.10f, 0.60f, 0.92f);

        private void Start()
        {
            roundLength = CourseSession.RoundLength;
            for (int i = 0; i < tees.Length; i++)
                if (tees[i] == CourseSession.TeeName) selectedTee = i;
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            subtitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = new Color(0.70f, 0.78f, 0.81f) } };
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.75f, 0.82f, 0.84f) } };
            valueStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold, fixedHeight = 44, margin = new RectOffset(4, 4, 4, 4) };
            tabStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold, fixedHeight = 42, normal = { textColor = new Color(0.78f, 0.84f, 0.86f) } };
            selectedTabStyle = new GUIStyle(tabStyle) { normal = { background = MakeTexture(1, 1, Color.white), textColor = navy } };
            cardStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(18, 18, 14, 14), normal = { background = MakeTexture(1, 1, panel) } };
            stylesReady = true;
        }

        private Texture2D MakeTexture(int width, int height, Color color)
        {
            Texture2D texture = new Texture2D(width, height);
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
                SceneManager.LoadScene("GolfSimZA_0_5_CourseSelection");
            GUILayout.FlexibleSpace();
            GUILayout.Label("GOLFSIM ZA", titleStyle, GUILayout.Width(190));
            GUILayout.FlexibleSpace();
            GUILayout.Label("PLAYERS    ⚙ SETTINGS", labelStyle, GUILayout.Width(210));
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label("ROUND SETTINGS", titleStyle, GUILayout.Width(270));
            GUILayout.FlexibleSpace();
            GUILayout.Label("01 COURSE", tabStyle, GUILayout.Width(130));
            GUILayout.Label("02 ROUND", selectedTabStyle, GUILayout.Width(130));
            GUILayout.Label("03 PLAYERS", tabStyle, GUILayout.Width(130));
            GUILayout.EndHorizontal();
            GUILayout.Space(12);

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(width * 0.23f));
            SideNav("ROUND SETUP", true);
            SideNav("COURSE SETUP", false);
            SideNav("SCORING & ASSISTANCE", false);
            GUILayout.Space(12);
            if (GUILayout.Button("COURSE RECOMMENDED SETTINGS", buttonStyle))
            {
                selectedGameMode = 0;
                selectedPins = 1;
                selectedGimmie = 2;
                selectedMulligan = 0;
                resumeRound = false;
            }
            GUILayout.EndVertical();

            GUILayout.Space(18);
            GUILayout.BeginVertical();
            GUILayout.Label("ROUND SETUP", titleStyle);
            GUILayout.Label($"{CourseSession.CourseName}  •  {roundLength} holes", subtitleStyle);
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            SettingCard("GAME MODE", gameModes, ref selectedGameMode);
            GUILayout.Space(12);
            SettingCard("TEE BOX", tees, ref selectedTee);
            GUILayout.EndHorizontal();
            GUILayout.Space(12);

            GUILayout.BeginHorizontal();
            SettingCard("PINS", pins, ref selectedPins);
            GUILayout.Space(12);
            SettingCard("GIMME / AUTO PUTT", gimmies, ref selectedGimmie);
            GUILayout.EndHorizontal();
            GUILayout.Space(12);

            GUILayout.BeginHorizontal();
            SettingCard("MULLIGANS", mulligans, ref selectedMulligan);
            GUILayout.Space(12);
            GUILayout.BeginVertical(cardStyle, GUILayout.Height(116));
            GUILayout.Label("RESUME ROUND", labelStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(resumeRound ? "ON" : "OFF", buttonStyle)) resumeRound = !resumeRound;
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("CONTINUE TO PLAYERS  →", buttonStyle, GUILayout.Width(280), GUILayout.Height(52)))
            {
                CourseSession.SetRoundSettings(gameModes[selectedGameMode], tees[selectedTee], pins[selectedPins], gimmies[selectedGimmie], mulligans[selectedMulligan], resumeRound, roundLength);
                SceneManager.LoadScene("GolfSimZA_0_5_Players");
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void SideNav(string text, bool active)
        {
            GUI.backgroundColor = active ? accent : panelLight;
            GUILayout.Button(text, active ? selectedTabStyle : tabStyle, GUILayout.Height(56));
            GUI.backgroundColor = Color.white;
        }

        private void SettingCard(string title, string[] values, ref int selected)
        {
            GUILayout.BeginVertical(cardStyle, GUILayout.Height(116));
            GUILayout.Label(title, labelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("‹", buttonStyle, GUILayout.Width(48))) selected = (selected - 1 + values.Length) % values.Length;
            GUILayout.Label(values[selected], valueStyle);
            if (GUILayout.Button("›", buttonStyle, GUILayout.Width(48))) selected = (selected + 1) % values.Length;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }
    }
}
