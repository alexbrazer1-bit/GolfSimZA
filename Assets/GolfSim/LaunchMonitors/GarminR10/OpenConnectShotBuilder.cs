using System;
using UnityEngine;

namespace GolfSimZA.LaunchMonitors.GarminR10
{
    /// <summary>
    /// Builds a complete OpenConnect v1 shot payload from already-decoded R10 metrics.
    /// This class contains no Garmin transport code; it is only the output/serialization layer.
    /// </summary>
    public static class OpenConnectShotBuilder
    {
        [Serializable]
        public sealed class OpenConnectShot
        {
            public string DeviceID = "GSPRO-R10";
            public string Units = "Yards";
            public int ShotNumber;
            public string APIVersion = "1";
            public BallData BallData;
            public ClubData ClubData;
            public ShotDataOptions ShotDataOptions;
        }

        [Serializable]
        public sealed class BallData
        {
            public double Speed;
            public double SpinAxis;
            public double TotalSpin;
            public double BackSpin;
            public double SideSpin;
            public double HLA;
            public double VLA;
            public double CarryDistance;
        }

        [Serializable]
        public sealed class ClubData
        {
            public double Speed;
            public double AngleOfAttack;
            public double FaceToTarget;
            public double Lie;
            public double Loft;
            public double Path;
            public double SpeedAtImpact;
            public double VerticalFaceImpact;
            public double HorizontalFaceImpact;
            public double ClosureRate;
        }

        [Serializable]
        public sealed class ShotDataOptions
        {
            public bool ContainsBallData;
            public bool ContainsClubData;
            public bool LaunchMonitorIsReady;
            public bool LaunchMonitorBallDetected;
            public bool IsHeartBeat;
        }

        /// <summary>
        /// ballSpeedMph/clubSpeedMph are MPH; spin is RPM; launch/club angles are degrees.
        /// </summary>
        public static string BuildShot(
            int shotNumber,
            double ballSpeedMph,
            double spinAxisDeg,
            double totalSpinRpm,
            double hlaDeg,
            double vlaDeg,
            double clubSpeedMph,
            double attackAngleDeg,
            double faceToTargetDeg,
            double clubPathDeg,
            double loftDeg = 0,
            double carryYards = 0)
        {
            double axisRad = -spinAxisDeg * Math.PI / 180.0;

            var ball = new BallData
            {
                Speed = ballSpeedMph,
                SpinAxis = spinAxisDeg,
                TotalSpin = totalSpinRpm,
                SideSpin = totalSpinRpm * Math.Sin(axisRad),
                BackSpin = totalSpinRpm * Math.Cos(axisRad),
                HLA = hlaDeg,
                VLA = vlaDeg,
                CarryDistance = carryYards
            };

            var club = new ClubData
            {
                Speed = clubSpeedMph,
                SpeedAtImpact = clubSpeedMph,
                AngleOfAttack = attackAngleDeg,
                FaceToTarget = faceToTargetDeg,
                Path = clubPathDeg,
                Loft = loftDeg
            };

            var payload = new OpenConnectShot
            {
                ShotNumber = shotNumber,
                BallData = ball,
                ClubData = club,
                ShotDataOptions = new ShotDataOptions
                {
                    ContainsBallData = true,
                    ContainsClubData = true,
                    LaunchMonitorIsReady = true,
                    LaunchMonitorBallDetected = true,
                    IsHeartBeat = false
                }
            };

            return JsonUtility.ToJson(payload);
        }
    }
}
