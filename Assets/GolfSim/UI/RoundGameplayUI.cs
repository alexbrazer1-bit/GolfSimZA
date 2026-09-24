using System;
using GolfSimZA.Core;
using GolfSimZA.Courses;
using GolfSimZA.Physics;
using UnityEngine;

namespace GolfSimZA.UI
{
    public sealed class RoundGameplayUI : MonoBehaviour
    {
        private readonly int[] parByHole = { 4, 4, 5, 3, 4, 4, 3, 5, 4, 4, 5, 4, 3, 4, 5, 4, 3, 5 };
        private readonly int[] distanceByHole = { 360, 385, 470, 165, 395, 410, 175, 505, 375, 390, 495, 400, 175, 420, 510, 405, 180, 525 };

        [SerializeField] private SimulatorController simulatorController;
        [SerializeField] private BallFlightSimulator ballFlightSimulator;
        [SerializeField] private Transform ball;
        [SerializeField] private Transform pin;
        [SerializeField] private Transform green;

        private GUIStyle smallPanel, darkPanel, titleStyle, smallStyle, valueStyle, rightValueStyle;
        private GUIStyle holeStyle, courseStyle, clubStyle, activeClubStyle, actionStyle;
        private GUIStyle playerStyle, activePlayerStyle, playerMetaStyle, distanceStyle, mapStyle;
        private GUIStyle centerStyle;
        private Texture2D transparent, darkTexture, darkerTexture, blueTexture, blueBrightTexture, whiteTexture, greenTexture, redTexture;
        private bool stylesReady;

        private int holeIndex, totalStrokes, holesCompleted, lastObservedShotCount;
        private bool wasInFlight, shotFinished, waitingForNextPlayer;
        private int activePlayerIndex, selectedClubNumber = 1;
        private float currentHoleDistance;
        private string status = "READY";

        private string[] playerNames = { "Player 1" };
        private Vector3[] playerPositions = { Vector3.zero };
        private int[] playerTotalStrokes = { 0 };
        private int[] playerHoleStrokes = { 0 };

        private readonly Color dark = new Color(0.015f, 0.055f, 0.065f, 0.86f);
        private readonly Color darker = new Color(0.005f, 0.025f, 0.03f, 0.92f);
        private readonly Color blue = new Color(0.03f, 0.48f, 0.82f, 0.95f);
        private readonly Color blueBright = new Color(0.08f, 0.64f, 1f, 1f);
        private readonly Color muted = new Color(0.76f, 0.83f, 0.85f, 1f);

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
            if (simulatorController == null || ballFlightSimulator == null) return;

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
                status = "SHOT IN PROGRESS";

                if (simulatorController.LastShot.IsValid)
                    selectedClubNumber = Mathf.Clamp(simulatorController.LastShot.ClubNumber, 1, 8);
            }

