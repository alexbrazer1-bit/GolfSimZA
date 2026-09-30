using System;
using GolfSimZA.Core;
using GolfSimZA.Physics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfSimZA.Players
{
    public sealed class ClubMappingSession : MonoBehaviour
    {
        private const string MapModeKey = "GolfSimZA.MapMode";
        private const string PlayerKey = "GolfSimZA.MapPlayer";
        private const string ClubKey = "GolfSimZA.MapClubIndex";
        public const int RequiredShots = 6;

        private BallFlightSimulator ballFlight;
        private bool subscribed;
        private string playerName;
        private int clubIndex;
        private string clubName;
        private readonly float[] carries = new float[RequiredShots];
        private readonly float[] totals = new float[RequiredShots];
        private int shotCount;
        private bool completed;

        public static bool IsActive => PlayerPrefs.GetInt(MapModeKey, 0) == 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<ClubMappingSession>() != null) return;
            GameObject go = new GameObject("GolfSimZA_ClubMappingSession");
            DontDestroyOnLoad(go);
            go.AddComponent<ClubMappingSession>();
        }

        private void Awake() => Instance = this;

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Unsubscribe();
        }

        private void Start()
        {
            ConfigureFromPrefs();
            TryAttach();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ConfigureFromPrefs();
            Unsubscribe();
            TryAttach();
        }

        private void ConfigureFromPrefs()
        {
            if (!IsActive)
            {
                completed = true;
                return;
            }

            string newPlayer = PlayerPrefs.GetString(PlayerKey, "Player 1");
            int newClub = Mathf.Clamp(PlayerPrefs.GetInt(ClubKey, 0), 0, GolfBagProfile.ClubCount - 1);
            bool changed = !string.Equals(playerName, newPlayer, StringComparison.Ordinal) || clubIndex != newClub;
            // A club that was mapped (or cancelled) before can be mapped again: a new mapping
            // session always starts from shot 1 - only a scene reload during the same mapping
            // (e.g. the game menu) keeps the shots already hit.
            if (changed || completed)
            {
                playerName = newPlayer;
                clubIndex = newClub;
                GolfBagProfile profile = GolfBagProfile.Load(playerName);
                clubName = profile.ClubNames[clubIndex];
                shotCount = 0;
                completed = false;
                for (int i = 0; i < RequiredShots; i++)
                {
                    carries[i] = 0f;
                    totals[i] = 0f;
                }
            }
        }

        private void TryAttach()
        {
            if (!IsActive || completed) return;
            ballFlight = FindFirstObjectByType<BallFlightSimulator>();
            if (ballFlight == null || subscribed) return;
            ballFlight.ShotCompleted += OnShotCompleted;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (ballFlight != null && subscribed)
                ballFlight.ShotCompleted -= OnShotCompleted;
            subscribed = false;
            ballFlight = null;
        }

        private void Update()
        {
            if (!IsActive || completed) return;
            if (!subscribed) TryAttach();
        }

        private void OnShotCompleted(ShotData shot, float carry, float total, float maxHeight, float flightTime)
        {
            if (!IsActive || completed || shotCount >= RequiredShots) return;
            if (!string.Equals(shot.ClubName, clubName, StringComparison.OrdinalIgnoreCase)) return;

            carries[shotCount] = Mathf.Max(0f, carry);
            totals[shotCount] = Mathf.Max(carries[shotCount], total);
            shotCount++;

            if (shotCount >= RequiredShots)
                FinishMapping();
        }

        private void FinishMapping()
        {
            GolfBagProfile profile = GolfBagProfile.Load(playerName);
            float carryAverage = 0f;
            float totalAverage = 0f;
            for (int i = 0; i < RequiredShots; i++)
            {
                carryAverage += carries[i];
                totalAverage += totals[i];
            }
            carryAverage /= RequiredShots;
            totalAverage /= RequiredShots;

            profile.CarryMeters[clubIndex] = carryAverage;
            profile.TotalMeters[clubIndex] = totalAverage;
            profile.InBag[clubIndex] = true;
            profile.Save(playerName);

            completed = true;
            PlayerPrefs.SetInt(MapModeKey, 0);
            PlayerPrefs.SetString("GolfSimZA.MapReturnPlayer", playerName);
            PlayerPrefs.Save();

            Debug.Log($"[GolfSimZA] Mapped {clubName} for {playerName}: {carryAverage:F1} m carry / {totalAverage:F1} m total from {RequiredShots} shots.");
            SceneManager.LoadScene("GolfSimZA_0_5_Players");
        }

        // ---------------------------------------------------------------- For the range HUD (TrackMan-style 6-shot block)

        /// <summary>The mapping session (the range HUD draws its 6-shot block above the club selector).</summary>
        public static ClubMappingSession Instance { get; private set; }
        public string PlayerName => playerName ?? "";
        public string ClubName => clubName ?? "";
        public int ShotCount => shotCount;
        public float Carry(int i) => i >= 0 && i < RequiredShots ? carries[i] : 0f;
        public float AverageCarry
        {
            get
            {
                if (shotCount == 0) return 0f;
                float sum = 0f;
                for (int i = 0; i < shotCount; i++) sum += carries[i];
                return sum / shotCount;
            }
        }

        /// <summary>START AGAIN: throw away the shots hit so far with this club and map it from shot 1.</summary>
        public void Restart()
        {
            shotCount = 0;
            for (int i = 0; i < RequiredShots; i++)
            {
                carries[i] = 0f;
                totals[i] = 0f;
            }
        }

        /// <summary>REDO LAST: remove the last shot (a mishit) and hit it again.</summary>
        public void UndoLast()
        {
            if (shotCount <= 0) return;
            shotCount--;
            carries[shotCount] = 0f;
            totals[shotCount] = 0f;
        }

        /// <summary>CANCEL: stop mapping and go back to the player's bag.</summary>
        public void Cancel()
        {
            PlayerPrefs.SetInt(MapModeKey, 0);
            PlayerPrefs.Save();
            completed = true;
            SceneManager.LoadScene("GolfSimZA_0_5_Players");
        }
    }
}
