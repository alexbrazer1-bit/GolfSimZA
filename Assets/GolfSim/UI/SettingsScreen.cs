using System;
using System.IO;
using GolfSimZA.Core;
using GolfSimZA.Courses;
using GolfSimZA.LaunchMonitors.GarminR10;
using GolfSimZA.Physics;
using UnityEngine;

namespace GolfSimZA.UI
{
    /// <summary>
    /// SETTINGS: PLAYERS | GAME (Game, Realism, System, Sounds) | VISUAL SETTINGS
    /// (User interface, Camera options, Offset) | UPDATES. Every option is saved to
    /// settings.json and applied straight away. Used on the home screen and in the game menu.
    /// </summary>
    public sealed class SettingsScreen
    {
        private enum Tab { Players, Game, Visual, Updates }
        private static readonly string[] TabNames = { "PLAYERS", "GAME", "VISUAL SETTINGS", "UPDATES" };
        private static readonly string[] GamePages = { "GAME", "REALISM", "SYSTEM", "SOUNDS" };
        private static readonly string[] VisualPages = { "USER INTERFACE", "CAMERA OPTIONS", "COURSE LOOK", "OFFSET" };

        private Tab tab = Tab.Game;
        private int gamePage, visualPage;
        private Vector2 scroll;
        private float contentHeight = 600f;
        private bool dirty;
        private bool confirmReset;
        private string systemMessage = "";

        private readonly PlayersPanel players;
        private readonly UpdatePanel updates;
        private readonly Action onClose;
        private readonly Action openImport;
        private GUIStyle pill, pillOn, sectionTitle, hint, valueBox, sliderTrack, sliderThumb;

        /// <param name="openBag">Map My Bag for a player (null during a round).</param>
        /// <param name="openImport">Opens the course importer (null during a round).</param>
        public SettingsScreen(Action onClose, Action<string> openBag, Action openImport, UpdatePanel updatePanel)
        {
            this.onClose = onClose;
            this.openImport = openImport;
            players = new PlayersPanel(openBag);
            updates = updatePanel ?? new UpdatePanel();
        }

        public void Open(bool showPlayers = false)
        {
            tab = showPlayers ? Tab.Players : Tab.Game;
            scroll = Vector2.zero;
            confirmReset = false;
        }

