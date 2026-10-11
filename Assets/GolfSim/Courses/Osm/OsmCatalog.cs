using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GolfSimZA.Courses.Osm
{
    /// <summary>One golf course on OpenStreetMap that GolfSimZA can build.</summary>
    [Serializable]
    public sealed class OsmCatalogEntry
    {
        public string name = "";
        public string osmType = "";   // way / relation
        public long osmId;
        public double lat, lon;
        public int holesMapped;       // golf=hole lines found near the course
        public string place = "";     // town / province when tagged

        public string Key => osmType + "/" + osmId;
    }

    [Serializable]
    public sealed class OsmCatalogFile
    {
        public string country = "";
        public string builtUtc = "";
        public List<OsmCatalogEntry> courses = new List<OsmCatalogEntry>();
    }

    /// <summary>
    /// List of a country's golf courses on OpenStreetMap with how many holes are mapped for each
    /// (South Africa by default). Downloaded once and kept for a week.
    /// </summary>
    public static class OsmCatalog
    {
        public const string SouthAfrica = "ZA";
        private const double MaxAgeHours = 24 * 7;

        public static string FilePath(string country) => Path.Combine(OsmNet.CacheFolder, "catalog-" + country + ".json");

        public static OsmCatalogFile LoadSaved(string country)
        {
            string path = FilePath(country);
            if (!File.Exists(path)) return null;
            try { return JsonUtility.FromJson<OsmCatalogFile>(File.ReadAllText(path)); }
            catch (Exception ex)
            {
                Debug.LogWarning("[GolfSimZA] Course list file damaged: " + ex.Message);
                return null;
            }
        }

        public static bool IsFresh(OsmCatalogFile file)
        {
            if (file == null || !DateTime.TryParse(file.builtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime built)) return false;
            return (DateTime.UtcNow - built).TotalHours < MaxAgeHours;
        }

        /// <summary>Downloads the course list. result.Error is set on failure; done receives the list.</summary>
        public static IEnumerator Download(string country, bool force, Action<string> status, Action<OsmCatalogFile> done, NetResult result)
        {
            OsmCatalogFile saved = LoadSaved(country);
            if (!force && IsFresh(saved))
            {
                done?.Invoke(saved);
                yield break;
            }

            string query =
                "[out:json][timeout:240];\n" +
                $"area[\"ISO3166-1\"=\"{country}\"][admin_level=2]->.c;\n" +
                "(way[\"leisure\"=\"golf_course\"](area.c);relation[\"leisure\"=\"golf_course\"](area.c););\n" +
                "out tags center;\n" +
                "way[\"golf\"=\"hole\"](area.c);\n" +
                "out tags center;";
            yield return OsmNet.Overpass(query, force ? 0 : MaxAgeHours, result, status);
            if (!result.Ok)
            {
                if (saved != null) { result.Error = null; done?.Invoke(saved); }
                yield break;
            }

            status?.Invoke("Reading the course list...");
            yield return null;
            OsmCatalogFile file;
            try
            {
                file = Parse(result.Text, country);
            }
            catch (Exception ex)
            {
                result.Error = "The course list could not be read: " + ex.Message;
                yield break;
            }
            try
            {
                Directory.CreateDirectory(OsmNet.CacheFolder);
                File.WriteAllText(FilePath(country), JsonUtility.ToJson(file));
            }
            catch (Exception ex) { Debug.LogWarning("[GolfSimZA] Could not save the course list: " + ex.Message); }
            done?.Invoke(file);
        }

        private static OsmCatalogFile Parse(string json, string country)
        {
            List<OsmElement> elements = OsmReader.Read(json);
            var file = new OsmCatalogFile { country = country, builtUtc = DateTime.UtcNow.ToString("o") };
            var holes = new List<OsmElement>();
            foreach (OsmElement e in elements)
            {
                if (e.Is("golf", "hole")) { holes.Add(e); continue; }
                if (!e.Is("leisure", "golf_course") || !e.HasCenter) continue;
                string name = e.Tag("name") ?? e.Tag("name:en") ?? e.Tag("official_name");
                if (string.IsNullOrWhiteSpace(name)) name = "Golf course (" + e.Type + " " + e.Id + ")";
                string place = e.Tag("addr:city") ?? e.Tag("addr:suburb") ?? e.Tag("addr:province") ?? e.Tag("addr:state") ?? "";
                file.courses.Add(new OsmCatalogEntry
                {
                    name = name.Trim(),
                    osmType = e.Type,
                    osmId = e.Id,
                    lat = e.CenterLat,
                    lon = e.CenterLon,
                    place = place
                });
            }

            // Each mapped hole counts for the nearest course (within 3 km).
            foreach (OsmElement h in holes)
            {
                if (!h.HasCenter) continue;
                OsmCatalogEntry best = null;
                double bestD = 3000;
                foreach (OsmCatalogEntry c in file.courses)
                {
                    double d = DistanceMetres(h.CenterLat, h.CenterLon, c.lat, c.lon);
                    if (d < bestD) { bestD = d; best = c; }
                }
                if (best != null) best.holesMapped++;
            }
            file.courses.Sort((a, b) =>
            {
                int byHoles = b.holesMapped.CompareTo(a.holesMapped);
                return byHoles != 0 ? byHoles : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
            });
            return file;
        }

        public static double DistanceMetres(double lat1, double lon1, double lat2, double lon2)
        {
            double dLat = (lat2 - lat1) * 110574.0;
            double dLon = (lon2 - lon1) * 111320.0 * Math.Cos((lat1 + lat2) * 0.5 * Math.PI / 180.0);
            return Math.Sqrt(dLat * dLat + dLon * dLon);
        }
    }
}
