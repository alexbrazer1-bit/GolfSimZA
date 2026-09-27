using System;
using System.Collections.Generic;
using GolfSimZA.Core;
using GolfSimZA.Courses;
using GolfSimZA.Physics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GolfSimZA.UI
{
    /// <summary>
    /// Runs a round: holes, tees, pins, aim, penalties, holing out, scoring and the round HUD.
    /// Plays an imported course (via CourseLoader) or the built-in demo holes.
    /// </summary>
    public sealed class RoundGameplayUI : MonoBehaviour
    {
        private readonly int[] demoPar = DemoCourse.Par;

        [SerializeField] private SimulatorController simulatorController;
        [SerializeField] private BallFlightSimulator ballFlightSimulator;
        [SerializeField] private Transform ball;
        [SerializeField] private Transform pin;
        [SerializeField] private Transform green;

        private GUIStyle smallPanel, darkPanel, titleStyle, smallStyle, valueStyle, rightValueStyle;
        private GUIStyle holeStyle, courseStyle, clubStyle, activeClubStyle, actionStyle;
        private GUIStyle playerStyle, activePlayerStyle, playerMetaStyle, distanceStyle, mapStyle;
        private GUIStyle centerStyle, bannerStyle, cellStyle, cellHeaderStyle, activeNameStyle, scoreBoxStyle;
        private Texture2D darkTexture, darkerTexture, blueTexture, blueBrightTexture, greenTexture, redTexture, whiteTexture, yellowTexture;
        private bool stylesReady;

        private CourseLoader courseLoader;
        private FlightPresentation presentation;
        private ShotTracer tracer;
        private CourseDefinition course;
        private bool waitingForCourse;
        private string courseError;

        private int holeIndex, lastObservedShotCount;
        private bool wasInFlight, shotFinished, waitingForNextPlayer, roundComplete;
        private int activePlayerIndex;
        private float currentHoleDistance;
        private float aimOffsetDegrees;
        private Vector3 pinPosition;
        private Vector3 teePosition;
        private string status = "READY";
        private bool showScorecard;
        private int lastShooterIndex;
        private int lastShotStrokes;
        private PuttGridRenderer puttGrid;
        private bool PracticeMode => CourseSession.PracticeMode;
        private string banner;
        private float bannerUntil;

        private string[] playerNames = { "Player 1" };
        private Vector3[] playerPositions = { Vector3.zero };
        private Vector3[] playerPreviousPositions = { Vector3.zero };
        private int[] playerTotalStrokes = { 0 };
        private int[] playerHoleStrokes = { 0 };
        private bool[] playerHoled = { false };
        private List<int[]> scorecard = new List<int[]>();
        private int[] mulligansUsed = { 0 };
        private bool matchDecidedAnnounced;

        /// <summary>Match play needs at least two players; practice has no scoring.</summary>
        private bool MatchPlay => !PracticeMode && playerNames.Length >= 2 &&
                                  string.Equals(CourseSession.GameMode, "Match Play", StringComparison.OrdinalIgnoreCase);

        private readonly Color dark = new Color(0.015f, 0.055f, 0.065f, 0.86f);
        private readonly Color darker = new Color(0.005f, 0.025f, 0.03f, 0.92f);
        private readonly Color blue = new Color(0.07f, 0.55f, 0.37f, 0.95f);
        private readonly Color blueBright = new Color(0.96f, 0.72f, 0.23f, 1f);
        private readonly Color muted = new Color(0.76f, 0.83f, 0.85f, 1f);

        private bool UsingCourse => course != null;
        /// <summary>Holes on the course (demo holes: 18).</summary>
        private int CourseHoles => UsingCourse ? Mathf.Max(1, course.HoleCount) : DemoCourse.Par.Length;
        /// <summary>First hole of the round (0-based; 9 = back nine).</summary>
        private int FirstHole => Mathf.Clamp(CourseSession.RoundStartHole, 0, CourseHoles - 1);
        /// <summary>One past the last hole of the round.</summary>
        private int EndHole => Mathf.Min(CourseHoles, FirstHole + Mathf.Clamp(CourseSession.RoundLength, 1, 18));
        /// <summary>Number of holes in this round.</summary>
        private int HoleCount => Mathf.Max(1, EndHole - FirstHole);
        private int CurrentPar => UsingCourse ? course.GetHole(holeIndex).par : demoPar[Mathf.Clamp(holeIndex, 0, demoPar.Length - 1)];

        private void Awake()
        {
            if (ModernGolfSimUI.IsRangeSession())
            {
                // The practice range uses the range HUD; no holes or scoring.
                enabled = false;
                return;
            }

            LoadPlayers();
            holeIndex = FirstHole;
            TryResumeRound();

            if (ballFlightSimulator == null) ballFlightSimulator = GetComponent<BallFlightSimulator>();
            presentation = GetComponent<FlightPresentation>();
            tracer = GetComponent<ShotTracer>();

            courseLoader = GetComponent<CourseLoader>();
            if (courseLoader == null) courseLoader = gameObject.AddComponent<CourseLoader>();
            courseLoader.Loaded += OnCourseLoaded;
            courseLoader.Failed += OnCourseFailed;

            puttGrid = gameObject.AddComponent<PuttGridRenderer>();
            markers = gameObject.AddComponent<CourseMarkers>();
            holeMap = gameObject.AddComponent<HoleMapCamera>();
            GameOptions.ScorecardRequested += ToggleScorecard;
            GameOptions.MulliganRequested += Mulligan;
            GameOptions.FlyoverRequested += Flyover;
            GameOptions.ShowFlagChanged += ApplyFlagVisibility;
            GameOptions.PuttGridChanged += UpdatePuttGrid;

            if (CourseSession.IsImportedCourse && !GolfSimZA.Players.ClubMappingSession.IsActive)
            {
                waitingForCourse = true;
                status = "LOADING COURSE";
            }
            else
            {
                // Demo holes: clouds and colour boost (imported courses get these when they load).
                GolfSimZA.Visual.CloudDome.Ensure(Camera.main);
                GolfSimZA.Visual.ColorGrade.Attach(Camera.main);
                StartHole();
            }
        }

        private void OnDestroy()
        {
            GameOptions.ScorecardRequested -= ToggleScorecard;
            GameOptions.MulliganRequested -= Mulligan;
            GameOptions.FlyoverRequested -= Flyover;
            GameOptions.ShowFlagChanged -= ApplyFlagVisibility;
            GameOptions.PuttGridChanged -= UpdatePuttGrid;
            if (courseLoader != null)
            {
                courseLoader.Loaded -= OnCourseLoaded;
                courseLoader.Failed -= OnCourseFailed;
            }
        }

        private void OnCourseLoaded(CourseDefinition loaded)
        {
            course = loaded;
            waitingForCourse = false;
            if (ballFlightSimulator != null) ballFlightSimulator.AltitudeMeters = loaded.altitudeMeters;
            if (green != null) green.gameObject.SetActive(false);
            StartHole();
        }

        private void OnCourseFailed(string message)
        {
            courseError = message;
            waitingForCourse = false;
        }

        private void PlayDemoInstead()
        {
            courseError = null;
            course = null;
            StartHole();
        }

        // ---------------------------------------------------------------- Update

        private void Update()
        {
            if (waitingForCourse || courseError != null || roundComplete) return;
            if (simulatorController == null || ballFlightSimulator == null) return;

            if (!GameMenuOverlay.IsOpen && !ballFlightSimulator.IsInFlight)
            {
                HandleAimKeys();
                ClubBar.HandleKeys();
            }

            int shotCount = simulatorController.History != null ? simulatorController.History.Count : 0;
            if (shotCount > lastObservedShotCount)
            {
                int newShots = shotCount - lastObservedShotCount;
                lastShooterIndex = activePlayerIndex;
                lastShotStrokes = 0;
                puttGrid?.Hide();
                AddStrokes(activePlayerIndex, newShots);
                lastObservedShotCount = shotCount;
                shotFinished = false;
                waitingForNextPlayer = false;
                status = "SHOT IN PROGRESS";

            }

            bool inFlight = ballFlightSimulator.IsInFlight;
            if (wasInFlight && !inFlight && playerHoleStrokes[activePlayerIndex] > 0)
                OnShotFinished();
            wasInFlight = inFlight;
            UpdateMarkers();
        }

        /// <summary>Gimme circle on the green and the aim line from the ball.</summary>
        private void UpdateMarkers()
        {
            if (markers == null || ball == null || ballFlightSimulator == null) return;
            AppSettings s = AppSettings.Current;
            bool flying = ballFlightSimulator.IsInFlight;
            bool ready = !flying && !roundComplete && !waitingForNextPlayer && !AllHoled() && !(presentation != null && presentation.IsFlyingOver);

            float gimme = GimmieMeters();
            if (s.showGimmeCircle && gimme > 0f && ready && ballFlightSimulator.IsOnGreen(ball.position))
                markers.ShowGimme(pinPosition, gimme, CourseMarkers.GimmeColors[Mathf.Clamp(s.gimmeCircleColor, 0, 2)]);
            else
                markers.HideGimme();

            if (s.showAimIndicator && ready)
            {
                Vector3 aim = Quaternion.Euler(0f, ballFlightSimulator.AimYawDegrees, 0f) * Vector3.forward;
                float length = Mathf.Clamp(HorizontalDistance(ball.position, AimTarget(ball.position)), 2f, 320f);
                markers.ShowAim(ball.position, aim, length);
            }
            else
            {
                markers.HideAim();
            }
        }

        private void HandleAimKeys()
        {
            if (ballFlightSimulator.IsInFlight || Keyboard.current == null) return;
            float step = Keyboard.current.shiftKey.isPressed ? 5f : 1f;
            bool changed = false;
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame) { aimOffsetDegrees -= step; changed = true; }
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame) { aimOffsetDegrees += step; changed = true; }
            if (Keyboard.current.upArrowKey.wasPressedThisFrame) { aimOffsetDegrees = 0f; changed = true; }
            if (changed)
            {
                aimOffsetDegrees = Mathf.Clamp(aimOffsetDegrees, -90f, 90f);
                ApplyAim(ball != null ? ball.position : Vector3.zero, false);
            }
        }

        private void OnShotFinished()
        {
            Vector3 rest = ball != null ? ball.position : Vector3.zero;

            if (ballFlightSimulator.WasHoled)
            {
                MarkHoled(activePlayerIndex, playerHoleStrokes[activePlayerIndex] == 1 ? "HOLE IN ONE!" : "IN THE HOLE");
            }
            else if (!ApplyPenalties(rest))
            {
                playerPositions[activePlayerIndex] = rest;
                float gimmie = GimmieMeters();
                float toPin = HorizontalDistance(rest, pinPosition);
                if (gimmie > 0f && toPin <= gimmie)
                {
                    AddStrokes(activePlayerIndex, 1);
                    MarkHoled(activePlayerIndex, "GIMME");
                }
                else if (AppSettings.Current.autoPutt && ballFlightSimulator.IsOnGreen(rest))
                {
                    // AUTO PUTT (Settings → GAME): putts are counted from the distance to the hole.
                    AppSettings a = AppSettings.Current;
                    int putts = toPin <= a.autoPuttOneMeters ? 1 : toPin <= a.autoPuttTwoMeters ? 2 : 3;
                    AddStrokes(activePlayerIndex, putts);
                    MarkHoled(activePlayerIndex, "AUTO PUTT  •  " + putts + (putts == 1 ? " PUTT" : " PUTTS"));
                }
            }

            shotFinished = true;
            status = "SHOT COMPLETE";
            aimOffsetDegrees = 0f;

            if (AllHoled())
            {
                waitingForNextPlayer = false;
                status = "HOLE COMPLETE";
                return;
            }

            int next = NextPlayerToHit();
            if (playerNames.Length > 1 && next != activePlayerIndex)
            {
                waitingForNextPlayer = true;
            }
            else
            {
                // Same player hits again: set up behind the ball towards the pin.
                activePlayerIndex = next;
                PrepareActivePlayer(false);
                shotFinished = false;
                status = "READY • " + playerNames[activePlayerIndex];
            }
        }

        /// <summary>Out of bounds and water: one penalty stroke, replay from the previous spot or the drop zone.</summary>
        private bool ApplyPenalties(Vector3 rest)
        {
            if (!UsingCourse) return false;

            if (course.outOfBounds != null && course.outOfBounds.IsValid && !course.outOfBounds.Contains(rest))
            {
                AddStrokes(activePlayerIndex, 1);
                playerPositions[activePlayerIndex] = playerPreviousPositions[activePlayerIndex];
                ShowBanner("OUT OF BOUNDS  •  +1 STROKE");
                return true;
            }

            if (course.hazards != null)
            {
                foreach (HazardDefinition hazard in course.hazards)
                {
                    if (hazard.area == null || !hazard.area.Contains(rest)) continue;
                    if (hazard.freeDrop)
                    {
                        playerPositions[activePlayerIndex] = hazard.hasDropZone ? hazard.dropZone : playerPreviousPositions[activePlayerIndex];
                        ShowBanner("FREE DROP");
                        return true;
                    }
                    AddStrokes(activePlayerIndex, 1);
                    playerPositions[activePlayerIndex] = hazard.hasDropZone && !hazard.innerOutOfBounds ? hazard.dropZone : playerPreviousPositions[activePlayerIndex];
                    ShowBanner(hazard.innerOutOfBounds ? "OUT OF BOUNDS  •  +1 STROKE" : "PENALTY AREA  •  +1 STROKE");
                    return true;
                }
            }
            return false;
        }

        private void MarkHoled(int player, string message)
        {
            playerHoled[player] = true;
            playerPositions[player] = pinPosition;
            ShowBanner(message + "  •  " + playerNames[player] + "  " + ScoreName(playerHoleStrokes[player] - CurrentPar));
        }

        private void AddStrokes(int player, int strokes)
        {
            playerHoleStrokes[player] += strokes;
            playerTotalStrokes[player] += strokes;
            if (player == lastShooterIndex) lastShotStrokes += strokes;
        }

        // ---------------------------------------------------------------- Game menu actions

        private void ToggleScorecard() => showScorecard = !showScorecard;

        /// <summary>Takes the last shot back: strokes (incl. penalties) removed, ball back where it was.</summary>
        private void Mulligan()
        {
            if (ballFlightSimulator == null || ballFlightSimulator.IsInFlight || lastShotStrokes <= 0) return;
            int p = lastShooterIndex;
            int allowance = MulliganAllowance;
            if (mulligansUsed[p] >= allowance)
            {
                ShowBanner(allowance == 0 ? "MULLIGANS ARE OFF FOR THIS ROUND" : "NO MULLIGANS LEFT  •  " + playerNames[p]);
                return;
            }
            mulligansUsed[p]++;
            playerHoleStrokes[p] = Mathf.Max(0, playerHoleStrokes[p] - lastShotStrokes);
            playerTotalStrokes[p] = Mathf.Max(0, playerTotalStrokes[p] - lastShotStrokes);
            lastShotStrokes = 0;
            playerHoled[p] = false;
            playerPositions[p] = playerPreviousPositions[p];
            activePlayerIndex = p;
            shotFinished = false;
            waitingForNextPlayer = false;
            aimOffsetDegrees = 0f;
            presentation?.HideLandingMarker();
            tracer?.Clear();
            PrepareActivePlayer(true);
            int left = allowance == int.MaxValue ? -1 : allowance - mulligansUsed[p];
            ShowBanner("MULLIGAN  •  " + playerNames[p] + " hits again" + (left >= 0 ? "  •  " + left + " LEFT" : ""));
        }

        private void Flyover()
        {
            if (presentation == null || ballFlightSimulator == null || ballFlightSimulator.IsInFlight) return;
            var path = new System.Collections.Generic.List<Vector3> { teePosition };
            if (UsingCourse)
            {
                HoleDefinition hole = course.GetHole(holeIndex);
                if (hole.aimPoints != null) path.AddRange(hole.aimPoints);
            }
            path.Add(pinPosition);
            presentation.PlayFlyover(path, () => ApplyAim(ball != null ? ball.position : teePosition, true));
        }

        private void ApplyFlagVisibility()
        {
            if (pin != null) pin.gameObject.SetActive(GameOptions.ShowFlag);
        }

        private void UpdatePuttGrid()
        {
            if (puttGrid == null || ball == null || ballFlightSimulator == null) return;
            if (GameOptions.PuttGrid && ballFlightSimulator.IsOnGreen(ball.position))
                puttGrid.Show(pinPosition, ball.position);
            else
                puttGrid.Hide();
        }

        private void JumpToHole(int index)
        {
            if (ballFlightSimulator != null && ballFlightSimulator.IsInFlight) return;
            // Practice: any hole on the course.
            holeIndex = ((index % CourseHoles) + CourseHoles) % CourseHoles;
            StartHole();
        }

        private bool AllHoled()
        {
            for (int i = 0; i < playerHoled.Length; i++) if (!playerHoled[i]) return false;
            return true;
        }

        /// <summary>The player furthest from the hole plays next (players who holed out are skipped).</summary>
        private int NextPlayerToHit()
        {
            int style = AppSettings.Current.rotationStyle;
            bool activeInPlay = activePlayerIndex < playerNames.Length && !playerHoled[activePlayerIndex] && playerHoleStrokes[activePlayerIndex] > 0;

            // PLAY OUT HOLE: the player keeps hitting until holed, then the next player in order.
            if (style == 1)
            {
                if (activeInPlay) return activePlayerIndex;
                for (int i = 0; i < playerNames.Length; i++) if (!playerHoled[i]) return i;
                return activePlayerIndex;
            }

            // PUTT OUT: once on the green a player keeps putting until holed.
            if (style == 2 && activeInPlay && ballFlightSimulator != null && ballFlightSimulator.IsOnGreen(playerPositions[activePlayerIndex]))
                return activePlayerIndex;

            int best = activePlayerIndex;
            float bestDistance = -1f;
            for (int i = 0; i < playerNames.Length; i++)
            {
                if (playerHoled[i]) continue;
                float d = HorizontalDistance(playerPositions[i], pinPosition);
                if (playerHoleStrokes[i] == 0) d = float.MaxValue - i; // nobody has teed off yet
                if (d > bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }

        private float GimmieMeters()
        {
            string setting = CourseSession.GimmieSetting ?? "Off";
            if (setting.StartsWith("Off", StringComparison.OrdinalIgnoreCase)) return 0f;
            string number = setting.Replace("m", "").Trim();
            return float.TryParse(number, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0f;
        }

        // ---------------------------------------------------------------- Holes

        private void LoadPlayers()
        {
            string saved = CourseSession.PlayerNames;
            if (string.IsNullOrWhiteSpace(saved)) saved = "Player 1";
            string[] savedNames = saved.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            if (savedNames.Length == 0) savedNames = new[] { "Player 1" };

            playerNames = savedNames;
            playerPositions = new Vector3[playerNames.Length];
            playerPreviousPositions = new Vector3[playerNames.Length];
            playerTotalStrokes = new int[playerNames.Length];
            playerHoleStrokes = new int[playerNames.Length];
            playerHoled = new bool[playerNames.Length];
            mulligansUsed = new int[playerNames.Length];
            activePlayerIndex = 0;
        }

        /// <summary>RESUME ROUND: continue the saved round on this course with the same players.</summary>
        private void TryResumeRound()
        {
            if (PracticeMode || !CourseSession.ResumeRound) return;
            RoundSave save = RoundSave.LoadFor(playerNames);
            if (save == null)
            {
                resumedBanner = "NO SAVED ROUND FOR THESE PLAYERS ON THIS COURSE  •  NEW ROUND";
                return;
            }
            scorecard = new List<int[]>(save.Scores);
            for (int h = 0; h < scorecard.Count; h++)
            {
                if (scorecard[h] == null) continue;
                for (int p = 0; p < playerNames.Length && p < scorecard[h].Length; p++) playerTotalStrokes[p] += scorecard[h][p];
            }
            for (int p = 0; p < playerNames.Length && p < save.MulligansUsed.Length; p++) mulligansUsed[p] = save.MulligansUsed[p];
            holeIndex = Mathf.Max(0, save.NextHole);
            resumedBanner = "ROUND RESUMED  •  HOLE " + (holeIndex + 1);
        }

        private string resumedBanner;
        private CourseMarkers markers;
        private float scorecardUntil = -1f;
        private Vector3[] playerTees = new Vector3[0];
        private float[] playerHoleLengths = new float[0];

        /// <summary>The tees this player plays (their own choice when the course has it, else the match tees).</summary>
        private string PlayerTeeName(int player)
        {
            string own = GolfSimZA.Players.PlayerRoster.TeeFor(playerNames[player]);
            return own != null && Array.IndexOf(CourseSession.AvailableTees, own) >= 0 ? own : CourseSession.TeeName;
        }

        private Color PlayerColor(int player) => GolfSimZA.Players.PlayerRoster.ColorFor(playerNames[player]);
        private string holeResultText;

        private void SaveRoundProgress()
        {
            if (PracticeMode) return;
            new RoundSave
            {
                CourseKey = RoundSave.CurrentCourseKey,
                CourseName = UsingCourse ? course.name : CourseSession.CourseName,
                Players = playerNames,
                NextHole = holeIndex + 1,
                RoundLength = HoleCount,
                Scores = new List<int[]>(scorecard),
                MulligansUsed = mulligansUsed
            }.Save();
        }

        /// <summary>Mulligans allowed per player per round (Round Settings → MULLIGANS).</summary>
        private int MulliganAllowance
        {
            get
            {
                if (PracticeMode) return int.MaxValue;
                string setting = CourseSession.MulliganSetting ?? "Unlimited";
                if (setting.StartsWith("Unlimited", StringComparison.OrdinalIgnoreCase)) return int.MaxValue;
                if (setting.StartsWith("Off", StringComparison.OrdinalIgnoreCase)) return 0;
                return int.TryParse(setting.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int n) ? Mathf.Max(0, n) : int.MaxValue;
            }
        }

        // ---------------------------------------------------------------- Match play

        /// <summary>Index of the player who won the hole outright, -1 when halved or not played.</summary>
        private int HoleWinner(int[] scores)
        {
            if (scores == null) return -1;
            int best = int.MaxValue, winner = -1;
            bool tie = false;
            for (int p = 0; p < scores.Length; p++)
            {
                if (scores[p] <= 0) return -1;
                if (scores[p] < best) { best = scores[p]; winner = p; tie = false; }
                else if (scores[p] == best) tie = true;
            }
            return tie ? -1 : winner;
        }

        private int[] HolesWon()
        {
            var won = new int[playerNames.Length];
            foreach (int[] scores in scorecard)
            {
                int w = HoleWinner(scores);
                if (w >= 0 && w < won.Length) won[w]++;
            }
            return won;
        }

        private int HolesPlayed()
        {
            int n = 0;
            foreach (int[] scores in scorecard) if (scores != null) n++;
            return n;
        }

        /// <summary>"JANLIE 2 UP", "ALL SQUARE", "JANLIE WINS 3&2" (two players) or holes won (3-4 players).</summary>
        private string MatchStatusText()
        {
            int[] won = HolesWon();
            if (playerNames.Length == 2)
            {
                int diff = won[0] - won[1];
                int remaining = HoleCount - HolesPlayed();
                if (diff == 0) return remaining == 0 ? "MATCH HALVED" : "ALL SQUARE";
                string leader = playerNames[diff > 0 ? 0 : 1].ToUpperInvariant();
                int lead = Mathf.Abs(diff);
                if (lead > remaining) return remaining == 0 ? leader + " WINS " + lead + " UP" : leader + " WINS " + lead + "&" + remaining;
                return leader + " " + lead + " UP" + (lead == remaining ? " (DORMIE)" : "");
            }
            var parts = new List<string>();
            for (int p = 0; p < playerNames.Length; p++) parts.Add(playerNames[p] + " " + won[p]);
            return "HOLES WON  •  " + string.Join("   ", parts);
        }

        private void StartHole()
        {
            holeIndex = PracticeMode ? Mathf.Clamp(holeIndex, 0, CourseHoles - 1) : Mathf.Clamp(holeIndex, FirstHole, EndHole - 1);
            activePlayerIndex = 0;
            shotFinished = false;
            waitingForNextPlayer = false;
            aimOffsetDegrees = 0f;
            lastObservedShotCount = simulatorController != null && simulatorController.History != null ? simulatorController.History.Count : 0;
            wasInFlight = false;

            if (playerTees.Length != playerNames.Length) playerTees = new Vector3[playerNames.Length];
            if (playerHoleLengths.Length != playerNames.Length) playerHoleLengths = new float[playerNames.Length];

            if (UsingCourse)
            {
                HoleDefinition hole = course.GetHole(holeIndex);
                TeeDefinition tee = hole.GetTee(CourseSession.TeeName);
                teePosition = tee != null ? tee.position : hole.GreenTarget;
                pinPosition = hole.GetPin(CourseSession.PinSetting);
                currentHoleDistance = hole.LengthMeters(CourseSession.TeeName);
                float greenRadius = hole.hasGreenCenter ? Mathf.Max(12f, HorizontalDistance(hole.greenCenter, pinPosition) + 9f) : 15f;
                pinPosition.y = GroundProbe.HeightAt(pinPosition);
                ballFlightSimulator?.SetHole(pinPosition, greenRadius);

                // Each player can play their own tees (Round Settings → player tile).
                for (int i = 0; i < playerNames.Length; i++)
                {
                    string teeName = PlayerTeeName(i);
                    TeeDefinition own = hole.GetTee(teeName);
                    playerTees[i] = own != null ? own.position : teePosition;
                    playerHoleLengths[i] = own != null ? hole.LengthMeters(teeName) : currentHoleDistance;
                }
            }
            else
            {
                currentHoleDistance = DemoHoleDistance(holeIndex, CourseSession.TeeName);
                teePosition = new Vector3(0f, 0f, 0f);
                pinPosition = new Vector3(0f, 0f, currentHoleDistance);
                for (int i = 0; i < playerNames.Length; i++)
                {
                    float length = DemoHoleDistance(holeIndex, PlayerTeeName(i));
                    playerTees[i] = new Vector3(0f, 0f, currentHoleDistance - length);
                    playerHoleLengths[i] = length;
                }
                if (green != null)
                {
                    green.position = new Vector3(0f, green.position.y, currentHoleDistance);
                    green.localScale = new Vector3(14f, green.localScale.y, 8f);
                }
                ballFlightSimulator?.SetHole(pinPosition, 9f);
            }

            if (pin != null)
            {
                pin.gameObject.SetActive(true);
                pin.position = new Vector3(pinPosition.x, GroundProbe.HeightAt(pinPosition) + 0.01f, pinPosition.z);
            }

            for (int i = 0; i < playerPositions.Length; i++)
            {
                playerPositions[i] = playerTees[i];
                playerPreviousPositions[i] = playerTees[i];
                playerHoleStrokes[i] = 0;
                playerHoled[i] = false;
            }

            presentation?.HideLandingMarker();
            tracer?.Clear();
            holeMap?.Render(HolePoints());
            // New wind for every hole (Settings → REALISM), relative to the tee-to-green line.
            Wind.NewHole(pinPosition - teePosition);
            PrepareActivePlayer(true);
            status = "READY • " + playerNames[activePlayerIndex];
            if (AppSettings.Current.autoFlyover) Flyover();
            string holeText = "HOLE " + (holeIndex + 1) + "  •  PAR " + CurrentPar + "  •  " + Units.DistanceText(currentHoleDistance);
            if (holeResultText != null) { holeText = holeResultText + "     " + holeText; holeResultText = null; }
            if (resumedBanner != null) { holeText = resumedBanner + "     " + holeText; resumedBanner = null; }
            ShowBanner(holeText);
            bannerUntil = Time.unscaledTime + 6f;
            // Tee shot on a par 4/5: start with the driver (first club in the bag).
            if (CurrentPar >= 4) ActiveClub.SelectBagIndex(0);
        }

        /// <summary>Puts the active player's ball down and aims at the next target.</summary>
        private void PrepareActivePlayer(bool snapCamera)
        {
            ActiveClub.Player = playerNames[activePlayerIndex];
            Vector3 position = playerPositions[activePlayerIndex];
            playerPreviousPositions[activePlayerIndex] = position;
            if (ballFlightSimulator != null) ballFlightSimulator.PlaceBall(position);
            else if (ball != null) ball.position = position;
            ApplyAim(ball != null ? ball.position : position, snapCamera);
            ApplyFlagVisibility();
            UpdatePuttGrid();
            GolfSimAudio.PlayReady();
        }

        private Vector3 AimTarget(Vector3 from)
        {
            if (UsingCourse && playerHoleStrokes[activePlayerIndex] == 0)
            {
                // From the tee, aim at the first aim point (doglegs) when there is one.
                HoleDefinition hole = course.GetHole(holeIndex);
                if (hole.aimPoints != null && hole.aimPoints.Length > 0)
                    return hole.aimPoints[0];
            }
            return pinPosition;
        }

        private void ApplyAim(Vector3 from, bool snapCamera)
        {
            Vector3 target = AimTarget(from);
            Vector3 direction = target - from;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + aimOffsetDegrees;
            if (ballFlightSimulator != null) ballFlightSimulator.AimYawDegrees = yaw;
            Vector3 aim = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            presentation?.SetAddress(from, aim, snapCamera);
        }

        private void AdvancePlayer()
        {
            activePlayerIndex = NextPlayerToHit();
            if (AppSettings.Current.showUpNextMessage)
                ShowBanner("UP NEXT  •  " + playerNames[activePlayerIndex].ToUpperInvariant() + "  •  " + Units.DistanceText(HorizontalDistance(pinPosition, playerPositions[activePlayerIndex])) + " TO THE PIN");
            shotFinished = false;
            waitingForNextPlayer = false;
            lastObservedShotCount = simulatorController != null && simulatorController.History != null ? simulatorController.History.Count : lastObservedShotCount;
            status = "READY • " + playerNames[activePlayerIndex];
            PrepareActivePlayer(true);
        }

        private void AdvanceHole()
        {
            RecordHoleScores();
            int seconds = AppSettings.Current.scorecardAfterHoleSeconds;
            if (seconds > 0 && !PracticeMode) scorecardUntil = Time.unscaledTime + seconds;
            if (PracticeMode)
            {
                JumpToHole(holeIndex + 1);
                return;
            }
            holeIndex++;
            if (holeIndex >= EndHole)
            {
                FinishRound();
                return;
            }
            StartHole();
        }

        private void RecordHoleScores()
        {
            int[] scores = new int[playerNames.Length];
            for (int i = 0; i < scores.Length; i++) scores[i] = playerHoleStrokes[i];
            while (scorecard.Count <= holeIndex) scorecard.Add(null);
            scorecard[holeIndex] = scores;
            if (PracticeMode) return;
            SaveRoundProgress();
            if (MatchPlay)
            {
                int w = HoleWinner(scores);
                string match = MatchStatusText();
                bool decided = match.Contains(" WINS ");
                if (decided && !matchDecidedAnnounced)
                {
                    matchDecidedAnnounced = true;
                    holeResultText = "MATCH OVER  •  " + match;
                }
                else
                {
                    holeResultText = (w >= 0 ? playerNames[w].ToUpperInvariant() + " WINS THE HOLE" : "HOLE HALVED") + "  •  " + match;
                }
            }
        }

        private void FinishRound()
        {
            RoundSave.Clear();
            roundComplete = true;
            status = "ROUND COMPLETE";
            shotFinished = false;
            waitingForNextPlayer = false;
        }

        private float DemoHoleDistance(int index, string tee) => DemoCourse.Length(index, tee);

        // ---------------------------------------------------------------- Helpers

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            Vector3 d = a - b;
            d.y = 0f;
            return d.magnitude;
        }

        private void ShowBanner(string text)
        {
            banner = text;
            bannerUntil = Time.unscaledTime + 4f;
        }

        private static string ScoreName(int relative)
        {
            switch (relative)
            {
                case -3: return "ALBATROSS";
                case -2: return "EAGLE";
                case -1: return "BIRDIE";
                case 0: return "PAR";
                case 1: return "BOGEY";
                case 2: return "DOUBLE BOGEY";
                default: return relative < 0 ? relative.ToString() : "+" + relative;
            }
        }

        private string FormatScore(int relativeToPar)
        {
            if (relativeToPar == 0) return "E";
            return relativeToPar > 0 ? "+" + relativeToPar : relativeToPar.ToString();
        }

        private int ParForHole(int index) => UsingCourse ? course.GetHole(index).par : demoPar[Mathf.Clamp(index, 0, demoPar.Length - 1)];

        private int GetPlayerRelativeToPar(int playerIndex)
        {
            // Completed holes only (plus the current hole once this player has holed out),
            // so a tee shot does not show as "-2".
            if (playerIndex < 0 || playerIndex >= playerNames.Length) return 0;
            int strokes = 0, par = 0;
            for (int h = FirstHole; h < scorecard.Count && h < EndHole; h++)
            {
                int[] scores = scorecard[h];
                if (scores == null || playerIndex >= scores.Length || scores[playerIndex] <= 0) continue;
                if (h == holeIndex && !roundComplete) continue; // counted below from the live strokes
                strokes += scores[playerIndex];
                par += ParForHole(h);
            }
            if (!roundComplete && playerHoled[playerIndex] && playerHoleStrokes[playerIndex] > 0)
            {
                strokes += playerHoleStrokes[playerIndex];
                par += ParForHole(holeIndex);
            }
            return strokes - par;
        }

        private float DistanceToPin(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= playerPositions.Length) return currentHoleDistance;
            if (playerHoled[playerIndex]) return 0f;
            Vector3 position = playerPositions[playerIndex];
            if (playerIndex == activePlayerIndex && ball != null) position = ball.position;
            return HorizontalDistance(pinPosition, position);
        }

        // ---------------------------------------------------------------- GUI

        private void EnsureStyles()
        {
            GolfSimTheme.Ensure();
            if (stylesReady) return;

            darkTexture = MakeTexture(dark);
            darkerTexture = MakeTexture(darker);
            blueTexture = MakeTexture(blue);
            blueBrightTexture = MakeTexture(blueBright);
            whiteTexture = MakeTexture(new Color(1f, 1f, 1f, 0.95f));
            greenTexture = MakeTexture(new Color(0.12f, 0.56f, 0.28f, 0.95f));
            redTexture = MakeTexture(new Color(0.92f, 0.15f, 0.13f, 1f));
            yellowTexture = MakeTexture(new Color(1f, 0.78f, 0.1f, 1f));

            smallPanel = MakePanel(darkTexture, 7);
            darkPanel = MakePanel(darkerTexture, 7);
            titleStyle = MakeLabel(14, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            smallStyle = MakeLabel(9, FontStyle.Normal, muted, TextAnchor.MiddleLeft);
            valueStyle = MakeLabel(13, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            rightValueStyle = MakeLabel(13, FontStyle.Bold, Color.white, TextAnchor.MiddleRight);
            holeStyle = MakeLabel(17, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            courseStyle = MakeLabel(10, FontStyle.Bold, muted, TextAnchor.MiddleCenter);
            clubStyle = MakeLabel(11, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            activeClubStyle = MakeLabel(11, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            actionStyle = new GUIStyle(GolfSimTheme.AccentButton) { fixedHeight = 0 };
            playerStyle = MakeLabel(13, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            activePlayerStyle = MakeLabel(11, FontStyle.Bold, blueBright, TextAnchor.MiddleLeft);
            playerMetaStyle = MakeLabel(12, FontStyle.Bold, muted, TextAnchor.MiddleLeft);
            distanceStyle = MakeLabel(18, FontStyle.Bold, Color.white, TextAnchor.MiddleRight);
            mapStyle = MakeLabel(8, FontStyle.Bold, muted, TextAnchor.MiddleCenter);
            centerStyle = MakeLabel(10, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            bannerStyle = MakeLabel(20, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            cellStyle = MakeLabel(12, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            cellHeaderStyle = MakeLabel(10, FontStyle.Bold, muted, TextAnchor.MiddleCenter);
            activeNameStyle = MakeLabel(20, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            scoreBoxStyle = MakeLabel(22, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            stylesReady = true;
        }

        private GUIStyle MakeLabel(int size, FontStyle fontStyle, Color color, TextAnchor alignment)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = size;
            style.fontStyle = fontStyle;
            style.alignment = alignment;
            style.normal.textColor = color;
            style.padding = new RectOffset(0, 0, 0, 0);
            return style;
        }

        private GUIStyle MakePanel(Texture2D texture, int border)
        {
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.normal.background = texture;
            style.border = new RectOffset(border, border, border, border);
            style.padding = new RectOffset(8, 8, 6, 6);
            return style;
        }

        private GUIStyle MakeButton(int size, Texture2D normalTexture)
        {
            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.fontSize = size;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = Color.white;
            style.normal.background = normalTexture;
            style.hover.textColor = Color.white;
            style.hover.background = blueBrightTexture;
            style.active.textColor = Color.white;
            style.active.background = blueTexture;
            style.border = new RectOffset(5, 5, 5, 5);
            return style;
        }

        private Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void OnGUI()
        {
            if (!enabled) return;
            EnsureStyles();
            GolfSimTheme.Ensure();

            if (waitingForCourse)
            {
                DrawLoading();
                return;
            }
            if (courseError != null)
            {
                DrawCourseError();
                return;
            }

            float margin = Mathf.Clamp(Screen.width * 0.015f, 10f, 24f);
            AppSettings settings = AppSettings.Current;
            bool flying = ballFlightSimulator != null && ballFlightSimulator.IsInFlight;
            bool hideUi = flying && settings.hideUiOnShot;

            if (!hideUi)
            {
                DrawDistanceBanner();
                DrawPlayerBanner(margin + 132f, margin);
                DrawCenterTop(margin);
                float cardBottom = DrawHoleCard(Screen.width - margin, margin);
                float tilesBottom = settings.miniMapRight ? Screen.height - margin - MiniMapHeight - 8f : Screen.height - margin;
                ShotDataTiles.Draw(Screen.width - margin, cardBottom + 8f, simulatorController != null ? simulatorController.LastShot : default(ShotData), tilesBottom);
                DrawMiniMap();
                bool clubBar = settings.clubSelectorMode == 0 || (settings.clubSelectorMode == 2 && !flying);
                if (clubBar) ClubBar.Draw(margin, Screen.height - margin);
                else ClubBar.Close();
                DrawShotCompletionAction();
                DrawBallReady();
            }
            DrawBanner();
            bool autoScorecard = scorecardUntil > 0f && Time.unscaledTime < scorecardUntil;
            if (roundComplete || showScorecard || autoScorecard) DrawScorecard();
        }

        private void DrawLoading()
        {
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none, darkPanel);
            float w = Mathf.Min(560f, Screen.width - 40f);
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height * 0.42f;
            CourseDefinition pending = courseLoader != null ? courseLoader.Course : null;
            GUI.Label(new Rect(x, y - 50f, w, 30f), pending != null ? pending.name : CourseSession.CourseName, bannerStyle);
            GUI.Label(new Rect(x, y - 16f, w, 20f), courseLoader != null ? courseLoader.StatusText : "Loading...", centerStyle);
            float progress = courseLoader != null ? courseLoader.Progress : 0f;
            GUI.DrawTexture(new Rect(x, y + 12f, w, 8f), darkTexture);
            GUI.DrawTexture(new Rect(x, y + 12f, w * Mathf.Clamp01(progress), 8f), blueBrightTexture);
        }

        private void DrawCourseError()
        {
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none, darkPanel);
            float w = Mathf.Min(640f, Screen.width - 40f);
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height * 0.30f;
            GUI.Label(new Rect(x, y, w, 30f), "COURSE COULD NOT BE LOADED", bannerStyle);
            GUIStyle wrap = new GUIStyle(centerStyle) { wordWrap = true, fontSize = 12, fontStyle = FontStyle.Normal };
            GUI.Label(new Rect(x, y + 40f, w, 90f), courseError, wrap);
            if (GUI.Button(new Rect(x, y + 140f, w * 0.48f, 42f), "PLAY DEMO HOLES", actionStyle)) PlayDemoInstead();
            if (GUI.Button(new Rect(x + w * 0.52f, y + 140f, w * 0.48f, 42f), "BACK TO COURSES", actionStyle))
                SceneManager.LoadScene("GolfSimZA_0_5_CourseSelection");
        }

        /// <summary>
        /// Active player top-left: name in the player's colour, shot number, distance to the pin and
        /// score. The other players are listed underneath (name in their colour).
        /// </summary>
        private void DrawPlayerBanner(float x, float y)
        {
            int a = Mathf.Clamp(activePlayerIndex, 0, playerNames.Length - 1);
            Color color = PlayerColor(a);
            float w = Mathf.Min(360f, Screen.width * 0.26f);
            Rect r = new Rect(x, y, w, 60f);
            GUI.Box(r, GUIContent.none, GolfSimTheme.Card);
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, 8f, r.height), whiteTexture);
            GUI.color = Color.white;

            float scoreW = 58f;
            GUI.Label(new Rect(r.x + 18f, r.y + 6f, w - scoreW - 26f, 26f), GolfSimTheme.Ellipsize(playerNames[a], activeNameStyle, w - scoreW - 26f), new GUIStyle(activeNameStyle) { normal = { textColor = color } });
            string shot = playerHoled[a] ? "IN THE HOLE" : "SHOT " + (playerHoleStrokes[a] + 1) + "   •   " + Units.DistanceText(DistanceToPin(a), "0.0");
            GUI.Label(new Rect(r.x + 18f, r.y + 34f, w - scoreW - 26f, 18f), shot, playerMetaStyle);

            Rect score = new Rect(r.xMax - scoreW, r.y, scoreW, r.height);
            GUI.color = color;
            GUI.DrawTexture(score, whiteTexture);
            GUI.color = Color.white;
            GUI.Label(score, PracticeMode ? "–" : FormatScore(GetPlayerRelativeToPar(a)), scoreBoxStyle);

            // Other players
            float rowH = 26f;
            int others = playerNames.Length - 1;
            float matchH = MatchPlay ? 24f : 0f;
            if (others <= 0 && matchH <= 0f) return;
            Rect list = new Rect(x, r.yMax + 4f, w, 8f + rowH * others + matchH);
            GUI.Box(list, GUIContent.none, GolfSimTheme.Card);
            float ry = list.y + 4f;
            for (int i = 0; i < playerNames.Length; i++)
            {
                if (i == a) continue;
                Color c = PlayerColor(i);
                GUI.color = c;
                GUI.DrawTexture(new Rect(list.x + 8f, ry + 7f, 5f, 12f), whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(list.x + 20f, ry, w * 0.5f, rowH), GolfSimTheme.Ellipsize(playerNames[i], playerStyle, w * 0.5f), new GUIStyle(playerStyle) { normal = { textColor = c } });
                GUI.Label(new Rect(list.x + w * 0.52f, ry, 40f, rowH), PracticeMode ? "" : FormatScore(GetPlayerRelativeToPar(i)), playerStyle);
                GUI.Label(new Rect(list.x + w * 0.62f, ry, w * 0.36f - 8f, rowH), playerHoled[i] ? "IN" : Units.DistanceText(DistanceToPin(i)), new GUIStyle(playerStyle) { alignment = TextAnchor.MiddleRight });
                ry += rowH;
            }
            if (MatchPlay)
                GUI.Label(new Rect(list.x + 12f, ry, w - 20f, 20f), "MATCH PLAY  •  " + MatchStatusText(), new GUIStyle(smallStyle) { fontStyle = FontStyle.Bold, normal = { textColor = GolfSimTheme.Gold } });
        }

        /// <summary>Hole card top-right (above the data tiles). Returns its bottom edge.</summary>
        private float DrawHoleCard(float right, float y)
        {
            float w = ShotDataTiles.TileWidth * 2f + 4f;
            float x = right - w;
            float h = PracticeMode ? 94f : 66f;
            Rect card = new Rect(x, y, w, h);
            GUI.Box(card, GUIContent.none, GolfSimTheme.Card);
            GUI.DrawTexture(new Rect(card.x, card.y, 56f, 66f), GolfSimTheme.AccentTex);
            GUI.Label(new Rect(card.x, card.y + 4f, 56f, 14f), "HOLE", new GUIStyle(centerStyle) { fontSize = 10 });
            GUI.Label(new Rect(card.x, card.y + 16f, 56f, 36f), (holeIndex + 1).ToString(), new GUIStyle(holeStyle) { fontSize = 30 });
            GUI.Label(new Rect(card.x, card.y + 50f, 56f, 14f), PracticeMode ? "PRACTICE" : (holeIndex - FirstHole + 1) + " / " + HoleCount, new GUIStyle(centerStyle) { fontSize = 9 });

            float tx = card.x + 64f, tw = w - 70f;
            string courseName = UsingCourse ? course.name : CourseSession.CourseName;
            GUI.Label(new Rect(tx, card.y + 6f, tw, 20f), GolfSimTheme.Ellipsize(courseName, valueStyle, tw), valueStyle);
            float length = activePlayerIndex < playerHoleLengths.Length ? playerHoleLengths[activePlayerIndex] : currentHoleDistance;
            GUI.Label(new Rect(tx, card.y + 26f, tw, 20f), "PAR " + CurrentPar + "   •   " + Units.DistanceText(length), new GUIStyle(valueStyle) { fontSize = 14, normal = { textColor = GolfSimTheme.Gold } });
            string tees = activePlayerIndex < playerNames.Length ? PlayerTeeName(activePlayerIndex) : CourseSession.TeeName;
            GUI.Label(new Rect(tx, card.y + 46f, tw, 14f), GolfSimTheme.Ellipsize(tees.ToUpperInvariant() + " TEES  •  " + CourseSession.PinSetting.ToUpperInvariant() + " PINS", smallStyle, tw), smallStyle);

            if (PracticeMode)
            {
                if (GUI.Button(new Rect(card.x + 6f, card.y + 68f, w * 0.5f - 9f, 22f), "◀ HOLE", GolfSimTheme.SmallButton)) JumpToHole(holeIndex - 1);
                if (GUI.Button(new Rect(card.x + w * 0.5f + 3f, card.y + 68f, w * 0.5f - 9f, 22f), "HOLE ▶", GolfSimTheme.SmallButton)) JumpToHole(holeIndex + 1);
            }
            return card.yMax;
        }

        /// <summary>Wind speed with an arrow showing where it blows (relative to the view), and status.</summary>
        private void DrawCenterTop(float y)
        {
            float w = 230f;
            float x = (Screen.width - w) * 0.5f;
            Rect pill = new Rect(x, y, w, 38f);
            GUI.Box(pill, GUIContent.none, GolfSimTheme.Card);
            Camera cam = Camera.main;
            Vector3 forward = cam != null ? cam.transform.forward : Vector3.forward;
            bool calm = Wind.SpeedMps < 0.1f;
            if (!calm)
            {
                Rect arrow = new Rect(pill.x + 12f, pill.y + 5f, 28f, 28f);
                Matrix4x4 saved = GUI.matrix;
                GUIUtility.RotateAroundPivot(Wind.ArrowAngle(forward), arrow.center);
                GUI.Label(arrow, "▲", new GUIStyle(centerStyle) { fontSize = 20, normal = { textColor = GolfSimTheme.Gold } });
                GUI.matrix = saved;
            }
            GUI.Label(new Rect(pill.x + 44f, pill.y, w - 52f, 38f), calm ? "NO WIND" : Wind.SpeedText() + "   " + Wind.Describe(forward), new GUIStyle(centerStyle) { fontSize = 13, alignment = TextAnchor.MiddleLeft });
            string aim = Mathf.Abs(aimOffsetDegrees) < 0.5f ? "AIM ON TARGET" : "AIM " + Mathf.Abs(aimOffsetDegrees).ToString("0") + "° " + (aimOffsetDegrees < 0f ? "LEFT" : "RIGHT");
            GUI.Label(new Rect(x - 120f, y + 42f, w + 240f, 18f), status + "   •   " + aim + "   (← → aim, ↑ reset)", new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } });
        }

        /// <summary>Distance box over the flag (Settings → DISTANCE BANNER).</summary>
        private void DrawDistanceBanner()
        {
            AppSettings s = AppSettings.Current;
            if (!s.showDistanceBanner || ball == null || roundComplete) return;
            if (s.hideBannerOnGreen && ballFlightSimulator != null && ballFlightSimulator.IsOnGreen(ball.position)) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 top = pinPosition + Vector3.up * 3.2f;
            Vector3 sp = cam.WorldToScreenPoint(top);
            if (sp.z < 1f || sp.x < 0f || sp.x > Screen.width || sp.y < 0f || sp.y > Screen.height) return;
            Vector2 p = new Vector2(sp.x, Screen.height - sp.y);
            Rect box = new Rect(p.x - 46f, p.y - 58f, 92f, 48f);
            GUI.Box(box, GUIContent.none, GolfSimTheme.Card);
            float d = DistanceToPin(activePlayerIndex);
            GUI.Label(new Rect(box.x, box.y + 2f, box.width, 28f), Units.Distance(d).ToString(d < 10f ? "0.0" : "0"), new GUIStyle(holeStyle) { fontSize = 22 });
            float rise = pinPosition.y - ball.position.y;
            string elevation = Mathf.Abs(rise) < 0.5f ? Units.DistanceUnit.ToUpperInvariant() : (rise > 0 ? "▲ " : "▼ ") + Units.Distance(Mathf.Abs(rise)).ToString("0") + " " + Units.DistanceUnit;
            GUI.Label(new Rect(box.x, box.y + 28f, box.width, 16f), elevation, new GUIStyle(centerStyle) { fontSize = 11, normal = { textColor = GolfSimTheme.Gold } });
            GUI.DrawTexture(new Rect(p.x - 1f, box.yMax, 2f, 10f), whiteTexture);
        }

        /// <summary>"BALL READY" pill (Settings → SHOW BALL READY INDICATOR).</summary>
        private void DrawBallReady()
        {
            if (!AppSettings.Current.showBallReady || ballFlightSimulator == null || ballFlightSimulator.IsInFlight) return;
            if (roundComplete || waitingForNextPlayer || AllHoled() || (presentation != null && presentation.IsFlyingOver)) return;
            float w = 260f;
            Rect r = new Rect((Screen.width - w) * 0.5f, Screen.height - ClubBar.Height - 112f, w, 34f);
            Color c = PlayerColor(activePlayerIndex);
            GUI.Box(r, GUIContent.none, GolfSimTheme.Card);
            GUI.color = new Color(0.35f, 0.95f, 0.5f);
            GUI.DrawTexture(new Rect(r.x + 12f, r.y + 12f, 10f, 10f), whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 30f, r.y, r.width - 36f, r.height), "BALL READY  •  " + playerNames[activePlayerIndex].ToUpperInvariant(), new GUIStyle(centerStyle) { fontSize = 13, normal = { textColor = c } });
        }

        private static float MiniMapWidth => Mathf.Clamp(Screen.width * 0.14f, 180f, 240f);
        private static float MiniMapHeight => MiniMapWidth * 1.5f + 36f;
        private HoleMapCamera holeMap;

        private List<Vector3> HolePoints()
        {
            var points = new List<Vector3> { teePosition };
            if (UsingCourse)
            {
                HoleDefinition hole = course.GetHole(holeIndex);
                if (hole.aimPoints != null) points.AddRange(hole.aimPoints);
            }
            points.Add(pinPosition);
            return points;
        }

        private void DrawMiniMap()
        {
            float margin = Mathf.Clamp(Screen.width * 0.015f, 10f, 24f);
            float width = MiniMapWidth;
            float height = MiniMapHeight;
            bool right = AppSettings.Current.miniMapRight;
            float x = right ? Screen.width - width - margin : margin;
            // On the left the map sits above the club selector.
            float y = Screen.height - height - margin - (right ? 0f : ClubBar.Height + 10f);

            GUI.Box(new Rect(x, y, width, height), GUIContent.none, darkPanel);
            GUI.Label(new Rect(x + 10f, y + 6f, width - 20f, 18f), "HOLE " + (holeIndex + 1), titleStyle);
            GUI.Label(new Rect(x + 10f, y + 6f, width - 20f, 18f), Units.DistanceText(DistanceToPin(activePlayerIndex)) + " TO PIN", new GUIStyle(mapStyle) { alignment = TextAnchor.MiddleRight, fontSize = 11 });
            DrawMiniMapGraphic(new Rect(x + 6f, y + 28f, width - 12f, height - 34f));
        }

        /// <summary>Top-down picture of the hole (tee bottom, pin top) with the route, pin and every player's ball.</summary>
        private void DrawMiniMapGraphic(Rect area)
        {
            List<Vector3> points = HolePoints();
            System.Func<Vector3, Vector2> toMap;

            if (holeMap != null && holeMap.HasImage)
            {
                GUI.DrawTexture(area, holeMap.Texture, ScaleMode.StretchToFill);
                toMap = w => holeMap.ToMap(w, area);
            }
            else
            {
                GUI.DrawTexture(area, darkerTexture, ScaleMode.StretchToFill);
                Vector3 axis = pinPosition - teePosition;
                axis.y = 0f;
                if (axis.sqrMagnitude < 1f) axis = Vector3.forward;
                axis.Normalize();
                Vector3 side = new Vector3(axis.z, 0f, -axis.x);
                float maxAlong = 1f, maxSide = 10f;
                foreach (Vector3 p in points)
                {
                    Vector3 d = p - teePosition;
                    maxAlong = Mathf.Max(maxAlong, Vector3.Dot(d, axis));
                    maxSide = Mathf.Max(maxSide, Mathf.Abs(Vector3.Dot(d, side)));
                }
                float scale = Mathf.Min((area.height - 30f) / maxAlong, (area.width * 0.5f - 12f) / maxSide);
                Vector3 tee = teePosition;
                toMap = w =>
                {
                    Vector3 d = w - tee;
                    return new Vector2(area.center.x + Vector3.Dot(d, side) * scale, area.yMax - 15f - Vector3.Dot(d, axis) * scale);
                };
            }

            GUI.BeginClip(area);
            Vector2 offset = new Vector2(area.x, area.y);
            for (int i = 1; i < points.Count; i++)
                DrawDots(toMap(points[i - 1]) - offset, toMap(points[i]) - offset, whiteTexture);

            Vector2 pinMap = toMap(pinPosition) - offset;
            GUI.DrawTexture(new Rect(pinMap.x - 1f, pinMap.y - 12f, 2f, 12f), whiteTexture);
            GUI.DrawTexture(new Rect(pinMap.x + 1f, pinMap.y - 12f, 7f, 5f), redTexture);

            for (int i = 0; i < playerPositions.Length; i++)
            {
                if (playerHoled[i]) continue;
                Vector3 pos = i == activePlayerIndex && ball != null ? ball.position : playerPositions[i];
                Vector2 m = toMap(pos) - offset;
                float size = i == activePlayerIndex ? 11f : 8f;
                GUI.DrawTexture(new Rect(m.x - size * 0.5f - 1f, m.y - size * 0.5f - 1f, size + 2f, size + 2f), darkerTexture);
                GUI.color = PlayerColor(i);
                GUI.DrawTexture(new Rect(m.x - size * 0.5f, m.y - size * 0.5f, size, size), whiteTexture);
                GUI.color = Color.white;
            }
            GUI.EndClip();
        }

        private void DrawDots(Vector2 a, Vector2 b, Texture2D texture)
        {
            float length = Vector2.Distance(a, b);
            int dots = Mathf.Clamp(Mathf.RoundToInt(length / 6f), 1, 80);
            for (int i = 0; i <= dots; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)dots);
                GUI.DrawTexture(new Rect(p.x - 1.5f, p.y - 1.5f, 3f, 3f), texture);
            }
        }

        private void DrawBanner()
        {
            if (string.IsNullOrEmpty(banner) || Time.unscaledTime > bannerUntil) return;
            // Centre column between the hole card (left) and the data tiles (right); wraps onto
            // several lines ("hole result" and "next hole" are separate lines).
            float margin = Mathf.Clamp(Screen.width * 0.015f, 10f, 24f);
            float w = Mathf.Clamp(Screen.width - 2f * (margin + 310f), 280f, 640f);
            string text = banner.Replace("     ", "\n");
            var style = new GUIStyle(bannerStyle) { wordWrap = true };
            float h = Mathf.Max(48f, style.CalcHeight(new GUIContent(text), w - 24f) + 16f);
            Rect r = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.22f, w, h);
            GUI.Box(r, GUIContent.none, darkPanel);
            GUI.Label(new Rect(r.x + 12f, r.y + 8f, r.width - 24f, r.height - 16f), text, style);
        }

        private void DrawShotCompletionAction()
        {
            if (roundComplete || ballFlightSimulator == null || ballFlightSimulator.IsInFlight) return;

            float width = Mathf.Clamp(Screen.width * 0.24f, 250f, 330f);
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - ClubBar.Height - 62f;
            bool holeDone = AllHoled();

            if (holeDone || (shotFinished && waitingForNextPlayer))
            {
                string text = !holeDone
                    ? "NEXT PLAYER  •  " + playerNames[NextPlayerToHit()]
                    : (PracticeMode || holeIndex + 1 < EndHole ? "NEXT HOLE" : "FINISH ROUND");

                if (GUI.Button(new Rect(x, y, width, 40f), text, actionStyle))
                {
                    if (!holeDone) AdvancePlayer();
                    else AdvanceHole();
                }
                return;
            }

            // Pick the ball up (counts one more stroke) to move on, like conceding a putt.
            if (playerHoleStrokes[activePlayerIndex] > 0 && !playerHoled[activePlayerIndex])
            {
                if (GUI.Button(new Rect(x + width * 0.5f - 70f, y, 140f, 40f), "PICK UP", actionStyle))
                {
                    AddStrokes(activePlayerIndex, 1);
                    MarkHoled(activePlayerIndex, "PICKED UP");
                    shotFinished = true;
                    if (AllHoled()) status = "HOLE COMPLETE";
                    else AdvancePlayer();
                }
            }
        }

        private static Texture2D scorecardBackdrop;

        /// <summary>Strokes on finished holes only (a hole in progress is not counted against par).</summary>
        private int ScoredStrokes(int p)
        {
            int strokes = 0;
            for (int h = FirstHole; h < scorecard.Count && h < EndHole; h++)
                if (scorecard[h] != null && p < scorecard[h].Length) strokes += scorecard[h][p];
            if (!roundComplete && playerHoled[p] && (holeIndex >= scorecard.Count || scorecard[holeIndex] == null)) strokes += playerHoleStrokes[p];
            return strokes;
        }

        private void DrawScorecard()
        {
            if (scorecardBackdrop == null) scorecardBackdrop = GolfSimTheme.Tex(new Color(0.02f, 0.05f, 0.06f, 0.97f));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), scorecardBackdrop);
            int holes = HoleCount;
            int first = FirstHole;
            float cellW = Mathf.Clamp((Screen.width - 260f) / (holes + 1), 30f, 52f);
            float tableW = 160f + cellW * (holes + 1);
            float x = (Screen.width - tableW) * 0.5f;
            float y = Screen.height * 0.18f;

            GUI.Label(new Rect(x, y - 50f, tableW, 34f), (roundComplete ? "ROUND COMPLETE  •  " : "SCORECARD  •  ") + (UsingCourse ? course.name : CourseSession.CourseName), bannerStyle);

            GUI.Label(new Rect(x, y, 160f, 24f), "HOLE", cellHeaderStyle);
            for (int h = 0; h < holes; h++) GUI.Label(new Rect(x + 160f + cellW * h, y, cellW, 24f), (first + h + 1).ToString(), cellHeaderStyle);
            GUI.Label(new Rect(x + 160f + cellW * holes, y, cellW, 24f), "TOT", cellHeaderStyle);

            y += 26f;
            GUI.Label(new Rect(x, y, 160f, 24f), "PAR", cellHeaderStyle);
            int parTotal = 0;
            for (int h = 0; h < holes; h++)
            {
                parTotal += ParForHole(first + h);
                GUI.Label(new Rect(x + 160f + cellW * h, y, cellW, 24f), ParForHole(first + h).ToString(), cellHeaderStyle);
            }
            GUI.Label(new Rect(x + 160f + cellW * holes, y, cellW, 24f), parTotal.ToString(), cellHeaderStyle);

            for (int p = 0; p < playerNames.Length; p++)
            {
                y += 30f;
                GUI.Label(new Rect(x, y, 160f, 26f), playerNames[p], new GUIStyle(cellStyle) { normal = { textColor = PlayerColor(p) } });
                int total = 0, parPlayed = 0;
                for (int c = 0; c < holes; c++)
                {
                    int h = first + c;
                    int strokes = h < scorecard.Count && scorecard[h] != null && p < scorecard[h].Length ? scorecard[h][p] : (h == holeIndex && !roundComplete ? playerHoleStrokes[p] : 0);
                    total += strokes;
                    bool finished = (h < scorecard.Count && scorecard[h] != null) || (h == holeIndex && !roundComplete && playerHoled[p]);
                    if (strokes > 0 && finished) parPlayed += ParForHole(h);
                    GUI.Label(new Rect(x + 160f + cellW * c, y, cellW, 26f), strokes > 0 ? strokes.ToString() : "-", cellStyle);
                }
                GUI.Label(new Rect(x + 160f + cellW * holes, y, cellW, 26f), total + " (" + FormatScore(ScoredStrokes(p) - parPlayed) + ")", cellStyle);
            }

            if (MatchPlay)
            {
                y += 30f;
                GUI.Label(new Rect(x, y, 160f, 26f), "MATCH", cellHeaderStyle);
                for (int c = 0; c < holes; c++)
                {
                    int h = first + c;
                    int[] scores = h < scorecard.Count ? scorecard[h] : null;
                    string cell = "-";
                    if (scores != null)
                    {
                        int w = HoleWinner(scores);
                        cell = w >= 0 ? (playerNames[w].Length > 3 ? playerNames[w].Substring(0, 3) : playerNames[w]).ToUpperInvariant() : "½";
                    }
                    GUI.Label(new Rect(x + 160f + cellW * c, y, cellW, 26f), cell, cellHeaderStyle);
                }
                y += 30f;
                GUI.Label(new Rect(x, y, tableW, 26f), MatchStatusText(), new GUIStyle(cellHeaderStyle) { alignment = TextAnchor.MiddleLeft, normal = { textColor = GolfSimTheme.Gold } });
            }

            if (!roundComplete)
            {
                if (GUI.Button(new Rect((Screen.width - 260f) * 0.5f, y + 60f, 260f, 44f), "CLOSE SCORECARD", actionStyle))
                    showScorecard = false;
                return;
            }
            if (GUI.Button(new Rect((Screen.width - 260f) * 0.5f, y + 60f, 260f, 44f), "BACK TO HOME", actionStyle))
                SceneManager.LoadScene("GolfSimZA_0_5_CourseSelection");
        }
    }
}
