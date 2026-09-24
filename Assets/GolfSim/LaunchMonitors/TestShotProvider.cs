using System;
using GolfSimZA.Core;
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
                this.name = name;
                this.number = number;
                this.loft = loft;
                this.ballSpeed = ballSpeed;
                this.clubSpeed = clubSpeed;
                this.launch = launch;
                this.spin = spin;
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
        public event Action<ShotData> ShotReceived;

        public bool TryConnect()
        {
            IsConnected = true;
            return true;
        }

        public void Disconnect()
        {
            IsConnected = false;
        }

        private void Awake()
        {
            TryConnect();
        }

        private void Update()
        {
            if (!IsConnected || Keyboard.current == null)
                return;

            for (int i = 0; i < clubs.Length; i++)
            {
                if (IsNumberKeyPressed(Keyboard.current, i + 1))
                {
                    selectedClubIndex = i;
                    Debug.Log($"[GolfSimZA] Selected club: {SelectedClubName}");
                }
            }

            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                ClubPreset club = clubs[Mathf.Clamp(selectedClubIndex, 0, clubs.Length - 1)];
                ShotReceived?.Invoke(ShotData.CreateTestShot(
                    club.name,
                    club.number,
                    club.loft,
                    club.ballSpeed,
                    club.clubSpeed,
                    club.launch,
                    club.spin));
            }
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
