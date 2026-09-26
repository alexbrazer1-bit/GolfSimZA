using System;
using System.Net.Sockets;
using System.Text;

namespace GolfSimZA.R10Bridge
{
    /// <summary>
    /// Output-only adapter for an already-decoded Garmin R10 bridge.
    /// It does not communicate with the R10 itself. Call SendShot() when the
    /// bridge has a completed BallMetrics + ClubMetrics result.
    /// </summary>
    public sealed class GspR10OpenConnectOutputAdapter : IDisposable
    {
        private readonly string host;
        private readonly int port;
        private TcpClient client;
        private NetworkStream stream;
        private int shotNumber = 1;

        public bool IsConnected => client != null && client.Connected;

        public GspR10OpenConnectOutputAdapter(string host = "127.0.0.1", int port = 921)
        {
            this.host = host;
            this.port = port;
        }

        public bool Connect()
        {
            Disconnect();
            try
            {
                client = new TcpClient();
                client.NoDelay = true;
                client.Connect(host, port);
                stream = client.GetStream();
                return true;
            }
            catch
            {
                Disconnect();
                return false;
            }
        }

        public bool SendShot(
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
            if (!IsConnected && !Connect())
                return false;

            string json = OpenConnectShotBuilder.BuildShot(
                shotNumber++,
                ballSpeedMph,
                spinAxisDeg,
                totalSpinRpm,
                hlaDeg,
                vlaDeg,
                clubSpeedMph,
                attackAngleDeg,
                faceToTargetDeg,
                clubPathDeg,
                loftDeg,
                carryYards);

            byte[] bytes = Encoding.UTF8.GetBytes(json);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
            return true;
        }

        public void ResetShotNumber(int nextShotNumber = 1)
        {
            shotNumber = Math.Max(1, nextShotNumber);
        }

        public void Disconnect()
        {
            try { stream?.Close(); } catch { }
            try { client?.Close(); } catch { }
            stream = null;
            client = null;
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
