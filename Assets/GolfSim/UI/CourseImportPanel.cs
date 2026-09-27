using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GolfSimZA.Core;
using GolfSimZA.Courses;
using UnityEngine;

namespace GolfSimZA.UI
{
    /// <summary>"Import Courses" screen, drawn by CourseSelectionUI.</summary>
    public sealed class CourseImportPanel
    {
        private string folder;
        private Task<List<CourseCandidate>> scanTask;
        private List<CourseCandidate> results = new List<CourseCandidate>();
        private Vector2 scroll, libraryScroll;
        private string message = "";
        private bool messageIsError;
        private CourseDefinition confirmRemove;

        public bool IsOpen { get; private set; }

        public void Open()
        {
            IsOpen = true;
            if (string.IsNullOrWhiteSpace(folder))
                folder = string.IsNullOrWhiteSpace(AppSettings.Current.lastCourseImportFolder) ? @"C:\Shared\GolfSimZA-Courses" : AppSettings.Current.lastCourseImportFolder;
            CourseLibrary.Reload();
        }

        public void Close() => IsOpen = false;

        public void Draw(Rect area)
        {
            GolfSimTheme.Ensure();
            // Only change what is drawn during Layout so Layout and Repaint agree.
            if (Event.current.type == EventType.Layout) PollScan();

            GUI.Box(area, GUIContent.none, GolfSimTheme.Overlay);
            GUILayout.BeginArea(new Rect(area.x + 24f, area.y + 18f, area.width - 48f, area.height - 36f));

            GUILayout.BeginHorizontal();
            GUILayout.Label("IMPORT COURSES", GolfSimTheme.Title);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("CLOSE", GolfSimTheme.Button, GUILayout.Width(120))) Close();
            GUILayout.EndHorizontal();

            GUILayout.Label("Point GolfSimZA at a folder that holds course folders (each with a .unity3d course file and a .GKD hole file). " +
                            "Encrypted .gspcrse courses are listed but cannot be imported. Course files stay where they are - GolfSimZA only stores the hole data and a preview.",
                            GolfSimTheme.Subtitle);
            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            GUILayout.Label("FOLDER", GolfSimTheme.Label, GUILayout.Width(70));
            folder = GUILayout.TextField(folder ?? "", GolfSimTheme.TextField);
            GUI.enabled = scanTask == null;
            if (GUILayout.Button(scanTask == null ? "SCAN" : "SCANNING...", GolfSimTheme.AccentButton, GUILayout.Width(140))) StartScan();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(message))
                GUILayout.Label(message, messageIsError ? GolfSimTheme.Bad_ : GolfSimTheme.Good_);

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();

