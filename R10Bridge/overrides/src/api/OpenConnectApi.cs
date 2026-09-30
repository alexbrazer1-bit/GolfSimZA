using System.Text.Json.Serialization;

namespace gspro_r10.OpenConnect
{
  public class OpenConnectApiMessage
  {
    public string DeviceID { get { return "GSPRO-R10"; } }
    public string Units { get { return "Yards"; } }
    public int ShotNumber { get; set; }
    public string APIVersion { get { return "1"; } }
    public BallData? BallData { get; set; }
    public ClubData? ClubData { get; set; }
    public ShotDataOptions? ShotDataOptions { get; set; }
    /// <summary>GolfSimZA: live state of the R10 itself (Bluetooth link, battery, ready). Ignored by other OpenConnect receivers.</summary>
    public R10Status? R10Status { get; set; }

    /// <summary>GolfSimZA: a heartbeat that carries the R10's Bluetooth / battery / ready state.</summary>
    public static OpenConnectApiMessage CreateStatus(R10Status status)
    {
      return new OpenConnectApiMessage()
      {
        ShotNumber = 0,
        R10Status = status,
        ShotDataOptions = new ShotDataOptions()
        {
          ContainsBallData = false,
          ContainsClubData = false,
          LaunchMonitorIsReady = status.Connected && status.Ready,
          LaunchMonitorBallDetected = status.Connected && status.Ready,
          IsHeartBeat = true
        }
      };
    }

    public static OpenConnectApiMessage CreateHeartbeat(bool launchMonitorReady = false)
    {
      return new OpenConnectApiMessage()
      {
        ShotNumber = 0,
        ShotDataOptions = new ShotDataOptions()
        {
          ContainsBallData = false,
          ContainsClubData = false,
          LaunchMonitorIsReady = launchMonitorReady,
          LaunchMonitorBallDetected = launchMonitorReady,
          IsHeartBeat = true
        }
      };
    }

    public static OpenConnectApiMessage CreateShotData(int shotNumber, BallData? ballData, ClubData? clubData = null)
    {
      return new OpenConnectApiMessage()
      {
        ShotNumber = shotNumber,
        BallData = ballData,
        ClubData = clubData,
        ShotDataOptions = new ShotDataOptions()
        {
          ContainsBallData = (ballData != null),
          ContainsClubData = (clubData != null),
        }
      };
    }


    public static OpenConnectApiMessage TestShot()
    {
      return new OpenConnectApiMessage()
      {
        ShotNumber = 0,
        BallData = new BallData()
        {
          Speed = 200,
          SpinAxis = -90,
          TotalSpin = 50000,
          SideSpin = 50000,
          BackSpin = -100000,
          HLA = 10,
          VLA = 20
        },
        ShotDataOptions = new ShotDataOptions()
        {
          ContainsBallData = true,
          ContainsClubData = false,
        }
      };
    }
  }

  /// <summary>GolfSimZA: state of the Garmin R10 as seen by this bridge.</summary>
  public class R10Status
  {
    /// <summary>Bluetooth link to the R10 is up and the device is set up.</summary>
    public bool Connected { get; set; }
    /// <summary>Battery level 0-100, -1 when not known yet.</summary>
    public int Battery { get; set; } = -1;
    /// <summary>R10 is waiting for a shot (radar armed).</summary>
    public bool Ready { get; set; }
    /// <summary>R10 state: Waiting, Recording, Processing, Standby, Error ...</summary>
    public string? State { get; set; }
    public string? DeviceName { get; set; }
    public string? Model { get; set; }
    public string? Firmware { get; set; }
    /// <summary>What the bridge is doing / the last problem, e.g. "Connecting", "Not paired".</summary>
    public string? Message { get; set; }

    public R10Status Clone() => (R10Status)MemberwiseClone();
  }

  public class ShotDataOptions
  {
    public bool ContainsBallData { get; set; }
    public bool ContainsClubData { get; set; }
    public bool? LaunchMonitorIsReady { get; set; }
    public bool? LaunchMonitorBallDetected { get; set; }
    public bool? IsHeartBeat { get; set; }
  }

  public class BallData
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

  public class ClubData
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

  public class OpenConnectApiResponse
  {
    public int Code { get; set; }
    public string? Message { get; set; }
    public PlayerInfo? Player { get; set; }
  }

  public class PlayerInfo
  {
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Handed? Handed { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Club? Club { get; set; }
    public float? DistanceToTarget { get; set; }
  }

  public enum Handed
  {
    RH,
    LH
  }

  public enum Club
  {
    unknown,
    DR,
    W2,
    W3,
    W4,
    W5,
    W6,
    W7,
    I1,
    I2,
    I3,
    I4,
    I5,
    I6,
    I7,
    I8,
    I9,
    H2,
    H3,
    H4,
    H5,
    H6,
    H7,
    PW,
    GW,
    SW,
    LW,
    PT
  }

}