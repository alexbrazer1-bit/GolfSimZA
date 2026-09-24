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
        private bool stylesReady;

        private int holeIndex;
        private int holeStartShotCount;
        private int holeStrokes;
        private int totalStrokes;
        private int holesCompleted;
        private int lastObservedShotCount;
        private bool wasInFlight;
        private bool shotFinished;
        private string status = "Ready to tee off";
        private float currentHoleDistance;

        private void Awake()
        {
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
                holeStrokes += shotCount - lastObservedShotCount;
                lastObservedShotCount = shotCount;
                shotFinished = false;
                status = "Ball in flight…";
            }

            bool inFlight = ballFlightSimulator.IsInFlight;
            if (wasInFlight && !inFlight && holeStrokes > 0)
            {
                shotFinished = true;
                status = $"Shot complete • Carry {ballFlightSimulator.CarryMeters:F1} m • Total {ballFlightSimulator.TotalMeters:F1} m";
            }
            wasInFlight = inFlight;
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;

            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
            sectionStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 15, fixedHeight = 38 };
            stylesReady = true;
        }

        private void OnGUI()
        {
            EnsureStyles();

            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);
            GUILayout.BeginArea(new Rect(24, 20, Mathf.Min(610, Screen.width - 48), Screen.height - 40));

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
            GUILayout.Label($"STROKES  {holeStrokes}    •    ROUND  {totalStrokes}", bodyStyle);

            if (holeStrokes > 0 && !ballFlightSimulator.IsInFlight)
            {
                int relativeToPar = holeStrokes - parByHole[holeIndex];
                string scoreText = relativeToPar == 0 ? "Even" : relativeToPar > 0 ? $"+{relativeToPar}" : relativeToPar.ToString();
                GUILayout.Label($"Hole score: {scoreText}", smallStyle);
            }

            GUILayout.Space(12);
            GUILayout.Label("CLUBS", sectionStyle);
            GUILayout.Label("1 Driver   2 3W   3 5i   4 7i   5 9i   6 PW   7 SW   8 Putter", smallStyle);
            GUILayout.Label("Press 1-8 to select a club, then SPACE to hit.", smallStyle);
            GUILayout.Space(8);

            if (shotFinished && holeStrokes > 0 && !ballFlightSimulator.IsInFlight)
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
        }

        private void StartHole()
        {
            holeStrokes = 0;
            shotFinished = false;
            status = "Ready to tee off";
            lastObservedShotCount = simulatorController != null && simulatorController.History != null
                ? simulatorController.History.Count
                : 0;
            holeStartShotCount = lastObservedShotCount;
            currentHoleDistance = GetHoleDistance(holeIndex);
            wasInFlight = false;

            if (ball != null)
                ball.position = new Vector3(0f, 0.04f, 0f);

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
            totalStrokes += holeStrokes;
            holesCompleted++;
            holeIndex++;
            StartHole();
        }

        private void FinishRound()
        {
            totalStrokes += holeStrokes;
            holesCompleted = CourseSession.RoundLength;
            status = $"Round complete • {totalStrokes} strokes";
            shotFinished = false;
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
