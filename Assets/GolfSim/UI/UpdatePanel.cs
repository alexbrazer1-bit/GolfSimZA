using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using GolfSimZA.Core;
using GolfSimZA.Updates;
using UnityEngine;

namespace GolfSimZA.UI
{
    /// <summary>"Updates &amp; Settings" screen, drawn by CourseSelectionUI.</summary>
    public sealed class UpdatePanel
    {
        private string folderField;
        private string altitudeField;
        private Task<UpdateCheckResult> checkTask;
        private Task<string> prepareTask;
        private ReleaseInfo installing;
        private UpdateCheckResult result;
        private string message = "";
        private bool messageIsError;
        private Vector2 scroll;

        public bool IsOpen { get; private set; }

        /// <summary>True when the last check found a newer version (shown as a badge on the menu).</summary>
        public bool UpdateAvailable => result != null && result.Success && result.UpdateAvailable;
        public string LatestVersion => result != null && result.Latest != null ? result.Latest.version : "";

        public void Open()
        {
            IsOpen = true;
            folderField = AppSettings.Current.updateFolder;
            altitudeField = AppSettings.Current.homeAltitudeMeters.ToString("0", CultureInfo.InvariantCulture);
            if (checkTask == null && result == null) StartCheck();
        }

        public void Close() => IsOpen = false;

        /// <summary>Quiet check when the menu opens.</summary>
        public void CheckInBackground()
        {
            if (AppSettings.Current.checkForUpdatesOnStart && checkTask == null)
                StartCheck();
        }

        /// <summary>Call every frame (during Layout) so background work is picked up.</summary>
        public void Poll()
        {
            if (checkTask != null && checkTask.IsCompleted)
            {
                result = checkTask.IsFaulted ? new UpdateCheckResult { Error = checkTask.Exception?.GetBaseException().Message } : checkTask.Result;
                checkTask = null;
                if (result.Success)
                    SetMessage(result.UpdateAvailable ? $"Version {result.Latest.version} is available." : "GolfSimZA is up to date.", false);
                else
                    SetMessage(result.Error, true);
            }

            if (prepareTask != null && prepareTask.IsCompleted)
            {
                if (prepareTask.IsFaulted)
                {
                    SetMessage("Update not installed: " + prepareTask.Exception?.GetBaseException().Message, true);
                }
                else
                {
                    SetMessage($"Installing version {installing.version}... GolfSimZA will close and restart.", false);
                    if (!UpdateService.LaunchInstallerAndQuit(prepareTask.Result, out string error))
                        SetMessage(error, true);
                }
                prepareTask = null;
                installing = null;
            }
        }

        public void Draw(Rect area)
        {
            GolfSimTheme.Ensure();
            if (Event.current.type == EventType.Layout) Poll();

            GUI.Box(area, GUIContent.none, GolfSimTheme.Overlay);
            GUILayout.BeginArea(new Rect(area.x + 24f, area.y + 18f, area.width - 48f, area.height - 36f));

            GUILayout.BeginHorizontal();
            GUILayout.Label("UPDATES & SETTINGS", GolfSimTheme.Title);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("CLOSE", GolfSimTheme.Button, GUILayout.Width(120))) Close();
            GUILayout.EndHorizontal();

            GUILayout.Label($"Installed version  {UpdateService.InstalledVersion}", GolfSimTheme.Heading);
            GUILayout.Space(6);

            // ---- update folder
            GUILayout.Label("UPDATE FOLDER", GolfSimTheme.Label);
            GUILayout.Label(@"The folder your build PC publishes updates to. On the simulator PC use the network path, e.g. \\BUILD-PC\Shared\GolfSimZA-Updates", GolfSimTheme.Subtitle);
            GUILayout.BeginHorizontal();
            folderField = GUILayout.TextField(folderField ?? "", GolfSimTheme.TextField);
            if (GUILayout.Button("SAVE", GolfSimTheme.Button, GUILayout.Width(100)))
            {
                AppSettings.Current.updateFolder = (folderField ?? "").Trim().Trim('"');
                AppSettings.Current.Save();
                result = null;
                StartCheck();
            }
            GUILayout.EndHorizontal();

