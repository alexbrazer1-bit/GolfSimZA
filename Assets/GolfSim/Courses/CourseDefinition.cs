using System;
using System.Collections.Generic;
using UnityEngine;

namespace GolfSimZA.Courses
{
    /// <summary>
    /// GolfSimZA's own course description (course.json in the course library).
    /// Positions are world coordinates of the course scene, in metres.
    /// </summary>
    [Serializable]
    public sealed class CourseDefinition
    {
        public const int CurrentFormatVersion = 1;
        public const string SourceUnityBundle = "UnityBundle";

        public int formatVersion = CurrentFormatVersion;
        public string id = "";
        public string name = "";
        public string location = "";
        public string country = "";
        public string designer = "";
        public string description = "";

        public string sourceType = SourceUnityBundle;
        public string sourceFolder = "";
        public string bundleFile = "";
        public string bundleUnityVersion = "";
        public long bundleSizeBytes;
        public string splashFile = "";
        public string importedAtUtc = "";

        public float altitudeMeters;
        public string[] tees = new string[0];
        public HoleDefinition[] holes = new HoleDefinition[0];
        public PolygonDefinition outOfBounds = new PolygonDefinition();
        public HazardDefinition[] hazards = new HazardDefinition[0];

        public int HoleCount => holes != null ? holes.Length : 0;

        public int TotalPar
        {
            get
            {
                int total = 0;
                if (holes != null)
                    foreach (HoleDefinition hole in holes) total += hole.par;
                return total;
            }
        }

        public HoleDefinition GetHole(int index)
        {
            if (holes == null || holes.Length == 0) return null;
            return holes[Mathf.Clamp(index, 0, holes.Length - 1)];
        }

        public float TotalLengthMeters(string teeName)
        {
            float total = 0f;
            if (holes != null)
                foreach (HoleDefinition hole in holes) total += hole.LengthMeters(teeName);
            return total;
        }
    }

    [Serializable]
    public sealed class HoleDefinition
    {
        public int number;
        public int par = 4;
        public int handicapIndex;
        public TeeDefinition[] tees = new TeeDefinition[0];
        public PinDefinition[] pins = new PinDefinition[0];
        public bool hasGreenCenter;
        public Vector3 greenCenter;
        public Vector3[] aimPoints = new Vector3[0];

        /// <summary>Finds the requested tee, falling back to the nearest available one.</summary>
        public TeeDefinition GetTee(string teeName)
        {
            if (tees == null || tees.Length == 0) return null;
            foreach (TeeDefinition tee in tees)
                if (string.Equals(tee.name, teeName, StringComparison.OrdinalIgnoreCase)) return tee;
            return tees[0];
        }

        /// <summary>Pin for the round's pin setting: Easy = Thursday, Standard = Friday, Tournament = Sunday.</summary>
        public Vector3 GetPin(string pinSetting)
        {
            string day;
            switch (pinSetting)
            {
                case "Easy": day = "Thursday"; break;
                case "Tournament": day = "Sunday"; break;
                default: day = "Friday"; break;
            }

            if (pins != null && pins.Length > 0)
            {
                foreach (PinDefinition pin in pins)
                    if (string.Equals(pin.day, day, StringComparison.OrdinalIgnoreCase)) return pin.position;
                return pins[0].position;
            }
            return hasGreenCenter ? greenCenter : Vector3.zero;
        }

        public Vector3 GreenTarget => hasGreenCenter ? greenCenter : (pins != null && pins.Length > 0 ? pins[0].position : Vector3.zero);

        /// <summary>Playing length along the aim points, in metres.</summary>
        public float LengthMeters(string teeName)
        {
            TeeDefinition tee = GetTee(teeName);
            if (tee == null) return 0f;
            List<Vector3> path = new List<Vector3> { tee.position };
            if (aimPoints != null) path.AddRange(aimPoints);
            path.Add(GreenTarget);

            float length = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 d = path[i] - path[i - 1];
                d.y = 0f;
                length += d.magnitude;
            }
            return length;
        }
    }

    [Serializable]
    public sealed class TeeDefinition
    {
        public string name;
        public Vector3 position;
    }

    [Serializable]
    public sealed class PinDefinition
    {
        public string day;
        public Vector3 position;
    }

    [Serializable]
    public sealed class PolygonDefinition
    {
        public Vector3[] points = new Vector3[0];

        public bool IsValid => points != null && points.Length >= 3;

        /// <summary>Point-in-polygon test on the ground plane (x/z).</summary>
        public bool Contains(Vector3 p)
        {
            if (!IsValid) return false;
            bool inside = false;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                Vector3 a = points[i];
                Vector3 b = points[j];
                if ((a.z > p.z) != (b.z > p.z) &&
                    p.x < (b.x - a.x) * (p.z - a.z) / (b.z - a.z + 1e-6f) + a.x)
                    inside = !inside;
            }
            return inside;
        }
    }

    [Serializable]
    public sealed class HazardDefinition
    {
        public PolygonDefinition area = new PolygonDefinition();
        public bool hasDropZone;
        public Vector3 dropZone;
        public bool freeDrop;
        public bool innerOutOfBounds;
        public bool islandGreen;
    }
}
