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
        private readonly int[] demoPar = { 4, 4, 5, 3, 4, 4, 3, 5, 4, 4, 5, 4, 3, 4, 5, 4, 3, 5 };
        private readonly int[] demoDistance = { 360, 385, 470, 165, 395, 410, 175, 505, 375, 390, 495, 400, 175, 420, 510, 405, 180, 525 };

        [SerializeField] private SimulatorController simulatorController;
        [SerializeField] private BallFlightSimulator ballFlightSimulator;
        [SerializeField] private Transform ball;
        [SerializeField] private Transform pin;
        [SerializeField] private Transform green;

        private GUIStyle smallPanel, darkPanel, titleStyle, smallStyle, valueStyle, rightValueStyle;
        private GUIStyle holeStyle, courseStyle, clubStyle, activeClubStyle, actionStyle;
        private GUIStyle playerStyle, activePlayerStyle, playerMetaStyle, distanceStyle, mapStyle;
        private GUIStyle centerStyle, bannerStyle, cellStyle, cellHeaderStyle;
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
        private string banner;
        private float bannerUntil;

        private string[] playerNames = { "Player 1" };
        private Vector3[] playerPositions = { Vector3.zero };
        private Vector3[] playerPreviousPositions = { Vector3.zero };
        private int[] playerTotalStrokes = { 0 };
        private int[] playerHoleStrokes = { 0 };
        private bool[] playerHoled = { false };
        private List<int[]> scorecard = new List<int[]>();

        private readonly Color dark = new Color(0.015f, 0.055f, 0.065f, 0.86f);
        private readonly Color darker = new Color(0.005f, 0.025f, 0.03f, 0.92f);
        private readonly Color blue = new Color(0.03f, 0.48f, 0.82f, 0.95f);
        private readonly Color blueBright = new Color(0.08f, 0.64f, 1f, 1f);
        private readonly Color muted = new Color(0.76f, 0.83f, 0.85f, 1f);

        private bool UsingCourse => course != null;
        private int HoleCount => UsingCourse ? Mathf.Min(course.HoleCount, Mathf.Clamp(CourseSession.RoundLength, 1, 18)) : Mathf.Clamp(CourseSession.RoundLength, 1, 18);
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
            holeIndex = 0;

            if (ballFlightSimulator == null) ballFlightSimulator = GetComponent<BallFlightSimulator>();
            presentation = GetComponent<FlightPresentation>();
            tracer = GetComponent<ShotTracer>();

            courseLoader = GetComponent<CourseLoader>();
            if (courseLoader == null) courseLoader = gameObject.AddComponent<CourseLoader>();
            courseLoader.Loaded += OnCourseLoaded;
            courseLoader.Failed += OnCourseFailed;

            if (CourseSession.IsImportedCourse && !GolfSimZA.Players.ClubMappingSession.IsActive)
            {
                waitingForCourse = true;
                status = "LOADING COURSE";
            }
            else
            {
                StartHole();
            }
        }

        private void OnDestroy()
        {
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
                if (gimmie > 0f && HorizontalDistance(rest, pinPosition) <= gimmie)
                {
                    AddStrokes(activePlayerIndex, 1);
                    MarkHoled(activePlayerIndex, "GIMME");
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
        }

        private bool AllHoled()
        {
            for (int i = 0; i < playerHoled.Length; i++) if (!playerHoled[i]) return false;
            return true;
        }

        /// <summary>The player furthest from the hole plays next (players who holed out are skipped).</summary>
        private int NextPlayerToHit()
        {
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
            activePlayerIndex = 0;
        }

        private void StartHole()
        {
            holeIndex = Mathf.Clamp(holeIndex, 0, HoleCount - 1);
            activePlayerIndex = 0;
            shotFinished = false;
            waitingForNextPlayer = false;
            aimOffsetDegrees = 0f;
            lastObservedShotCount = simulatorController != null && simulatorController.History != null ? simulatorController.History.Count : 0;
            wasInFlight = false;

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
            }
            else
            {
                currentHoleDistance = DemoHoleDistance(holeIndex);
                teePosition = new Vector3(0f, 0f, 0f);
                pinPosition = new Vector3(0f, 0f, currentHoleDistance);
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
                playerPositions[i] = teePosition;
                playerPreviousPositions[i] = teePosition;
                playerHoleStrokes[i] = 0;
                playerHoled[i] = false;
            }

            presentation?.HideLandingMarker();
            tracer?.Clear();
            PrepareActivePlayer(true);
            status = "READY • " + playerNames[activePlayerIndex];
            ShowBanner("HOLE " + (holeIndex + 1) + "  •  PAR " + CurrentPar + "  •  " + currentHoleDistance.ToString("F0") + " m");
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
            shotFinished = false;
            waitingForNextPlayer = false;
            lastObservedShotCount = simulatorController != null && simulatorController.History != null ? simulatorController.History.Count : lastObservedShotCount;
            status = "READY • " + playerNames[activePlayerIndex];
            PrepareActivePlayer(true);
        }

        private void AdvanceHole()
        {
            RecordHoleScores();
            holeIndex++;
            if (holeIndex >= HoleCount)
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
        }

        private void FinishRound()
        {
            roundComplete = true;
            status = "ROUND COMPLETE";
            shotFinished = false;
            waitingForNextPlayer = false;
        }

        private float DemoHoleDistance(int index)
        {
            int safeIndex = Mathf.Clamp(index, 0, demoDistance.Length - 1);
            float teeMultiplier = 1f;
            switch (CourseSession.TeeName)
            {
                case "Red": teeMultiplier = 0.86f; break;
                case "White": teeMultiplier = 0.93f; break;
                case "Blue": teeMultiplier = 1f; break;
                case "Black": teeMultiplier = 1.07f; break;
            }
            return demoDistance[safeIndex] * teeMultiplier;
        }

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
            if (playerIndex < 0 || playerIndex >= playerTotalStrokes.Length || playerTotalStrokes[playerIndex] == 0) return 0;
            int parToCount = 0;
            for (int i = 0; i < holeIndex && i < HoleCount; i++) parToCount += ParForHole(i);
            if (playerHoleStrokes[playerIndex] > 0 && !roundComplete) parToCount += ParForHole(holeIndex);
            return playerTotalStrokes[playerIndex] - parToCount;
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
            actionStyle = MakeButton(12, blueTexture);
            playerStyle = MakeLabel(11, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            activePlayerStyle = MakeLabel(11, FontStyle.Bold, blueBright, TextAnchor.MiddleLeft);
            playerMetaStyle = MakeLabel(9, FontStyle.Normal, muted, TextAnchor.MiddleLeft);
            distanceStyle = MakeLabel(18, FontStyle.Bold, Color.white, TextAnchor.MiddleRight);
            mapStyle = MakeLabel(8, FontStyle.Bold, muted, TextAnchor.MiddleCenter);
            centerStyle = MakeLabel(10, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            bannerStyle = MakeLabel(20, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            cellStyle = MakeLabel(12, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            cellHeaderStyle = MakeLabel(10, FontStyle.Bold, muted, TextAnchor.MiddleCenter);
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

            DrawTopHUD();
            DrawShotCard();
            DrawAimCard();
            DrawHoleInfo();
            DrawPlayerCard();
            DrawClubBar();
            DrawMiniMap();
            DrawShotCompletionAction();
            DrawBanner();
            if (roundComplete) DrawScorecard();
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

        private void DrawTopHUD()
        {
            float margin = Mathf.Max(14f, Screen.width * 0.018f);
            float width = Screen.width - margin * 2f;
            float height = 42f;

            GUI.Box(new Rect(margin, 8f, width, height), GUIContent.none, darkPanel);
            // The MENU button (GameMenuOverlay) sits at the left of this bar.
            GUI.Label(new Rect(margin + 140f, 8f, 135f, height), "GOLFSIM ZA", titleStyle);
            GUI.Label(new Rect(margin + 275f, 8f, width - 520f, height), UsingCourse ? course.name : CourseSession.CourseName, courseStyle);
            GUI.Label(new Rect(margin + width - 245f, 8f, 115f, height), "HOLE " + (holeIndex + 1) + " / " + HoleCount, holeStyle);
            GUI.Label(new Rect(margin + width - 130f, 8f, 115f, height), "PAR " + CurrentPar + "  •  " + currentHoleDistance.ToString("F0") + " m", courseStyle);
        }

        private void DrawShotCard()
        {
            float x = 16f;
            float y = 60f;
            float width = Mathf.Clamp(Screen.width * 0.17f, 205f, 250f);
            float height = 194f;

            GUI.Box(new Rect(x, y, width, height), GUIContent.none, smallPanel);
            GUI.Label(new Rect(x + 10f, y + 8f, width - 20f, 18f), "SHOT", titleStyle);
            GUI.Label(new Rect(x + 10f, y + 29f, width - 20f, 15f), status, smallStyle);

            ShotData shot = simulatorController != null ? simulatorController.LastShot : default(ShotData);
            bool hasShot = shot.IsValid;
            float row = y + 49f;
            CompactMetric("CLUB", hasShot ? shot.ClubName : ActiveClub.Resolve(), ref row, x, width);
            CompactMetric("BALL", hasShot ? (shot.BallSpeedMps * 2.2369363f).ToString("F1") + " mph" : "—", ref row, x, width);
            CompactMetric("LAUNCH", hasShot ? shot.LaunchAngleDeg.ToString("F1") + "°" : "—", ref row, x, width);
            CompactMetric("DIRECTION", hasShot ? shot.LaunchDirectionDeg.ToString("F1") + "°" : "—", ref row, x, width);
            CompactMetric("SPIN", hasShot ? shot.BackSpinRpm.ToString("F0") + " rpm" : "—", ref row, x, width);
            CompactMetric("CARRY", hasShot ? shot.CarryMeters.ToString("F1") + " m" : "—", ref row, x, width);
            CompactMetric("TOTAL", hasShot ? shot.TotalMeters.ToString("F1") + " m" : "—", ref row, x, width);
        }

        private void CompactMetric(string label, string value, ref float y, float x, float width)
        {
            GUI.Label(new Rect(x + 10f, y, width * 0.40f, 17f), label, smallStyle);
            GUI.Label(new Rect(x + width * 0.38f, y - 1f, width * 0.56f, 18f), value, rightValueStyle);
            y += 20f;
        }

        private void DrawAimCard()
        {
            float width = 168f;
            float x = (Screen.width - width) * 0.5f;
            float y = 58f;
            GUI.Box(new Rect(x, y, width, 40f), GUIContent.none, smallPanel);
            string aim = Mathf.Abs(aimOffsetDegrees) < 0.5f ? "AIM  •  ON TARGET" : "AIM  •  " + Mathf.Abs(aimOffsetDegrees).ToString("F0") + "° " + (aimOffsetDegrees < 0f ? "LEFT" : "RIGHT");
            GUI.Label(new Rect(x + 8f, y + 3f, width - 16f, 16f), aim, centerStyle);
            GUI.Label(new Rect(x + 8f, y + 19f, width - 16f, 17f), "← →  adjust   ↑  reset", courseStyle);
        }

        private void DrawHoleInfo()
        {
            float width = Mathf.Clamp(Screen.width * 0.22f, 245f, 320f);
            float x = Screen.width - width - 16f;
            float y = 60f;
            float height = 74f;

            GUI.Box(new Rect(x, y, width, height), GUIContent.none, smallPanel);
            GUI.Label(new Rect(x + 10f, y + 8f, 34f, 38f), (holeIndex + 1).ToString(), holeStyle);
            GUI.Label(new Rect(x + 50f, y + 7f, width - 62f, 22f), UsingCourse ? course.name : CourseSession.CourseName, valueStyle);
            GUI.Label(new Rect(x + 50f, y + 30f, width - 62f, 17f), "PAR " + CurrentPar + "   •   " + currentHoleDistance.ToString("F0") + " m", smallStyle);
            GUI.Label(new Rect(x + 50f, y + 48f, width - 62f, 17f), CourseSession.TeeName.ToUpperInvariant() + " TEES  •  " + CourseSession.PinSetting.ToUpperInvariant() + " PINS", smallStyle);
        }

        private void DrawPlayerCard()
        {
            float width = Mathf.Clamp(Screen.width * 0.18f, 215f, 270f);
            float x = Screen.width - width - 16f;
            float y = 145f;
            float rowHeight = 58f;
            float height = 30f + rowHeight * Mathf.Min(playerNames.Length, 4);

            GUI.Box(new Rect(x, y, width, height), GUIContent.none, smallPanel);
            GUI.Label(new Rect(x + 10f, y + 8f, width - 20f, 18f), "PLAYERS", titleStyle);
            GUI.Label(new Rect(x + 10f, y + 27f, width - 20f, 14f), "SCORE  •  DISTANCE TO PIN", smallStyle);

            float rowY = y + 45f;
            int shown = Mathf.Min(playerNames.Length, 4);
            for (int i = 0; i < shown; i++)
            {
                bool active = i == activePlayerIndex;
                if (active)
                    GUI.Box(new Rect(x + 7f, rowY, width - 14f, rowHeight - 4f), GUIContent.none, darkPanel);

                GUI.Label(new Rect(x + 15f, rowY + 5f, width * 0.50f, 18f), active ? "● " + playerNames[i] : playerNames[i], active ? activePlayerStyle : playerStyle);
                GUI.Label(new Rect(x + 15f, rowY + 25f, width * 0.55f, 15f), "Score  " + FormatScore(GetPlayerRelativeToPar(i)) + "   Strokes " + playerHoleStrokes[i], playerMetaStyle);
                GUI.Label(new Rect(x + width * 0.51f, rowY + 6f, width * 0.40f, 22f), playerHoled[i] ? "IN" : DistanceToPin(i).ToString("F0") + " m", distanceStyle);
                GUI.Label(new Rect(x + width * 0.51f, rowY + 29f, width * 0.40f, 13f), playerHoled[i] ? "holed" : "to pin", smallStyle);
                rowY += rowHeight;
            }
        }

        private void DrawClubBar()
        {
            float width = Mathf.Min(Screen.width - 32f - Mathf.Clamp(Screen.width * 0.15f, 190f, 225f) - 16f, 76f + 64f * Mathf.Max(8, ActiveClub.Bag.Count));
            ClubBar.Draw(new Rect(16f, Screen.height - ClubBar.Height - 10f, width, ClubBar.Height));
        }

        private void DrawMiniMap()
        {
            float width = Mathf.Clamp(Screen.width * 0.15f, 190f, 225f);
            float x = Screen.width - width - 16f;
            float y = Screen.height - 265f;
            float height = 235f;

            GUI.Box(new Rect(x, y, width, height), GUIContent.none, darkPanel);
            GUI.Label(new Rect(x + 10f, y + 8f, width - 20f, 18f), "HOLE MAP", titleStyle);
            DrawMiniMapGraphic(new Rect(x + 10f, y + 30f, width - 20f, height - 42f));
        }

        /// <summary>Top-down map with the hole running bottom (tee) to top (pin).</summary>
        private void DrawMiniMapGraphic(Rect area)
        {
            GUI.DrawTexture(area, darkerTexture, ScaleMode.StretchToFill);

            var points = new List<Vector3> { teePosition };
            if (UsingCourse)
            {
                HoleDefinition hole = course.GetHole(holeIndex);
                if (hole.aimPoints != null) points.AddRange(hole.aimPoints);
            }
            points.Add(pinPosition);

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
            if (ball != null)
            {
                Vector3 d = ball.position - teePosition;
                maxAlong = Mathf.Max(maxAlong, Vector3.Dot(d, axis));
                maxSide = Mathf.Max(maxSide, Mathf.Abs(Vector3.Dot(d, side)));
            }
            float scale = Mathf.Min((area.height - 30f) / maxAlong, (area.width * 0.5f - 12f) / maxSide);

            Vector2 ToMap(Vector3 world)
            {
                Vector3 d = world - teePosition;
                float along = Vector3.Dot(d, axis);
                float across = Vector3.Dot(d, side);
                return new Vector2(area.center.x + across * scale, area.yMax - 15f - along * scale);
            }

            for (int i = 1; i < points.Count; i++)
                DrawDots(ToMap(points[i - 1]), ToMap(points[i]), greenTexture);

            Vector2 pinMap = ToMap(pinPosition);
            GUI.DrawTexture(new Rect(pinMap.x - 12f, pinMap.y - 9f, 24f, 18f), greenTexture);
            GUI.DrawTexture(new Rect(pinMap.x - 3f, pinMap.y - 3f, 6f, 6f), redTexture);
            Vector2 teeMap = ToMap(teePosition);
            GUI.DrawTexture(new Rect(teeMap.x - 4f, teeMap.y - 4f, 8f, 8f), whiteTexture);

            for (int i = 0; i < playerPositions.Length; i++)
            {
                if (playerHoled[i]) continue;
                Vector3 pos = i == activePlayerIndex && ball != null ? ball.position : playerPositions[i];
                Vector2 m = ToMap(pos);
                GUI.DrawTexture(new Rect(m.x - 4f, m.y - 4f, 8f, 8f), i == activePlayerIndex ? yellowTexture : blueBrightTexture);
            }

            GUI.Label(new Rect(area.x, area.y + 2f, area.width, 15f), DistanceToPin(activePlayerIndex).ToString("F0") + " m TO PIN", mapStyle);
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
            float w = Mathf.Min(620f, Screen.width - 40f);
            Rect r = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.22f, w, 48f);
            GUI.Box(r, GUIContent.none, darkPanel);
            GUI.Label(r, banner, bannerStyle);
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
                    : (holeIndex + 1 < HoleCount ? "NEXT HOLE" : "FINISH ROUND");

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

        private void DrawScorecard()
        {
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none, darkPanel);
            int holes = HoleCount;
            float cellW = Mathf.Clamp((Screen.width - 260f) / (holes + 1), 30f, 52f);
            float tableW = 160f + cellW * (holes + 1);
            float x = (Screen.width - tableW) * 0.5f;
            float y = Screen.height * 0.18f;

            GUI.Label(new Rect(x, y - 50f, tableW, 34f), "ROUND COMPLETE  •  " + (UsingCourse ? course.name : CourseSession.CourseName), bannerStyle);

            GUI.Label(new Rect(x, y, 160f, 24f), "HOLE", cellHeaderStyle);
            for (int h = 0; h < holes; h++) GUI.Label(new Rect(x + 160f + cellW * h, y, cellW, 24f), (h + 1).ToString(), cellHeaderStyle);
            GUI.Label(new Rect(x + 160f + cellW * holes, y, cellW, 24f), "TOT", cellHeaderStyle);

            y += 26f;
            GUI.Label(new Rect(x, y, 160f, 24f), "PAR", cellHeaderStyle);
            int parTotal = 0;
            for (int h = 0; h < holes; h++)
            {
                parTotal += ParForHole(h);
                GUI.Label(new Rect(x + 160f + cellW * h, y, cellW, 24f), ParForHole(h).ToString(), cellHeaderStyle);
            }
            GUI.Label(new Rect(x + 160f + cellW * holes, y, cellW, 24f), parTotal.ToString(), cellHeaderStyle);

            for (int p = 0; p < playerNames.Length; p++)
            {
                y += 30f;
                GUI.Label(new Rect(x, y, 160f, 26f), playerNames[p], cellStyle);
                int total = 0;
                for (int h = 0; h < holes; h++)
                {
                    int strokes = h < scorecard.Count && scorecard[h] != null && p < scorecard[h].Length ? scorecard[h][p] : 0;
                    total += strokes;
                    GUI.Label(new Rect(x + 160f + cellW * h, y, cellW, 26f), strokes > 0 ? strokes.ToString() : "-", cellStyle);
                }
                GUI.Label(new Rect(x + 160f + cellW * holes, y, cellW, 26f), total + " (" + FormatScore(total - parTotal) + ")", cellStyle);
            }

            if (GUI.Button(new Rect((Screen.width - 260f) * 0.5f, y + 60f, 260f, 44f), "BACK TO COURSES", actionStyle))
                SceneManager.LoadScene("GolfSimZA_0_5_CourseSelection");
        }
    }
}
