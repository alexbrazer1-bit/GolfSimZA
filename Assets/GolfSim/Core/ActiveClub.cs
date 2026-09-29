using System;
using GolfSimZA.Players;
using UnityEngine;

namespace GolfSimZA.Core
{
    /// <summary>
    /// The club the golfer is currently hitting. The Garmin R10 cannot identify the
    /// club, so every real shot is stamped with this value (like other simulators,
    /// the player chooses the club on screen). Resolution order:
    /// 1. Map My Bag session club, 2. club picked in the distance advisor,
    /// 3. club picked on the club bar / number keys, 4. Driver.
    /// </summary>
    public static class ActiveClub
    {
        public const string AdvisorClubKey = "GolfSimZA.RoundClubName";
        private const string MapModeKey = "GolfSimZA.MapMode";
        private const string MapPlayerKey = "GolfSimZA.MapPlayer";
        private const string MapClubKey = "GolfSimZA.MapClubIndex";

        private static string selectedName = "Driver";
        private static string cachedMapKey;
        private static string cachedMapName;
        private static float cachedMapTime = -10f;

        /// <summary>Legacy 8-slot club bar used by the HUDs and keyboard test mode.</summary>
        public static readonly string[] SlotNames = { "Driver", "3 Wood", "5 Iron", "7 Iron", "9 Iron", "Pitching Wedge", "Sand Wedge", "Putter" };
        public static readonly string[] SlotShortNames = { "Driver", "3W", "5i", "7i", "9i", "PW", "SW", "Putter" };

        public static event Action<string> Changed;

        /// <summary>Called by the club bar, number keys or advisor.</summary>
        public static void Select(string clubName)
        {
            if (string.IsNullOrWhiteSpace(clubName)) return;
            selectedName = clubName.Trim();
            // An explicit club-bar choice replaces an older advisor recommendation.
            PlayerPrefs.DeleteKey(AdvisorClubKey);
            Changed?.Invoke(selectedName);
        }

        public static void SelectSlot(int slot)
        {
            int index = Mathf.Clamp(slot - 1, 0, SlotNames.Length - 1);
            Select(SlotNames[index]);
        }

        public static string Resolve()
        {
            if (PlayerPrefs.GetInt(MapModeKey, 0) == 1)
            {
                int index = Mathf.Clamp(PlayerPrefs.GetInt(MapClubKey, 0), 0, GolfBagProfile.ClubCount - 1);
                string player = PlayerPrefs.GetString(MapPlayerKey, "Player 1");
                string key = player + "|" + index;
                // The HUD asks every frame; re-read the bag at most once a second.
                if (key != cachedMapKey || Time.unscaledTime - cachedMapTime > 1f)
                {
                    GolfBagProfile profile = GolfBagProfile.Load(player);
                    cachedMapName = profile.ClubNames[index];
                    cachedMapKey = key;
                    cachedMapTime = Time.unscaledTime;
                }
                if (!string.IsNullOrWhiteSpace(cachedMapName)) return cachedMapName;
            }

            string advisor = PlayerPrefs.GetString(AdvisorClubKey, "");
            if (!string.IsNullOrWhiteSpace(advisor)) return advisor;

            return string.IsNullOrWhiteSpace(selectedName) ? "Driver" : selectedName;
        }

        // ---------------------------------------------------------------- Player bag

        public struct BagClub
        {
            public string Name;
            public string Short;
            public float Loft;
        }

        private static string player = "";
        private static string cachedBagPlayer;
        private static float cachedBagTime = -10f;
        private static readonly System.Collections.Generic.List<BagClub> cachedBag = new System.Collections.Generic.List<BagClub>();