            // ---- scan results
            GUILayout.BeginVertical(GUILayout.Width((area.width - 60f) * 0.58f));
            GUILayout.BeginHorizontal();
            int ready = CountImportable();
            GUILayout.Label($"FOUND  {results.Count}   •   IMPORTABLE  {ready}", GolfSimTheme.Label);
            GUILayout.FlexibleSpace();
            GUI.enabled = ready > 0 && scanTask == null;
            if (GUILayout.Button("IMPORT ALL", GolfSimTheme.SmallButton, GUILayout.Width(130))) ImportAll();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            scroll = GUILayout.BeginScrollView(scroll);
            foreach (CourseCandidate candidate in results)
            {
                GUILayout.BeginHorizontal(GolfSimTheme.Card);
                GUILayout.BeginVertical();
                GUILayout.Label(candidate.DisplayName, GolfSimTheme.Heading);
                GUILayout.Label(StatusText(candidate), StatusStyle(candidate));
                if (!string.IsNullOrEmpty(candidate.Message)) GUILayout.Label(candidate.Message, GolfSimTheme.Subtitle);
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                GUI.enabled = candidate.CanImport;
                if (GUILayout.Button(candidate.Status == CourseCandidateStatus.AlreadyImported ? "RE-IMPORT" : "IMPORT", GolfSimTheme.SmallButton, GUILayout.Width(120)))
                    ImportOne(candidate);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.Space(16);

            // ---- library
            GUILayout.BeginVertical();
            GUILayout.Label($"YOUR COURSES  ({CourseLibrary.All.Count})", GolfSimTheme.Label);
            libraryScroll = GUILayout.BeginScrollView(libraryScroll);
            foreach (CourseDefinition course in CourseLibrary.All)
            {
                GUILayout.BeginVertical(GolfSimTheme.Card);
                GUILayout.Label(course.name, GolfSimTheme.Heading);
                GUILayout.Label($"{course.HoleCount} holes  •  par {course.TotalPar}  •  tees: {string.Join(", ", course.tees)}", GolfSimTheme.Subtitle);
                bool available = CourseLibrary.IsAvailable(course);
                GUILayout.Label(available ? "Ready to play" : "Course file missing: " + course.bundleFile, available ? GolfSimTheme.Good_ : GolfSimTheme.Warning_);
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (confirmRemove == course)
                {
                    GUILayout.Label("Remove from GolfSimZA?", GolfSimTheme.Warning_);
                    if (GUILayout.Button("YES", GolfSimTheme.SmallButton, GUILayout.Width(70)))
                    {
                        CourseLibrary.Remove(course.id);
                        confirmRemove = null;
                        RefreshStatuses();
                        GUILayout.EndHorizontal();
                        GUILayout.EndVertical();
                        break;
                    }
                    if (GUILayout.Button("NO", GolfSimTheme.SmallButton, GUILayout.Width(70))) confirmRemove = null;
                }
                else if (GUILayout.Button("REMOVE", GolfSimTheme.SmallButton, GUILayout.Width(100)))
                {
                    confirmRemove = course;
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void StartScan()
        {
            string path = (folder ?? "").Trim().Trim('"');
            if (string.IsNullOrEmpty(path) || !System.IO.Directory.Exists(path))
            {
                SetMessage("Folder not found: " + path, true);
                return;
            }

            AppSettings.Current.lastCourseImportFolder = path;
            AppSettings.Current.Save();
            SetMessage("Scanning...", false);
            results = new List<CourseCandidate>();
            scanTask = Task.Run(() => CourseImporter.Scan(path));
        }

        private void PollScan()
        {
            if (scanTask == null || !scanTask.IsCompleted) return;
            if (scanTask.IsFaulted)
            {
                SetMessage("Scan failed: " + (scanTask.Exception != null ? scanTask.Exception.GetBaseException().Message : "unknown error"), true);
                results = new List<CourseCandidate>();
            }
            else
            {
                results = scanTask.Result ?? new List<CourseCandidate>();
                RefreshStatuses();
                SetMessage(results.Count == 0 ? "No course folders found in that folder." : $"Found {results.Count} course folders.", results.Count == 0);
            }
            scanTask = null;
        }

        /// <summary>Library checks need the main thread, so "already imported" is applied here.</summary>
        private void RefreshStatuses()
        {
            foreach (CourseCandidate c in results)
            {
                if (c.Status == CourseCandidateStatus.Ready && CourseLibrary.Contains(c.Id)) c.Status = CourseCandidateStatus.AlreadyImported;
                else if (c.Status == CourseCandidateStatus.AlreadyImported && !CourseLibrary.Contains(c.Id)) c.Status = CourseCandidateStatus.Ready;
            }
        }

        private int CountImportable()
        {
            int n = 0;
            foreach (CourseCandidate c in results) if (c.Status == CourseCandidateStatus.Ready) n++;
            return n;
        }

        private void ImportOne(CourseCandidate candidate)
        {
            CourseDefinition course = CourseImporter.Import(candidate, out string error);
            if (course == null)
            {
                SetMessage("Import failed for " + candidate.DisplayName + ": " + error, true);
                return;
            }
            candidate.Status = CourseCandidateStatus.AlreadyImported;
            SetMessage($"Imported {course.name}.", false);
        }

        private void ImportAll()
        {
            int ok = 0, failed = 0;
            foreach (CourseCandidate candidate in results)
            {
                if (candidate.Status != CourseCandidateStatus.Ready) continue;
                if (CourseImporter.Import(candidate, out _) != null)
                {
                    candidate.Status = CourseCandidateStatus.AlreadyImported;
                    ok++;
                }
                else failed++;
            }
            SetMessage($"Imported {ok} courses" + (failed > 0 ? $", {failed} failed." : "."), failed > 0);
        }

        private void SetMessage(string text, bool error)
        {
            message = text;
            messageIsError = error;
        }

        private static string StatusText(CourseCandidate c)
        {
            switch (c.Status)
            {
                case CourseCandidateStatus.Ready: return "Ready to import" + (string.IsNullOrEmpty(c.BundleUnityVersion) ? "" : "  •  built with Unity " + c.BundleUnityVersion) + "  •  " + (c.SizeBytes / 1048576) + " MB";
                case CourseCandidateStatus.AlreadyImported: return "Already imported";
                case CourseCandidateStatus.Encrypted: return "Encrypted - not supported";
                case CourseCandidateStatus.MissingCourseData: return "Hole data missing";
                case CourseCandidateStatus.MissingCourseFile: return "Course file missing";
                default: return "Unreadable";
            }
        }

        private static GUIStyle StatusStyle(CourseCandidate c)
        {
            switch (c.Status)
            {
                case CourseCandidateStatus.Ready:
                case CourseCandidateStatus.AlreadyImported: return GolfSimTheme.Good_;
                case CourseCandidateStatus.Encrypted: return GolfSimTheme.Bad_;
                default: return GolfSimTheme.Warning_;
            }
        }
    }
}
