using System;
using System.Reflection;
using GolfSimZA.Core;
using GolfSimZA.Players;
using GolfSimZA.UI;
using GolfSimZA.Courses;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GolfSimZA.LaunchMonitors
{
    public sealed class TestShotProvider : MonoBehaviour, ILaunchMonitorAdapter
    {
        [Serializable]
        private struct ClubPreset
        {
            public string name;
            public int number;
            public float loft;
            public float ballSpeed;
            public float clubSpeed;
            public float launch;
            public float spin;

            public ClubPreset(string name, int number, float loft, float ballSpeed, float clubSpeed, float launch, float spin)
            {
                this.name = name; this.number = number; this.loft = loft; this.ballSpeed = ballSpeed;
                this.clubSpeed = clubSpeed; this.launch = launch; this.spin = spin;
            }
        }

        [SerializeField] private int selectedClubIndex;
        private readonly ClubPreset[] clubs =
        {
            new ClubPreset("Driver", 1, 10.5f, 67.0f, 45.0f, 13.5f, 2400.0f),
            new ClubPreset("3 Wood", 3, 15.0f, 63.0f, 43.0f, 12.0f, 3000.0f),
            new ClubPreset("5 Iron", 5, 25.0f, 55.0f, 38.0f, 15.0f, 5000.0f),
            new ClubPreset("7 Iron", 7, 34.0f, 50.0f, 35.0f, 18.0f, 6500.0f),
            new ClubPreset("9 Iron", 9, 42.0f, 45.0f, 32.0f, 21.0f, 8000.0f),
            new ClubPreset("Pitching Wedge", 10, 46.0f, 41.0f, 29.0f, 24.0f, 9000.0f),
            new ClubPreset("Sand Wedge", 11, 56.0f, 36.0f, 26.0f, 29.0f, 9500.0f),
            new ClubPreset("Putter", 0, 3.0f, 8.0f, 5.0f, 3.0f, 1200.0f)
        };

        public string DeviceName => "Development Test Shot";
        public bool IsConnected { get; private set; }
        public string SelectedClubName => clubs[Mathf.Clamp(selectedClubIndex, 0, clubs.Length - 1)].name;
        public int SelectedClubSlot => Mathf.Clamp(selectedClubIndex + 1, 1, clubs.Length);
        public event Action<ShotData> ShotReceived;

        private RoundGameplayUI roundGameplayUI;
        private FieldInfo roundSelectedClubField;
        private float lastShotTime = -10f;
        private int lastShotSlot;
        private int mappingShotSequence;

        public bool TryConnect() { IsConnected = true; return true; }
        public void Disconnect() { IsConnected = false; }

        private void Awake()
        {
            TryConnect();
            roundGameplayUI = FindFirstObjectByType<RoundGameplayUI>();
            if (roundGameplayUI != null)
                roundSelectedClubField = typeof(RoundGameplayUI).GetField("selectedClubNumber", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private void Update()
        {
            if (!IsConnected || Keyboard.current == null) return;

            if (ClubMappingSession.IsActive)
            {
                HandleMappingInput();
                return;
            }

            SyncFromRoundUI();
            ApplyRecommendedClub();

            for (int i = 0; i < clubs.Length; i++)
            {
                if (IsNumberKeyPressed(Keyboard.current, i + 1))
                {
                    selectedClubIndex = i;
                    PlayerPrefs.DeleteKey("GolfSimZA.RoundClubName");
                    Debug.Log($"[GolfSimZA] Selected club: {SelectedClubName}");
                }
            }

            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                ClubPreset club = GetActiveClubPreset();
                lastShotSlot = selectedClubIndex + 1;
                lastShotTime = Time.unscaledTime;
                ShotReceived?.Invoke(ShotData.CreateTestShot(club.name, club.number, club.loft, club.ballSpeed, club.clubSpeed, club.launch, club.spin));
            }
        }

        private void ApplyRecommendedClub()
        {
            string requested = PlayerPrefs.GetString("GolfSimZA.RoundClubName", "");
            if (string.IsNullOrWhiteSpace(requested)) return;

            string player = "Player 1";
            string names = CourseSession.PlayerNames;
            if (!string.IsNullOrWhiteSpace(names))
            {
                string[] split = names.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                if (split.Length > 0 && !string.IsNullOrWhiteSpace(split[0])) player = split[0].Trim();
            }

            GolfBagProfile profile = GolfBagProfile.Load(player);
            int index = profile.FindClubIndex(requested);
            if (index < 0 || !profile.InBag[index]) return;

            // Keep the simulator's legacy club buttons in sync where possible.
            for (int i = 0; i < clubs.Length; i++)
                if (string.Equals(clubs[i].name, requested, StringComparison.OrdinalIgnoreCase))
                    selectedClubIndex = i;
        }

        private ClubPreset GetActiveClubPreset()
        {
            string requested = PlayerPrefs.GetString("GolfSimZA.RoundClubName", "");
            if (!string.IsNullOrWhiteSpace(requested))
            {
                string player = "Player 1";
                string names = CourseSession.PlayerNames;
                if (!string.IsNullOrWhiteSpace(names))
                {
                    string[] split = names.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                    if (split.Length > 0 && !string.IsNullOrWhiteSpace(split[0])) player = split[0].Trim();
                }
                GolfBagProfile profile = GolfBagProfile.Load(player);
                int index = profile.FindClubIndex(requested);
                if (index >= 0 && profile.InBag[index])
                    return BuildMappingPreset(profile.ClubNames[index], index, profile.Lofts[index], 0);
            }
            return clubs[Mathf.Clamp(selectedClubIndex, 0, clubs.Length - 1)];
        }

        private void HandleMappingInput()
        {
            int requestedIndex = Mathf.Clamp(PlayerPrefs.GetInt("GolfSimZA.MapClubIndex", 0), 0, GolfBagProfile.ClubCount - 1);
            if (!Keyboard.current.spaceKey.wasPressedThisFrame) return;
            GolfBagProfile profile = GolfBagProfile.Load(PlayerPrefs.GetString("GolfSimZA.MapPlayer", "Player 1"));
            string clubName = profile.ClubNames[requestedIndex];
            float loft = profile.Lofts[requestedIndex];
            ClubPreset shot = BuildMappingPreset(clubName, requestedIndex, loft, mappingShotSequence++);
            ShotReceived?.Invoke(ShotData.CreateTestShot(shot.name, shot.number, shot.loft, shot.ballSpeed, shot.clubSpeed, shot.launch, shot.spin));
        }

        private ClubPreset BuildMappingPreset(string name, int index, float loft, int shotNumber)
        {
            if (string.Equals(name, "Putter", StringComparison.OrdinalIgnoreCase))
                return new ClubPreset(name, index + 1, loft, 8.0f, 5.0f, 3.0f, 1200.0f);
            float speed = Mathf.Clamp(74f - loft * 0.72f, 26f, 69f);
            float clubSpeed = Mathf.Max(18f, speed / 1.48f);
            float launch = Mathf.Clamp(loft * 0.52f + 8f, 10f, 32f);
            float spin = Mathf.Clamp(1800f + loft * 145f, 2200f, 10500f);
            float variance = 1f + UnityEngine.Random.Range(-0.018f, 0.018f);
            speed *= variance; clubSpeed *= variance;
            launch += UnityEngine.Random.Range(-0.7f, 0.7f);
            spin *= UnityEngine.Random.Range(0.96f, 1.04f);
            return new ClubPreset(name, index + 1, loft, speed, clubSpeed, launch, spin);
        }

        private void LateUpdate()
        {
            if (ClubMappingSession.IsActive || roundGameplayUI == null || roundSelectedClubField == null) return;
            int uiSlot = GetRoundUISlot();
            if (Time.unscaledTime - lastShotTime < 0.35f)
            {
                if (uiSlot != lastShotSlot) roundSelectedClubField.SetValue(roundGameplayUI, lastShotSlot);
                return;
            }
            if (uiSlot >= 1 && uiSlot <= clubs.Length && uiSlot != selectedClubIndex + 1)
                selectedClubIndex = uiSlot - 1;
        }

        private void SyncFromRoundUI()
        {
            if (roundGameplayUI == null || roundSelectedClubField == null)
            {
                roundGameplayUI = FindFirstObjectByType<RoundGameplayUI>();
                if (roundGameplayUI != null)
                    roundSelectedClubField = typeof(RoundGameplayUI).GetField("selectedClubNumber", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            if (roundGameplayUI == null || roundSelectedClubField == null) return;
            int uiSlot = GetRoundUISlot();
            if (uiSlot >= 1 && uiSlot <= clubs.Length && Time.unscaledTime - lastShotTime >= 0.35f)
                selectedClubIndex = uiSlot - 1;
        }

        private int GetRoundUISlot()
        {
            object value = roundSelectedClubField.GetValue(roundGameplayUI);
            return value is int ? (int)value : 1;
        }

        private static bool IsNumberKeyPressed(Keyboard keyboard, int number)
        {
            switch (number)
            {
                case 1: return keyboard.digit1Key.wasPressedThisFrame;
                case 2: return keyboard.digit2Key.wasPressedThisFrame;
                case 3: return keyboard.digit3Key.wasPressedThisFrame;
                case 4: return keyboard.digit4Key.wasPressedThisFrame;
                case 5: return keyboard.digit5Key.wasPressedThisFrame;
                case 6: return keyboard.digit6Key.wasPressedThisFrame;
                case 7: return keyboard.digit7Key.wasPressedThisFrame;
                case 8: return keyboard.digit8Key.wasPressedThisFrame;
                default: return false;
            }
        }
    }
}
