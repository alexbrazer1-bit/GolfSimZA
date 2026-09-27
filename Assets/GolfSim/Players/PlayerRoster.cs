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
    }

    /// <summary>
    /// Saved list of golfers on this simulator (players.json in persistentDataPath).
    /// The selected players become the round's players. Golf bags stay per player name.
    /// </summary>
    [Serializable]
    public sealed class PlayerRoster
    {
        public const int MaxPlayersInRound = 4;
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