            bool autoCheck = GUILayout.Toggle(AppSettings.Current.checkForUpdatesOnStart, "  Check for updates when GolfSimZA starts", GolfSimTheme.Body);
            if (autoCheck != AppSettings.Current.checkForUpdatesOnStart)
            {
                AppSettings.Current.checkForUpdatesOnStart = autoCheck;
                AppSettings.Current.Save();
            }

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUI.enabled = checkTask == null && prepareTask == null;
            if (GUILayout.Button(checkTask == null ? "CHECK FOR UPDATES" : "CHECKING...", GolfSimTheme.Button, GUILayout.Width(220))) StartCheck();
            if (UpdateAvailable && GUILayout.Button($"INSTALL {result.Latest.version} NOW", GolfSimTheme.AccentButton, GUILayout.Width(260))) Install(result.Latest);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(message))
                GUILayout.Label(message, messageIsError ? GolfSimTheme.Bad_ : GolfSimTheme.Good_);
            if (UpdateAvailable && !string.IsNullOrEmpty(result.Latest.notes))
                GUILayout.Label("What's new: " + result.Latest.notes, GolfSimTheme.Body);

            // ---- all versions (roll back)
            if (result != null && result.Success && result.Releases.Count > 0)
            {
                GUILayout.Space(8);
                GUILayout.Label("ALL VERSIONS  (install an older one to roll back)", GolfSimTheme.Label);
                scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(Mathf.Max(120f, area.height * 0.28f)));
                foreach (ReleaseInfo release in result.Releases)
                {
                    GUILayout.BeginHorizontal(GolfSimTheme.Card);
                    bool current = UpdateService.CompareVersions(release.version, UpdateService.InstalledVersion) == 0;
                    GUILayout.Label(release.version + (current ? "   (installed)" : ""), GolfSimTheme.Heading, GUILayout.Width(200));
                    GUILayout.Label(string.IsNullOrEmpty(release.notes) ? release.publishedUtc : release.notes, GolfSimTheme.Subtitle);
                    GUILayout.FlexibleSpace();
                    GUI.enabled = !current && prepareTask == null;
                    if (GUILayout.Button("INSTALL", GolfSimTheme.SmallButton, GUILayout.Width(110))) Install(release);
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
            }

            // ---- simulator settings
            GUILayout.Space(10);
            GUILayout.Label("SIMULATOR", GolfSimTheme.Label);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Home / range altitude (metres above sea level)", GolfSimTheme.Body, GUILayout.Width(340));
            altitudeField = GUILayout.TextField(altitudeField ?? "0", GolfSimTheme.TextField, GUILayout.Width(120));
            if (GUILayout.Button("SAVE", GolfSimTheme.SmallButton, GUILayout.Width(90)))
            {
                if (float.TryParse(altitudeField, NumberStyles.Float, CultureInfo.InvariantCulture, out float metres) && metres > -400f && metres < 5000f)
                {
                    AppSettings.Current.homeAltitudeMeters = metres;
                    AppSettings.Current.Save();
                    SetMessage($"Home altitude saved: {metres:0} m. Imported courses use their own altitude.", false);
                }
                else
                {
                    SetMessage("Enter the altitude as a number of metres, e.g. 1400.", true);
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Settings file: " + AppSettings.FilePath, GolfSimTheme.Subtitle);

            GUILayout.EndArea();
        }

        private void StartCheck()
        {
            if (checkTask != null) return;
            SetMessage("Checking " + AppSettings.Current.updateFolder + " ...", false);
            checkTask = UpdateService.CheckAsync(AppSettings.Current.updateFolder);
        }

        private void Install(ReleaseInfo release)
        {
            if (release == null || prepareTask != null) return;
            installing = release;
            SetMessage($"Copying and verifying version {release.version}...", false);
            string temp = Path.Combine(Application.temporaryCachePath, "Updates");
            prepareTask = UpdateService.PrepareInstallerAsync(AppSettings.Current.updateFolder, release, temp);
        }

        private void SetMessage(string text, bool error)
        {
            message = text ?? "";
            messageIsError = error;
        }
    }
}
