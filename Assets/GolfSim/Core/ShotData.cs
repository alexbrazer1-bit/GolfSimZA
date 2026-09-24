using System;
using UnityEngine;

namespace GolfSimZA.Core
{
    [Serializable]
    public struct ShotData
    {
        public DateTime TimestampUtc;
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
            return new ShotData
            {
                TimestampUtc = DateTime.UtcNow,
                BallSpeedMps = ballSpeedMps,
                ClubSpeedMps = 45.0f,
                LaunchAngleDeg = 13.5f,
                LaunchDirectionDeg = 0.0f,
                BackSpinRpm = 2400.0f,
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
