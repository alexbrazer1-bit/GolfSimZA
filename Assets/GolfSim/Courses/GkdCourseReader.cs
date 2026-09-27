using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace GolfSimZA.Courses
{
    /// <summary>
    /// Reads the plain-text hole data that ships next to a course (GKD JSON, scorecard CSV
    /// and coursedetails.txt) and turns it into a GolfSimZA CourseDefinition.
    /// Only readable data files are used; nothing encrypted or proprietary is decoded.
    /// </summary>
    public static class GkdCourseReader
    {
        // ---- JSON shape of the GKD file (only the fields GolfSimZA uses) ----
#pragma warning disable 0649
        [Serializable] private sealed class GkdVector { public float x, y, z; }
        [Serializable] private sealed class GkdTee { public string TeeType; public bool Enabled; public float Distance; public GkdVector Position; }
        [Serializable] private sealed class GkdPin { public string Day; public GkdVector Position; }
        [Serializable] private sealed class GkdHole { public bool Enabled; public int HoleNumber; public int Par; public int Index; public GkdTee[] Tees; public GkdPin[] Pins; }
        [Serializable] private sealed class GkdPolygon { public int pointCount; public GkdVector[] coords; }
        [Serializable] private sealed class GkdHazard { public int pointCount; public GkdVector[] coords; public GkdVector DZpos; public bool freeDrop; public bool innerOOB; public bool hasDZ; public bool islandGreen; }
        [Serializable]
        private sealed class GkdFile
        {
            public float gkversion;
            public string CourseName, Location, CityTown, StateCountyProvince, Country, Designer, CourseInfo, SceneFolderName, CourseImageFileName;
            public float altitude, altitudeV2;
            public int unitOfMeasure;
            public GkdPolygon pOOB;
            public GkdHole[] Holes;
            public GkdHazard[] Hazards;
        }
#pragma warning restore 0649

        /// <summary>Tee boxes a golfer can play from, longest first (GolfSimZA order).</summary>
        public static readonly string[] PlayableTeeOrder = { "Black", "Blue", "White", "Yellow", "Green", "Red", "Junior", "Par3" };

        public static bool TryRead(string gkdPath, string csvPath, string detailsPath, CourseDefinition target, out string error)
        {
            error = null;
            GkdFile file;
            try
            {
                string json = File.ReadAllText(gkdPath);
                file = JsonUtility.FromJson<GkdFile>(json);
            }
            catch (Exception ex)
            {
                error = "Course data file could not be read: " + ex.Message;
                return false;
            }

            if (file == null || file.Holes == null || file.Holes.Length == 0)
            {
                error = "Course data file has no holes.";
                return false;
            }

            target.name = FirstNonEmpty(file.CourseName, file.SceneFolderName, Path.GetFileNameWithoutExtension(gkdPath));
            target.location = FirstNonEmpty(file.Location, JoinNonEmpty(", ", file.CityTown, file.StateCountyProvince, file.Country));
            target.country = file.Country ?? "";
            target.designer = file.Designer ?? "";
            target.description = (file.CourseInfo ?? "").Trim();
            target.altitudeMeters = ReadAltitudeMeters(csvPath, file);

            var holes = new List<HoleDefinition>();
            var teeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (GkdHole gkdHole in file.Holes)
            {
                if (gkdHole == null || !gkdHole.Enabled) continue;

                var hole = new HoleDefinition
                {
                    number = gkdHole.HoleNumber > 0 ? gkdHole.HoleNumber : holes.Count + 1,
                    par = Mathf.Clamp(gkdHole.Par, 3, 6),
                    handicapIndex = gkdHole.Index
                };

                var tees = new List<TeeDefinition>();
                var aims = new SortedDictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
                if (gkdHole.Tees != null)
                {
                    foreach (GkdTee tee in gkdHole.Tees)
                    {
                        if (tee == null || string.IsNullOrEmpty(tee.TeeType) || !IsSet(tee.Position)) continue;
                        Vector3 position = ToVector(tee.Position);

                        if (tee.TeeType.Equals("GreenCenterPoint", StringComparison.OrdinalIgnoreCase))
                        {
                            hole.hasGreenCenter = true;
                            hole.greenCenter = position;
                        }
                        else if (tee.TeeType.StartsWith("AimPoint", StringComparison.OrdinalIgnoreCase))
                        {
                            if (tee.Enabled) aims[tee.TeeType] = position;
                        }
                        else if (tee.Enabled)
                        {
                            tees.Add(new TeeDefinition { name = tee.TeeType, position = position });
                            teeNames.Add(tee.TeeType);
                        }
                    }
                }

                tees.Sort((a, b) => TeeRank(a.name).CompareTo(TeeRank(b.name)));
                hole.tees = tees.ToArray();
                hole.aimPoints = new List<Vector3>(aims.Values).ToArray();

                var pins = new List<PinDefinition>();
                if (gkdHole.Pins != null)
                    foreach (GkdPin pin in gkdHole.Pins)
                        if (pin != null && IsSet(pin.Position))
                            pins.Add(new PinDefinition { day = pin.Day ?? "", position = ToVector(pin.Position) });
                hole.pins = pins.ToArray();

                if (hole.tees.Length == 0 || (hole.pins.Length == 0 && !hole.hasGreenCenter))
                    continue; // hole cannot be played without a tee and a target

                holes.Add(hole);
            }

            if (holes.Count == 0)
            {
                error = "No playable holes (every hole is missing a tee or pin position).";
                return false;
            }

            holes.Sort((a, b) => a.number.CompareTo(b.number));
            target.holes = holes.ToArray();

            var orderedTees = new List<string>(teeNames);
            orderedTees.Sort((a, b) => TeeRank(a).CompareTo(TeeRank(b)));
            target.tees = orderedTees.ToArray();

            target.outOfBounds = new PolygonDefinition { points = ToPoints(file.pOOB != null ? file.pOOB.coords : null) };

            var hazards = new List<HazardDefinition>();
            if (file.Hazards != null)
            {
                foreach (GkdHazard hazard in file.Hazards)
                {
                    if (hazard == null) continue;
                    Vector3[] points = ToPoints(hazard.coords);
                    if (points.Length < 3) continue;
                    hazards.Add(new HazardDefinition
                    {
                        area = new PolygonDefinition { points = points },
                        hasDropZone = hazard.hasDZ && IsSet(hazard.DZpos),
                        dropZone = IsSet(hazard.DZpos) ? ToVector(hazard.DZpos) : Vector3.zero,
                        freeDrop = hazard.freeDrop,
                        innerOutOfBounds = hazard.innerOOB,
                        islandGreen = hazard.islandGreen
                    });
                }
            }
            target.hazards = hazards.ToArray();

            ApplyDetailsFallback(detailsPath, target);
            return true;
        }

        /// <summary>coursedetails.txt: name|designer|altitude|description|location|...</summary>
        public static void ApplyDetailsFallback(string detailsPath, CourseDefinition target)
        {
            if (string.IsNullOrEmpty(detailsPath) || !File.Exists(detailsPath)) return;
            try
            {
                string[] parts = File.ReadAllText(detailsPath).Split('|');
                if (parts.Length > 0 && string.IsNullOrWhiteSpace(target.name)) target.name = parts[0].Trim();
                if (parts.Length > 1 && string.IsNullOrWhiteSpace(target.designer)) target.designer = parts[1].Trim();
                if (parts.Length > 3 && string.IsNullOrWhiteSpace(target.description)) target.description = parts[3].Trim();
                if (parts.Length > 4 && string.IsNullOrWhiteSpace(target.location)) target.location = parts[4].Trim();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GolfSimZA] Could not read course details: " + ex.Message);
            }
        }

        private static float ReadAltitudeMeters(string csvPath, GkdFile file)
        {
            // The scorecard CSV states the altitude explicitly in metres.
            if (!string.IsNullOrEmpty(csvPath) && File.Exists(csvPath))
            {
                try
                {
                    foreach (string line in File.ReadAllLines(csvPath))
                    {
                        if (!line.StartsWith("Altitude", StringComparison.OrdinalIgnoreCase)) continue;
                        string[] cells = line.Split('\t', ',');
                        for (int i = 1; i < cells.Length; i++)
                            if (float.TryParse(cells[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float metres))
                                return Mathf.Clamp(metres, -400f, 5000f);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[GolfSimZA] Could not read scorecard altitude: " + ex.Message);
                }
            }

            float value = file.altitudeV2 > 0f ? file.altitudeV2 : file.altitude;
            return Mathf.Clamp(value, -400f, 5000f);
        }

        private static int TeeRank(string name)
        {
            int index = Array.FindIndex(PlayableTeeOrder, t => string.Equals(t, name, StringComparison.OrdinalIgnoreCase));
            return index < 0 ? PlayableTeeOrder.Length : index;
        }

        private static bool IsSet(GkdVector v) => v != null && (Mathf.Abs(v.x) > 0.001f || Mathf.Abs(v.z) > 0.001f);
        private static Vector3 ToVector(GkdVector v) => new Vector3(v.x, v.y, v.z);

        private static Vector3[] ToPoints(GkdVector[] coords)
        {
            var points = new List<Vector3>();
            if (coords != null)
                foreach (GkdVector c in coords)
                    if (IsSet(c)) points.Add(ToVector(c));
            return points.ToArray();
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string v in values)
                if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
            return "";
        }

        private static string JoinNonEmpty(string separator, params string[] values)
        {
            var list = new List<string>();
            foreach (string v in values)
                if (!string.IsNullOrWhiteSpace(v)) list.Add(v.Trim());
            return string.Join(separator, list);
        }
    }
}
