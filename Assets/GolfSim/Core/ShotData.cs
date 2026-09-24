using System;
using UnityEngine;

namespace GolfSimZA.Core
{
    [Serializable]
    public struct ShotData
    {
        public DateTime TimestampUtc;
        public string ClubName;
        public int ClubNumber;
        public float ClubLoftDeg;
        public float BallSpeedMps;
        public float ClubSpeedMps;
        public float LaunchAngleDeg;
        public float LaunchDirectionDeg;
        public float BackSpinRpm;
        public float SpinAxisDeg;
        public float CarryMeters;
        public float TotalMeters;
        public bool HasClubData;
        public bool IsValid;

        public static ShotData TestDriver(float ballSpeedMps = 67.0f)
        {
            return CreateTestShot("Driver", 1, 10.5f, ballSpeedMps, 45.0f, 13.5f, 2400.0f);
        }

        public static ShotData CreateTestShot(
            string clubName,
            int clubNumber,
            float loftDeg,
            float ballSpeedMps,
            float clubSpeedMps,
            float launchAngleDeg,
            float backSpinRpm)
        {
            return new ShotData
            {
                TimestampUtc = DateTime.UtcNow,
                ClubName = clubName,
                ClubNumber = clubNumber,
                ClubLoftDeg = loftDeg,
                BallSpeedMps = ballSpeedMps,
                ClubSpeedMps = clubSpeedMps,
                LaunchAngleDeg = launchAngleDeg,
                LaunchDirectionDeg = 0.0f,
                BackSpinRpm = backSpinRpm,
                SpinAxisDeg = 0.0f,
                CarryMeters = 0.0f,
                TotalMeters = 0.0f,
                HasClubData = true,
                IsValid = true
            };
        }

        public float BallSpeedKph => BallSpeedMps * 3.6f;
        public float ClubSpeedKph => ClubSpeedMps * 3.6f;
    }
}
