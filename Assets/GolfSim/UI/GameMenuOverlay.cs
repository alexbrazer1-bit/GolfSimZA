using GolfSimZA.Core;
using GolfSimZA.Courses;
using GolfSimZA.Players;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GolfSimZA.UI
{
    /// <summary>
    /// MENU button (top-left of the play screen) and the in-game menu: data tiles, settings,
    /// lighting, mulligan, flyover, putt grid, putt mode, flag, shortcuts, scorecard,
    /// players &amp; bags and end round. Esc opens and closes it.
    /// </summary>
    public sealed class GameMenuOverlay : MonoBehaviour
    {
        public const string PlayScene = "GolfSimZA_0_6_PlayRound";
        private const string HomeScene = "GolfSimZA_0_5_CourseSelection";
        private const string PlayersScene = "GolfSimZA_0_5_Players";

        private enum Page { Grid, Settings, Shortcuts, ConfirmEnd, ConfirmQuit }

        private bool open;
        private Page page;
        private GUIStyle tile, tileOn, tileOff, menuButton;
        private string altitudeField;

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
        private static bool OnRange => ModernGolfSimUI.IsRangeSession();

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
            page = Page.Grid;
            if (value) ClubBar.Close();
        }

        private void Ensure()
        {
            GolfSimTheme.Ensure();
            if (tile != null) return;
            tile = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true, border = new RectOffset(8, 8, 8, 8),
                normal = { background = GolfSimTheme.Rounded(new Color(0.10f, 0.17f, 0.21f, 1f), 8), textColor = Color.white },
                hover = { background = GolfSimTheme.Rounded(new Color(0.14f, 0.26f, 0.30f, 1f), 8, GolfSimTheme.Accent), textColor = Color.white },
                active = { background = GolfSimTheme.Rounded(GolfSimTheme.Accent, 8), textColor = Color.white }
            };
            tileOn = new GUIStyle(tile) { normal = { background = GolfSimTheme.Rounded(new Color(0.08f, 0.42f, 0.29f, 1f), 8, GolfSimTheme.Accent), textColor = Color.white } };
            tileOff = new GUIStyle(tile) { normal = { background = GolfSimTheme.Rounded(new Color(0.08f, 0.12f, 0.14f, 1f), 8), textColor = new Color(1f, 1f, 1f, 0.35f) }, hover = { background = GolfSimTheme.Rounded(new Color(0.08f, 0.12f, 0.14f, 1f), 8), textColor = new Color(1f, 1f, 1f, 0.35f) } };
            menuButton = new GUIStyle(GolfSimTheme.AccentButton) { fixedHeight = 38, fontSize = 14 };
        }

        private void OnGUI()
        {
            if (!OnPlayScreen) return;
            Ensure();
            GUI.depth = -3000;
            float margin = Mathf.Clamp(Screen.width * 0.015f, 10f, 24f);

            if (!open)
            {
                if (GUI.Button(new Rect(margin, margin, 120f, 38f), "≡  MENU", menuButton)) SetOpen(true);
                return;
            }

            if (dimTexture == null) dimTexture = GolfSimTheme.Tex(new Color(0f, 0f, 0f, 0.45f));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), dimTexture);
            float w = 420f;
            bool confirm = page == Page.ConfirmEnd || page == Page.ConfirmQuit;
            float h = Mathf.Min(Screen.height - 40f, confirm ? 280f : 640f);
            Rect panel = new Rect(Screen.width - w - margin - 250f, (Screen.height - h) * 0.5f, w, h);
            panel.x = Mathf.Max(margin, panel.x);
            GUI.Box(panel, GUIContent.none, GolfSimTheme.Overlay);

            GUI.Label(new Rect(panel.x, panel.y + 14f, panel.width, 34f), PageTitle(), new GUIStyle(GolfSimTheme.Title) { alignment = TextAnchor.MiddleCenter, fontSize = 26 });
            if (GUI.Button(new Rect(panel.xMax - 54f, panel.y + 14f, 40f, 36f), "X", GolfSimTheme.SmallButton)) SetOpen(false);

            Rect body = new Rect(panel.x + 18f, panel.y + 64f, panel.width - 36f, panel.height - 82f);
            switch (page)
            {
                case Page.Grid: DrawGrid(body); break;
                case Page.Settings: DrawSettings(body); break;
                case Page.Shortcuts: DrawShortcuts(body); break;
                case Page.ConfirmEnd: DrawConfirm(body, OnRange ? "Leave the driving range?" : (CourseSession.PracticeMode ? "End practice and go home?" : "End this round? Finished holes are saved - turn on RESUME ROUND in Round Settings to carry on later."), () => Load(HomeScene)); break;
                case Page.ConfirmQuit: DrawConfirm(body, "Quit GolfSim ZA?", Quit); break;
            }

            // Keep clicks from reaching the game screen underneath while the menu is open.
            if (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp)
                Event.current.Use();
        }

        private string PageTitle()
        {
            switch (page)
            {
                case Page.Settings: return "SETTINGS";
                case Page.Shortcuts: return "SHORTCUTS";
                case Page.ConfirmEnd: return OnRange ? "LEAVE RANGE" : "END ROUND";
                case Page.ConfirmQuit: return "QUIT";
                default: return "GAME MENU";
            }
        }

        private void DrawGrid(Rect body)
        {
            bool round = !OnRange;
            float gap = 8f;
            float cw = (body.width - gap) * 0.5f;
            float ch = Mathf.Min(62f, (body.height - 110f - gap * 5f) / 6f);
            int i = 0;

            if (Cell(body, ref i, cw, ch, gap, "DATA TILES", GameOptions.ShowDataTiles ? tileOn : tile)) GameOptions.ShowDataTiles = !GameOptions.ShowDataTiles;
            if (Cell(body, ref i, cw, ch, gap, "SETTINGS", tile)) { page = Page.Settings; altitudeField = AppSettings.Current.homeAltitudeMeters.ToString("0"); }
            if (Cell(body, ref i, cw, ch, gap, "LIGHTING\n" + GameOptions.Lighting.ToString().ToUpperInvariant(), tile)) GameOptions.CycleLighting();
            if (Cell(body, ref i, cw, ch, gap, "MULLIGAN\n(take shot back)", round ? tile : tileOff) && round) { GameOptions.RequestMulligan(); SetOpen(false); }
            if (Cell(body, ref i, cw, ch, gap, "FLYOVER", round ? tile : tileOff) && round) { SetOpen(false); GameOptions.RequestFlyover(); }
            if (Cell(body, ref i, cw, ch, gap, "PUTT GRID", !round ? tileOff : GameOptions.PuttGrid ? tileOn : tile) && round) GameOptions.TogglePuttGrid();
            if (Cell(body, ref i, cw, ch, gap, "PUTT MODE\n(select putter)", tile)) { ActiveClub.Select("Putter"); SetOpen(false); }
            if (Cell(body, ref i, cw, ch, gap, "SHOW FLAG", GameOptions.ShowFlag ? tileOn : tile)) GameOptions.ToggleFlag();
            if (Cell(body, ref i, cw, ch, gap, "SHORTCUTS", tile)) page = Page.Shortcuts;
            if (Cell(body, ref i, cw, ch, gap, "SCORECARD", round ? tile : tileOff) && round) { GameOptions.RequestScorecard(); SetOpen(false); }
            if (Cell(body, ref i, cw, ch, gap, "PLAYERS & BAGS", tile)) Load(PlayersScene);
            if (Cell(body, ref i, cw, ch, gap, "RESUME", tile)) SetOpen(false);

            float y = body.y + Mathf.Ceil(i / 2f) * (ch + gap) + 6f;
            if (GUI.Button(new Rect(body.x, y, body.width, 44f), OnRange ? "LEAVE RANGE" : "END ROUND", GolfSimTheme.AccentButton)) page = Page.ConfirmEnd;
            if (GUI.Button(new Rect(body.x, y + 50f, body.width, 40f), "QUIT GOLFSIM ZA", GolfSimTheme.Button)) page = Page.ConfirmQuit;
        }

        private static bool Cell(Rect body, ref int index, float w, float h, float gap, string text, GUIStyle style)
        {
            int row = index / 2, col = index % 2;
            index++;
            return GUI.Button(new Rect(body.x + col * (w + gap), body.y + row * (h + gap), w, h), text, style);
        }

        private void DrawSettings(Rect body)
        {
            float y = body.y;
            GUI.Label(new Rect(body.x, y, body.width, 20f), "UNITS", GolfSimTheme.Label);
            y += 24f;
            bool metric = AppSettings.Current.metricUnits;
            if (GUI.Button(new Rect(body.x, y, body.width * 0.49f, 42f), "METRIC (km/h, m)", metric ? tileOn : tile)) SetUnits(true);
            if (GUI.Button(new Rect(body.x + body.width * 0.51f, y, body.width * 0.49f, 42f), "IMPERIAL (mph, yd)", !metric ? tileOn : tile)) SetUnits(false);
            y += 58f;

            GUI.Label(new Rect(body.x, y, body.width, 20f), "HOME / RANGE ALTITUDE (metres)", GolfSimTheme.Label);
            y += 24f;
            altitudeField = GUI.TextField(new Rect(body.x, y, body.width * 0.5f, 36f), altitudeField ?? "0", GolfSimTheme.TextField);
            if (GUI.Button(new Rect(body.x + body.width * 0.54f, y, body.width * 0.46f, 36f), "SAVE", GolfSimTheme.AccentButton)
                && float.TryParse(altitudeField, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float m))
            {
                AppSettings.Current.homeAltitudeMeters = Mathf.Clamp(m, -400f, 5000f);
                AppSettings.Current.Save();
            }
            y += 50f;
            GUI.Label(new Rect(body.x, y, body.width, 60f), "Courses use their own altitude. The range uses this altitude (Johannesburg is about 1 750 m).", GolfSimTheme.Subtitle);

            if (GUI.Button(new Rect(body.x, body.yMax - 44f, body.width, 44f), "BACK", GolfSimTheme.Button)) page = Page.Grid;
        }

        private static void SetUnits(bool metric)
        {
            AppSettings.Current.metricUnits = metric;
            AppSettings.Current.Save();
        }

        private void DrawShortcuts(Rect body)
        {
            string[] lines =
            {
                "Esc  —  open / close this menu",
                "C  —  open the club list",
                "1 – 9, 0  —  first ten clubs in the bag",
                "Q / E  —  previous / next club",
                "← / →  —  aim left / right (Shift = 5°)",
                "↑  —  aim back at the target",
                "SPACE  —  keyboard test shot",
                "R  —  (range) put the ball back on the tee"
            };
            float y = body.y;
            foreach (string line in lines)
            {
                GUI.Label(new Rect(body.x, y, body.width, 26f), line, GolfSimTheme.Body);
                y += 32f;
            }
            if (GUI.Button(new Rect(body.x, body.yMax - 44f, body.width, 44f), "BACK", GolfSimTheme.Button)) page = Page.Grid;
        }

        private static Texture2D dimTexture;

        private void DrawConfirm(Rect body, string question, System.Action yes)
        {
            GUI.Label(new Rect(body.x, body.y + 4f, body.width, 90f), question, new GUIStyle(GolfSimTheme.Heading) { alignment = TextAnchor.MiddleCenter, wordWrap = true });
            if (GUI.Button(new Rect(body.x, body.y + 100f, body.width * 0.48f, 48f), "YES", GolfSimTheme.AccentButton)) yes();
            if (GUI.Button(new Rect(body.x + body.width * 0.52f, body.y + 100f, body.width * 0.48f, 48f), "NO", GolfSimTheme.Button)) page = Page.Grid;
        }

        private static void Quit()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
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
            if (scene == PlayersScene) PlayerPrefs.SetInt("GolfSimZA.BagReturnHome", 1);
            SceneManager.LoadScene(scene);
        }
    }
}
