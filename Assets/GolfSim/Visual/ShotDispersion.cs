using System;
using System.Collections.Generic;
using System.Globalization;
using GolfSimZA.Core;
using GolfSimZA.Physics;
using UnityEngine;

namespace GolfSimZA.Visual
{
    /// <summary>
    /// Driving range SHOT DISPERSION per club: every shot's landing spot (carry) is saved for the
    /// player and club, relative to where they aimed. The range shows the selected club's
    /// dispersion circle (an ellipse holding about two thirds of the shots: long/short and
    /// left/right spread) around the average landing spot, plus a dot for every saved shot.
    /// </summary>
    public sealed class ShotDispersion : MonoBehaviour
    {
        public const int MaxShots = 40;
        private const int Segments = 64;
        private const float Spread = 1.5f; // ellipse size in standard deviations

        private BallFlightSimulator flight;
        private LineRenderer ellipse, centreMark;
        private readonly List<LineRenderer> dots = new List<LineRenderer>();
        private string pendingClub;
        private Vector3 pendingOrigin;
        private float pendingYaw;
        private bool pending;
        private string shownKey;
        private Vector3 shownOrigin;
        private float shownYaw = float.NaN;

        /// <summary>Stats of the club shown now (for the range panel).</summary>
        public Stats Current { get; private set; }

        public struct Stats
        {
            public string Club, Player;
            public int Shots;
            public float MeanCarry, MeanSide, CarrySpread, SideSpread;
        }

        public static ShotDispersion Attach(GameObject host, BallFlightSimulator flight)
        {
            ShotDispersion d = host.GetComponent<ShotDispersion>();
            if (d == null) d = host.AddComponent<ShotDispersion>();
            d.Hook(flight);
            return d;
        }

        private void Hook(BallFlightSimulator f)
        {
            if (flight != null) { flight.Launched -= OnLaunched; flight.Landed -= OnLanded; }
            flight = f;
            if (flight != null) { flight.Launched += OnLaunched; flight.Landed += OnLanded; }
        }

        private void OnDestroy() => Hook(null);

        private void Awake()
        {
            ellipse = MakeLine("GolfSimZA_Dispersion", Segments + 1, new Color(0.35f, 0.85f, 1f, 0.95f));
            centreMark = MakeLine("GolfSimZA_DispersionCentre", 5, new Color(0.35f, 0.85f, 1f, 0.95f));
            ellipse.gameObject.SetActive(false);
            centreMark.gameObject.SetActive(false);
        }

