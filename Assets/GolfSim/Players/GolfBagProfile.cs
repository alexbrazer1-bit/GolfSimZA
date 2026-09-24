using System;
using UnityEngine;

namespace GolfSimZA.Players
{
    [Serializable]
    public sealed class GolfBagProfile
    {
        public const int ClubCount = 36;
        public const int MaxBagClubs = 14;

        public static readonly string[] DefaultClubNames =
        {
            "Driver", "2 Wood", "3 Wood", "4 Wood", "5 Wood", "7 Wood", "9 Wood",
            "2 Hybrid", "3 Hybrid", "4 Hybrid", "5 Hybrid", "6 Hybrid",
            "2 Iron", "3 Iron", "4 Iron", "5 Iron", "6 Iron", "7 Iron", "8 Iron", "9 Iron",
            "Pitching Wedge", "Gap Wedge", "Approach Wedge", "48°", "50°", "52°", "54°", "56°", "58°", "60°", "62°",
            "Chipper", "Putter", "Driving Iron", "Utility Iron", "Bump & Run"
        };

        public static readonly float[] DefaultLofts =
        {
            10.5f, 13.5f, 15f, 16.5f, 18f, 21f, 24f,
            18f, 19f, 22f, 25f, 28f,
            18f, 21f, 24f, 27f, 30f, 34f, 38f, 42f,
            46f, 50f, 52f, 48f, 50f, 52f, 54f, 56f, 58f, 60f, 62f,
            35f, 3f, 20f, 23f, 34f
        };

        public readonly string[] ClubNames = new string[ClubCount];
        public readonly float[] Lofts = new float[ClubCount];
        public readonly float[] CarryMeters = new float[ClubCount];
        public readonly float[] TotalMeters = new float[ClubCount];
        public readonly bool[] InBag = new bool[ClubCount];

        private GolfBagProfile()
        {
            for (int i = 0; i < ClubCount; i++)
            {
                ClubNames[i] = DefaultClubNames[i];
                Lofts[i] = DefaultLofts[i];
                CarryMeters[i] = 0f;
                TotalMeters[i] = 0f;
                InBag[i] = i < 14;
            }
        }

        public static GolfBagProfile Load(string playerName)
        {
            GolfBagProfile profile = new GolfBagProfile();
            string key = Prefix(playerName);

            for (int i = 0; i < ClubCount; i++)
            {
                profile.ClubNames[i] = PlayerPrefs.GetString(key + ".Name." + i, DefaultClubNames[i]);
                profile.Lofts[i] = PlayerPrefs.GetFloat(key + ".Loft." + i, DefaultLofts[i]);
                profile.CarryMeters[i] = PlayerPrefs.GetFloat(key + ".Carry." + i, 0f);
                profile.TotalMeters[i] = PlayerPrefs.GetFloat(key + ".Total." + i, 0f);
                profile.InBag[i] = PlayerPrefs.GetInt(key + ".InBag." + i, i < 14 ? 1 : 0) == 1;
            }

            return profile;
        }

        public void Save(string playerName)
        {
            string key = Prefix(playerName);
            for (int i = 0; i < ClubCount; i++)
            {
                PlayerPrefs.SetString(key + ".Name." + i, ClubNames[i] ?? DefaultClubNames[i]);
                PlayerPrefs.SetFloat(key + ".Loft." + i, Lofts[i]);
                PlayerPrefs.SetFloat(key + ".Carry." + i, Mathf.Max(0f, CarryMeters[i]));
                PlayerPrefs.SetFloat(key + ".Total." + i, Mathf.Max(0f, TotalMeters[i]));
                PlayerPrefs.SetInt(key + ".InBag." + i, InBag[i] ? 1 : 0);
            }
            PlayerPrefs.Save();
        }

        public int FindClubIndex(string clubName)
        {
            if (string.IsNullOrWhiteSpace(clubName)) return -1;
            for (int i = 0; i < ClubCount; i++)
                if (string.Equals(ClubNames[i], clubName, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        public int CountInBag()
        {
            int count = 0;
            for (int i = 0; i < ClubCount; i++) if (InBag[i]) count++;
            return count;
        }

        private static string Prefix(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName)) playerName = "Player 1";
            char[] chars = playerName.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i])) chars[i] = '_';
            return "GolfSimZA.GolfBag." + new string(chars);
        }
    }
}
