using System;
using UnityEngine;

namespace GolfSimZA.Players
{
    [Serializable]
    public sealed class GolfBagProfile
    {
        public const int ClubCount = 14;

        public static readonly string[] DefaultClubNames =
        {
            "Driver", "3 Wood", "5 Wood", "4 Hybrid", "5 Iron", "6 Iron", "7 Iron",
            "8 Iron", "9 Iron", "Pitching Wedge", "Gap Wedge", "Sand Wedge", "Lob Wedge", "Putter"
        };

        public static readonly float[] DefaultLofts =
        {
            10.5f, 15f, 18f, 22f, 25f, 28f, 34f, 38f, 42f, 46f, 50f, 56f, 60f, 3f
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
                InBag[i] = true;
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
                profile.InBag[i] = PlayerPrefs.GetInt(key + ".InBag." + i, 1) == 1;
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
