using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace GolfSimZA.Courses.Osm
{
    /// <summary>Progress of a course build (read by the UI every frame).</summary>
    public sealed class OsmBuildState
    {
        public string Status = "";
        public float Progress;
        public string Error;
        public bool Done;
        public readonly List<CourseDefinition> Built = new List<CourseDefinition>();
        public string Report = "";
    }

    /// <summary>
    /// Builds a playable GolfSimZA course from OpenStreetMap: downloads the course layout and the
    /// ground heights, works out holes, tees, greens, pins, aim points, water hazards and the
    /// out-of-bounds line, shapes the ground (flat tees, smooth greens, bunkers, lakes) and saves
    /// everything in the course library. The 3D course is made from these files when a round
    /// starts (<see cref="OsmCourseScene"/>).
    /// </summary>
    public static class OsmCourseBuilder
    {
        public const string HeightsFile = "heights.bin";
        public const string SurfaceFile = "surface.bin";
        public const string FeaturesFile = "features.json";
        public const string RawFile = "osm.json";
        public const string SplashFile = "splash.png";

        private const float Margin = 150f;          // ground built around the course boundary
        private const float GreenRampOut = 6f;
        private const float TeeRampOut = 3f;
        /// <summary>Furthest a green / tee bank reaches out on a steep hillside.</summary>
        private const float PlatformReach = 24f;
        /// <summary>Rise per metre of the bank around a levelled green / tee (the smooth ramp peaks at 1.5x: about 28 degrees).</summary>
        private const float PlatformBankSlope = 0.35f;

        /// <summary>Width of the ramp from a levelled green / tee to the natural ground: wider where it has to climb or drop more.</summary>
        private static float RampWidth(float minimum, float heightDifference) =>
            Mathf.Clamp(Mathf.Abs(heightDifference) / PlatformBankSlope, minimum, PlatformReach);

        public static string BaseId(string osmType, long osmId) => "osm-" + (osmType == "relation" ? "r" : "w") + osmId;

        public static IEnumerator Build(OsmCatalogEntry entry, string countryName, OsmBuildState state)
        {
            string libraryRoot = CourseLibrary.RootFolder;
            state.Status = "Finding " + entry.name + " on OpenStreetMap...";
            state.Progress = 0.02f;

            // 1. The course outline (for the area to download).
            var r1 = new NetResult();
            yield return OsmNet.Overpass($"[out:json][timeout:50];{entry.osmType}({entry.osmId});out geom;", 24, r1, s => state.Status = s, 60);
            if (!r1.Ok) { Fail(state, r1.Error); yield break; }
            List<OsmElement> outline;
            try { outline = OsmReader.Read(r1.Text); }
            catch (Exception ex) { Fail(state, "OpenStreetMap answer could not be read: " + ex.Message); yield break; }
            OsmElement courseElement = outline.Find(e => e.Type == entry.osmType && e.Id == entry.osmId);
            if (courseElement == null) { Fail(state, "The course was not found on OpenStreetMap any more (it may have been changed)."); yield break; }

            double south = double.MaxValue, north = double.MinValue, west = double.MaxValue, east = double.MinValue;
            void Grow(GeoPoint p) { south = Math.Min(south, p.Lat); north = Math.Max(north, p.Lat); west = Math.Min(west, p.Lon); east = Math.Max(east, p.Lon); }
            foreach (GeoPoint p in courseElement.Geometry) Grow(p);
            foreach (OsmMember m in courseElement.Members) foreach (GeoPoint p in m.Geometry) Grow(p);
            if (south > north) { Fail(state, "The course outline on OpenStreetMap has no shape."); yield break; }
            double originLat = (south + north) * 0.5, originLon = (west + east) * 0.5;
            var projection = new GeoProjection(originLat, originLon);
            double dLat = Margin / 110574.0, dLon = Margin / (111320.0 * Math.Cos(originLat * Math.PI / 180.0));
            south -= dLat; north += dLat; west -= dLon; east += dLon;

            // 2. Everything on and around the course.
            state.Status = "Downloading the course layout...";
            state.Progress = 0.08f;
            string bbox = string.Format(CultureInfo.InvariantCulture, "{0:0.0000000},{1:0.0000000},{2:0.0000000},{3:0.0000000}", south, west, north, east);
            string query =
                "[out:json][timeout:240][bbox:" + bbox + "];\n(\n" +
                "  nwr[\"golf\"];\n" +
                "  nwr[\"leisure\"=\"golf_course\"];\n" +
                "  nwr[\"natural\"~\"^(water|wood|scrub|heath|sand|tree_row|tree)$\"];\n" +
                "  nwr[\"water\"];\n" +
                "  nwr[\"landuse\"~\"^(forest|reservoir|basin)$\"];\n" +
                "  way[\"waterway\"];\n" +
                "  nwr[\"building\"];\n" +
                "  way[\"highway\"];\n" +
                ");\nout geom;";
            var r2 = new NetResult();
            yield return OsmNet.Overpass(query, 24, r2, s => state.Status = s);
            if (!r2.Ok) { Fail(state, r2.Error); yield break; }

            state.Status = "Reading the course layout...";
            state.Progress = 0.2f;
            Vector2 localMin = projection.ToLocal(south, west), localMax = projection.ToLocal(north, east);
            Task<OsmCourseFeatures> parse = Task.Run(() =>
            {
                List<OsmElement> elements = OsmReader.Read(r2.Text);
                OsmElement course = elements.Find(e => e.Type == entry.osmType && e.Id == entry.osmId) ?? courseElement;
                return OsmCourseFeatures.From(elements, course, projection, localMin, localMax);
            });
            while (!parse.IsCompleted) yield return null;
            if (parse.IsFaulted) { Fail(state, "The course layout could not be read: " + parse.Exception?.GetBaseException().Message); yield break; }
            OsmCourseFeatures features = parse.Result;
            features.courseName = string.IsNullOrWhiteSpace(entry.name) ? features.courseName : entry.name;
            Debug.Log("[GolfSimZA] OpenStreetMap " + features.courseName + ": " + features.Summary);
            if (features.holes.Count == 0)
            {
                Fail(state, features.courseName + " has no holes mapped on OpenStreetMap yet (no golf=hole lines). " +
                            "Anyone can add them on openstreetmap.org; the course can be built a day later.");
                yield break;
            }

            // 3. Ground heights.
            var grid = new ElevationGrid();
            var r3 = new NetResult();
            yield return grid.Download(new GeoPoint(south, west), new GeoPoint(north, east), (s, p) => { state.Status = s; state.Progress = 0.25f + p * 0.25f; }, r3);
            if (!r3.Ok) { Fail(state, r3.Error); yield break; }

            // 4. Holes, ground shaping and files (worker thread).
            state.Status = "Shaping the course (greens, tees, bunkers, water)...";
            state.Progress = 0.55f;
            string baseId = BaseId(entry.osmType, entry.osmId);
            string dataFolder = CourseLibrary.SafeId(baseId);
            string dataPath = Path.Combine(libraryRoot, dataFolder);
            string place = entry.place ?? "";
            Task<BuildOutput> work = Task.Run(() => Compute(features, grid, baseId, dataFolder, dataPath, place, countryName, entry));
            while (!work.IsCompleted)
            {
                state.Progress = Mathf.Min(0.9f, state.Progress + Time.unscaledDeltaTime * 0.02f);
                yield return null;
            }
            if (work.IsFaulted) { Fail(state, "The course could not be built: " + work.Exception?.GetBaseException().Message); yield break; }
            BuildOutput output = work.Result;
            try { File.WriteAllText(Path.Combine(dataPath, RawFile), r2.Text); }
            catch (Exception ex) { Debug.LogWarning("[GolfSimZA] Could not keep the OpenStreetMap download: " + ex.Message); }

            // 5. Preview picture and the course library (main thread).
            state.Status = "Saving the course...";
            state.Progress = 0.95f;
            yield return null;
            byte[] splash = null;
            try { splash = MakeSplash(output.Surface); }
            catch (Exception ex) { Debug.LogWarning("[GolfSimZA] Course preview failed: " + ex.Message); }
            foreach (CourseDefinition def in output.Courses)
            {
                try
                {
                    string folder = CourseLibrary.FolderFor(def.id);
                    Directory.CreateDirectory(folder);
                    if (splash != null) File.WriteAllBytes(Path.Combine(folder, SplashFile), splash);
                    else def.splashFile = "";
                    CourseLibrary.Save(def);
                    state.Built.Add(def);
                }
                catch (Exception ex)
                {
                    Fail(state, "The course could not be saved: " + ex.Message);
                    yield break;
                }
            }
            state.Report = output.Report;
            Debug.Log("[GolfSimZA] OpenStreetMap course built: " + output.Report);
            state.Status = "Ready to play";
            state.Progress = 1f;
            state.Done = true;
        }

        private static void Fail(OsmBuildState state, string message)
        {
            state.Error = message;
            state.Done = true;
            state.Status = "";
            Debug.LogWarning("[GolfSimZA] OpenStreetMap course build failed: " + message);
        }

        // ================================================================ computing

        private sealed class BuildOutput
        {
            public readonly List<CourseDefinition> Courses = new List<CourseDefinition>();
            public OsmSurfaceGrid Surface;
            public string Report;
        }

        private sealed class HolePlan
        {
            public OsmShape Line;
            public int Number;
            public int Par;
            public int Handicap;
            public OsmShape Green;
            public readonly List<OsmShape> Tees = new List<OsmShape>();
            public Vector2 Start => Line.pts[0];
            public Vector2 End => Line.pts[Line.pts.Length - 1];
        }

        private static BuildOutput Compute(OsmCourseFeatures f, ElevationGrid elevation, string baseId, string dataFolder, string dataPath,
                                           string place, string countryName, OsmCatalogEntry entry)
        {
            var report = new StringBuilder();
            Directory.CreateDirectory(dataPath);

            // ---- holes: greens and tees for every hole (made up when they are not mapped)
            var plans = new List<HolePlan>();
            foreach (OsmShape h in f.holes)
            {
                if (h.pts.Length < 2 || Poly2D.LineLength(h.pts) < 40f) continue;
                int hcp = 0;
                int bar = h.tag.IndexOf('|');
                if (bar >= 0) int.TryParse(h.tag.Substring(bar + 1), out hcp);
                plans.Add(new HolePlan { Line = h, Number = h.number, Par = (int)h.value, Handicap = hcp });
            }
            if (plans.Count == 0) throw new InvalidOperationException("the mapped holes are too short to play");

            int madeGreens = 0, madeTees = 0;
            foreach (HolePlan p in plans)
            {
                OsmShape best = null;
                float bestD = 45f;
                foreach (OsmShape g in f.greens)
                {
                    if (g.pts.Length < 3) continue;
                    float d = Poly2D.Distance(g.pts, p.End);
                    if (d < bestD) { bestD = d; best = g; }
                }
                if (best == null)
                {
                    best = new OsmShape { pts = Circle(p.End, 13f, 20), tag = "made" };
                    f.greens.Add(best);
                    madeGreens++;
                }
                p.Green = best;
            }
            foreach (OsmShape t in f.tees)
            {
                if (t.pts.Length < 3) continue;
                Vector2 c = Poly2D.Centroid(t.pts);
                HolePlan owner = null;
                float bestD = 80f;
                foreach (HolePlan p in plans)
                {
                    float d = Vector2.Distance(c, p.Start);
                    // A tee in front of the hole start along the line still belongs to it (forward tees).
                    float along = Poly2D.LineDistance(p.Line.pts, c);
                    if (along < 15f && Vector2.Distance(c, p.End) > 60f) d = Mathf.Min(d, along + Mathf.Min(60f, Vector2.Distance(c, p.Start) * 0.3f));
                    if (d < bestD) { bestD = d; owner = p; }
                }
                owner?.Tees.Add(t);
            }
            foreach (HolePlan p in plans)
            {
                if (p.Tees.Count > 0) continue;
                Vector2 dir = (Poly2D.PointAlong(p.Line.pts, 15f) - p.Start).normalized;
                if (dir.sqrMagnitude < 0.5f) dir = Vector2.up;
                Vector2 side = new Vector2(-dir.y, dir.x);
                Vector2 c = p.Start;
                var tee = new OsmShape { pts = new[] { c - dir * 6f - side * 4f, c + dir * 6f - side * 4f, c + dir * 6f + side * 4f, c - dir * 6f + side * 4f }, tag = "made" };
                f.tees.Add(tee);
                p.Tees.Add(tee);
                madeTees++;
            }

            // ---- terrain square
            Vector2 min = f.min, max = f.max;
            float size = Mathf.Max(max.x - min.x, max.y - min.y);
            size = Mathf.Ceil(size / 64f) * 64f;
            int resolution = size <= 1000f ? 1025 : size <= 2400f ? 2049 : 4097;
            if (size > 6000f) throw new InvalidOperationException($"the course area is too large ({size / 1000f:0.0} km across)");
            Vector2 centre = (min + max) * 0.5f;
            var heights = new OsmHeightField
            {
                Resolution = resolution,
                Size = size,
                OriginX = centre.x - size * 0.5f,
                OriginZ = centre.y - size * 0.5f,
                Heights = new float[resolution * resolution]
            };
            GeoProjection projection = f.Projection;
            float fallback = elevation.AverageHeight();
            float spacing = heights.Spacing;
            Parallel.For(0, resolution, z =>
            {
                for (int x = 0; x < resolution; x++)
                {
                    GeoPoint g = projection.ToGeo(new Vector2(heights.OriginX + x * spacing, heights.OriginZ + z * spacing));
                    heights.Heights[z * resolution + x] = elevation.HeightAt(g.Lat, g.Lon, fallback);
                }
            });
            // The source data is ~30 m: smooth away its steps (about 8 m blur), more on fairways below.
            BoxBlur(heights.Heights, resolution, Mathf.Max(1, Mathf.RoundToInt(5f / spacing)), 3);

            // ---- surfaces
            int cells = resolution - 1;
            var surface = new OsmSurfaceGrid { Cells = cells, Size = size, OriginX = heights.OriginX, OriginZ = heights.OriginZ, Ids = new byte[cells * cells] };
            PaintSurfaces(f, surface);

            // ---- ground shaping
            ShapeFairways(heights, surface);
            // Lake levels and their banks first, so greens / tees / bunkers next to the water are
            // fitted to the graded ground; the lake beds are carved last.
            foreach (OsmShape w in f.water) ShapeWaterBanks(heights, w);
            foreach (OsmShape g in f.greens) ShapeGreen(heights, g);
            foreach (OsmShape t in f.tees) ShapeTee(heights, t);
            foreach (OsmShape b in f.bunkers) ShapeBunker(heights, b);
            int lakes = 0;
            foreach (OsmShape w in f.water) if (ShapeWater(heights, w)) lakes++;

            heights.Save(Path.Combine(dataPath, HeightsFile));
            surface.Save(Path.Combine(dataPath, SurfaceFile));
            File.WriteAllText(Path.Combine(dataPath, FeaturesFile), JsonUtility.ToJson(f));

            // ---- courses (clubs with 27 / 36 holes map several courses on one area)
            List<List<HolePlan>> groups = GroupHoles(plans);
            var output = new BuildOutput { Surface = surface };
            for (int gi = 0; gi < groups.Count; gi++)
            {
                List<HolePlan> group = groups[gi];
                string id = gi == 0 ? baseId : baseId + "-" + (char)('b' + gi - 1);
                string name = groups.Count == 1 ? f.courseName : f.courseName + " - Course " + (char)('A' + gi);
                CourseDefinition def = MakeCourse(f, heights, group, id, name, dataFolder, place, countryName, entry, out string notes);
                output.Courses.Add(def);
                report.Append(name).Append(": ").Append(def.HoleCount).Append(" holes, par ").Append(def.TotalPar)
                      .Append(", tees ").Append(string.Join("/", def.tees)).Append(", ").Append(def.hazards.Length).Append(" water hazards")
                      .Append(def.outOfBounds.IsValid ? ", out of bounds line" : ", no out of bounds line").Append(notes).Append(". ");
            }
            report.Append($"Ground {size:0} m square, {resolution - 1} cells, heights {heights.MinHeight:0}-{heights.MaxHeight:0} m. ");
            report.Append($"{f.greens.Count} greens ({madeGreens} made up), {f.tees.Count} tees ({madeTees} made up), {f.bunkers.Count} bunkers, {lakes} water areas, ");
            report.Append($"{f.trees.Count} trees + {f.woods.Count} woods + {f.treeRows.Count} tree rows, {f.buildings.Count} buildings.");
            if (elevation.MissingTiles > 0) report.Append($" {elevation.MissingTiles} ground height tiles missing.");
            output.Report = report.ToString();
            return output;
        }

        // ---------------------------------------------------------------- holes → course

        private static List<List<HolePlan>> GroupHoles(List<HolePlan> plans)
        {
            plans.Sort((a, b) => (a.Number == 0 ? 999 : a.Number).CompareTo(b.Number == 0 ? 999 : b.Number));
            var groups = new List<List<HolePlan>>();
            foreach (HolePlan p in plans)
            {
                List<HolePlan> best = null;
                float bestD = 1500f;
                foreach (List<HolePlan> g in groups)
                {
                    if (p.Number > 0 && g.Exists(h => h.Number == p.Number)) continue;
                    float d = float.MaxValue;
                    foreach (HolePlan h in g)
                        d = Mathf.Min(d, Mathf.Min(Vector2.Distance(h.End, p.Start), Vector2.Distance(h.Start, p.End)));
                    if (d < bestD) { bestD = d; best = g; }
                }
                if (best == null) { best = new List<HolePlan>(); groups.Add(best); }
                best.Add(p);
            }
            // Tiny leftovers (a practice hole, a mapping slip) join the biggest group when their number is free.
            groups.Sort((a, b) => b.Count.CompareTo(a.Count));
            for (int i = groups.Count - 1; i >= 1; i--)
            {
                if (groups[i].Count >= 3) continue;
                foreach (HolePlan p in groups[i])
                    if (p.Number == 0 || !groups[0].Exists(h => h.Number == p.Number)) groups[0].Add(p);
                groups.RemoveAt(i);
            }
            foreach (List<HolePlan> g in groups) OrderHoles(g);
            return groups;
        }

        /// <summary>Numbered holes in order; unnumbered holes follow the walk from green to the nearest next tee.</summary>
        private static void OrderHoles(List<HolePlan> g)
        {
            var numbered = g.FindAll(h => h.Number > 0);
            var loose = g.FindAll(h => h.Number <= 0);
            numbered.Sort((a, b) => a.Number.CompareTo(b.Number));
            var ordered = new List<HolePlan>(numbered);
            while (loose.Count > 0)
            {
                Vector2 from = ordered.Count > 0 ? ordered[ordered.Count - 1].End : loose[0].Start;
                int bi = 0;
                float bd = float.MaxValue;
                for (int i = 0; i < loose.Count; i++)
                {
                    float d = Vector2.Distance(from, loose[i].Start);
                    if (d < bd) { bd = d; bi = i; }
                }
                ordered.Add(loose[bi]);
                loose.RemoveAt(bi);
            }
            g.Clear();
            g.AddRange(ordered);
        }

        private static CourseDefinition MakeCourse(OsmCourseFeatures f, OsmHeightField heights, List<HolePlan> group, string id, string name,
                                                   string dataFolder, string place, string countryName, OsmCatalogEntry entry, out string notes)
        {
            var noteText = new StringBuilder();
            Vector3 W(Vector2 p, float lift = 0f) => new Vector3(p.x, heights.Sample(p) + lift, p.y);

            // Tee names for the whole course.
            int maxTees = 1;
            var named = new Dictionary<string, List<float>>();
            int namedHoles = 0;
            foreach (HolePlan p in group)
            {
                maxTees = Mathf.Max(maxTees, p.Tees.Count);
                bool any = false;
                foreach (OsmShape t in p.Tees)
                {
                    if (string.IsNullOrEmpty(t.tag) || t.tag == "made") continue;
                    any = true;
                    if (!named.TryGetValue(t.tag, out List<float> dists)) named[t.tag] = dists = new List<float>();
                    dists.Add(Vector2.Distance(Poly2D.Centroid(t.pts), Poly2D.Centroid(p.Green.pts)));
                }
                if (any) namedHoles++;
            }
            string[] teeNames;
            bool useTagNames = named.Count > 0 && namedHoles >= group.Count / 2;
            if (useTagNames)
            {
                var list = new List<string>(named.Keys);
                list.Sort((a, b) => Average(named[b]).CompareTo(Average(named[a]))); // longest first
                teeNames = list.ToArray();
            }
            else teeNames = maxTees >= 3 ? new[] { "Back", "Middle", "Forward" } : maxTees == 2 ? new[] { "Back", "Forward" } : new[] { "Club" };

            var holes = new List<HoleDefinition>();
            int number = 0;
            foreach (HolePlan p in group)
            {
                number++;
                Vector2 greenCentre = Poly2D.Centroid(p.Green.pts);
                if (!Poly2D.Contains(p.Green.pts, greenCentre)) greenCentre = p.End;

                // Tees: farthest first.
                var tees = new List<OsmShape>(p.Tees);
                tees.Sort((a, b) => Vector2.Distance(Poly2D.Centroid(b.pts), greenCentre).CompareTo(Vector2.Distance(Poly2D.Centroid(a.pts), greenCentre)));
                var teeDefs = new List<TeeDefinition>();
                for (int i = 0; i < teeNames.Length; i++)
                {
                    OsmShape pick = null;
                    if (useTagNames) pick = tees.Find(t => t.tag == teeNames[i]);
                    if (pick == null)
                    {
                        float frac = teeNames.Length == 1 ? 0f : i / (float)(teeNames.Length - 1);
                        pick = tees[Mathf.Clamp(Mathf.RoundToInt(frac * (tees.Count - 1)), 0, tees.Count - 1)];
                    }
                    teeDefs.Add(new TeeDefinition { name = teeNames[i], position = W(Poly2D.Centroid(pick.pts), 0.02f) });
                }

                // Pins: a mapped pin, else the middle; front and back pins along the approach.
                Vector2 approachFrom = p.Line.pts.Length > 2 ? p.Line.pts[p.Line.pts.Length - 2] : p.Start;
                Vector2 dir = (greenCentre - approachFrom).normalized;
                if (dir.sqrMagnitude < 0.5f) dir = Vector2.up;
                Vector2 side = new Vector2(-dir.y, dir.x);
                Vector2 friday = greenCentre;
                foreach (Vector2 pin in f.pins)
                    if (Poly2D.Contains(p.Green.pts, pin)) { friday = pin; break; }
                float depth = GreenExtent(p.Green.pts, greenCentre, dir);
                float width = GreenExtent(p.Green.pts, greenCentre, side);
                Vector2 thursday = InsideGreen(p.Green.pts, greenCentre - dir * depth * 0.45f + side * width * 0.15f, greenCentre);
                Vector2 sunday = InsideGreen(p.Green.pts, greenCentre + dir * depth * 0.5f - side * width * 0.4f, greenCentre);
                friday = InsideGreen(p.Green.pts, friday, greenCentre);

                // Aim points: bends of the hole line away from tee and green.
                var aims = new List<Vector3>();
                float length = Poly2D.LineLength(p.Line.pts);
                for (int i = 1; i < p.Line.pts.Length - 1; i++)
                {
                    Vector2 a = p.Line.pts[i - 1], b = p.Line.pts[i], c = p.Line.pts[i + 1];
                    float turn = Vector2.Angle(b - a, c - b);
                    if (turn < 8f) continue;
                    if (Vector2.Distance(b, p.Start) < 60f || Vector2.Distance(b, p.End) < 50f) continue;
                    aims.Add(W(b));
                }

                int par = p.Par >= 3 && p.Par <= 6 ? p.Par : length <= 230f ? 3 : length <= 430f ? 4 : 5;
                holes.Add(new HoleDefinition
                {
                    number = number,
                    par = par,
                    handicapIndex = p.Handicap,
                    tees = teeDefs.ToArray(),
                    pins = new[]
                    {
                        new PinDefinition { day = "Thursday", position = W(thursday) },
                        new PinDefinition { day = "Friday", position = W(friday) },
                        new PinDefinition { day = "Saturday", position = W(InsideGreen(p.Green.pts, greenCentre + side * width * 0.35f, greenCentre)) },
                        new PinDefinition { day = "Sunday", position = W(sunday) }
                    },
                    hasGreenCenter = true,
                    greenCenter = W(greenCentre),
                    aimPoints = aims.ToArray()
                });
            }

            // Water hazards near this course's holes.
            var hazards = new List<HazardDefinition>();
            foreach (OsmShape w in f.water)
            {
                if (w.pts.Length < 3 || Poly2D.Area(w.pts) < 40f) continue;
                bool near = false, holdsGreenOrTee = false;
                Vector2 c = Poly2D.Centroid(w.pts);
                foreach (HolePlan p in group)
                {
                    if (Poly2D.LineDistance(p.Line.pts, c) < 350f || Poly2D.Distance(w.pts, p.End) < 120f) near = true;
                    if (Poly2D.Contains(w.pts, Poly2D.Centroid(p.Green.pts)) || Poly2D.Contains(w.pts, p.Start)) holdsGreenOrTee = true;
                }
                if (!near || holdsGreenOrTee) continue;
                var pts = new Vector3[w.pts.Length];
                for (int i = 0; i < pts.Length; i++) pts[i] = new Vector3(w.pts[i].x, w.value == StreamLevel ? heights.Sample(w.pts[i]) : w.value, w.pts[i].y);
                hazards.Add(new HazardDefinition { area = new PolygonDefinition { points = pts } });
            }

            // Out of bounds: the course boundary (a little outside it), when every tee and green is inside.
            var ob = new PolygonDefinition();
            OsmShape boundary = null;
            foreach (OsmShape b in f.boundary)
                if (b.pts.Length >= 3 && (boundary == null || Poly2D.Area(b.pts) > Poly2D.Area(boundary.pts))) boundary = b;
            if (boundary != null)
            {
                Vector2[] grown = Grow(boundary.pts, 10f);
                bool allInside = true;
                foreach (HolePlan p in group)
                {
                    if (!Poly2D.Contains(grown, p.End) || !Poly2D.Contains(grown, p.Start)) { allInside = false; break; }
                }
                if (allInside)
                {
                    var pts = new Vector3[grown.Length];
                    for (int i = 0; i < pts.Length; i++) pts[i] = W(grown[i]);
                    ob.points = pts;
                }
                else noteText.Append(" (course outline does not hold every hole - out of bounds left off)");
            }

            float altitude = 0f;
            foreach (HoleDefinition h in holes) altitude += h.greenCenter.y;
            altitude /= Mathf.Max(1, holes.Count);

            notes = noteText.ToString();
            return new CourseDefinition
            {
                id = id,
                name = name,
                location = place,
                country = countryName,
                designer = "",
                description = "Built from OpenStreetMap (© OpenStreetMap contributors, ODbL) and free elevation data.",
                sourceType = CourseDefinition.SourceOpenStreetMap,
                sourceFolder = dataFolder,
                bundleFile = "",
                splashFile = SplashFile,
                importedAtUtc = DateTime.UtcNow.ToString("o"),
                altitudeMeters = altitude,
                tees = teeNames,
                holes = holes.ToArray(),
                outOfBounds = ob,
                hazards = hazards.ToArray(),
                osmType = entry.osmType,
                osmId = entry.osmId,
                originLat = f.originLat,
                originLon = f.originLon
            };
        }

        private static float Average(List<float> v)
        {
            float s = 0f;
            foreach (float x in v) s += x;
            return v.Count > 0 ? s / v.Count : 0f;
        }

        /// <summary>How far the green reaches from its centre along a direction.</summary>
        private static float GreenExtent(Vector2[] green, Vector2 centre, Vector2 dir)
        {
            float reach = 4f;
            for (float d = 1f; d < 60f; d += 1f)
            {
                if (!Poly2D.Contains(green, centre + dir * d)) break;
                reach = d;
            }
            return reach;
        }

        /// <summary>Moves a pin toward the green centre until it is at least 3 m inside the edge.</summary>
        private static Vector2 InsideGreen(Vector2[] green, Vector2 p, Vector2 centre)
        {
            for (int i = 0; i <= 10; i++)
            {
                Vector2 q = Vector2.Lerp(p, centre, i / 10f);
                if (Poly2D.Contains(green, q) && Poly2D.EdgeDistance(green, q) >= 3f) return q;
            }
            return centre;
        }

        private static Vector2[] Circle(Vector2 c, float r, int n)
        {
            var pts = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                pts[i] = c + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.85f);
            }
            return pts;
        }

        /// <summary>Pushes every corner of a polygon outward by distance.</summary>
        private static Vector2[] Grow(Vector2[] p, float distance)
        {
            int n = p.Length;
            float sign = Poly2D.SignedArea(p) > 0f ? 1f : -1f; // counter-clockwise: outward = right of each edge
            var result = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = p[(i + n - 1) % n], cur = p[i], next = p[(i + 1) % n];
                Vector2 e1 = (cur - prev).normalized, e2 = (next - cur).normalized;
                Vector2 n1 = new Vector2(e1.y, -e1.x) * sign, n2 = new Vector2(e2.y, -e2.x) * sign;
                Vector2 normal = (n1 + n2);
                normal = normal.sqrMagnitude > 1e-4f ? normal.normalized : n1;
                result[i] = cur + normal * distance;
            }
            return result;
        }

        // ---------------------------------------------------------------- surfaces

        private static void PaintSurfaces(OsmCourseFeatures f, OsmSurfaceGrid s)
        {
            int n = s.Cells;
            byte[] ids = s.Ids;
            var gmin = new Vector2(s.OriginX, s.OriginZ);
            float cell = s.CellSize;

            bool hasBoundary = false;
            foreach (OsmShape b in f.boundary) if (b.pts.Length >= 3) hasBoundary = true;
            for (int i = 0; i < ids.Length; i++) ids[i] = hasBoundary ? OsmSurface.Native : OsmSurface.Rough;
            void Fill(List<OsmShape> shapes, byte id)
            {
                foreach (OsmShape sh in shapes)
                    if (sh.pts.Length >= 3) Poly2D.Rasterize(sh.pts, gmin, cell, n, n, (x, z) => ids[z * n + x] = id);
            }
            if (hasBoundary) Fill(f.boundary, OsmSurface.Rough);
            Fill(f.roughs, OsmSurface.Rough);
            Fill(f.fairways, OsmSurface.Fairway);
            // Fringe: a collar around every green.
            foreach (OsmShape g in f.greens)
            {
                if (g.pts.Length < 3) continue;
                Vector2[] collar = Grow(g.pts, 1.8f);
                Poly2D.Rasterize(collar, gmin, cell, n, n, (x, z) => ids[z * n + x] = OsmSurface.Fringe);
            }
            Fill(f.greens, OsmSurface.Green);
            Fill(f.tees, OsmSurface.Tee);
            Fill(f.bunkers, OsmSurface.Bunker);
            foreach (OsmShape p in f.paths)
            {
                if (p.pts.Length < 2) continue;
                bool onCourse = p.tag == "cartpath" || p.value <= 3f;
                Poly2D.RasterizeLine(p.pts, p.value * 0.5f, gmin, cell, n, n, (x, z) =>
                {
                    byte cur = ids[z * n + x];
                    // Paths never cut through greens, tees or bunkers.
                    if (cur == OsmSurface.Green || cur == OsmSurface.Tee || cur == OsmSurface.Bunker) return;
                    if (!onCourse && cur == OsmSurface.Fairway) return;
                    ids[z * n + x] = OsmSurface.Path;
                });
            }
            Fill(f.water, OsmSurface.Native);
        }

        // ---------------------------------------------------------------- ground shaping

        private static void ShapeFairways(OsmHeightField h, OsmSurfaceGrid s)
        {
            int res = h.Resolution;
            var smooth = (float[])h.Heights.Clone();
            BoxBlur(smooth, res, Mathf.Max(1, Mathf.RoundToInt(10f / h.Spacing)), 2);
            var mask = new float[res * res];
            int n = s.Cells;
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    byte id = s.Ids[Mathf.Min(z, n - 1) * n + Mathf.Min(x, n - 1)];
                    mask[z * res + x] = id == OsmSurface.Fairway || id == OsmSurface.Green || id == OsmSurface.Fringe || id == OsmSurface.Tee ? 1f : 0f;
                }
            BoxBlur(mask, res, Mathf.Max(1, Mathf.RoundToInt(6f / h.Spacing)), 2);
            for (int i = 0; i < h.Heights.Length; i++) h.Heights[i] = Mathf.Lerp(h.Heights[i], smooth[i], mask[i] * 0.7f);
        }

        /// <summary>Calls visit(x, z, point) for every height point within reach of a polygon.</summary>
        private static void ForPoints(OsmHeightField h, Vector2[] poly, float reach, Action<int, int, Vector2> visit)
        {
            Poly2D.Bounds(poly, out Vector2 min, out Vector2 max);
            int x0 = Mathf.Max(0, Mathf.FloorToInt((min.x - reach - h.OriginX) / h.Spacing));
            int x1 = Mathf.Min(h.Resolution - 1, Mathf.CeilToInt((max.x + reach - h.OriginX) / h.Spacing));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((min.y - reach - h.OriginZ) / h.Spacing));
            int z1 = Mathf.Min(h.Resolution - 1, Mathf.CeilToInt((max.y + reach - h.OriginZ) / h.Spacing));
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                    visit(x, z, new Vector2(h.OriginX + x * h.Spacing, h.OriginZ + z * h.Spacing));
        }

        private static void ShapeGreen(OsmHeightField h, OsmShape g)
        {
            if (g.pts.Length < 3) return;
            // Plane through the ground under the green, its slope kept playable (max 2.5 %).
            double sx = 0, sz = 0, sh = 0, sxx = 0, szz = 0, sxz = 0, sxh = 0, szh = 0;
            int count = 0;
            Vector2 c = Poly2D.Centroid(g.pts);
            ForPoints(h, g.pts, 0f, (x, z, p) =>
            {
                if (!Poly2D.Contains(g.pts, p)) return;
                double dx = p.x - c.x, dz = p.y - c.y, hh = h.Heights[z * h.Resolution + x];
                sx += dx; sz += dz; sh += hh; sxx += dx * dx; szz += dz * dz; sxz += dx * dz; sxh += dx * hh; szh += dz * hh;
                count++;
            });
            if (count < 4) return;
            double mean = sh / count;
            double det = sxx * szz - sxz * sxz;
            double a = 0, b = 0;
            if (Math.Abs(det) > 1e-6)
            {
                double cxh = sxh - sx * mean, czh = szh - sz * mean;
                a = (cxh * szz - czh * sxz) / det;
                b = (czh * sxx - cxh * sxz) / det;
            }
            double slope = Math.Sqrt(a * a + b * b);
            if (slope > 0.025) { a *= 0.025 / slope; b *= 0.025 / slope; }
            float seed = c.x * 0.013f + c.y * 0.007f;
            ForPoints(h, g.pts, PlatformReach, (x, z, p) =>
            {
                float sd = Poly2D.SignedDistance(g.pts, p);
                if (sd > PlatformReach) return;
                float target = (float)(mean + a * (p.x - c.x) + b * (p.y - c.y));
                // Gentle breaks and a slight crown.
                float inside = Mathf.Clamp01(-sd / 6f);
                target += 0.16f * inside + 0.14f * (Noise(p.x / 13f + seed, p.y / 13f) - 0.5f) + 0.06f * (Noise(p.x / 5f, p.y / 5f + seed) - 0.5f);
                int i = z * h.Resolution + x;
                float ramp = RampWidth(GreenRampOut, target - h.Heights[i]);
                if (sd > ramp) return;
                float w = sd <= 0f ? 1f : 1f - Smooth(0f, ramp, sd);
                h.Heights[i] = Mathf.Lerp(h.Heights[i], target, w);
            });
        }

        private static void ShapeTee(OsmHeightField h, OsmShape t)
        {
            if (t.pts.Length < 3) return;
            double sum = 0;
            int count = 0;
            ForPoints(h, t.pts, 0f, (x, z, p) =>
            {
                if (!Poly2D.Contains(t.pts, p)) return;
                sum += h.Heights[z * h.Resolution + x];
                count++;
            });
            if (count == 0) { Vector2 c = Poly2D.Centroid(t.pts); sum = h.Sample(c); count = 1; }
            float level = (float)(sum / count) + 0.35f;
            ForPoints(h, t.pts, PlatformReach, (x, z, p) =>
            {
                float sd = Poly2D.SignedDistance(t.pts, p);
                if (sd > PlatformReach) return;
                int i = z * h.Resolution + x;
                float ramp = RampWidth(TeeRampOut, level - h.Heights[i]);
                if (sd > ramp) return;
                float w = sd <= 0f ? 1f : 1f - Smooth(0f, ramp, sd);
                h.Heights[i] = Mathf.Lerp(h.Heights[i], level, w);
            });
        }

        private static void ShapeBunker(OsmHeightField h, OsmShape b)
        {
            if (b.pts.Length < 3) return;
            float area = Poly2D.Area(b.pts);
            float depth = Mathf.Clamp(0.35f + Mathf.Sqrt(area) * 0.03f, 0.4f, 0.9f);
            ForPoints(h, b.pts, 0.5f, (x, z, p) =>
            {
                float sd = Poly2D.SignedDistance(b.pts, p);
                int i = z * h.Resolution + x;
                if (sd > 0f)
                {
                    // A small lip just outside.
                    h.Heights[i] += 0.08f * (1f - Smooth(0f, 0.5f, sd));
                    return;
                }
                h.Heights[i] -= depth * Smooth(0f, 3f, -sd);
            });
        }

        private static bool IsLake(OsmShape w) => w.pts.Length >= 3 && Poly2D.Area(w.pts) >= 8f;

        /// <summary>Water level of a lake: just under the lowest point of its shore.</summary>
        private static float LakeLevel(OsmHeightField h, OsmShape w)
        {
            float level = float.MaxValue;
            foreach (Vector2 p in w.pts) level = Mathf.Min(level, h.Sample(p));
            return level - 0.35f;
        }

        /// <summary>Bank slope for the first BankNear metres from the water (rise per metre, about 17 degrees).</summary>
        private const float BankSlope = 0.3f;
        /// <summary>Steepest ground further out (about 31 degrees: a ball still stops on it).</summary>
        private const float BankSlopeFar = 0.6f;
        private const float BankNear = 10f;
        private const float BankReach = 60f;

        /// <summary>
        /// The lake level comes from the LOWEST point of its shore, so on a hillside the uphill shore
        /// stands metres above the water and the ground would drop to the water in one step (a wall
        /// that traps a ball). Around a lake the ground may rise at most BankSlope for the first
        /// BankNear metres and BankSlopeFar after that: ground above that limit is lowered onto it.
        /// The limit is one continuous surface, so it never leaves a step of its own. Stores the
        /// water level in w.value (streams: worked out when the scene is built).
        /// </summary>
        private static void ShapeWaterBanks(OsmHeightField h, OsmShape w)
        {
            if (!IsLake(w)) return;
            if (w.tag == "stream")
            {
                w.value = StreamLevel;
                return;
            }
            float level = LakeLevel(h, w);
            w.value = level;
            float shore = level + 0.3f;
            ForPoints(h, w.pts, BankReach, (x, z, p) =>
            {
                float sd = Poly2D.SignedDistance(w.pts, p);
                if (sd <= 0f || sd > BankReach) return;
                float limit = shore + BankSlope * Mathf.Min(sd, BankNear) + BankSlopeFar * Mathf.Max(0f, sd - BankNear);
                int i = z * h.Resolution + x;
                if (h.Heights[i] > limit) h.Heights[i] = limit;
            });
        }

        /// <summary>Lowers the ground under a lake / stream (level from ShapeWaterBanks, in w.value).</summary>
        private static bool ShapeWater(OsmHeightField h, OsmShape w)
        {
            if (!IsLake(w)) return false;
            bool stream = w.tag == "stream";
            float level = stream ? 0f : w.value;
            ForPoints(h, w.pts, 0f, (x, z, p) =>
            {
                float sd = Poly2D.SignedDistance(w.pts, p);
                if (sd > 0f) return;
                int i = z * h.Resolution + x;
                float bed = (stream ? h.Heights[i] - 0.9f : level) - 0.3f - 1.4f * Smooth(0f, 6f, -sd);
                if (h.Heights[i] > bed) h.Heights[i] = bed;
            });
            return true;
        }

        /// <summary>Water level marker for streams (their surface follows the ground).</summary>
        public const float StreamLevel = -99999f;

        private static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Repeated box blur (close to a gaussian) on a square grid.</summary>
        private static void BoxBlur(float[] data, int res, int radius, int passes)
        {
            var tmp = new float[data.Length];
            for (int pass = 0; pass < passes; pass++)
            {
                Parallel.For(0, res, z =>
                {
                    float sum = 0f;
                    int row = z * res;
                    for (int x = -radius; x <= radius; x++) sum += data[row + Mathf.Clamp(x, 0, res - 1)];
                    for (int x = 0; x < res; x++)
                    {
                        tmp[row + x] = sum / (2 * radius + 1);
                        sum += data[row + Mathf.Min(res - 1, x + radius + 1)] - data[row + Mathf.Max(0, x - radius)];
                    }
                });
                Parallel.For(0, res, x =>
                {
                    float sum = 0f;
                    for (int z = -radius; z <= radius; z++) sum += tmp[Mathf.Clamp(z, 0, res - 1) * res + x];
                    for (int z = 0; z < res; z++)
                    {
                        data[z * res + x] = sum / (2 * radius + 1);
                        sum += tmp[Mathf.Min(res - 1, z + radius + 1) * res + x] - tmp[Mathf.Max(0, z - radius) * res + x];
                    }
                });
            }
        }

        /// <summary>Smooth value noise 0..1 (thread safe, no Unity calls).</summary>
        public static float Noise(float x, float y)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float tx = x - x0, ty = y - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            float a = Hash(x0, y0), b = Hash(x0 + 1, y0), c = Hash(x0, y0 + 1), d = Hash(x0 + 1, y0 + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        // ---------------------------------------------------------------- preview

        private static readonly Color32[] MapColours =
        {
            new Color32(70, 110, 45, 255),   // rough
            new Color32(98, 160, 62, 255),   // fairway
            new Color32(110, 190, 80, 255),  // green
            new Color32(100, 165, 70, 255),  // tee
            new Color32(222, 205, 160, 255), // bunker
            new Color32(150, 145, 135, 255), // path
            new Color32(92, 150, 60, 255),   // fringe
            new Color32(118, 120, 78, 255),  // native
        };

        private static byte[] MakeSplash(OsmSurfaceGrid s)
        {
            const int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int cx = x * s.Cells / size, cz = y * s.Cells / size;
                    px[y * size + x] = MapColours[Mathf.Min(s.Ids[cz * s.Cells + cx], (byte)7)];
                }
            tex.SetPixels32(px);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.Destroy(tex);
            return png;
        }
    }
}
