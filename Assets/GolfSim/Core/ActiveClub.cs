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

        /// <summary>1-8 slot on the legacy club bar for highlighting (0 when not on the bar).</summary>
        public static int SlotFor(string clubName)
        {
            if (string.IsNullOrWhiteSpace(clubName)) return 0;
            for (int i = 0; i < SlotNames.Length; i++)
                if (string.Equals(SlotNames[i], clubName, StringComparison.OrdinalIgnoreCase)) return i + 1;
            return 0;
        }

        public static bool IsPutter(string clubName)
        {
            return !string.IsNullOrEmpty(clubName) && clubName.IndexOf("putter", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
