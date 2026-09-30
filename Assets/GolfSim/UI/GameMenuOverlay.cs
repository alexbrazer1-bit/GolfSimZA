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

        private enum Page { Grid, Settings, Shortcuts, AutoPutt, AddPlayer, ConfirmEnd, ConfirmQuit }

        private bool open;
        private Page page;
        private GUIStyle tile, tileOn, tileOff, menuButton;
        private SettingsScreen settingsScreen;
        private static Texture2D settingsBackdrop;

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
        private static bool InMiniGame => GolfSimZA.MiniGames.MiniGameSession.Active;

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

            if (page == Page.Settings)
            {
                // Full-screen settings (same screen as the home SETTINGS).
                if (settingsScreen == null) settingsScreen = new SettingsScreen(() => page = Page.Grid, null, null, null);
                if (settingsBackdrop == null) settingsBackdrop = GolfSimTheme.Tex(new Color(0.02f, 0.05f, 0.06f, 0.97f));
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), settingsBackdrop);
                float m = Mathf.Max(24f, Screen.width * 0.03f);
                settingsScreen.Draw(new Rect(m, 24f, Screen.width - m * 2f, Screen.height - 48f));
                if (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp) Event.current.Use();
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
                case Page.Shortcuts: DrawShortcuts(body); break;
                case Page.AutoPutt: DrawAutoPutt(body); break;
                case Page.AddPlayer: DrawAddPlayer(body); break;
                case Page.ConfirmEnd: if (InMiniGame) { DrawConfirm(body, "Leave this mini game? The scores are not kept.", () => { SetOpen(false); CourseSelectionUI.OpenHome("MiniGames"); }); break; }
                    DrawConfirm(body, OnRange ? "Leave the driving range?" : (CourseSession.PracticeMode ? "End practice and go home?" : "End this round? Finished holes are saved - turn on RESUME ROUND in Round Settings to carry on later."), () => Load(HomeScene)); break;
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
                case Page.AutoPutt: return "AUTO PUTT";
                case Page.AddPlayer: return "ADD PLAYER";
                case Page.ConfirmEnd: return InMiniGame ? "LEAVE GAME" : OnRange ? "LEAVE RANGE" : "END ROUND";
                case Page.ConfirmQuit: return "QUIT";
                default: return "GAME MENU";
            }
        }

        private void DrawGrid(Rect body)
        {
            bool round = !OnRange;
            float gap = 8f;
            float cw = (body.width - gap) * 0.5f;
            float ch = Mathf.Min(62f, (body.height - 110f - gap * 7f) / 8f);
            int i = 0;

            if (Cell(body, ref i, cw, ch, gap, "DATA TILES", GameOptions.ShowDataTiles ? tileOn : tile)) GameOptions.ShowDataTiles = !GameOptions.ShowDataTiles;
            if (Cell(body, ref i, cw, ch, gap, "SETTINGS", tile)) OpenSettings(false);
            if (Cell(body, ref i, cw, ch, gap, "LIGHTING\n" + GameOptions.Lighting.ToString().ToUpperInvariant(), tile)) GameOptions.CycleLighting();
            if (Cell(body, ref i, cw, ch, gap, "MULLIGAN\n(take shot back)", round ? tile : tileOff) && round) { GameOptions.RequestMulligan(); SetOpen(false); }
            if (Cell(body, ref i, cw, ch, gap, "FLYOVER", round ? tile : tileOff) && round) { SetOpen(false); GameOptions.RequestFlyover(); }
            if (Cell(body, ref i, cw, ch, gap, "PUTT GRID", !round ? tileOff : GameOptions.PuttGrid ? tileOn : tile) && round) GameOptions.TogglePuttGrid();
            if (Cell(body, ref i, cw, ch, gap, "PUTT MODE\n(select putter)", tile)) { ActiveClub.Select("Putter"); SetOpen(false); }
            if (Cell(body, ref i, cw, ch, gap, "SHOW FLAG", GameOptions.ShowFlag ? tileOn : tile)) GameOptions.ToggleFlag();
            if (Cell(body, ref i, cw, ch, gap, "SHORTCUTS", tile)) page = Page.Shortcuts;
            if (Cell(body, ref i, cw, ch, gap, "SCORECARD", round ? tile : tileOff) && round) { GameOptions.RequestScorecard(); SetOpen(false); }
            // During a round the players page opens in place (the round keeps going);
            // on the range the golf bag editor can be opened.
            if (round) { if (Cell(body, ref i, cw, ch, gap, "PLAYERS", tile)) OpenSettings(true); }
            else if (Cell(body, ref i, cw, ch, gap, "PLAYERS & BAGS", InMiniGame ? tileOff : tile) && !InMiniGame) Load(PlayersScene);
            AppSettings st = AppSettings.Current;
            if (Cell(body, ref i, cw, ch, gap, "AUTO PUTT\n" + (st.autoPutt ? "ON  •  " + Units.Distance(st.autoPuttOneMeters).ToString("0.#") + " / " + Units.Distance(st.autoPuttTwoMeters).ToString("0.#") + " " + Units.DistanceUnit : "OFF"), round ? (st.autoPutt ? tileOn : tile) : tileOff) && round) page = Page.AutoPutt;
            RoundGameplayUI roundUi = RoundGameplayUI.Current;
            bool canAdd = round && roundUi != null && roundUi.CanAddPlayers;
            if (Cell(body, ref i, cw, ch, gap, "ADD PLAYER\n(join this round)", canAdd ? tile : tileOff) && canAdd) { OpenAddPlayer(); }
            if (Cell(body, ref i, cw, ch, gap, "RESUME", tile)) SetOpen(false);

            float y = body.y + Mathf.Ceil(i / 2f) * (ch + gap) + 6f;
            if (GUI.Button(new Rect(body.x, y, body.width, 44f), InMiniGame ? "LEAVE GAME" : OnRange ? "LEAVE RANGE" : "END ROUND", GolfSimTheme.AccentButton)) page = Page.ConfirmEnd;
            if (GUI.Button(new Rect(body.x, y + 50f, body.width, 40f), "QUIT GOLFSIM ZA", GolfSimTheme.Button)) page = Page.ConfirmQuit;
        }

        private static bool Cell(Rect body, ref int index, float w, float h, float gap, string text, GUIStyle style)
        {
            int row = index / 2, col = index % 2;
            index++;
            return GUI.Button(new Rect(body.x + col * (w + gap), body.y + row * (h + gap), w, h), text, style);
        }

        private void OpenSettings(bool players)
        {
            if (settingsScreen == null) settingsScreen = new SettingsScreen(() => page = Page.Grid, null, null, null);
            settingsScreen.Open(players);
            page = Page.Settings;
        }

        private void DrawShortcuts(Rect body)
        {
            string[] lines =
            {
                "Esc  —  open / close this menu",
                "C  —  open the club list",
                "1 – 9, 0  —  first ten clubs in the bag",
                "Q / E  —  previous / next club",
                "Drag the gold aim target (or right-click the ground / click the hole map)",
                "← / →  —  turn the aim (Shift = 5°)",
                "↑ / ↓  —  aim further / closer (Shift = 10 m)",
                "Home  —  aim back on the target line",
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

        /// <summary>In-round AUTO PUTT: on / off and the 1-putt / 2-putt circle sizes (changes show on the green at once).</summary>
        private void DrawAutoPutt(Rect body)
        {
            AppSettings s = AppSettings.Current;
            bool changed = false;
            float y = body.y;
            if (GUI.Button(new Rect(body.x, y, body.width, 44f), s.autoPutt ? "AUTO PUTT  •  ON" : "AUTO PUTT  •  OFF", s.autoPutt ? GolfSimTheme.AccentButton : GolfSimTheme.Button)) { s.autoPutt = !s.autoPutt; changed = true; }
            y += 56f;
            GUI.enabled = s.autoPutt;
            GUI.Label(new Rect(body.x, y, body.width, 20f), "1 PUTT CIRCLE (gold)", new GUIStyle(GolfSimTheme.Label) { normal = { textColor = GolfSimTheme.Gold } });
            y += 24f;
            if (Stepper(new Rect(body.x, y, body.width, 42f), Units.DistanceText(s.autoPuttOneMeters, "0.0"), out int d1))
            {
                s.autoPuttOneMeters = Mathf.Clamp(s.autoPuttOneMeters + d1 * 0.5f, 0.5f, 10f);
                if (s.autoPuttTwoMeters < s.autoPuttOneMeters + 0.5f) s.autoPuttTwoMeters = s.autoPuttOneMeters + 0.5f;
                changed = true;
            }
            y += 54f;
            GUI.Label(new Rect(body.x, y, body.width, 20f), "2 PUTT CIRCLE (white)", GolfSimTheme.Label);
            y += 24f;
            if (Stepper(new Rect(body.x, y, body.width, 42f), Units.DistanceText(s.autoPuttTwoMeters, "0.0"), out int d2))
            {
                s.autoPuttTwoMeters = Mathf.Clamp(s.autoPuttTwoMeters + d2 * 0.5f, s.autoPuttOneMeters + 0.5f, 30f);
                changed = true;
            }
            y += 54f;
            GUI.Label(new Rect(body.x, y, body.width, 20f), "OUTSIDE THE 2 PUTT CIRCLE", GolfSimTheme.Label);
            y += 24f;
            if (GUI.Button(new Rect(body.x, y, body.width * 0.49f, 40f), "3 PUTTS", s.autoPuttBeyond == 0 ? GolfSimTheme.TabActive : GolfSimTheme.Button)) { s.autoPuttBeyond = 0; changed = true; }
            if (GUI.Button(new Rect(body.x + body.width * 0.51f, y, body.width * 0.49f, 40f), "PUTT IT MYSELF", s.autoPuttBeyond == 1 ? GolfSimTheme.TabActive : GolfSimTheme.Button)) { s.autoPuttBeyond = 1; changed = true; }
            y += 52f;
            if (GUI.Button(new Rect(body.x, y, body.width, 40f), s.showPuttCircles ? "CIRCLES ON THE GREEN: SHOWN" : "CIRCLES ON THE GREEN: HIDDEN", s.showPuttCircles ? GolfSimTheme.TabActive : GolfSimTheme.Button)) { s.showPuttCircles = !s.showPuttCircles; changed = true; }
            GUI.enabled = true;
            if (changed) s.Save();
            if (GUI.Button(new Rect(body.x, body.yMax - 44f, body.width, 44f), "BACK", GolfSimTheme.Button)) page = Page.Grid;
        }

        // ---------------------------------------------------------------- ADD PLAYER (mid round)

        private string newPlayerName = "";
        private int joinTeam;
        private string addMessage;
        private Vector2 addScroll;

        private void OpenAddPlayer()
        {
            newPlayerName = "";
            addMessage = null;
            joinTeam = 0;
            RoundGameplayUI r = RoundGameplayUI.Current;
            if (r != null && r.JoinNeedsTeam)
            {
                // Default: the smallest team.
                for (int t = 1; t < r.TeamCount; t++) if (r.TeamSize(t) < r.TeamSize(joinTeam)) joinTeam = t;
            }
            page = Page.AddPlayer;
        }

        /// <summary>A golfer joins the round being played: pick a saved player or type a new name (team formats: pick the team).</summary>
        private void DrawAddPlayer(Rect body)
        {
            RoundGameplayUI round = RoundGameplayUI.Current;
            if (round == null) { page = Page.Grid; return; }
            string[] playing = round.RoundPlayers;
            float y = body.y;
            GUI.Label(new Rect(body.x, y, body.width, 22f), playing.Length + " of " + CourseSession.MaxPlayers + " players  •  " + round.FormatName.ToUpperInvariant(), new GUIStyle(GolfSimTheme.Label) { normal = { textColor = GolfSimTheme.Gold } });
            y += 28f;

            if (round.JoinNeedsTeam)
            {
                GUI.Label(new Rect(body.x, y, body.width, 20f), "JOIN TEAM", GolfSimTheme.Label);
                y += 22f;
                float tw = (body.width - 6f * (round.TeamCount - 1)) / Mathf.Max(1, round.TeamCount);
                for (int t = 0; t < round.TeamCount; t++)
                {
                    bool full = round.TeamSize(t) >= round.MaxTeamSize;
                    string label = "TEAM " + GameFormats.TeamLetters[t] + "  (" + round.TeamSize(t) + "/" + round.MaxTeamSize + ")";
                    GUI.enabled = !full;
                    if (GUI.Button(new Rect(body.x + t * (tw + 6f), y, tw, 38f), label, joinTeam == t ? GolfSimTheme.TabActive : GolfSimTheme.Button)) joinTeam = t;
                    GUI.enabled = true;
                }
                y += 48f;
            }

            GUI.Label(new Rect(body.x, y, body.width, 20f), "NEW PLAYER NAME", GolfSimTheme.Label);
            y += 22f;
            GUI.SetNextControlName("GolfSimZA_NewPlayer");
            newPlayerName = GUI.TextField(new Rect(body.x, y, body.width - 110f, 40f), newPlayerName ?? "", 24, GolfSimTheme.TextField);
            if (GUI.Button(new Rect(body.xMax - 102f, y, 102f, 40f), "ADD", GolfSimTheme.AccentButton)) Join(round, newPlayerName);
            y += 50f;

            // Saved players who are not in this round.
            var others = new System.Collections.Generic.List<string>();
            foreach (PlayerProfile p in PlayerRoster.Current.players)
            {
                bool inRound = false;
                foreach (string n in playing) if (string.Equals(n, p.name, System.StringComparison.OrdinalIgnoreCase)) inRound = true;
                if (!inRound) others.Add(p.name);
            }
            GUI.Label(new Rect(body.x, y, body.width, 20f), others.Count > 0 ? "OR PICK A SAVED PLAYER" : "NO OTHER SAVED PLAYERS", GolfSimTheme.Label);
            y += 24f;
            float listBottom = body.yMax - 100f;
            Rect view = new Rect(body.x, y, body.width, Mathf.Max(44f, listBottom - y));
            Rect content = new Rect(0f, 0f, body.width - 18f, others.Count * 46f);
            addScroll = GUI.BeginScrollView(view, addScroll, content);
            for (int i = 0; i < others.Count; i++)
            {
                Rect b = new Rect(0f, i * 46f, content.width, 40f);
                if (GUI.Button(b, GUIContent.none, GolfSimTheme.Button)) { Join(round, others[i]); break; }
                GUI.color = PlayerRoster.ColorFor(others[i]);
                GUI.DrawTexture(new Rect(b.x + 10f, b.y + 10f, 6f, 20f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(b.x + 26f, b.y, b.width - 30f, b.height), others[i].ToUpperInvariant(), new GUIStyle(GolfSimTheme.Label) { alignment = TextAnchor.MiddleLeft, fontSize = 15 });
            }
            GUI.EndScrollView();

            if (!string.IsNullOrEmpty(addMessage))
                GUI.Label(new Rect(body.x, body.yMax - 94f, body.width, 42f), addMessage, new GUIStyle(GolfSimTheme.Label) { wordWrap = true, normal = { textColor = new Color(1f, 0.55f, 0.45f) } });
            if (GUI.Button(new Rect(body.x, body.yMax - 44f, body.width, 44f), "BACK", GolfSimTheme.Button)) page = Page.Grid;
        }

        private void Join(RoundGameplayUI round, string name)
        {
            addMessage = round.AddPlayer(name, joinTeam);
            if (addMessage == null) SetOpen(false);
        }

        private static bool Stepper(Rect r, string text, out int direction)
        {
            direction = 0;
            if (GUI.Button(new Rect(r.x, r.y, 60f, r.height), "–", GolfSimTheme.Button)) direction = -1;
            GUI.Label(new Rect(r.x + 64f, r.y, r.width - 128f, r.height), text, new GUIStyle(GolfSimTheme.Heading) { alignment = TextAnchor.MiddleCenter });
            if (GUI.Button(new Rect(r.xMax - 60f, r.y, 60f, r.height), "+", GolfSimTheme.Button)) direction = 1;
            return direction != 0;
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
            if (scene != PlayScene) GolfSimZA.MiniGames.MiniGameSession.End();
            if (scene == PlayersScene) PlayerPrefs.SetInt("GolfSimZA.BagReturnHome", 1);
            SceneManager.LoadScene(scene);
        }
    }
}
