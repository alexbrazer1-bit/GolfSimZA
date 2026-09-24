using System;
using System.Collections.Generic;
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

        private GUIStyle titleStyle;
        private GUIStyle sectionStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;
        private GUIStyle buttonStyle;
        private GUIStyle sidePanelStyle;
        private GUIStyle playerStyle;
        private GUIStyle activePlayerStyle;
        private GUIStyle playerMetaStyle;
        private GUIStyle distanceStyle;
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

        private readonly Color navy = new Color(0.025f, 0.10f, 0.14f);
        private readonly Color panel = new Color(0.055f, 0.16f, 0.20f);
        private readonly Color activePanel = new Color(0.08f, 0.24f, 0.30f);
        private readonly Color muted = new Color(0.70f, 0.78f, 0.81f);

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
                status = playerNames[activePlayerIndex] + " • Ball in flight…";
            }

            bool inFlight = ballFlightSimulator.IsInFlight;
            if (wasInFlight && !inFlight && playerHoleStrokes[activePlayerIndex] > 0)
            {
                SaveActivePlayerPosition();
                shotFinished = true;
                waitingForNextPlayer = playerNames.Length > 1;
                status = $"{playerNames[activePlayerIndex]} • Shot complete • {ballFlightSimulator.CarryMeters:F1} m carry • {ballFlightSimulator.TotalMeters:F1} m total";
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

            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            sectionStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = Color.white } };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = muted } };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold, fixedHeight = 38 };
            sidePanelStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(12, 12, 12, 12), normal = { background = MakeTexture(panel) } };
            playerStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            activePlayerStyle = new GUIStyle(playerStyle) { fontSize = 16, normal = { textColor = new Color(0.35f, 0.85f, 1f) } };
            playerMetaStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = muted } };
            distanceStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight, normal = { textColor = Color.white } };
            stylesReady = true;
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

            GUI.backgroundColor = navy;
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);

            float margin = Mathf.Max(24f, Screen.width * 0.025f);
            float sidebarWidth = Mathf.Clamp(Screen.width * 0.20f, 230f, 330f);
            float contentWidth = Screen.width - sidebarWidth - margin * 2f;

            GUILayout.BeginArea(new Rect(margin, 20f, contentWidth, Screen.height - 40f));
            GUILayout.Label("GOLFSIM ZA", titleStyle);
            GUILayout.Label($"0.6 • PLAY ROUND   {CourseSession.CourseName}", sectionStyle);
            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"HOLE {holeIndex + 1} / {CourseSession.RoundLength}", sectionStyle);
            GUILayout.Label($"PAR {parByHole[holeIndex]}", sectionStyle);
            GUILayout.Label($"{Mathf.RoundToInt(currentHoleDistance)} m", sectionStyle);
            GUILayout.EndHorizontal();

            GUILayout.Label($"{CourseSession.TeeName} tees  •  {status}", bodyStyle);
            GUILayout.Space(6);
            GUILayout.Label($"STROKES  {playerHoleStrokes[activePlayerIndex]}    •    ROUND  {totalStrokes}", bodyStyle);

            if (playerHoleStrokes[activePlayerIndex] > 0 && !ballFlightSimulator.IsInFlight)
            {
                int relativeToPar = playerHoleStrokes[activePlayerIndex] - parByHole[holeIndex];
                string scoreText = relativeToPar == 0 ? "Even" : relativeToPar > 0 ? $"+{relativeToPar}" : relativeToPar.ToString();
                GUILayout.Label($"{playerNames[activePlayerIndex]} hole score: {scoreText}", smallStyle);
            }

            GUILayout.Space(12);
            GUILayout.Label("CLUBS", sectionStyle);
            GUILayout.Label("1 Driver   2 3W   3 5i   4 7i   5 9i   6 PW   7 SW   8 Putter", smallStyle);
            GUILayout.Label("Press 1-8 to select a club, then SPACE to hit.", smallStyle);
            GUILayout.Space(8);

            if (shotFinished && waitingForNextPlayer && !ballFlightSimulator.IsInFlight)
            {
                if (GUILayout.Button($"NEXT PLAYER  •  {NextPlayerName()}", buttonStyle))
                    AdvancePlayer();
            }
            else if (shotFinished && !ballFlightSimulator.IsInFlight)
            {
                if (holeIndex + 1 < CourseSession.RoundLength)
                {
                    if (GUILayout.Button($"FINISH HOLE {holeIndex + 1} / NEXT HOLE", buttonStyle))
                        AdvanceHole();
                }
                else if (GUILayout.Button("FINISH ROUND", buttonStyle))
                {
                    FinishRound();
                }
            }

            if (GUILayout.Button("BACK TO COURSE SELECT", buttonStyle))
                SceneManager.LoadScene("GolfSimZA_0_5_CourseSelection");

            GUILayout.Space(8);
            GUILayout.Label("Prototype round: course geometry is procedural and will be replaced by licensed/original course assets later.", smallStyle);
            GUILayout.EndArea();

            DrawPlayerSidebar(margin + contentWidth + 16f, margin, sidebarWidth - 8f);
        }

        private void DrawPlayerSidebar(float x, float y, float width)
        {
            GUILayout.BeginArea(new Rect(x, y, width, Screen.height - y - 20f), sidePanelStyle);
            GUILayout.Label("PLAYERS", sectionStyle);
            GUILayout.Label("SCORE  •  DISTANCE TO PIN", smallStyle);
            GUILayout.Space(8);

            for (int i = 0; i < playerNames.Length; i++)
            {
                bool active = i == activePlayerIndex;
                GUI.Box(GUILayoutUtility.GetRect(1f, active ? 86f : 76f), GUIContent.none, active ? GUI.skin.box : GUI.skin.box);
                Rect row = GUILayoutUtility.GetLastRect();
                row.x += 6f;
                row.width -= 12f;

                GUIStyle nameStyle = active ? activePlayerStyle : playerStyle;
                GUI.Label(new Rect(row.x, row.y + 8f, row.width * 0.52f, 24f), active ? "● " + playerNames[i] : playerNames[i], nameStyle);

                int relativeToPar = GetPlayerRelativeToPar(i);
                string score = playerTotalStrokes[i] == 0 ? "E" : FormatScore(relativeToPar);
                GUI.Label(new Rect(row.x, row.y + 34f, row.width * 0.52f, 22f), $"Score  {score}  •  {playerTotalStrokes[i]} shots", playerMetaStyle);

                float distance = DistanceToPin(i);
                GUI.Label(new Rect(row.x + row.width * 0.50f, row.y + 20f, row.width * 0.48f, 34f), $"{distance:F0} m", distanceStyle);
                GUI.Label(new Rect(row.x + row.width * 0.50f, row.y + 51f, row.width * 0.48f, 18f), "to pin", playerMetaStyle);

                GUILayout.Space(5);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label($"{playerNames.Length} player{(playerNames.Length == 1 ? "" : "s")}  •  Hole {holeIndex + 1}", smallStyle);
            GUILayout.EndArea();
        }

        private string FormatScore(int relativeToPar)
        {
            if (relativeToPar == 0) return "E";
            return relativeToPar > 0 ? "+" + relativeToPar : relativeToPar.ToString();
        }

        private int GetPlayerRelativeToPar(int playerIndex)
        {
            int completedHolePar = 0;
            for (int i = 0; i < holesCompleted; i++)
                completedHolePar += parByHole[Mathf.Clamp(i, 0, parByHole.Length - 1)];

            int currentPar = parByHole[holeIndex];
            int currentStrokes = playerIndex == activePlayerIndex ? playerHoleStrokes[playerIndex] : playerHoleStrokes[playerIndex];
            int holesPar = completedHolePar + currentPar;
            return playerTotalStrokes[playerIndex] - holesPar;
        }

        private float DistanceToPin(int playerIndex)
        {
            Vector3 position = playerPositions[playerIndex];
            if (playerIndex == activePlayerIndex && ball != null)
                position = ball.position;

            if (pin == null)
                return 0f;

            Vector3 delta = pin.position - position;
            delta.y = 0f;
            return delta.magnitude / Mathf.Max(0.0001f, ballFlightSimulator != null ? GetMetersToUnity() : 1f);
        }

        private float GetMetersToUnity()
        {
            // Current prototype uses a 1:1 world scale. Keeping this in one place
            // makes the sidebar ready for real course scaling later.
            return 1f;
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
            status = playerNames[activePlayerIndex] + " • Your turn";

            if (ball != null)
                ball.position = playerPositions[activePlayerIndex];
        }

        private void StartHole()
        {
            holeIndex = Mathf.Clamp(holeIndex, 0, CourseSession.RoundLength - 1);
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
                case "White": teeMultiplier = 0.94f; break;
                case "Black": teeMultiplier = 1.08f; break;
            }

            float courseMultiplier = 1f;
            if (CourseSession.CourseName == "Karoo Valley Demo Course") courseMultiplier = 1.03f;
            if (CourseSession.CourseName == "Free State Hills Demo Course") courseMultiplier = 0.97f;

            return distanceByHole[safeIndex] * teeMultiplier * courseMultiplier;
        }
    }
}