            bool inFlight = ballFlightSimulator.IsInFlight;
            if (wasInFlight && !inFlight && playerHoleStrokes[activePlayerIndex] > 0)
            {
                SaveActivePlayerPosition();
                shotFinished = true;
                waitingForNextPlayer = playerNames.Length > 1;
                status = "SHOT COMPLETE";
            }
            wasInFlight = inFlight;
        }

        private void LoadPlayers()
        {
            string saved = CourseSession.PlayerNames;
            if (string.IsNullOrWhiteSpace(saved)) saved = "Player 1";
            string[] savedNames = saved.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            if (savedNames.Length == 0) savedNames = new[] { "Player 1" };

            playerNames = savedNames;
            playerPositions = new Vector3[playerNames.Length];
            playerTotalStrokes = new int[playerNames.Length];
            playerHoleStrokes = new int[playerNames.Length];
            activePlayerIndex = 0;
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;

            transparent = MakeTexture(new Color(0f, 0f, 0f, 0f));
            darkTexture = MakeTexture(dark);
            darkerTexture = MakeTexture(darker);
            blueTexture = MakeTexture(blue);
            blueBrightTexture = MakeTexture(blueBright);
            whiteTexture = MakeTexture(new Color(1f, 1f, 1f, 0.95f));
            greenTexture = MakeTexture(new Color(0.12f, 0.56f, 0.28f, 0.95f));
            redTexture = MakeTexture(new Color(0.92f, 0.15f, 0.13f, 1f));

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
            EnsureStyles();

            DrawTopHUD();
            DrawShotCard();
            DrawWindCard();
            DrawHoleInfo();
            DrawPlayerCard();
            DrawClubBar();
            DrawMiniMap();
            DrawShotCompletionAction();
        }

        private void DrawTopHUD()
        {
            float margin = Mathf.Max(14f, Screen.width * 0.018f);
            float width = Screen.width - margin * 2f;
            float height = 42f;

            GUI.Box(new Rect(margin, 8f, width, height), GUIContent.none, darkPanel);
            GUI.Label(new Rect(margin + 12f, 8f, 135f, height), "GOLFSIM ZA", titleStyle);
            GUI.Label(new Rect(margin + 145f, 8f, width - 390f, height), CourseSession.CourseName, courseStyle);
            GUI.Label(new Rect(margin + width - 245f, 8f, 115f, height), "HOLE " + (holeIndex + 1) + " / " + CourseSession.RoundLength, holeStyle);
            GUI.Label(new Rect(margin + width - 130f, 8f, 115f, height), "PAR " + parByHole[holeIndex] + "  •  " + currentHoleDistance.ToString("F0") + " m", courseStyle);
        }

        private void DrawShotCard()
        {
            float x = 16f;
            float y = 60f;
            float width = Mathf.Clamp(Screen.width * 0.17f, 205f, 250f);
            float height = 152f;

            GUI.Box(new Rect(x, y, width, height), GUIContent.none, smallPanel);
            GUI.Label(new Rect(x + 10f, y + 8f, width - 20f, 18f), "SHOT", titleStyle);
            GUI.Label(new Rect(x + 10f, y + 29f, width - 20f, 15f), status, smallStyle);

            ShotData shot = simulatorController != null ? simulatorController.LastShot : default(ShotData);
            bool hasShot = shot.IsValid;
            float row = y + 49f;
            CompactMetric("CLUB", hasShot ? shot.ClubName : "Driver", ref row, x, width);
            CompactMetric("BALL", hasShot ? shot.BallSpeedKph.ToString("F1") + " km/h" : "—", ref row, x, width);
            CompactMetric("LAUNCH", hasShot ? shot.LaunchAngleDeg.ToString("F1") + "°" : "—", ref row, x, width);
            CompactMetric("SPIN", hasShot ? shot.BackSpinRpm.ToString("F0") + " rpm" : "—", ref row, x, width);
            CompactMetric("CARRY", hasShot ? shot.CarryMeters.ToString("F1") + " m" : "—", ref row, x, width);
        }

        private void CompactMetric(string label, string value, ref float y, float x, float width)
        {
            GUI.Label(new Rect(x + 10f, y, width * 0.40f, 17f), label, smallStyle);
            GUI.Label(new Rect(x + width * 0.38f, y - 1f, width * 0.56f, 18f), value, rightValueStyle);
            y += 21f;
        }

        private void DrawWindCard()
        {
            float width = 128f;
            float x = (Screen.width - width) * 0.5f;
            float y = 58f;

            GUI.Box(new Rect(x, y, width, 40f), GUIContent.none, smallPanel);
            GUI.Label(new Rect(x + 8f, y + 3f, width - 16f, 16f), "0 MPH", centerStyle);
            GUI.Label(new Rect(x + 8f, y + 19f, width - 16f, 17f), "▲   •   W", courseStyle);
        }

        private void DrawHoleInfo()
        {
            float width = Mathf.Clamp(Screen.width * 0.22f, 245f, 320f);
            float x = Screen.width - width - 16f;
            float y = 60f;
            float height = 74f;

            GUI.Box(new Rect(x, y, width, height), GUIContent.none, smallPanel);
            GUI.Label(new Rect(x + 10f, y + 8f, 34f, 38f), (holeIndex + 1).ToString(), holeStyle);
            GUI.Label(new Rect(x + 50f, y + 7f, width - 62f, 22f), CourseSession.CourseName, valueStyle);
            GUI.Label(new Rect(x + 50f, y + 30f, width - 62f, 17f), "PAR " + parByHole[holeIndex] + "   •   " + currentHoleDistance.ToString("F0") + " m", smallStyle);
            GUI.Label(new Rect(x + 50f, y + 48f, width - 62f, 17f), "BLUE TEES", smallStyle);
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
                GUI.Label(new Rect(x + 15f, rowY + 25f, width * 0.50f, 15f), "Score  " + FormatScore(GetPlayerRelativeToPar(i)), playerMetaStyle);
                GUI.Label(new Rect(x + width * 0.51f, rowY + 6f, width * 0.40f, 22f), DistanceToPin(i).ToString("F0") + " m", distanceStyle);
                GUI.Label(new Rect(x + width * 0.51f, rowY + 29f, width * 0.40f, 13f), "to pin", smallStyle);
                rowY += rowHeight;
            }
        }

        private void DrawClubBar()
        {
            float width = Mathf.Clamp(Screen.width * 0.38f, 360f, 500f);
            float x = 16f;
            float y = Screen.height - 70f;
            float height = 54f;

            GUI.Box(new Rect(x, y, width, height), GUIContent.none, darkPanel);
            GUI.Label(new Rect(x + 8f, y + 5f, 45f, 16f), "CLUB", smallStyle);

            string[] clubs = { "Driver", "3W", "5i", "7i", "9i", "PW", "SW", "Putter" };
            float buttonWidth = (width - 62f) / clubs.Length;
            for (int i = 0; i < clubs.Length; i++)
            {
                Rect r = new Rect(x + 54f + buttonWidth * i, y + 7f, buttonWidth - 3f, 30f);
                bool active = selectedClubNumber == i + 1;
                if (GUI.Button(r, clubs[i], active ? activeClubStyle : clubStyle)) selectedClubNumber = i + 1;
                if (active)
                    GUI.DrawTexture(new Rect(r.x + 4f, r.yMax - 3f, r.width - 8f, 3f), blueBrightTexture);
            }
        }

        private void DrawMiniMap()
        {
            float width = Mathf.Clamp(Screen.width * 0.15f, 190f, 225f);
            float x = Screen.width - width - 16f;
            float y = Screen.height - 235f;
            float height = 205f;

            GUI.Box(new Rect(x, y, width, height), GUIContent.none, darkPanel);
            GUI.Label(new Rect(x + 10f, y + 8f, width - 20f, 18f), "HOLE MAP", titleStyle);
            DrawMiniMapGraphic(x + 10f, y + 30f, width - 20f, height - 42f);
        }

        private void DrawMiniMapGraphic(float x, float y, float width, float height)
        {
            GUI.DrawTexture(new Rect(x, y, width, height), darkerTexture, ScaleMode.StretchToFill);

            float center = x + width * 0.5f;
            GUI.DrawTexture(new Rect(center - 17f, y + 10f, 34f, height - 20f), greenTexture, ScaleMode.StretchToFill);
            GUI.DrawTexture(new Rect(center - 31f, y + height * 0.40f, 62f, 34f), greenTexture, ScaleMode.StretchToFill);
            GUI.DrawTexture(new Rect(center - 4f, y + 12f, 8f, 8f), redTexture, ScaleMode.StretchToFill);
            GUI.DrawTexture(new Rect(center - 5f, y + height - 19f, 10f, 10f), blueBrightTexture, ScaleMode.StretchToFill);
            GUI.Label(new Rect(x, y + 2f, width, 15f), "GREEN", mapStyle);
            GUI.Label(new Rect(x, y + height - 18f, width, 15f), "TEE", mapStyle);
        }

        private void DrawShotCompletionAction()
        {
            if (!shotFinished || ballFlightSimulator == null || ballFlightSimulator.IsInFlight) return;

            float width = Mathf.Clamp(Screen.width * 0.24f, 250f, 330f);
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - 67f;
            string text = waitingForNextPlayer
                ? "NEXT PLAYER  •  " + NextPlayerName()
                : (holeIndex + 1 < CourseSession.RoundLength ? "NEXT HOLE" : "FINISH ROUND");

            if (GUI.Button(new Rect(x, y, width, 40f), text, actionStyle))
            {
                if (waitingForNextPlayer) AdvancePlayer();
                else if (holeIndex + 1 < CourseSession.RoundLength) AdvanceHole();
                else FinishRound();
            }
        }

        private string FormatScore(int relativeToPar)
        {
            if (relativeToPar == 0) return "E";
            return relativeToPar > 0 ? "+" + relativeToPar : relativeToPar.ToString();
        }

        private int GetPlayerRelativeToPar(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= playerTotalStrokes.Length || playerTotalStrokes[playerIndex] == 0) return 0;

            int parToCount = 0;
            for (int i = 0; i < holesCompleted; i++)
                parToCount += parByHole[Mathf.Clamp(i, 0, parByHole.Length - 1)];

            if (playerHoleStrokes[playerIndex] > 0)
                parToCount += parByHole[Mathf.Clamp(holeIndex, 0, parByHole.Length - 1)];

            return playerTotalStrokes[playerIndex] - parToCount;
        }

        private float DistanceToPin(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= playerPositions.Length) return currentHoleDistance;

            Vector3 position = playerPositions[playerIndex];
            if (playerIndex == activePlayerIndex && ball != null) position = ball.position;
            if (pin == null) return currentHoleDistance;

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
            if (playerNames.Length == 0) return "Player 1";
            return playerNames[(activePlayerIndex + 1) % playerNames.Length];
        }

        private void AdvancePlayer()
        {
            SaveActivePlayerPosition();
            activePlayerIndex = (activePlayerIndex + 1) % playerNames.Length;
            shotFinished = false;
            waitingForNextPlayer = false;
            lastObservedShotCount = simulatorController != null && simulatorController.History != null ? simulatorController.History.Count : lastObservedShotCount;
            status = "READY • " + playerNames[activePlayerIndex];
            if (ball != null) ball.position = playerPositions[activePlayerIndex];
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
            status = "READY • " + playerNames[activePlayerIndex];

            for (int i = 0; i < playerPositions.Length; i++)
            {
                playerPositions[i] = new Vector3(0f, 0.04f, 0f);
                playerHoleStrokes[i] = 0;
            }

            if (ball != null) ball.position = playerPositions[activePlayerIndex];
            if (green != null)
            {
                green.position = new Vector3(0f, green.position.y, currentHoleDistance);
                green.localScale = new Vector3(14f, green.localScale.y, 8f);
            }
            if (pin != null) pin.position = new Vector3(0f, pin.position.y, currentHoleDistance);
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
            status = "ROUND COMPLETE";
            shotFinished = false;
            waitingForNextPlayer = false;
        }

        private int Sum(int[] values)
        {
            int total = 0;
            for (int i = 0; i < values.Length; i++) total += values[i];
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