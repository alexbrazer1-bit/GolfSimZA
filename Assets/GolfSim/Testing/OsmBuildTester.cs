#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Text;
using GolfSimZA.Courses;
using GolfSimZA.Courses.Osm;
using UnityEngine;

namespace GolfSimZA.Testing
{
    /// <summary>
    /// Editor test (GolfSimZA → Test → OpenStreetMap ...): downloads the South African course list,
    /// builds the named courses from OpenStreetMap and writes osm-build-report.txt next to
    /// settings.json. Play mode stops when done; the menu then starts an automatic round.
    /// </summary>
    public sealed class OsmBuildTester : MonoBehaviour
    {
        public const string QueueKey = "GolfSimZA.OsmBuildTest";
        public const string ResultKey = "GolfSimZA.OsmBuildTestResult";
        public static string ReportPath => Path.Combine(Application.persistentDataPath, "osm-build-report.txt");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string queue = PlayerPrefs.GetString(QueueKey, "");
            if (string.IsNullOrEmpty(queue)) return;
            PlayerPrefs.DeleteKey(QueueKey);
            PlayerPrefs.Save();
            var go = new GameObject("GolfSimZA_OsmBuildTester");
            DontDestroyOnLoad(go);
            go.AddComponent<OsmBuildTester>().StartCoroutine(Run(queue));
        }

        private static IEnumerator Run(string queue)
        {
            var report = new StringBuilder();
            report.AppendLine("GolfSimZA OpenStreetMap build test  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            string firstBuilt = "";
            float started = Time.realtimeSinceStartup;

            OsmCatalogFile catalog = null;
            var listResult = new NetResult();
            yield return OsmCatalog.Download(OsmCatalog.SouthAfrica, false, s => Debug.Log("[GolfSimZA] OSM test: " + s), f => catalog = f, listResult);
            if (catalog == null)
            {
                report.AppendLine("COURSE LIST FAILED: " + listResult.Error);
                Finish(report, "");
                yield break;
            }
            int mapped9 = 0;
            foreach (OsmCatalogEntry e in catalog.courses) if (e.holesMapped >= 9) mapped9++;
            report.AppendLine($"Course list: {catalog.courses.Count} South African courses, {mapped9} with 9+ holes mapped ({Time.realtimeSinceStartup - started:0} s)");

            foreach (string name in queue.Split(';'))
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                OsmCatalogEntry entry = null;
                foreach (OsmCatalogEntry e in catalog.courses)
                    if (e.name.IndexOf(name.Trim(), StringComparison.OrdinalIgnoreCase) >= 0 && (entry == null || e.holesMapped > entry.holesMapped)) entry = e;
                if (entry == null)
                {
                    report.AppendLine(name + ": NOT IN THE LIST");
                    continue;
                }
                float t0 = Time.realtimeSinceStartup;
                var state = new OsmBuildState();
                yield return OsmCourseBuilder.Build(entry, "South Africa", state);
                if (state.Error != null)
                {
                    report.AppendLine($"{entry.name} ({entry.osmType} {entry.osmId}, {entry.holesMapped} holes mapped): FAILED - {state.Error}");
                    continue;
                }
                report.AppendLine($"{entry.name} ({entry.osmType} {entry.osmId}): built in {Time.realtimeSinceStartup - t0:0} s - {state.Report}");
                foreach (CourseDefinition c in state.Built)
                {
                    report.AppendLine("  " + c.id + "  " + c.name + "  holes " + c.HoleCount + "  par " + c.TotalPar + "  tees " + string.Join("/", c.tees) +
                                      "  length " + c.TotalLengthMeters(c.tees[0]).ToString("0") + " m");
                    foreach (HoleDefinition h in c.holes)
                        report.AppendLine($"    hole {h.number}: par {h.par}, {h.LengthMeters(c.tees[0]):0} m, tees {h.tees.Length}, aim points {h.aimPoints.Length}, green at {h.greenCenter}");
                    if (firstBuilt.Length == 0) firstBuilt = c.name;
                }
            }
            Finish(report, firstBuilt);
        }

        private static void Finish(StringBuilder report, string firstBuilt)
        {
            File.WriteAllText(ReportPath, report.ToString());
            Debug.Log("[GolfSimZA] OpenStreetMap build test done:\n" + report);
            PlayerPrefs.SetString(ResultKey, firstBuilt);
            PlayerPrefs.Save();
            UnityEditor.EditorApplication.isPlaying = false;
        }
    }
}
#endif
