using GolfSimZA.Courses;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfSimZA.UI
{
    public sealed class CourseSelectionUI : MonoBehaviour
    {
        private readonly string[] courses =
        {
            "GolfSim ZA Practice Range",
            "Karoo Valley Demo Course",
            "Free State Hills Demo Course"
        };

        private readonly string[] locations = { "Practice", "South Africa", "South Africa" };
        private readonly string[] courseInfo = { "Training • Par 3", "Parkland • 18 holes", "Heathland • 18 holes" };
        private int selectedCourse;
        private int selectedTee = 2;
        private int roundLength = 18;
        private string searchText = "";
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private GUIStyle selectedButtonStyle;
        private GUIStyle tabStyle;
        private GUIStyle selectedTabStyle;
        private GUIStyle cardStyle;
        private GUIStyle courseNameStyle;
        private bool stylesReady;
        private readonly Color navy = new Color(0.025f, 0.10f, 0.14f);
        private readonly Color panel = new Color(0.055f, 0.16f, 0.20f);
        private readonly Color panelLight = new Color(0.10f, 0.22f, 0.27f);
        private readonly Color accent = new Color(0.10f, 0.60f, 0.92f);

        private void EnsureStyles()
        {
            if (stylesReady) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            subtitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = new Color(0.68f, 0.77f, 0.80f) } };
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.80f, 0.86f, 0.88f) } };
            courseNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold, fixedHeight = 44, margin = new RectOffset(4, 4, 4, 4) };
            selectedButtonStyle = new GUIStyle(buttonStyle) { normal = { background = MakeTexture(accent), textColor = Color.white } };
            tabStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold, fixedHeight = 40 };
            selectedTabStyle = new GUIStyle(tabStyle) { normal = { background = MakeTexture(Color.white), textColor = navy } };
            cardStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(18, 18, 14, 14), normal = { background = MakeTexture(panel) } };
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
            GUILayout.Label("‹  BACK", buttonStyle, GUILayout.Width(110));
            GUILayout.FlexibleSpace();
            GUILayout.Label("GOLFSIM ZA", titleStyle, GUILayout.Width(190));
            GUILayout.FlexibleSpace();
            GUILayout.Label("PLAYERS    ⚙ SETTINGS", labelStyle, GUILayout.Width(210));
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label("SELECT COURSE", titleStyle, GUILayout.Width(300));
            GUILayout.FlexibleSpace();
            GUILayout.Label("01 COURSE", selectedTabStyle, GUILayout.Width(130));
            GUILayout.Label("02 ROUND", tabStyle, GUILayout.Width(130));
            GUILayout.Label("03 PLAYERS", tabStyle, GUILayout.Width(130));
            GUILayout.EndHorizontal();
            GUILayout.Space(14);

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(width * 0.72f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("COURSE LIBRARY", labelStyle, GUILayout.Width(150));
            searchText = GUILayout.TextField(searchText, "Search courses...", buttonStyle);
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            for (int i = 0; i < courses.Length; i++)
            {
                if (!string.IsNullOrEmpty(searchText) && courses[i].ToLowerInvariant().IndexOf(searchText.ToLowerInvariant()) < 0) continue;
                bool selected = i == selectedCourse;
                GUILayout.BeginHorizontal(selected ? selectedTabStyle : cardStyle, GUILayout.Height(76));
                GUILayout.BeginVertical();
                GUILayout.Label(courses[i], courseNameStyle);
                GUILayout.Label(locations[i] + "   •   " + courseInfo[i], subtitleStyle);
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(selected ? "SELECTED ✓" : "SELECT", selected ? selectedButtonStyle : buttonStyle, GUILayout.Width(135))) selectedCourse = i;
                GUILayout.EndHorizontal();
                GUILayout.Space(7);
            }
            GUILayout.EndVertical();

            GUILayout.Space(18);
            GUILayout.BeginVertical(cardStyle, GUILayout.Width(width * 0.28f));
            GUILayout.Label("ROUND PREVIEW", titleStyle);
            GUILayout.Space(8);
            GUILayout.Label(courses[selectedCourse], courseNameStyle);
            GUILayout.Label(locations[selectedCourse], subtitleStyle);
            GUILayout.Space(16);
            GUILayout.Label("TEE BOX", labelStyle);
            GUILayout.BeginHorizontal();
            string[] tees = { "Red", "White", "Blue", "Black" };
            for (int i = 0; i < tees.Length; i++)
                if (GUILayout.Button(tees[i] + (i == selectedTee ? " ✓" : ""), i == selectedTee ? selectedButtonStyle : buttonStyle)) selectedTee = i;
            GUILayout.EndHorizontal();
            GUILayout.Space(12);
            GUILayout.Label("ROUND", labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("9 Holes" + (roundLength == 9 ? " ✓" : ""), roundLength == 9 ? selectedButtonStyle : buttonStyle)) roundLength = 9;
            if (GUILayout.Button("18 Holes" + (roundLength == 18 ? " ✓" : ""), roundLength == 18 ? selectedButtonStyle : buttonStyle)) roundLength = 18;
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Your course selection is saved for the round setup.", subtitleStyle);
            GUILayout.Space(8);
            if (GUILayout.Button("CONTINUE TO ROUND  →", selectedButtonStyle, GUILayout.Height(54)))
            {
                CourseSession.SetSession(courses[selectedCourse], tees[selectedTee], roundLength);
                SceneManager.LoadScene("GolfSimZA_0_5_RoundSettings");
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
