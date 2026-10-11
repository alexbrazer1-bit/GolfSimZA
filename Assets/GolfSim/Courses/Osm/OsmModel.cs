using System;
using System.Collections.Generic;
using UnityEngine;

namespace GolfSimZA.Courses.Osm
{
    /// <summary>A point on the earth (degrees).</summary>
    [Serializable]
    public struct GeoPoint
    {
        public double Lat;
        public double Lon;
        public GeoPoint(double lat, double lon) { Lat = lat; Lon = lon; }
    }

    /// <summary>One OpenStreetMap object from an Overpass "out geom" answer.</summary>
    public sealed class OsmElement
    {
        public string Type;          // node, way, relation
        public long Id;
        public readonly Dictionary<string, string> Tags = new Dictionary<string, string>();
        public double Lat, Lon;      // node
        public readonly List<GeoPoint> Geometry = new List<GeoPoint>();   // way
        public readonly List<OsmMember> Members = new List<OsmMember>(); // relation
        public bool HasCenter;
        public double CenterLat, CenterLon;

        public string Tag(string key) => Tags.TryGetValue(key, out string v) ? v : null;
        public bool Is(string key, string value) => Tags.TryGetValue(key, out string v) && string.Equals(v, value, StringComparison.OrdinalIgnoreCase);
        public bool Has(string key) => Tags.ContainsKey(key);

        public bool IsClosedWay => Type == "way" && Geometry.Count >= 4 &&
                                   Math.Abs(Geometry[0].Lat - Geometry[Geometry.Count - 1].Lat) < 1e-9 &&
                                   Math.Abs(Geometry[0].Lon - Geometry[Geometry.Count - 1].Lon) < 1e-9;
    }

    public sealed class OsmMember
    {
        public string Type;
        public long Ref;
        public string Role;
        public readonly List<GeoPoint> Geometry = new List<GeoPoint>();
    }

    /// <summary>Reads Overpass JSON ("out geom" / "out tags center").</summary>
    public static class OsmReader
    {
        public static List<OsmElement> Read(string json)
        {
            var result = new List<OsmElement>();
            object root = MiniJson.Parse(json);
            List<object> elements = MiniJson.Arr(root, "elements");
            if (elements == null) return result;
            foreach (object o in elements)
            {
                var d = o as Dictionary<string, object>;
                if (d == null) continue;
                var e = new OsmElement
                {
                    Type = MiniJson.Str(d, "type") ?? "",
                    Id = (long)MiniJson.Num(d, "id"),
                    Lat = MiniJson.Num(d, "lat"),
                    Lon = MiniJson.Num(d, "lon")
                };
                Dictionary<string, object> tags = MiniJson.Obj(d, "tags");
                if (tags != null)
                    foreach (KeyValuePair<string, object> t in tags)
                        e.Tags[t.Key] = t.Value as string ?? Convert.ToString(t.Value, System.Globalization.CultureInfo.InvariantCulture);
                Dictionary<string, object> center = MiniJson.Obj(d, "center");
                if (center != null)
                {
                    e.HasCenter = true;
                    e.CenterLat = MiniJson.Num(center, "lat");
                    e.CenterLon = MiniJson.Num(center, "lon");
                }
                ReadGeometry(MiniJson.Arr(d, "geometry"), e.Geometry);
                List<object> members = MiniJson.Arr(d, "members");
                if (members != null)
                    foreach (object mo in members)
                    {
                        var m = new OsmMember
                        {
                            Type = MiniJson.Str(mo, "type") ?? "",
                            Ref = (long)MiniJson.Num(mo, "ref"),
                            Role = MiniJson.Str(mo, "role") ?? ""
                        };
                        ReadGeometry(MiniJson.Arr(mo, "geometry"), m.Geometry);
                        e.Members.Add(m);
                    }
                if (e.Type == "node" && !e.HasCenter)
                {
                    e.CenterLat = e.Lat;
                    e.CenterLon = e.Lon;
                }
                result.Add(e);
            }
            return result;
        }

