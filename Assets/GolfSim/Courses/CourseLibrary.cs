using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GolfSimZA.Courses
{
    /// <summary>
    /// Imported courses on this PC. Each course is a folder under
    /// persistentDataPath/Courses/&lt;id&gt; with course.json and a preview image.
    /// The large course bundle stays where it was imported from.
    /// This folder is never touched by updates or reinstalls.
    /// </summary>
    public static class CourseLibrary
    {
        private const string DefinitionFile = "course.json";
        private static List<CourseDefinition> cache;

        public static string RootFolder => Path.Combine(Application.persistentDataPath, "Courses");

        public static event Action Changed;

        public static IReadOnlyList<CourseDefinition> All
        {
            get
            {
                if (cache == null) Reload();
                return cache;
            }
        }

        public static void Reload()
        {
            cache = new List<CourseDefinition>();
            try
            {
                if (!Directory.Exists(RootFolder)) return;
                foreach (string folder in Directory.GetDirectories(RootFolder))
                {
                    string path = Path.Combine(folder, DefinitionFile);
                    if (!File.Exists(path)) continue;
                    try
                    {
                        CourseDefinition course = JsonUtility.FromJson<CourseDefinition>(File.ReadAllText(path));
                        if (course != null && !string.IsNullOrEmpty(course.id) && course.HoleCount > 0)
                            cache.Add(course);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[GolfSimZA] Skipped damaged course file " + path + ": " + ex.Message);
                    }
                }
                cache.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                Debug.LogError("[GolfSimZA] Could not read the course library: " + ex.Message);
            }
        }

        public static CourseDefinition Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (CourseDefinition course in All)
                if (string.Equals(course.id, id, StringComparison.OrdinalIgnoreCase)) return course;
            return null;
        }

        public static bool Contains(string id) => Get(id) != null;

        public static string FolderFor(string id) => Path.Combine(RootFolder, SafeId(id));

        public static bool IsAvailable(CourseDefinition course)
        {
            return course != null && !string.IsNullOrEmpty(course.bundleFile) && File.Exists(course.bundleFile);
        }

        public static void Save(CourseDefinition course)
        {
            string folder = FolderFor(course.id);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, DefinitionFile);
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(course, true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
            Reload();
            Changed?.Invoke();
        }

        /// <summary>Removes the course from GolfSimZA only. The original course files are not deleted.</summary>
        public static void Remove(string id)
        {
            string folder = FolderFor(id);
            try
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
            catch (Exception ex)
            {
                Debug.LogError("[GolfSimZA] Could not remove course " + id + ": " + ex.Message);
            }
            Reload();
            Changed?.Invoke();
        }

        public static Texture2D LoadSplash(CourseDefinition course)
        {
            if (course == null || string.IsNullOrEmpty(course.splashFile)) return null;
            string path = Path.Combine(FolderFor(course.id), course.splashFile);
            if (!File.Exists(path)) return null;
            try
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (texture.LoadImage(File.ReadAllBytes(path))) return texture;
                UnityEngine.Object.Destroy(texture);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GolfSimZA] Could not load course preview: " + ex.Message);
            }
            return null;
        }

        public static string SafeId(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "course";
            char[] chars = raw.Trim().ToLowerInvariant().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-') chars[i] = '_';
            return new string(chars);
        }
    }
}
