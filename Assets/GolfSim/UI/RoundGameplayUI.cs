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

        // ---- Game format and teams (Round Settings → FORMAT)
        private GameFormatInfo format = GameFormats.All[0];
        private int[] playerTeam = { 0 };
        private int teamCount = 1;
        private List<int>[] teamMembers = { new List<int> { 0 } };
        /// <summary>Team member already hit from the team's spot in the current "everyone hits" round.</summary>
        private bool[] hitThisRound = { false };
        /// <summary>Team is in an "everyone hits, pick the best" round (tee shots; every shot in a scramble).</summary>
        private bool[] teamRoundActive = { false };
        /// <summary>The team has picked its drive on this hole.</summary>
        private bool[] teamDriveChosen = { false };
        /// <summary>Greensomes: who hits the team's ball next (-1 = nobody yet).</summary>
        private int[] teamNextHitter = { -1 };
        /// <summary>Team that must pick its ball before play goes on (-1 = none).</summary>
        private int pendingSelection = -1;

        private bool TeamFormat => format.Team;
        /// <summary>Formats where the team picks a drive / shot (scramble, shamble, greensomes).</summary>
        private bool SelectFormat => format.Team && format.SelectDrive;

        // ---- Aim target (drag with the mouse, right-click to place, arrow keys)
        private AimPointer aimPointer;
        private bool aimMoved;

        /// <summary>Match play needs at least two players; practice has no scoring.</summary>
        private bool MatchPlay => !PracticeMode && playerNames.Length >= 2 && format.Id == GameFormatId.MatchPlay;

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
            aimPointer = gameObject.AddComponent<AimPointer>();
            aimPointer.CanInteract = () => !GameMenuOverlay.IsOpen && ballFlightSimulator != null && !ballFlightSimulator.IsInFlight
                                           && !roundComplete && pendingSelection < 0 && !showScorecard
                                           && !(presentation != null && presentation.IsFlyingOver);
            aimPointer.Moved += OnAimMoved;
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
                CourseScenery.ImproveDemo(gameObject, Camera.main);
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
                // A shot while the team is still choosing its ball: take the recommended ball first.
                if (pendingSelection >= 0) ChooseShot(pendingSelection, RecommendedBall(pendingSelection), true, "BEST BALL PICKED AUTOMATICALLY");
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
            bool ready = !flying && !roundComplete && !waitingForNextPlayer && pendingSelection < 0 && !AllHoled() && !(presentation != null && presentation.IsFlyingOver);

            // Auto putt: 1-putt (gold) and 2-putt (white) circles around the hole.
            bool holeLive = !roundComplete && !AllHoled() && !(presentation != null && presentation.IsFlyingOver);
            if (s.autoPutt && s.showPuttCircles && holeLive)
                markers.ShowPuttCircles(pinPosition, s.autoPuttOneMeters, Mathf.Max(s.autoPuttTwoMeters, s.autoPuttOneMeters + 0.5f));
            else
                markers.HidePuttCircles();

            if (aimPointer != null)
            {
                if (ready) { if (!aimPointer.Visible) aimPointer.Set(ball.position, aimPointer.Point); }
                else aimPointer.Hide();
            }

            float gimme = GimmieMeters();
            if (s.showGimmeCircle && gimme > 0f && ready && ballFlightSimulator.IsOnGreen(ball.position))
                markers.ShowGimme(pinPosition, gimme, CourseMarkers.GimmeColors[Mathf.Clamp(s.gimmeCircleColor, 0, 2)]);
            else
                markers.HideGimme();

            if (s.showAimIndicator && ready)
            {
                Vector3 aim = Quaternion.Euler(0f, ballFlightSimulator.AimYawDegrees, 0f) * Vector3.forward;
                float length = Mathf.Clamp(HorizontalDistance(ball.position, aimPointer != null ? aimPointer.Point : AimTarget(ball.position)), 2f, 400f);
                markers.ShowAim(ball.position, aim, length);
            }
            else
            {
                markers.HideAim();
            }
        }

        /// <summary>
        /// ← / → turn the aim target around the ball (Shift = 5°), ↑ / ↓ move it further / closer
        /// (Shift = 10 m), Home puts it back on the target line.
        /// </summary>
        private void HandleAimKeys()
        {
            if (ballFlightSimulator.IsInFlight || Keyboard.current == null || ball == null || aimPointer == null) return;
            Keyboard k = Keyboard.current;
            bool shift = k.shiftKey.isPressed;
            float turn = 0f, move = 0f;
            if (k.leftArrowKey.wasPressedThisFrame) turn = shift ? -5f : -1f;
            if (k.rightArrowKey.wasPressedThisFrame) turn = shift ? 5f : 1f;
            if (k.upArrowKey.wasPressedThisFrame) move = shift ? 10f : 2f;
            if (k.downArrowKey.wasPressedThisFrame) move = shift ? -10f : -2f;
            if (k.homeKey.wasPressedThisFrame) { ResetAimPoint(); ApplyAim(ball.position, false); return; }
            if (turn == 0f && move == 0f) return;
            Vector3 from = ball.position;
            Vector3 d = aimPointer.Point - from;
            d.y = 0f;
            float distance = Mathf.Clamp(d.magnitude + move, 1f, aimPointer.MaxDistance);
            Vector3 dir = d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward;
            dir = Quaternion.Euler(0f, turn, 0f) * dir;
            aimPointer.Set(from, from + dir * distance);
            aimMoved = true;
            ApplyAim(from, false);
        }

        private void OnAimMoved(Vector3 point)
        {
            aimMoved = true;
            if (ball != null) ApplyAim(ball.position, false);
        }

        /// <summary>Aim target back on the hole's line at the selected club's distance (or the pin when closer).</summary>
        private void ResetAimPoint()
        {
            if (ball == null || aimPointer == null) return;
            Vector3 from = ball.position;
            Vector3 target = AimTarget(from);
            Vector3 d = target - from;
            d.y = 0f;
            float toTarget = d.magnitude;
            float carry = GolfSimZA.Core.ActiveClub.ExpectedCarry(GolfSimZA.Core.ActiveClub.Resolve());
            float distance = carry > 1f ? Mathf.Min(carry, toTarget) : toTarget;
            if (HorizontalDistance(from, pinPosition) <= carry) distance = HorizontalDistance(from, pinPosition);
            Vector3 dir = d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward;
            aimPointer.Set(from, from + dir * Mathf.Max(1f, distance));
            aimMoved = false;
        }

        /// <summary>Signed angle (degrees, + = right) between the hole's target line and the aim target.</summary>
        private float AimAngleFromLine()
        {
            if (ball == null || aimPointer == null) return 0f;
            Vector3 line = AimTarget(ball.position) - ball.position;
            Vector3 aim = aimPointer.Point - ball.position;
            line.y = aim.y = 0f;
            if (line.sqrMagnitude < 0.01f || aim.sqrMagnitude < 0.01f) return 0f;
            return Vector3.SignedAngle(line, aim, Vector3.up);
        }

        private void OnShotFinished()
        {
            Vector3 rest = ball != null ? ball.position : Vector3.zero;
            int shooter = activePlayerIndex;
            bool reallyHoled = false;

            if (ballFlightSimulator.WasHoled)
            {
                MarkHoled(shooter, playerHoleStrokes[shooter] == 1 ? "HOLE IN ONE!" : "IN THE HOLE");
                reallyHoled = true;
            }
            else if (!ApplyPenalties(rest))
            {
                playerPositions[shooter] = rest;
                float gimmie = GimmieMeters();
                float toPin = HorizontalDistance(rest, pinPosition);
                if (gimmie > 0f && toPin <= gimmie)
                {
                    AddStrokes(shooter, 1);
                    MarkHoled(shooter, "GIMME");
                }
                else if (AppSettings.Current.autoPutt && ballFlightSimulator.IsOnGreen(rest))
                {
                    // AUTO PUTT: putts are counted from the distance to the hole (1-putt and 2-putt circles).
                    int putts = AutoPutts(toPin);
                    if (putts > 0)
                    {
                        AddStrokes(shooter, putts);
                        MarkHoled(shooter, "AUTO PUTT  •  " + putts + (putts == 1 ? " PUTT" : " PUTTS"));
                    }
                    else ShowBanner("OUTSIDE THE 2 PUTT CIRCLE  •  PUTT IT  •  " + Units.DistanceText(toPin, "0.0"));
                }
            }

            shotFinished = true;
            status = "SHOT COMPLETE";

            if (TeamAfterShot(shooter, reallyHoled)) return; // the team picks its ball first
            ContinueAfterShot();
        }

        /// <summary>Putts for a ball this far from the hole, or 0 when the player putts it themselves.</summary>
        private static int AutoPutts(float toPin)
        {
            AppSettings a = AppSettings.Current;
            if (toPin <= a.autoPuttOneMeters) return 1;
            if (toPin <= Mathf.Max(a.autoPuttTwoMeters, a.autoPuttOneMeters + 0.5f)) return 2;
            return a.autoPuttBeyond == 0 ? 3 : 0;
        }

        /// <summary>After a shot: hole complete, next player, or the same player again.</summary>
        private void ContinueAfterShot()
        {
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

        // ---------------------------------------------------------------- Team formats

        /// <summary>
        /// Team rules after a player's shot. Returns true when the team must now pick its ball
        /// (the CHOOSE panel is shown and play waits).
        /// </summary>
        private bool TeamAfterShot(int p, bool reallyHoled)
        {
            if (!SelectFormat || PracticeMode) return false;
            int t = playerTeam[p];
            if (teamRoundActive[t])
            {
                hitThisRound[p] = true;
                if (reallyHoled || teamMembers[t].Count == 1)
                {
                    ChooseShot(t, p, true, reallyHoled ? "IN THE HOLE" : null);
                    return false;
                }
                foreach (int m in teamMembers[t])
                    if (!hitThisRound[m]) return false; // team-mates still to hit from the spot
                pendingSelection = t;
                waitingForNextPlayer = false;
                status = "TEAM " + GameFormats.TeamLetters[t] + "  •  CHOOSE THE BALL";
                ShowBanner("TEAM " + GameFormats.TeamLetters[t] + "  •  PICK THE " + (teamDriveChosen[t] ? "BEST SHOT" : "BEST DRIVE"));
                return true;
            }
            if (format.AlternateAfterDrive && teamDriveChosen[t])
            {
                // Greensomes: one ball - every stroke is the team's, then the partner hits.
                SyncTeam(t, p);
                teamNextHitter[t] = NextMember(t, p);
            }
            return false;
        }

        /// <summary>The team plays on from this member's ball.</summary>
        private void ChooseShot(int t, int chosen, bool automatic, string reason = null)
        {
            bool drive = !teamDriveChosen[t];
            SyncTeam(t, chosen);
            lastShotStrokes = 0; // no mulligan once the ball is picked
            pendingSelection = -1;
            teamDriveChosen[t] = true;
            foreach (int m in teamMembers[t]) hitThisRound[m] = false;
            bool holed = playerHoled[chosen];
            if (format.SelectEveryShot && !holed)
            {
                teamRoundActive[t] = true; // scramble: everyone hits again from the chosen spot
            }
            else
            {
                teamRoundActive[t] = false;
                teamNextHitter[t] = format.AlternateAfterDrive && !holed ? NextMember(t, chosen) : -1;
            }

            string team = "TEAM " + GameFormats.TeamLetters[t];
            string text = holed
                ? team + " HOLED OUT WITH " + playerNames[chosen].ToUpperInvariant() + "'S BALL  •  " + ScoreName(playerHoleStrokes[chosen] - CurrentPar)
                : team + " PLAYS " + playerNames[chosen].ToUpperInvariant() + "'S " + (drive ? "DRIVE" : "SHOT") + "  •  " + Units.DistanceText(HorizontalDistance(playerPositions[chosen], pinPosition)) + " TO THE PIN";
            if (!string.IsNullOrEmpty(reason) && reason != "IN THE HOLE") text = reason + "     " + text;
            ShowBanner(text);

            if (!automatic)
            {
                shotFinished = true;
                ContinueAfterShot();
            }
        }

        /// <summary>Every team member takes over this member's ball: position, strokes, holed.</summary>
        private void SyncTeam(int t, int from)
        {
            foreach (int m in teamMembers[t])
            {
                if (m == from) continue;
                int delta = playerHoleStrokes[from] - playerHoleStrokes[m];
                playerHoleStrokes[m] += delta;
                playerTotalStrokes[m] += delta;
                playerPositions[m] = playerPositions[from];
                playerPreviousPositions[m] = playerPositions[from];
                playerHoled[m] = playerHoled[from];
            }
        }

        private int NextMember(int t, int p)
        {
            List<int> members = teamMembers[t];
            int i = members.IndexOf(p);
            return members[(i + 1) % members.Count];
        }

        /// <summary>Best ball for the team: holed with the fewest strokes, else the lowest expected score.</summary>
        private int RecommendedBall(int t)
        {
            int best = teamMembers[t][0];
            float bestScore = float.MaxValue;
            foreach (int m in teamMembers[t])
            {
                float score = ExpectedScore(m);
                float tie = playerHoled[m] ? 0f : HorizontalDistance(playerPositions[m], pinPosition) * 0.0001f;
                if (score + tie < bestScore) { bestScore = score + tie; best = m; }
            }
            return best;
        }

        private float ExpectedScore(int m)
        {
            if (playerHoled[m]) return playerHoleStrokes[m];
            float d = HorizontalDistance(playerPositions[m], pinPosition);
            bool green = ballFlightSimulator != null && ballFlightSimulator.IsOnGreen(playerPositions[m]);
            float remaining = green ? (d <= 1f ? 1f : d <= 8f ? 1.6f : 2.1f) : (d <= 30f ? 2.3f : d <= 180f ? 2.9f : 3.8f);
            return playerHoleStrokes[m] + remaining;
        }

        /// <summary>May this player hit now (format rules)?</summary>
        private bool CanHit(int p)
        {
            if (playerHoled[p]) return false;
            if (!SelectFormat || PracticeMode) return true;
            int t = playerTeam[p];
            if (teamRoundActive[t]) return !hitThisRound[p];
            if (format.AlternateAfterDrive) return teamNextHitter[t] == p;
            return true; // shamble after the drive: own ball
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
            if (SelectFormat && !PracticeMode)
            {
                int t = playerTeam[p];
                if (teamRoundActive[t])
                {
                    hitThisRound[p] = false;
                    if (pendingSelection == t) pendingSelection = -1;
                }
                else if (format.AlternateAfterDrive && teamDriveChosen[t])
                {
                    // One team ball: the stroke comes off the whole team and the same player hits again.
                    foreach (int m in teamMembers[t])
                    {
                        if (m == p) continue;
                        playerHoleStrokes[m] = Mathf.Max(0, playerHoleStrokes[m] - lastShotStrokes);
                        playerTotalStrokes[m] = Mathf.Max(0, playerTotalStrokes[m] - lastShotStrokes);
                        playerHoled[m] = false;
                        playerPositions[m] = playerPreviousPositions[p];
                    }
                    teamNextHitter[t] = p;
                }
            }
            playerHoleStrokes[p] = Mathf.Max(0, playerHoleStrokes[p] - lastShotStrokes);
            playerTotalStrokes[p] = Mathf.Max(0, playerTotalStrokes[p] - lastShotStrokes);
            lastShotStrokes = 0;
            playerHoled[p] = false;
            playerPositions[p] = playerPreviousPositions[p];
            activePlayerIndex = p;
            shotFinished = false;
            waitingForNextPlayer = false;
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

        /// <summary>
        /// Who plays next: the player furthest from the hole among those allowed to hit (format
        /// rules: team rounds, alternate shot), with the PLAY OUT HOLE / PUTT OUT rotation styles.
        /// </summary>
        private int NextPlayerToHit()
        {
            int style = AppSettings.Current.rotationStyle;
            bool teamTurn = SelectFormat && !PracticeMode && activePlayerIndex < playerNames.Length &&
                            (teamRoundActive[playerTeam[activePlayerIndex]] || format.AlternateAfterDrive);
            bool activeInPlay = activePlayerIndex < playerNames.Length && CanHit(activePlayerIndex) && playerHoleStrokes[activePlayerIndex] > 0;

            if (!teamTurn)
            {
                // PLAY OUT HOLE: the player keeps hitting until holed, then the next player in order.
                if (style == 1)
                {
                    if (activeInPlay) return activePlayerIndex;
                    for (int i = 0; i < playerNames.Length; i++) if (CanHit(i)) return i;
                    return activePlayerIndex;
                }

                // PUTT OUT: once on the green a player keeps putting until holed.
                if (style == 2 && activeInPlay && ballFlightSimulator != null && ballFlightSimulator.IsOnGreen(playerPositions[activePlayerIndex]))
                    return activePlayerIndex;
            }

            int best = activePlayerIndex;
            float bestDistance = -1f;
            for (int i = 0; i < playerNames.Length; i++)
            {
                if (!CanHit(i)) continue;
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
            if (savedNames.Length > CourseSession.MaxPlayers) Array.Resize(ref savedNames, CourseSession.MaxPlayers);

            playerNames = savedNames;
            playerPositions = new Vector3[playerNames.Length];
            playerPreviousPositions = new Vector3[playerNames.Length];
            playerTotalStrokes = new int[playerNames.Length];
            playerHoleStrokes = new int[playerNames.Length];
            playerHoled = new bool[playerNames.Length];
            mulligansUsed = new int[playerNames.Length];
            activePlayerIndex = 0;
            SetUpTeams();
        }

        /// <summary>Format and teams of this round; an impossible combination plays as stroke play.</summary>
        private void SetUpTeams()
        {
            int n = playerNames.Length;
            format = GameFormats.Current;
            int[] teams = CourseSession.Teams;
            if (format.Team && teams.Length != n) teams = GameFormats.DefaultTeams(n, Mathf.Max(2, format.MinTeamSize));
            string problem = GameFormats.Problem(format, n, teams);
            if (problem != null && !PracticeMode)
            {
                formatWarning = format.Name.ToUpperInvariant() + " NOT POSSIBLE (" + problem.TrimEnd('.') + ")  •  PLAYING " + (n >= 2 && format.Id == GameFormatId.Skins ? "STROKE PLAY" : "STROKE PLAY");
                format = GameFormats.All[0];
            }
            if (!format.Team) teams = new int[n];

            playerTeam = new int[n];
            teamCount = 1;
            for (int i = 0; i < n; i++)
            {
                playerTeam[i] = format.Team ? Mathf.Clamp(teams[i], 0, GameFormats.MaxTeams - 1) : 0;
                teamCount = Mathf.Max(teamCount, playerTeam[i] + 1);
            }
            teamMembers = new List<int>[teamCount];
            for (int t = 0; t < teamCount; t++) teamMembers[t] = new List<int>();
            for (int i = 0; i < n; i++) teamMembers[playerTeam[i]].Add(i);
            hitThisRound = new bool[n];
            teamRoundActive = new bool[teamCount];
            teamDriveChosen = new bool[teamCount];
            teamNextHitter = new int[teamCount];
        }

        private string formatWarning;

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

        // ---------------------------------------------------------------- Format scoring

        /// <summary>Competitors: teams in team formats, otherwise the players.</summary>
        private int SideCount => TeamFormat ? teamCount : playerNames.Length;
        private int SideOf(int player) => TeamFormat ? playerTeam[player] : player;
        private string SideName(int side) => TeamFormat ? "TEAM " + GameFormats.TeamLetters[side] : playerNames[side];
        private Color SideColor(int side) => TeamFormat ? GameFormats.TeamColors[side] : PlayerColor(side);

        /// <summary>A side's score on a finished hole (team formats: the lowest member score), 0 when not played.</summary>
        private int SideScore(int[] scores, int side)
        {
            if (scores == null) return 0;
            if (!TeamFormat) return side < scores.Length ? Mathf.Max(0, scores[side]) : 0;
            int best = int.MaxValue;
            foreach (int m in teamMembers[side])
            {
                if (m >= scores.Length || scores[m] <= 0) return 0;
                best = Mathf.Min(best, scores[m]);
            }
            return best == int.MaxValue ? 0 : best;
        }

        /// <summary>The side's score on the hole being played, once every member has finished it.</summary>
        private int LiveSideScore(int side)
        {
            if (roundComplete) return 0;
            if (!TeamFormat) return playerHoled[side] ? playerHoleStrokes[side] : 0;
            int best = int.MaxValue;
            foreach (int m in teamMembers[side])
            {
                if (!playerHoled[m]) return 0;
                best = Mathf.Min(best, playerHoleStrokes[m]);
            }
            return best == int.MaxValue ? 0 : best;
        }

        private bool HoleRecorded(int h) => h < scorecard.Count && scorecard[h] != null;

        /// <summary>Strokes against par for a side over finished holes (and the current hole once finished).</summary>
        private int SideRelativeToPar(int side)
        {
            int total = 0;
            for (int h = FirstHole; h < EndHole; h++)
            {
                int score = HoleRecorded(h) ? SideScore(scorecard[h], side) : (h == holeIndex ? LiveSideScore(side) : 0);
                if (h == holeIndex && !roundComplete && HoleRecorded(h)) score = SideScore(scorecard[h], side);
                if (score > 0) total += score - ParForHole(h);
            }
            return total;
        }

        private int SideStrokes(int side)
        {
            int total = 0;
            for (int h = FirstHole; h < EndHole; h++)
            {
                int score = HoleRecorded(h) ? SideScore(scorecard[h], side) : (h == holeIndex ? LiveSideScore(side) : 0);
                total += score;
            }
            return total;
        }

        /// <summary>Stableford points of a player over finished holes (and the current hole once holed).</summary>
        private int StablefordTotal(int player)
        {
            int total = 0;
            for (int h = FirstHole; h < EndHole; h++)
            {
                int score = HoleRecorded(h) ? SideScore(scorecard[h], player) : (h == holeIndex ? LiveSideScore(player) : 0);
                total += GameFormats.StablefordPoints(score, ParForHole(h));
            }
            return total;
        }

        /// <summary>Skins won per player, the skins carried into the next hole, and last hole's result.</summary>
        private int[] SkinsWon(out int carry, out int lastWinner, out int lastValue)
        {
            var won = new int[playerNames.Length];
            carry = 0;
            lastWinner = -1;
            lastValue = 0;
            for (int h = FirstHole; h < EndHole; h++)
            {
                if (!HoleRecorded(h)) continue;
                int w = HoleWinner(scorecard[h]);
                lastValue = carry + 1;
                if (w >= 0 && w < won.Length)
                {
                    won[w] += lastValue;
                    carry = 0;
                    lastWinner = w;
                }
                else
                {
                    carry++;
                    lastWinner = -1;
                }
            }
            return won;
        }

        /// <summary>Running format status for the HUD (null = nothing extra to show).</summary>
        private string FormatStatusText()
        {
            if (PracticeMode) return null;
            switch (format.Id)
            {
                case GameFormatId.MatchPlay:
                    return MatchPlay ? "MATCH PLAY  •  " + MatchStatusText() : null;
                case GameFormatId.Stableford:
                {
                    var parts = new List<string>();
                    for (int p = 0; p < playerNames.Length; p++) parts.Add(playerNames[p] + " " + StablefordTotal(p));
                    return "STABLEFORD PTS  •  " + string.Join("   ", parts);
                }
                case GameFormatId.Skins:
                {
                    int[] won = SkinsWon(out int carry, out _, out _);
                    var parts = new List<string>();
                    for (int p = 0; p < playerNames.Length; p++) parts.Add(playerNames[p] + " " + won[p]);
                    return "SKINS  •  " + string.Join("   ", parts) + "   •   THIS HOLE " + (carry + 1);
                }
                default:
                    if (!TeamFormat) return null;
                    var teams = new List<string>();
                    for (int t = 0; t < teamCount; t++) teams.Add(GameFormats.TeamLetters[t] + " " + FormatScore(SideRelativeToPar(t)));
                    return format.Name.ToUpperInvariant() + "  •  " + string.Join("   ", teams);
            }
        }

        /// <summary>Who leads / won, for the scorecard.</summary>
        private string ResultText()
        {
            string lead = roundComplete ? "WINNER" : "LEADER";
            switch (format.Id)
            {
                case GameFormatId.MatchPlay:
                    if (MatchPlay) return MatchStatusText();
                    break;
                case GameFormatId.Stableford:
                {
                    int best = 0;
                    for (int p = 1; p < playerNames.Length; p++) if (StablefordTotal(p) > StablefordTotal(best)) best = p;
                    return lead + ":  " + playerNames[best].ToUpperInvariant() + "  •  " + StablefordTotal(best) + " POINTS";
                }
                case GameFormatId.Skins:
                {
                    int[] won = SkinsWon(out int carry, out _, out _);
                    int best = 0;
                    for (int p = 1; p < won.Length; p++) if (won[p] > won[best]) best = p;
                    return lead + ":  " + playerNames[best].ToUpperInvariant() + "  •  " + won[best] + " SKINS" + (carry > 0 ? "   (" + carry + " CARRIED OVER)" : "");
                }
            }
            int bestSide = 0;
            for (int sd = 1; sd < SideCount; sd++) if (SideRelativeToPar(sd) < SideRelativeToPar(bestSide)) bestSide = sd;
            string name = TeamFormat ? SideName(bestSide) + " (" + TeamNames(bestSide) + ")" : playerNames[bestSide].ToUpperInvariant();
            return lead + ":  " + name + "  •  " + SideStrokes(bestSide) + " (" + FormatScore(SideRelativeToPar(bestSide)) + ")";
        }

        private string TeamNames(int t)
        {
            var names = new List<string>();
            foreach (int m in teamMembers[t]) names.Add(playerNames[m]);
            return string.Join(" & ", names);
        }

        private void StartHole()
        {
            holeIndex = PracticeMode ? Mathf.Clamp(holeIndex, 0, CourseHoles - 1) : Mathf.Clamp(holeIndex, FirstHole, EndHole - 1);
            activePlayerIndex = 0;
            shotFinished = false;
            waitingForNextPlayer = false;
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
                hitThisRound[i] = false;
            }
            pendingSelection = -1;
            for (int t = 0; t < teamCount; t++)
            {
                // Scramble / shamble / greensomes: everyone tees off, the team picks the drive.
                teamRoundActive[t] = SelectFormat && !PracticeMode;
                teamDriveChosen[t] = false;
                teamNextHitter[t] = -1;
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
            if (formatWarning != null) { holeText = formatWarning + "     " + holeText; formatWarning = null; }
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
            ResetAimPoint();
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
            Vector3 target = aimPointer != null && aimPointer.Visible ? aimPointer.Point : AimTarget(from);
            Vector3 direction = target - from;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            if (ballFlightSimulator != null) ballFlightSimulator.AimYawDegrees = yaw;
            Vector3 aim = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            presentation?.SetAddress(from, aim, snapCamera);
        }

        private string lastClubForAim;

        /// <summary>A new club moves the aim target to that club's distance (until the player moves it).</summary>
        private void LateUpdate()
        {
            if (aimPointer == null || ball == null || ballFlightSimulator == null || ballFlightSimulator.IsInFlight) return;
            string club = GolfSimZA.Core.ActiveClub.Resolve();
            if (club == lastClubForAim) return;
            lastClubForAim = club;
            if (aimMoved || !aimPointer.Visible) return;
            ResetAimPoint();
            ApplyAim(ball.position, false);
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
            if (format.Id == GameFormatId.Stableford)
            {
                var parts = new List<string>();
                for (int p = 0; p < playerNames.Length; p++)
                    parts.Add(playerNames[p].ToUpperInvariant() + " " + GameFormats.StablefordPoints(scores[p], CurrentPar) + " PTS");
                holeResultText = "STABLEFORD  •  " + string.Join("  •  ", parts);
            }
            else if (format.Id == GameFormatId.Skins && playerNames.Length >= 2)
            {
                SkinsWon(out int carry, out int winner, out int value);
                holeResultText = winner >= 0
                    ? playerNames[winner].ToUpperInvariant() + " WINS " + (value == 1 ? "THE SKIN" : value + " SKINS")
                    : "TIED  •  SKIN CARRIES OVER  •  " + (carry + 1) + " ON THE NEXT HOLE";
            }
            else if (TeamFormat)
            {
                var parts = new List<string>();
                int best = int.MaxValue, bestTeam = -1;
                bool tie = false;
                for (int t = 0; t < teamCount; t++)
                {
                    int sc = SideScore(scores, t);
                    parts.Add("TEAM " + GameFormats.TeamLetters[t] + " " + sc);
                    if (sc < best) { best = sc; bestTeam = t; tie = false; }
                    else if (sc == best) tie = true;
                }
                holeResultText = (teamCount > 1 ? (tie ? "HOLE HALVED  •  " : "TEAM " + GameFormats.TeamLetters[bestTeam] + " WINS THE HOLE  •  ") : "") + string.Join("  •  ", parts);
            }
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
                DrawGroundLabels();
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
            if (TeamFormat) shot = "TEAM " + GameFormats.TeamLetters[playerTeam[a]] + "  •  " + shot;
            GUI.Label(new Rect(r.x + 18f, r.y + 34f, w - scoreW - 26f, 18f), shot, playerMetaStyle);

            Rect score = new Rect(r.xMax - scoreW, r.y, scoreW, r.height);
            GUI.color = color;
            GUI.DrawTexture(score, whiteTexture);
            GUI.color = Color.white;
            DrawScoreBox(score, a);

            // Other players
            float rowH = 26f;
            int others = playerNames.Length - 1;
            string formatStatus = FormatStatusText();
            float matchH = formatStatus != null ? 24f : 0f;
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
                if (TeamFormat)
                {
                    GUI.color = GameFormats.TeamColors[playerTeam[i]];
                    GUI.Label(new Rect(list.x + w * 0.44f, ry, 20f, rowH), GameFormats.TeamLetters[playerTeam[i]], playerStyle);
                    GUI.color = Color.white;
                }
                GUI.Label(new Rect(list.x + w * 0.52f, ry, 50f, rowH), PracticeMode ? "" : ScoreText(i), playerStyle);
                GUI.Label(new Rect(list.x + w * 0.62f, ry, w * 0.36f - 8f, rowH), playerHoled[i] ? "IN" : Units.DistanceText(DistanceToPin(i)), new GUIStyle(playerStyle) { alignment = TextAnchor.MiddleRight });
                ry += rowH;
            }
            if (formatStatus != null)
                GUI.Label(new Rect(list.x + 12f, ry, w - 20f, 20f), GolfSimTheme.Ellipsize(formatStatus, smallStyle, w - 20f), new GUIStyle(smallStyle) { fontStyle = FontStyle.Bold, normal = { textColor = GolfSimTheme.Gold } });
        }

        /// <summary>Score of a player in this format: points, skins or strokes against par (team formats: the team's).</summary>
        private string ScoreText(int player)
        {
            switch (format.Id)
            {
                case GameFormatId.Stableford: return StablefordTotal(player) + " PT";
                case GameFormatId.Skins: return SkinsWon(out _, out _, out _)[player] + " SK";
            }
            return FormatScore(TeamFormat ? SideRelativeToPar(SideOf(player)) : GetPlayerRelativeToPar(player));
        }

        private void DrawScoreBox(Rect box, int player)
        {
            if (PracticeMode) { GUI.Label(box, "–", scoreBoxStyle); return; }
            if (format.Id == GameFormatId.Stableford || format.Id == GameFormatId.Skins)
            {
                int value = format.Id == GameFormatId.Stableford ? StablefordTotal(player) : SkinsWon(out _, out _, out _)[player];
                GUI.Label(new Rect(box.x, box.y + 4f, box.width, 34f), value.ToString(), scoreBoxStyle);
                GUI.Label(new Rect(box.x, box.y + 38f, box.width, 16f), format.Id == GameFormatId.Stableford ? "PTS" : "SKINS", new GUIStyle(centerStyle) { fontSize = 9 });
                return;
            }
            GUI.Label(box, ScoreText(player), scoreBoxStyle);
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
            float angle = AimAngleFromLine();
            string aim = (Mathf.Abs(angle) < 0.5f ? "AIM ON LINE" : "AIM " + Mathf.Abs(angle).ToString("0") + "° " + (angle < 0f ? "LEFT" : "RIGHT"))
                         + (aimPointer != null && aimPointer.Visible ? "  •  " + Units.DistanceText(aimPointer.Distance) : "");
            GUI.Label(new Rect(x - 170f, y + 42f, w + 340f, 18f), status + "   •   " + aim + "   (drag gold target / right-click ground)", new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } });
        }

        /// <summary>Labels on the ground: "1 PUTT" / "2 PUTTS" on the circles and the aim target distance.</summary>
        private void DrawGroundLabels()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            AppSettings s = AppSettings.Current;
            // Circle labels only when close enough to read them (from far away they pile up on the flag).
            if (markers != null && markers.PuttCirclesVisible && Vector3.Distance(cam.transform.position, pinPosition) < 70f)
            {
                Vector3 right = cam.transform.right;
                right.y = 0f;
                right = right.sqrMagnitude > 0.001f ? right.normalized : Vector3.right;
                GroundLabel(cam, pinPosition + right * s.autoPuttOneMeters, "1 PUTT", CourseMarkers.OnePuttColor);
                GroundLabel(cam, pinPosition + right * Mathf.Max(s.autoPuttTwoMeters, s.autoPuttOneMeters + 0.5f), "2 PUTTS", CourseMarkers.TwoPuttColor);
            }
            if (aimPointer != null && aimPointer.Visible && ball != null)
            {
                float rise = aimPointer.Point.y - ball.position.y;
                string text = Units.DistanceText(aimPointer.Distance) + (Mathf.Abs(rise) >= 1f ? (rise > 0f ? "  ▲" : "  ▼") + Units.Distance(Mathf.Abs(rise)).ToString("0") : "");
                GroundLabel(cam, aimPointer.Point, text, GolfSimTheme.Gold, 15, true);
            }
        }

        private void GroundLabel(Camera cam, Vector3 world, string text, Color color, int size = 11, bool below = false)
        {
            world.y = Mathf.Max(world.y, GroundProbe.HeightAt(world));
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0.5f || sp.x < 0f || sp.x > Screen.width || sp.y < 0f || sp.y > Screen.height) return;
            var style = new GUIStyle(centerStyle) { fontSize = size, normal = { textColor = color } };
            Vector2 sz = style.CalcSize(new GUIContent(text));
            float top = below ? Screen.height - sp.y + 8f : Screen.height - sp.y - sz.y - 6f;
            Rect r = new Rect(sp.x - sz.x * 0.5f - 6f, top, sz.x + 12f, sz.y + 4f);
            GUI.Box(r, GUIContent.none, darkPanel);
            GUI.Label(r, text, style);
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
            if (roundComplete || waitingForNextPlayer || pendingSelection >= 0 || AllHoled() || (presentation != null && presentation.IsFlyingOver)) return;
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

            // Click / drag on the hole map to put the aim target there.
            Event e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && area.Contains(e.mousePosition)
                && aimPointer != null && aimPointer.Visible && ball != null && holeMap != null && holeMap.HasImage
                && aimPointer.CanInteract != null && aimPointer.CanInteract()
                && holeMap.FromMap(e.mousePosition, area, out Vector3 mapPoint))
            {
                aimPointer.Set(ball.position, mapPoint);
                OnAimMoved(aimPointer.Point);
                e.Use();
            }

            GUI.BeginClip(area);
            Vector2 offset = new Vector2(area.x, area.y);
            for (int i = 1; i < points.Count; i++)
                DrawDots(toMap(points[i - 1]) - offset, toMap(points[i]) - offset, whiteTexture);

            if (aimPointer != null && aimPointer.Visible && ball != null)
            {
                Vector2 from = toMap(ball.position) - offset, to = toMap(aimPointer.Point) - offset;
                GUI.color = GolfSimTheme.Gold;
                DrawDots(from, to, whiteTexture);
                GUI.DrawTexture(new Rect(to.x - 5f, to.y - 5f, 10f, 10f), whiteTexture);
                GUI.color = Color.white;
            }

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

            if (pendingSelection >= 0)
            {
                DrawChooseBall(pendingSelection);
                return;
            }

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
                    int p = activePlayerIndex;
                    AddStrokes(p, 1);
                    MarkHoled(p, "PICKED UP");
                    if (SelectFormat && !PracticeMode && (teamRoundActive[playerTeam[p]] || format.AlternateAfterDrive))
                    {
                        // Team ball: the whole team picks up.
                        int t = playerTeam[p];
                        SyncTeam(t, p);
                        teamRoundActive[t] = false;
                        foreach (int m in teamMembers[t]) hitThisRound[m] = false;
                        if (pendingSelection == t) pendingSelection = -1;
                    }
                    shotFinished = true;
                    if (AllHoled()) status = "HOLE COMPLETE";
                    else AdvancePlayer();
                }
            }
        }

        /// <summary>CHOOSE panel: the team picks which ball it plays on from.</summary>
        private void DrawChooseBall(int t)
        {
            List<int> members = teamMembers[t];
            int recommended = RecommendedBall(t);
            float w = Mathf.Min(620f, Screen.width - 40f);
            float rowH = 46f;
            float h = 70f + members.Count * (rowH + 6f) + 26f;
            Rect r = new Rect((Screen.width - w) * 0.5f, Screen.height - ClubBar.Height - h - 24f, w, h);
            GUI.Box(r, GUIContent.none, darkPanel);
            GUI.color = GameFormats.TeamColors[t];
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 5f), whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 16f, r.y + 12f, w - 32f, 26f), "TEAM " + GameFormats.TeamLetters[t] + "  •  " + format.Name.ToUpperInvariant() + "  •  PICK THE " + (teamDriveChosen[t] ? "BEST SHOT" : "BEST DRIVE"), new GUIStyle(titleStyle) { fontSize = 17 });
            GUI.Label(new Rect(r.x + 16f, r.y + 38f, w - 32f, 20f), "Every ball is marked on the hole map. The team plays on from the ball you pick.", smallStyle);
            float y = r.y + 64f;
            foreach (int m in members)
            {
                string where = playerHoled[m] ? "IN THE HOLE  •  " + playerHoleStrokes[m] + (playerHoleStrokes[m] == 1 ? " STROKE" : " STROKES")
                    : Units.DistanceText(HorizontalDistance(playerPositions[m], pinPosition)) + " TO THE PIN" + (ballFlightSimulator != null && ballFlightSimulator.IsOnGreen(playerPositions[m]) ? "  •  ON THE GREEN" : "")
                      + "  •  " + playerHoleStrokes[m] + (playerHoleStrokes[m] == 1 ? " STROKE" : " STROKES");
                Rect b = new Rect(r.x + 16f, y, w - 32f, rowH);
                if (GUI.Button(b, GUIContent.none, m == recommended ? actionStyle : GolfSimTheme.Button)) { ChooseShot(t, m, false); return; }
                GUI.color = PlayerColor(m);
                GUI.DrawTexture(new Rect(b.x + 8f, b.y + 8f, 6f, b.height - 16f), whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(b.x + 24f, b.y, b.width * 0.34f, b.height), playerNames[m].ToUpperInvariant(), new GUIStyle(playerStyle) { fontSize = 15, normal = { textColor = m == recommended ? Color.white : PlayerColor(m) } });
                GUI.Label(new Rect(b.x + b.width * 0.34f, b.y, b.width * 0.66f - 12f, b.height), where + (m == recommended ? "   ★ BEST" : ""), new GUIStyle(playerStyle) { alignment = TextAnchor.MiddleRight, fontSize = 12 });
                y += rowH + 6f;
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
            float cellW = Mathf.Clamp((Screen.width - 340f) / (holes + 2), 30f, 52f);
            float extraW = format.Id == GameFormatId.Stableford || format.Id == GameFormatId.Skins ? 70f : 0f;
            float tableW = 160f + cellW * (holes + 1) + 40f + extraW;
            int[] skins = format.Id == GameFormatId.Skins ? SkinsWon(out _, out _, out _) : null;
            float x = (Screen.width - tableW) * 0.5f;
            float y = Screen.height * 0.18f;

            GUI.Label(new Rect(x, y - 50f, tableW, 34f), (roundComplete ? "ROUND COMPLETE  •  " : "SCORECARD  •  ") + (UsingCourse ? course.name : CourseSession.CourseName) + "  •  " + format.Name.ToUpperInvariant(), bannerStyle);

            GUI.Label(new Rect(x, y, 160f, 24f), "HOLE", cellHeaderStyle);
            for (int h = 0; h < holes; h++) GUI.Label(new Rect(x + 160f + cellW * h, y, cellW, 24f), (first + h + 1).ToString(), cellHeaderStyle);
            GUI.Label(new Rect(x + 160f + cellW * holes, y, cellW + 40f, 24f), "TOT", cellHeaderStyle);
            if (extraW > 0f) GUI.Label(new Rect(x + 200f + cellW * (holes + 1), y, extraW, 24f), format.Id == GameFormatId.Stableford ? "PTS" : "SKINS", cellHeaderStyle);

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
                GUI.Label(new Rect(x, y, 160f, 26f), (TeamFormat ? GameFormats.TeamLetters[playerTeam[p]] + "  " : "") + playerNames[p], new GUIStyle(cellStyle) { normal = { textColor = PlayerColor(p) } });
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
                GUI.Label(new Rect(x + 160f + cellW * holes, y, cellW + 40f, 26f), total + " (" + FormatScore(ScoredStrokes(p) - parPlayed) + ")", cellStyle);
                if (extraW > 0f)
                    GUI.Label(new Rect(x + 200f + cellW * (holes + 1), y, extraW, 26f), format.Id == GameFormatId.Stableford ? StablefordTotal(p).ToString() : skins[p].ToString(), new GUIStyle(cellStyle) { normal = { textColor = GolfSimTheme.Gold } });
            }

            // Team formats: one line per team with the score that counts on each hole.
            if (TeamFormat)
                for (int t = 0; t < teamCount; t++)
                {
                    y += 30f;
                    GUI.Label(new Rect(x, y, 160f, 26f), "TEAM " + GameFormats.TeamLetters[t], new GUIStyle(cellStyle) { normal = { textColor = GameFormats.TeamColors[t] } });
                    for (int c = 0; c < holes; c++)
                    {
                        int h = first + c;
                        int sc = HoleRecorded(h) ? SideScore(scorecard[h], t) : (h == holeIndex ? LiveSideScore(t) : 0);
                        GUI.Label(new Rect(x + 160f + cellW * c, y, cellW, 26f), sc > 0 ? sc.ToString() : "-", new GUIStyle(cellStyle) { normal = { textColor = GameFormats.TeamColors[t] } });
                    }
                    GUI.Label(new Rect(x + 160f + cellW * holes, y, cellW + 40f, 26f), SideStrokes(t) + " (" + FormatScore(SideRelativeToPar(t)) + ")", new GUIStyle(cellStyle) { normal = { textColor = GameFormats.TeamColors[t] } });
                }

            // Skins: who won each hole ("–" = carried over).
            if (format.Id == GameFormatId.Skins && playerNames.Length >= 2)
            {
                y += 30f;
                GUI.Label(new Rect(x, y, 160f, 26f), "SKIN", cellHeaderStyle);
                for (int c = 0; c < holes; c++)
                {
                    int h = first + c;
                    string cell = "";
                    if (HoleRecorded(h))
                    {
                        int w = HoleWinner(scorecard[h]);
                        cell = w >= 0 ? (playerNames[w].Length > 3 ? playerNames[w].Substring(0, 3) : playerNames[w]).ToUpperInvariant() : "→";
                    }
                    GUI.Label(new Rect(x + 160f + cellW * c, y, cellW, 26f), cell, cellHeaderStyle);
                }
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

            if (!MatchPlay && !PracticeMode)
            {
                y += 34f;
                GUI.Label(new Rect(x, y, tableW, 26f), ResultText(), new GUIStyle(cellHeaderStyle) { alignment = TextAnchor.MiddleLeft, fontSize = 13, normal = { textColor = GolfSimTheme.Gold } });
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
