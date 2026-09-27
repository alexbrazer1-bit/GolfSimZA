using GolfSimZA.Players;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GolfSimZA.UI
{
    /// <summary>
    /// MENU button (top-left) and Esc menu on the play screen: resume, restart,
    /// players &amp; bags, back to courses, quit. Created automatically.
    /// </summary>
    public sealed class GameMenuOverlay : MonoBehaviour
    {
        public const string PlayScene = "GolfSimZA_0_6_PlayRound";
        private const string CourseScene = "GolfSimZA_0_5_CourseSelection";
        private const string PlayersScene = "GolfSimZA_0_5_Players";

        private bool open;
        private bool confirmQuit;
        private GUIStyle menuButton;

        public static bool IsOpen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<GameMenuOverlay>() != null) return;
            GameObject go = new GameObject("GolfSimZA_GameMenu");
            DontDestroyOnLoad(go);
            go.AddComponent<GameMenuOverlay>();
        }

        private static bool OnPlayScreen => SceneManager.GetSceneByName(PlayScene).isLoaded;

        private void Update()
        {
            if (!OnPlayScreen)
            {
                SetOpen(false);
                return;
            }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetOpen(!open);
        }

        private void SetOpen(bool value)
        {
            open = value;
            IsOpen = value;
            if (!value) confirmQuit = false;
        }

        private void OnGUI()
        {
            if (!OnPlayScreen) return;
            GolfSimTheme.Ensure();
            GUI.depth = -3000;
            if (menuButton == null)
                menuButton = new GUIStyle(GolfSimTheme.AccentButton) { fixedHeight = 34, fontSize = 13 };

            if (!open)
            {
                if (GUI.Button(new Rect(Mathf.Max(14f, Screen.width * 0.018f) + 6f, 12f, 120f, 34f), "☰  MENU", menuButton))
                    SetOpen(true);
                return;
            }

            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none, GolfSimTheme.Overlay);
            float w = 380f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height * 0.22f;
            GUI.Label(new Rect(x, y, w, 40f), "MENU", new GUIStyle(GolfSimTheme.Title) { alignment = TextAnchor.MiddleCenter });
            y += 56f;

            if (Item(ref y, x, w, "RESUME  (Esc)")) SetOpen(false);
            if (Item(ref y, x, w, "RESTART ROUND")) Load(PlayScene);
            if (Item(ref y, x, w, "PLAYERS & GOLF BAGS")) Load(PlayersScene);
            if (Item(ref y, x, w, "BACK TO COURSES")) Load(CourseScene);
            y += 12f;
            if (!confirmQuit)
            {
                if (Item(ref y, x, w, "QUIT GOLFSIMZA")) confirmQuit = true;
            }
            else
            {
                GUI.Label(new Rect(x, y, w, 24f), "Quit GolfSimZA?", new GUIStyle(GolfSimTheme.Warning_) { alignment = TextAnchor.MiddleCenter });
                y += 28f;
                if (GUI.Button(new Rect(x, y, w * 0.48f, 44f), "YES, QUIT", GolfSimTheme.Button))
                {
                    Application.Quit();
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#endif
                }
                if (GUI.Button(new Rect(x + w * 0.52f, y, w * 0.48f, 44f), "NO", GolfSimTheme.Button)) confirmQuit = false;
            }

            // Keep clicks from reaching the game screen underneath while the menu is open.
            if (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp)
                Event.current.Use();
        }

        private static bool Item(ref float y, float x, float w, string text)
        {
            bool clicked = GUI.Button(new Rect(x, y, w, 48f), text, GolfSimTheme.Button);
            y += 56f;
            return clicked;
        }

        private void Load(string scene)
        {
            SetOpen(false);
            // Leaving the play screen ends any Map My Bag session that was in progress.
            if (scene != PlayScene && ClubMappingSession.IsActive)
            {
                PlayerPrefs.SetInt("GolfSimZA.MapMode", 0);
                PlayerPrefs.Save();
            }
            SceneManager.LoadScene(scene);
        }
    }
}
