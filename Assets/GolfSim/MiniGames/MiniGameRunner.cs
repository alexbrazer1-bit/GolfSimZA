using System;
using System.Collections.Generic;
using GolfSimZA.Core;
using GolfSimZA.Physics;
using GolfSimZA.Players;
using GolfSimZA.UI;
using UnityEngine;

namespace GolfSimZA.MiniGames
{
    /// <summary>
    /// Runs the mini game picked on the MINI GAMES tab on the driving range: builds the game,
    /// takes turns (the club bar follows the player on the mat), scores every shot when the ball
    /// stops, shows the result banner, the scoreboard (left panel), labels over the targets and
    /// the final results with PLAY AGAIN / NEW GAME / HOME.
    /// Created by the range screen (ModernGolfSimUI) when a mini game session is active.
    /// </summary>
    public sealed class MiniGameRunner : MonoBehaviour
    {
        /// <summary>Seconds the shot result stays up before the next player is called.</summary>
        public const float TurnDelay = 2.8f;

        public static MiniGameRunner Current { get; private set; }

        public readonly List<MiniPlayer> Players = new List<MiniPlayer>();
        public MiniGame Game { get; private set; }
        public MiniGameInfo Info { get; private set; }
        public int Turn { get; private set; }
        public bool Finished { get; private set; }
        /// <summary>The ball being scored finished in the cup (closest to the pin).</summary>
        public bool BallHoled { get; private set; }
        /// <summary>Something on the range changed - the map picture needs redrawing.</summary>
        public bool MapDirty { get; set; }
        /// <summary>Results are showing - the range must not take clicks.</summary>
        public bool BlocksInput => Finished;

        /// <summary>PLAY AGAIN rebuilt the game (the range clears its shot list).</summary>
        public event Action Restarted;

        public MiniPlayer CurrentPlayer => Turn >= 0 && Turn < Players.Count ? Players[Turn] : null;

        private RangeEnvironment range;
        private Transform root;
        private int shooter = -1;
        private string banner;
        private float bannerUntil;
        private bool bannerGood;
        private MiniPlayer bannerWho;
        private string announce;
        private float announceUntil;
        private float nextTurnAt = -1f, finishAt = -1f;
        private bool showRules;
        private Vector2 boardScroll;
        private GUIStyle label, shadow, bannerText, bannerSub, rowName, rowScore, rowMuted, head, caption, bigTitle, rulesText;
        private Texture2D dot, bar, dim, panel, rowTex, rowActive;
        private readonly List<Rect> labelRects = new List<Rect>();

        // ------------------------------------------------------------ Set-up

        public static MiniGame Create(MiniGameId id)
        {
            switch (id)
            {
                case MiniGameId.LongDrive: return new LongDriveGame();
                case MiniGameId.TargetRings: return new TargetRingsGame();
                case MiniGameId.EdgeKnockout: return new EdgeKnockoutGame();
                case MiniGameId.CaptureFlags: return new CaptureFlagsGame();
                case MiniGameId.JunkyardBlast: return new JunkyardBlastGame();
                case MiniGameId.CreaturePond: return new CreaturePondGame();
                case MiniGameId.DesertOasis: return new DesertOasisGame();
                default: return new ClosestToPinGame();
            }
        }

