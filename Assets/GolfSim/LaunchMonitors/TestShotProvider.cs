using System;
using GolfSimZA.Core;
using GolfSimZA.LaunchMonitors;
using UnityEngine;

namespace GolfSimZA.LaunchMonitors
{
    public sealed class TestShotProvider : MonoBehaviour, ILaunchMonitorAdapter
    {
        [SerializeField] private KeyCode testShotKey = KeyCode.Space;
        [SerializeField] private float ballSpeedMps = 67.0f;

        public string DeviceName => "Development Test Shot";
        public bool IsConnected { get; private set; }
        public event Action<ShotData> ShotReceived;

        public bool TryConnect()
        {
            IsConnected = true;
            return true;
        }

        public void Disconnect()
        {
            IsConnected = false;
        }

        private void Awake()
        {
            TryConnect();
        }

        private void Update()
        {
            if (IsConnected && Input.GetKeyDown(testShotKey))
                ShotReceived?.Invoke(ShotData.TestDriver(ballSpeedMps));
        }
    }
}
