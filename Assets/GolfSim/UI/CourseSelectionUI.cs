using System;
using System.Collections.Generic;
using GolfSimZA.Core;
using GolfSimZA.Courses;
using GolfSimZA.MiniGames;
using GolfSimZA.Players;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfSimZA.UI
{
    /// <summary>
    /// GolfSim ZA home: EXIT / logo / PLAYERS / SETTINGS bar, welcome banner and the mode
    /// tiles (Local Match, Practice, Map My Bag, Import Courses). Also hosts the course list
    /// (search, favourites, filters), the practice screen, player management, settings &amp;
    /// updates and course import. (Class name kept so the existing scene keeps working.)
    /// </summary>
    public sealed class CourseSelectionUI : MonoBehaviour
    {
        private enum Screen_ { Home, LocalMatch, Practice, OnCoursePractice, Players, Settings, Import, ConfirmQuit, MiniGames }

        private sealed class CourseEntry
        {
            public string Key;          // favourites key
            public string Id;           // library id, empty for demo courses
            public string Name, Location, Designer;
            public string[] Tees;
            public int Holes, Par;
            public bool Available = true;
            public bool Demo;
            public CourseDefinition Definition;
        }

        private const string RangeName = "GolfSim ZA Practice Range";
        private const string PlayScene = "GolfSimZA_0_6_PlayRound";

        private readonly List<CourseEntry> entries = new List<CourseEntry>();
        private readonly Dictionary<string, Texture2D> previews = new Dictionary<string, Texture2D>();
        private readonly CourseImportPanel importPanel = new CourseImportPanel();
        private readonly UpdatePanel updatePanel = new UpdatePanel();

        private Screen_ screen = Screen_.Home;
        private string search = "";
        private int libraryFilter;        // 0 all, 1 favourites, 2 imported, 3 demo
        private int sortMode;             // 0 name, 1 holes
        private int roundHoles = 18;
        private Vector2 listScroll;
        private SettingsScreen settingsScreen;
        private bool rebuildRequested = true;
        private Texture2D hero, heroShade, tileShade, heroGradient, navyTex;

        // MINI GAMES page
        private MiniGameId miniGame;
        private readonly List<string> miniPlayers = new List<string>();
        private readonly List<string> miniGuests = new List<string>();
        private int miniShots = 5, miniDistance;
        private bool miniExplore;
        private string guestName = "";
        private Vector2 miniScroll, miniDetailScroll;
        private string miniError = "";

        private void OnEnable() => CourseLibrary.Changed += RequestRebuild;
        private void OnDisable() => CourseLibrary.Changed -= RequestRebuild;
        private void RequestRebuild() => rebuildRequested = true;

        private void Start()
        {
            CourseLibrary.Reload();
            RebuildEntries();
            updatePanel.CheckInBackground();
            PlayerRoster.Current.ApplyToSession();
            CourseSession.PracticeMode = false;
            // Back on the home screen: any mini game has ended.
            MiniGameSession.End();
            settingsScreen = new SettingsScreen(() => screen = Screen_.Home, OpenBag, () => { screen = Screen_.Import; importPanel.Open(); }, updatePanel);

            // Another screen asked to come back to a specific home page (e.g. BACK from Round Settings).
            string open = PlayerPrefs.GetString(OpenScreenKey, "");
            if (!string.IsNullOrEmpty(open))
            {
                PlayerPrefs.DeleteKey(OpenScreenKey);
                if (Enum.TryParse(open, out Screen_ requested)) screen = requested;
                if (screen == Screen_.Players || screen == Screen_.Settings) settingsScreen.Open(screen == Screen_.Players);
                if (screen == Screen_.MiniGames) OpenMiniGames();
            }
        }

        private const string OpenScreenKey = "GolfSimZA.OpenHomeScreen";

        /// <summary>Loads the home scene on a given page: "Home", "LocalMatch", "Practice", "Players", "Settings" or "MiniGames".</summary>
        public static void OpenHome(string page)
        {
            PlayerPrefs.SetString(OpenScreenKey, page ?? "Home");
            PlayerPrefs.Save();
            UnityEngine.SceneManagement.SceneManager.LoadScene("GolfSimZA_0_5_CourseSelection");
        }

        // ---------------------------------------------------------------- Data

        private void RebuildEntries()
        {
            rebuildRequested = false;
            entries.Clear();
            entries.Add(new CourseEntry { Key = "demo:karoo", Id = "", Name = "Karoo Valley Demo Course", Location = "South Africa", Designer = "GolfSim ZA", Tees = CourseSession.DemoTees, Holes = 18, Par = 72, Demo = true });
            entries.Add(new CourseEntry { Key = "demo:freestate", Id = "", Name = "Free State Hills Demo Course", Location = "South Africa", Designer = "GolfSim ZA", Tees = CourseSession.DemoTees, Holes = 18, Par = 72, Demo = true });

            foreach (CourseDefinition course in CourseLibrary.All)
            {
                entries.Add(new CourseEntry
                {
                    Key = course.id, Id = course.id, Name = course.name,
                    Location = string.IsNullOrEmpty(course.location) ? "" : course.location,
                    Designer = course.designer ?? "",
                    Tees = course.tees != null && course.tees.Length > 0 ? course.tees : CourseSession.DemoTees,
                    Holes = course.HoleCount, Par = course.TotalPar,
                    Available = CourseLibrary.IsAvailable(course), Definition = course
                });
            }

            hero = null;
            foreach (CourseEntry e in entries)
            {
                Texture2D t = Preview(e);
                if (t != null) { hero = t; break; }
            }
        }

        private Texture2D Preview(CourseEntry entry)
        {
            if (entry == null || entry.Definition == null) return null;
            if (!previews.TryGetValue(entry.Id, out Texture2D texture))
            {
                texture = CourseLibrary.LoadSplash(entry.Definition);
                previews[entry.Id] = texture;
            }
            return texture;
        }

        private List<CourseEntry> Filtered()
        {
            var list = new List<CourseEntry>();
            string q = (search ?? "").Trim().ToLowerInvariant();
            foreach (CourseEntry e in entries)
            {
                if (libraryFilter == 1 && !AppSettings.Current.IsFavourite(e.Key)) continue;
                if (libraryFilter == 2 && e.Demo) continue;
                if (libraryFilter == 3 && !e.Demo) continue;
                if (q.Length > 0 && (e.Name + " " + e.Location + " " + e.Designer).ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0) continue;
                list.Add(e);
            }
            // One comparison (List.Sort is not stable): favourites first, then the chosen order.
            AppSettings settings = AppSettings.Current;
            list.Sort((a, b) =>
            {
                int fav = settings.IsFavourite(b.Key).CompareTo(settings.IsFavourite(a.Key));
                if (fav != 0) return fav;
                if (sortMode == 1 && a.Holes != b.Holes) return b.Holes.CompareTo(a.Holes);
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }

        // ---------------------------------------------------------------- Actions

        private void PlayCourse(CourseEntry e)
        {
            string tee = PickTee(e);
            int holes = Mathf.Clamp(Mathf.Min(roundHoles, e.Holes), 1, 18);
            if (e.Holes < 18 && roundHoles == 18) holes = e.Holes;
            // Players are chosen fresh in Round Settings for every course (nothing carried over from home).
            CourseSession.SetPlayers(new string[0]);
            CourseSession.Teams = null;
            CourseSession.PracticeMode = false;
            CourseSession.SetSession(e.Name, tee, holes);
            CourseSession.SetCourse(e.Id, e.Demo ? null : e.Tees);
            CourseSession.SetHoles(e.Holes, 0, holes);
            SceneManager.LoadScene("GolfSimZA_0_5_RoundSettings");
        }

        private void PracticeCourse(CourseEntry e)
        {
            PlayerProfile first = PlayerRoster.Current.Selected[0];
            CourseSession.SetPlayers(new[] { PlayerRoster.Clean(first.name) });
            CourseSession.PracticeMode = true;
            CourseSession.SetSession(e.Name, PickTee(e), Mathf.Clamp(e.Holes, 1, 18));
            CourseSession.SetCourse(e.Id, e.Demo ? null : e.Tees);
            CourseSession.SetHoles(e.Holes, 0, Mathf.Clamp(e.Holes, 1, 18));
            SceneManager.LoadScene(PlayScene);
        }

        private void PlayRange()
        {
            PlayerRoster.Current.ApplyToSession();
            CourseSession.PracticeMode = false;
            CourseSession.SetSession(RangeName, "Blue", 18);
            CourseSession.SetCourse("", null);
            CourseSession.SetHoles(18, 0, 18);
            SceneManager.LoadScene(PlayScene);
        }

        private static string PickTee(CourseEntry e)
        {
            string current = CourseSession.TeeName;
            foreach (string t in e.Tees) if (t == current) return t;
            return e.Tees.Length > 2 ? e.Tees[e.Tees.Length / 2] : e.Tees[0];
        }

        private static void OpenBag(string playerName)
        {
            PlayerPrefs.SetString("GolfSimZA.OpenBagFor", playerName ?? "");
            PlayerPrefs.SetInt("GolfSimZA.BagReturnHome", 1);
            PlayerPrefs.Save();
            SceneManager.LoadScene("GolfSimZA_0_5_Players");
        }

        // ---------------------------------------------------------------- GUI

        private void EnsureTextures()
        {
            GolfSimTheme.Ensure();
            if (heroShade != null) return;
            heroShade = GolfSimTheme.Tex(new Color(0.02f, 0.05f, 0.06f, 0.55f));
            navyTex = GolfSimTheme.Tex(GolfSimTheme.Navy);
            tileShade = GolfSimTheme.Tex(new Color(0f, 0f, 0f, 0.35f));
            heroGradient = new Texture2D(1, 64) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
            {
                float t = y / 63f; // bottom → top
                Color c = t < 0.45f
                    ? Color.Lerp(new Color(0.10f, 0.28f, 0.12f), new Color(0.24f, 0.46f, 0.20f), t / 0.45f)
                    : Color.Lerp(new Color(0.60f, 0.76f, 0.86f), new Color(0.20f, 0.42f, 0.66f), (t - 0.45f) / 0.55f);
                heroGradient.SetPixel(0, y, c);
            }
            heroGradient.Apply();
        }

        private void OnGUI()
        {
            EnsureTextures();
            if (Event.current.type == EventType.Layout)
            {
                if (rebuildRequested) RebuildEntries();
                updatePanel.Poll();
            }

            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), navyTex);
            float margin = Mathf.Max(24f, Screen.width * 0.03f);
            Rect content = new Rect(margin, 86f, Screen.width - margin * 2f, Screen.height - 110f);

            DrawTopBar(margin);

            switch (screen)
            {
                case Screen_.Home: DrawHome(content); break;
                case Screen_.LocalMatch: DrawCourseList(content, false); break;
                case Screen_.OnCoursePractice: DrawCourseList(content, true); break;
                case Screen_.Practice: DrawPractice(content); break;
                case Screen_.Players:
                case Screen_.Settings: settingsScreen.Draw(content); break;
                case Screen_.Import:
                    importPanel.Draw(content);
                    if (!importPanel.IsOpen) screen = Screen_.LocalMatch;
                    break;
                case Screen_.ConfirmQuit: DrawConfirmQuit(content); break;
                case Screen_.MiniGames: DrawMiniGames(content); break;
            }
        }

        private void DrawTopBar(float margin)
        {
            float y = 20f;
            if (screen == Screen_.Home)
            {
                if (GUI.Button(new Rect(margin, y, 110f, 40f), "EXIT", GolfSimTheme.TopBarButton)) screen = Screen_.ConfirmQuit;
            }
            else if (GUI.Button(new Rect(margin, y, 110f, 40f), "←  BACK", GolfSimTheme.TopBarButton))
            {
                importPanel.Close();
                updatePanel.Close();
                screen = screen == Screen_.OnCoursePractice ? Screen_.Practice : Screen_.Home;
            }

            GolfSimTheme.DrawLogo(new Rect(0, y, Screen.width, 40f));

            float right = Screen.width - margin;
            string settings = updatePanel.UpdateAvailable ? "UPDATE " + updatePanel.LatestVersion : "SETTINGS";
            if (GUI.Button(new Rect(right - 190f, y, 190f, 40f), settings, updatePanel.UpdateAvailable ? GolfSimTheme.AccentButton : GolfSimTheme.TopBarButton))
            {
                screen = Screen_.Settings;
                settingsScreen.Open();
            }
            if (GUI.Button(new Rect(right - 330f, y, 130f, 40f), "PLAYERS", GolfSimTheme.TopBarButton)) { screen = Screen_.Players; settingsScreen.Open(true); }
            GUI.DrawTexture(new Rect(margin, y + 50f, Screen.width - margin * 2f, 2f), GolfSimTheme.AccentTex);
        }

        // ---- Home

        private void DrawHome(Rect r)
        {
            Rect heroRect = new Rect(r.x, r.y, r.width, r.height - 20f);
            GUI.DrawTexture(heroRect, hero != null ? hero : heroGradient, ScaleMode.ScaleAndCrop);
            GUI.DrawTexture(heroRect, heroShade);

            GUI.Label(new Rect(heroRect.x + 40f, heroRect.y + 30f, 700f, 60f), "WELCOME", new GUIStyle(GolfSimTheme.Title) { fontSize = 52 });
            GUI.Label(new Rect(heroRect.x + 44f, heroRect.y + 92f, 700f, 26f), "Your home course, your swing, your numbers.", new GUIStyle(GolfSimTheme.Heading) { fontStyle = FontStyle.Normal });
            GUI.Label(new Rect(heroRect.x + 44f, heroRect.y + 124f, 900f, 22f), "Pick a course, then choose who plays and the format in Round Settings.", GolfSimTheme.Label);

            string[] names = { "LOCAL MATCH", "PRACTICE", "MINI GAMES", "MAP MY BAG", "IMPORT COURSES" };
            string[] subs = { "Play a full round", "Range & on-course practice", "8 games  •  1-8 players", "Six-shot club distances", "Add courses from a folder" };
            float gap = 18f;
            float tw = (heroRect.width - 80f - gap * (names.Length - 1)) / names.Length;
            float th = Mathf.Min(170f, heroRect.height * 0.32f);
            float ty = heroRect.yMax - th - 34f;
            for (int i = 0; i < names.Length; i++)
            {
                Rect t = new Rect(heroRect.x + 40f + i * (tw + gap), ty, tw, th);
                Texture2D img = i == 2 ? GolfSimTheme.RangePicture() : TileImage(i > 2 ? i - 1 : i);
                if (img != null)
                {
                    GUI.DrawTexture(new Rect(t.x + 3f, t.y + 3f, t.width - 6f, t.height - 6f), img, ScaleMode.ScaleAndCrop);
                    GUI.DrawTexture(new Rect(t.x + 3f, t.y + 3f, t.width - 6f, t.height - 6f), tileShade);
                }
                if (GolfSimTheme.FitButton(t, names[i], GolfSimTheme.BigTile)) OnTile(i);
                GUI.Label(new Rect(t.x, t.y + 12f, t.width, 20f), subs[i], new GUIStyle(GolfSimTheme.Label) { alignment = TextAnchor.MiddleCenter });
            }

            GUI.Label(new Rect(heroRect.x + 12f, heroRect.yMax - 22f, 300f, 18f), "v" + GolfSimVersion.Version, GolfSimTheme.Subtitle);
        }

        private Texture2D TileImage(int index)
        {
            int n = 0;
            foreach (CourseEntry e in entries)
            {
                Texture2D t = Preview(e);
                if (t == null) continue;
                if (n == index) return t;
                n++;
            }
            return null;
        }

        private void OnTile(int index)
        {
            switch (index)
            {
                case 0: screen = Screen_.LocalMatch; search = ""; break;
                case 1: screen = Screen_.Practice; break;
                case 2: OpenMiniGames(); screen = Screen_.MiniGames; break;
                case 3: OpenBag(""); break; // choose the player first
                case 4: screen = Screen_.Import; importPanel.Open(); break;
            }
        }

        // ---- Course list (Local Match / On-course practice)

        private void DrawCourseList(Rect r, bool practice)
        {
            float filterW = Mathf.Min(340f, r.width * 0.28f);
            Rect listRect = new Rect(r.x, r.y, r.width - filterW - 16f, r.height);
            Rect filterRect = new Rect(listRect.xMax + 16f, r.y, filterW, r.height);

            GUI.Label(new Rect(listRect.x, listRect.y, 360f, 40f), practice ? "ON-COURSE PRACTICE" : "SELECT COURSE", GolfSimTheme.Title);
            search = GUI.TextField(new Rect(listRect.x + 380f, listRect.y + 2f, Mathf.Max(160f, listRect.width - 380f), 36f), search ?? "", GolfSimTheme.TextField);
            if (string.IsNullOrEmpty(search)) GUI.Label(new Rect(listRect.x + 392f, listRect.y + 10f, 200f, 20f), "Search courses…", GolfSimTheme.Subtitle);

            List<CourseEntry> list = Filtered();
            Rect view = new Rect(listRect.x, listRect.y + 52f, listRect.width, listRect.height - 60f);
            float rowH = 64f;
            listScroll = GUI.BeginScrollView(view, listScroll, new Rect(0, 0, view.width - 20f, Mathf.Max(view.height, list.Count * (rowH + 6f))));
            for (int i = 0; i < list.Count; i++)
            {
                CourseEntry e = list[i];
                Rect row = new Rect(0, i * (rowH + 6f), view.width - 20f, rowH);
                GUI.Box(row, GUIContent.none, GolfSimTheme.Card);
                Texture2D p = Preview(e);
                float x = row.x + 10f;
                if (p != null) GUI.DrawTexture(new Rect(x, row.y + 7f, 88f, rowH - 14f), p, ScaleMode.ScaleAndCrop);
                x += 100f;
                float infoX = Mathf.Max(x + 180f, row.x + row.width * 0.47f);
                float nameW = infoX - x - 12f;
                float infoW = Mathf.Max(60f, row.xMax - 222f - infoX);
                GUI.Label(new Rect(x, row.y + 10f, nameW, 22f), GolfSimTheme.Ellipsize(e.Name, GolfSimTheme.Heading, nameW), GolfSimTheme.Heading);
                GUI.Label(new Rect(x, row.y + 36f, nameW, 18f), GolfSimTheme.Ellipsize(e.Demo ? "Demo course" : e.Location, GolfSimTheme.Subtitle, nameW), GolfSimTheme.Subtitle);
                GUI.Label(new Rect(infoX, row.y + 12f, infoW, 20f), GolfSimTheme.Ellipsize(e.Holes + " holes  •  par " + e.Par, GolfSimTheme.Label, infoW), GolfSimTheme.Label);
                GUI.Label(new Rect(infoX, row.y + 36f, infoW, 18f), GolfSimTheme.Ellipsize(e.Designer, GolfSimTheme.Subtitle, infoW), GolfSimTheme.Subtitle);

                bool fav = AppSettings.Current.IsFavourite(e.Key);
                if (GUI.Button(new Rect(row.xMax - 214f, row.y + 12f, 44f, 40f), fav ? "★" : "☆", new GUIStyle(GolfSimTheme.GhostButton) { fontSize = 26, normal = { textColor = fav ? GolfSimTheme.Gold : GolfSimTheme.Muted } }))
                    AppSettings.Current.ToggleFavourite(e.Key);

                GUI.enabled = e.Available;
                if (GUI.Button(new Rect(row.xMax - 160f, row.y + 12f, 148f, 40f), e.Available ? "PLAY" : "FILE MISSING", GolfSimTheme.AccentButton))
                {
                    if (practice) PracticeCourse(e); else PlayCourse(e);
                }
                GUI.enabled = true;
            }
            if (list.Count == 0)
                GUI.Label(new Rect(10f, 10f, view.width - 40f, 60f), libraryFilter == 1 ? "No favourites yet - tap ☆ next to a course." : "No courses match. Use IMPORT COURSES to add your own.", GolfSimTheme.Subtitle);
            GUI.EndScrollView();

            DrawFilters(filterRect, list.Count, practice);
        }

        private void DrawFilters(Rect r, int count, bool practice)
        {
            GUI.Box(r, GUIContent.none, GolfSimTheme.Card);
            float x = r.x + 16f, w = r.width - 32f, y = r.y + 14f;
            GUI.Label(new Rect(x, y, w, 24f), "FILTERS", GolfSimTheme.Heading);
            GUI.Label(new Rect(x, y + 2f, w, 22f), count + (count == 1 ? " COURSE" : " COURSES"), new GUIStyle(GolfSimTheme.Label) { alignment = TextAnchor.MiddleRight });
            y += 38f;

            GUI.Label(new Rect(x, y, w, 18f), "COURSE LIBRARY", GolfSimTheme.Label);
            y += 22f;
            string[] libs = { "ALL", "FAVOURITES", "IMPORTED", "DEMO" };
            for (int i = 0; i < libs.Length; i++)
                if (GUI.Button(new Rect(x + (i % 2) * (w * 0.5f + 2f), y + (i / 2) * 44f, w * 0.5f - 2f, 40f), libs[i], libraryFilter == i ? GolfSimTheme.TabActive : GolfSimTheme.Button)) libraryFilter = i;
            y += 96f;

            GUI.Label(new Rect(x, y, w, 18f), "SORT", GolfSimTheme.Label);
            y += 22f;
            if (GUI.Button(new Rect(x, y, w * 0.5f - 2f, 40f), "NAME A–Z", sortMode == 0 ? GolfSimTheme.TabActive : GolfSimTheme.Button)) sortMode = 0;
            if (GUI.Button(new Rect(x + w * 0.5f + 2f, y, w * 0.5f - 2f, 40f), "MOST HOLES", sortMode == 1 ? GolfSimTheme.TabActive : GolfSimTheme.Button)) sortMode = 1;
            y += 56f;

            if (!practice)
            {
                GUI.Label(new Rect(x, y, w, 18f), "ROUND LENGTH", GolfSimTheme.Label);
                y += 22f;
                if (GUI.Button(new Rect(x, y, w * 0.5f - 2f, 40f), "9 HOLES", roundHoles == 9 ? GolfSimTheme.TabActive : GolfSimTheme.Button)) roundHoles = 9;
                if (GUI.Button(new Rect(x + w * 0.5f + 2f, y, w * 0.5f - 2f, 40f), "18 HOLES", roundHoles == 18 ? GolfSimTheme.TabActive : GolfSimTheme.Button)) roundHoles = 18;
                y += 56f;
                GUI.Label(new Rect(x, y, w, 60f), "After you pick a course you choose the players (up to " + PlayerRoster.MaxPlayersInRound + "), teams and the format.", GolfSimTheme.Body);
            }
            else
            {
                GUI.Label(new Rect(x, y, w, 80f), "Practise any hole with no scoring. Use ◀ HOLE ▶ on the play screen to change holes, and MULLIGAN in the menu to replay a shot.", GolfSimTheme.Body);
            }

            if (GUI.Button(new Rect(x, r.yMax - 110f, w, 40f), "RESET FILTERS", GolfSimTheme.Button)) { libraryFilter = 0; sortMode = 0; search = ""; }
            if (GUI.Button(new Rect(x, r.yMax - 60f, w, 44f), "IMPORT COURSES", GolfSimTheme.AccentButton)) { screen = Screen_.Import; importPanel.Open(); }
        }

        // ---- Practice

        private void DrawPractice(Rect r)
        {
            GUI.DrawTexture(r, hero != null ? hero : heroGradient, ScaleMode.ScaleAndCrop);
            GUI.DrawTexture(r, heroShade);
            GUI.Label(new Rect(r.x + 40f, r.y + 30f, 800f, 60f), "PRACTICE", new GUIStyle(GolfSimTheme.Title) { fontSize = 52 });
            GUI.Label(new Rect(r.x + 44f, r.y + 92f, 800f, 26f), "Head to the range for an endless bucket, or practise holes on any course.", new GUIStyle(GolfSimTheme.Heading) { fontStyle = FontStyle.Normal });

            float tw = Mathf.Min(320f, r.width * 0.3f), th = tw * 0.62f;
            float cx = r.center.x;
            Rect a = new Rect(cx - tw - 12f, r.yMax - th - 60f, tw, th);
            Rect b = new Rect(cx + 12f, r.yMax - th - 60f, tw, th);
            GUI.DrawTexture(new Rect(a.x + 3f, a.y + 3f, a.width - 6f, a.height - 6f), GolfSimTheme.RangePicture(), ScaleMode.ScaleAndCrop);
            GUI.DrawTexture(new Rect(a.x + 3f, a.y + 3f, a.width - 6f, a.height - 6f), tileShade);
            if (GolfSimTheme.FitButton(a, "PRACTICE RANGE", GolfSimTheme.BigTile)) PlayRange();
            GUI.Label(new Rect(a.x, a.y + 14f, a.width, 20f), "Targets, distances, shot data", new GUIStyle(GolfSimTheme.Label) { alignment = TextAnchor.MiddleCenter });
            Texture2D img = TileImage(0);
            if (img != null)
            {
                GUI.DrawTexture(new Rect(b.x + 3f, b.y + 3f, b.width - 6f, b.height - 6f), img, ScaleMode.ScaleAndCrop);
                GUI.DrawTexture(new Rect(b.x + 3f, b.y + 3f, b.width - 6f, b.height - 6f), tileShade);
            }
            if (GolfSimTheme.FitButton(b, "ON-COURSE PRACTICE", GolfSimTheme.BigTile)) { screen = Screen_.OnCoursePractice; search = ""; }
            GUI.Label(new Rect(b.x, b.y + 14f, b.width, 20f), "Any hole, no scoring", new GUIStyle(GolfSimTheme.Label) { alignment = TextAnchor.MiddleCenter });
        }

        // ---- Mini games

        /// <summary>Opens the MINI GAMES page with the last game and players (or the players selected for rounds).</summary>
        private void OpenMiniGames()
        {
            miniGame = MiniGameSession.LastGame;
            miniPlayers.Clear();
            miniGuests.Clear();
            foreach (string n in MiniGameSession.LastPlayers)
            {
                string name = PlayerRoster.Clean(n);
                if (string.IsNullOrEmpty(name) || miniPlayers.Contains(name)) continue;
                miniPlayers.Add(name);
                if (PlayerRoster.Current.Find(name) == null) miniGuests.Add(name);
            }
            if (miniPlayers.Count == 0)
                foreach (PlayerProfile p in PlayerRoster.Current.Selected) miniPlayers.Add(PlayerRoster.Clean(p.name));
            miniExplore = MiniGameSession.Explore;
            miniDistance = MiniGameSession.Distance;
            SelectMiniGame(miniGame, true);
            miniError = "";
        }

        private void SelectMiniGame(MiniGameId id, bool keepShots)
        {
            miniGame = id;
            MiniGameInfo info = MiniGameCatalog.Get(id);
            int last = MiniGameSession.Shots;
            miniShots = keepShots && Array.IndexOf(info.ShotChoices, last) >= 0 ? last : info.ShotChoices[0];
            if (!info.HasExplore) miniExplore = false;
            while (miniPlayers.Count > info.MaxPlayers) miniPlayers.RemoveAt(miniPlayers.Count - 1);
            miniDetailScroll = Vector2.zero;
        }

        private void StartMiniGame()
        {
            MiniGameInfo info = MiniGameCatalog.Get(miniGame);
            if (miniPlayers.Count == 0) { miniError = "Pick at least one player."; return; }
            string[] names = miniPlayers.ToArray();
            MiniGameSession.Start(miniGame, names, miniShots, info.HasExplore && miniExplore, info.HasDistance ? miniDistance : 0);
            // Mini games are played on the driving range.
            CourseSession.SetPlayers(names);
            CourseSession.Teams = null;
            CourseSession.PracticeMode = false;
            CourseSession.SetSession(RangeName, "Blue", 18);
            CourseSession.SetCourse("", null);
            CourseSession.SetHoles(18, 0, 18);
            SceneManager.LoadScene(PlayScene);
        }

        private void DrawMiniGames(Rect r)
        {
            float leftW = Mathf.Clamp(r.width * 0.42f, 360f, 620f);
            Rect left = new Rect(r.x, r.y, leftW, r.height);
            Rect right = new Rect(left.xMax + 18f, r.y, r.width - leftW - 18f, r.height);

            GUI.Label(new Rect(left.x, left.y, left.width, 40f), "MINI GAMES", GolfSimTheme.Title);
            GUI.Label(new Rect(left.x, left.y + 40f, left.width, 20f), "Fun games on the range for 1 to 8 players", GolfSimTheme.Subtitle);

            // Game cards by group.
            string[] groups = { MiniGameCatalog.Target, MiniGameCatalog.Classic, MiniGameCatalog.Fun };
            const float cardH = 66f, headH = 30f;
            float total = 0f;
            foreach (string g in groups)
            {
                total += headH;
                foreach (MiniGameInfo info in MiniGameCatalog.All) if (info.Group == g) total += cardH + 6f;
            }
            Rect view = new Rect(left.x, left.y + 70f, left.width, left.height - 76f);
            float innerW = view.width - (total > view.height ? 18f : 0f);
            miniScroll = GUI.BeginScrollView(view, miniScroll, new Rect(0, 0, innerW, Mathf.Max(view.height, total)));
            float y = 0f;
            foreach (string g in groups)
            {
                GUI.Label(new Rect(0, y + 6f, innerW, 20f), g, new GUIStyle(GolfSimTheme.Label) { normal = { textColor = GolfSimTheme.Gold } });
                y += headH;
                foreach (MiniGameInfo info in MiniGameCatalog.All)
                {
                    if (info.Group != g) continue;
                    Rect card = new Rect(0, y, innerW, cardH);
                    bool on = info.Id == miniGame;
                    if (GUI.Button(card, GUIContent.none, on ? GolfSimTheme.SelectedCard : GolfSimTheme.Card)) SelectMiniGame(info.Id, false);
                    GUI.color = info.Accent;
                    GUI.DrawTexture(new Rect(card.x + 4f, card.y + 8f, 6f, card.height - 16f), GolfSimTheme.White);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(card.x + 22f, card.y + 10f, card.width - 34f, 24f), info.Name, new GUIStyle(GolfSimTheme.Heading) { normal = { textColor = on ? info.Accent : Color.white } });
                    GUI.Label(new Rect(card.x + 22f, card.y + 36f, card.width - 34f, 20f), GolfSimTheme.Ellipsize(info.Tagline, GolfSimTheme.Subtitle, card.width - 34f), GolfSimTheme.Subtitle);
                    y += cardH + 6f;
                }
            }
            GUI.EndScrollView();

            DrawMiniDetails(right);
        }

        private void DrawMiniDetails(Rect r)
        {
            MiniGameInfo info = MiniGameCatalog.Get(miniGame);
            GUI.Box(r, GUIContent.none, GolfSimTheme.Card);
            GUI.color = info.Accent;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 5f), GolfSimTheme.White);
            GUI.color = Color.white;

            float x = r.x + 22f, w = r.width - 44f;
            Rect startRect = new Rect(x, r.yMax - 66f, w, 50f);
            Rect view = new Rect(r.x + 4f, r.y + 10f, r.width - 8f, startRect.y - r.y - 18f);
            var body = new GUIStyle(GolfSimTheme.Body) { wordWrap = true };
            float innerW = view.width - 36f;
            float howH = body.CalcHeight(new GUIContent(info.HowToPlay), innerW);
            float scoreH = body.CalcHeight(new GUIContent(info.Scoring), innerW);
            int rosterRows = (PlayerRoster.Current.players.Count + miniGuests.Count + 2) / 3;
            float contentH = 110f + howH + scoreH + 60f + (info.HasExplore ? 76f : 0f) + (info.HasDistance ? 76f : 0f) + 76f + 40f + rosterRows * 46f + 60f;
            miniDetailScroll = GUI.BeginScrollView(view, miniDetailScroll, new Rect(0, 0, view.width - 18f, Mathf.Max(view.height, contentH)));
            float cx = 14f, y = 6f;

            GUI.Label(new Rect(cx, y, innerW, 40f), info.Name, new GUIStyle(GolfSimTheme.Title) { normal = { textColor = info.Accent } });
            y += 42f;
            GUI.Label(new Rect(cx, y, innerW, 22f), info.Tagline, new GUIStyle(GolfSimTheme.Heading) { fontStyle = FontStyle.Normal });
            y += 34f;
            GUI.Label(new Rect(cx, y, innerW, 18f), "HOW TO PLAY", GolfSimTheme.Label);
            y += 20f;
            GUI.Label(new Rect(cx, y, innerW, howH), info.HowToPlay, body);
            y += howH + 10f;
            GUI.Label(new Rect(cx, y, innerW, 18f), "SCORING", GolfSimTheme.Label);
            y += 20f;
            GUI.Label(new Rect(cx, y, innerW, scoreH), info.Scoring, body);
            y += scoreH + 18f;

            if (info.HasExplore)
            {
                GUI.Label(new Rect(cx, y, innerW, 18f), "MODE", GolfSimTheme.Label);
                y += 22f;
                if (GUI.Button(new Rect(cx, y, innerW * 0.5f - 4f, 42f), "CHALLENGE  (scored)", !miniExplore ? GolfSimTheme.TabActive : GolfSimTheme.Button)) miniExplore = false;
                if (GUI.Button(new Rect(cx + innerW * 0.5f + 4f, y, innerW * 0.5f - 4f, 42f), "EXPLORE  (free play)", miniExplore ? GolfSimTheme.TabActive : GolfSimTheme.Button)) miniExplore = true;
                y += 54f;
            }

            if (info.HasDistance)
            {
                GUI.Label(new Rect(cx, y, innerW, 18f), "PIN DISTANCE", GolfSimTheme.Label);
                y += 22f;
                string u = " " + Units.DistanceUnit;
                string[] labels = { "RANDOM", Units.Distance(60f).ToString("0") + "-" + Units.Distance(100f).ToString("0") + u, Units.Distance(100f).ToString("0") + "-" + Units.Distance(150f).ToString("0") + u, Units.Distance(150f).ToString("0") + "-" + Units.Distance(200f).ToString("0") + u };
                float bw = (innerW - 24f) / 4f;
                for (int i = 0; i < labels.Length; i++)
                    if (GUI.Button(new Rect(cx + i * (bw + 8f), y, bw, 42f), labels[i], miniDistance == i ? GolfSimTheme.TabActive : GolfSimTheme.Button)) miniDistance = i;
                y += 54f;
            }

            if (!(info.HasExplore && miniExplore))
            {
                GUI.Label(new Rect(cx, y, innerW, 18f), info.ShotsLabel, GolfSimTheme.Label);
                y += 22f;
                int[] choices = (int[])info.ShotChoices.Clone();
                Array.Sort(choices);
                float bw = Mathf.Min(110f, (innerW - 8f * (choices.Length - 1)) / choices.Length);
                for (int i = 0; i < choices.Length; i++)
                    if (GUI.Button(new Rect(cx + i * (bw + 8f), y, bw, 42f), choices[i].ToString(), miniShots == choices[i] ? GolfSimTheme.TabActive : GolfSimTheme.Button)) miniShots = choices[i];
                y += 54f;
            }
            else
            {
                GUI.Label(new Rect(cx, y, innerW, 40f), "Explore has no score and no shot limit - press FINISH on the range when you are done.", body);
                y += 54f;
            }

            // Players: the roster plus guests, tap to add / remove (order = playing order).
            GUI.Label(new Rect(cx, y, innerW, 18f), "PLAYERS  " + miniPlayers.Count + " / " + info.MaxPlayers + "   (tap to add or remove - they play in the order picked)", GolfSimTheme.Label);
            y += 24f;
            var everyone = new List<string>();
            foreach (PlayerProfile p in PlayerRoster.Current.players) everyone.Add(PlayerRoster.Clean(p.name));
            foreach (string g in miniGuests) if (!everyone.Contains(g)) everyone.Add(g);
            float pw = (innerW - 16f) / 3f;
            for (int i = 0; i < everyone.Count; i++)
            {
                string name = everyone[i];
                int order = miniPlayers.IndexOf(name);
                Rect b = new Rect(cx + (i % 3) * (pw + 8f), y + (i / 3) * 46f, pw, 40f);
                bool guest = miniGuests.Contains(name);
                string text = (order >= 0 ? (order + 1) + ".  " : "") + name + (guest ? "  (guest)" : "");
                if (GUI.Button(b, GolfSimTheme.Ellipsize(text, GolfSimTheme.Button, pw - 30f), order >= 0 ? GolfSimTheme.TabActive : GolfSimTheme.Button))
                {
                    miniError = "";
                    if (order >= 0)
                    {
                        miniPlayers.RemoveAt(order);
                        if (guest) miniGuests.Remove(name);
                    }
                    else if (miniPlayers.Count < info.MaxPlayers) miniPlayers.Add(name);
                    else miniError = "Up to " + info.MaxPlayers + " players.";
                }
                GUI.color = PlayerRoster.Current.Find(name) != null ? PlayerRoster.ColorFor(name) : GolfSimTheme.Muted;
                GUI.DrawTexture(new Rect(b.x + 8f, b.y + 15f, 10f, 10f), GolfSimTheme.White);
                GUI.color = Color.white;
            }
            y += Mathf.Max(1, (everyone.Count + 2) / 3) * 46f + 4f;

            // Guest (plays this game only, not added to the players list).
            guestName = GUI.TextField(new Rect(cx, y, innerW - 170f, 40f), guestName ?? "", 24, GolfSimTheme.TextField);
            if (string.IsNullOrEmpty(guestName)) GUI.Label(new Rect(cx + 12f, y + 10f, 260f, 20f), "Guest name…", GolfSimTheme.Subtitle);
            if (GUI.Button(new Rect(cx + innerW - 160f, y, 160f, 40f), "+ ADD GUEST", GolfSimTheme.Button))
            {
                string g = PlayerRoster.Clean(guestName);
                miniError = "";
                if (string.IsNullOrEmpty(g)) miniError = "Type the guest's name first.";
                else if (everyone.Exists(e => string.Equals(e, g, StringComparison.OrdinalIgnoreCase))) miniError = g + " is already on the list.";
                else if (miniPlayers.Count >= info.MaxPlayers) miniError = "Up to " + info.MaxPlayers + " players.";
                else { miniGuests.Add(g); miniPlayers.Add(g); guestName = ""; }
            }
            y += 48f;
            if (miniError.Length > 0) GUI.Label(new Rect(cx, y, innerW, 20f), miniError, new GUIStyle(GolfSimTheme.Label) { normal = { textColor = GolfSimTheme.Bad } });
            GUI.EndScrollView();

            GUI.enabled = miniPlayers.Count > 0;
            string start = "START  " + info.Name + "  •  " + miniPlayers.Count + (miniPlayers.Count == 1 ? " PLAYER" : " PLAYERS");
            if (GUI.Button(startRect, start, new GUIStyle(GolfSimTheme.AccentButton) { fontSize = 18 })) StartMiniGame();
            GUI.enabled = true;
        }

        private void DrawConfirmQuit(Rect r)
        {
            Rect box = new Rect(r.center.x - 220f, r.center.y - 110f, 440f, 200f);
            GUI.Box(box, GUIContent.none, GolfSimTheme.Overlay);
            GUI.Label(new Rect(box.x, box.y + 30f, box.width, 40f), "Quit GolfSim ZA?", new GUIStyle(GolfSimTheme.Title) { alignment = TextAnchor.MiddleCenter, fontSize = 26 });
            if (GUI.Button(new Rect(box.x + 30f, box.y + 110f, 180f, 48f), "YES, QUIT", GolfSimTheme.AccentButton))
            {
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }
            if (GUI.Button(new Rect(box.xMax - 210f, box.y + 110f, 180f, 48f), "NO", GolfSimTheme.Button)) screen = Screen_.Home;
        }

        private void OnDestroy()
        {
            foreach (Texture2D texture in previews.Values)
                if (texture != null) Destroy(texture);
            previews.Clear();
        }
    }
}
