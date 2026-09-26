using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GolfSimZA.LaunchMonitors.GarminR10
{
    /// <summary>
    /// Builds a complete OpenConnect v1 shot payload from already-decoded R10 metrics.
    /// This class contains no Garmin transport code; it is only the output/serialization layer.
    /// </summary>
    public static class OpenConnectShotBuilder
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public sealed class OpenConnectShot
        {
            public string DeviceID { get; set; } = "GSPRO-R10";
            public string Units { get; set; } = "Yards";
            public int ShotNumber { get; set; }
            public string APIVersion { get; set; } = "1";
            public BallData BallData { get; set; }
            public ClubData ClubData { get; set; }
            public ShotDataOptions ShotDataOptions { get; set; }
        }

        public sealed class BallData
        {
            public double Speed { get; set; }
            public double SpinAxis { get; set; }
            public double TotalSpin { get; set; }
            public double BackSpin { get; set; }
            public double SideSpin { get; set; }
            public double HLA { get; set; }
            public double VLA { get; set; }
            public double CarryDistance { get; set; }
        }

        public sealed class ClubData
        {
            public double Speed { get; set; }
            public double AngleOfAttack { get; set; }
            public double FaceToTarget { get; set; }
            public double Lie { get; set; }
            public double Loft { get; set; }
            public double Path { get; set; }
            public double SpeedAtImpact { get; set; }
            public double VerticalFaceImpact { get; set; }
            public double HorizontalFaceImpact { get; set; }
            public double ClosureRate { get; set; }
        }

        public sealed class ShotDataOptions
        {
            public bool ContainsBallData { get; set; }
            public bool ContainsClubData { get; set; }
            public bool LaunchMonitorIsReady { get; set; }
            public bool LaunchMonitorBallDetected { get; set; }
            public bool IsHeartBeat { get; set; }
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

            return JsonSerializer.Serialize(payload, JsonOptions);
        }
    }
}
