using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace GolfSimZA.LaunchMonitors.GarminR10
{
    /// <summary>
    /// Starts the packaged GolfSimZA R10 Bluetooth bridge when present.
    /// The bridge connects directly to the paired R10 and forwards OpenConnect
    /// shot data to GolfSimZA on TCP port 921.
    /// </summary>
    public sealed class R10BridgeLauncher : MonoBehaviour
    {
        [SerializeField] private bool autoStart = true;
        [SerializeField] private bool closeBridgeOnDestroy = true;
        [SerializeField] private string executableRelativePath = "GolfSimZA-R10-Bridge/gspro-r10.exe";

        private Process bridgeProcess;

        public bool IsRunning => bridgeProcess != null && !bridgeProcess.HasExited;

        private void Start()
        {
            if (autoStart)
                StartBridge();
        }

        public bool StartBridge()
        {
            if (IsRunning)
                return true;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            string executablePath = Path.Combine(Application.streamingAssetsPath, executableRelativePath);
            if (!File.Exists(executablePath))
            {
                UnityEngine.Debug.LogWarning(
                    $"[GolfSimZA] R10 bridge executable not found at '{executablePath}'. " +
                    "The Unity receiver will still listen on TCP 921, but the R10 Bluetooth bridge must be started separately until the packaged bridge is installed.");
                return false;
            }

            try
            {
                bridgeProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = executablePath,
                        WorkingDirectory = Path.GetDirectoryName(executablePath),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = false,
                        RedirectStandardError = false
                    }
                };

                bridgeProcess.Start();
                UnityEngine.Debug.Log($"[GolfSimZA] Started packaged R10 bridge: {executablePath}");
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[GolfSimZA] Could not start packaged R10 bridge: {ex.Message}");
                bridgeProcess = null;
                return false;
            }
#else
            UnityEngine.Debug.LogWarning("[GolfSimZA] The packaged R10 Bluetooth bridge is currently Windows-only.");
            return false;
#endif
        }

        public void StopBridge()
        {
            if (!IsRunning)
            {
                bridgeProcess?.Dispose();
                bridgeProcess = null;
                return;
            }

            try
            {
                bridgeProcess.CloseMainWindow();
                if (!bridgeProcess.WaitForExit(1500))
                    bridgeProcess.Kill();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[GolfSimZA] R10 bridge shutdown warning: {ex.Message}");
            }
            finally
            {
                bridgeProcess.Dispose();
                bridgeProcess = null;
            }
        }

        private void OnDestroy()
        {
            if (closeBridgeOnDestroy)
                StopBridge();
        }
    }
}