        private static void ReadGeometry(List<object> geometry, List<GeoPoint> into)
        {
            if (geometry == null) return;
            foreach (object g in geometry)
            {
                if (g == null) continue; // Overpass writes null for nodes outside the query area
                into.Add(new GeoPoint(MiniJson.Num(g, "lat"), MiniJson.Num(g, "lon")));
            }
        }
    }

    /// <summary>
    /// Flat local coordinates around the course: x = metres east, y = metres north of the
    /// origin (Unity world: x = east, z = north). Accurate to centimetres over a few km.
    /// </summary>
    [Serializable]
    public struct GeoProjection
    {
        public double OriginLat;
        public double OriginLon;

        private const double MetresPerDegreeLat = 110574.0;
        private const double MetresPerDegreeLonEquator = 111320.0;

        public GeoProjection(double lat, double lon) { OriginLat = lat; OriginLon = lon; }

        private double LonScale => MetresPerDegreeLonEquator * Math.Cos(OriginLat * Math.PI / 180.0);

        public Vector2 ToLocal(double lat, double lon)
        {
            return new Vector2((float)((lon - OriginLon) * LonScale), (float)((lat - OriginLat) * MetresPerDegreeLat));
        }

        public Vector2 ToLocal(GeoPoint p) => ToLocal(p.Lat, p.Lon);

        public GeoPoint ToGeo(Vector2 local)
        {
            return new GeoPoint(OriginLat + local.y / MetresPerDegreeLat, OriginLon + local.x / LonScale);
        }
    }

    /// <summary>Joins relation member ways into closed rings (multipolygons).</summary>
    public static class RingBuilder
    {
        public static List<List<GeoPoint>> Rings(OsmElement relation, string role)
        {
            var open = new List<List<GeoPoint>>();
            var rings = new List<List<GeoPoint>>();
            foreach (OsmMember m in relation.Members)
            {
                if (m.Type != "way" || m.Geometry.Count < 2) continue;
                bool outer = string.IsNullOrEmpty(m.Role) || m.Role == "outer";
                if (role == "outer" ? !outer : m.Role != role) continue;
                var pts = new List<GeoPoint>(m.Geometry);
                if (Same(pts[0], pts[pts.Count - 1]) && pts.Count >= 4) rings.Add(pts);
                else open.Add(pts);
            }

            // Stitch the open pieces end to end.
            while (open.Count > 0)
            {
                List<GeoPoint> ring = open[0];
                open.RemoveAt(0);
                bool grew = true;
                while (grew && !Same(ring[0], ring[ring.Count - 1]))
                {
                    grew = false;
                    for (int k = 0; k < open.Count; k++)
                    {
                        List<GeoPoint> piece = open[k];
                        GeoPoint end = ring[ring.Count - 1];
                        if (Same(end, piece[0])) { ring.AddRange(piece.GetRange(1, piece.Count - 1)); }
                        else if (Same(end, piece[piece.Count - 1])) { piece.Reverse(); ring.AddRange(piece.GetRange(1, piece.Count - 1)); }
                        else if (Same(ring[0], piece[piece.Count - 1])) { ring.InsertRange(0, piece.GetRange(0, piece.Count - 1)); }
                        else if (Same(ring[0], piece[0])) { piece.Reverse(); ring.InsertRange(0, piece.GetRange(0, piece.Count - 1)); }
                        else continue;
                        open.RemoveAt(k);
                        grew = true;
                        break;
                    }
                }
                if (ring.Count >= 4)
                {
                    if (!Same(ring[0], ring[ring.Count - 1])) ring.Add(ring[0]); // close a nearly closed ring
                    rings.Add(ring);
                }
            }
            return rings;
        }

        private static bool Same(GeoPoint a, GeoPoint b) => Math.Abs(a.Lat - b.Lat) < 1e-8 && Math.Abs(a.Lon - b.Lon) < 1e-8;
    }
}
