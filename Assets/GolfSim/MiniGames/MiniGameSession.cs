using System;
using UnityEngine;

namespace GolfSimZA.MiniGames
{
    /// <summary>
    /// The mini game picked on the MINI GAMES tab: which game, who plays (1-8), shots per player
    /// and the game's options. Kept in PlayerPrefs from the home screen to the range.
    /// </summary>
    public static class MiniGameSession
    {
        private const string ActiveKey = "GolfSimZA.MiniGame.Active";
        private const string GameKey = "GolfSimZA.MiniGame.Id";
        private const string PlayersKey = "GolfSimZA.MiniGame.Players";
        private const string ShotsKey = "GolfSimZA.MiniGame.Shots";
        private const string ExploreKey = "GolfSimZA.MiniGame.Explore";
        private const string DistanceKey = "GolfSimZA.MiniGame.Distance";

        public static bool Active => PlayerPrefs.GetInt(ActiveKey, 0) == 1;
        public static MiniGameId Game => (MiniGameId)Mathf.Clamp(PlayerPrefs.GetInt(GameKey, 0), 0, MiniGameCatalog.All.Length - 1);
        public static int Shots => Mathf.Clamp(PlayerPrefs.GetInt(ShotsKey, 5), 1, 30);
        public static bool Explore => PlayerPrefs.GetInt(ExploreKey, 0) == 1;
        /// <summary>Closest to the pin: 0 random, 1 short (60-100 m), 2 middle (100-150 m), 3 long (150-200 m).</summary>
        public static int Distance => Mathf.Clamp(PlayerPrefs.GetInt(DistanceKey, 0), 0, 3);

        public static string[] Players
        {
            get
            {
                string[] names = PlayerPrefs.GetString(PlayersKey, "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                return names.Length > 0 ? names : new[] { "Player 1" };
            }
        }

        public static void Start(MiniGameId game, string[] players, int shots, bool explore, int distance)
        {
            PlayerPrefs.SetInt(ActiveKey, 1);
            PlayerPrefs.SetInt(GameKey, (int)game);
            PlayerPrefs.SetString(PlayersKey, string.Join("|", players));
            PlayerPrefs.SetInt(ShotsKey, shots);
            PlayerPrefs.SetInt(ExploreKey, explore ? 1 : 0);
            PlayerPrefs.SetInt(DistanceKey, distance);
            PlayerPrefs.SetInt("GolfSimZA.MapMode", 0); // never mixed with Map My Bag
            PlayerPrefs.Save();
        }

        /// <summary>Back to normal range / rounds.</summary>
        public static void End()
        {
            if (!Active) return;
            PlayerPrefs.SetInt(ActiveKey, 0);
            PlayerPrefs.Save();
        }

        /// <summary>Last choices on the setup page.</summary>
        public static MiniGameId LastGame => Game;
        public static string[] LastPlayers => PlayerPrefs.GetString(PlayersKey, "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
    }
}
