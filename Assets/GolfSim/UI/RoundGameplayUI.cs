using System;
using GolfSimZA.Core;
using GolfSimZA.Courses;
using GolfSimZA.Physics;
using UnityEngine;

namespace GolfSimZA.UI
{
    public sealed class RoundGameplayUI : MonoBehaviour
    {
        private readonly int[] parByHole =
        {
            4, 4, 5, 3, 4, 4, 3, 5, 4,
            4, 5, 4, 3, 4, 5, 4, 3, 5
        };

        private readonly int[] distanceByHole =
        {
            360, 385, 470, 165, 395, 410, 175, 505, 375,
            390, 495, 400, 175, 420, 510, 405, 180, 525
        };

        [SerializeField] private SimulatorController simulatorController;
        [SerializeField] private BallFlightSimulator ballFlightSimulator;
        [SerializeField] private Transform ball;
        [SerializeField] private Transform pin;
        [SerializeField] private Transform green;

        private GUIStyle brandStyle;
        private GUIStyle holeStyle;
        private GUIStyle courseStyle;
        private GUIStyle labelStyle;
        private GUIStyle valueStyle;
        private GUIStyle mutedStyle;
        private GUIStyle playerNameStyle;
        private GUIStyle activePlayerNameStyle;
        private GUIStyle playerMetaStyle;
        private GUIStyle distanceStyle;
        private GUIStyle shotValueStyle;
        private GUIStyle miniMapStyle;
        private GUIStyle clubStyle;
        private GUIStyle activeClubStyle;
        private GUIStyle actionStyle;
        private GUIStyle panelStyle;
        private GUIStyle panelStrongStyle;
        private GUIStyle topBarStyle;
        private GUIStyle rightCardStyle;
        private GUIStyle metricValueStyle;
        private bool stylesReady;

        private int holeIndex;
        private int totalStrokes;
        private int holesCompleted;
        private int lastObservedShotCount;
        private bool wasInFlight;
        private bool shotFinished;
        private bool waitingForNextPlayer;
        private int activePlayerIndex;
        private int selectedClubNumber = 1;
        private float currentHoleDistance;
        private string status = "Ready to tee off";

        private string[] playerNames = { "Player 1" };
        private Vector3[] playerPositions = { Vector3.zero };
        private int[] playerTotalStrokes = { 0 };
        private int[] playerHoleStrokes = { 0 };

        private readonly Color overlay = new Color(0.015f, 0.055f, 0.065f, 0.78f);
        private readonly Color overlayStrong = new Color(0.01f, 0.035f, 0.04f, 0.90f);
        private readonly Color blue = new Color(0.05f, 0.55f, 0.92f, 1f);
        private readonly Color blueBright = new Color(0.20f, 0.72f, 1f, 1f);
        private readonly Color muted = new Color(0.78f, 0.84f, 0.86f, 1f);
        private readonly Color green = new Color(0.20f, 0.70f, 0.35f, 1f);

        private void Awake()
        {
            LoadPlayers();
            holeIndex = 0;
            holesCompleted = 0;
            totalStrokes = 0;
            StartHole();
        }

        private void Update()
        {
            if (simulatorController == null || ballFlightSimulator == null)
                return;

            int shotCount = simulatorController.History != null ? simulatorController.History.Count : 0;
            if (shotCount > lastObservedShotCount)
            {
                int newShots = shotCount - lastObservedShotCount;
                playerHoleStrokes[activePlayerIndex] += newShots;
                playerTotalStrokes[activePlayerIndex] += newShots;
                lastObservedShotCount = shotCount;
                totalStrokes = Sum(playerTotalStrokes);
                shotFinished = false;
                waitingForNextPlayer = false;
                status = playerNames[activePlayerIndex] + " • Shot in progress";

                if (simulatorController.LastShot.IsValid)
                    selectedClubNumber = Mathf.Clamp(simulatorController.LastShot.ClubNumber, 1, 8);
            }

            bool inFlight = ballFlightSimulator.IsInFlight;
            if (wasInFlight && !inFlight && playerHoleStrokes[activePlayerIndex] > 0)
            {
                SaveActivePlayerPosition();
                shotFinished = true;
                waitingForNextPlayer = playerNames.Length > 1;
                status = playerNames[activePlayerIndex] + " • Shot complete";
            }

            wasInFlight = inFlight;
        }

