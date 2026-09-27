using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using GolfSimZA.Core;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GolfSimZA.Updates
{
    [Serializable]
    public sealed class ReleaseInfo
    {
        public string version = "";
        public string installer = "";
        public string sha256 = "";
        public string publishedUtc = "";
        public string notes = "";
    }

    /// <summary>latest.json in the update folder, written by Installer/Publish-Update.ps1.</summary>
    [Serializable]
    public sealed class UpdateManifest
    {
        public string product = GolfSimVersion.ProductName;
        public string latestVersion = "";
        public ReleaseInfo[] releases = new ReleaseInfo[0];
    }

    public sealed class UpdateCheckResult
    {
        public bool Success;
        public string Error;
        public string Folder;
        public ReleaseInfo Latest;
        public bool UpdateAvailable;
        public List<ReleaseInfo> Releases = new List<ReleaseInfo>();
    }

    /// <summary>
    /// Local-folder updater. Your build PC publishes installers into a shared folder
    /// (default C:\Shared\GolfSimZA-Updates, or \\PC-NAME\Shared\GolfSimZA-Updates from
    /// the simulator PC). The game checks that folder, verifies the installer's SHA-256
    /// checksum, runs it and closes; the installer upgrades in place and restarts GolfSimZA.
    /// Player bags, settings and imported courses are stored outside the install folder
    /// and are never touched by an update.
    /// </summary>
    public static class UpdateService
    {
        public const string ManifestFileName = "latest.json";

        public static string InstalledVersion => GolfSimVersion.Version;

        public static Task<UpdateCheckResult> CheckAsync(string folder)
        {
            string installed = InstalledVersion;
            return Task.Run(() => Check(folder, installed));
        }

        private static UpdateCheckResult Check(string folder, string installedVersion)
        {
            var result = new UpdateCheckResult { Folder = folder };
            try
            {
                if (string.IsNullOrWhiteSpace(folder))
                {
                    result.Error = "No update folder is set.";
                    return result;
                }
                if (!Directory.Exists(folder))
                {
                    result.Error = "Update folder not found: " + folder;
                    return result;
                }

                string manifestPath = Path.Combine(folder, ManifestFileName);
                if (!File.Exists(manifestPath))
                {
                    result.Error = "No updates have been published to this folder yet (" + ManifestFileName + " is missing).";
                    return result;
                }

                UpdateManifest manifest = JsonUtility.FromJson<UpdateManifest>(File.ReadAllText(manifestPath));
                if (manifest == null || manifest.releases == null || manifest.releases.Length == 0)
                {
                    result.Error = "The update list is empty or damaged.";
                    return result;
                }

                result.Releases.AddRange(manifest.releases);
                result.Releases.Sort((a, b) => CompareVersions(b.version, a.version));
                result.Latest = result.Releases[0];
                result.UpdateAvailable = CompareVersions(result.Latest.version, installedVersion) > 0;
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Error = "Could not read the update folder: " + ex.Message;
            }
            return result;
        }

        /// <summary>Negative when a &lt; b, zero when equal, positive when a &gt; b.</summary>
        public static int CompareVersions(string a, string b)
        {
            if (Version.TryParse(Normalize(a), out Version va) && Version.TryParse(Normalize(b), out Version vb))
                return va.CompareTo(vb);
            return string.Compare(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string v)
        {
            if (string.IsNullOrWhiteSpace(v)) return "0.0";
            v = v.Trim().TrimStart('v', 'V');
            return v.Contains(".") ? v : v + ".0";
        }

        /// <summary>Resolves the installer path and refuses anything outside the update folder.</summary>
        public static string ResolveInstallerPath(string folder, ReleaseInfo release)
        {
            if (release == null || string.IsNullOrWhiteSpace(release.installer)) return null;
            string root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(Path.Combine(folder, release.installer));
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return null;
            if (!string.Equals(Path.GetExtension(full), ".exe", StringComparison.OrdinalIgnoreCase)) return null;
            return full;
        }

        /// <summary>
        /// Verifies and copies the installer to a local temp folder. Returns the local path or throws.
        /// Runs on a background thread (network folders can be slow).
        /// </summary>
        public static Task<string> PrepareInstallerAsync(string folder, ReleaseInfo release, string tempFolder)
        {
            return Task.Run(() =>
            {
                string source = ResolveInstallerPath(folder, release);
                if (source == null)
                    throw new InvalidOperationException("The update entry points outside the update folder or is not an installer.");
                if (!File.Exists(source))
                    throw new FileNotFoundException("Installer not found: " + source);

                Directory.CreateDirectory(tempFolder);
                string local = Path.Combine(tempFolder, "GolfSimZA-Setup-" + SafeFilePart(release.version) + ".exe");
                File.Copy(source, local, true);

                if (string.IsNullOrWhiteSpace(release.sha256))
                    throw new InvalidOperationException("The update has no checksum, so it cannot be verified.");

                string actual = Sha256(local);
                if (!string.Equals(actual, release.sha256.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(local); } catch { }
                    throw new InvalidOperationException("The installer failed the checksum test (the file is incomplete or was changed). Publish the update again.");
                }
                return local;
            });
        }

        /// <summary>Starts the verified installer and closes GolfSimZA so it can be replaced.</summary>
        public static bool LaunchInstallerAndQuit(string localInstaller, out string error)
        {
            error = null;
            if (Application.isEditor)
            {
                error = "Updates are installed by the built program, not inside the Unity Editor. The installer was downloaded and verified: " + localInstaller;
                return false;
            }

            try
            {
                var start = new ProcessStartInfo
                {
                    FileName = localInstaller,
                    Arguments = "/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS",
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(localInstaller)
                };
                Process.Start(start);
                Debug.Log("[GolfSimZA] Update installer started: " + localInstaller);
                Application.Quit();
                return true;
            }
            catch (Exception ex)
            {
                error = "Could not start the installer: " + ex.Message;
                return false;
            }
        }

        public static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private static string SafeFilePart(string value)
        {
            if (string.IsNullOrEmpty(value)) return "unknown";
            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '.' && chars[i] != '-') chars[i] = '_';
            return new string(chars);
        }
    }
}
