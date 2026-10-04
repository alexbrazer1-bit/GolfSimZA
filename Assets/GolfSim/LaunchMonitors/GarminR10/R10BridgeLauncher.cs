using UnityEngine;

namespace GolfSimZA.LaunchMonitors.GarminR10
{
    /// <summary>
    /// Play-scene switch for the packaged GolfSimZA R10 Bluetooth bridge. The bridge itself is run
    /// by <see cref="R10BridgeService"/>, which lives for the whole app - so leaving this scene does
    /// NOT stop the bridge (the R10 stays connected between rounds, the range and mini games).
    /// </summary>
    public sealed class R10BridgeLauncher : MonoBehaviour
    {
        [SerializeField] private bool autoStart = true;
        [Tooltip("Not used any more: the bridge keeps running between scenes and stops when the app quits.")]
        [SerializeField] private bool closeBridgeOnDestroy = true;
        [SerializeField] private string executableRelativePath = "GolfSimZA-R10-Bridge/gspro-r10.exe";

        /// <summary>
        /// RESTART R10 BRIDGE (Settings → GAME → SYSTEM): stops every running GolfSim ZA bridge; the
        /// app's bridge runner starts a fresh one within a few seconds.
        /// </summary>
        public static int RestartAll() => R10BridgeService.RestartAll();

        public bool IsRunning => R10BridgeService.Instance != null && R10BridgeService.Instance.IsRunning;

        private void Start()
        {
            if (autoStart) StartBridge();
        }

        /// <summary>Saved scene option from older versions (the bridge now always keeps running between scenes).</summary>
        public bool CloseBridgeOnDestroy => closeBridgeOnDestroy;

        public bool StartBridge()
        {
            R10BridgeService service = R10BridgeService.Get(executableRelativePath);
            return service != null && service.StartBridge();
        }

        /// <summary>Stops the app's bridge (it is not restarted until something starts it again).</summary>
        public void StopBridge() => R10BridgeService.Instance?.StopBridge();
    }
}
