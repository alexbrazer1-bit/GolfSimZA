using GolfSimZA.Core;
using UnityEngine;

namespace GolfSimZA.UI
{
    public sealed class SimulatorHUD : MonoBehaviour
    {
        [SerializeField] private SimulatorController simulatorController;

        private GUIStyle titleStyle;
        private GUIStyle sectionStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;
        private bool stylesReady;

        private void EnsureStyles()
        {
            if (stylesReady)
                return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold
            };
            sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12
            };
            stylesReady = true;
        }

        private void OnGUI()
        {
            EnsureStyles();

            if (simulatorController == null)
                return;

            ShotData shot = simulatorController.LastShot;
            bool hasShot = shot.IsValid;

            GUILayout.BeginArea(new Rect(18, 18, 430, 370), GUI.skin.box);
            GUILayout.Label("GOLFSIM ZA", titleStyle);
            GUILayout.Label($"Launch monitor: {simulatorController.LaunchMonitorName}", smallStyle);
            GUILayout.Label(
                simulatorController.IsLaunchMonitorConnected ? "● CONNECTED" : "● TEST / DISCONNECTED",
                sectionStyle);
            GUILayout.Space(6);

            GUILayout.Label("LATEST SHOT", sectionStyle);
            if (hasShot)
            {
                GUILayout.Label($"{shot.ClubName}  •  {shot.ClubLoftDeg:F1}° loft", bodyStyle);
                GUILayout.Label($"Ball {shot.BallSpeedKph:F1} km/h   |   Club {shot.ClubSpeedKph:F1} km/h", bodyStyle);
                GUILayout.Label($"Launch {shot.LaunchAngleDeg:F1}°   |   Direction {shot.LaunchDirectionDeg:F1}°", bodyStyle);
                GUILayout.Label($"Backspin {shot.BackSpinRpm:F0} rpm   |   Spin axis {shot.SpinAxisDeg:F1}°", bodyStyle);
                GUILayout.Space(4);
                GUILayout.Label($"Carry {shot.CarryMeters:F1} m   |   Total {shot.TotalMeters:F1} m", bodyStyle);
            }
            else
            {
                GUILayout.Label("No shot recorded yet.", bodyStyle);
            }

            GUILayout.Space(6);
            GUILayout.Label("CLUBS", sectionStyle);
            GUILayout.Label("1 Driver   2 3W   3 5i   4 7i   5 9i   6 PW   7 SW   8 Putter", smallStyle);
            GUILayout.Label("Press SPACE to hit the selected club.", smallStyle);

            GUILayout.Space(6);
            int historyCount = simulatorController.History?.Count ?? 0;
            GUILayout.Label($"Shot history: {historyCount} recorded", smallStyle);

            if (simulatorController.History != null && historyCount > 0)
            {
                int start = Mathf.Max(0, historyCount - 3);
                for (int i = historyCount - 1; i >= start; i--)
                {
                    ShotData historyShot = simulatorController.History.Shots[i];
                    GUILayout.Label(
                        $"• {historyShot.ClubName}: {historyShot.TotalMeters:F0} m | {historyShot.BallSpeedKph:F0} km/h",
                        smallStyle);
                }
            }

            GUILayout.EndArea();
        }
    }
}
