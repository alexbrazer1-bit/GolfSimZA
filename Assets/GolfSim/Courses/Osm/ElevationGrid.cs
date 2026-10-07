using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GolfSimZA.Courses.Osm
{
    /// <summary>
    /// Ground heights for a course area from the free worldwide elevation tiles (Terrarium PNG
    /// tiles, about 30 m source detail in South Africa). Tiles are downloaded and decoded on the
    /// main thread; sampling is plain maths and can run on a worker thread.
    /// </summary>
    public sealed class ElevationGrid
    {
        public const int Zoom = 15;
        private const int TileSize = 256;

        private readonly Dictionary<long, float[]> tiles = new Dictionary<long, float[]>();
        public int TileCount => tiles.Count;
        public int MissingTiles { get; private set; }

        /// <summary>Downloads every tile covering the area (with 1 tile of margin for smooth edges).</summary>
        public IEnumerator Download(GeoPoint southWest, GeoPoint northEast, Action<string, float> progress, NetResult error)
        {
            TileXY(northEast.Lat, southWest.Lon, out double fx0, out double fy0);
            TileXY(southWest.Lat, northEast.Lon, out double fx1, out double fy1);
            int x0 = (int)Math.Floor(fx0), x1 = (int)Math.Floor(fx1);
            int y0 = (int)Math.Floor(fy0), y1 = (int)Math.Floor(fy1);
            int total = (x1 - x0 + 1) * (y1 - y0 + 1), done = 0;
            for (int ty = y0; ty <= y1; ty++)
                for (int tx = x0; tx <= x1; tx++)
                {
                    progress?.Invoke($"Downloading ground heights ({done + 1}/{total})...", done / (float)total);
                    var result = new NetResult();
                    yield return OsmNet.ElevationTile(Zoom, tx, ty, result);
                    done++;
                    if (!result.Ok)
                    {
                        MissingTiles++;
                        Debug.LogWarning("[GolfSimZA] Elevation tile " + tx + "/" + ty + ": " + result.Error);
                        continue;
                    }
                    float[] heights = Decode(result.Bytes);
                    if (heights != null) tiles[Key(tx, ty)] = heights;
                    else MissingTiles++;
                }
            if (tiles.Count == 0) error.Error = "No ground height data could be downloaded. Check the internet connection.";
        }

        private static float[] Decode(byte[] png)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!tex.LoadImage(png) || tex.width != TileSize || tex.height != TileSize) return null;
                Color32[] px = tex.GetPixels32();
                var h = new float[TileSize * TileSize];
                // GetPixels32 starts at the bottom row; tiles count rows from the top.
                for (int y = 0; y < TileSize; y++)
                    for (int x = 0; x < TileSize; x++)
                    {
                        Color32 c = px[(TileSize - 1 - y) * TileSize + x];
                        h[y * TileSize + x] = c.r * 256f + c.g + c.b / 256f - 32768f;
                    }
                return h;
            }
            finally
            {
                UnityEngine.Object.Destroy(tex);
            }
        }

        /// <summary>Height (metres above sea level) at a point, bilinear between tile pixels. Thread safe.</summary>
        public float HeightAt(double lat, double lon, float fallback)
        {
            TileXY(lat, lon, out double fx, out double fy);
            double px = fx * TileSize - 0.5, py = fy * TileSize - 0.5;
            long ix = (long)Math.Floor(px), iy = (long)Math.Floor(py);
            float tx = (float)(px - ix), ty = (float)(py - iy);
            float h00 = Pixel(ix, iy, fallback), h10 = Pixel(ix + 1, iy, fallback);
            float h01 = Pixel(ix, iy + 1, fallback), h11 = Pixel(ix + 1, iy + 1, fallback);
            return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), ty);
        }

        private float Pixel(long gx, long gy, float fallback)
        {
            long tx = gx >= 0 ? gx / TileSize : (gx - TileSize + 1) / TileSize;
            long ty = gy >= 0 ? gy / TileSize : (gy - TileSize + 1) / TileSize;
            if (!tiles.TryGetValue(Key((int)tx, (int)ty), out float[] h)) return fallback;
            int x = (int)(gx - tx * TileSize), y = (int)(gy - ty * TileSize);
            float v = h[y * TileSize + x];
            // Ocean / no-data pixels come out very low.
            return v < -500f ? fallback : v;
        }

        private static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;

        private static void TileXY(double lat, double lon, out double x, out double y)
        {
            double n = 1 << Zoom;
            double latRad = lat * Math.PI / 180.0;
            x = (lon + 180.0) / 360.0 * n;
            y = (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n;
        }

        /// <summary>The average of all downloaded heights (fallback for gaps).</summary>
        public float AverageHeight()
        {
            double sum = 0;
            long count = 0;
            foreach (float[] t in tiles.Values)
                for (int i = 0; i < t.Length; i += 97)
                    if (t[i] > -500f) { sum += t[i]; count++; }
            return count > 0 ? (float)(sum / count) : 0f;
        }
    }
}
