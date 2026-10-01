#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using GolfSimZA.Core;
using GolfSimZA.LaunchMonitors;
using GolfSimZA.MiniGames;
using GolfSimZA.Physics;
using GolfSimZA.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfSimZA.Testing
{
    /// <summary>
    /// EDITOR ONLY. Plays every mini game by itself (GolfSimZA → Test → Mini games): for each game in
    /// the queue it aims at the game's targets, sends launch-monitor shots for the player up, waits
    /// for the turns, checks the scoring and the results screen (and PLAY AGAIN once), and takes a
    /// picture during play and of the results. Writes minigames-report.txt (next to settings.json
    /// and in the project's Logs folder, with the pictures in Logs/minigames) and stops Play mode.
    /// </summary>
    public sealed class MiniGameAutoTester : MonoBehaviour
    {
        public const string FlagKey = "GolfSimZA.MiniGameTest";
        public const string QueueKey = "GolfSimZA.MiniGameTestQueue";
        private const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private readonly StringBuilder report = new StringBuilder();
        private readonly List<string> problems = new List<string>();
        private readonly Dictionary<string, float> carryRatio = new Dictionary<string, float>();
        private List<string> queue;
        private string item;
        private int itemIndex;
        private MiniGameRunner runner;
        private ModernGolfSimUI range;
        private TestShotProvider shots;
        private BallFlightSimulator flight;
        private float nextShotAt, itemStart, lastProgress, stoppedAt = -1f;
        private int shotsSent, shotsScored, lastScoredTotal;
        private string lastClub;
        private float lastExpected;
        private bool fallPicture, mapPicture;
        private bool resultsSeen, playAgainDone, testPlayAgain, pictureTaken, resultsPicture;
        private int exploreLimit;
        private float resultsAt = -1f;
        private string logsDir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (PlayerPrefs.GetInt(FlagKey, 0) != 1) return;
            PlayerPrefs.SetInt(FlagKey, 0);
            PlayerPrefs.Save();
            var go = new GameObject("GolfSimZA_MiniGameTester");
            DontDestroyOnLoad(go);
            go.AddComponent<MiniGameAutoTester>();
        }

        private void Start()
        {
            Application.logMessageReceived += OnLog;
            queue = new List<string>(PlayerPrefs.GetString(QueueKey, "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            logsDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "minigames");
            Directory.CreateDirectory(logsDir);
            report.AppendLine("GolfSim ZA automatic mini games test  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            report.AppendLine(queue.Count + " games in the queue");
            report.AppendLine();
            Time.timeScale = 2f;
            BeginItem(0, false);
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
                if (problems.Count < 200) problems.Add((item ?? "") + " • " + type + ": " + message + (string.IsNullOrEmpty(stack) ? "" : "  @ " + stack.Split('\n')[0]));
            }
            else if (message.StartsWith("[GolfSimZA] Mini game"))
            {
                report.AppendLine("  " + message.Substring(12));
                if (!fallPicture && runner != null && (message.Contains("CANYON") || message.Contains("SIDE OF THE CLIFF")))
                {
                    fallPicture = true;
                    Picture("fall");
                }
            }
        }

        /// <summary>Queue item: game id : shots : explore (0/1) : distance : players (comma list) [: again].</summary>
        private void BeginItem(int index, bool reload)
        {
            itemIndex = index;
            if (index >= queue.Count) { Finish(); return; }
            item = queue[index];
            string[] f = item.Split(':');
            var id = (MiniGameId)int.Parse(f[0]);
            int shotsEach = int.Parse(f[1]);
            bool explore = f[2] == "1";
            int distance = int.Parse(f[3]);
            string[] players = f[4].Split(',');
            testPlayAgain = f.Length > 5 && f[5] == "again";
            exploreLimit = explore ? 5 : 0;
            MiniGameSession.Start(id, players, shotsEach, explore, distance);
            report.AppendLine("=== " + MiniGameCatalog.Get(id).Name + "  •  " + players.Length + " player(s)  •  " + (explore ? "EXPLORE" : shotsEach + " shots") + (MiniGameCatalog.Get(id).HasDistance ? "  •  distance " + distance : "") + (testPlayAgain ? "  •  + PLAY AGAIN" : ""));
            runner = null;
            range = null;
            shots = null;
            flight = null;
            shotsSent = shotsScored = lastScoredTotal = 0;
            resultsSeen = playAgainDone = pictureTaken = resultsPicture = mapPicture = false;
            resultsAt = -1f;
            itemStart = lastProgress = Time.unscaledTime;
            nextShotAt = Time.unscaledTime + 3f;
            if (reload) SceneManager.LoadScene(GameMenuOverlay.PlayScene);
        }

        private void Update()
        {
            if (item == null) return;
            if (runner == null) runner = MiniGameRunner.Current;
            if (range == null) range = FindFirstObjectByType<ModernGolfSimUI>();
            if (shots == null) shots = FindFirstObjectByType<TestShotProvider>();
            if (flight == null) flight = FindFirstObjectByType<BallFlightSimulator>();
            if (runner == null || range == null || shots == null || flight == null || runner.Game == null)
            {
                if (Time.unscaledTime - itemStart > 30f) { problems.Add(item + " • the game did not start in 30 s"); NextItem(); }
                return;
            }

            float now = Time.unscaledTime;
            int scored = 0;
            foreach (MiniPlayer p in runner.Players) scored += p.Shots;
            if (scored != lastScoredTotal) { lastScoredTotal = scored; lastProgress = now; }
            if (now - lastProgress > 45f) { problems.Add(item + " • STUCK: nothing happened for 45 s (shots sent " + shotsSent + ", scored " + scored + ")"); NextItem(); return; }

            if (runner.Finished)
            {
                if (!resultsSeen)
                {
                    resultsSeen = true;
                    resultsAt = now;
                    CheckResults(scored);
                }
                if (now - resultsAt > 1.5f && !resultsPicture)
                {
                    resultsPicture = true;
                    Picture(playAgainDone ? "results-again" : "results");
                }
                if (now - resultsAt > 3f)
                {
                    if (testPlayAgain && !playAgainDone)
                    {
                        playAgainDone = true;
                        runner.StartGame();
                        CheckPlayAgain();
                        resultsSeen = pictureTaken = resultsPicture = false;
                        nextShotAt = now + 3f;
                        lastProgress = now;
                        return;
                    }
                    NextItem();
                }
                return;
            }

            // Explore has no end: finish after a few shots.
            if (exploreLimit > 0 && scored >= exploreLimit && !flight.IsInFlight) { runner.FinishNow(); return; }

            if (flight.IsInFlight) { stoppedAt = -1f; return; }
            if (stoppedAt < 0f) stoppedAt = now;
            // Like a golfer: hit once the last ball has been counted and the ball is back on the mat.
            if (now - stoppedAt < 0.6f || GetField<string>(range, "status") == "BALL IN FLIGHT") return;
            if (now < nextShotAt) return;
            // Wait for the result banner and the turn change before the next shot.
            if (GetField<float>(runner, "nextTurnAt") > 0f || GetField<float>(runner, "finishAt") > 0f) return;

            if (shotsSent == 2 && !pictureTaken) { Picture("play"); pictureTaken = true; }
            // Once per game: the enlarged map (as if the corner map was clicked).
            if (shotsSent == 3 && !mapPicture)
            {
                mapPicture = true;
                typeof(ModernGolfSimUI).GetField("mapExpanded", F).SetValue(range, true);
                Picture("map");
                StartCoroutine(CloseMapLater());
                return;
            }
            if (lastClub != null && flight.CarryMeters > 1f && lastExpected > 1f)
                carryRatio[lastClub] = Mathf.Lerp(carryRatio.TryGetValue(lastClub, out float r0) ? r0 : 1f, flight.CarryMeters / lastExpected, 0.6f);
            Hit();
            nextShotAt = now + 1.2f;
        }

        private void Hit()
        {
            MiniPlayer p = runner.CurrentPlayer;
            if (p == null) return;
            Vector3 target = runner.Game.AimHint(p);
            // Edge Knockout on its cliff map: now and then go long over the edge or wide off the side,
            // so falling into the canyon gets tested too.
            if (runner.Info.Id == MiniGameId.EdgeKnockout)
            {
                if (shotsSent % 4 == 3) target += Vector3.forward * 30f;
                else if (shotsSent % 4 == 1) target += Vector3.right * (shotsSent % 8 == 1 ? 60f : -60f);
            }
            float distance = new Vector2(target.x, target.z).magnitude;

            // Shortest club that carries far enough (from this test's own carries), softer if needed.
            string club = null, longestClub = null;
            float bestCarry = float.MaxValue, longest = 0f;
            foreach (ActiveClub.BagClub c in ActiveClub.Bag)
            {
                if (ActiveClub.IsPutter(c.Name)) continue;
                float carry = Carry(c.Name);
                if (carry > longest) { longest = carry; longestClub = c.Name; }
                if (carry >= distance * 0.97f && carry < bestCarry) { bestCarry = carry; club = c.Name; }
            }
            if (club == null) club = longestClub;
            ActiveClub.Select(club);
            float expected = Carry(club);
            float factor = distance < expected ? Mathf.Clamp(Mathf.Pow(distance / expected, 0.8f), 0.3f, 1f) : 1f;

            AimPointer aim = GetField<AimPointer>(range, "aimPointer");
            if (aim != null)
            {
                aim.Set(Vector3.zero, target);
                typeof(ModernGolfSimUI).GetField("aimMoved", F).SetValue(range, true);
                typeof(ModernGolfSimUI).GetMethod("ApplyRangeAim", F).Invoke(range, new object[] { false });
            }
            ShotData shot = TestShotProvider.BuildShot(club, ActiveClub.LoftOf(club));
            shot.BallSpeedMps *= factor;
            shot.ClubSpeedMps *= factor;
            lastClub = club;
            lastExpected = ActiveClub.ExpectedCarry(club) * factor;
            shotsSent++;
            shots.Inject(shot);
        }

        private float Carry(string club) => ActiveClub.ExpectedCarry(club) * (carryRatio.TryGetValue(club, out float r) ? r : 1f);

        private void CheckResults(int scored)
        {
            MiniGame game = runner.Game;
            List<MiniPlayer> ranked = runner.Ranked();
            report.AppendLine("  RESULTS after " + scored + " scored shots (" + shotsSent + " sent):");
            foreach (MiniPlayer p in ranked)
                report.AppendLine("    " + p.Name + "  " + game.ScoreText(p) + " " + game.ScoreCaption + "  •  " + p.Shots + " shots" + (game.HasCollection ? "  •  " + game.Collection(p) : "") + (p.Log.Count > 0 ? "  •  " + string.Join(" ", p.Log) : ""));

            MiniGameInfo info = runner.Info;
            bool explore = game.Explore;
            if (!explore && info.Id != MiniGameId.EdgeKnockout)
                foreach (MiniPlayer p in runner.Players)
                    if (p.Shots != game.ShotsPerPlayer) problems.Add(item + " • " + p.Name + " hit " + p.Shots + " shots, expected " + game.ShotsPerPlayer);
            foreach (MiniPlayer p in runner.Players)
                if (p.Log.Count != p.Shots) problems.Add(item + " • " + p.Name + ": " + p.Shots + " shots but " + p.Log.Count + " results logged");
            if (scored > shotsSent) problems.Add(item + " • more shots scored (" + scored + ") than sent (" + shotsSent + ")");
            // Ranking order must follow the rank key.
            for (int i = 1; i < ranked.Count; i++)
            {
                float a = game.RankKey(ranked[i - 1]), b = game.RankKey(ranked[i]);
                if (game.LowerIsBetter ? a > b : a < b) problems.Add(item + " • results in the wrong order (" + ranked[i - 1].Name + " before " + ranked[i].Name + ")");
            }
            if (info.Id == MiniGameId.EdgeKnockout && runner.Players.Count > 1)
            {
                int left = runner.Players.FindAll(p => !p.Out).Count;
                int rounds = GetField<int>(game, "round");
                if (left > 1 && rounds < game.ShotsPerPlayer) problems.Add(item + " • knockout ended with " + left + " players still in after round " + rounds);
            }
            if (info.Id == MiniGameId.CaptureFlags)
            {
                int held = 0;
                foreach (MiniPlayer p in runner.Players) held += p.Flags;
                if (held > 9 || held < 0) problems.Add(item + " • " + held + " flags held (9 on the range)");
            }
        }

        private void CheckPlayAgain()
        {
            foreach (MiniPlayer p in runner.Players)
                if (p.Shots != 0 || p.Score != 0f || p.Log.Count != 0 || p.Out) problems.Add(item + " • PLAY AGAIN did not reset " + p.Name);
            if (runner.Turn != 0) problems.Add(item + " • PLAY AGAIN did not start with the first player");
            report.AppendLine("  PLAY AGAIN: game rebuilt, scores reset - playing again");
        }

        private void Picture(string what)
        {
            string file = Path.Combine(logsDir, (itemIndex + 1).ToString("00") + "-" + runner.Info.Id + "-" + what + ".png");
            StartCoroutine(Grab(file));
            report.AppendLine("  picture: Logs/minigames/" + Path.GetFileName(file));
        }

        private System.Collections.IEnumerator CloseMapLater()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            if (range != null) typeof(ModernGolfSimUI).GetField("mapExpanded", F).SetValue(range, false);
        }

        /// <summary>Reads the finished frame (3D view and the HUD) into a PNG.</summary>
        private System.Collections.IEnumerator Grab(string file)
        {
            yield return new WaitForEndOfFrame();
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Destroy(tex);
        }

        private void NextItem()
        {
            report.AppendLine();
            item = null;
            BeginItem(itemIndex + 1, true);
        }

        private void Finish()
        {
            item = null;
            report.AppendLine(problems.Count == 0 ? "NO PROBLEMS FOUND" : problems.Count + " PROBLEM(S):");
            foreach (string p in problems) report.AppendLine("  - " + p);
            string text = report.ToString();
            File.WriteAllText(Path.Combine(Application.persistentDataPath, "minigames-report.txt"), text);
            File.WriteAllText(Path.Combine(Directory.GetParent(logsDir).FullName, "minigames-report.txt"), text);
            Debug.Log("[GolfSimZA] Mini games test finished: " + (problems.Count == 0 ? "no problems" : problems.Count + " problem(s)") + " - Logs/minigames-report.txt");
            MiniGameSession.End();
            UnityEditor.EditorApplication.isPlaying = false;
            Destroy(gameObject);
        }

        private static T GetField<T>(object o, string name)
        {
            FieldInfo f = o.GetType().GetField(name, F);
            if (f != null) return (T)f.GetValue(o);
            PropertyInfo p = o.GetType().GetProperty(name, F);
            return p != null ? (T)p.GetValue(o) : default;
        }
    }
}
#endif
