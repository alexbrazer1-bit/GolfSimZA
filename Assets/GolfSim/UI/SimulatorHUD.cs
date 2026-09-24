using GolfSimZA.Core;
using UnityEngine;

namespace GolfSimZA.UI
{
    public sealed class SimulatorHUD : MonoBehaviour
    {
        [SerializeField] private SimulatorController simulatorController;

        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;
        private bool stylesReady;

        private void EnsureStyles()
        {
            if (stylesReady)
                return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14
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

            GUILayout.BeginArea(new Rect(24, 24, 560, 430), GUI.skin.box);
            GUILayout.Label("GOLFSIM ZA", titleStyle);
            GUILayout.Label($"Launch monitor: {simulatorController.LaunchMonitorName}", bodyStyle);
            GUILayout.Label($"Status: {(simulatorController.IsLaunchMonitorConnected ? "CONNECTED" : "TEST / DISCONNECTED")}", bodyStyle);
            GUILayout.Space(8);

            if (hasShot)
            {
                GUILayout.Label($"Club: {shot.ClubName}  |  Loft: {shot.ClubLoftDeg:F1}°", bodyStyle);
                GUILayout.Label($"Ball speed: {shot.BallSpeedKph:F1} km/h", bodyStyle);
                GUILayout.Label($"Club speed: {shot.ClubSpeedKph:F1} km/h", bodyStyle);
                GUILayout.Label($"Launch: {shot.LaunchAngleDeg:F1}°  |  Direction: {shot.LaunchDirectionDeg:F1}°", bodyStyle);
                GUILayout.Label($"Backspin: {shot.BackSpinRpm:F0} rpm  |  Spin axis: {shot.SpinAxisDeg:F1}°", bodyStyle);
            }
            else
            {
                GUILayout.Label("No shot recorded yet.", bodyStyle);
            }

            GUILayout.Space(10);
            GUILayout.Label("TEST CLUBS: 1 Driver  2 3W  3 5i  4 7i  5 9i  6 PW  7 SW  8 Putter", smallStyle);
            GUILayout.Label("Press SPACE to hit the selected club.", smallStyle);
            GUILayout.Space(8);
            GUILayout.Label($"Shot history: {simulatorController.History?.Count ?? 0} recorded", smallStyle);

            if (simulatorController.History != null)
            {
                int start = Mathf.Max(0, simulatorController.History.Count - 5);
                for (int i = simulatorController.History.Count - 1; i >= start; i--)
                {
                    ShotData historyShot = simulatorController.History.Shots[i];
                    GUILayout.Label($"• {historyShot.ClubName}: {historyShot.BallSpeedKph:F0} km/h | {historyShot.LaunchAngleDeg:F1}° | {historyShot.BackSpinRpm:F0} rpm", smallStyle);
                }
            }

            GUILayout.EndArea();
        }
    }
}