        private LineRenderer MakeLine(string name, int points, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.layer = GroundProbe.IgnoreRaycastLayer;
            LineRenderer line = go.AddComponent<LineRenderer>();
            Shader shader = Shader.Find("Sprites/Default");
            line.material = new Material(shader != null ? shader : Shader.Find("Unlit/Color"));
            line.positionCount = points;
            line.useWorldSpace = true;
            line.startColor = line.endColor = color;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void OnLaunched(ShotData shot)
        {
            pendingClub = string.IsNullOrWhiteSpace(shot.ClubName) ? ActiveClub.Resolve() : shot.ClubName;
            pendingOrigin = flight.LaunchPosition;
            pendingYaw = flight.AimYawDegrees;
            pending = !ActiveClub.IsPutter(pendingClub);
        }

        private void OnLanded(float speed)
        {
            if (!pending || flight == null) return;
            pending = false;
            Vector3 d = flight.LandingPosition - pendingOrigin;
            d.y = 0f;
            Quaternion toAim = Quaternion.Euler(0f, -pendingYaw, 0f);
            Vector3 local = toAim * d; // x = right of the aim line, z = along it
            if (local.z < 2f) return;  // topped / mishit - not a real carry
            Add(ActiveClub.Player, pendingClub, new Vector2(local.x, local.z));
            shownKey = null; // redraw
        }

        // ------------------------------------------------------------ Storage (per player and club)

        private static string Key(string player, string club)
        {
            string Clean(string v)
            {
                char[] c = (v ?? "").Trim().ToCharArray();
                for (int i = 0; i < c.Length; i++) if (!char.IsLetterOrDigit(c[i])) c[i] = '_';
                return new string(c);
            }
            return "GolfSimZA.Dispersion." + Clean(player) + "." + Clean(club);
        }

        public static List<Vector2> Load(string player, string club)
        {
            var list = new List<Vector2>();
            string text = PlayerPrefs.GetString(Key(player, club), "");
            foreach (string part in text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] xy = part.Split(',');
                if (xy.Length == 2 && float.TryParse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                    && float.TryParse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                    list.Add(new Vector2(x, z));
            }
            return list;
        }

        public static void Add(string player, string club, Vector2 landing)
        {
            List<Vector2> list = Load(player, club);
            list.Add(landing);
            while (list.Count > MaxShots) list.RemoveAt(0);
            var parts = new List<string>();
            foreach (Vector2 v in list) parts.Add(v.x.ToString("0.0", CultureInfo.InvariantCulture) + "," + v.y.ToString("0.0", CultureInfo.InvariantCulture));
            PlayerPrefs.SetString(Key(player, club), string.Join(";", parts));
            PlayerPrefs.Save();
        }

        public static void Clear(string player, string club)
        {
            PlayerPrefs.DeleteKey(Key(player, club));
            PlayerPrefs.Save();
        }

        public void ClearCurrent()
        {
            Clear(ActiveClub.Player, ActiveClub.Resolve());
            shownKey = null;
        }

        public static Stats Compute(string player, string club, List<Vector2> shots)
        {
            var st = new Stats { Club = club, Player = player, Shots = shots.Count };
            if (shots.Count == 0) return st;
            foreach (Vector2 v in shots) { st.MeanSide += v.x; st.MeanCarry += v.y; }
            st.MeanSide /= shots.Count;
            st.MeanCarry /= shots.Count;
            if (shots.Count > 1)
            {
                float sx = 0f, sz = 0f;
                foreach (Vector2 v in shots) { sx += (v.x - st.MeanSide) * (v.x - st.MeanSide); sz += (v.y - st.MeanCarry) * (v.y - st.MeanCarry); }
                st.SideSpread = Mathf.Sqrt(sx / (shots.Count - 1));
                st.CarrySpread = Mathf.Sqrt(sz / (shots.Count - 1));
            }
            return st;
        }

        // ------------------------------------------------------------ Drawing

        /// <summary>Shows the selected club's dispersion around the aim line from origin (call every frame).</summary>
        public void Show(Vector3 origin, float aimYaw, bool visible)
        {
            if (!visible || !AppSettings.Current.showDispersion)
            {
                SetVisible(false, 0);
                return;
            }
            string player = ActiveClub.Player, club = ActiveClub.Resolve();
            string key = player + "|" + club;
            if (key == shownKey && Mathf.Approximately(aimYaw, shownYaw) && (origin - shownOrigin).sqrMagnitude < 0.01f && ellipse.gameObject.activeSelf == (Current.Shots > 1)) return;
            shownKey = key;
            shownYaw = aimYaw;
            shownOrigin = origin;

            List<Vector2> shots = ActiveClub.IsPutter(club) ? new List<Vector2>() : Load(player, club);
            Current = Compute(player, club, shots);
            Quaternion aim = Quaternion.Euler(0f, aimYaw, 0f);
            Vector3 forward = aim * Vector3.forward, right = aim * Vector3.right;

            // Dots: one per saved shot.
            while (dots.Count < shots.Count) dots.Add(MakeLine("GolfSimZA_DispersionDot", 13, new Color(1f, 1f, 1f, 0.85f)));
            for (int i = 0; i < dots.Count; i++)
            {
                bool on = i < shots.Count;
                dots[i].gameObject.SetActive(on);
                if (!on) continue;
                Vector3 p = origin + right * shots[i].x + forward * shots[i].y;
                Circle(dots[i], p, 0.7f, 0.7f, forward, right, 0.08f);
                dots[i].widthMultiplier = 0.25f;
            }

            bool enough = shots.Count > 1;
            ellipse.gameObject.SetActive(enough);
            centreMark.gameObject.SetActive(shots.Count > 0);
            if (shots.Count == 0) return;
            Vector3 centre = origin + right * Current.MeanSide + forward * Current.MeanCarry;
            if (enough)
            {
                float a = Mathf.Max(2f, Current.SideSpread * Spread);
                float b = Mathf.Max(2f, Current.CarrySpread * Spread);
                Circle(ellipse, centre, a, b, forward, right, 0.1f);
                ellipse.widthMultiplier = Mathf.Clamp(Current.MeanCarry * 0.004f, 0.25f, 0.9f);
            }
            float c = 1.6f;
            Vector3[] pts = { centre - right * c, centre + right * c, centre, centre - forward * c, centre + forward * c };
            for (int i = 0; i < pts.Length; i++) { pts[i].y = GroundProbe.HeightAt(pts[i]) + 0.1f; centreMark.SetPosition(i, pts[i]); }
            centreMark.widthMultiplier = 0.3f;
        }

        private void SetVisible(bool on, int dotCount)
        {
            if (ellipse.gameObject.activeSelf) ellipse.gameObject.SetActive(on);
            if (centreMark.gameObject.activeSelf) centreMark.gameObject.SetActive(on);
            foreach (LineRenderer d in dots) if (d.gameObject.activeSelf) d.gameObject.SetActive(false);
            shownKey = null;
        }

        private static void Circle(LineRenderer line, Vector3 centre, float sideRadius, float lengthRadius, Vector3 forward, Vector3 right, float lift)
        {
            int n = line.positionCount;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1) * Mathf.PI * 2f;
                Vector3 p = centre + right * (Mathf.Cos(t) * sideRadius) + forward * (Mathf.Sin(t) * lengthRadius);
                p.y = GroundProbe.HeightAt(p) + lift;
                line.SetPosition(i, p);
            }
        }
    }
}
