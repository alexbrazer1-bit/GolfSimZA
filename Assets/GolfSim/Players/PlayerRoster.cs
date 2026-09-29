using System;
using System.Collections.Generic;
using System.IO;
using GolfSimZA.Courses;
using UnityEngine;

namespace GolfSimZA.Players
{
    [Serializable]
    public sealed class PlayerProfile
    {
        public string name = "Player";
        public bool leftHanded;
        public bool selected = true;   // plays in the next round
        /// <summary>Colour / team index into PlayerRoster.Colors (-1 = not chosen yet).</summary>
        public int color = -1;
        /// <summary>Tee box for this player ("" = the match tees chosen in Round Settings).</summary>
        public string tee = "";
    }

    /// <summary>
    /// Saved list of golfers on this simulator (players.json in persistentDataPath).
    /// The selected players become the round's players. Golf bags stay per player name.
    /// </summary>
    [Serializable]
    public sealed class PlayerRoster
    {
        public const int MaxPlayersInRound = GolfSimZA.Courses.CourseSession.MaxPlayers;

        public static readonly string[] ColorNames = { "RED", "BLUE", "GREEN", "GOLD", "PURPLE", "ORANGE", "TEAL", "PINK" };
        public static readonly Color[] Colors =
        {
            new Color(0.90f, 0.20f, 0.20f), new Color(0.20f, 0.50f, 0.95f), new Color(0.15f, 0.72f, 0.38f), new Color(0.96f, 0.72f, 0.20f),
            new Color(0.58f, 0.34f, 0.88f), new Color(0.98f, 0.50f, 0.12f), new Color(0.10f, 0.72f, 0.72f), new Color(0.93f, 0.38f, 0.66f)
        };

        /// <summary>The player's colour (their chosen one, or one by position in the roster).</summary>
        public static Color ColorFor(string name)
        {
            PlayerRoster r = Current;
            PlayerProfile p = r.Find(name);
            int index = p != null ? r.ColorIndex(p) : Mathf.Abs((name ?? "").GetHashCode()) % Colors.Length;
            return Colors[index];
        }

        public int ColorIndex(PlayerProfile p)
        {
            if (p.color >= 0 && p.color < Colors.Length) return p.color;
            int i = players.IndexOf(p);
            return (i < 0 ? 0 : i) % Colors.Length;
        }

        /// <summary>First colour no other selected player uses.</summary>
        public int FreeColor(PlayerProfile except = null)
        {
            var used = new HashSet<int>();
            foreach (PlayerProfile p in players)
                if (p != except && p.selected) used.Add(ColorIndex(p));
            for (int i = 0; i < Colors.Length; i++) if (!used.Contains(i)) return i;
            return 0;
        }

        /// <summary>This player's tee on the current course, or null to use the match tees.</summary>
        public static string TeeFor(string name)
        {
            PlayerProfile p = Current.Find(name);
            return p != null && !string.IsNullOrEmpty(p.tee) ? p.tee : null;
        }
        public List<PlayerProfile> players = new List<PlayerProfile>();

        private static PlayerRoster current;
        public static string FilePath => Path.Combine(Application.persistentDataPath, "players.json");

        public static PlayerRoster Current
        {
            get
            {
                if (current == null) current = Load();
                return current;
            }
        }

        private static PlayerRoster Load()
        {
            PlayerRoster roster = null;
            try
            {
                if (File.Exists(FilePath)) roster = JsonUtility.FromJson<PlayerRoster>(File.ReadAllText(FilePath));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GolfSimZA] Could not read players: " + ex.Message);
            }

            if (roster != null && roster.players != null)
                foreach (PlayerProfile p in roster.players)
                    if (p.tee == null) p.tee = "";

            if (roster == null || roster.players == null || roster.players.Count == 0)
            {
                // First run: start from the names used in the last round.
                roster = new PlayerRoster();
                foreach (string name in (CourseSession.PlayerNames ?? "Player 1").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                    roster.players.Add(new PlayerProfile { name = name.Trim(), selected = true });
                if (roster.players.Count == 0) roster.players.Add(new PlayerProfile { name = "Player 1" });
            }
            return roster;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonUtility.ToJson(this, true));
            }
            catch (Exception ex)
            {
                Debug.LogError("[GolfSimZA] Could not save players: " + ex.Message);
            }
            ApplyToSession();
        }

        public List<PlayerProfile> Selected
        {
            get
            {
                var list = new List<PlayerProfile>();
                foreach (PlayerProfile p in players)
                    if (p.selected && list.Count < MaxPlayersInRound) list.Add(p);
                if (list.Count == 0 && players.Count > 0) list.Add(players[0]);
                return list;
            }
        }

        /// <summary>Makes the selected players the players of the next round.</summary>
        public void ApplyToSession()
        {
            var names = new List<string>();
            foreach (PlayerProfile p in Selected) names.Add(Clean(p.name));
            if (names.Count == 0) names.Add("Player 1");
            CourseSession.SetPlayers(names.ToArray());
        }

        public PlayerProfile Add(string name)
        {
            name = Clean(name);
            if (string.IsNullOrEmpty(name)) name = "Player " + (players.Count + 1);
            string unique = name;
            int n = 2;
            while (Find(unique) != null) unique = name + " " + n++;
            var profile = new PlayerProfile { name = unique, selected = Selected.Count < MaxPlayersInRound };
            profile.color = FreeColor();
            players.Add(profile);
            Save();
            return profile;
        }

        public void Remove(PlayerProfile profile)
        {
            if (players.Count <= 1) return;
            players.Remove(profile);
            Save();
        }

        public PlayerProfile Find(string name)
        {
            foreach (PlayerProfile p in players)
                if (string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }

        public bool IsLeftHanded(string name)
        {
            PlayerProfile p = Find(name);
            return p != null && p.leftHanded;
        }

        public static string Clean(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            name = name.Replace("|", "").Trim();
            return name.Length > 24 ? name.Substring(0, 24) : name;
        }
    }
}
