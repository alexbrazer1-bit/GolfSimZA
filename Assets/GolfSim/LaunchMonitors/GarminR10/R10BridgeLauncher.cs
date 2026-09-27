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
#if UNITY_EDITOR
            // A script reload in Play mode skips OnDestroy; stop the bridge first so it is not orphaned.
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += StopBridge;
#endif
            if (autoStart)
                StartBridge();
        }

        private void OnApplicationQuit()
        {
            StopBridge();
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

            StopOrphanedBridges(executablePath);

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
                // Parse settings.json numbers the same way on every regional setting (e.g. en-ZA uses ',').
                bridgeProcess.StartInfo.EnvironmentVariables["DOTNET_SYSTEM_GLOBALIZATION_INVARIANT"] = "1";

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
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= StopBridge;
#endif
            if (closeBridgeOnDestroy)
                StopBridge();
        }

        /// <summary>
        /// Stops bridges started from this same exe that are still running (e.g. after a crash),
        /// so only one bridge talks to the R10 and the exe can be updated.
        /// </summary>
        private static void StopOrphanedBridges(string executablePath)
        {
            string full = Path.GetFullPath(executablePath);
            foreach (Process p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(full)))
            {
                try
                {
                    if (p.MainModule == null || !string.Equals(Path.GetFullPath(p.MainModule.FileName), full, StringComparison.OrdinalIgnoreCase)) continue;
                    p.Kill();
                    p.WaitForExit(3000);
                    UnityEngine.Debug.Log($"[GolfSimZA] Stopped an old R10 bridge (process {p.Id}) before starting a new one.");
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[GolfSimZA] Could not check R10 bridge process: {ex.Message}");
                }
                finally
                {
                    p.Dispose();
                }
            }
        }
    }
}
