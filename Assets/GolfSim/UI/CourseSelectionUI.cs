using System.Collections.Generic;
using GolfSimZA.Core;
using GolfSimZA.Courses;
using GolfSimZA.Updates;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfSimZA.UI
{
    /// <summary>Main menu: course library (demo + imported courses), import, updates and settings.</summary>
    public sealed class CourseSelectionUI : MonoBehaviour
    {
        private sealed class CourseEntry
        {
            public string Id;           // empty for demo courses
            public string Name;
            public string Location;
            public string Info;
            public string[] Tees;
            public int Holes;
            public bool Available = true;
            public CourseDefinition Definition;
        }

        private readonly List<CourseEntry> entries = new List<CourseEntry>();
        private readonly Dictionary<string, Texture2D> previews = new Dictionary<string, Texture2D>();
        private readonly CourseImportPanel importPanel = new CourseImportPanel();
        private readonly UpdatePanel updatePanel = new UpdatePanel();

        private int selectedCourse;
        private int selectedTee;
        private int roundLength = 18;
        private string searchText = "";
        private Vector2 listScroll;
        private bool rebuildRequested = true;
        private GUIStyle courseNameStyle, selectedButtonStyle, tabStyle, selectedTabStyle, badgeStyle;
        private bool stylesReady;

        private void OnEnable() => CourseLibrary.Changed += RequestRebuild;
        private void OnDisable() => CourseLibrary.Changed -= RequestRebuild;
        private void RequestRebuild() => rebuildRequested = true;

        private void Start()
        {
            CourseLibrary.Reload();
            RebuildEntries();
            updatePanel.CheckInBackground();
        }

        private void RebuildEntries()
        {
            rebuildRequested = false;
            string previouslySelected = entries.Count > 0 && selectedCourse < entries.Count ? entries[selectedCourse].Name : CourseSession.CourseName;
            entries.Clear();

            entries.Add(new CourseEntry { Id = "", Name = "GolfSim ZA Practice Range", Location = "Practice", Info = "Driving range", Tees = CourseSession.DemoTees, Holes = 18 });
            entries.Add(new CourseEntry { Id = "", Name = "Karoo Valley Demo Course", Location = "South Africa", Info = "Demo holes • 18 holes", Tees = CourseSession.DemoTees, Holes = 18 });
            entries.Add(new CourseEntry { Id = "", Name = "Free State Hills Demo Course", Location = "South Africa", Info = "Demo holes • 18 holes", Tees = CourseSession.DemoTees, Holes = 18 });

            foreach (CourseDefinition course in CourseLibrary.All)
            {
                entries.Add(new CourseEntry
                {
                    Id = course.id,
                    Name = course.name,
                    Location = string.IsNullOrEmpty(course.location) ? "Imported course" : course.location,
                    Info = $"{course.HoleCount} holes • par {course.TotalPar}" + (string.IsNullOrEmpty(course.designer) ? "" : " • by " + course.designer),
                    Tees = course.tees != null && course.tees.Length > 0 ? course.tees : CourseSession.DemoTees,
                    Holes = course.HoleCount,
                    Available = CourseLibrary.IsAvailable(course),
                    Definition = course
                });
            }

            selectedCourse = 0;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Name == previouslySelected) selectedCourse = i;
            SelectCourse(selectedCourse);
        }

        private void SelectCourse(int index)
        {
            selectedCourse = Mathf.Clamp(index, 0, entries.Count - 1);
            CourseEntry entry = entries[selectedCourse];
            selectedTee = 0;
            for (int i = 0; i < entry.Tees.Length; i++)
                if (entry.Tees[i] == CourseSession.TeeName) selectedTee = i;
            if (entry.Holes < 18) roundLength = Mathf.Min(roundLength, entry.Holes);
            if (roundLength != 9 && roundLength != 18 && roundLength != entry.Holes) roundLength = entry.Holes >= 18 ? 18 : entry.Holes;
        }

        private Texture2D Preview(CourseEntry entry)
        {
            if (entry.Definition == null) return null;
            if (!previews.TryGetValue(entry.Id, out Texture2D texture))
            {
                texture = CourseLibrary.LoadSplash(entry.Definition);
                previews[entry.Id] = texture;
            }
            return texture;
        }

        private void EnsureStyles()
        {
            GolfSimTheme.Ensure();
            if (stylesReady) return;
            courseNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            selectedButtonStyle = new GUIStyle(GolfSimTheme.Button) { normal = { background = GolfSimTheme.Tex(GolfSimTheme.Accent), textColor = Color.white } };
            tabStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold, fixedHeight = 40 };
            selectedTabStyle = new GUIStyle(tabStyle) { normal = { background = GolfSimTheme.Tex(Color.white), textColor = GolfSimTheme.Navy } };
            badgeStyle = new GUIStyle(GolfSimTheme.Button) { normal = { background = GolfSimTheme.Tex(GolfSimTheme.Warning), textColor = GolfSimTheme.Navy } };
            stylesReady = true;
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (Event.current.type == EventType.Layout)
            {
                if (rebuildRequested) RebuildEntries();
                updatePanel.Poll();
            }

            GUI.backgroundColor = GolfSimTheme.Navy;
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);
            GUI.backgroundColor = Color.white;

            float margin = Mathf.Max(28f, Screen.width * 0.035f);
            float width = Screen.width - margin * 2f;
            Rect panelArea = new Rect(margin, 80f, width, Screen.height - 100f);

            GUILayout.BeginArea(new Rect(margin, 22f, width, 50f));
            DrawTopBar();
            GUILayout.EndArea();

            if (importPanel.IsOpen)
            {
                importPanel.Draw(panelArea);
                return;
            }
            if (updatePanel.IsOpen)
            {
                updatePanel.Draw(panelArea);
                return;
            }

            GUILayout.BeginArea(new Rect(margin, 80f, width, Screen.height - 100f));
            DrawCourseSelection(width);
            GUILayout.EndArea();
        }

        private void DrawTopBar()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("GOLFSIM ZA", GolfSimTheme.Title, GUILayout.Width(200));
            GUILayout.Label("v" + GolfSimVersion.Version, GolfSimTheme.Subtitle, GUILayout.Width(70));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("IMPORT COURSES", GolfSimTheme.Button, GUILayout.Width(180)))
            {
                updatePanel.Close();
                importPanel.Open();
            }
            string updateLabel = updatePanel.UpdateAvailable ? "UPDATE " + updatePanel.LatestVersion + " AVAILABLE" : "UPDATES & SETTINGS";
            if (GUILayout.Button(updateLabel, updatePanel.UpdateAvailable ? badgeStyle : GolfSimTheme.Button, GUILayout.Width(260)))
            {
                importPanel.Close();
                updatePanel.Open();
            }
            if (GUILayout.Button("QUIT", GolfSimTheme.Button, GUILayout.Width(90)))
            {
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }
            GUILayout.EndHorizontal();
        }

        private void DrawCourseSelection(float width)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("SELECT COURSE", GolfSimTheme.Title, GUILayout.Width(300));
            GUILayout.FlexibleSpace();
            GUILayout.Label("01 COURSE", selectedTabStyle, GUILayout.Width(130));
            GUILayout.Label("02 ROUND", tabStyle, GUILayout.Width(130));
            GUILayout.Label("03 PLAYERS", tabStyle, GUILayout.Width(130));
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();

            // ---- course list
            GUILayout.BeginVertical(GUILayout.Width(width * 0.66f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("COURSE LIBRARY", GolfSimTheme.Label, GUILayout.Width(150));
            GUILayout.Label("SEARCH", GolfSimTheme.Subtitle, GUILayout.Width(60));
            searchText = GUILayout.TextField(searchText, GolfSimTheme.TextField);
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            listScroll = GUILayout.BeginScrollView(listScroll);
            for (int i = 0; i < entries.Count; i++)
            {
                CourseEntry entry = entries[i];
                if (!string.IsNullOrEmpty(searchText) &&
                    entry.Name.ToLowerInvariant().IndexOf(searchText.ToLowerInvariant()) < 0 &&
                    entry.Location.ToLowerInvariant().IndexOf(searchText.ToLowerInvariant()) < 0) continue;

                bool selected = i == selectedCourse;
                GUILayout.BeginHorizontal(selected ? GolfSimTheme.SelectedCard : GolfSimTheme.Card, GUILayout.Height(80));
                Texture2D preview = Preview(entry);
                if (preview != null) GUILayout.Label(preview, GUILayout.Width(110), GUILayout.Height(62));
                GUILayout.BeginVertical();
                GUILayout.Label(entry.Name, courseNameStyle);
                GUILayout.Label(entry.Location + "   •   " + entry.Info, GolfSimTheme.Subtitle);
                if (!entry.Available) GUILayout.Label("Course file missing - import it again", GolfSimTheme.Warning_);
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(selected ? "SELECTED ✓" : "SELECT", selected ? selectedButtonStyle : GolfSimTheme.Button, GUILayout.Width(135)))
                    SelectCourse(i);
                GUILayout.EndHorizontal();
            }
            if (entries.Count <= 3)
                GUILayout.Label("Tip: use IMPORT COURSES at the top to add your own courses.", GolfSimTheme.Subtitle);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.Space(18);

            // ---- round preview
            CourseEntry current = entries[Mathf.Clamp(selectedCourse, 0, entries.Count - 1)];
            GUILayout.BeginVertical(GolfSimTheme.Card);
            GUILayout.Label("ROUND PREVIEW", GolfSimTheme.Title);
            GUILayout.Space(6);
            Texture2D big = Preview(current);
            if (big != null) GUILayout.Label(big, GUILayout.Height(140), GUILayout.ExpandWidth(true));
            GUILayout.Label(current.Name, courseNameStyle);
            GUILayout.Label(current.Location, GolfSimTheme.Subtitle);
            if (current.Definition != null && !string.IsNullOrEmpty(current.Definition.description))
                GUILayout.Label(Truncate(current.Definition.description, 220), GolfSimTheme.Body);
            GUILayout.Space(12);

            GUILayout.Label("TEE BOX", GolfSimTheme.Label);
            int perRow = 4;
            for (int row = 0; row * perRow < current.Tees.Length; row++)
            {
                GUILayout.BeginHorizontal();
                for (int i = row * perRow; i < Mathf.Min(current.Tees.Length, (row + 1) * perRow); i++)
                {
                    string label = current.Tees[i] + (i == selectedTee ? " ✓" : "");
                    if (GUILayout.Button(label, i == selectedTee ? selectedButtonStyle : GolfSimTheme.Button)) selectedTee = i;
                }
                GUILayout.EndHorizontal();
            }
            if (current.Definition != null)
                GUILayout.Label($"Length from {current.Tees[Mathf.Clamp(selectedTee, 0, current.Tees.Length - 1)]}: {current.Definition.TotalLengthMeters(current.Tees[Mathf.Clamp(selectedTee, 0, current.Tees.Length - 1)]):0} m", GolfSimTheme.Subtitle);

            GUILayout.Space(10);
            GUILayout.Label("ROUND", GolfSimTheme.Label);
            GUILayout.BeginHorizontal();
            if (current.Holes >= 9 && GUILayout.Button("9 Holes" + (roundLength == 9 ? " ✓" : ""), roundLength == 9 ? selectedButtonStyle : GolfSimTheme.Button)) roundLength = 9;
            if (current.Holes >= 18 && GUILayout.Button("18 Holes" + (roundLength == 18 ? " ✓" : ""), roundLength == 18 ? selectedButtonStyle : GolfSimTheme.Button)) roundLength = 18;
            if (current.Holes < 9 || (current.Holes > 9 && current.Holes < 18))
                if (GUILayout.Button(current.Holes + " Holes" + (roundLength == current.Holes ? " ✓" : ""), roundLength == current.Holes ? selectedButtonStyle : GolfSimTheme.Button)) roundLength = current.Holes;
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            GUI.enabled = current.Available;
            if (GUILayout.Button("CONTINUE TO ROUND  →", selectedButtonStyle, GUILayout.Height(54)))
            {
                string tee = current.Tees[Mathf.Clamp(selectedTee, 0, current.Tees.Length - 1)];
                int holes = Mathf.Clamp(roundLength, 1, Mathf.Max(1, current.Holes));
                CourseSession.SetSession(current.Name, tee, holes);
                CourseSession.SetCourse(current.Id, current.Definition != null ? current.Tees : null);
                SceneManager.LoadScene("GolfSimZA_0_5_RoundSettings");
            }
            GUI.enabled = true;
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) return text;
            return text.Substring(0, max).TrimEnd() + "…";
        }

        private void OnDestroy()
        {
            foreach (Texture2D texture in previews.Values)
                if (texture != null) Destroy(texture);
            previews.Clear();
        }
    }
}
