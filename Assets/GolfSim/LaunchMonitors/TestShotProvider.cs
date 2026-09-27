using System;
using GolfSimZA.Core;
using GolfSimZA.Players;
using GolfSimZA.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GolfSimZA.LaunchMonitors
{
    /// <summary>
    /// Keyboard test shots (SPACE) for practising without the R10. The shot uses the club
    /// selected on the club bar and its loft from the player's bag, with a little random
    /// variation, so every club in the bag can be tested and mapped.
    /// </summary>
    public sealed class TestShotProvider : MonoBehaviour, ILaunchMonitorAdapter
    {
        public string DeviceName => "Development Test Shot";
        public bool IsConnected { get; private set; }
        public string SelectedClubName => ActiveClub.Resolve();
        public event Action<ShotData> ShotReceived;

        public bool TryConnect() { IsConnected = true; return true; }
        public void Disconnect() { IsConnected = false; }

        private void Awake() => TryConnect();

        private void Update()
        {
            if (!IsConnected || Keyboard.current == null || GameMenuOverlay.IsOpen) return;
            if (!Keyboard.current.spaceKey.wasPressedThisFrame) return;

            string club = ActiveClub.Resolve();
            float loft = ActiveClub.LoftOf(club);
            if (ClubMappingSession.IsActive)
            {
                // Map My Bag: use the loft stored in the mapping player's bag.
                GolfBagProfile profile = GolfBagProfile.Load(PlayerPrefs.GetString("GolfSimZA.MapPlayer", "Player 1"));
                int index = profile.FindClubIndex(club);
                if (index >= 0) loft = profile.Lofts[index];
            }
            ShotReceived?.Invoke(BuildShot(club, loft));
        }

        /// <summary>Typical amateur launch numbers for a club of this loft.</summary>
        public static ShotData BuildShot(string clubName, float loft)
        {
            if (ActiveClub.IsPutter(clubName))
                return ShotData.CreateTestShot(clubName, 0, loft, 2.6f * UnityEngine.Random.Range(0.95f, 1.05f), 1.8f, 1.0f, 0f);

            float speed = Mathf.Clamp(74f - loft * 0.72f, 26f, 69f);
            float clubSpeed = Mathf.Max(18f, speed / 1.48f);
            float launch = Mathf.Clamp(loft * 0.52f + 8f, 10f, 32f);
            float spin = Mathf.Clamp(1800f + loft * 145f, 2200f, 10500f);
            float variance = 1f + UnityEngine.Random.Range(-0.018f, 0.018f);
            speed *= variance;
            clubSpeed *= variance;
            launch += UnityEngine.Random.Range(-0.7f, 0.7f);
            spin *= UnityEngine.Random.Range(0.96f, 1.04f);

            ShotData shot = ShotData.CreateTestShot(clubName, ActiveClub.SlotFor(clubName), loft, speed, clubSpeed, launch, spin);
            shot.LaunchDirectionDeg = UnityEngine.Random.Range(-1.5f, 1.5f);
            shot.SpinAxisDeg = UnityEngine.Random.Range(-4f, 4f);
            return shot;
        }
    }
}
