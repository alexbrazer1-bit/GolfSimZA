using System.Text.Json;
using System.Text.Json.Serialization;
using gspro_r10.OpenConnect;
using Microsoft.Extensions.Configuration;

namespace gspro_r10
{
  public class ConnectionManager: IDisposable
  {
    private R10ConnectionServer? R10Server;
    private OpenConnectClient OpenConnectClient;
    private BluetoothConnection? BluetoothConnection { get; }
    internal HttpPuttingServer? PuttingConnection { get; }
    public event ClubChangedEventHandler? ClubChanged;
    public delegate void ClubChangedEventHandler(object sender, ClubChangedEventArgs e);
    public class ClubChangedEventArgs: EventArgs
    {
      public Club Club { get; set; }
    }

    private JsonSerializerOptions serializerSettings = new JsonSerializerOptions()
    {
      DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private int shotNumber = 0;
    private bool disposedValue;

    public ConnectionManager(IConfigurationRoot configuration)
    {
      OpenConnectClient = new OpenConnectClient(this, configuration.GetSection("openConnect"));
      OpenConnectClient.ConnectAsync();

      if (bool.Parse(configuration.GetSection("r10E6Server")["enabled"] ?? "false"))
      {
        R10Server = new R10ConnectionServer(this, configuration.GetSection("r10E6Server"));
        R10Server.Start();
      }

      if (bool.Parse(configuration.GetSection("bluetooth")["enabled"] ?? "false"))
        BluetoothConnection = new BluetoothConnection(this, configuration.GetSection("bluetooth"));

      if (bool.Parse(configuration.GetSection("putting")["enabled"] ?? "false"))
      {
        PuttingConnection = new HttpPuttingServer(this, configuration.GetSection("putting"));
        PuttingConnection.Start();
      }
    }

    internal void SendShot(OpenConnect.BallData? ballData, OpenConnect.ClubData? clubData)
    {
      string openConnectMessage = JsonSerializer.Serialize(OpenConnectApiMessage.CreateShotData(
        shotNumber++,
        ballData,
        clubData
      ), serializerSettings);

      OpenConnectClient.SendAsync(openConnectMessage);
    }

    public void ClubUpdate(Club club)
    {
      Task.Run(() => {
        ClubChanged?.Invoke(this, new ClubChangedEventArgs()
        {
          Club = club
        });
      });

    }

    internal void SendLaunchMonitorReadyUpdate(bool deviceReady)
    {
      OpenConnectClient.SetDeviceReady(deviceReady);
    }

    private readonly object statusLock = new object();
    private R10Status r10Status = new R10Status() { Message = "Starting" };

    /// <summary>GolfSimZA: the latest R10 state (a copy).</summary>
    internal R10Status CurrentR10Status
    {
      get { lock (statusLock) return r10Status.Clone(); }
    }

    /// <summary>GolfSimZA: changes the R10 state and sends it to the receiver straight away.</summary>
    internal void UpdateR10Status(Action<R10Status> change)
    {
      R10Status copy;
      lock (statusLock)
      {
        change(r10Status);
        copy = r10Status.Clone();
      }
      OpenConnectClient.SendStatus(copy);
    }

    protected virtual void Dispose(bool disposing)
    {
      if (!disposedValue)
      {
        if (disposing)
        {
          R10Server?.Dispose();
          PuttingConnection?.Dispose();
          BluetoothConnection?.Dispose();
          OpenConnectClient?.DisconnectAndStop();
          OpenConnectClient?.Dispose();
        }
        disposedValue = true;
      }
    }

    public void Dispose()
    {
      Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }
  }
}