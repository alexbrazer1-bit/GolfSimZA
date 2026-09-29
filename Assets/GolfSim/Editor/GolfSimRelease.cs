#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using GolfSimZA.Core;
using GolfSimZA.Courses;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace GolfSimZA.Editor
{
    /// <summary>
    /// Release tools: builds the Windows player (GolfSimZA.exe), bundles the R10 bridge,
    /// makes the installer (GolfSimZA-Setup-x.y.z.exe) and publishes it to the local update
    /// folder that the in-game UPDATE button reads.
    /// Menu: GolfSimZA → Release → Release Manager.
    /// Command line: Unity.exe -batchmode -quit -projectPath . -executeMethod GolfSimZA.Editor.GolfSimRelease.BuildAndPublishFromCommandLine -notes "..."
    /// </summary>
    public sealed class GolfSimRelease : EditorWindow
    {
        public const string BuildFolder = "Builds/GolfSimZA";
        public const string ExeName = "GolfSimZA.exe";
        private const string BridgeStreamingFolder = "Assets/StreamingAssets/GolfSimZA-R10-Bridge";
        private const string BridgeExe = "gspro-r10.exe";
        private const string UpdateFolderPref = "GolfSimZA.Release.UpdateFolder";
        private const string VersionFile = "Assets/GolfSim/Core/GolfSimVersion.cs";

        private string notes = "";
        private string updateFolder;
        private string status = "";
        private Vector2 scroll;

        public static string UpdateFolder
        {
            get => EditorPrefs.GetString(UpdateFolderPref, AppSettings.DefaultUpdateFolder);
            set => EditorPrefs.SetString(UpdateFolderPref, value);
        }

        [MenuItem("GolfSimZA/Release/Release Manager", priority = 0)]
        public static void Open()
        {
            GolfSimRelease window = GetWindow<GolfSimRelease>(true, "GolfSimZA Release Manager");
            window.minSize = new Vector2(560, 420);
            window.updateFolder = UpdateFolder;
        }

        [MenuItem("GolfSimZA/Release/Build Windows Player Only", priority = 20)]
        public static void BuildPlayerMenu()
        {
            if (BuildPlayer(interactive: true, out string error)) EditorUtility.DisplayDialog("GolfSimZA", "Build finished:\n" + Path.GetFullPath(Path.Combine(BuildFolder, ExeName)), "OK");
            else EditorUtility.DisplayDialog("GolfSimZA build failed", error, "OK");
        }

        [MenuItem("GolfSimZA/Release/Open Update Folder", priority = 40)]
        public static void OpenUpdateFolder()
        {
            Directory.CreateDirectory(UpdateFolder);
            EditorUtility.RevealInFinder(UpdateFolder);
        }

        private void OnGUI()
        {
            if (updateFolder == null) updateFolder = UpdateFolder;
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.LabelField("GolfSimZA Release Manager", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("1. Bump the version.  2. Write what changed.  3. Build + Publish.\n" +
                                    "The simulator PC then shows UPDATE AVAILABLE and installs it with one click.", MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Current version", GolfSimVersion.Version, EditorStyles.boldLabel);
            string published = LatestPublishedVersion(updateFolder);
            EditorGUILayout.LabelField("Latest published", string.IsNullOrEmpty(published) ? "(nothing published yet)" : published);

            bool versionOk = string.IsNullOrEmpty(published) || CompareVersions(GolfSimVersion.Version, published) > 0;
            if (!versionOk)
                EditorGUILayout.HelpBox("The current version is not newer than the latest published one. Bump the version first, otherwise the simulator PC will not offer the update.", MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Bump patch (x.y.Z)")) BumpVersion(2);
            if (GUILayout.Button("Bump minor (x.Y.0)")) BumpVersion(1);
            if (GUILayout.Button("Bump major (X.0.0)")) BumpVersion(0);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("What changed (shown in the game)");
            notes = EditorGUILayout.TextArea(notes, new GUIStyle(EditorStyles.textArea) { wordWrap = true }, GUILayout.MinHeight(60), GUILayout.MaxWidth(position.width - 12f));

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            updateFolder = EditorGUILayout.TextField("Update folder", updateFolder);
            if (GUILayout.Button("Open", GUILayout.Width(60))) { UpdateFolder = updateFolder; OpenUpdateFolder(); }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("R10 bridge", BridgeSourceDescription());

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Build Windows Player", GUILayout.Height(34)))
            {
                UpdateFolder = updateFolder;
                status = BuildPlayer(true, out string error) ? "Player built: " + Path.GetFullPath(BuildFolder) : "Build failed: " + error;
            }
            GUI.enabled = versionOk;
            if (GUILayout.Button("Build + Publish Update", GUILayout.Height(34)))
            {
                UpdateFolder = updateFolder;
                status = BuildAndPublish(true, notes, updateFolder, out string error) ? $"Version {GolfSimVersion.Version} published to {updateFolder}" : "Failed: " + error;
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (File.Exists(InstallerPath(updateFolder)) && GUILayout.Button("Install " + GolfSimVersion.Version + " on this PC (test the installer)"))
                InstallOnThisPc();

            if (!string.IsNullOrEmpty(status))
                EditorGUILayout.HelpBox(status, status.StartsWith("Fail") || status.StartsWith("Build failed") ? MessageType.Error : MessageType.Info);

            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------ Version

        private static void BumpVersion(int part)
        {
            string[] pieces = GolfSimVersion.Version.Split('.');
            int[] numbers = new int[3];
            for (int i = 0; i < 3; i++) numbers[i] = i < pieces.Length && int.TryParse(pieces[i], out int n) ? n : 0;
            numbers[part]++;
            for (int i = part + 1; i < 3; i++) numbers[i] = 0;
            string next = $"{numbers[0]}.{numbers[1]}.{numbers[2]}";

            string text = File.ReadAllText(VersionFile);
            string updated = Regex.Replace(text, "public const string Version = \"[^\"]*\";", $"public const string Version = \"{next}\";");
            File.WriteAllText(VersionFile, updated);
            AssetDatabase.ImportAsset(VersionFile);
            Debug.Log("[GolfSimZA] Version bumped to " + next + " (scripts will recompile).");
        }

        private static int CompareVersions(string a, string b)
        {
            return Version.TryParse(a, out Version va) && Version.TryParse(b, out Version vb) ? va.CompareTo(vb) : string.CompareOrdinal(a, b);
        }

        private static string LatestPublishedVersion(string folder)
        {
            try
            {
                string path = Path.Combine(folder ?? "", "latest.json");
                if (!File.Exists(path)) return "";
                Match m = Regex.Match(File.ReadAllText(path), "\"latestVersion\"\\s*:\\s*\"([^\"]*)\"");
                return m.Success ? m.Groups[1].Value : "";
            }
            catch
            {
                return "";
            }
        }

        // ------------------------------------------------------------ Build

        public static bool BuildAndPublish(bool interactive, string releaseNotes, string folder, out string error)
        {
            if (!BuildPlayer(interactive, out error)) return false;
            return Publish(releaseNotes, folder, out error);
        }

        public static bool BuildPlayer(bool interactive, out string error)
        {
            error = null;
            if (interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                error = "Cancelled.";
                return false;
            }

            try
            {
                EnsureScenes();
                GolfSimBuildScenes.Apply();
                List<string> missing = GolfSimBuildScenes.Missing();
                if (missing.Count > 0)
                {
                    error = "Scenes missing: " + string.Join(", ", missing);
                    return false;
                }

                ConfigurePlayerSettings();
                IncludeFallbackShaders();

                if (!InstallBridge(out string bridgeMessage))
                {
                    if (interactive && !EditorUtility.DisplayDialog("R10 bridge not found", bridgeMessage + "\n\nBuild without the bridge? (Keyboard test shots still work; the R10 bridge must then be started separately.)", "Build anyway", "Cancel"))
                    {
                        error = "Cancelled: " + bridgeMessage;
                        return false;
                    }
                    Debug.LogWarning("[GolfSimZA] " + bridgeMessage);
                }

                var scenes = new List<string>();
                foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                    if (scene.enabled) scenes.Add(scene.path);

                string output = Path.Combine(BuildFolder, ExeName);
                if (Directory.Exists(BuildFolder)) Directory.Delete(BuildFolder, true);

                var options = new BuildPlayerOptions
                {
                    scenes = scenes.ToArray(),
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.None
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    error = $"Unity build {report.summary.result} with {report.summary.totalErrors} errors. See the Console.";
                    return false;
                }

                Debug.Log($"[GolfSimZA] Built {GolfSimVersion.Version}: {Path.GetFullPath(output)} ({report.summary.totalSize / 1048576} MB)");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Debug.LogException(ex);
                return false;
            }
        }

        private static void EnsureScenes()
        {
            if (!File.Exists(GolfSimBuildScenes.CourseSelection) || !File.Exists(GolfSimBuildScenes.RoundSettings) || !File.Exists(GolfSimBuildScenes.Players))
                CreateCourseRoundFlow.CreateFlow();

            // Creates Play Round when missing and makes sure the R10 receiver, bridge
            // launcher and router are wired in (safe to run repeatedly).
            CreatePlayableR10.Create();
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.productName = GolfSimVersion.ProductName;
            PlayerSettings.bundleVersion = GolfSimVersion.Version;
            PlayerSettings.runInBackground = true;          // keep receiving R10 shots when the window loses focus
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.usePlayerLog = true;
            // Linear lighting: GSPro courses are made in linear colour space; in gamma they look flat and washed out.
            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        }

        /// <summary>Course shader repair needs these built-in shaders to exist in the player.</summary>
        private static void IncludeFallbackShaders()
        {
            UnityEngine.Object graphicsSettings = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/GraphicsSettings.asset");
            if (graphicsSettings == null)
            {
                Debug.LogWarning("[GolfSimZA] GraphicsSettings.asset not found; imported courses may render pink.");
                return;
            }

            var serialized = new SerializedObject(graphicsSettings);
            SerializedProperty list = serialized.FindProperty("m_AlwaysIncludedShaders");
            var existing = new HashSet<Shader>();
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is Shader s) existing.Add(s);

            int added = 0;
            foreach (string name in ShaderRepair.FallbackShaders)
            {
                Shader shader = Shader.Find(name);
                if (shader == null || existing.Contains(shader)) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                existing.Add(shader);
                added++;
            }

            if (added > 0)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                Debug.Log($"[GolfSimZA] Added {added} shaders to Always Included Shaders.");
            }
        }

        // ------------------------------------------------------------ R10 bridge

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static string BridgePublishFolder => Path.Combine(ProjectRoot, "R10Bridge", "publish");
        private static string BridgeZip => Path.Combine(ProjectRoot, "R10Bridge", "GolfSimZA-R10-Bridge-win-x64.zip");

        private static string BridgeSourceDescription()
        {
            if (File.Exists(Path.Combine(BridgePublishFolder, BridgeExe))) return "R10Bridge/publish (built locally)";
            if (File.Exists(BridgeZip)) return "R10Bridge/GolfSimZA-R10-Bridge-win-x64.zip";
            if (File.Exists(Path.Combine(ProjectRoot, BridgeStreamingFolder, BridgeExe))) return "already in StreamingAssets";
            if (File.Exists(Path.Combine(ProjectRoot, "R10Bridge", "_upstream", "gspro-r10.csproj"))) return "will be built from R10Bridge/_upstream during the build";
            return "NOT FOUND - download the GitHub Actions artifact into R10Bridge/";
        }

        /// <summary>Copies the bridge into StreamingAssets so it ships inside the player.</summary>
        private static bool InstallBridge(out string message)
        {
            string target = Path.Combine(ProjectRoot, BridgeStreamingFolder);
            string settings = Path.Combine(ProjectRoot, "R10Bridge", "settings.json");

            try
            {
                if (File.Exists(Path.Combine(BridgePublishFolder, BridgeExe)))
                {
                    CopyFolder(BridgePublishFolder, target);
                }
                else if (File.Exists(BridgeZip))
                {
                    string unpacked = Path.Combine(Path.GetTempPath(), "GolfSimZA-R10-Bridge-zip");
                    if (Directory.Exists(unpacked)) Directory.Delete(unpacked, true);
                    ZipFile.ExtractToDirectory(BridgeZip, unpacked);
                    CopyFolder(unpacked, target);
                }
                else if (BuildBridge(out string bridgeError))
                {
                    CopyFolder(BridgePublishFolder, target);
                }
                else if (!File.Exists(Path.Combine(target, BridgeExe)))
                {
                    message = "The R10 bridge could not be built (" + bridgeError + "). See Logs/GolfSimZA-bridge.log, or put the GitHub Actions zip 'GolfSimZA-R10-Bridge-win-x64.zip' in the R10Bridge folder.";
                    return false;
                }

                if (File.Exists(settings)) File.Copy(settings, Path.Combine(target, "settings.json"), true);
                AssetDatabase.Refresh();
                message = "R10 bridge included.";
                return File.Exists(Path.Combine(target, BridgeExe));
            }
            catch (Exception ex)
            {
                message = "Could not copy the R10 bridge: " + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Copies changed files only (identical files - including a running bridge exe - are left alone),
        /// so Unity's .meta files are kept and a locked exe does not break the build.
        /// </summary>
        private static void CopyFolder(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(dir.Replace(source, target));
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string destination = file.Replace(source, target);
                if (SameFile(file, destination)) continue;
                try
                {
                    File.Copy(file, destination, true);
                }
                catch (IOException) when (StopBridgeProcesses(target) > 0)
                {
                    File.Copy(file, destination, true); // retry once the stale bridge has exited
                }
                catch (UnauthorizedAccessException) when (StopBridgeProcesses(target) > 0)
                {
                    File.Copy(file, destination, true);
                }
            }
        }

        private static bool SameFile(string a, string b)
        {
            if (!File.Exists(b)) return false;
            var fa = new FileInfo(a);
            var fb = new FileInfo(b);
            if (fa.Length != fb.Length) return false;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            using (FileStream sa = File.OpenRead(a))
            using (FileStream sb = new FileStream(b, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                byte[] ha = sha.ComputeHash(sa);
                byte[] hb = sha.ComputeHash(sb);
                for (int i = 0; i < ha.Length; i++) if (ha[i] != hb[i]) return false;
                return true;
            }
        }

        /// <summary>Stops bridge processes running from the given folder (left over from Play mode). Returns how many.</summary>
        private static int StopBridgeProcesses(string folder)
        {
            int stopped = 0;
            string full = Path.GetFullPath(folder).TrimEnd('\\', '/');
            foreach (Process p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(BridgeExe)))
            {
                try
                {
                    string path = p.MainModule != null ? Path.GetDirectoryName(p.MainModule.FileName) : null;
                    if (path == null || !string.Equals(Path.GetFullPath(path).TrimEnd('\\', '/'), full, StringComparison.OrdinalIgnoreCase)) continue;
                    p.Kill();
                    p.WaitForExit(5000);
                    stopped++;
                    Debug.Log("[GolfSimZA] Stopped a running R10 bridge (" + p.Id + ") so it can be updated.");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[GolfSimZA] Could not stop R10 bridge process: " + ex.Message);
                }
                finally
                {
                    p.Dispose();
                }
            }
            return stopped;
        }

        // ------------------------------------------------------------ Publish

        public static bool Publish(string releaseNotes, string folder, out string error)
        {
            string buildDir = Path.GetFullPath(BuildFolder);
            string notesFile = Path.Combine(Path.GetTempPath(), "GolfSimZA-release-notes.txt");
            File.WriteAllText(notesFile, releaseNotes ?? "", new UTF8Encoding(false));
            return RunPowerShell("Publish-Update.ps1",
                $"-BuildDir \"{buildDir}\" -Version \"{GolfSimVersion.Version}\" -UpdateFolder \"{folder}\" -NotesFile \"{notesFile}\"",
                "Creating the installer and publishing the update...", out error);
        }

        /// <summary>Builds R10Bridge/publish/gspro-r10.exe from the pinned source (installs the .NET SDK if needed).</summary>
        public static bool BuildBridge(out string error)
        {
            if (!File.Exists(Path.Combine(ProjectRoot, "R10Bridge", "_upstream", "gspro-r10.csproj")))
            {
                error = "bridge source R10Bridge/_upstream is missing";
                return false;
            }
            return RunPowerShell("Build-R10Bridge.ps1", "", "Building the R10 Bluetooth bridge (first time can take a few minutes)...", out error)
                   && File.Exists(Path.Combine(BridgePublishFolder, BridgeExe));
        }

        [MenuItem("GolfSimZA/Release/Build R10 Bridge", priority = 21)]
        public static void BuildBridgeMenu()
        {
            if (BuildBridge(out string error)) EditorUtility.DisplayDialog("GolfSimZA", "R10 bridge built:\n" + BridgePublishFolder, "OK");
            else EditorUtility.DisplayDialog("R10 bridge build failed", error + "\n\nSee Logs/GolfSimZA-bridge.log", "OK");
        }

        [MenuItem("GolfSimZA/Release/Build + Publish Now (current version)", priority = 10)]
        public static void BuildAndPublishMenu()
        {
            string latest = LatestPublishedVersion(UpdateFolder);
            if (!string.IsNullOrEmpty(latest) && CompareVersions(GolfSimVersion.Version, latest) <= 0)
            {
                EditorUtility.DisplayDialog("GolfSimZA", $"Version {GolfSimVersion.Version} is not newer than the published {latest}. Bump the version in the Release Manager first.", "OK");
                return;
            }
            bool ok = BuildAndPublish(true, "GolfSimZA " + GolfSimVersion.Version, UpdateFolder, out string error);
            string message = ok ? $"GolfSimZA {GolfSimVersion.Version} published.\n\nInstaller:\n{InstallerPath(UpdateFolder)}" : error;
            Debug.Log("[GolfSimZA] Release: " + message);
            EditorUtility.DisplayDialog(ok ? "GolfSimZA published" : "GolfSimZA publish failed", message, "OK");
        }

        [MenuItem("GolfSimZA/Release/Install Published Version On This PC", priority = 30)]
        public static void InstallOnThisPc()
        {
            string installer = InstallerPath(UpdateFolder);
            if (!File.Exists(installer))
            {
                EditorUtility.DisplayDialog("GolfSimZA", "No installer found for " + GolfSimVersion.Version + ":\n" + installer, "OK");
                return;
            }
            Process.Start(new ProcessStartInfo { FileName = installer, Arguments = "/SILENT", UseShellExecute = true });
        }

        public static string InstallerPath(string folder)
        {
            return Path.Combine(folder ?? AppSettings.DefaultUpdateFolder, "installers", "GolfSimZA-Setup-" + GolfSimVersion.Version + ".exe");
        }

        private static bool RunPowerShell(string scriptName, string arguments, string progressText, out string error)
        {
            error = null;
            string script = Path.Combine(ProjectRoot, "Installer", scriptName);
            if (!File.Exists(script))
            {
                error = "Installer/" + scriptName + " not found.";
                return false;
            }

            var start = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" {arguments}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = ProjectRoot
            };

            try
            {
                if (!Application.isBatchMode) EditorUtility.DisplayProgressBar("GolfSimZA", progressText, 0.5f);
                using (Process process = Process.Start(start))
                {
                    var output = new StringBuilder();
                    var errors = new StringBuilder();
                    process.OutputDataReceived += (s, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
                    process.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (errors) errors.AppendLine(e.Data); };
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                    if (output.Length > 0) Debug.Log($"[GolfSimZA] {scriptName}:\n{output}");
                    if (process.ExitCode != 0)
                    {
                        error = $"{scriptName} failed:\n" + (errors.Length > 0 ? errors.ToString() : output.ToString());
                        Debug.LogError("[GolfSimZA] " + error);
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = "Could not run PowerShell: " + ex.Message;
                return false;
            }
            finally
            {
                if (!Application.isBatchMode) EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>Entry point for Installer/Build-And-Publish.ps1 (Unity batch mode).</summary>
        public static void BuildAndPublishFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            string releaseNotes = "";
            string folder = UpdateFolder;
            bool publish = true;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-notes" && i + 1 < args.Length) releaseNotes = args[i + 1];
                if (args[i] == "-updateFolder" && i + 1 < args.Length) folder = args[i + 1];
                if (args[i] == "-buildOnly") publish = false;
            }

            bool ok = publish ? BuildAndPublish(false, releaseNotes, folder, out string error) : BuildPlayer(false, out error);
            if (!ok)
            {
                Debug.LogError("[GolfSimZA] " + error);
                EditorApplication.Exit(1);
                return;
            }
            EditorApplication.Exit(0);
        }
    }
}
#endif
