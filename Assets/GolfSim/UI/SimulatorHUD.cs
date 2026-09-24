using GolfSimZA.Core;
using GolfSimZA.LaunchMonitors;
using UnityEngine;

namespace GolfSimZA.UI
{
    public sealed class SimulatorHUD : MonoBehaviour
    {
        [SerializeField] private SimulatorController simulatorController;
        [SerializeField] private MonoBehaviour launchMonitorBehaviour;

        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;

        private void Awake()
        {
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
        }

        private void OnGUI()
        {
            if (simulatorController == null)
                return;

            ILaunchMonitorAdapter monitor = launchMonitorBehaviour as ILaunchMonitorAdapter;
            ShotData shot = simulatorController.LastShot;

            GUILayout.BeginArea(new Rect(24, 24, 420, 260), GUI.skin.box);
            GUILayout.Label("GOLFSIM ZA", titleStyle);
            GUILayout.Label($"Launch monitor: {monitor?.DeviceName ?? "Not configured"}", bodyStyle);
            GUILayout.Label($"Status: {(monitor != null && monitor.IsConnected ? "CONNECTED" : "TEST / DISCONNECTED")}", bodyStyle);
            GUILayout.Space(10);
            GUILayout.Label($"Ball speed: {shot.BallSpeedKph:F1} km/h", bodyStyle);
            GUILayout.Label($"Launch: {shot.LaunchAngleDeg:F1}°", bodyStyle);
            GUILayout.Label($"Backspin: {shot.BackSpinRpm:F0} rpm", bodyStyle);
            GUILayout.Label("Press SPACE for a test shot", bodyStyle);
            GUILayout.EndArea();
        }
    }
}