        private void EnsureStyles()
        {
            GolfSimTheme.Ensure();
            if (pill != null) return;
            pill = new GUIStyle(GolfSimTheme.Button) { fixedHeight = 36, fontSize = 13 };
            pillOn = new GUIStyle(GolfSimTheme.TabActive) { fixedHeight = 36, fontSize = 13 };
            sectionTitle = new GUIStyle(GolfSimTheme.Label) { fontSize = 14 };
            hint = new GUIStyle(GolfSimTheme.Subtitle) { fontSize = 12, wordWrap = true };
            valueBox = new GUIStyle(GolfSimTheme.Center) { fontSize = 16 };
            sliderTrack = new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = 8f, margin = new RectOffset(0, 0, 0, 0), border = new RectOffset(4, 4, 4, 4), normal = { background = GolfSimTheme.Rounded(new Color(1f, 1f, 1f, 0.2f), 4) } };
            sliderThumb = new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedWidth = 22f, fixedHeight = 22f, margin = new RectOffset(0, 0, -7, 0), border = new RectOffset(10, 10, 10, 10), normal = { background = GolfSimTheme.Rounded(GolfSimTheme.Accent, 10, Color.white) }, hover = { background = GolfSimTheme.Rounded(GolfSimTheme.AccentHover, 10, Color.white) }, active = { background = GolfSimTheme.Rounded(GolfSimTheme.Gold, 10, Color.white) } };
        }

        public void Draw(Rect r)
        {
            EnsureStyles();
            AppSettings s = AppSettings.Current;

            GUI.Label(new Rect(r.x, r.y, 260f, 40f), "SETTINGS", GolfSimTheme.Title);
            float tabW = Mathf.Min(190f, (r.width - 280f) / TabNames.Length - 6f);
            float tx = r.x + Mathf.Max(260f, (r.width - (tabW + 6f) * TabNames.Length) * 0.5f);
            for (int i = 0; i < TabNames.Length; i++)
                if (GolfSimTheme.FitButton(new Rect(tx + i * (tabW + 6f), r.y + 2f, tabW, 38f), TabNames[i], (int)tab == i ? GolfSimTheme.TabActive : GolfSimTheme.Tab))
                {
                    tab = (Tab)i;
                    scroll = Vector2.zero;
                    if (tab == Tab.Updates) updates.Open();
                }
            GUI.DrawTexture(new Rect(r.x, r.y + 48f, r.width, 2f), GolfSimTheme.AccentTex);

            Rect body = new Rect(r.x, r.y + 60f, r.width, r.height - 60f - 58f);
            switch (tab)
            {
                case Tab.Players: players.Draw(body); break;
                case Tab.Updates:
                    if (!updates.IsOpen) updates.Open();
                    updates.Draw(body);
                    break;
                default: DrawPaged(body, s); break;
            }

            if (GUI.Button(new Rect(r.xMax - 220f, r.yMax - 48f, 220f, 46f), "DONE", GolfSimTheme.AccentButton))
            {
                SaveIfDirty(true);
                onClose?.Invoke();
            }
            GUI.Label(new Rect(r.x, r.yMax - 40f, r.width - 240f, 30f), "Changes are saved and applied straight away.", GolfSimTheme.Subtitle);

            SaveIfDirty(false);
        }

        private void SaveIfDirty(bool force)
        {
            // Sliders save once they are let go (not on every frame of a drag).
            if (dirty && (force || GUIUtility.hotControl == 0))
            {
                dirty = false;
                AppSettings.Current.Save();
            }
        }

        private void DrawPaged(Rect body, AppSettings s)
        {
            string[] pages = tab == Tab.Game ? GamePages : VisualPages;
            ref int page = ref (tab == Tab.Game ? ref gamePage : ref visualPage);
            float navW = Mathf.Clamp(body.width * 0.2f, 170f, 260f);
            for (int i = 0; i < pages.Length; i++)
                if (GolfSimTheme.FitButton(new Rect(body.x, body.y + i * 50f, navW, 44f), pages[i], page == i ? GolfSimTheme.TabActive : GolfSimTheme.Button))
                {
                    page = i;
                    scroll = Vector2.zero;
                    confirmReset = false;
                }

            Rect panel = new Rect(body.x + navW + 16f, body.y, body.width - navW - 16f, body.height);
            GUI.Box(panel, GUIContent.none, GolfSimTheme.Card);
            Rect view = new Rect(panel.x + 8f, panel.y + 8f, panel.width - 16f, panel.height - 16f);
            float w = view.width - 40f;
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, view.width - 20f, Mathf.Max(view.height, contentHeight)));
            float y = 10f;
            float x = 14f;
            string title = pages[page];
            GUI.Label(new Rect(x, y, w, 30f), title, GolfSimTheme.Heading);
            y += 42f;

            if (tab == Tab.Game)
            {
                switch (page)
                {
                    case 0: y = GamePage(s, x, y, w); break;
                    case 1: y = RealismPage(s, x, y, w); break;
                    case 2: y = SystemPage(s, x, y, w); break;
                    case 3: y = SoundsPage(s, x, y, w); break;
                }
            }
            else
            {
                switch (page)
                {
                    case 0: y = InterfacePage(s, x, y, w); break;
                    case 1: y = CameraPage(s, x, y, w); break;
                    case 2: y = LookPage(s, x, y, w); break;
                    case 3: y = OffsetPage(s, x, y, w); break;
                }
            }
            contentHeight = y + 20f;
            GUI.EndScrollView();
        }

        // ------------------------------------------------------------ GAME pages

        private float GamePage(AppSettings s, float x, float y, float w)
        {
            int units = s.metricUnits ? 0 : 1;
            if (Choice(ref y, x, w, "UNITS", new[] { "METRIC  (km/h, m)", "IMPERIAL  (mph, yd)" }, ref units)) s.metricUnits = units == 0;

            float alt = s.homeAltitudeMeters;
            if (Slider(ref y, x, w, "ALTITUDE / ELEVATION  (driving range and demo holes)", ref alt, 0f, 3000f,
                s.metricUnits ? alt.ToString("0") + " m" : (alt * 3.28084f).ToString("0") + " ft", 10f))
                s.homeAltitudeMeters = alt;
            Hint(ref y, x, w, "Thinner air flies further. Johannesburg is about 1 750 m. Imported courses use their own altitude.");

            int map = s.miniMapRight ? 1 : 0;
            if (Choice(ref y, x, w, "MINI MAP LOCATION", new[] { "LEFT", "RIGHT" }, ref map)) s.miniMapRight = map == 1;

            int rot = s.rotationStyle;
            if (Choice(ref y, x, w, "PLAYER ROTATION STYLE", new[] { "CLASSIC", "PLAY OUT HOLE", "PUTT OUT" }, ref rot)) s.rotationStyle = rot;
            Hint(ref y, x, w, rot == 0 ? "Classic: the player furthest from the hole plays next."
                : rot == 1 ? "Play out hole: each player holes out before the next player tees off."
                : "Putt out: furthest from the hole plays, but once on the green a player keeps putting until holed.");

            Switch(ref y, x, w, "AUTO PUTT  (a ball that stops on the green is holed automatically)", ref s.autoPutt);
            if (s.autoPutt)
            {
                float one = s.autoPuttOneMeters, two = s.autoPuttTwoMeters;
                if (Slider(ref y, x, w, "ONE PUTT INSIDE", ref one, 0.5f, 10f, Units.DistanceText(one, "0.0"), 0.5f)) s.autoPuttOneMeters = one;
                if (Slider(ref y, x, w, "TWO PUTTS INSIDE", ref two, 1f, 30f, Units.DistanceText(two, "0.0"), 0.5f)) s.autoPuttTwoMeters = Mathf.Max(two, s.autoPuttOneMeters + 0.5f);
                int beyond = s.autoPuttBeyond;
                if (Choice(ref y, x, w, "OUTSIDE THE 2 PUTT CIRCLE", new[] { "3 PUTTS", "PUTT IT MYSELF" }, ref beyond)) s.autoPuttBeyond = beyond;
                Switch(ref y, x, w, "SHOW THE 1 PUTT (gold) AND 2 PUTT (white) CIRCLES ON THE GREEN", ref s.showPuttCircles);
            }
            return y;
        }

        private float RealismPage(AppSettings s, float x, float y, float w)
        {
            int wind = s.windMode;
            if (Choice(ref y, x, w, "WIND", Wind.ModeNames, ref wind)) s.windMode = wind;
            Hint(ref y, x, w, wind == 0 ? "No wind." : wind == 4 ? "The same wind on every hole." : "A new random wind on every hole (light 0.5-3, moderate 3-6, strong 6-10 m/s).");
            if (wind == 4)
            {
                float speed = s.windCustomMps;
                if (Slider(ref y, x, w, "WIND SPEED", ref speed, 0f, 15f, s.metricUnits ? speed.ToString("0.0") + " m/s" : (speed * 2.2369363f).ToString("0.0") + " mph", 0.5f)) s.windCustomMps = speed;
                float from = s.windCustomFromDeg;
                if (Slider(ref y, x, w, "WIND FROM", ref from, 0f, 359f, WindFromText(from), 5f)) s.windCustomFromDeg = from;
                Hint(ref y, x, w, "0° = from the green (headwind), 90° = from the right, 180° = from behind (tailwind), 270° = from the left.");
            }

            float stimp = s.greenStimp;
            if (Slider(ref y, x, w, "GREEN SPEED  (stimpmeter)", ref stimp, 7f, 14f, stimp.ToString("0.0"), 0.5f)) s.greenStimp = stimp;
            Hint(ref y, x, w, "8 = slow club greens, 10 = normal, 12+ = tournament fast.");

            int firm = s.fairwayFirmness;
            if (Choice(ref y, x, w, "FAIRWAY FIRMNESS", new[] { "SOFT", "NORMAL", "FIRM" }, ref firm)) s.fairwayFirmness = firm;
            Hint(ref y, x, w, "Firm fairways bounce and roll further; soft fairways stop the ball sooner.");
            Hint(ref y, x, w, "Gimme distance and mulligans are chosen per round in Round Settings.");
            return y;
        }

        private static string WindFromText(float deg)
        {
            string[] names = { "HEAD", "HEAD / RIGHT", "RIGHT", "TAIL / RIGHT", "TAIL", "TAIL / LEFT", "LEFT", "HEAD / LEFT" };
            int i = Mathf.RoundToInt(Mathf.Repeat(deg, 360f) / 45f) % 8;
            return deg.ToString("0") + "°  " + names[i];
        }

        private float SystemPage(AppSettings s, float x, float y, float w)
        {
            Section(ref y, x, w, "COURSE FOLDER");
            GUI.Label(new Rect(x, y, w, 36f), string.IsNullOrEmpty(s.lastCourseImportFolder) ? "(no folder imported yet)" : s.lastCourseImportFolder, GolfSimTheme.Body);
            y += 30f;
            float bw = Mathf.Min(220f, (w - 12f) / 3f);
            if (openImport != null && GUI.Button(new Rect(x, y, bw, 40f), "IMPORT COURSES", GolfSimTheme.AccentButton)) openImport();
            if (GUI.Button(new Rect(x + (openImport != null ? bw + 6f : 0f), y, bw, 40f), "RELOAD COURSES", GolfSimTheme.Button))
            {
                CourseLibrary.Reload();
                systemMessage = "Course list reloaded: " + CourseLibrary.All.Count + " imported courses.";
            }
            y += 52f;

            Section(ref y, x, w, "GARMIN R10");
            if (GUI.Button(new Rect(x, y, bw, 40f), "RESTART R10 BRIDGE", GolfSimTheme.Button))
            {
                int n = R10BridgeLauncher.RestartAll();
                systemMessage = n > 0 ? "R10 bridge stopped; the play screen starts it again in a few seconds." : "No R10 bridge was running. It starts with the next round or range session.";
            }
            y += 46f;
            Hint(ref y, x, w, "Use this if shots stop arriving from the R10 (same as unplugging and reconnecting).");

            Switch(ref y, x, w, "CHECK FOR UPDATES WHEN GOLFSIM ZA STARTS", ref s.checkForUpdatesOnStart);

            Section(ref y, x, w, "CHECK ALL COURSES  (trees, grass, water and textures of every imported course)");
            CourseSurvey survey = CourseSurvey.Running;
            if (openImport == null)
            {
                Hint(ref y, x, w, "Available on the home screen (not during a round).");
            }
            else if (survey != null && !survey.Finished)
            {
                GUI.Label(new Rect(x, y, w, 24f), "Checking " + (survey.Done + 1) + " of " + survey.Total + ":  " + survey.Current, GolfSimTheme.Body);
                y += 34f;
            }
            else
            {
                if (GUI.Button(new Rect(x, y, bw, 40f), "CHECK ALL COURSES", GolfSimTheme.Button)) CourseSurvey.Begin();
                if (File.Exists(CourseSurvey.LastReportPath) && GUI.Button(new Rect(x + bw + 6f, y, bw, 40f), "OPEN REPORT", GolfSimTheme.Button))
                    Application.OpenURL("file:///" + CourseSurvey.LastReportPath.Replace('\\', '/'));
                y += 48f;
                if (survey != null && survey.Finished) Hint(ref y, x, w, survey.Summary);
            }

            Section(ref y, x, w, "SETTINGS FILE");
            GUI.Label(new Rect(x, y, w, 22f), AppSettings.FilePath, GolfSimTheme.Body);
            y += 28f;
            if (GUI.Button(new Rect(x, y, bw, 40f), "OPEN FOLDER", GolfSimTheme.Button))
                Application.OpenURL("file:///" + Path.GetDirectoryName(AppSettings.FilePath).Replace('\\', '/'));
            if (!confirmReset)
            {
                if (GUI.Button(new Rect(x + bw + 6f, y, bw, 40f), "RESET TO DEFAULTS", GolfSimTheme.Button)) confirmReset = true;
            }
            else
            {
                if (GUI.Button(new Rect(x + bw + 6f, y, bw, 40f), "YES, RESET", GolfSimTheme.AccentButton))
                {
                    ResetToDefaults();
                    confirmReset = false;
                    systemMessage = "Game and visual settings are back to the defaults (players, favourites and the update folder are kept).";
                }
                if (GUI.Button(new Rect(x + (bw + 6f) * 2f, y, bw, 40f), "CANCEL", GolfSimTheme.Button)) confirmReset = false;
            }
            y += 52f;
            if (!string.IsNullOrEmpty(systemMessage)) Hint(ref y, x, w, systemMessage);
            return y;
        }

        private static void ResetToDefaults()
        {
            AppSettings old = AppSettings.Current;
            var fresh = new AppSettings
            {
                updateFolder = old.updateFolder,
                checkForUpdatesOnStart = old.checkForUpdatesOnStart,
                lastCourseImportFolder = old.lastCourseImportFolder,
                favouriteCourses = old.favouriteCourses,
                homeAltitudeMeters = old.homeAltitudeMeters,
                rangeTargetMeters = old.rangeTargetMeters,
                rangeFairwayWidth = old.rangeFairwayWidth,
                rangeGreenWidth = old.rangeGreenWidth,
                rangeRandomizer = old.rangeRandomizer
            };
            // Copy the defaults onto the live object so every screen keeps its reference.
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fresh), old);
            old.Save();
        }

        private float SoundsPage(AppSettings s, float x, float y, float w)
        {
            Volume(ref y, x, w, "GOLFSIM ZA SOUNDS  (club strike, landing, ball in the cup)", ref s.volumeGolf);
            Volume(ref y, x, w, "COURSE SOUNDS  (birds, water and other sounds inside imported courses)", ref s.volumeCourse);
            Volume(ref y, x, w, "MENU SOUNDS  (button clicks)", ref s.volumeMenu);
            Volume(ref y, x, w, "MASTER", ref s.volumeMaster);
            Switch(ref y, x, w, "BALL READY SOUND ALERT", ref s.ballReadySound);
            if (GUI.Button(new Rect(x, y, 200f, 40f), "TEST SOUNDS", GolfSimTheme.Button))
            {
                SaveIfDirty(true);
                GolfSimAudio.PlayStrike(65f, false);
                GolfSimAudio.PlayReady();
            }
            y += 52f;
            return y;
        }

        // ------------------------------------------------------------ VISUAL pages

        private float InterfacePage(AppSettings s, float x, float y, float w)
        {
            Switch(ref y, x, w, "ENABLE GIMME CIRCLE  (ring around the hole on the green)", ref s.showGimmeCircle);
            if (s.showGimmeCircle)
            {
                int c = s.gimmeCircleColor;
                if (Choice(ref y, x, w, "GIMME CIRCLE COLOUR", new[] { "RED", "BLUE", "WHITE" }, ref c)) s.gimmeCircleColor = c;
            }
            Switch(ref y, x, w, "SHOW AIM INDICATOR  (line on the ground along the aim)", ref s.showAimIndicator);
            Switch(ref y, x, w, "SHOW BALL READY INDICATOR", ref s.showBallReady);
            Switch(ref y, x, w, "HIDE USER INTERFACE ON SHOT  (clean screen while the ball flies)", ref s.hideUiOnShot);
            Switch(ref y, x, w, "DISTANCE BANNER OVER THE FLAG", ref s.showDistanceBanner);
            if (s.showDistanceBanner) Switch(ref y, x, w, "HIDE DISTANCE BANNER ON THE GREEN", ref s.hideBannerOnGreen);

            float sec = s.scorecardAfterHoleSeconds;
            if (Slider(ref y, x, w, "SCORECARD AFTER HOLE", ref sec, 0f, 15f, sec < 0.5f ? "OFF" : sec.ToString("0") + " s", 1f)) s.scorecardAfterHoleSeconds = Mathf.RoundToInt(sec);
            Switch(ref y, x, w, "SHOW UP NEXT MESSAGE  (who plays next)", ref s.showUpNextMessage);

            int club = s.clubSelectorMode;
            if (Choice(ref y, x, w, "SHOW CLUB SELECTOR", new[] { "SHOW", "HIDE", "AUTO HIDE" }, ref club)) s.clubSelectorMode = club;
            Hint(ref y, x, w, "Auto hide: hidden while the ball is in the air. With HIDE, change clubs with the keys (1-0, Q/E, C).");

            Section(ref y, x, w, "SHOT DATA TILES  (tap to show / hide, up to " + ShotDataTiles.MaxTiles + "; shown in the order picked)");
            var selected = ShotDataTiles.Selected;
            float cw = (w - 3 * 6f) / 4f;
            for (int i = 0; i < ShotDataTiles.All.Length; i++)
            {
                ShotDataTiles.TileDef t = ShotDataTiles.All[i];
                int order = selected.IndexOf(t.Id);
                Rect cell = new Rect(x + (i % 4) * (cw + 6f), y + (i / 4) * 44f, cw, 38f);
                if (GolfSimTheme.FitButton(cell, (order >= 0 ? (order + 1) + ".  " : "") + t.Caption(), order >= 0 ? pillOn : pill))
                    ShotDataTiles.Toggle(t.Id);
            }
            y += Mathf.Ceil(ShotDataTiles.All.Length / 4f) * 44f + 6f;
            if (GUI.Button(new Rect(x, y, 220f, 36f), "DEFAULT TILES", GolfSimTheme.Button))
            {
                s.dataTiles = new System.Collections.Generic.List<string>(ShotDataTiles.DefaultTiles);
                dirty = true;
            }
            y += 48f;
            return y;
        }

        private float CameraPage(AppSettings s, float x, float y, float w)
        {
            Switch(ref y, x, w, "FOLLOW CAMERA  (the camera follows the ball in flight)", ref s.followCamera);
            if (s.followCamera)
            {
                float d = s.followDelay;
                if (Slider(ref y, x, w, "FOLLOW DELAY", ref d, 0f, 1f, d < 0.2f ? "LOW" : d > 0.8f ? "HIGH" : (d * 100f).ToString("0") + "%", 0.05f)) s.followDelay = d;
            }
            Switch(ref y, x, w, "AUTOMATIC HOLE FLYOVER  (fly down each hole before the tee shot)", ref s.autoFlyover);
            float fs = s.flyoverSpeed, fh = s.flyoverHeight;
            if (Slider(ref y, x, w, "FLYOVER SPEED", ref fs, 0f, 1f, fs < 0.2f ? "SLOW" : fs > 0.8f ? "FAST" : (fs * 100f).ToString("0") + "%", 0.05f)) s.flyoverSpeed = fs;
            if (Slider(ref y, x, w, "FLYOVER HEIGHT", ref fh, 0f, 1f, fh < 0.2f ? "LOW" : fh > 0.8f ? "HIGH" : (fh * 100f).ToString("0") + "%", 0.05f)) s.flyoverHeight = fh;

            int trail = s.ballTrailColor;
            if (Choice(ref y, x, w, "BALL TRAIL COLOUR", ShotTracer.ColorNames, ref trail)) s.ballTrailColor = trail;
            int size = s.ballTrailThick ? 1 : 0;
            if (Choice(ref y, x, w, "BALL TRAIL SIZE", new[] { "STANDARD", "THICK" }, ref size)) s.ballTrailThick = size == 1;

            Switch(ref y, x, w, "GRADIENT FOG  (distance haze)", ref s.gradientFog);
            float draw = s.drawDistanceMeters;
            if (Slider(ref y, x, w, "DRAW DISTANCE  (lower = faster on a slow PC)", ref draw, 500f, 15000f, Units.DistanceText(draw), 100f)) s.drawDistanceMeters = draw;
            return y;
        }

        private float LookPage(AppSettings s, float x, float y, float w)
        {
            int quality = s.graphicsQuality;
            if (Choice(ref y, x, w, "GRAPHICS QUALITY  (shadows, sharp textures far away, smooth edges)", GolfSimZA.Visual.GraphicsQuality.Names, ref quality)) s.graphicsQuality = quality;
            Switch(ref y, x, w, "PHOTO LIGHTING  (bright sunlight and soft highlights like a course photo)", ref s.photoLighting);
            Switch(ref y, x, w, "CRISP PICTURE  (mild sharpening and softly darkened corners)", ref s.sharpen);

            int grass = s.roughGrass;
            if (Choice(ref y, x, w, "ROUGH GRASS  (3D grass in the rough, swaying in the wind)", new[] { "OFF", "ON" }, ref grass)) s.roughGrass = grass;
            if (grass > 0)
            {
                float gd = s.grassDensity;
                if (Slider(ref y, x, w, "GRASS AMOUNT", ref gd, 0.2f, 1f, gd < 0.35f ? "THIN" : gd > 0.85f ? "THICK" : (gd * 100f).ToString("0") + "%", 0.05f)) s.grassDensity = gd;
                float gdist = s.grassDistance;
                if (Slider(ref y, x, w, "GRASS DISTANCE  (lower = faster on a slow PC)", ref gdist, 30f, 150f, Units.DistanceText(gdist), 5f)) s.grassDistance = gdist;
            }
            Hint(ref y, x, w, "Grass grows only in the rough - never on fairways, greens, tees, bunkers, paths or water - and takes the colour of the course ground under it.");

            int boost = s.colourBoost;
            if (Choice(ref y, x, w, "COLOUR BOOST  (fairway and rough greens, sky)", GolfSimZA.Visual.ColorGrade.PresetNames, ref boost)) s.colourBoost = boost;
            Hint(ref y, x, w, "LUSH gives the rich summer-green fairways and rough of a well-kept course. Changes show straight away.");

            Switch(ref y, x, w, "CLOUDS  (over a plain sky; courses with their own sky picture keep it)", ref s.clouds);

            int trees = s.addTrees;
            if (Choice(ref y, x, w, "ADD TREES", new[] { "OFF", "COURSES WITHOUT TREES", "ALL COURSES" }, ref trees)) s.addTrees = trees;
            if (trees > 0)
            {
                float density = s.treeDensity;
                if (Slider(ref y, x, w, "TREE AMOUNT", ref density, 0.2f, 1f, density < 0.35f ? "FEW" : density > 0.8f ? "MANY" : (density * 100f).ToString("0") + "%", 0.05f)) s.treeDensity = density;
            }
            Hint(ref y, x, w, "GolfSim ZA trees are planted in the rough between holes (never on fairways, greens, tees, bunkers or water) and on both sides of the driving range. The ball hits them. Tree changes apply the next time a course or the range is opened.");
            return y;
        }

        private float OffsetPage(AppSettings s, float x, float y, float w)
        {
            Hint(ref y, x, w, "If the R10 is not square to the screen every shot starts left or right (or too high / low). Offsets are added to every shot's flight; the data tiles still show what the R10 measured.");
            float aim = s.aimOffsetDeg;
            if (Slider(ref y, x, w, "LAUNCH DIRECTION OFFSET  (+ = right)", ref aim, -10f, 10f, (aim > 0 ? "+" : "") + aim.ToString("0.0") + "°", 0.1f)) s.aimOffsetDeg = aim;
            float vla = s.launchAngleOffsetDeg;
            if (Slider(ref y, x, w, "LAUNCH ANGLE OFFSET  (+ = higher)", ref vla, -5f, 5f, (vla > 0 ? "+" : "") + vla.ToString("0.0") + "°", 0.1f)) s.launchAngleOffsetDeg = vla;
            if (GUI.Button(new Rect(x, y, 200f, 38f), "RESET OFFSETS", GolfSimTheme.Button))
            {
                s.aimOffsetDeg = 0f;
                s.launchAngleOffsetDeg = 0f;
                dirty = true;
            }
            y += 50f;
            return y;
        }

        // ------------------------------------------------------------ Controls

        private void Section(ref float y, float x, float w, string title)
        {
            GUI.Label(new Rect(x, y, w, 20f), title, sectionTitle);
            y += 26f;
        }

        private void Hint(ref float y, float x, float w, string text)
        {
            float h = hint.CalcHeight(new GUIContent(text), w);
            GUI.Label(new Rect(x, y - 4f, w, h), text, hint);
            y += h + 10f;
        }

        private void Switch(ref float y, float x, float w, string label, ref bool value)
        {
            GUI.Label(new Rect(x, y + 8f, w - 110f, 22f), label, sectionTitle);
            if (GUI.Button(new Rect(x + w - 96f, y, 96f, 36f), value ? "ON" : "OFF", value ? pillOn : pill))
            {
                value = !value;
                dirty = true;
            }
            y += 48f;
        }

        private bool Choice(ref float y, float x, float w, string label, string[] options, ref int value)
        {
            Section(ref y, x, w, label);
            float bw = Mathf.Min(200f, (w - (options.Length - 1) * 6f) / options.Length);
            bool changed = false;
            for (int i = 0; i < options.Length; i++)
                if (GolfSimTheme.FitButton(new Rect(x + i * (bw + 6f), y, bw, 36f), options[i], value == i ? pillOn : pill) && value != i)
                {
                    value = i;
                    changed = true;
                    dirty = true;
                }
            y += 50f;
            return changed;
        }

        private bool Slider(ref float y, float x, float w, string label, ref float value, float min, float max, string shown, float step)
        {
            Section(ref y, x, w, label);
            float sw = Mathf.Min(520f, w - 150f);
            float v = GUI.HorizontalSlider(new Rect(x + 4f, y + 10f, sw, 8f), value, min, max, sliderTrack, sliderThumb);
            if (step > 0f) v = Mathf.Round(v / step) * step;
            GUI.Box(new Rect(x + sw + 20f, y - 2f, 130f, 32f), GUIContent.none, GolfSimTheme.Card);
            GUI.Label(new Rect(x + sw + 20f, y - 2f, 130f, 32f), shown, valueBox);
            y += 46f;
            if (Mathf.Abs(v - value) < 0.0001f) return false;
            value = v;
            dirty = true;
            return true;
        }

        private void Volume(ref float y, float x, float w, string label, ref float value)
        {
            float v = value;
            if (Slider(ref y, x, w, label, ref v, 0f, 1f, Mathf.RoundToInt(v * 100f) + "%", 0.05f)) value = v;
        }
    }
}
