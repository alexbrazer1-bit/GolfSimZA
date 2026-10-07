using System.Collections;
using System.Collections.Generic;
using GolfSimZA.Courses;
using GolfSimZA.Courses.Osm;
using UnityEngine;

namespace GolfSimZA.UI
{
    /// <summary>Runs downloads and course builds (coroutines) for screens that are not MonoBehaviours.</summary>
    public sealed class OsmRunner : MonoBehaviour
    {
        private static OsmRunner instance;

        public static OsmRunner Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("GolfSimZA_OsmRunner") { hideFlags = HideFlags.HideInHierarchy };
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<OsmRunner>();
                }
                return instance;
            }
        }

        public static Coroutine Run(IEnumerator routine) => Instance.StartCoroutine(routine);
    }

    /// <summary>
    /// IMPORT COURSES → OPENSTREETMAP: South Africa's golf courses on OpenStreetMap, how many
    /// holes are mapped for each, and BUILD to turn one into a playable GolfSimZA course.
    /// </summary>
    public sealed class OsmCoursePanel
    {
        private const string Country = OsmCatalog.SouthAfrica;
        private const string CountryName = "South Africa";

        private OsmCatalogFile catalog;
        private OsmCatalogFile pendingCatalog;
        private bool loadingList;
        private string listStatus = "";
        private string listError;
        private string search = "";
        private bool mappedOnly = true;
        private Vector2 scroll;

        private OsmCatalogEntry building;
        private OsmBuildState build;
        private string message = "";
        private bool messageIsError;

        // What is drawn is fixed at each Layout event (downloads finish between events).
        private bool vBuilding, vLoading, vMessageIsError;
        private string vMessage = "", vListError, vBuildName = "";

        /// <summary>Set when a build finished, so the library list refreshes.</summary>
        public bool LibraryChanged { get; set; }

        public bool IsBusy => building != null || loadingList;

        public void Open()
        {
            if (catalog == null && !loadingList) LoadList(false);
        }

        /// <summary>Starts a build from code (tests). Returns false when a build is running.</summary>
        public bool StartBuild(OsmCatalogEntry entry)
        {
            if (building != null) return false;
            building = entry;
            build = new OsmBuildState();
            message = "";
            OsmRunner.Run(BuildRoutine(entry, build));
            return true;
        }

        public OsmBuildState CurrentBuild => build;

        private void LoadList(bool force)
        {
            loadingList = true;
            listError = null;
            listStatus = "Downloading the list of South African courses from OpenStreetMap...";
            var result = new NetResult();
            OsmRunner.Run(ListRoutine(force, result));
        }

        private IEnumerator ListRoutine(bool force, NetResult result)
        {
            yield return OsmCatalog.Download(Country, force, s => listStatus = s, file => pendingCatalog = file, result);
            if (!result.Ok) listError = result.Error;
            loadingList = false;
            listStatus = "";
        }

        private IEnumerator BuildRoutine(OsmCatalogEntry entry, OsmBuildState state)
        {
            yield return OsmCourseBuilder.Build(entry, CountryName, state);
            if (state.Error != null)
            {
                message = state.Error;
                messageIsError = true;
            }
            else
            {
                message = "Built " + string.Join(", ", state.Built.ConvertAll(c => c.name)) + ". It is in your course list - ready to play.\n" + state.Report;
                messageIsError = false;
                LibraryChanged = true;
            }
            building = null;
        }

        public void Draw(float width)
        {
            if (Event.current.type == EventType.Layout)
            {
                if (pendingCatalog != null)
                {
                    catalog = pendingCatalog;
                    pendingCatalog = null;
                }
                vBuilding = building != null && build != null;
                vBuildName = building != null ? building.name : "";
                vLoading = loadingList;
                vListError = listError;
                vMessage = message;
                vMessageIsError = messageIsError;
            }

            GUILayout.Label("Build a course from OpenStreetMap: the course layout (holes, tees, greens, fairways, bunkers, water, trees) " +
                            "is downloaded with free ground height data and turned into a playable course. Only courses whose holes are " +
                            "mapped can be built. Map data © OpenStreetMap contributors (ODbL).", GolfSimTheme.Subtitle);
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            GUILayout.Label("SEARCH", GolfSimTheme.Label, GUILayout.Width(70));
            search = GUILayout.TextField(search ?? "", GolfSimTheme.TextField);
            mappedOnly = GUILayout.Toggle(mappedOnly, mappedOnly ? "  HOLES MAPPED ONLY" : "  ALL COURSES", GolfSimTheme.SmallButton, GUILayout.Width(190));
            GUI.enabled = !IsBusy;
            if (GUILayout.Button("REFRESH LIST", GolfSimTheme.SmallButton, GUILayout.Width(140))) LoadList(true);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(vMessage))
                GUILayout.Label(vMessage, vMessageIsError ? GolfSimTheme.Bad_ : GolfSimTheme.Good_);
            if (vBuilding)
            {
                GUILayout.BeginVertical(GolfSimTheme.Card);
                GUILayout.Label("BUILDING  " + vBuildName.ToUpperInvariant(), GolfSimTheme.Heading);
                GUILayout.Label(build != null ? build.Status : "", GolfSimTheme.Subtitle);
                Rect bar = GUILayoutUtility.GetRect(width - 40f, 10f);
                if (Event.current.type == EventType.Repaint)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.15f);
                    GUI.DrawTexture(bar, Texture2D.whiteTexture);
                    GUI.color = GolfSimTheme.Accent;
                    GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(build != null ? build.Progress : 1f), bar.height), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
                GUILayout.EndVertical();
            }
            if (vLoading) GUILayout.Label(listStatus, GolfSimTheme.Label);
            if (vListError != null) GUILayout.Label(vListError, GolfSimTheme.Bad_);

            if (catalog == null)
            {
                GUILayout.FlexibleSpace();
                return;
            }

            List<OsmCatalogEntry> shown = Filter();
            int mapped = 0;
            foreach (OsmCatalogEntry e in catalog.courses) if (e.holesMapped >= 9) mapped++;
            GUILayout.Label($"{catalog.courses.Count} COURSES IN SOUTH AFRICA  •  {mapped} WITH 9+ HOLES MAPPED  •  SHOWING {shown.Count}", GolfSimTheme.Label);

            scroll = GUILayout.BeginScrollView(scroll);
            int drawn = 0;
            foreach (OsmCatalogEntry e in shown)
            {
                if (++drawn > 250) { GUILayout.Label("... narrow the search to see more", GolfSimTheme.Subtitle); break; }
                GUILayout.BeginHorizontal(GolfSimTheme.Card);
                GUILayout.BeginVertical();
                GUILayout.Label(e.name, GolfSimTheme.Heading);
                string holes = e.holesMapped == 0 ? "No holes mapped yet" : e.holesMapped + " holes mapped";
                bool built = CourseLibrary.Contains(OsmCourseBuilder.BaseId(e.osmType, e.osmId));
                GUILayout.Label(holes + (string.IsNullOrEmpty(e.place) ? "" : "  •  " + e.place) + (built ? "  •  BUILT" : ""),
                                e.holesMapped >= 9 ? GolfSimTheme.Good_ : GolfSimTheme.Warning_);
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                GUI.enabled = building == null && e.holesMapped > 0;
                if (GUILayout.Button(building == e ? "BUILDING..." : built ? "REBUILD" : "BUILD", GolfSimTheme.SmallButton, GUILayout.Width(120)))
                    StartBuild(e);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.Label("Course missing or not mapped? Anyone can map it on openstreetmap.org (hole lines, greens, tees, fairways, bunkers). " +
                            "Press REFRESH LIST a day later and build it.", GolfSimTheme.Subtitle);
        }

        private List<OsmCatalogEntry> Filter()
        {
            var list = new List<OsmCatalogEntry>();
            string s = (search ?? "").Trim();
            foreach (OsmCatalogEntry e in catalog.courses)
            {
                if (mappedOnly && e.holesMapped == 0) continue;
                if (s.Length > 0 && e.name.IndexOf(s, System.StringComparison.OrdinalIgnoreCase) < 0 &&
                    (e.place == null || e.place.IndexOf(s, System.StringComparison.OrdinalIgnoreCase) < 0)) continue;
                list.Add(e);
            }
            return list;
        }

        /// <summary>Finds a course in the list by name (tests).</summary>
        public OsmCatalogEntry Find(string namePart)
        {
            if (catalog == null && pendingCatalog != null) catalog = pendingCatalog;
            if (catalog == null) return null;
            OsmCatalogEntry best = null;
            foreach (OsmCatalogEntry e in catalog.courses)
                if (e.name.IndexOf(namePart, System.StringComparison.OrdinalIgnoreCase) >= 0 && (best == null || e.holesMapped > best.holesMapped)) best = e;
            return best;
        }

        public bool HasList => catalog != null || pendingCatalog != null;
    }
}
