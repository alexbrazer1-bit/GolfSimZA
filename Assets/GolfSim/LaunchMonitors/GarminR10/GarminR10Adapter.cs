using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    /// The R10 bridge owns the Bluetooth transport and connects to this TCP
    /// server on port 921.  The bridge may emit club metrics and ball metrics
    /// as separate OpenConnect packets for the same ShotNumber.  This adapter
    /// deliberately assembles those packets before publishing one ShotData.
    /// </summary>
    public sealed class GarminR10Adapter : MonoBehaviour, ILaunchMonitorAdapter
    {
        [SerializeField] private int listenPort = 921;
        [SerializeField] private bool listenOnAllInterfaces = true;
        [SerializeField] private bool autoStart = true;
        [SerializeField] private bool sendReadyHeartbeat = true;
        [SerializeField] private float heartbeatIntervalSeconds = 2f;

        private TcpListener listener;
        private TcpClient client;
        private readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<ShotData> shots = new ConcurrentQueue<ShotData>();
        private readonly StringBuilder streamBuffer = new StringBuilder();
        private readonly Dictionary<int, PendingShot> pendingShots = new Dictionary<int, PendingShot>();
        private readonly HashSet<int> publishedShots = new HashSet<int>();
        private volatile bool stopping;
        private float nextHeartbeat;
        private string lastPacketSummary = "None";

        public string DeviceName => "Garmin Approach R10";
        public bool IsConnected { get; private set; }
        public bool IsListening { get; private set; }
        public int ListenPort => listenPort;
        public string LastPacketSummary => lastPacketSummary;
        public event Action<ShotData> ShotReceived;

        [Serializable]
        private sealed class OpenConnectMessage
        {
            public string DeviceID;
            public string Units;
            public int ShotNumber;
            public string APIversion;
            public string APIVersion;
            public BallData BallData;
            public ClubData ClubData;
            public ShotDataOptions ShotDataOptions;

            // Legacy/bridge aliases. They are optional and only used when a
            // bridge emits R10-style fields outside the standard BallData object.
            public double BallSpeed;
            public double LaunchAngle;
            public double LaunchDirection;
            public double SpinAxis;
            public double TotalSpin;
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

        private sealed class PendingShot
        {
            public BallData Ball;
            public ClubData Club;
        }

        private sealed class ReadState
        {
            public TcpClient Client;
            public NetworkStream Stream;
            public byte[] Buffer;
        }

        private void Awake()
        {
            if (autoStart)
                TryConnect();
        }

        public bool TryConnect()
        {
            if (listener != null)
                return IsListening;

            try
            {
                stopping = false;
                IPAddress bindAddress = listenOnAllInterfaces ? IPAddress.Any : IPAddress.Loopback;
                listener = new TcpListener(bindAddress, Mathf.Clamp(listenPort, 1, 65535));
                listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                listener.Start();
                listener.BeginAcceptTcpClient(OnClientAccepted, null);
                IsListening = true;
                IsConnected = false;
                Debug.Log($"[GolfSimZA] Garmin R10 OpenConnect server listening on {bindAddress}:{listenPort}. Waiting for bridge connection...");
                return true;
            }
            catch (Exception ex)
            {
                IsListening = false;
                IsConnected = false;
                Debug.LogError($"[GolfSimZA] Could not start Garmin R10 receiver on port {listenPort}: {ex.Message}");
                return false;
            }
        }

        private void OnClientAccepted(IAsyncResult result)
        {
            if (stopping || listener == null)
                return;

            try
            {
                TcpClient next = listener.EndAcceptTcpClient(result);
                try { client?.Close(); } catch { }
                client = next;
                client.NoDelay = true;
                IsConnected = true;
                lastPacketSummary = "TCP client connected";
                pendingShots.Clear();
                publishedShots.Clear();
                Debug.Log($"[GolfSimZA] Garmin R10 bridge connected from {client.Client.RemoteEndPoint}.");
                BeginRead(client);
            }
            catch (Exception ex)
            {
                if (!stopping)
                    Debug.LogWarning("[GolfSimZA] R10 receiver accept error: " + ex.Message);
            }
            finally
            {
                try
                {
                    if (!stopping && listener != null)
                        listener.BeginAcceptTcpClient(OnClientAccepted, null);
                }
                catch { }
            }
        }

        private void BeginRead(TcpClient tcpClient)
        {
            try
            {
                NetworkStream stream = tcpClient.GetStream();
                var state = new ReadState
                {
                    Client = tcpClient,
                    Stream = stream,
                    Buffer = new byte[8192]
                };
                stream.BeginRead(state.Buffer, 0, state.Buffer.Length, OnRead, state);
            }
            catch (Exception ex)
            {
                if (!stopping)
                    Debug.LogWarning("[GolfSimZA] R10 receiver read setup error: " + ex.Message);
            }
        }

        private void OnRead(IAsyncResult result)
        {
            var state = (ReadState)result.AsyncState;
            try
            {
                int count = state.Stream.EndRead(result);
                if (count <= 0)
                {
                    HandleClientDisconnected(state.Client);
                    return;
                }

                string chunk = Encoding.UTF8.GetString(state.Buffer, 0, count);
                lastPacketSummary = $"Received {count} bytes";
                incoming.Enqueue(chunk);
                state.Stream.BeginRead(state.Buffer, 0, state.Buffer.Length, OnRead, state);
            }
            catch
            {
                HandleClientDisconnected(state.Client);
            }
        }

        private void HandleClientDisconnected(TcpClient disconnectedClient)
        {
            if (!ReferenceEquals(client, disconnectedClient))
                return;

            IsConnected = false;
            lastPacketSummary = "TCP client disconnected";
            try { client?.Close(); } catch { }
            client = null;
            pendingShots.Clear();
            if (!stopping)
                Debug.Log("[GolfSimZA] Garmin R10 bridge disconnected; server remains ready for reconnect.");
        }

        private void Update()
        {
            while (incoming.TryDequeue(out string chunk))
            {
                if (string.IsNullOrEmpty(chunk))
                    continue;

                lock (streamBuffer)
                {
                    streamBuffer.Append(chunk);
                    ParseBufferedMessages();
                }
            }

            while (shots.TryDequeue(out ShotData shot))
                ShotReceived?.Invoke(shot);

            if (sendReadyHeartbeat && IsConnected && client != null && client.Connected && Time.unscaledTime >= nextHeartbeat)
            {
                SendHeartbeat();
                nextHeartbeat = Time.unscaledTime + Mathf.Max(0.25f, heartbeatIntervalSeconds);
            }
        }

        private void ParseBufferedMessages()
        {
            while (true)
            {
                string json = ExtractJsonObject(streamBuffer);
                if (json == null)
                    return;

                try
                {
                    OpenConnectMessage message = JsonUtility.FromJson<OpenConnectMessage>(json);
                    if (message == null)
                        continue;

                    ShotDataOptions options = message.ShotDataOptions;
                    bool heartbeat = options != null && options.IsHeartBeat;
                    bool optionSaysBall = options != null && options.ContainsBallData;
                    bool optionSaysClub = options != null && options.ContainsClubData;

                    BallData ball = message.BallData;
                    ClubData club = message.ClubData;

                    // Some bridge versions put the R10 ball values at the root.
                    // Promote them into the normal OpenConnect BallData shape.
                    if (ball == null && message.BallSpeed > 0.01)
                    {
                        ball = new BallData
                        {
                            Speed = message.BallSpeed,
                            SpinAxis = message.SpinAxis,
                            TotalSpin = message.TotalSpin,
                            HLA = message.LaunchDirection,
                            VLA = message.LaunchAngle
                        };
                    }

                    bool hasBall = ball != null && ball.Speed > 0.01;
                    bool hasClub = club != null && club.Speed > 0.01;

                    if (heartbeat)
                    {
                        lastPacketSummary = "Heartbeat received";
                        continue;
                    }

                    lastPacketSummary = $"Shot {message.ShotNumber}: ball={hasBall}, club={hasClub}, flags=ball:{optionSaysBall}/club:{optionSaysClub}";

                    if (message.ShotNumber <= 0)
                    {
                        Debug.Log($"[GolfSimZA] R10 diagnostic packet: {lastPacketSummary}");
                        continue;
                    }

                    // ACK every real shot packet so the bridge knows GolfSimZA is alive.
                    SendShotAcknowledgement(message.ShotNumber, hasBall);

                    if (!pendingShots.TryGetValue(message.ShotNumber, out PendingShot pending))
                    {
                        pending = new PendingShot();
                        pendingShots[message.ShotNumber] = pending;
                    }

                    if (hasClub)
                        pending.Club = club;
                    if (hasBall)
                        pending.Ball = ball;

                    // The bridge can send club data first and ball data later.
                    // Only publish once usable BallData exists, then merge any cached
                    // club metrics for the same ShotNumber.
                    if (pending.Ball != null && pending.Ball.Speed > 0.01 && !publishedShots.Contains(message.ShotNumber))
                    {
                        ShotData shot = ConvertShot(pending.Ball, pending.Club);
                        if (shot.IsValid)
                        {
                            publishedShots.Add(message.ShotNumber);
                            shots.Enqueue(shot);
                            pendingShots.Remove(message.ShotNumber);

                            Debug.Log(
                                $"[GolfSimZA] R10 COMPLETE SHOT #{message.ShotNumber}: " +
                                $"{shot.ClubName}, ball {shot.BallSpeedMps:0.00} m/s, " +
                                $"launch {shot.LaunchAngleDeg:0.0}°, HLA {shot.LaunchDirectionDeg:0.0}°, " +
                                $"spin {shot.BackSpinRpm:0} rpm, carry {shot.CarryMeters:0.0} m, " +
                                $"club {(shot.HasClubData ? shot.ClubSpeedMps.ToString("0.00") : "n/a")} m/s.");
                        }
                    }
                    else if (hasClub)
                    {
                        Debug.Log($"[GolfSimZA] R10 CLUB DATA cached for shot #{message.ShotNumber}; waiting for BallData.");
                    }
                    else if (!hasBall)
                    {
                        Debug.Log($"[GolfSimZA] R10 packet for shot #{message.ShotNumber} has no usable BallData yet. " +
                                  "Waiting for the bridge's completed ball packet.");
                    }
                }
                catch (Exception ex)
                {
                    lastPacketSummary = "Invalid OpenConnect JSON";
                    Debug.LogWarning("[GolfSimZA] Ignored invalid R10 OpenConnect message: " + ex.Message);
                }
            }
        }

        private void SendShotAcknowledgement(int shotNumber, bool containsBallData)
        {
            try
            {
                if (client == null || !client.Connected)
                    return;

                string message = containsBallData
                    ? "{\"Code\":200,\"Message\":\"Ball Data received\"}"
                    : "{\"Code\":200,\"Message\":\"Shot data received\"}";

                byte[] data = Encoding.UTF8.GetBytes(message);
                client.GetStream().Write(data, 0, data.Length);
                Debug.Log($"[GolfSimZA] OpenConnect ACK sent for shot #{shotNumber}: {message}");
            }
            catch
            {
                HandleClientDisconnected(client);
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
                    if (c == '{')
                    {
                        start = i;
                        depth = 1;
                    }
                    continue;
                }

                if (inString)
                {
                    if (escaped)
                        escaped = false;
                    else if (c == '\\')
                        escaped = true;
                    else if (c == '"')
                        inString = false;
                    continue;
                }

                if (c == '"')
                    inString = true;
                else if (c == '{')
                    depth++;
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

        private static ShotData ConvertShot(BallData b, ClubData c)
        {
            float ballSpeedMps = (float)(Math.Max(0.0, b.Speed) * 0.44704);
            float clubSpeedMps = c != null ? (float)(Math.Max(0.0, c.Speed) * 0.44704) : 0f;
            float carryMeters = (float)(Math.Max(0.0, b.CarryDistance) * 0.9144);
            float loft = c != null ? (float)c.Loft : 0f;

            return new ShotData
            {
                TimestampUtc = DateTime.UtcNow,
                ClubName = MapClubName(c),
                ClubNumber = MapClubNumber(c),
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

        private static string MapClubName(ClubData club)
        {
            if (club == null || club.Loft <= 0.1)
                return "R10 Club";

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

        private static int MapClubNumber(ClubData club)
        {
            if (club == null || club.Loft <= 0.1)
                return 0;

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
                if (client == null || !client.Connected)
                    return;

                // Use the exact OpenConnect v1 field spelling documented by GSPro.
                string heartbeat =
                    "{\"DeviceID\":\"GolfSimZA\",\"Units\":\"Yards\",\"ShotNumber\":0," +
                    "\"APIversion\":\"1\",\"ShotDataOptions\":{" +
                    "\"ContainsBallData\":false,\"ContainsClubData\":false," +
                    "\"LaunchMonitorIsReady\":true,\"LaunchMonitorBallDetected\":true," +
                    "\"IsHeartBeat\":true}}";

                byte[] data = Encoding.UTF8.GetBytes(heartbeat);
                client.GetStream().Write(data, 0, data.Length);
            }
            catch
            {
                HandleClientDisconnected(client);
            }
        }

        public void Disconnect()
        {
            stopping = true;
            IsConnected = false;
            IsListening = false;

            try { listener?.Stop(); } catch { }
            try { client?.Close(); } catch { }

            listener = null;
            client = null;
            pendingShots.Clear();
            publishedShots.Clear();
            lock (streamBuffer)
                streamBuffer.Clear();
        }

        private void OnDestroy()
        {
            Disconnect();
        }
    }
}
