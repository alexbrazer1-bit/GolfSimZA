using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace GolfSimZA.Courses.Osm
{
    /// <summary>Result of a download (text or bytes, or an error message).</summary>
    public sealed class NetResult
    {
        public string Text;
        public byte[] Bytes;
        public string Error;
        public bool FromCache;
        public bool Ok => Error == null;
    }

    /// <summary>
    /// Internet access for the course builder: OpenStreetMap's Overpass servers (course layouts)
    /// and the free worldwide elevation tiles (ground heights). Answers are kept on disk
    /// (persistentDataPath/OsmCache) so a course can be rebuilt offline and the public servers
    /// are not asked twice.
    /// </summary>
    public static class OsmNet
    {
        /// <summary>Public Overpass servers, tried in turn when one is busy.</summary>
        public static readonly string[] OverpassServers =
        {
            "https://overpass-api.de/api/interpreter",
            "https://overpass.kumi.systems/api/interpreter",
            "https://overpass.private.coffee/api/interpreter",
            "https://maps.mail.ru/osm/tools/overpass/api/interpreter"
        };

        /// <summary>Terrarium elevation tiles (Mapzen / AWS Open Data: SRTM, GMTED, ETOPO and others).</summary>
        public const string ElevationTileUrl = "https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{0}/{1}/{2}.png";

        public static string CacheFolder => Path.Combine(Application.persistentDataPath, "OsmCache");

        private static string UserAgent => "GolfSimZA/" + GolfSimZA.Core.GolfSimVersion.Version + " (home golf simulator; course builder)";

        /// <summary>Runs an Overpass query. maxAgeHours: use a cached answer younger than this (0 = always ask).</summary>
        public static IEnumerator Overpass(string query, double maxAgeHours, NetResult result, Action<string> status, int requestTimeoutSeconds = 200)
        {
            string cacheFile = Path.Combine(CacheFolder, "overpass-" + Hash(query) + ".json");
            if (maxAgeHours > 0 && File.Exists(cacheFile) && (DateTime.UtcNow - File.GetLastWriteTimeUtc(cacheFile)).TotalHours < maxAgeHours)
            {
                result.Text = File.ReadAllText(cacheFile);
                result.FromCache = true;
                yield break;
            }

            string lastError = "no server answered";
            for (int round = 0; round < 2; round++)
            {
                foreach (string server in OverpassServers)
                {
                    status?.Invoke("Asking OpenStreetMap (" + new Uri(server).Host + ")...");
                    WWWForm form = new WWWForm();
                    form.AddField("data", query);
                    using (UnityWebRequest request = UnityWebRequest.Post(server, form))
                    {
                        request.timeout = requestTimeoutSeconds;
                        TrySetUserAgent(request);
                        yield return request.SendWebRequest();
                        if (request.result == UnityWebRequest.Result.Success)
                        {
                            string text = request.downloadHandler.text;
                            if (!string.IsNullOrEmpty(text) && text.TrimStart().StartsWith("{", StringComparison.Ordinal) && text.Contains("\"elements\""))
                            {
                                // Overpass reports timeouts inside a 200 answer ("remark": "runtime error ...").
                                if (text.Contains("\"remark\"") && text.Contains("runtime error"))
                                {
                                    lastError = new Uri(server).Host + ": the server ran out of time";
                                    continue;
                                }
                                result.Text = text;
                                try
                                {
                                    Directory.CreateDirectory(CacheFolder);
                                    File.WriteAllText(cacheFile, text);
                                }
                                catch (Exception ex) { Debug.LogWarning("[GolfSimZA] Could not cache the OpenStreetMap answer: " + ex.Message); }
                                yield break;
                            }
                            lastError = new Uri(server).Host + ": unexpected answer";
                        }
                        else
                        {
                            lastError = new Uri(server).Host + ": " + (request.responseCode > 0 ? "HTTP " + request.responseCode : request.error);
                        }
                    }
                    Debug.LogWarning("[GolfSimZA] OpenStreetMap server failed: " + lastError);
                }
                if (round == 0)
                {
                    status?.Invoke("OpenStreetMap servers are busy - trying again in 20 seconds...");
                    yield return new WaitForSecondsRealtime(20f);
                }
            }

            // Offline or every server busy: an older cached answer is better than nothing.
            if (File.Exists(cacheFile))
            {
                result.Text = File.ReadAllText(cacheFile);
                result.FromCache = true;
                Debug.LogWarning("[GolfSimZA] OpenStreetMap unreachable (" + lastError + "); using the saved copy from " + File.GetLastWriteTime(cacheFile));
                yield break;
            }
            result.Error = "OpenStreetMap could not be reached (" + lastError + "). Check the internet connection and try again.";
        }

        /// <summary>Downloads an elevation tile (PNG bytes), cached on disk for good.</summary>
        public static IEnumerator ElevationTile(int z, int x, int y, NetResult result)
        {
            string cacheFile = Path.Combine(CacheFolder, "terrain", $"{z}-{x}-{y}.png");
            if (File.Exists(cacheFile))
            {
                result.Bytes = File.ReadAllBytes(cacheFile);
                result.FromCache = true;
                yield break;
            }
            string url = string.Format(ElevationTileUrl, z, x, y);
            string lastError = null;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
                    request.timeout = 60;
                    TrySetUserAgent(request);
                    yield return request.SendWebRequest();
                    if (request.result == UnityWebRequest.Result.Success && request.downloadHandler.data != null && request.downloadHandler.data.Length > 100)
                    {
                        result.Bytes = request.downloadHandler.data;
                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(cacheFile));
                            File.WriteAllBytes(cacheFile, result.Bytes);
                        }
                        catch (Exception ex) { Debug.LogWarning("[GolfSimZA] Could not cache an elevation tile: " + ex.Message); }
                        yield break;
                    }
                    lastError = request.responseCode > 0 ? "HTTP " + request.responseCode : request.error;
                }
                yield return new WaitForSecondsRealtime(2f + attempt * 3f);
            }
            result.Error = "Ground height data could not be downloaded (" + lastError + ").";
        }

        private static void TrySetUserAgent(UnityWebRequest request)
        {
            try { request.SetRequestHeader("User-Agent", UserAgent); }
            catch (Exception) { /* not allowed on some platforms */ }
        }

        private static string Hash(string text)
        {
            using (SHA1 sha = SHA1.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                var sb = new StringBuilder(h.Length * 2);
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
