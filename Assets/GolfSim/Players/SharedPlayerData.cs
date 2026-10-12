using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GolfSimZA.Core;
using UnityEngine;

namespace GolfSimZA.Players
{
    /// <summary>One player's golf bag as kept in the shared file.</summary>
    [Serializable]
    public sealed class SharedBag
    {
        public string player = "";
        /// <summary>When this bag was last changed (UTC ticks; 0 = never changed since sharing started).</summary>
        public long modifiedUtc;
        public string modifiedOn = "";
        public int setupVersion;
        public int mappedClubs;
        public string[] names = new string[0];
        public float[] lofts = new float[0];
        public float[] carry = new float[0];
        public float[] total = new float[0];
        public bool[] inBag = new bool[0];
    }

    /// <summary>The shared file: the player list and every player's bag.</summary>
    [Serializable]
    public sealed class SharedPlayersFile
    {
        public int version = 1;
        public long rosterModifiedUtc;
        public string rosterModifiedOn = "";
        public List<PlayerProfile> players = new List<PlayerProfile>();
        public List<SharedBag> bags = new List<SharedBag>();
    }

    /// <summary>
    /// Keeps the players and their mapped golf bags the same on every PC (main PC and simulator PC).
    /// They are kept in players-and-bags.json in a shared network folder (Settings → UPDATES → SHARED
    /// PLAYERS &amp; BAGS FOLDER, default \\Desktop-Nofear\Shared\GolfSimZA-Data). Each PC still keeps its own
    /// copy, so the game works the same when the shared folder cannot be reached; it catches up when it can.
    /// Newest change wins: per bag, and for the player list. Bags that were never changed since sharing
    /// started (the first time each PC joins) keep the one with the most mapped clubs, so a bag mapped on
    /// one PC is never replaced by an unmapped default bag from the other.
    /// </summary>
    public sealed class SharedPlayerData : MonoBehaviour
    {
        public const string FileName = "players-and-bags.json";
        private const float PollSeconds = 30f;
        internal const string RosterModifiedKey = "GolfSimZA.Players.ModifiedUtc";

        /// <summary>Last sync result, shown in Settings → UPDATES.</summary>
        public static string Status { get; private set; } = "Not synced yet.";
        public static bool StatusIsError { get; private set; }
        /// <summary>Raised on the main thread after players or bags were changed by another PC.</summary>
        public static event Action Changed;
        /// <summary>True while shared data is being written into this PC (local saves are not counted as changes then).</summary>
        internal static bool Applying { get; private set; }

        private sealed class ReadResult
        {
            public string Folder;
            public bool Reachable;
            public bool Unchanged;
            public SharedPlayersFile Data;
            public string Json;
            public DateTime FileTimeUtc;
            public string Error;
        }

        private static SharedPlayerData instance;
        private static Task<ReadResult> reading;
        private static Task<string> writing;
        private static bool syncWanted = true;
        private static DateTime lastFileTimeUtc;
        private static string lastFolder;
        private float nextPoll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject("GolfSimZA Shared Players");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SharedPlayerData>();
        }

        /// <summary>The shared folder in use (setting, or the default network folder).</summary>
        public static string Folder
        {
            get
            {
                string f = AppSettings.Current.sharedDataFolder;
                return string.IsNullOrWhiteSpace(f) ? AppSettings.DefaultSharedDataFolder : f.Trim().Trim('"');
            }
        }

        /// <summary>Something changed on this PC: merge it into the shared file soon.</summary>
        public static void MarkChanged()
        {
            if (Applying) return;
            syncWanted = true;
        }

        /// <summary>Sync now (e.g. after the folder setting changed).</summary>
        public static void SyncNow()
        {
            lastFileTimeUtc = default;
            syncWanted = true;
        }

        private void Update()
        {
            if (reading != null && reading.IsCompleted)
            {
                ReadResult r = reading.IsFaulted ? new ReadResult { Folder = lastFolder, Error = reading.Exception?.GetBaseException().Message } : reading.Result;
                reading = null;
                try { Apply(r); }
                catch (Exception ex) { SetStatus("Players & bags not synced: " + ex.Message, true); Debug.LogWarning("[GolfSimZA] Shared players: " + ex); }
            }

            if (writing != null && writing.IsCompleted)
            {
                string error = writing.IsFaulted ? writing.Exception?.GetBaseException().Message : writing.Result;
                writing = null;
                if (!string.IsNullOrEmpty(error)) SetStatus("Could not save players & bags to " + lastFolder + ": " + error, true);
            }

            if (reading != null || writing != null) return;
            // Do not touch the network in the middle of a shot.
            if (GolfSimZA.Physics.BallFlightSimulator.AnyInFlight) return;
            if (syncWanted || Time.unscaledTime >= nextPoll)
            {
                bool force = syncWanted;
                syncWanted = false;
                nextPoll = Time.unscaledTime + PollSeconds;
                StartRead(force);
            }
        }