        /// <summary>Starts the chosen game on this range with the chosen players.</summary>
        public void Begin(RangeEnvironment rangeEnvironment)
        {
            Current = this;
            range = rangeEnvironment;
            Info = MiniGameCatalog.Get(MiniGameSession.Game);
            Players.Clear();
            var used = new List<Color>();
            foreach (string raw in MiniGameSession.Players)
            {
                string name = PlayerRoster.Clean(raw);
                if (string.IsNullOrEmpty(name) || Players.Exists(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))) continue;
                Color c = PlayerRoster.Current.Find(name) != null ? PlayerRoster.ColorFor(name) : FreeColour(used);
                if (used.Exists(u => Close(u, c))) c = FreeColour(used);
                used.Add(c);
                Players.Add(new MiniPlayer { Name = name, Color = c });
                if (Players.Count >= Info.MaxPlayers) break;
            }
            if (Players.Count == 0) Players.Add(new MiniPlayer { Name = "Player 1", Color = PlayerRoster.Colors[0] });
            Debug.Log("[GolfSimZA] Mini game: " + Info.Name + " • " + Players.Count + " player(s) • " + (MiniGameSession.Explore && Info.HasExplore ? "explore" : MiniGameSession.Shots + " shots"));
            StartGame();
        }

        private static bool Close(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.2f;

        private static Color FreeColour(List<Color> used)
        {
            foreach (Color c in PlayerRoster.Colors)
                if (!used.Exists(u => Close(u, c))) return c;
            return PlayerRoster.Colors[used.Count % PlayerRoster.Colors.Length];
        }

        /// <summary>(Re)builds the game - PLAY AGAIN keeps the players and options.</summary>
        public void StartGame()
        {
            Game?.Clear();
            StopAllCoroutines();
            if (root != null) Destroy(root.gameObject);
            root = new GameObject("GolfSimZA_MiniGame").transform;

            for (int i = 0; i < Players.Count; i++)
                Players[i] = new MiniPlayer { Name = Players[i].Name, Color = Players[i].Color };

            Game = Create(Info.Id);
            Game.Info = Info;
            Game.Runner = this;
            Game.Range = range;
            Game.Root = root;
            Game.Explore = Info.HasExplore && MiniGameSession.Explore;
            Game.ShotsPerPlayer = Game.Explore ? 9999 : MiniGameSession.Shots;
            Finished = false;
            nextTurnAt = finishAt = -1f;
            banner = announce = null;
            shooter = -1;
            Game.Build();
            MapDirty = true;
            SetTurn(0);
            Announce(Info.Name);
            Restarted?.Invoke();
        }

        private void SetTurn(int index)
        {
            Turn = Mathf.Clamp(index, 0, Players.Count - 1);
            ActiveClub.Player = Players[Turn].Name;
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
            if (root != null) Destroy(root.gameObject);
            // (The session ends on the home screen - a scene reload for the next game must keep it.)
        }

        private void OnApplicationQuit() => MiniGameSession.End();

        // ------------------------------------------------------------ Called by the game pieces

        public void MapChanged() => MapDirty = true;

        /// <summary>Big message in the middle of the screen (round result, new barrels ...).</summary>
        public void Announce(string text)
        {
            announce = text;
            announceUntil = Time.unscaledTime + 3f;
        }

        // ------------------------------------------------------------ Shots

        /// <summary>A shot is about to fly: it belongs to the player whose turn it is.</summary>
        public void OnLaunch()
        {
            if (Finished || finishAt > 0f) { shooter = -1; return; }
            if (nextTurnAt > 0f) Advance();
            shooter = Finished || finishAt > 0f ? -1 : Turn;
            banner = null;
        }

        /// <summary>The ball stopped: score it for the player who hit it.</summary>
        public void OnShotFinished(ShotData shot, Vector3 landing, Vector3 rest, bool holed)
        {
            if (Game == null || shooter < 0 || shooter >= Players.Count) return;
            MiniPlayer p = Players[shooter];
            shooter = -1;
            BallHoled = holed;
            ShotOutcome outcome = Game.Score(p, landing, rest, shot);
            p.Shots++;
            p.Last = outcome.Text;
            banner = outcome.Text;
            bannerWho = p;
            bannerGood = outcome.Good;
            bannerUntil = Time.unscaledTime + TurnDelay + 0.6f;
            nextTurnAt = Time.unscaledTime + TurnDelay;
            Debug.Log("[GolfSimZA] Mini game shot: " + p.Name + " • " + outcome.Text + " • landing " + landing.ToString("0.0") + " rest " + rest.ToString("0.0"));
        }

        private void Advance()
        {
            nextTurnAt = -1f;
            int next = Game.NextPlayer(Turn);
            if (next < 0)
            {
                finishAt = Time.unscaledTime + (announce != null && Time.unscaledTime < announceUntil ? 2.6f : 0.4f);
                return;
            }
            SetTurn(next);
        }

        /// <summary>END GAME / FINISH: straight to the results.</summary>
        public void FinishNow()
        {
            nextTurnAt = -1f;
            finishAt = Time.unscaledTime;
        }

        private void Update()
        {
            if (Game == null) return;
            Game.Tick();
            float now = Time.unscaledTime;
            if (nextTurnAt > 0f && now >= nextTurnAt && !GameMenuOverlay.IsOpen) Advance();
            if (finishAt > 0f && now >= finishAt)
            {
                finishAt = -1f;
                Finished = true;
                GolfSimAudio.PlayWin();
                Debug.Log("[GolfSimZA] Mini game over: " + Info.Name + " • " + string.Join(", ", Ranked().ConvertAll(p => p.Name + " " + Game.ScoreText(p))));
            }
        }

        /// <summary>Players best first.</summary>
        public List<MiniPlayer> Ranked()
        {
            var list = new List<MiniPlayer>(Players);
            bool low = Game != null && Game.LowerIsBetter;
            list.Sort((a, b) =>
            {
                float ka = Game.RankKey(a), kb = Game.RankKey(b);
                int c = low ? ka.CompareTo(kb) : kb.CompareTo(ka);
                return c != 0 ? c : Players.IndexOf(a).CompareTo(Players.IndexOf(b));
            });
            return list;
        }

        private int RankOf(List<MiniPlayer> ranked, MiniPlayer p)
        {
            int i = ranked.IndexOf(p);
            while (i > 0 && Mathf.Approximately(Game.RankKey(ranked[i - 1]), Game.RankKey(p))) i--;
            return i + 1;
        }

        // ------------------------------------------------------------ GUI

        private void EnsureStyles()
        {
            GolfSimTheme.Ensure();
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow };
            shadow = new GUIStyle(label) { normal = { textColor = new Color(0f, 0f, 0f, 0.75f) } };
            bannerText = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true, normal = { textColor = Color.white } };
            bannerSub = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 1f, 1f, 0.8f) } };
            rowName = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, normal = { textColor = Color.white } };
            rowScore = new GUIStyle(rowName) { fontSize = 17, alignment = TextAnchor.MiddleRight };
            rowMuted = new GUIStyle(rowName) { fontSize = 11, fontStyle = FontStyle.Normal, normal = { textColor = new Color(0.75f, 0.8f, 0.85f) } };
            head = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, normal = { textColor = Color.white } };
            caption = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(1f, 0.55f, 0.18f) } };
            bigTitle = new GUIStyle(GolfSimTheme.Title) { fontSize = 30, alignment = TextAnchor.MiddleCenter };
            rulesText = new GUIStyle(GolfSimTheme.Body) { wordWrap = true, fontSize = 13 };
            dot = GolfSimTheme.Rounded(Color.white, 32);
            bar = GolfSimTheme.Tex(Color.white);
            dim = GolfSimTheme.Tex(new Color(0f, 0.02f, 0.03f, 0.7f));
            panel = GolfSimTheme.Rounded(new Color(0.04f, 0.05f, 0.07f, 0.94f), 8);
            rowTex = GolfSimTheme.Rounded(new Color(0.12f, 0.15f, 0.19f, 0.95f), 6);
            rowActive = GolfSimTheme.Rounded(new Color(0.10f, 0.22f, 0.34f, 0.98f), 6, new Color(0.25f, 0.62f, 1f));
        }

        private void Panel(Rect r, Texture2D tex)
        {
            GUI.Box(r, GUIContent.none, new GUIStyle(GUI.skin.box) { border = new RectOffset(8, 8, 8, 8), normal = { background = tex } });
        }

        /// <summary>Text over a point in the world (with a shadow), if it is on screen.</summary>
        public void WorldLabel(Camera cam, Vector3 world, string text, Color colour, int size)
        {
            if (cam == null || string.IsNullOrEmpty(text)) return;
            Vector3 s = cam.WorldToScreenPoint(world);
            if (s.z < 1f || s.x < -60f || s.x > Screen.width + 60f || s.y < -30f || s.y > Screen.height + 30f) return;
            EnsureStyles();
            label.fontSize = shadow.fontSize = size;
            label.normal.textColor = colour;
            // Labels never pile on top of each other: one that would overlap an earlier one is skipped.
            Vector2 size2 = label.CalcSize(new GUIContent(text));
            Rect used = new Rect(s.x - size2.x * 0.5f - 3f, Screen.height - s.y - size2.y * 0.5f - 1f, size2.x + 6f, size2.y + 2f);
            foreach (Rect o in labelRects) if (o.Overlaps(used)) return;
            labelRects.Add(used);
            Rect r = new Rect(s.x - 100f, Screen.height - s.y - 12f, 200f, 24f);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, shadow);
            GUI.Label(r, text, label);
        }

        private void OnGUI()
        {
            if (Game == null) return;
            EnsureStyles();
            AppSettings settings = AppSettings.Current;
            BallFlightSimulator flight = FindFlight();
            bool flying = flight != null && flight.IsInFlight;
            if (flying && settings.hideUiOnShot) return;

            GUI.depth = 5; // under the game menu
            labelRects.Clear();
            if (!Finished) Game.DrawLabels(Camera.main);

            float m = Mathf.Clamp(Screen.width * 0.015f, 10f, 24f);
            float now = Time.unscaledTime;
            // Between the scoreboard (left) and the map (right) so nothing is covered.
            float laneL = m + 292f + 10f, laneR = Screen.width - m - Mathf.Clamp(Screen.width * 0.2f, 220f, 320f) - 10f;
            float laneC = (laneL + laneR) * 0.5f, laneW = Mathf.Max(300f, laneR - laneL);
            if (!Finished)
            {
                // Shot result, or whose turn it is.
                float bw0 = Mathf.Min(660f, laneW);
                Rect b = new Rect(laneC - bw0 * 0.5f, m + 66f, bw0, 62f);
                if (banner != null && now < bannerUntil)
                {
                    Panel(b, panel);
                    MiniPlayer who = bannerWho ?? CurrentPlayer;
                    GUI.color = bannerGood ? GolfSimTheme.Good : GolfSimTheme.Bad;
                    GUI.DrawTexture(new Rect(b.x, b.y, b.width, 4f), bar);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(b.x + 10f, b.y + 4f, b.width - 20f, 18f), who != null ? who.Name.ToUpperInvariant() : "", bannerSub);
                    GUI.Label(new Rect(b.x + 10f, b.y + 20f, b.width - 20f, 40f), banner, new GUIStyle(bannerText) { fontSize = 21 });
                }
                else if (CurrentPlayer != null && !flying)
                {
                    float tw0 = Mathf.Min(460f, laneW);
                    Rect t = new Rect(laneC - tw0 * 0.5f, m + 66f, tw0, 48f);
                    Panel(t, panel);
                    GUI.color = CurrentPlayer.Color;
                    GUI.DrawTexture(new Rect(t.x + 14f, t.y + 12f, 24f, 24f), dot);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(t.x + 46f, t.y, t.width - 56f, 26f), CurrentPlayer.Name.ToUpperInvariant() + "  TO HIT", new GUIStyle(head) { fontSize = 18 });
                    GUI.Label(new Rect(t.x + 46f, t.y + 24f, t.width - 56f, 20f), Game.ShotLabel(CurrentPlayer) + "   •   " + Info.Name, new GUIStyle(rowMuted) { fontSize = 12 });
                }

                if (announce != null && now < announceUntil)
                {
                    float a = Mathf.Clamp01((announceUntil - now) / 0.4f);
                    float aw0 = Mathf.Min(720f, laneW);
                    Rect ar = new Rect(laneC - aw0 * 0.5f, Screen.height * 0.30f, aw0, 70f);
                    GUI.color = new Color(1f, 1f, 1f, a);
                    Panel(ar, panel);
                    GUI.color = new Color(Info.Accent.r, Info.Accent.g, Info.Accent.b, a);
                    GUI.DrawTexture(new Rect(ar.x, ar.y, ar.width, 4f), bar);
                    GUI.color = new Color(1f, 1f, 1f, a);
                    GUI.Label(ar, announce, new GUIStyle(bannerText) { fontSize = 28, normal = { textColor = GolfSimTheme.Gold } });
                    GUI.color = Color.white;
                }
            }
            else
            {
                DrawResults();
            }
        }

        private BallFlightSimulator flightCache;
        private BallFlightSimulator FindFlight()
        {
            if (flightCache == null) flightCache = FindFirstObjectByType<BallFlightSimulator>();
            return flightCache;
        }

        /// <summary>Left panel on the range: game, status, every player's score and the last shots.</summary>
        public void DrawScoreboard(Rect r)
        {
            if (Game == null || r.height < 140f) return;
            EnsureStyles();
            Panel(r, panel);
            GUI.color = Info.Accent;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 4f), bar);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 10f, r.y + 8f, r.width - 20f, 24f), Info.Name, new GUIStyle(head) { normal = { textColor = Info.Accent } });
            GUI.Label(new Rect(r.x + 10f, r.y + 32f, r.width - 20f, 16f), (Game.Explore ? "EXPLORE  •  " : "") + Game.StatusLine(), new GUIStyle(rowMuted) { fontSize = 11 });

            // Rules overlay replaces the list while open.
            float footY = r.yMax - 38f;
            if (showRules)
            {
                Rect body = new Rect(r.x + 10f, r.y + 54f, r.width - 20f, footY - r.y - 60f);
                GUI.Label(new Rect(body.x, body.y, body.width, 16f), "HOW TO PLAY", caption);
                float h1 = rulesText.CalcHeight(new GUIContent(Info.HowToPlay), body.width);
                GUI.Label(new Rect(body.x, body.y + 16f, body.width, h1), Info.HowToPlay, rulesText);
                float y2 = body.y + 22f + h1;
                GUI.Label(new Rect(body.x, y2, body.width, 16f), "SCORING", caption);
                GUI.Label(new Rect(body.x, y2 + 16f, body.width, body.yMax - y2 - 16f), Info.Scoring, rulesText);
            }
            else
            {
                GUI.Label(new Rect(r.x + 10f, r.y + 52f, 100f, 14f), "PLAYER", caption);
                GUI.Label(new Rect(r.xMax - 170f, r.y + 52f, 160f, 14f), Game.ScoreCaption, new GUIStyle(caption) { alignment = TextAnchor.MiddleRight });
                List<MiniPlayer> ranked = Ranked();
                const float rowH = 40f;
                Rect list = new Rect(r.x + 6f, r.y + 68f, r.width - 12f, Mathf.Max(60f, footY - r.y - 68f - 64f));
                Rect content = new Rect(0, 0, list.width - (Players.Count * (rowH + 4f) > list.height ? 16f : 0f), Mathf.Max(list.height, Players.Count * (rowH + 4f)));
                boardScroll = GUI.BeginScrollView(list, boardScroll, content);
                for (int i = 0; i < Players.Count; i++)
                {
                    MiniPlayer p = Players[i];
                    Rect row = new Rect(0, i * (rowH + 4f), content.width, rowH);
                    Panel(row, i == Turn ? rowActive : rowTex);
                    GUI.Label(new Rect(row.x + 6f, row.y, 22f, rowH), RankOf(ranked, p).ToString(), new GUIStyle(rowMuted) { fontSize = 12, alignment = TextAnchor.MiddleCenter });
                    GUI.color = p.Out ? new Color(p.Color.r, p.Color.g, p.Color.b, 0.35f) : p.Color;
                    GUI.DrawTexture(new Rect(row.x + 30f, row.y + 12f, 16f, 16f), dot);
                    GUI.color = Color.white;
                    string shots = Game.Explore ? p.Shots + " SHOTS" : Info.Id == MiniGameId.EdgeKnockout ? (p.Out ? "KNOCKED OUT" : "STILL IN") : p.Shots + " / " + Game.ShotsPerPlayer;
                    float nameW = row.width - 54f - 90f;
                    GUI.Label(new Rect(row.x + 52f, row.y + 3f, nameW, 20f), GolfSimTheme.Ellipsize(p.Name.ToUpperInvariant(), rowName, nameW), new GUIStyle(rowName) { normal = { textColor = p.Out ? new Color(1f, 1f, 1f, 0.45f) : Color.white } });
                    GUI.Label(new Rect(row.x + 52f, row.y + 21f, nameW, 16f), shots, rowMuted);
                    GUI.Label(new Rect(row.xMax - 96f, row.y, 88f, rowH), Game.ScoreText(p), rowScore);
                }
                GUI.EndScrollView();

                // Last shots of the player up (newest first).
                MiniPlayer cur = CurrentPlayer;
                float ly = list.yMax + 6f;
                GUI.Label(new Rect(r.x + 10f, ly, r.width - 20f, 14f), cur != null ? "LAST SHOTS  •  " + cur.Name.ToUpperInvariant() : "LAST SHOTS", caption);
                if (cur != null)
                {
                    var parts = new List<string>();
                    for (int i = cur.Log.Count - 1; i >= 0 && parts.Count < 6; i--) parts.Add(cur.Log[i]);
                    string text = parts.Count > 0 ? string.Join("   ", parts) : "—";
                    GUI.Label(new Rect(r.x + 10f, ly + 16f, r.width - 20f, 20f), GolfSimTheme.Ellipsize(text, rowName, r.width - 20f), new GUIStyle(rowName) { fontSize = 13 });
                    GUI.Label(new Rect(r.x + 10f, ly + 36f, r.width - 20f, 18f), GolfSimTheme.Ellipsize(cur.Last ?? "", rowMuted, r.width - 20f), rowMuted);
                }
            }

            float bw = (r.width - 26f) * 0.5f;
            if (GUI.Button(new Rect(r.x + 8f, footY, bw, 30f), showRules ? "SCORES" : "RULES", GolfSimTheme.SmallButton)) showRules = !showRules;
            if (GUI.Button(new Rect(r.x + 18f + bw, footY, bw, 30f), Game.Explore ? "FINISH" : "END GAME", GolfSimTheme.SmallButton)) FinishNow();
        }

        private void DrawResults()
        {
            GUI.depth = -50;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), dim);
            List<MiniPlayer> ranked = Ranked();
            bool collection = Game.HasCollection;
            float rowH = collection ? 58f : 48f;
            float w = Mathf.Min(760f, Screen.width - 40f);
            float h = Mathf.Min(Screen.height - 40f, 150f + ranked.Count * (rowH + 6f) + 80f);
            Rect box = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.Box(box, GUIContent.none, GolfSimTheme.Overlay);
            GUI.color = Info.Accent;
            GUI.DrawTexture(new Rect(box.x, box.y, box.width, 5f), bar);
            GUI.color = Color.white;
            GUI.Label(new Rect(box.x, box.y + 16f, box.width, 36f), Info.Name + "  •  RESULTS", bigTitle);

            string headline;
            if (ranked.Count == 1) headline = "YOUR SCORE  " + Game.ScoreText(ranked[0]) + (Game.ScoreCaption.Length > 0 ? "  " + Game.ScoreCaption : "");
            else if (ranked.Count > 1 && Mathf.Approximately(Game.RankKey(ranked[0]), Game.RankKey(ranked[1]))) headline = "IT'S A TIE!";
            else headline = ranked[0].Name.ToUpperInvariant() + " WINS!";
            GUI.Label(new Rect(box.x, box.y + 56f, box.width, 32f), headline, new GUIStyle(bannerText) { fontSize = 24, normal = { textColor = GolfSimTheme.Gold } });

            Color[] medal = { new Color(1f, 0.82f, 0.25f), new Color(0.80f, 0.84f, 0.88f), new Color(0.85f, 0.55f, 0.30f) };
            float y = box.y + 100f;
            float listBottom = box.yMax - 80f;
            for (int i = 0; i < ranked.Count && y + rowH <= listBottom + 1f; i++)
            {
                MiniPlayer p = ranked[i];
                int rank = RankOf(ranked, p);
                Rect row = new Rect(box.x + 24f, y, box.width - 48f, rowH);
                Panel(row, i == 0 ? rowActive : rowTex);
                GUI.color = rank <= 3 ? medal[rank - 1] : new Color(1f, 1f, 1f, 0.3f);
                GUI.DrawTexture(new Rect(row.x + 10f, row.y + rowH * 0.5f - 15f, 30f, 30f), dot);
                GUI.color = Color.white;
                GUI.Label(new Rect(row.x + 10f, row.y + rowH * 0.5f - 15f, 30f, 30f), rank.ToString(), new GUIStyle(rowName) { alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.05f, 0.05f, 0.05f) } });
                GUI.color = p.Color;
                GUI.DrawTexture(new Rect(row.x + 52f, row.y + rowH * 0.5f - 8f, 16f, 16f), dot);
                GUI.color = Color.white;
                float nameW = row.width - 80f - 170f;
                GUI.Label(new Rect(row.x + 76f, row.y + 4f, nameW, 24f), GolfSimTheme.Ellipsize(p.Name.ToUpperInvariant(), rowName, nameW), new GUIStyle(rowName) { fontSize = 17 });
                string detail = collection ? Game.Collection(p) : p.Shots + (p.Shots == 1 ? " SHOT" : " SHOTS") + (p.Log.Count > 0 ? "   •   " + string.Join("  ", p.Log) : "");
                GUI.Label(new Rect(row.x + 76f, row.y + 26f, nameW, 20f), GolfSimTheme.Ellipsize(detail, rowMuted, nameW), rowMuted);
                GUI.Label(new Rect(row.xMax - 170f, row.y, 156f, rowH * 0.62f), Game.ScoreText(p), new GUIStyle(rowScore) { fontSize = 22 });
                GUI.Label(new Rect(row.xMax - 170f, row.y + rowH * 0.55f, 156f, 16f), Game.ScoreCaption, new GUIStyle(caption) { alignment = TextAnchor.MiddleRight });
                y += rowH + 6f;
            }

            float bw = (box.width - 48f - 20f) / 3f, by = box.yMax - 64f;
            if (GUI.Button(new Rect(box.x + 24f, by, bw, 46f), "PLAY AGAIN", GolfSimTheme.AccentButton)) StartGame();
            if (GUI.Button(new Rect(box.x + 34f + bw, by, bw, 46f), "NEW GAME", GolfSimTheme.Button)) CourseSelectionUI.OpenHome("MiniGames");
            if (GUI.Button(new Rect(box.x + 44f + bw * 2f, by, bw, 46f), "HOME", GolfSimTheme.Button)) CourseSelectionUI.OpenHome("Home");

            if (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp)
                Event.current.Use();
        }
    }
}
