using System;

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
        public float AttackAngleDeg;
        public float ClubPathDeg;
        public float FaceAngleDeg;
        public float SmashFactor;

        public static ShotData CreateTestDriverShot()
        {
            return new ShotData
            {
                TimestampUtc = DateTime.UtcNow,
                BallSpeedMps = 67.0f,
                ClubSpeedMps = 45.0f,
                LaunchAngleDeg = 14.5f,
                LaunchDirectionDeg = 0.0f,
                BackSpinRpm = 2400.0f,
                SpinAxisDeg = 0.0f,
                AttackAngleDeg = 2.0f,
                ClubPathDeg = 1.0f,
                FaceAngleDeg = 0.5f,
                SmashFactor = 1.49f
            };
        }
    }
}
