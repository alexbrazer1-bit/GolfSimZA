using UnityEngine;

namespace GolfSimZA.Core
{
    /// <summary>
    /// Wind for the current hole or range session (Settings → GAME → REALISM).
    /// The ball flight uses the air speed relative to the ball, so a headwind balloons
    /// and shortens a shot and a crosswind moves it sideways.
    /// </summary>
    public static class Wind
    {
        public static readonly string[] ModeNames = { "OFF", "LIGHT", "MODERATE", "STRONG", "CUSTOM" };

        /// <summary>Wind velocity in world space (metres per second, horizontal), the direction the air moves TO.</summary>
        public static Vector3 Velocity { get; private set; }
        public static float SpeedMps => Velocity.magnitude;

        /// <summary>
        /// Picks the wind for a hole. referenceForward is the tee-to-green direction; a custom
        /// wind is set relative to it (0° = from behind the green, i.e. a headwind).
        /// </summary>
        public static void NewHole(Vector3 referenceForward)
        {
            referenceForward.y = 0f;
            if (referenceForward.sqrMagnitude < 0.0001f) referenceForward = Vector3.forward;
            referenceForward.Normalize();

            AppSettings s = AppSettings.Current;
            float speed;
            float fromDeg; // relative to referenceForward, clockwise
            switch (s.windMode)
            {
                case 1: speed = Random.Range(0.5f, 3f); fromDeg = Random.Range(0f, 360f); break;
                case 2: speed = Random.Range(3f, 6f); fromDeg = Random.Range(0f, 360f); break;
                case 3: speed = Random.Range(6f, 10f); fromDeg = Random.Range(0f, 360f); break;
                case 4: speed = Mathf.Clamp(s.windCustomMps, 0f, 20f); fromDeg = s.windCustomFromDeg; break;
                default: speed = 0f; fromDeg = 0f; break;
            }

            // "From" direction: 0° = the wind comes from straight ahead (from the green).
            Vector3 from = Quaternion.Euler(0f, fromDeg, 0f) * referenceForward;
            Velocity = -from * speed;
        }

        public static void Clear() => Velocity = Vector3.zero;

        /// <summary>Wind speed at a height above ground (weaker near the ground, log profile).</summary>
        public static Vector3 At(float heightAboveGround)
        {
            if (Velocity.sqrMagnitude < 0.0001f) return Vector3.zero;
            // Wind is quoted at 10 m; roughness length about 3 cm on short grass.
            float h = Mathf.Clamp(heightAboveGround, 0.2f, 200f);
            float factor = Mathf.Log(h / 0.03f) / Mathf.Log(10f / 0.03f);
            return Velocity * Mathf.Clamp(factor, 0.15f, 1.35f);
        }

        /// <summary>Angle of the wind arrow relative to a view direction (0 = blowing straight away from the camera).</summary>
        public static float ArrowAngle(Vector3 viewForward)
        {
            viewForward.y = 0f;
            if (Velocity.sqrMagnitude < 0.0001f || viewForward.sqrMagnitude < 0.0001f) return 0f;
            return Vector3.SignedAngle(viewForward.normalized, Velocity.normalized, Vector3.up);
        }

        /// <summary>"HEAD", "TAIL", "L→R" style description relative to a view direction.</summary>
        public static string Describe(Vector3 viewForward)
        {
            if (SpeedMps < 0.1f) return "CALM";
            float a = ArrowAngle(viewForward);
            float abs = Mathf.Abs(a);
            if (abs <= 30f) return "TAIL";
            if (abs >= 150f) return "HEAD";
            string cross = a > 0f ? "L→R" : "R→L";
            if (abs < 70f) return "TAIL " + cross;
            if (abs > 110f) return "HEAD " + cross;
            return cross;
        }

        public static string SpeedText()
        {
            return Units.Metric ? SpeedMps.ToString("0.0") + " m/s" : (SpeedMps * 2.2369363f).ToString("0.0") + " mph";
        }
    }
}
