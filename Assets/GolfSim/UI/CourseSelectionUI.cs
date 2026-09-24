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

        private readonly string[] tees = { "Red", "White", "Blue", "Black" };
        private int selectedCourse;
        private int selectedTee = 2;
        private int roundLength = 18;
        private GUIStyle titleStyle;
        private GUIStyle sectionStyle;
        private GUIStyle bodyStyle;
        private GUIStyle buttonStyle;
        private GUIStyle smallStyle;
        private bool stylesReady;

        private void EnsureStyles()
        {
            if (stylesReady) return;

            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
            sectionStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fixedHeight = 42 };
            stylesReady = true;
        }

        private void OnGUI()
        {
            EnsureStyles();

            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);
            GUILayout.BeginArea(new Rect(40, 30, Mathf.Min(620, Screen.width - 80), Screen.height - 60));

            GUILayout.Label("GOLFSIM ZA", titleStyle);
            GUILayout.Label("0.5 • COURSE & ROUND SELECT", sectionStyle);
            GUILayout.Space(12);

            GUILayout.Label("SELECT COURSE", sectionStyle);
            for (int i = 0; i < courses.Length; i++)
            {
                string prefix = i == selectedCourse ? "✓  " : "    ";
                if (GUILayout.Button(prefix + courses[i], buttonStyle))
                    selectedCourse = i;
            }

            GUILayout.Space(12);
            GUILayout.Label("TEE", sectionStyle);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < tees.Length; i++)
            {
                if (GUILayout.Button(tees[i] + (i == selectedTee ? " ✓" : ""), buttonStyle))
                    selectedTee = i;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(12);
            GUILayout.Label("ROUND", sectionStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("9 Holes" + (roundLength == 9 ? " ✓" : ""), buttonStyle)) roundLength = 9;
            if (GUILayout.Button("18 Holes" + (roundLength == 18 ? " ✓" : ""), buttonStyle)) roundLength = 18;
            GUILayout.EndHorizontal();

            GUILayout.Space(18);
            GUILayout.Label($"{courses[selectedCourse]}  •  {tees[selectedTee]} tees  •  {roundLength} holes", bodyStyle);
            GUILayout.Label("Demo course names are placeholders until licensed/original course content is added.", smallStyle);
            GUILayout.Space(12);

            if (GUILayout.Button("START PRACTICE / ROUND", buttonStyle))
            {
                CourseSession.SetSession(courses[selectedCourse], tees[selectedTee], roundLength);
                SceneManager.LoadScene("GolfSimZA_0_4_SimulatorPresentationRange");
            }

            GUILayout.Space(8);
            if (GUILayout.Button("QUIT", buttonStyle))
                Application.Quit();

            GUILayout.EndArea();
        }
    }
}
