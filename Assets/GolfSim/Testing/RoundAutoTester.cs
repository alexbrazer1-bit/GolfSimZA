#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using GolfSimZA.Core;
using GolfSimZA.Courses;
using GolfSimZA.LaunchMonitors;
using GolfSimZA.Physics;
using GolfSimZA.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfSimZA.Testing
{
    /// <summary>
    /// EDITOR ONLY. Plays a whole round by itself (GolfSimZA → Test → Auto-play menu): picks a club
    /// for the distance, aims at the hole's target, sends launch-monitor shots, picks team balls,
    /// presses NEXT PLAYER / NEXT HOLE, and checks the round for errors, stuck states and wrong
    /// scores. Writes autoplay-report.txt next to settings.json and stops Play mode at the end.
    /// </summary>
    public sealed class RoundAutoTester : MonoBehaviour
    {
        public const string FlagKey = "GolfSimZA.AutoPlay";
        private const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private RoundGameplayUI round;
        private TestShotProvider shots;
        private readonly StringBuilder report = new StringBuilder();
        private readonly List<string> problems = new List<string>();
        private readonly StringBuilder trace = new StringBuilder();

        /// <summary>One line of the round's state (for tracking down a wrong turn order).</summary>
        private void Trace(string what)
        {
            try
            {
                string J<TT>(TT[] a) => a == null ? "-" : string.Join(",", a);
                trace.AppendLine(string.Format("{0:0.0} h{1} {2} | active {3} | strokes {4} | holed {5} | hit {6} | roundActive {7} | next {8} | pending {9}",
                    Time.unscaledTime, Get<int>("holeIndex") + 1, what, Get<int>("activePlayerIndex"), J(Get<int[]>("playerHoleStrokes")), J(Get<bool[]>("playerHoled")),
                    J(Get<bool[]>("hitThisRound")), J(Get<bool[]>("teamRoundActive")), J(Get<int[]>("teamNextHitter")), Get<int>("pendingSelection")));
            }
            catch (Exception ex) { trace.AppendLine("trace failed: " + ex.Message); }
        }
        private float nextActionAt;
        private int loggedHole = -1;
        private float waitSince;
        private bool waitingForAuto;
        private int autoAdvances;
        private bool guestTried, guestAdded, guestExistedBefore;
        private int guestHole = -1, guestIndex = -1;
        private string savedPlayers;
        private int[] savedTeams;
        private const string GuestName = "Test Guest";

        private int FirstHoleOfRound() => Mathf.Clamp(CourseSession.RoundStartHole, 0, 17);

        /// <summary>Nobody has hit yet on this hole.</summary>
        private bool holeStartsNow()
        {
            foreach (int strokes in Get<int[]>("playerHoleStrokes")) if (strokes > 0) return false;
            return true;
        }

        private void AddGuest(int hole)
        {
            guestTried = true;
            if (!round.CanAddPlayers) { report.AppendLine("  (add player test skipped: round is full)"); return; }
            int team = 0;
            if (round.JoinNeedsTeam)
            {
                team = -1;
                for (int t = 0; t < round.TeamCount; t++)
                    if (round.TeamSize(t) < round.MaxTeamSize && (team < 0 || round.TeamSize(t) < round.TeamSize(team))) team = t;
                if (team < 0) { report.AppendLine("  (add player test skipped: every team is full in " + round.FormatName + ")"); return; }
            }
            string why = round.AddPlayer(GuestName, team);
            if (why != null) { problems.Add("ADD PLAYER failed on hole " + (hole + 1) + ": " + why); return; }
            guestAdded = true;
            guestHole = hole;
            guestIndex = round.RoundPlayers.Length - 1;
            report.AppendLine("  ADD PLAYER: " + GuestName + " joined on hole " + (hole + 1) + (round.JoinNeedsTeam ? " (team " + GameFormats.TeamLetters[team] + ")" : ""));
            Trace("guest added");
        }

        /// <summary>The guest has no score before joining and a score on every hole from then on.</summary>
        private void CheckGuest()
        {
            if (!guestAdded) return;
            var card = Get<List<int[]>>("scorecard");
            int end = Mathf.Min(card.Count, 18);
            for (int h = FirstHoleOfRound(); h < end; h++)
            {
                int[] sc = card[h];
                if (sc == null) continue;
                int v = guestIndex < sc.Length ? sc[guestIndex] : 0;
                if (h < guestHole && v != 0) problems.Add("ADD PLAYER: guest has a score (" + v + ") on hole " + (h + 1) + " before joining");
                if (h >= guestHole && v <= 0) problems.Add("ADD PLAYER: guest has no score on hole " + (h + 1) + " after joining");
            }
            report.AppendLine("ADD PLAYER check done (guest joined on hole " + (guestHole + 1) + ")");
        }
        private int shotsThisHole, lastHole = -1, totalShots;
        private float stateSince;
        private string lastState = "";
        private float holeStartTime;
        private int frames;
        private float frameTime;
        private bool finished;
        private float finishAt = -1f;
        private bool savedAutoFlyover;
        private int savedScorecardSeconds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (PlayerPrefs.GetInt(FlagKey, 0) != 1) return;
            // One test per request: a stopped test never restarts by itself on the next Play.
            PlayerPrefs.SetInt(FlagKey, 0);
            PlayerPrefs.Save();
            var go = new GameObject("GolfSimZA_AutoTester");
            DontDestroyOnLoad(go);
            go.AddComponent<RoundAutoTester>();
        }

        private void Start()
        {
            Application.logMessageReceived += OnLog;
            savedPlayers = CourseSession.PlayerNames;
            savedTeams = CourseSession.Teams;
            guestExistedBefore = GolfSimZA.Players.PlayerRoster.Current.Find(GuestName) != null;
            AppSettings s = AppSettings.Current;
            savedAutoFlyover = s.autoFlyover;
            savedScorecardSeconds = s.scorecardAfterHoleSeconds;
            s.autoFlyover = false;
            s.scorecardAfterHoleSeconds = 0;
            Time.timeScale = 3f;
            report.AppendLine("GolfSim ZA automatic round test  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            report.AppendLine("Course: " + CourseSession.CourseName + "  |  format: " + CourseSession.GameMode + "  |  players: " + CourseSession.PlayerNames.Replace("|", ", ") + "  |  holes: " + CourseSession.RoundLength + "  |  auto putt " + (s.autoPutt ? "on" : "off"));
            report.AppendLine();
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            Time.timeScale = 1f;
        }

        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                string line = type + ": " + message + (string.IsNullOrEmpty(stack) ? "" : "  @ " + stack.Split('\n')[0]);
                if (problems.Count < 200) problems.Add(line);
            }
            else if ((message.StartsWith("[GolfSimZA] Hole ") && message.Contains(" green: ")) || message.StartsWith("[GolfSimZA] Auto putt:") || message.StartsWith("[GolfSimZA] Just off the green:"))
                report.AppendLine("  " + message.Substring(12));
        }

        private T Get<T>(string name)
        {
            FieldInfo f = typeof(RoundGameplayUI).GetField(name, F);
            if (f != null) return (T)f.GetValue(round);
            PropertyInfo p = typeof(RoundGameplayUI).GetProperty(name, F);
            return p != null ? (T)p.GetValue(round) : default;
        }

        private object Call(string name, params object[] args)
        {
            foreach (MethodInfo m in typeof(RoundGameplayUI).GetMethods(F))
                if (m.Name == name && m.GetParameters().Length == args.Length) return m.Invoke(round, args);
            throw new MissingMethodException(name);
        }

        private void Update()
        {
            if (finished)
            {
                if (finishAt > 0f && Time.unscaledTime > finishAt) Stop();
                return;
            }
            if (SceneManager.GetActiveScene().name != GameMenuOverlay.PlayScene && !SceneManager.GetSceneByName(GameMenuOverlay.PlayScene).isLoaded) return;
            if (round == null) round = FindFirstObjectByType<RoundGameplayUI>();
            if (shots == null) shots = FindFirstObjectByType<TestShotProvider>();
            if (round == null || shots == null || !round.enabled) return;

            frames++;
            frameTime += Time.unscaledDeltaTime;

            if (Get<bool>("waitingForCourse")) return;
            string error = Get<string>("courseError");
            if (error != null) { Fail("COURSE DID NOT LOAD: " + error); return; }

            BallFlightSimulator flight = Get<BallFlightSimulator>("ballFlightSimulator");
            FlightPresentation presentation = Get<FlightPresentation>("presentation");
            int hole = Get<int>("holeIndex");
            if (hole != lastHole)
            {
                lastHole = hole;
                shotsThisHole = 0;
                holeStartTime = Time.unscaledTime;
                frames = 0;
                frameTime = 0f;
            }

            if (Get<bool>("roundComplete"))
            {
                Finish();
                return;
            }

            bool flying = flight != null && flight.IsInFlight;
            bool flyover = presentation != null && presentation.IsFlyingOver;
            int pending = Get<int>("pendingSelection");
            bool waitingNext = Get<bool>("waitingForNextPlayer");
            bool shotFinished = Get<bool>("shotFinished");
            bool allHoled = (bool)Call("AllHoled");
            int active = Get<int>("activePlayerIndex");

            string state = hole + "|" + active + "|" + flying + "|" + pending + "|" + waitingNext + "|" + allHoled + "|" + totalShots;
            if (state != lastState) { lastState = state; stateSince = Time.unscaledTime; }
            else if (Time.unscaledTime - stateSince > 40f)
            {
                problems.Add("STUCK on hole " + (hole + 1) + " for 40 s (player " + active + ", flying " + flying + ", pending " + pending + ", waitingNext " + waitingNext + ")");
                stateSince = Time.unscaledTime;
                if (flying) return;
                PickUp(active); // get going again
                return;
            }

            // Wait until the round has finished the last shot (auto putt, penalties, next player).
            if (flying || flyover || Get<bool>("wasInFlight") || Get<bool>("awaitingFinish")) { nextActionAt = Mathf.Max(nextActionAt, Time.unscaledTime + 0.3f); return; }
            if (Time.unscaledTime < nextActionAt) return;

            if (pending >= 0)
            {
                int best = (int)Call("RecommendedBall", pending);
                Trace("choose " + best);
                Call("ChooseShot", pending, best, false, null);
                Trace("chosen");
                CheckTeamSync(pending);
                nextActionAt = Time.unscaledTime + 0.4f;
                return;
            }
            // AUTO NEXT HOLE / AUTO NEXT PLAYER: the round must move on by itself within the set time.
            AppSettings st = AppSettings.Current;
            if (allHoled)
            {
                if (loggedHole != hole) { loggedHole = hole; LogHole(hole); Trace("hole complete - waiting for auto next hole"); waitSince = Time.time; }
                if (Time.time - waitSince > st.autoNextHoleSeconds + 4f)
                {
                    problems.Add("Hole " + (hole + 1) + ": AUTO NEXT HOLE did not start the next hole (waited " + (Time.time - waitSince).ToString("0.0") + " s)");
                    Call("AdvanceHole");
                    waitSince = Time.time;
                }
                nextActionAt = Time.unscaledTime + 0.2f;
                return;
            }
            if (waitingNext && shotFinished)
            {
                if (!waitingForAuto) { waitingForAuto = true; waitSince = Time.time; Trace("waiting for auto next player"); }
                if (Time.time - waitSince > st.autoNextPlayerSeconds + 4f)
                {
                    problems.Add("Hole " + (hole + 1) + ": AUTO NEXT PLAYER did not move on (waited " + (Time.time - waitSince).ToString("0.0") + " s)");
                    Call("AdvancePlayer");
                    waitSince = Time.time;
                }
                nextActionAt = Time.unscaledTime + 0.2f;
                return;
            }
            if (waitingForAuto)
            {
                waitingForAuto = false;
                autoAdvances++;
                Trace("auto next player");
            }

            // ADD PLAYER mid round: a guest joins at the start of the round's third hole.
            if (!guestTried && hole == Get<int>("holeIndex") && hole >= FirstHoleOfRound() + 2 && holeStartsNow())
                AddGuest(hole);

            bool[] holed = Get<bool[]>("playerHoled");
            if (holed[active])
            {
                // The round should have moved on to the next player by itself.
                problems.Add("Hole " + (hole + 1) + ": player " + active + " is in the hole but still the active player (shotFinished " + shotFinished + ", waitingNext " + waitingNext + ")");
                Trace("holed but active");
                Call("AdvancePlayer");
                nextActionAt = Time.unscaledTime + 0.5f;
                if (problems.Count > 150) Fail("Too many problems - test stopped");
                return;
            }

            int[] holeStrokes = Get<int[]>("playerHoleStrokes");
            if (holeStrokes[active] >= 12) { PickUp(active); return; }

            // Team rounds: a player who already hit from the team's spot must not hit again.
            GameFormatInfo fmt = Get<GameFormatInfo>("format");
            if (fmt.Team && fmt.SelectDrive)
            {
                int team = Get<int[]>("playerTeam")[active];
                if (Get<bool[]>("teamRoundActive")[team] && Get<bool[]>("hitThisRound")[active])
                    problems.Add("Hole " + (hole + 1) + ": player " + active + " asked to hit twice in the team's round");
                if (!Get<bool[]>("teamRoundActive")[team] && fmt.AlternateAfterDrive && Get<int[]>("teamNextHitter")[team] != active)
                    problems.Add("Hole " + (hole + 1) + ": player " + active + " hits out of turn (alternate shot)");
            }
            Trace("hit");
            Hit(flight, active);
            nextActionAt = Time.unscaledTime + 0.5f;
        }

        private void Hit(BallFlightSimulator flight, int active)
        {
            Transform ball = Get<Transform>("ball");
            Vector3 pin = Get<Vector3>("pinPosition");
            Vector3 target = (Vector3)Call("AimTarget", ball.position);
            Vector3 d = target - ball.position;
            d.y = 0f;
            float toPin = Flat(pin - ball.position);
            float distance = d.magnitude;
            if (toPin < distance) { target = pin; distance = toPin; }

            bool onGreen = flight.IsOnGreen(ball.position);
            string club;
            float factor = 1f;
            if (onGreen || toPin < 12f)
            {
                club = "Putter";
            }
            else
            {
                // Shortest club that reaches, else the longest; wedges are hit softer for short shots.
                club = null;
                float bestCarry = float.MaxValue, longest = 0f;
                string longestClub = null;
                foreach (ActiveClub.BagClub c in ActiveClub.Bag)
                {
                    if (ActiveClub.IsPutter(c.Name)) continue;
                    float carry = ActiveClub.ExpectedCarry(c.Name);
                    if (carry > longest) { longest = carry; longestClub = c.Name; }
                    if (carry >= distance * 0.95f && carry < bestCarry) { bestCarry = carry; club = c.Name; }
                }
                if (club == null) club = longestClub;
                float expected = ActiveClub.ExpectedCarry(club);
                if (distance < expected) factor = Mathf.Clamp(Mathf.Pow(distance / expected, 0.75f), 0.25f, 1f);
            }
            ActiveClub.Select(club);

            AimPointer aim = Get<AimPointer>("aimPointer");
            if (aim != null)
            {
                aim.Set(ball.position, target);
                Call("OnAimMoved", aim.Point);
            }

            ShotData shot = TestShotProvider.BuildShot(club, ActiveClub.LoftOf(club));
            if (ActiveClub.IsPutter(club))
            {
                // Roughly the speed that rolls the ball to the hole on a normal green.
                shot.BallSpeedMps = Mathf.Clamp(Mathf.Sqrt(2f * 0.65f * Mathf.Max(0.3f, toPin)) * 1.05f, 0.6f, 12f);
                shot.LaunchDirectionDeg = UnityEngine.Random.Range(-1f, 1f);
            }
            else
            {
                shot.BallSpeedMps *= factor;
                shot.ClubSpeedMps *= factor;
            }
            shotsThisHole++;
            totalShots++;
            shots.Inject(shot);
        }

        private void PickUp(int p)
        {
            Call("AddStrokes", p, 1);
            Call("MarkHoled", p, "PICKED UP (auto test)");
            problems.Add("Picked up player " + p + " on hole " + (Get<int>("holeIndex") + 1) + " (too many shots or stuck)");
            GameFormatInfo format = Get<GameFormatInfo>("format");
            if (format.Team && format.SelectDrive)
            {
                int t = Get<int[]>("playerTeam")[p];
                Call("SyncTeam", t, p);
                Get<bool[]>("teamRoundActive")[t] = false;
            }
            typeof(RoundGameplayUI).GetField("shotFinished", F).SetValue(round, true);
            Call("ContinueAfterShot");
            nextActionAt = Time.unscaledTime + 0.5f;
        }

        private void CheckTeamSync(int team)
        {
            GameFormatInfo format = Get<GameFormatInfo>("format");
            List<int>[] members = Get<List<int>[]>("teamMembers");
            int[] strokes = Get<int[]>("playerHoleStrokes");
            Vector3[] positions = Get<Vector3[]>("playerPositions");
            int first = members[team][0];
            foreach (int m in members[team])
                if (strokes[m] != strokes[first] || Flat(positions[m] - positions[first]) > 0.01f)
                    problems.Add("Team " + GameFormats.TeamLetters[team] + " not in step after choosing a ball (" + format.Name + ")");
        }

        private void LogHole(int hole)
        {
            string[] names = Get<string[]>("playerNames");
            int[] strokes = Get<int[]>("playerHoleStrokes");
            int par = (int)Call("ParForHole", hole);
            var parts = new List<string>();
            for (int i = 0; i < names.Length; i++)
            {
                parts.Add(names[i] + " " + strokes[i]);
                if (strokes[i] <= 0) problems.Add("Hole " + (hole + 1) + ": " + names[i] + " finished with " + strokes[i] + " strokes");
            }
            float seconds = Time.unscaledTime - holeStartTime;
            float fps = frameTime > 0f ? frames / frameTime : 0f;
            string status = Call("FormatStatusText") as string;
            try { File.WriteAllText(Path.Combine(Application.persistentDataPath, "autoplay-trace.txt"), trace.ToString()); } catch (Exception) { }
            report.AppendLine(string.Format("Hole {0,2}  par {1}  |  {2}  |  {3} shots  |  {4:0}s  |  {5:0} fps{6}",
                hole + 1, par, string.Join(", ", parts), shotsThisHole, seconds, fps, string.IsNullOrEmpty(status) ? "" : "  |  " + status));
        }

        private void Finish()
        {
            finished = true;
            CheckGuest();
            report.AppendLine("Auto next player moves: " + autoAdvances);
            report.AppendLine();
            report.AppendLine("Result: " + (Call("ResultText") as string));
            report.AppendLine("Total shots hit: " + totalShots);
            report.AppendLine();
            report.AppendLine(problems.Count == 0 ? "NO PROBLEMS FOUND" : problems.Count + " PROBLEM(S):");
            foreach (string p in problems) report.AppendLine("  - " + p);
            Write();
            finishAt = Time.unscaledTime + 4f;
        }

        private void Fail(string why)
        {
            problems.Add(why);
            Finish();
        }

        private void Write()
        {
            string path = Path.Combine(Application.persistentDataPath, "autoplay-report.txt");
            try
            {
                File.WriteAllText(path, report.ToString());
                File.WriteAllText(Path.Combine(Application.persistentDataPath, "autoplay-trace.txt"), trace.ToString());
                File.AppendAllText(Path.Combine(Application.persistentDataPath, "autoplay-all.txt"), report + "\n==========================================\n\n");
                // A copy in the project's Logs folder as well, so it is easy to find.
                string logs = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs");
                Directory.CreateDirectory(logs);
                File.WriteAllText(Path.Combine(logs, "autoplay-report.txt"), report.ToString());
                File.WriteAllText(Path.Combine(logs, "autoplay-trace.txt"), trace.ToString());
            } catch (Exception ex) { Debug.LogWarning(ex.Message); }
            Debug.Log("[GolfSimZA] Auto-play finished:\n" + report);
        }

        private void Stop()
        {
            AppSettings s = AppSettings.Current;
            s.autoFlyover = savedAutoFlyover;
            s.scorecardAfterHoleSeconds = savedScorecardSeconds;
            s.Save();
            // The guest was only for the test: the round's players and the roster go back as they were.
            if (savedPlayers != null) CourseSession.SetPlayers(savedPlayers.Split('|'));
            CourseSession.Teams = savedTeams;
            var roster = GolfSimZA.Players.PlayerRoster.Current;
            var guest = roster.Find(GuestName);
            if (guest != null && !guestExistedBefore) { roster.players.Remove(guest); roster.Save(); CourseSession.SetPlayers(savedPlayers.Split('|')); }
            PlayerPrefs.SetInt(FlagKey, 0);
            PlayerPrefs.Save();
            Time.timeScale = 1f;
            UnityEditor.EditorApplication.isPlaying = false;
            Destroy(gameObject);
        }

        private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }
    }
}
#endif
