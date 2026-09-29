using System;
using System.Collections.Generic;
using GolfSimZA.Core;
using GolfSimZA.Courses;
using GolfSimZA.Physics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GolfSimZA.UI
{
    /// <summary>
    /// Driving range screen: range settings (target distance, fairway and green width,
    /// randomizer) top-left, wind top-centre, shot data tiles top-right, club selector
    /// bottom-left, recent shots bottom-right, flag distance marker on the range, a gold aim
    /// target the player drags (direction and distance) and the selected club's shot dispersion
    /// circle.
    /// On a course this component switches itself off (RoundGameplayUI runs the round).
    /// </summary>
    public sealed partial class ModernGolfSimUI : MonoBehaviour
    {
        private const float ResetDelay = 2.2f;

        [SerializeField] private SimulatorController simulator;
        [SerializeField] private BallFlightSimulator flight;

        private FlightPresentation presentation;
        private ShotTracer tracer;
        private RangeEnvironment range;
        private bool isRange;
        private bool panelOpen;
        private int lastShotCount;
        private bool wasInFlight;
        private float resetAt = -1f;
        private float lastProximity = -1f;
        private string status = "READY";

        private float targetSlider, widthSlider, greenSlider;
        private bool randomizer;

        private readonly List<ShotData> recent = new List<ShotData>();
        private AimPointer aimPointer;
        private bool aimMoved;
        private GolfSimZA.Visual.ShotDispersion dispersion;
        private GUIStyle pill, flagBox, flagValue, flagSub, sliderValue, markerLabel, small, sliderTrack, sliderThumb, switchOn, switchOff;

        private void Awake()
        {
            simulator = simulator != null ? simulator : GetComponent<SimulatorController>();
            flight = flight != null ? flight : (simulator != null ? simulator.BallFlight : FindFirstObjectByType<BallFlightSimulator>());
            presentation = GetComponent<FlightPresentation>();
            tracer = GetComponent<ShotTracer>();

            isRange = IsRangeSession();
            RoundGameplayUI round = FindFirstObjectByType<RoundGameplayUI>();
            if (!isRange)
            {
                // Playing a course: the round HUD runs the hole, scoring and course.
                enabled = false;
                return;
            }
            if (round != null) round.enabled = false;
        }

        private void Start()
        {
            if (!isRange) return;

            foreach (string name in new[] { "CourseGround", "Fairway", "HoleGreen", "HolePin", "GolfSimZA_DrivingRange" })
            {
                GameObject old = GameObject.Find(name);
                if (old != null) old.SetActive(false);
            }

            AppSettings s = AppSettings.Current;
            targetSlider = s.rangeTargetMeters;
            widthSlider = s.rangeFairwayWidth;
            greenSlider = s.rangeGreenWidth;
            randomizer = s.rangeRandomizer;

            GameObject root = new GameObject("GolfSimZA_Range");
            range = root.AddComponent<RangeEnvironment>();
            range.Build(targetSlider, widthSlider, greenSlider);
            CourseScenery.ImproveRange(root, Camera.main, widthSlider);
            Wind.NewHole(Vector3.forward);
            aimPointer = gameObject.AddComponent<AimPointer>();
            aimPointer.MaxDistance = RangeEnvironment.MaxTarget + 60f;
            aimPointer.CanInteract = () => !GameMenuOverlay.IsOpen && flight != null && !flight.IsInFlight && !panelOpen;
            aimPointer.Moved += p => { aimMoved = true; ApplyRangeAim(false); };
            dispersion = GolfSimZA.Visual.ShotDispersion.Attach(gameObject, flight);
            if (simulator != null)
            {
                simulator.AllowShotDuringFlight = true;
                simulator.BeforeLaunch += PutBallOnMat;
            }
            // Range view: low behind the mat like a golfer's eye line, looking down the range to the mountains.
            presentation?.ConfigureAddressView(2.2f, 4.6f, 70f, 1.4f);
            if (Camera.main != null) Camera.main.fieldOfView = 50f;
            SetUpRangeViews(root);
            ResetBall(true);
        }

        /// <summary>True when the selected "course" is the practice range.</summary>
        public static bool IsRangeSession()
        {
            if (CourseSession.IsImportedCourse) return false;
            string name = CourseSession.CourseName ?? string.Empty;
            return name.IndexOf("range", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("practice", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ResetBall(bool snapCamera)
        {
            if (flight == null || range == null) return;
            flight.PlaceBall(range.TeePosition);
            flight.SetHole(range.TargetPosition, range.GreenWidth * 0.5f + 1f);
            // The aim target stays where the player put it; otherwise it sits on the flag.
            aimPointer?.Set(range.TeePosition, aimMoved ? aimPointer.Point : range.TargetPosition);
            ApplyRangeAim(snapCamera);
            tracer?.Clear();
            status = "READY";
            GolfSimAudio.PlayReady();
        }

        /// <summary>
        /// Driving range: every shot is hit from the mat - even when the player hits again before
        /// the last ball has been put back - never from where the previous ball stopped.
        /// </summary>
        private void PutBallOnMat(ShotData shot)
        {
            if (!isRange || flight == null || range == null) return;
            resetAt = -1f;
            wasInFlight = false;
            flight.PlaceBall(range.TeePosition);
            flight.SetHole(range.TargetPosition, range.GreenWidth * 0.5f + 1f);
            if (aimPointer != null) aimPointer.Set(range.TeePosition, aimMoved ? aimPointer.Point : range.TargetPosition);
            ApplyRangeAim(true);
            tracer?.Clear();
            presentation?.HideLandingMarker();
        }

        /// <summary>Turns the shot towards the aim target (or the flag).</summary>
        private void ApplyRangeAim(bool snapCamera)
        {
            if (flight == null || range == null) return;
            Vector3 target = aimPointer != null && aimPointer.Visible ? aimPointer.Point : range.TargetPosition;
            Vector3 aim = target - range.TeePosition;
            aim.y = 0f;
            if (aim.sqrMagnitude < 0.01f) aim = Vector3.forward;
            flight.AimYawDegrees = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
            presentation?.SetAddress(range.TeePosition, aim, snapCamera);
        }

        /// <summary>← / → turn, ↑ / ↓ further / closer (Shift = bigger steps), Home = back on the flag.</summary>
        private void HandleRangeAimKeys()
        {
            Keyboard k = Keyboard.current;
            if (k == null || aimPointer == null || !aimPointer.Visible) return;
            bool shift = k.shiftKey.isPressed;
            float turn = 0f, move = 0f;
            if (k.leftArrowKey.wasPressedThisFrame) turn = shift ? -5f : -1f;
            if (k.rightArrowKey.wasPressedThisFrame) turn = shift ? 5f : 1f;
            if (k.upArrowKey.wasPressedThisFrame) move = shift ? 10f : 2f;
            if (k.downArrowKey.wasPressedThisFrame) move = shift ? -10f : -2f;
            if (k.homeKey.wasPressedThisFrame) { aimMoved = false; aimPointer.Set(range.TeePosition, range.TargetPosition); ApplyRangeAim(false); return; }
            if (turn == 0f && move == 0f) return;
            Vector3 d = aimPointer.Point - range.TeePosition;
            d.y = 0f;
            float distance = Mathf.Clamp(d.magnitude + move, 5f, aimPointer.MaxDistance);
            Vector3 dir = Quaternion.Euler(0f, turn, 0f) * (d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward);
            aimPointer.Set(range.TeePosition, range.TeePosition + dir * distance);
            aimMoved = true;
            ApplyRangeAim(false);
        }

        private void OnDestroy()
        {
            if (simulator != null)
            {
                simulator.BeforeLaunch -= PutBallOnMat;
                simulator.AllowShotDuringFlight = false;
            }
        }

        private void Update()
        {
            if (!isRange || simulator == null || flight == null) return;

            if (!GameMenuOverlay.IsOpen && !flight.IsInFlight)
            {
                ClubBar.HandleKeys();
                HandleRangeAimKeys();
            }
            if (aimPointer != null)
            {
                if (flight.IsInFlight || GameMenuOverlay.IsOpen) { if (aimPointer.Visible) aimPointer.Hide(); }
                else if (!aimPointer.Visible && status == "READY") { aimPointer.Set(range.TeePosition, aimMoved ? aimPointer.Point : range.TargetPosition); }
            }
            dispersion?.Show(range.TeePosition, flight.AimYawDegrees, !GameMenuOverlay.IsOpen);

            int count = simulator.TotalShots;
            if (count > lastShotCount)
            {
                lastShotCount = count;
                status = "BALL IN FLIGHT";
                resetAt = -1f;
            }

            bool inFlight = flight.IsInFlight;
            if (wasInFlight && !inFlight)
            {
                ShotData shot = simulator.LastShot;
                RecordRangeShot(shot);
                recent.Insert(0, shot);
                if (recent.Count > 6) recent.RemoveAt(recent.Count - 1);
                Vector3 d = flight.Ball.position - range.TargetPosition;
                d.y = 0f;
                lastProximity = d.magnitude;
                status = "SHOT COMPLETE";
                resetAt = Time.unscaledTime + ResetDelay;
            }
            wasInFlight = inFlight;

            if (resetAt > 0f && Time.unscaledTime >= resetAt)
            {
                resetAt = -1f;
                if (randomizer)
                {
                    range.RandomTarget(targetSlider);
                    mapDirty = true;
                    aimMoved = false;
                    lastProximity = -1f;
                    Wind.NewHole(Vector3.forward);
                }
                ResetBall(false);
            }

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame && !inFlight && !GameMenuOverlay.IsOpen)
                ResetBall(true);
        }

        // ---------------------------------------------------------------- GUI

        private void EnsureStyles()
        {
            GolfSimTheme.Ensure();
            if (pill != null) return;
            pill = new GUIStyle(GUI.skin.box) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, border = new RectOffset(10, 10, 10, 10), normal = { background = GolfSimTheme.Rounded(new Color(0.03f, 0.07f, 0.09f, 0.86f), 10), textColor = Color.white } };
            flagBox = new GUIStyle(GUI.skin.box) { border = new RectOffset(8, 8, 8, 8), normal = { background = GolfSimTheme.Rounded(new Color(0.03f, 0.07f, 0.09f, 0.90f), 8, Color.white) } };
            flagValue = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            flagSub = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = GolfSimTheme.Gold } };
            sliderValue = new GUIStyle(GUI.skin.box) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, border = new RectOffset(6, 6, 6, 6), normal = { background = GolfSimTheme.Rounded(new Color(0.02f, 0.05f, 0.06f, 1f), 6), textColor = Color.white } };
            markerLabel = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 1f, 1f, 0.85f) } };
            small = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = GolfSimTheme.Muted } };
            sliderTrack = new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = 8f, margin = new RectOffset(0, 0, 0, 0), border = new RectOffset(4, 4, 4, 4), normal = { background = GolfSimTheme.Rounded(new Color(1f, 1f, 1f, 0.18f), 4) } };
            sliderThumb = new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedWidth = 22f, fixedHeight = 22f, margin = new RectOffset(0, 0, -7, 0), border = new RectOffset(10, 10, 10, 10), normal = { background = GolfSimTheme.Rounded(GolfSimTheme.Accent, 10, Color.white) }, hover = { background = GolfSimTheme.Rounded(GolfSimTheme.AccentHover, 10, Color.white) }, active = { background = GolfSimTheme.Rounded(GolfSimTheme.Gold, 10, Color.white) } };
            switchOn = new GUIStyle(GolfSimTheme.AccentButton) { fixedHeight = 30f, fontSize = 13 };
            switchOff = new GUIStyle(GolfSimTheme.Button) { fixedHeight = 30f, fontSize = 13 };
        }

        private void OnGUI()
        {
            if (!isRange || range == null) return;
            EnsureStyles();

            AppSettings settings = AppSettings.Current;
            bool flying = flight != null && flight.IsInFlight;
            if (flying && settings.hideUiOnShot) return;

            DrawWorldLabels();
            DrawRangeHud(settings, flying);
        }

        private void DrawWind(float y)
        {
            Rect r = new Rect(Screen.width * 0.5f - 115f, y, 230f, 38f);
            GUI.Box(r, GUIContent.none, pill);
            Camera cam = Camera.main;
            Vector3 forward = cam != null ? cam.transform.forward : Vector3.forward;
            bool calm = Wind.SpeedMps < 0.1f;
            if (!calm)
            {
                Rect arrow = new Rect(r.x + 12f, r.y + 5f, 28f, 28f);
                Matrix4x4 saved = GUI.matrix;
                GUIUtility.RotateAroundPivot(Wind.ArrowAngle(forward), arrow.center);
                GUI.Label(arrow, "▲", new GUIStyle(markerLabel) { fontSize = 20, normal = { textColor = GolfSimTheme.Gold } });
                GUI.matrix = saved;
            }
            GUI.Label(new Rect(r.x + 44f, r.y, r.width - 52f, r.height), calm ? "NO WIND" : Wind.SpeedText() + "   " + Wind.Describe(forward), new GUIStyle(small) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } });
        }

        private void DrawWorldLabels()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            // Distance numbers on both sides of the range. Far lines bunch up near the horizon,
            // so a line is only labelled when it is far enough (on screen) from the last label.
            float lastY = float.MaxValue;
            if (AppSettings.Current.rangeDistanceLines)
            foreach (float d in range.MarkerDistances)
            {
                Vector3 left = new Vector3(-range.FairwayWidth * 0.36f, 0.2f, d);
                Vector3 right = new Vector3(range.FairwayWidth * 0.36f, 0.2f, d);
                if (!ToScreen(cam, left, out Vector2 pl) || !ToScreen(cam, right, out Vector2 pr)) continue;
                if (lastY - pl.y < 22f || pr.x - pl.x < 90f) continue;
                lastY = pl.y;
                int size = Mathf.RoundToInt(Mathf.Clamp((pr.x - pl.x) * 0.05f, 11f, 18f));
                markerLabel.fontSize = size;
                string text = Units.Distance(d).ToString("0");
                GUI.Label(new Rect(pl.x - 40f, pl.y - 12f, 80f, 24f), text, markerLabel);
                GUI.Label(new Rect(pr.x - 40f, pr.y - 12f, 80f, 24f), text, markerLabel);
            }

            // Aim target distance.
            if (aimPointer != null && aimPointer.Visible && Vector3.Distance(aimPointer.Point, range.TargetPosition) > 3f && ToScreen(cam, aimPointer.Point + Vector3.up * 1.2f, out Vector2 ap))
            {
                string t = Units.DistanceText(aimPointer.Distance);
                var st = new GUIStyle(flagSub) { fontSize = 14 };
                Vector2 sz = st.CalcSize(new GUIContent(t));
                Rect r = new Rect(ap.x - sz.x * 0.5f - 6f, ap.y - sz.y - 6f, sz.x + 12f, sz.y + 4f);
                GUI.Box(r, GUIContent.none, flagBox);
                GUI.Label(r, t, st);
            }

            if (ToScreen(cam, range.TargetPosition + Vector3.up * 2.7f, out Vector2 flag))
            {
                Rect box = new Rect(flag.x - 44f, flag.y - 64f, 88f, 54f);
                GUI.Box(box, GUIContent.none, flagBox);
                GUI.Label(new Rect(box.x, box.y + 3f, box.width, 28f), Units.Distance(range.TargetDistance).ToString("0"), flagValue);
                string proximity = lastProximity >= 0f ? ProximityText(lastProximity) : Units.DistanceUnit.ToUpperInvariant();
                GUI.Label(new Rect(box.x, box.y + 30f, box.width, 18f), proximity, flagSub);
                GUI.DrawTexture(new Rect(flag.x - 1f, box.yMax, 2f, 10f), GolfSimTheme.White);
            }
        }

        private static string ProximityText(float meters)
        {
            if (meters < 1f) return (meters * 100f).ToString("0") + " cm";
            return Units.DistanceText(meters, "0.0");
        }

        private static bool ToScreen(Camera cam, Vector3 world, out Vector2 gui)
        {
            Vector3 s = cam.WorldToScreenPoint(world);
            gui = new Vector2(s.x, Screen.height - s.y);
            return s.z > 0.5f && s.x > -50f && s.x < Screen.width + 50f && s.y > -50f && s.y < Screen.height + 50f;
        }

        private void DrawRangePanel(float x, float y)
        {
            if (GUI.Button(new Rect(x, y, 230f, 36f), (panelOpen ? "▲ " : "▼ ") + "RANGE  •  " + Units.DistanceText(range.TargetDistance), GolfSimTheme.SmallButton))
                panelOpen = !panelOpen;
            if (!panelOpen) return;

            Rect panel = new Rect(x, y + 42f, 420f, 388f);
            GUI.Box(panel, GUIContent.none, GolfSimTheme.Overlay);
            float px = panel.x + 18f, py = panel.y + 14f, w = panel.width - 36f;

            GUI.Label(new Rect(px, py, w, 22f), "DRIVING RANGE", GolfSimTheme.Heading);
            py += 34f;
            targetSlider = SliderRow(px, ref py, w, "TARGET DISTANCE", targetSlider, RangeEnvironment.MinTarget, RangeEnvironment.MaxTarget, Units.Distance(targetSlider).ToString("0"));
            widthSlider = SliderRow(px, ref py, w, "RANGE WIDTH", widthSlider, 20f, 100f, Units.Distance(widthSlider).ToString("0"));
            greenSlider = SliderRow(px, ref py, w, "GREEN WIDTH", greenSlider, 5f, 30f, Units.Distance(greenSlider).ToString("0"));

            GUI.Label(new Rect(px, py + 6f, w - 100f, 20f), "RANDOM TARGET AFTER EVERY SHOT", GolfSimTheme.Label);
            if (GUI.Button(new Rect(px + w - 78f, py, 78f, 30f), randomizer ? "ON" : "OFF", randomizer ? switchOn : switchOff))
                randomizer = !randomizer;
            py += 44f;
            AppSettings st = AppSettings.Current;
            GUI.Label(new Rect(px, py + 6f, w - 100f, 20f), "DISTANCE LINES ON THE FAIRWAY", GolfSimTheme.Label);
            if (GUI.Button(new Rect(px + w - 78f, py, 78f, 30f), st.rangeDistanceLines ? "ON" : "OFF", st.rangeDistanceLines ? switchOn : switchOff))
            {
                st.rangeDistanceLines = !st.rangeDistanceLines;
                st.Save();
                range.Build(range.TargetDistance, range.FairwayWidth, range.GreenWidth);
                mapDirty = true;
            }
            py += 44f;
            GUI.Label(new Rect(px, py + 6f, w - 100f, 20f), "TARGET VIEW (small live picture)", GolfSimTheme.Label);
            if (GUI.Button(new Rect(px + w - 78f, py, 78f, 30f), st.rangeTargetCam ? "ON" : "OFF", st.rangeTargetCam ? switchOn : switchOff))
            {
                st.rangeTargetCam = !st.rangeTargetCam;
                st.Save();
            }
            py += 44f;
            if (GUI.Button(new Rect(px, py, w * 0.48f, 40f), "CANCEL", GolfSimTheme.Button))
            {
                targetSlider = AppSettings.Current.rangeTargetMeters;
                widthSlider = AppSettings.Current.rangeFairwayWidth;
                greenSlider = AppSettings.Current.rangeGreenWidth;
                randomizer = AppSettings.Current.rangeRandomizer;
                panelOpen = false;
            }
            if (GUI.Button(new Rect(px + w * 0.52f, py, w * 0.48f, 40f), "APPLY", GolfSimTheme.AccentButton))
            {
                AppSettings s = AppSettings.Current;
                s.rangeTargetMeters = Mathf.Round(targetSlider);
                s.rangeFairwayWidth = Mathf.Round(widthSlider);
                s.rangeGreenWidth = Mathf.Round(greenSlider);
                s.rangeRandomizer = randomizer;
                s.Save();
                range.Build(s.rangeTargetMeters, s.rangeFairwayWidth, s.rangeGreenWidth);
                CourseScenery.ImproveRange(range.gameObject, Camera.main, s.rangeFairwayWidth);
                mapDirty = true;
                aimMoved = false;
                lastProximity = -1f;
                ResetBall(true);
                panelOpen = false;
            }
        }

        private float SliderRow(float x, ref float y, float w, string title, float value, float min, float max, string shown)
        {
            GUI.Label(new Rect(x, y, w, 18f), title, GolfSimTheme.Label);
            float result = GUI.HorizontalSlider(new Rect(x, y + 29f, w - 96f, 8f), value, min, max, sliderTrack, sliderThumb);
            GUI.Box(new Rect(x + w - 78f, y + 16f, 78f, 34f), shown, sliderValue);
            y += 60f;
            return result;
        }

    }
}
