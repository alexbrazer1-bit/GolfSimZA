using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace GolfSimZA.Courses
{
    public enum CourseCandidateStatus
    {
        Ready,
        AlreadyImported,
        Encrypted,
        MissingCourseData,
        MissingCourseFile,
        Unreadable
    }

    public sealed class CourseCandidate
    {
        public string Folder;
        public string Id;
        public string DisplayName;
        public string BundlePath;
        public string GkdPath;
        public string CsvPath;
        public string DetailsPath;
        public string SplashPath;
        public string BundleUnityVersion;
        public long SizeBytes;
        public CourseCandidateStatus Status;
        public string Message;

        public bool CanImport => Status == CourseCandidateStatus.Ready || Status == CourseCandidateStatus.AlreadyImported;
    }

    /// <summary>
    /// Finds course folders and imports them into the GolfSimZA course library.
    ///
    /// A course is importable when its folder has a standard (unencrypted) Unity
    /// course file (.unity3d, starts with "UnityFS") plus the readable hole data (.GKD).
    /// Encrypted course packages (.gspcrse) are detected and reported, never decoded.
    /// For personal use on your own simulator only.
    /// </summary>
    public static class CourseImporter
    {
        private static readonly byte[] UnityFsSignature = Encoding.ASCII.GetBytes("UnityFS");

        /// <summary>
        /// Scans a folder. It can be a single course folder, a folder of course folders,
        /// or a simulator install folder that contains Core/GSP/Courses.
        /// </summary>
        public static List<CourseCandidate> Scan(string root)
        {
            var results = new List<CourseCandidate>();
            if (string.IsNullOrWhiteSpace(root)) return results;
            root = root.Trim().Trim('"');
            if (!Directory.Exists(root)) return results;

            string nested = Path.Combine(root, "Core", "GSP", "Courses");
            if (Directory.Exists(nested)) root = nested;

            if (LooksLikeCourseFolder(root))
            {
                results.Add(Inspect(root));
                return results;
            }

            string[] folders;
            try
            {
                folders = Directory.GetDirectories(root);
            }
            catch (Exception ex)
            {
                Debug.LogError("[GolfSimZA] Could not list " + root + ": " + ex.Message);
                return results;
            }

            Array.Sort(folders, StringComparer.OrdinalIgnoreCase);
            foreach (string folder in folders)
            {
                if (LooksLikeCourseFolder(folder))
                    results.Add(Inspect(folder));
            }
            return results;
        }

        private static bool LooksLikeCourseFolder(string folder)
        {
            try
            {
                return Directory.GetFiles(folder, "*.gkd").Length > 0 ||
                       Directory.GetFiles(folder, "*.unity3d").Length > 0 ||
                       Directory.GetFiles(folder, "*.gspcrse").Length > 0;
            }
            catch
            {
                return false;
            }
        }

        public static CourseCandidate Inspect(string folder)
        {
            var candidate = new CourseCandidate
            {
                Folder = folder,
                Id = CourseLibrary.SafeId(Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))),
                DisplayName = Path.GetFileName(folder)
            };

            try
            {
                candidate.GkdPath = FirstFile(folder, "*.gkd");
                candidate.DetailsPath = FirstFile(folder, "coursedetails.txt");
                candidate.CsvPath = FirstFile(folder, "*.csv");
                candidate.SplashPath = FindSplash(folder);

                string bundle = FirstFile(folder, "*.unity3d");
                string encrypted = FirstFile(folder, "*.gspcrse");

                if (bundle != null)
                {
                    candidate.BundlePath = bundle;
                    candidate.SizeBytes = new FileInfo(bundle).Length;
                    if (!IsUnityBundle(bundle, out string unityVersion))
                    {
                        candidate.Status = CourseCandidateStatus.Encrypted;
                        candidate.Message = "Course file is encrypted or not a Unity course file - cannot be imported.";
                        return candidate;
                    }
                    candidate.BundleUnityVersion = unityVersion;
                }
                else if (encrypted != null)
                {
                    candidate.SizeBytes = new FileInfo(encrypted).Length;
                    candidate.Status = CourseCandidateStatus.Encrypted;
                    candidate.Message = "Encrypted course package (.gspcrse) - cannot be imported.";
                    return candidate;
                }
                else
                {
                    candidate.Status = CourseCandidateStatus.MissingCourseFile;
                    candidate.Message = "No course file (.unity3d) in this folder.";
                    return candidate;
                }

                if (candidate.GkdPath == null)
                {
                    candidate.Status = CourseCandidateStatus.MissingCourseData;
                    candidate.Message = "No hole data (.GKD) - tees and pins are unknown.";
                    return candidate;
                }

                // Quick read of the course name for the list.
                var preview = new CourseDefinition();
                if (GkdCourseReader.TryRead(candidate.GkdPath, candidate.CsvPath, candidate.DetailsPath, preview, out string error))
                {
                    candidate.DisplayName = preview.name;
                    candidate.Message = $"{preview.HoleCount} holes  •  par {preview.TotalPar}  •  {preview.location}";
                }
                else
                {
                    candidate.Status = CourseCandidateStatus.MissingCourseData;
                    candidate.Message = error;
                    return candidate;
                }

                // "Already imported" is decided on the main thread by the caller (Scan may run on a worker thread).
                candidate.Status = CourseCandidateStatus.Ready;
            }
            catch (Exception ex)
            {
                candidate.Status = CourseCandidateStatus.Unreadable;
                candidate.Message = ex.Message;
            }
            return candidate;
        }

        /// <summary>Imports (or re-imports) a course. Returns null and an error message on failure.</summary>
        public static CourseDefinition Import(CourseCandidate candidate, out string error)
        {
            error = null;
            if (candidate == null || !candidate.CanImport)
            {
                error = candidate != null ? candidate.Message : "Nothing selected.";
                return null;
            }

            var course = new CourseDefinition
            {
                id = candidate.Id,
                sourceType = CourseDefinition.SourceUnityBundle,
                sourceFolder = candidate.Folder,
                bundleFile = candidate.BundlePath,
                bundleUnityVersion = candidate.BundleUnityVersion ?? "",
                bundleSizeBytes = candidate.SizeBytes,
                importedAtUtc = DateTime.UtcNow.ToString("o")
            };

            if (!GkdCourseReader.TryRead(candidate.GkdPath, candidate.CsvPath, candidate.DetailsPath, course, out error))
                return null;

            try
            {
                string folder = CourseLibrary.FolderFor(course.id);
                Directory.CreateDirectory(folder);
                if (!string.IsNullOrEmpty(candidate.SplashPath) && File.Exists(candidate.SplashPath))
                {
                    string splashName = "preview" + Path.GetExtension(candidate.SplashPath).ToLowerInvariant();
                    File.Copy(candidate.SplashPath, Path.Combine(folder, splashName), true);
                    course.splashFile = splashName;
                }
                CourseLibrary.Save(course);
            }
            catch (Exception ex)
            {
                error = "Could not save the course: " + ex.Message;
                return null;
            }

            Debug.Log($"[GolfSimZA] Imported course '{course.name}' ({course.HoleCount} holes, tees: {string.Join(", ", course.tees)}).");
            return course;
        }

        /// <summary>Checks the "UnityFS" signature and reads the Unity version the course was built with.</summary>
        public static bool IsUnityBundle(string path, out string unityVersion)
        {
            unityVersion = "";
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var header = new byte[128];
                    int read = stream.Read(header, 0, header.Length);
                    if (read < UnityFsSignature.Length + 1) return false;
                    for (int i = 0; i < UnityFsSignature.Length; i++)
                        if (header[i] != UnityFsSignature[i]) return false;

                    // UnityFS\0 + uint32 format + "5.x.x\0" + "2018.2.8f1\0"
                    int position = UnityFsSignature.Length + 1 + 4;
                    SkipString(header, read, ref position);
                    unityVersion = ReadString(header, read, ref position);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GolfSimZA] Could not read course file header: " + ex.Message);
                return false;
            }
        }

        private static void SkipString(byte[] data, int length, ref int position)
        {
            while (position < length && data[position] != 0) position++;
            position++;
        }

        private static string ReadString(byte[] data, int length, ref int position)
        {
            int start = position;
            while (position < length && data[position] != 0) position++;
            string value = position > start ? Encoding.ASCII.GetString(data, start, position - start) : "";
            position++;
            return value;
        }

        private static string FirstFile(string folder, string pattern)
        {
            string[] files = Directory.GetFiles(folder, pattern);
            if (files.Length == 0) return null;
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            // Prefer the real file over backups such as *.GKD_BAK (GetFiles can match those on Windows).
            foreach (string f in files)
                if (string.Equals(Path.GetExtension(f), pattern.Replace("*", ""), StringComparison.OrdinalIgnoreCase)) return f;
            return files[0];
        }

        private static string FindSplash(string folder)
        {
            string splash = null, altered = null, other = null;
            foreach (string file in Directory.GetFiles(folder))
            {
                string name = Path.GetFileName(file).ToLowerInvariant();
                string ext = Path.GetExtension(name);
                if (ext != ".jpg" && ext != ".jpeg" && ext != ".png") continue;
                if (name.Contains("scorecard") || name.Contains("benchmark")) continue;
                if (name.StartsWith("splash")) splash = splash ?? file;
                else if (name.StartsWith("image_altered")) altered = altered ?? file;
                else other = other ?? file;
            }
            return splash ?? altered ?? other;
        }
    }
}
