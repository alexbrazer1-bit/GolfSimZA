using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using GolfSimZA.Core;
using GolfSimZA.LaunchMonitors;
using UnityEngine;

namespace GolfSimZA.LaunchMonitors.GarminR10
{
    /// <summary>
    /// Garmin Approach R10 OpenConnect receiver.
    ///
    /// The R10's Bluetooth transport remains outside GolfSimZA. A Windows R10
    /// bridge (for example an OpenConnect-compatible bridge) pairs to the R10
    /// and forwards its shot messages to this receiver on TCP port 921.
    /// GolfSimZA then owns the complete gameplay/physics/UI path.
    /// </summary>
    public sealed class GarminR10Adapter : MonoBehaviour, ILaunchMonitorAdapter
    {
        [SerializeField] private int listenPort = 921;
        [SerializeField] private bool autoStart = true;
        [SerializeField] private bool sendReadyHeartbeat = true;

        private TcpListener listener;
        private TcpClient client;
        private readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<ShotData> shots = new ConcurrentQueue<ShotData>();
        private readonly StringBuilder streamBuffer = new StringBuilder();
        private volatile bool stopping;
        private float nextHeartbeat;

        public string DeviceName => "Garmin Approach R10";
        public bool IsConnected { get; private set; }
        public int ListenPort => listenPort;
        public event Action<ShotData> ShotReceived;

        [Serializable]
        private sealed class OpenConnectMessage
        {
            public string DeviceID;
            public string Units;
            public int ShotNumber;
            public string APIVersion;
            public BallData BallData;
            public ClubData ClubData;
            public ShotDataOptions ShotDataOptions;
        }

        [Serializable]
        private sealed class BallData
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
        private sealed class ClubData
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
        private sealed class ShotDataOptions
        {
            public bool ContainsBallData;
            public bool ContainsClubData;
            public bool LaunchMonitorIsReady;
            public bool LaunchMonitorBallDetected;
            public bool IsHeartBeat;
        }

        private void Awake()
        {
            if (autoStart)
                TryConnect();
        }

        public bool TryConnect()
        {
            if (listener != null)
                return true;

            try
            {
                stopping = false;
                listener = new TcpListener(IPAddress.Loopback, Mathf.Clamp(listenPort, 1, 65535));
                listener.Start();
                listener.BeginAcceptTcpClient(OnClientAccepted, null);
                IsConnected = true;
                Debug.Log($"[GolfSimZA] Garmin R10 receiver listening on 127.0.0.1:{listenPort}. Waiting for R10 bridge...");
                return true;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                Debug.LogError("[GolfSimZA] Could not start Garmin R10 receiver: " + ex.Message);
                return false;
            }
        }

        private void OnClientAccepted(IAsyncResult result)
        {
            if (stopping || listener == null) return;
            try
            {
                TcpClient next = listener.EndAcceptTcpClient(result);
                try { client?.Close(); } catch { }
                client = next;
                incoming.Enqueue(string.Empty);
                BeginRead(client);
            }
            catch (Exception ex)
            {
                if (!stopping) Debug.LogWarning("[GolfSimZA] R10 receiver accept error: " + ex.Message);
            }
            finally
            {
                try { if (!stopping) listener.BeginAcceptTcpClient(OnClientAccepted, null); } catch { }
            }
        }

        private void BeginRead(TcpClient tcpClient)
        {
            try
            {
                NetworkStream stream = tcpClient.GetStream();
                var state = new ReadState { Client = tcpClient, Stream = stream, Buffer = new byte[8192] };
                stream.BeginRead(state.Buffer, 0, state.Buffer.Length, OnRead, state);
            }
            catch (Exception ex)
            {
                if (!stopping) Debug.LogWarning("[GolfSimZA] R10 receiver read setup error: " + ex.Message);
            }
        }

        private sealed class ReadState
        {
            public TcpClient Client;
            public NetworkStream Stream;
            public byte[] Buffer;
        }

        private void OnRead(IAsyncResult result)
        {
            ReadState state = (ReadState)result.AsyncState;
            try
            {
                int count = state.Stream.EndRead(result);
                if (count <= 0) return;
                incoming.Enqueue(Encoding.UTF8.GetString(state.Buffer, 0, count));
                state.Stream.BeginRead(state.Buffer, 0, state.Buffer.Length, OnRead, state);
            }
            catch
            {
                if (!stopping) IsConnected = listener != null;
            }
        }

        private void Update()
        {
            while (incoming.TryDequeue(out string chunk))
            {
                if (!string.IsNullOrEmpty(chunk))
                {
                    lock (streamBuffer)
                    {
                        streamBuffer.Append(chunk);
                        ParseBufferedMessages();
                    }
                }
            }

            while (shots.TryDequeue(out ShotData shot))
                ShotReceived?.Invoke(shot);

            if (sendReadyHeartbeat && client != null && client.Connected && Time.unscaledTime >= nextHeartbeat)
            {
                SendHeartbeat();
                nextHeartbeat = Time.unscaledTime + 2f;
            }
        }

