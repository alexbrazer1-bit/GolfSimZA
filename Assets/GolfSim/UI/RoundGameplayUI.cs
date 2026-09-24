using System;
using GolfSimZA.Core;
using GolfSimZA.Courses;
using GolfSimZA.Physics;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        private GUIStyle panelTitleStyle;
        private GUIStyle labelStyle;
        private GUIStyle valueStyle;
        private GUIStyle mutedStyle;
        private GUIStyle buttonStyle;
        private GUIStyle activeButtonStyle;
        private GUIStyle playerNameStyle;
        private GUIStyle activePlayerNameStyle;
        private GUIStyle playerMetaStyle;
        private GUIStyle distanceStyle;
        private GUIStyle shotValueStyle;
        private GUIStyle miniMapStyle;
        private bool stylesReady;

        private int holeIndex;
        private int totalStrokes;
        private int holesCompleted;
        private int lastObservedShotCount;
        private bool wasInFlight;
        private bool shotFinished;
        private string status = "Ready to tee off";
        private float currentHoleDistance;

        private string[] playerNames = { "Player 1" };
        private Vector3[] playerPositions = { Vector3.zero };
        private int[] playerTotalStrokes = { 0 };
        private int[] playerHoleStrokes = { 0 };
        private int activePlayerIndex;
        private bool waitingForNextPlayer;
        private int selectedClubNumber = 1;

        private readonly Color background = new Color(0.015f, 0.07f, 0.08f);
        private readonly Color panel = new Color(0.035f, 0.13f, 0.16f, 0.94f);
        private readonly Color panelSoft = new Color(0.08f, 0.19f, 0.23f, 0.94f);
        private readonly Color blue = new Color(0.08f, 0.57f, 0.90f);
        private readonly Color blueSoft = new Color(0.14f, 0.70f, 0.98f);
        private readonly Color muted = new Color(0.70f, 0.78f, 0.81f);
        private readonly Color greenAccent = new Color(0.25f, 0.82f, 0.47f);

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
                    selectedClubNumber = simulatorController.LastShot.ClubNumber;
            }

            bool inFlight = ballFlightSimulator.IsInFlight;
            if (wasInFlight && !inFlight && playerHoleStrokes[activePlayerIndex] > 0)
            {
                SaveActivePlayerPosition();
                shotFinished = true;
                waitingForNextPlayer = playerNames.Length > 1;
                status = $"{playerNames[activePlayerIndex]} • Shot complete";
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
            if (stylesReady) return;

            brandStyle = MakeLabel(26, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            holeStyle = MakeLabel(18, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            courseStyle = MakeLabel(13, FontStyle.Bold, muted, TextAnchor.MiddleCenter);
            panelTitleStyle = MakeLabel(16, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            labelStyle = MakeLabel(12, FontStyle.Bold, muted, TextAnchor.MiddleLeft);
            valueStyle = MakeLabel(16, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            mutedStyle = MakeLabel(11, FontStyle.Normal, muted, TextAnchor.MiddleLeft);
            buttonStyle = MakeButton(13, panelSoft);
            activeButtonStyle = MakeButton(13, blue);
            playerNameStyle = MakeLabel(14, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            activePlayerNameStyle = MakeLabel(15, FontStyle.Bold, blueSoft, TextAnchor.MiddleLeft);
            playerMetaStyle = MakeLabel(11, FontStyle.Normal, muted, TextAnchor.MiddleLeft);
            distanceStyle = MakeLabel(22, FontStyle.Bold, Color.white, TextAnchor.MiddleRight);
            shotValueStyle = MakeLabel(14, FontStyle.Bold, Color.white, TextAnchor.MiddleRight);
            miniMapStyle = MakeLabel(10, FontStyle.Bold, muted, TextAnchor.MiddleCenter);
            stylesReady = true;
        }

        private GUIStyle MakeLabel(int size, FontStyle fontStyle, Color color, TextAnchor alignment)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = fontStyle,
                alignment = alignment,
                normal = { textColor = color }
            };
        }

        private GUIStyle MakeButton(int size, Color backgroundColor)
        {
            GUIStyle style = new GUIStyle(GUI.skin.button)
            {
                fontSize = size,
                fontStyle = FontStyle.Bold,
                fixedHeight = 38,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = MakeTexture(backgroundColor), textColor = Color.white },
                hover = { background = MakeTexture(blueSoft), textColor = Color.white },
                active = { background = MakeTexture(blue), textColor = Color.white }
            };
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
            GUI.backgroundColor = background;
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);

            float margin = Mathf.Max(18f, Screen.width * 0.018f);
            float rightWidth = Mathf.Clamp(Screen.width * 0.205f, 270f, 360f);
            float leftWidth = Mathf.Clamp(Screen.width * 0.19f, 235f, 330f);
            float centerX = margin + leftWidth + 14f;
            float centerWidth = Screen.width - leftWidth - rightWidth - margin * 2f - 28f;

            DrawTopBar(margin, Screen.width - margin * 2f);
            DrawLeftShotPanel(margin, 72f, leftWidth, Screen.height - 92f);
            DrawCenterHud(centerX, 76f, centerWidth);
            DrawRightPlayerPanel(Screen.width - margin - rightWidth, 72f, rightWidth, Screen.height - 92f);
        }

        private void DrawTopBar(float x, float width)
        {
            GUI.Box(new Rect(x, 12f, width, 48f), GUIContent.none, GUI.skin.box);
            GUI.Label(new Rect(x + 12f, 12f, 170f, 48f), "GOLFSIM ZA", brandStyle);
            GUI.Label(new Rect(x + 190f, 12f, width - 500f, 48f), CourseSession.CourseName, courseStyle);

            string holeText = $"{holeIndex + 1}  •  HOLE {holeIndex + 1} / {CourseSession.RoundLength}";
            GUI.Label(new Rect(x + width - 310f, 12f, 150f, 48f), holeText, holeStyle);
            GUI.Label(new Rect(x + width - 160f, 12f, 150f, 48f), $"PAR {parByHole[holeIndex]}  •  {currentHoleDistance:F0} m", courseStyle);
        }

        private void DrawLeftShotPanel(float x, float y, float width, float height)
        {
            GUI.Box(new Rect(x, y, width, height), GUIContent.none, GUI.skin.box);
            GUI.Label(new Rect(x + 14f, y + 12f, width - 28f, 24f), "SHOT DATA", panelTitleStyle);
            GUI.Label(new Rect(x + 14f, y + 38f, width - 28f, 18f), status, mutedStyle);

            ShotData shot = simulatorController != null ? simulatorController.LastShot : default(ShotData);
            bool hasShot = shot.IsValid;
            float rowY = y + 68f;

            ShotMetric("CLUB", hasShot ? shot.ClubName : "Driver", ref rowY, x, width, false);
            ShotMetric("BALL SPEED", hasShot ? $"{shot.BallSpeedKph:F1} km/h" : "—", ref rowY, x, width, true);
            ShotMetric("CLUB SPEED", hasShot ? $"{shot.ClubSpeedKph:F1} km/h" : "—", ref rowY, x, width, true);
            ShotMetric("LAUNCH", hasShot ? $"{shot.LaunchAngleDeg:F1}°" : "—", ref rowY, x, width, true);
            ShotMetric("DIRECTION", hasShot ? $"{shot.LaunchDirectionDeg:F1}°" : "—", ref rowY, x, width, true);
            ShotMetric("BACKSPIN", hasShot ? $"{shot.BackSpinRpm:F0} rpm" : "—", ref rowY, x, width, true);
            ShotMetric("SPIN AXIS", hasShot ? $"{shot.SpinAxisDeg:F1}°" : "—", ref rowY, x, width, true);
            ShotMetric("CARRY", hasShot ? $"{shot.CarryMeters:F1} m" : "—", ref rowY, x, width, true);
            ShotMetric("TOTAL", hasShot ? $"{shot.TotalMeters:F1} m" : "—", ref rowY, x, width, true);

            GUILayout.BeginArea(new Rect(x + 12f, y + height - 150f, width - 24f, 132f));
            GUILayout.Label("CLUB", labelStyle);
            string[] clubs = { "Driver", "3W", "5i", "7i", "9i", "PW", "SW", "Putter" };
            for (int i = 0; i < clubs.Length; i += 4)
            {
                GUILayout.BeginHorizontal();
                for (int j = 0; j < 4 && i + j < clubs.Length; j++)
                {
                    int clubNumber = i + j + 1;
                    bool active = selectedClubNumber == clubNumber;
                    if (GUILayout.Button($"{clubNumber}  {clubs[i + j]}", active ? activeButtonStyle : buttonStyle, GUILayout.Width((width - 36f) / 4f)))
                        selectedClubNumber = clubNumber;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("Press 1–8 to select a club • SPACE to hit", mutedStyle);
            GUILayout.EndArea();
        }

        private void ShotMetric(string label, string value, ref float y, float x, float width, bool rightAligned)
        {
            GUI.Label(new Rect(x + 14f, y, width * 0.47f, 20f), label, labelStyle);
            GUI.Label(new Rect(x + width * 0.46f, y, width * 0.48f, 22f), value, rightAligned ? shotValueStyle : valueStyle);
            y += 30f;
        }

        private void DrawCenterHud(float x, float y, float width)
        {
            float topHeight = 74f;
            GUI.Box(new Rect(x, y, width, topHeight), GUIContent.none, GUI.skin.box);

            GUI.Label(new Rect(x + 14f, y + 10f, width * 0.24f, 24f), $"{playerNames[activePlayerIndex]}", activePlayerNameStyle);
            GUI.Label(new Rect(x + 14f, y + 37f, width * 0.24f, 22f), $"STROKES  {playerHoleStrokes[activePlayerIndex]}  •  SCORE {FormatScore(GetPlayerRelativeToPar(activePlayerIndex))}", mutedStyle);

            GUI.Label(new Rect(x + width * 0.29f, y + 8f, width * 0.22f, 28f), $"{currentHoleDistance:F0} m", distanceStyle);
            GUI.Label(new Rect(x + width * 0.29f, y + 37f, width * 0.22f, 18f), "TO PIN", labelStyle);

            float distance = DistanceToPin(activePlayerIndex);
            GUI.Label(new Rect(x + width * 0.55f, y + 8f, width * 0.20f, 28f), $"{distance:F0} m", distanceStyle);
            GUI.Label(new Rect(x + width * 0.55f, y + 37f, width * 0.20f, 18f), "CURRENT BALL", labelStyle);

            string connection = simulatorController != null && simulatorController.IsLaunchMonitorConnected ? "● CONNECTED" : "● TEST MODE";
            GUI.Label(new Rect(x + width * 0.77f, y + 17f, width * 0.21f, 28f), connection, labelStyle);

            DrawCourseViewPlaceholder(x, y + topHeight + 10f, width, Screen.height - y - topHeight - 95f);
            DrawBottomControls(x, Screen.height - 70f, width);
        }

        private void DrawCourseViewPlaceholder(float x, float y, float width, float height)
        {
            GUI.Box(new Rect(x, y, width, height), GUIContent.none, GUI.skin.box);

            float horizon = y + height * 0.38f;
            GUI.DrawTexture(new Rect(x + 1f, y + 1f, width - 2f, height * 0.38f), MakeTexture(new Color(0.35f, 0.62f, 0.76f)), ScaleMode.StretchToFill);
            GUI.DrawTexture(new Rect(x + 1f, horizon, width - 2f, height * 0.62f), MakeTexture(new Color(0.12f, 0.38f, 0.22f)), ScaleMode.StretchToFill);

            float center = x + width * 0.5f;
            float bottom = y + height - 12f;
            Vector3[] fairway =
            {
                new Vector3(center - width * 0.42f, bottom),
                new Vector3(center + width * 0.42f, bottom),
                new Vector3(center + width * 0.12f, horizon + 8f),
                new Vector3(center - width * 0.12f, horizon + 8f)
            };
            DrawQuad(fairway, new Color(0.18f, 0.60f, 0.30f));

            float pinY = Mathf.Lerp(horizon + 18f, bottom - 30f, 0.16f);
            GUI.DrawTexture(new Rect(center - 3f, pinY - 56f, 6f, 56f), MakeTexture(new Color(0.96f, 0.96f, 0.96f)));
            GUI.DrawTexture(new Rect(center - 4f, pinY - 58f, 22f, 13f), MakeTexture(new Color(0.95f, 0.20f, 0.18f)));

            GUI.Label(new Rect(center - 50f, pinY - 86f, 100f, 24f), $"{DistanceToPin(activePlayerIndex):F0} m", miniMapStyle);

            if (ball != null)
            {
                float ballRatio = Mathf.Clamp01(ball.position.z / Mathf.Max(1f, currentHoleDistance));
                float ballX = center + Mathf.Clamp(ball.position.x / Mathf.Max(1f, currentHoleDistance), -0.35f, 0.35f) * width;
                float ballY = Mathf.Lerp(bottom - 22f, pinY, ballRatio);
                GUI.DrawTexture(new Rect(ballX - 6f, ballY - 6f, 12f, 12f), MakeTexture(blueSoft));
            }

            GUI.Label(new Rect(x + 14f, y + 12f, 170f, 22f), "PLAYING VIEW", labelStyle);
            GUI.Label(new Rect(x + width - 190f, y + 12f, 176f, 22f), $"{CourseSession.TeeName} TEES", labelStyle);
        }

        private void DrawQuad(Vector3[] points, Color color)
        {
            Texture2D texture = MakeTexture(color);
            GUIUtility.RotateAroundPivot(0f, Vector2.zero);
            GUI.DrawTexture(new Rect(points[3].x, points[2].y, points[1].x - points[0].x, points[0].y - points[2].y), texture, ScaleMode.StretchToFill);
        }

        private void DrawBottomControls(float x, float y, float width)
        {
            if (shotFinished && waitingForNextPlayer && !ballFlightSimulator.IsInFlight)
            {
                if (GUI.Button(new Rect(x, y, width, 48f), $"NEXT PLAYER  •  {NextPlayerName()}", activeButtonStyle))
                    AdvancePlayer();
                return;
            }

            if (shotFinished && !ballFlightSimulator.IsInFlight)
            {
                string text = holeIndex + 1 < CourseSession.RoundLength ? "FINISH HOLE  •  NEXT HOLE" : "FINISH ROUND";
                if (GUI.Button(new Rect(x, y, width, 48f), text, activeButtonStyle))
                {
                    if (holeIndex + 1 < CourseSession.RoundLength) AdvanceHole();
                    else FinishRound();
                }
            }
        }

        private void DrawRightPlayerPanel(float x, float y, float width, float height)
        {
            GUI.Box(new Rect(x, y, width, height), GUIContent.none, GUI.skin.box);
            GUI.Label(new Rect(x + 14f, y + 12f, width - 28f, 24f), "PLAYERS", panelTitleStyle);
            GUI.Label(new Rect(x + 14f, y + 38f, width - 28f, 18f), "SCORE  •  DISTANCE TO PIN", mutedStyle);

            float rowY = y + 68f;
            for (int i = 0; i < playerNames.Length; i++)
            {
                bool active = i == activePlayerIndex;
                float rowHeight = active ? 94f : 84f;
                GUI.Box(new Rect(x + 10f, rowY, width - 20f, rowHeight), GUIContent.none, GUI.skin.box);

                GUI.Label(new Rect(x + 22f, rowY + 9f, width * 0.52f, 24f), active ? "● " + playerNames[i] : playerNames[i], active ? activePlayerNameStyle : playerNameStyle);
                GUI.Label(new Rect(x + 22f, rowY + 37f, width * 0.56f, 20f), $"Score  {FormatScore(GetPlayerRelativeToPar(i))}", playerMetaStyle);
                GUI.Label(new Rect(x + 22f, rowY + 59f, width * 0.56f, 18f), $"{playerTotalStrokes[i]} shots", playerMetaStyle);

                float distance = DistanceToPin(i);
                GUI.Label(new Rect(x + width * 0.53f, rowY + 14f, width * 0.40f, 30f), $"{distance:F0} m", distanceStyle);
                GUI.Label(new Rect(x + width * 0.53f, rowY + 46f, width * 0.40f, 18f), "to pin", labelStyle);

                rowY += rowHeight + 8f;
            }

            float mapY = y + height - 190f;
            GUI.Box(new Rect(x + 12f, mapY, width - 24f, 145f), GUIContent.none, GUI.skin.box);
            GUI.Label(new Rect(x + 22f, mapY + 8f, width - 44f, 20f), "HOLE MAP", labelStyle);
            DrawMiniMap(x + 22f, mapY + 34f, width - 44f, 100f);

            GUI.Label(new Rect(x + 14f, y + height - 30f, width - 28f, 20f), $"Hole {holeIndex + 1}  •  {CourseSession.RoundLength} holes", mutedStyle);
        }

        private void DrawMiniMap(float x, float y, float width, float height)
        {
            GUI.DrawTexture(new Rect(x, y, width, height), MakeTexture(new Color(0.08f, 0.24f, 0.14f)), ScaleMode.StretchToFill);
            float center = x + width * 0.5f;
            float startY = y + height - 12f;
            float endY = y + 12f;
            GUI.DrawTexture(new Rect(center - 10f, y + 8f, 20f, height - 16f), MakeTexture(new Color(0.20f, 0.56f, 0.27f)), ScaleMode.StretchToFill);
            GUI.DrawTexture(new Rect(center - 4f, endY - 4f, 8f, 8f), MakeTexture(new Color(0.95f, 0.20f, 0.18f)));
            GUI.DrawTexture(new Rect(center - 5f, startY - 5f, 10f, 10f), MakeTexture(blueSoft));
            GUI.Label(new Rect(x, y + height - 20f, width, 18f), "TEE", miniMapStyle);
            GUI.Label(new Rect(x, y, width, 18f), "GREEN", miniMapStyle);
        }

        private string FormatScore(int relativeToPar)
        {
            if (relativeToPar == 0) return "E";
            return relativeToPar > 0 ? "+" + relativeToPar : relativeToPar.ToString();
        }

        private int GetPlayerRelativeToPar(int playerIndex)
        {
            if (playerTotalStrokes[playerIndex] == 0)
                return 0;

            int parToCount = 0;
            for (int i = 0; i < holesCompleted; i++)
                parToCount += parByHole[Mathf.Clamp(i, 0, parByHole.Length - 1)];

            if (playerHoleStrokes[playerIndex] > 0)
                parToCount += parByHole[holeIndex];

            return playerTotalStrokes[playerIndex] - parToCount;
        }

        private float DistanceToPin(int playerIndex)
        {
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
            if (ball != null)
                playerPositions[activePlayerIndex] = ball.position;
        }

        private string NextPlayerName()
        {
            if (playerNames.Length == 0) return "Player 1";
            return playerNames[(activePlayerIndex + 1) % playerNames.Length];
        }

        private void AdvancePlayer()
        {
            SaveActivePlayerPosition();
            activePlayerIndex = (activePlayerIndex + 1) % playerNames.Length;
            shotFinished = false;
            waitingForNextPlayer = false;
            lastObservedShotCount = simulatorController != null && simulatorController.History != null
                ? simulatorController.History.Count
                : lastObservedShotCount;
            status = playerNames[activePlayerIndex] + " • Your turn";

            if (ball != null)
                ball.position = playerPositions[activePlayerIndex];
        }

        private void StartHole()
        {
            holeIndex = Mathf.Clamp(holeIndex, 0, Mathf.Max(0, CourseSession.RoundLength - 1));
            shotFinished = false;
            waitingForNextPlayer = false;
            status = "Ready to tee off • " + playerNames[activePlayerIndex];
            lastObservedShotCount = simulatorController != null && simulatorController.History != null
                ? simulatorController.History.Count
                : 0;
            currentHoleDistance = GetHoleDistance(holeIndex);
            wasInFlight = false;
            activePlayerIndex = 0;

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
            if (holeIndex >= CourseSession.RoundLength)
            {
                FinishRound();
                return;
            }

            StartHole();
        }

        private void FinishRound()
        {
            totalStrokes = Sum(playerTotalStrokes);
            holesCompleted = CourseSession.RoundLength;
            status = $"Round complete • {totalStrokes} strokes";
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