        /// <summary>Player whose bag the club bar shows (the player about to hit).</summary>
        public static string Player
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(player)) return player;
                string names = GolfSimZA.Courses.CourseSession.PlayerNames ?? "";
                string[] split = names.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                return split.Length > 0 && !string.IsNullOrWhiteSpace(split[0]) ? split[0].Trim() : "Player 1";
            }
            set
            {
                string next = value ?? "";
                if (next == player) return;
                player = next;
                cachedBagPlayer = null;
            }
        }

        /// <summary>Clubs in the current player's bag (lowest loft first, putter always last).</summary>
        public static System.Collections.Generic.IReadOnlyList<BagClub> Bag
        {
            get
            {
                string who = Player;
                if (cachedBagPlayer != who || Time.unscaledTime - cachedBagTime > 2f)
                {
                    cachedBag.Clear();
                    GolfBagProfile profile = GolfBagProfile.Load(who);
                    for (int i = 0; i < GolfBagProfile.ClubCount; i++)
                    {
                        if (!profile.InBag[i] || string.IsNullOrWhiteSpace(profile.ClubNames[i])) continue;
                        if (IsPutter(profile.ClubNames[i])) continue;
                        cachedBag.Add(new BagClub { Name = profile.ClubNames[i], Short = ShortName(profile.ClubNames[i]), Loft = profile.Lofts[i] });
                    }
                    cachedBag.Sort((a, b) => a.Loft.CompareTo(b.Loft));
                    // A putter is always available so every hole can be finished.
                    int putterIndex = profile.FindClubIndex("Putter");
                    cachedBag.Add(new BagClub { Name = "Putter", Short = "PT", Loft = putterIndex >= 0 ? profile.Lofts[putterIndex] : 3f });
                    if (cachedBag.Count == 1)
                    {
                        // Empty bag: offer the standard clubs so the player can still hit.
                        for (int i = 0; i < SlotNames.Length - 1; i++)
                            cachedBag.Insert(cachedBag.Count - 1, new BagClub { Name = SlotNames[i], Short = SlotShortNames[i], Loft = 10f + i * 6f });
                    }
                    cachedBagPlayer = who;
                    cachedBagTime = Time.unscaledTime;
                }
                return cachedBag;
            }
        }

        public static int BagIndexOf(string clubName)
        {
            var bag = Bag;
            for (int i = 0; i < bag.Count; i++)
                if (string.Equals(bag[i].Name, clubName, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        public static void SelectBagIndex(int index)
        {
            var bag = Bag;
            if (bag.Count == 0) return;
            index = ((index % bag.Count) + bag.Count) % bag.Count;
            Select(bag[index].Name);
        }

        /// <summary>Moves to the previous (-1) or next (+1) club in the bag.</summary>
        public static void Cycle(int direction)
        {
            int current = BagIndexOf(Resolve());
            SelectBagIndex(current < 0 ? 0 : current + direction);
        }

        public static float LoftOf(string clubName)
        {
            foreach (BagClub club in Bag)
                if (string.Equals(club.Name, clubName, StringComparison.OrdinalIgnoreCase)) return club.Loft;
            return 30f;
        }

        public static string ShortName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            string n = name.Trim();
            string lower = n.ToLowerInvariant();
            if (lower == "driver") return "DR";
            if (lower == "putter") return "PT";
            if (lower == "pitching wedge") return "PW";
            if (lower == "gap wedge") return "GW";
            if (lower == "approach wedge") return "AW";
            if (lower == "sand wedge") return "SW";
            if (lower == "lob wedge") return "LW";
            if (lower == "driving iron") return "DI";
            if (lower == "utility iron") return "UI";
            if (lower == "bump & run") return "B&R";
            if (lower == "chipper") return "CH";
            string[] parts = n.Split(' ');
            if (parts.Length == 2)
            {
                string kind = parts[1].ToLowerInvariant();
                if (kind == "wood") return parts[0] + "W";
                if (kind == "hybrid") return parts[0] + "H";
                if (kind == "iron") return parts[0] + "i";
            }
            return n.Length <= 4 ? n : n.Substring(0, 4);
        }

        /// <summary>1-8 slot on the legacy club bar for highlighting (0 when not on the bar).</summary>
        public static int SlotFor(string clubName)
        {
            if (string.IsNullOrWhiteSpace(clubName)) return 0;
            for (int i = 0; i < SlotNames.Length; i++)
                if (string.Equals(SlotNames[i], clubName, StringComparison.OrdinalIgnoreCase)) return i + 1;
            return 0;
        }

        /// <summary>
        /// Expected carry of a club for the current player: their mapped carry (MAP MY BAG) when
        /// there is one, otherwise a typical amateur carry from the loft. 0 for the putter.
        /// </summary>
        public static float ExpectedCarry(string clubName)
        {
            if (string.IsNullOrWhiteSpace(clubName) || IsPutter(clubName)) return 0f;
            GolfBagProfile profile = GolfBagProfile.Load(Player);
            int index = profile.FindClubIndex(clubName);
            if (index >= 0 && profile.CarryMeters[index] > 1f) return profile.CarryMeters[index];
            float loft = index >= 0 ? profile.Lofts[index] : LoftOf(clubName);
            return Mathf.Clamp(215f - (loft - 10f) * 3.1f, 35f, 230f);
        }

        public static bool IsPutter(string clubName)
        {
            return !string.IsNullOrEmpty(clubName) && clubName.IndexOf("putter", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