        private void ParseBufferedMessages()
        {
            while (true)
            {
                string json = ExtractJsonObject(streamBuffer);
                if (json == null) return;
                if (json.Length < 2) continue;

                try
                {
                    OpenConnectMessage message = JsonUtility.FromJson<OpenConnectMessage>(json);
                    if (message != null && message.BallData != null && message.ShotDataOptions != null && message.ShotDataOptions.ContainsBallData)
                    {
                        ShotData shot = ConvertShot(message);
                        if (shot.IsValid) shots.Enqueue(shot);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[GolfSimZA] Ignored invalid R10 OpenConnect message: " + ex.Message);
                }
            }
        }

        private static string ExtractJsonObject(StringBuilder buffer)
        {
            int start = -1;
            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = 0; i < buffer.Length; i++)
            {
                char c = buffer[i];
                if (start < 0)
                {
                    if (c == '{') { start = i; depth = 1; }
                    continue;
                }

                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                    continue;
                }

                if (c == '"') inString = true;
                else if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        string json = buffer.ToString(start, i - start + 1);
                        buffer.Remove(0, i + 1);
                        return json;
                    }
                }
            }
            return null;
        }

        private static ShotData ConvertShot(OpenConnectMessage message)
        {
            BallData b = message.BallData;
            ClubData c = message.ClubData;

            // OpenConnect uses yards for distance and mph for speed. GolfSimZA
            // internally uses SI units for its physics engine.
            float ballSpeedMps = (float)(Math.Max(0.0, b.Speed) * 0.44704);
            float clubSpeedMps = c != null ? (float)(Math.Max(0.0, c.Speed) * 0.44704) : 0f;
            float carryMeters = (float)(Math.Max(0.0, b.CarryDistance) * 0.9144);
            float loft = c != null ? (float)c.Loft : 0f;
            string clubName = MapClubName(c, message);
            int clubNumber = MapClubNumber(c, message);

            return new ShotData
            {
                TimestampUtc = DateTime.UtcNow,
                ClubName = clubName,
                ClubNumber = clubNumber,
                ClubLoftDeg = loft,
                BallSpeedMps = ballSpeedMps,
                ClubSpeedMps = clubSpeedMps,
                LaunchAngleDeg = (float)b.VLA,
                LaunchDirectionDeg = (float)b.HLA,
                BackSpinRpm = (float)Math.Max(0.0, b.BackSpin > 0 ? b.BackSpin : b.TotalSpin),
                SpinAxisDeg = (float)b.SpinAxis,
                CarryMeters = carryMeters,
                TotalMeters = carryMeters,
                HasClubData = c != null && c.Speed > 0,
                IsValid = ballSpeedMps > 0.5f
            };
        }

        private static string MapClubName(ClubData club, OpenConnectMessage message)
        {
            if (club == null) return "Garmin R10";
            float loft = (float)club.Loft;
            if (loft <= 11.5f) return "Driver";
            if (loft <= 17f) return "3 Wood";
            if (loft <= 22f) return "Hybrid";
            if (loft <= 27f) return "5 Iron";
            if (loft <= 31f) return "6 Iron";
            if (loft <= 35f) return "7 Iron";
            if (loft <= 39f) return "8 Iron";
            if (loft <= 44f) return "9 Iron";
            if (loft <= 49f) return "Pitching Wedge";
            if (loft <= 54f) return "Gap Wedge";
            if (loft <= 58f) return "Sand Wedge";
            return "Lob Wedge";
        }

        private static int MapClubNumber(ClubData club, OpenConnectMessage message)
        {
            if (club == null) return 0;
            float loft = (float)club.Loft;
            if (loft <= 11.5f) return 1;
            if (loft <= 17f) return 3;
            if (loft <= 22f) return 4;
            if (loft <= 27f) return 5;
            if (loft <= 31f) return 6;
            if (loft <= 35f) return 7;
            if (loft <= 39f) return 8;
            if (loft <= 44f) return 9;
            if (loft <= 49f) return 10;
            if (loft <= 54f) return 11;
            if (loft <= 58f) return 12;
            return 13;
        }

        private void SendHeartbeat()
        {
            try
            {
                if (client == null || !client.Connected) return;
                string heartbeat = "{\"DeviceID\":\"GolfSimZA\",\"Units\":\"Yards\",\"ShotNumber\":0,\"APIVersion\":\"1\",\"ShotDataOptions\":{\"ContainsBallData\":false,\"ContainsClubData\":false,\"LaunchMonitorIsReady\":true,\"LaunchMonitorBallDetected\":true,\"IsHeartBeat\":true}}";
                byte[] data = Encoding.UTF8.GetBytes(heartbeat);
                client.GetStream().Write(data, 0, data.Length);
            }
            catch { }
        }

        public void Disconnect()
        {
            stopping = true;
            IsConnected = false;
            try { client?.Close(); } catch { }
            try { listener?.Stop(); } catch { }
            client = null;
            listener = null;
        }

        private void OnDestroy() => Disconnect();
    }
}