        private void LoadPlayers()
        {
            string saved = CourseSession.PlayerNames;
            if (string.IsNullOrWhiteSpace(saved))
                saved = "Player 1";

            string[] savedNames = saved.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            if (savedNames.Length == 0)
                savedNames = new[] { "Player 1" };

            playerNames = savedNames;
            playerPositions = new Vector3[playerNames.Length];
            playerTotalStrokes = new int[playerNames.Length];
            playerHoleStrokes = new int[playerNames.Length];
            activePlayerIndex = 0;
        }

        private void EnsureStyles()
        {
            if (stylesReady)
                return;

            brandStyle = MakeLabel(24, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            holeStyle = MakeLabel(18, FontStyle.Bold, Color.white, TextAnchor.MiddleRight);
            courseStyle = MakeLabel(12, FontStyle.Bold, muted, TextAnchor.MiddleCenter);
            labelStyle = MakeLabel(10, FontStyle.Bold, muted, TextAnchor.MiddleLeft);
            valueStyle = MakeLabel(15, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            mutedStyle = MakeLabel(10, FontStyle.Normal, muted, TextAnchor.MiddleLeft);
            playerNameStyle = MakeLabel(14, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            activePlayerNameStyle = MakeLabel(14, FontStyle.Bold, blueBright, TextAnchor.MiddleLeft);
            playerMetaStyle = MakeLabel(10, FontStyle.Normal, muted, TextAnchor.MiddleLeft);
            distanceStyle = MakeLabel(22, FontStyle.Bold, Color.white, TextAnchor.MiddleRight);
            shotValueStyle = MakeLabel(13, FontStyle.Bold, Color.white, TextAnchor.MiddleRight);
            miniMapStyle = MakeLabel(9, FontStyle.Bold, muted, TextAnchor.MiddleCenter);
            clubStyle = MakeLabel(12, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            activeClubStyle = MakeLabel(12, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            actionStyle = MakeButton(12, blue);
            panelStyle = MakePanel(overlay);
            panelStrongStyle = MakePanel(overlayStrong);
            topBarStyle = MakePanel(new Color(0.01f, 0.04f, 0.05f, 0.82f));
            rightCardStyle = MakePanel(new Color(0.01f, 0.05f, 0.055f, 0.70f));
            metricValueStyle = MakeLabel(16, FontStyle.Bold, Color.white, TextAnchor.MiddleRight);
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

        private GUIStyle MakePanel(Color color)
        {
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.normal.background = MakeTexture(color);
            style.border = new RectOffset(8, 8, 8, 8);
            style.padding = new RectOffset(10, 10, 8, 8);
            return style;
        }

        private GUIStyle MakeButton(int size, Color color)
        {
            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.fontSize = size;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = Color.white;
            style.normal.background = MakeTexture(color);
            style.hover.textColor = Color.white;
            style.hover.background = MakeTexture(blueBright);
            style.active.textColor = Color.white;
            style.active.background = MakeTexture(blue);
            style.border = new RectOffset(6, 6, 6, 6);
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
            EnsureStyles();

            // The simulator view is now the hero. UI is deliberately sparse and
            // sits on top of the course rather than boxing in the entire screen.
            DrawTopHUD();
            DrawLeftShotOverlay();
            DrawRightPlayerOverlay();
            DrawBottomClubBar();
            DrawMiniMapOverlay();
            DrawShotCompletionAction();
        }

        private void DrawTopHUD()
        {
            float margin = Mathf.Max(18f, Screen.width * 0.018f);
            float barHeight = 52f;
            float width = Screen.width - margin * 2f;

            GUI.Box(new Rect(margin, 10f, width, barHeight), GUIContent.none, topBarStyle);
            GUI.Label(new Rect(margin + 14f, 10f, 160f, barHeight), "GOLFSIM ZA", brandStyle);
            GUI.Label(new Rect(margin + 170f, 10f, width - 500f, barHeight), CourseSession.CourseName, courseStyle);

            GUI.Label(new Rect(margin + width - 330f, 10f, 155f, barHeight), "HOLE " + (holeIndex + 1) + " / " + CourseSession.RoundLength, holeStyle);
            GUI.Label(new Rect(margin + width - 175f, 10f, 160f, barHeight), "PAR " + parByHole[holeIndex] + "   " + currentHoleDistance.ToString("F0") + " m", holeStyle);
        }

        private void DrawLeftShotOverlay()
        {
            float x = 18f;
            float y = 76f;
            float width = Mathf.Clamp(Screen.width * 0.19f, 225f, 285f);
            float height = 252f;

            GUI.Box(new Rect(x, y, width, height), GUIContent.none, panelStyle);
            GUI.Label(new Rect(x + 12f, y + 10f, width - 24f, 22f), "SHOT DATA", valueStyle);
            GUI.Label(new Rect(x + 12f, y + 34f, width - 24f, 18f), status, mutedStyle);

            ShotData shot = simulatorController != null ? simulatorController.LastShot : default(ShotData);
            bool hasShot = shot.IsValid;
            float row = y + 60f;

            CompactMetric("CLUB", hasShot ? shot.ClubName : "Driver", ref row, x, width, false);
            CompactMetric("BALL SPEED", hasShot ? shot.BallSpeedKph.ToString("F1") + " km/h" : "-", ref row, x, width, true);
            CompactMetric("CLUB SPEED", hasShot ? shot.ClubSpeedKph.ToString("F1") + " km/h" : "-", ref row, x, width, true);
            CompactMetric("LAUNCH", hasShot ? shot.LaunchAngleDeg.ToString("F1") + "°" : "-", ref row, x, width, true);
            CompactMetric("DIRECTION", hasShot ? shot.LaunchDirectionDeg.ToString("F1") + "°" : "-", ref row, x, width, true);
            CompactMetric("BACKSPIN", hasShot ? shot.BackSpinRpm.ToString("F0") + " rpm" : "-", ref row, x, width, true);
            CompactMetric("CARRY", hasShot ? shot.CarryMeters.ToString("F1") + " m" : "-", ref row, x, width, true);
            CompactMetric("TOTAL", hasShot ? shot.TotalMeters.ToString("F1") + " m" : "-", ref row, x, width, true);
        }

        private void CompactMetric(string label, string value, ref float y, float x, float width, bool rightAligned)
        {
            GUI.Label(new Rect(x + 12f, y, width * 0.47f, 18f), label, labelStyle);
            GUI.Label(new Rect(x + width * 0.43f, y - 1f, width * 0.51f, 20f), value, rightAligned ? shotValueStyle : valueStyle);
            y += 25f;
        }

        private void DrawRightPlayerOverlay()
        {
            float margin = Mathf.Max(18f, Screen.width * 0.018f);
            float width = Mathf.Clamp(Screen.width * 0.19f, 225f, 285f);
            float x = Screen.width - margin - width;
            float y = 76f;

            GUI.Box(new Rect(x, y, width, 220f), GUIContent.none, panelStyle);
            GUI.Label(new Rect(x + 12f, y + 10f, width - 24f, 22f), "PLAYERS", valueStyle);
            GUI.Label(new Rect(x + 12f, y + 34f, width - 24f, 16f), "SCORE   •   DISTANCE TO PIN", mutedStyle);

            float rowY = y + 60f;
            for (int i = 0; i < playerNames.Length; i++)
            {
                bool active = i == activePlayerIndex;
                if (active)
                    GUI.Box(new Rect(x + 9f, rowY, width - 18f, 67f), GUIContent.none, rightCardStyle);

                GUI.Label(new Rect(x + 18f, rowY + 7f, width * 0.50f, 20f), active ? "● " + playerNames[i] : playerNames[i], active ? activePlayerNameStyle : playerNameStyle);
                GUI.Label(new Rect(x + 18f, rowY + 30f, width * 0.50f, 17f), "Score  " + FormatScore(GetPlayerRelativeToPar(i)), playerMetaStyle);
                GUI.Label(new Rect(x + width * 0.52f, rowY + 7f, width * 0.39f, 25f), DistanceToPin(i).ToString("F0") + " m", distanceStyle);
                GUI.Label(new Rect(x + width * 0.52f, rowY + 34f, width * 0.39f, 15f), "to pin", labelStyle);
                rowY += 72f;
            }
        }

        private void DrawBottomClubBar()
        {
            float width = Mathf.Min(440f, Screen.width * 0.40f);
            float x = 18f;
            float y = Screen.height - 92f;
            float h = 70f;

            GUI.Box(new Rect(x, y, width, h), GUIContent.none, panelStrongStyle);
            GUI.Label(new Rect(x + 10f, y + 7f, 85f, 18f), "CLUB", labelStyle);

            string[] clubs = { "Driver", "3W", "5i", "7i", "9i", "PW", "SW", "Putter" };
            float buttonWidth = (width - 24f) / 8f;
            for (int i = 0; i < clubs.Length; i++)
            {
                Rect r = new Rect(x + 10f + buttonWidth * i, y + 28f, buttonWidth - 3f, 30f);
                bool active = selectedClubNumber == i + 1;
                if (GUI.Button(r, clubs[i], active ? activeClubStyle : clubStyle))
                    selectedClubNumber = i + 1;
                if (active)
                    GUI.DrawTexture(new Rect(r.x + 3f, r.yMax - 3f, r.width - 6f, 3f), MakeTexture(blueBright));
            }
        }

        private void DrawMiniMapOverlay()
        {
            float margin = Mathf.Max(18f, Screen.width * 0.018f);
            float width = Mathf.Clamp(Screen.width * 0.16f, 190f, 240f);
            float x = Screen.width - margin - width;
            float y = Screen.height - 230f;

            GUI.Box(new Rect(x, y, width, 205f), GUIContent.none, panelStrongStyle);
            GUI.Label(new Rect(x + 12f, y + 10f, width - 24f, 20f), "HOLE MAP", labelStyle);
            DrawMiniMap(x + 12f, y + 34f, width - 24f, 150f);
        }

        private void DrawMiniMap(float x, float y, float width, float height)
        {
            GUI.DrawTexture(new Rect(x, y, width, height), MakeTexture(new Color(0.03f, 0.13f, 0.08f, 0.95f)), ScaleMode.StretchToFill);
            float center = x + width * 0.5f;
            GUI.DrawTexture(new Rect(center - 16f, y + 10f, 32f, height - 20f), MakeTexture(new Color(0.12f, 0.48f, 0.23f, 1f)), ScaleMode.StretchToFill);
            GUI.DrawTexture(new Rect(center - 30f, y + 62f, 60f, 35f), MakeTexture(new Color(0.18f, 0.60f, 0.28f, 1f)), ScaleMode.StretchToFill);
            GUI.DrawTexture(new Rect(center - 4f, y + 11f, 8f, 8f), MakeTexture(new Color(0.95f, 0.20f, 0.18f, 1f)));
            GUI.DrawTexture(new Rect(center - 5f, y + height - 18f, 10f, 10f), MakeTexture(blueBright));
            GUI.Label(new Rect(x, y + 2f, width, 18f), "GREEN", miniMapStyle);
            GUI.Label(new Rect(x, y + height - 20f, width, 18f), "TEE", miniMapStyle);
        }

        private void DrawShotCompletionAction()
        {
            if (!shotFinished || ballFlightSimulator == null || ballFlightSimulator.IsInFlight)
                return;

            float width = Mathf.Min(420f, Screen.width * 0.38f);
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - 74f;

            string text;
            if (waitingForNextPlayer)
                text = "NEXT PLAYER  •  " + NextPlayerName();
            else
                text = holeIndex + 1 < CourseSession.RoundLength ? "NEXT HOLE  •  FINISH HOLE" : "FINISH ROUND";

            if (GUI.Button(new Rect(x, y, width, 46f), text, actionStyle))
            {
                if (waitingForNextPlayer)
                    AdvancePlayer();
                else if (holeIndex + 1 < CourseSession.RoundLength)
                    AdvanceHole();
                else
                    FinishRound();
            }
        }

        private string FormatScore(int relativeToPar)
        {
            if (relativeToPar == 0)
                return "E";
            return relativeToPar > 0 ? "+" + relativeToPar : relativeToPar.ToString();
        }

        private int GetPlayerRelativeToPar(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= playerTotalStrokes.Length || playerTotalStrokes[playerIndex] == 0)
                return 0;

            int parToCount = 0;
            for (int i = 0; i < holesCompleted; i++)
                parToCount += parByHole[Mathf.Clamp(i, 0, parByHole.Length - 1)];

            if (playerHoleStrokes[playerIndex] > 0)
                parToCount += parByHole[Mathf.Clamp(holeIndex, 0, parByHole.Length - 1)];

            return playerTotalStrokes[playerIndex] - parToCount;
        }

        private float DistanceToPin(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= playerPositions.Length)
                return currentHoleDistance;

            Vector3 position = playerPositions[playerIndex];
            if (playerIndex == activePlayerIndex && ball != null)
                position = ball.position;

            if (pin == null)
                return currentHoleDistance;

            Vector3 delta = pin.position - position;
            delta.y = 0f;
            return delta.magnitude;
        }

        private void SaveActivePlayerPosition()
        {
            if (ball != null && activePlayerIndex >= 0 && activePlayerIndex < playerPositions.Length)
                playerPositions[activePlayerIndex] = ball.position;
        }

        private string NextPlayerName()
        {
            if (playerNames.Length == 0)
                return "Player 1";
            return playerNames[(activePlayerIndex + 1) % playerNames.Length];
        }

        private void AdvancePlayer()
        {
            SaveActivePlayerPosition();
            activePlayerIndex = (activePlayerIndex + 1) % playerNames.Length;
            shotFinished = false;
            waitingForNextPlayer = false;
            lastObservedShotCount = simulatorController != null && simulatorController.History != null ? simulatorController.History.Count : lastObservedShotCount;
            status = playerNames[activePlayerIndex] + " • Your turn";

            if (ball != null)
                ball.position = playerPositions[activePlayerIndex];
        }

        private void StartHole()
        {
            int roundLength = Mathf.Clamp(CourseSession.RoundLength, 1, 18);
            holeIndex = Mathf.Clamp(holeIndex, 0, roundLength - 1);
            activePlayerIndex = 0;
            shotFinished = false;
            waitingForNextPlayer = false;
            currentHoleDistance = GetHoleDistance(holeIndex);
            lastObservedShotCount = simulatorController != null && simulatorController.History != null ? simulatorController.History.Count : 0;
            wasInFlight = false;
            status = "Ready to tee off - " + playerNames[activePlayerIndex];

            for (int i = 0; i < playerPositions.Length; i++)
            {
                playerPositions[i] = new Vector3(0f, 0.04f, 0f);
                playerHoleStrokes[i] = 0;
            }

            if (ball != null)
                ball.position = playerPositions[activePlayerIndex];

            if (green != null)
            {
                green.position = new Vector3(0f, green.position.y, currentHoleDistance);
                green.localScale = new Vector3(14f, green.localScale.y, 8f);
            }

            if (pin != null)
                pin.position = new Vector3(0f, pin.position.y, currentHoleDistance);
        }

        private void AdvanceHole()
        {
            holesCompleted++;
            holeIndex++;
            if (holeIndex >= Mathf.Clamp(CourseSession.RoundLength, 1, 18))
            {
                FinishRound();
                return;
            }
            StartHole();
        }

        private void FinishRound()
        {
            totalStrokes = Sum(playerTotalStrokes);
            holesCompleted = Mathf.Clamp(CourseSession.RoundLength, 1, 18);
            status = "Round complete - " + totalStrokes + " strokes";
            shotFinished = false;
            waitingForNextPlayer = false;
        }

        private int Sum(int[] values)
        {
            int total = 0;
            for (int i = 0; i < values.Length; i++)
                total += values[i];
            return total;
        }

        private float GetHoleDistance(int index)
        {
            int safeIndex = Mathf.Clamp(index, 0, distanceByHole.Length - 1);
            float teeMultiplier = 1f;
            switch (CourseSession.TeeName)
            {
                case "Red": teeMultiplier = 0.86f; break;
                case "White": teeMultiplier = 0.93f; break;
                case "Blue": teeMultiplier = 1f; break;
                case "Black": teeMultiplier = 1.07f; break;
            }
            return distanceByHole[safeIndex] * teeMultiplier;
        }
    }
}
