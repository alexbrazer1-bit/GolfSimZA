using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace GolfSimZA.Courses.Osm
{
    /// <summary>A polygon or line in local metres (x = east, y = north) with a few properties.</summary>
    [Serializable]
    public sealed class OsmShape
    {
        public Vector2[] pts = new Vector2[0];
        public string tag = "";   // tee colour / hole ref / water kind / path kind
        public float value;       // building height, path width, hole par
        public int number;        // hole number, handicap
        public long osmId;
    }

    /// <summary>
    /// Everything GolfSimZA uses from OpenStreetMap for one course, already in local metres.
    /// Saved with the course (features.json) so the course can be rebuilt without the internet.
    /// </summary>
    [Serializable]
    public sealed class OsmCourseFeatures
    {
        public double originLat, originLon;
        public string courseName = "";
        public long courseOsmId;
        public string courseOsmType = "";
        public Vector2 min, max;                  // area covered (metres)

        public List<OsmShape> boundary = new List<OsmShape>();
        public List<OsmShape> holes = new List<OsmShape>();
        public List<OsmShape> greens = new List<OsmShape>();
        public List<OsmShape> tees = new List<OsmShape>();
        public List<OsmShape> fairways = new List<OsmShape>();
        public List<OsmShape> bunkers = new List<OsmShape>();
        public List<OsmShape> roughs = new List<OsmShape>();
        public List<OsmShape> water = new List<OsmShape>();
        public List<OsmShape> woods = new List<OsmShape>();
        public List<OsmShape> scrub = new List<OsmShape>();
        public List<OsmShape> treeRows = new List<OsmShape>();
        public List<Vector2> trees = new List<Vector2>();
        public List<OsmShape> buildings = new List<OsmShape>();
        public List<OsmShape> paths = new List<OsmShape>();
        public List<Vector2> pins = new List<Vector2>();

        public GeoProjection Projection => new GeoProjection(originLat, originLon);

        public string Summary =>
            $"{holes.Count} holes, {greens.Count} greens, {tees.Count} tees, {fairways.Count} fairways, {bunkers.Count} bunkers, " +
            $"{water.Count} water, {woods.Count} woods, {trees.Count} single trees, {treeRows.Count} tree rows, {buildings.Count} buildings, {paths.Count} paths";

        /// <summary>
        /// Sorts the OpenStreetMap objects of a course download into golf features.
        /// course = the leisure=golf_course object the player picked.
        /// </summary>
        public static OsmCourseFeatures From(List<OsmElement> elements, OsmElement course, GeoProjection projection, Vector2 min, Vector2 max)
        {
            var f = new OsmCourseFeatures
            {
                originLat = projection.OriginLat,
                originLon = projection.OriginLon,
                courseName = course != null ? (course.Tag("name") ?? "Golf course") : "Golf course",
                courseOsmId = course != null ? course.Id : 0,
                courseOsmType = course != null ? course.Type : "",
                min = min,
                max = max
            };

            foreach (OsmElement e in elements)
            {
                if (course != null && e.Type == course.Type && e.Id == course.Id)
                {
                    foreach (List<GeoPoint> ring in Polygons(e)) f.boundary.Add(Shape(ring, projection, e.Id));
                    continue;
                }

                string golf = e.Tag("golf");
                if (e.Type == "node")
                {
                    Vector2 p = projection.ToLocal(e.Lat, e.Lon);
                    if (e.Is("natural", "tree")) f.trees.Add(p);
                    else if (golf == "pin" || golf == "hole_pin" || golf == "flag") f.pins.Add(p);
                    continue;
                }

                if (golf == "hole" && e.Type == "way" && e.Geometry.Count >= 2)
                {
                    OsmShape hole = Line(e.Geometry, projection, e.Id);
                    hole.tag = e.Tag("ref") ?? e.Tag("name") ?? "";
                    hole.number = ParseInt(e.Tag("ref"), 0);
                    hole.value = ParseInt(e.Tag("par"), 0);
                    // handicap index rides along in the tag field after a '|'
                    int hcp = ParseInt(e.Tag("handicap"), 0);
                    if (hcp > 0) hole.tag += "|" + hcp;
                    f.holes.Add(hole);
                    continue;
                }

                // Lines.
                if (e.Type == "way" && !e.IsClosedWay)
                {
                    string highway = e.Tag("highway");
                    if (golf == "cartpath" || golf == "path" || highway != null)
                    {
                        if (e.Is("area", "yes")) continue;
                        OsmShape path = Line(e.Geometry, projection, e.Id);
                        path.tag = golf == "cartpath" || e.Is("golf_cart", "designated") || e.Is("golf_cart", "yes") ? "cartpath" : highway ?? "path";
                        path.value = PathWidth(path.tag, e.Tag("width"));
                        f.paths.Add(path);
                    }
                    else if (e.Is("natural", "tree_row"))
                    {
                        f.treeRows.Add(Line(e.Geometry, projection, e.Id));
                    }
                    else if (golf == "water_hazard" || golf == "lateral_water_hazard")
                    {
                        // Some hazards are drawn as lines along a stream: give them a 6 m wide band.
                        OsmShape line = Line(e.Geometry, projection, e.Id);
                        f.water.Add(new OsmShape { pts = Buffer(line.pts, 3f), tag = golf == "lateral_water_hazard" ? "lateral" : "hazard", osmId = e.Id });
                    }
                    else if (e.Has("waterway") && (e.Is("waterway", "stream") || e.Is("waterway", "river") || e.Is("waterway", "canal") || e.Is("waterway", "ditch")))
                    {
                        OsmShape line = Line(e.Geometry, projection, e.Id);
                        float half = e.Is("waterway", "river") ? 8f : e.Is("waterway", "canal") ? 5f : 1.5f;
                        f.water.Add(new OsmShape { pts = Buffer(line.pts, half), tag = "stream", osmId = e.Id });
                    }
                    continue;
                }

                // Areas (closed ways and multipolygon relations).
                List<List<GeoPoint>> polygons = Polygons(e);
                if (polygons.Count == 0) continue;
                List<OsmShape> target = null;
                string kind = "";
                if (golf == "green") target = f.greens;
                else if (golf == "tee") { target = f.tees; kind = TeeName(e); }
                else if (golf == "fairway") target = f.fairways;
                else if (golf == "bunker" || (golf == null && e.Is("natural", "sand"))) target = f.bunkers;
                else if (golf == "rough") target = f.roughs;
                else if (golf == "driving_range") { target = f.fairways; kind = "range"; }
                else if (golf == "water_hazard" || golf == "lateral_water_hazard") { target = f.water; kind = golf == "lateral_water_hazard" ? "lateral" : "hazard"; }
                else if (e.Is("natural", "water") || e.Has("water") || e.Is("landuse", "reservoir") || e.Is("landuse", "basin") || e.Is("waterway", "riverbank"))
                {
                    target = f.water;
                    // A river / canal area runs downhill: shaped like a stream (follows the ground), not as a flat lake.
                    kind = e.Is("water", "river") || e.Is("water", "canal") || e.Is("water", "stream") || e.Is("waterway", "riverbank") ? "stream" : "water";
                }
                else if (e.Is("natural", "wood") || e.Is("landuse", "forest")) target = f.woods;
                else if (e.Is("natural", "scrub") || e.Is("natural", "heath")) target = f.scrub;
                else if (e.Has("building")) { target = f.buildings; }
                else if (e.Is("leisure", "golf_course")) continue; // a neighbouring course
                if (target == null) continue;

                foreach (List<GeoPoint> ring in polygons)
                {
                    OsmShape s = Shape(ring, projection, e.Id);
                    s.tag = kind;
                    if (target == f.buildings) s.value = BuildingHeight(e);
                    target.Add(s);
                }
            }
            return f;
        }

        // ------------------------------------------------------------ helpers

        private static List<List<GeoPoint>> Polygons(OsmElement e)
        {
            var list = new List<List<GeoPoint>>();
            if (e.Type == "way" && e.IsClosedWay) list.Add(e.Geometry);
            else if (e.Type == "relation") list.AddRange(RingBuilder.Rings(e, "outer"));
            return list;
        }

        private static OsmShape Shape(List<GeoPoint> ring, GeoProjection projection, long id)
        {
            // Closed ring without the repeated last point.
            int n = ring.Count;
            if (n > 1 && Math.Abs(ring[0].Lat - ring[n - 1].Lat) < 1e-9 && Math.Abs(ring[0].Lon - ring[n - 1].Lon) < 1e-9) n--;
            var pts = new Vector2[n];
            for (int i = 0; i < n; i++) pts[i] = projection.ToLocal(ring[i]);
            return new OsmShape { pts = pts, osmId = id };
        }

        private static OsmShape Line(List<GeoPoint> line, GeoProjection projection, long id)
        {
            var pts = new Vector2[line.Count];
            for (int i = 0; i < line.Count; i++) pts[i] = projection.ToLocal(line[i]);
            return new OsmShape { pts = pts, osmId = id };
        }

        private static int ParseInt(string s, int fallback)
        {
            if (string.IsNullOrEmpty(s)) return fallback;
            // "7", "7a", "par 4" -> leading digits
            int value = 0, digits = 0;
            foreach (char c in s)
            {
                if (c >= '0' && c <= '9') { value = value * 10 + (c - '0'); digits++; }
                else if (digits > 0) break;
            }
            return digits > 0 ? value : fallback;
        }

        private static float BuildingHeight(OsmElement e)
        {
            string h = e.Tag("height");
            if (!string.IsNullOrEmpty(h))
            {
                string number = h.Replace("m", "").Trim();
                if (float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out float metres) && metres > 1f) return Mathf.Min(metres, 60f);
            }
            string levels = e.Tag("building:levels");
            if (!string.IsNullOrEmpty(levels) && float.TryParse(levels, NumberStyles.Float, CultureInfo.InvariantCulture, out float l) && l > 0f)
                return Mathf.Clamp(l * 3.2f + 1f, 3f, 60f);
            string b = e.Tag("building") ?? "";
            if (b == "roof" || b == "carport") return 3f;
            if (b == "shed" || b == "hut" || b == "garage") return 3.2f;
            return 5.5f;
        }

        private static float PathWidth(string kind, string widthTag)
        {
            if (!string.IsNullOrEmpty(widthTag) && float.TryParse(widthTag.Replace("m", "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float w) && w > 0.5f)
                return Mathf.Min(w, 20f);
            switch (kind)
            {
                case "cartpath": return 2.4f;
                case "footway": case "path": case "track": case "cycleway": case "bridleway": return 1.8f;
                case "service": return 4f;
                case "residential": case "unclassified": return 6f;
                case "tertiary": case "secondary": case "primary": case "trunk": return 8f;
                case "motorway": return 14f;
                default: return 3f;
            }
        }

        /// <summary>Tee colour / name from the tags (tee=blue, golf:tee=..., colour=..., name=...).</summary>
        private static string TeeName(OsmElement e)
        {
            foreach (string key in new[] { "tee", "golf:tee", "colour", "color", "name", "ref" })
            {
                string v = e.Tag(key);
                if (string.IsNullOrWhiteSpace(v)) continue;
                v = v.Trim();
                string lower = v.ToLowerInvariant();
                foreach (string c in new[] { "black", "blue", "white", "yellow", "red", "green", "gold", "silver", "orange" })
                    if (lower.Contains(c)) return char.ToUpperInvariant(c[0]) + c.Substring(1);
                if (key == "tee" || key == "golf:tee") return char.ToUpperInvariant(v[0]) + (v.Length > 1 ? v.Substring(1) : "");
            }
            return "";
        }

        /// <summary>A line widened into a closed band (for streams and line-drawn hazards).</summary>
        public static Vector2[] Buffer(Vector2[] line, float half)
        {
            if (line.Length < 2) return new Vector2[0];
            var left = new List<Vector2>();
            var right = new List<Vector2>();
            for (int i = 0; i < line.Length; i++)
            {
                Vector2 dir = i == 0 ? line[1] - line[0] : i == line.Length - 1 ? line[i] - line[i - 1] : (line[i + 1] - line[i - 1]);
                if (dir.sqrMagnitude < 1e-6f) dir = Vector2.right;
                dir.Normalize();
                Vector2 n = new Vector2(-dir.y, dir.x) * half;
                left.Add(line[i] + n);
                right.Add(line[i] - n);
            }
            right.Reverse();
            left.AddRange(right);
            return left.ToArray();
        }
    }
}