        private static void StartRead(bool force)
        {
            string folder = Folder;
            if (folder != lastFolder) lastFileTimeUtc = default;
            lastFolder = folder;
            DateTime known = force ? default : lastFileTimeUtc;
            reading = Task.Run(() => Read(folder, known));
        }

        /// <summary>Background thread: reads the shared file (no Unity calls here).</summary>
        private static ReadResult Read(string folder, DateTime knownFileTimeUtc)
        {
            var r = new ReadResult { Folder = folder };
            try
            {
                if (!Directory.Exists(folder))
                {
                    // The shared folder lives on this PC (same name as this PC): create it.
                    if (IsOnThisPc(folder)) Directory.CreateDirectory(folder);
                    else
                    {
                        string parent = Path.GetDirectoryName(folder.TrimEnd('\\', '/'));
                        if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent)) { r.Error = "folder not reachable"; return r; }
                        Directory.CreateDirectory(folder);
                    }
                }
                r.Reachable = true;
                string file = Path.Combine(folder, FileName);
                if (!File.Exists(file)) return r;
                r.FileTimeUtc = File.GetLastWriteTimeUtc(file);
                if (knownFileTimeUtc != default && r.FileTimeUtc == knownFileTimeUtc) { r.Unchanged = true; return r; }
                r.Json = File.ReadAllText(file);
                r.Data = JsonUtility.FromJson<SharedPlayersFile>(r.Json);
            }
            catch (Exception ex)
            {
                r.Error = ex.Message;
                if (r.Json != null) r.Reachable = true;
            }
            return r;
        }

        private static bool IsOnThisPc(string folder)
        {
            if (!folder.StartsWith(@"\\")) return true;
            string host = folder.TrimStart('\\').Split('\\', '/')[0];
            return string.Equals(host, Environment.MachineName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Main thread: merges the shared file with this PC, updates this PC, writes the file back if needed.</summary>
        private static void Apply(ReadResult r)
        {
            if (r == null) return;
            if (!r.Reachable)
            {
                SetStatus($"Shared folder {r.Folder} not reachable ({r.Error ?? "unknown"}) - using the players & bags on this PC.", true);
                return;
            }
            if (r.Unchanged) return;
            if (r.Error != null && r.Data == null)
            {
                // A broken file is never overwritten blindly: keep this PC's data and report it.
                SetStatus($"Could not read {FileName} in {r.Folder}: {r.Error}", true);
                return;
            }

            SharedPlayersFile shared = r.Data;
            SharedPlayersFile merged = Merge(Snapshot(shared), shared, out bool localChanged);
            if (localChanged) WriteLocal(merged);

            string mergedJson = JsonUtility.ToJson(merged, true);
            bool fileNeedsWrite = shared == null || JsonUtility.ToJson(shared, true) != mergedJson;
            if (fileNeedsWrite)
            {
                string folder = r.Folder;
                writing = Task.Run(() => Write(folder, mergedJson));
                lastFileTimeUtc = default;   // re-read our own write next poll (cheap) so the time is known
            }
            else lastFileTimeUtc = r.FileTimeUtc;

            SetStatus($"Players & bags synced with {r.Folder} at {DateTime.Now:HH:mm}" + (localChanged ? " (updated from the other PC)." : "."), false);
            if (localChanged)
            {
                Debug.Log("[GolfSimZA] Players & bags updated from " + r.Folder);
                Changed?.Invoke();
            }
        }

        /// <summary>Background thread: writes the file safely (temp file, then swap).</summary>
        private static string Write(string folder, string json)
        {
            try
            {
                Directory.CreateDirectory(folder);
                string file = Path.Combine(folder, FileName);
                string temp = Path.Combine(folder, FileName + "." + Environment.MachineName + ".tmp");
                File.WriteAllText(temp, json);
                if (File.Exists(file)) File.Replace(temp, file, null);
                else File.Move(temp, file);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ------------------------------------------------------------------ this PC

        /// <summary>This PC's players and bags (bags for every player here or in the shared file).</summary>
        private static SharedPlayersFile Snapshot(SharedPlayersFile shared)
        {
            var local = new SharedPlayersFile
            {
                rosterModifiedUtc = ReadTicks(RosterModifiedKey),
                rosterModifiedOn = Environment.MachineName
            };
            foreach (PlayerProfile p in PlayerRoster.Current.players) local.players.Add(Clone(p));

            var names = new List<string>();
            foreach (PlayerProfile p in local.players) AddName(names, p.name);
            if (shared != null)
            {
                if (shared.players != null) foreach (PlayerProfile p in shared.players) AddName(names, p.name);
                if (shared.bags != null) foreach (SharedBag b in shared.bags) AddName(names, b.player);
            }
            Applying = true;   // loading a bag may run its one-time setup; that is not a change by the golfer
            try
            {
                foreach (string name in names)
                    local.bags.Add(GolfBagProfile.Load(name).ToShared(name));
            }
            finally { Applying = false; }
            return local;
        }

        private static void WriteLocal(SharedPlayersFile merged)
        {
            Applying = true;
            try
            {
                foreach (SharedBag bag in merged.bags)
                {
                    GolfBagProfile current = GolfBagProfile.Load(bag.player);
                    if (!current.SameAs(bag)) GolfBagProfile.FromShared(bag).Store(bag.player, bag.modifiedUtc, bag.setupVersion);
                    else GolfBagProfile.StoreModified(bag.player, bag.modifiedUtc);
                }
                PlayerRoster.ReplaceFromShared(merged.players);
                WriteTicks(RosterModifiedKey, merged.rosterModifiedUtc);
                PlayerPrefs.Save();
            }
            finally { Applying = false; }
        }

        // ------------------------------------------------------------------ merge

        private static SharedPlayersFile Merge(SharedPlayersFile local, SharedPlayersFile shared, out bool localChanged)
        {
            localChanged = false;
            if (shared == null) return local;
            if (shared.players == null) shared.players = new List<PlayerProfile>();
            if (shared.bags == null) shared.bags = new List<SharedBag>();

            var merged = new SharedPlayersFile();

            // Player list: the newer list wins; if neither was changed since sharing started, join them.
            if (local.rosterModifiedUtc > shared.rosterModifiedUtc || shared.players.Count == 0)
            {
                merged.rosterModifiedUtc = local.rosterModifiedUtc;
                merged.rosterModifiedOn = local.rosterModifiedOn;
                foreach (PlayerProfile p in local.players) merged.players.Add(Clone(p));
            }
            else
            {
                merged.rosterModifiedUtc = shared.rosterModifiedUtc;
                merged.rosterModifiedOn = shared.rosterModifiedOn;
                foreach (PlayerProfile p in shared.players) merged.players.Add(Clone(p));
                if (local.rosterModifiedUtc == shared.rosterModifiedUtc)
                    foreach (PlayerProfile p in local.players)
                        if (FindPlayer(merged.players, p.name) == null) merged.players.Add(Clone(p));
            }
            if (!SamePlayers(merged.players, local.players)) localChanged = true;

            // Bags: newest change wins; never-changed bags keep the one with the most mapped clubs.
            foreach (SharedBag mine in local.bags)
            {
                SharedBag theirs = FindBag(shared.bags, mine.player);
                SharedBag pick = mine;
                if (theirs != null && IsValid(theirs))
                {
                    if (theirs.modifiedUtc > mine.modifiedUtc) pick = theirs;
                    else if (theirs.modifiedUtc == mine.modifiedUtc && theirs.mappedClubs >= mine.mappedClubs) pick = theirs;
                }
                if (pick != mine && JsonUtility.ToJson(pick) != JsonUtility.ToJson(mine)) localChanged = true;
                merged.bags.Add(pick);
            }
            return merged;
        }

        private static bool IsValid(SharedBag b) =>
            b.names != null && b.names.Length == GolfBagProfile.ClubCount && b.lofts != null && b.lofts.Length == GolfBagProfile.ClubCount
            && b.carry != null && b.carry.Length == GolfBagProfile.ClubCount && b.total != null && b.total.Length == GolfBagProfile.ClubCount
            && b.inBag != null && b.inBag.Length == GolfBagProfile.ClubCount;

        private static bool SamePlayers(List<PlayerProfile> a, List<PlayerProfile> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (JsonUtility.ToJson(a[i]) != JsonUtility.ToJson(b[i])) return false;
            return true;
        }

        private static PlayerProfile FindPlayer(List<PlayerProfile> list, string name)
        {
            foreach (PlayerProfile p in list) if (string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }

        private static SharedBag FindBag(List<SharedBag> list, string name)
        {
            foreach (SharedBag b in list) if (b != null && string.Equals(b.player, name, StringComparison.OrdinalIgnoreCase)) return b;
            return null;
        }

        private static void AddName(List<string> names, string name)
        {
            name = PlayerRoster.Clean(name);
            if (string.IsNullOrEmpty(name)) return;
            foreach (string n in names) if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) return;
            names.Add(name);
        }

        private static PlayerProfile Clone(PlayerProfile p) => JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(p));

        internal static long ReadTicks(string key) => long.TryParse(PlayerPrefs.GetString(key, "0"), out long t) ? t : 0L;
        internal static void WriteTicks(string key, long ticks) => PlayerPrefs.SetString(key, ticks.ToString());

        private static void SetStatus(string text, bool error)
        {
            if (text != Status) Debug.Log("[GolfSimZA] " + text);
            Status = text;
            StatusIsError = error;
        }
    }
}
