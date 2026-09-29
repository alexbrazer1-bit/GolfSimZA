using System.Collections.Generic;
using GolfSimZA.Core;
using GolfSimZA.Physics;
using UnityEngine;

namespace GolfSimZA.UI
{
    /// <summary>
    /// Driving range HUD in the style of a tour launch-monitor practice screen:
    /// header (player, range settings) and shot data tiles top-left, the session panel for the
    /// selected club bottom-left (average, target hits, every shot), club and target distance at
    /// the bottom, and on the right a live target view plus a top-down range map with distance
    /// arcs, side markers, the target circle and every shot of the session.
    /// </summary>
    public sealed partial class ModernGolfSimUI
    {
        private sealed class RangeShot
        {
            public int Number;
            public string Club;
            public float Carry, Total, Offline, ToTarget;
            public Vector3 Landing, Rest;
            public bool Hit;
        }

        private readonly List<RangeShot> session = new List<RangeShot>();
        private int sessionNumber;
        private HoleMapCamera rangeMap;
        private bool mapDirty = true;
        private Camera targetCam;
        private RenderTexture targetTexture;
        private Vector2 sessionScroll;
        private GUIStyle tmTileCaption, tmTileValue, tmHeader, tmBig, tmSmall, tmMuted, tmRowText, tmMapLabel, tmChip;
        private Texture2D tmWhite, tmCircle;
        private GUIStyle tmPanel, tmPanelDark, tmRow, tmRowActive;
        private static readonly Color Orange = new Color(1f, 0.55f, 0.18f);
        private static readonly Color Blue = new Color(0.25f, 0.62f, 1f);

        // ------------------------------------------------------------ Set-up and data

        private void SetUpRangeViews(GameObject root)
        {
            rangeMap = root.AddComponent<HoleMapCamera>();
            mapDirty = true;

            var go = new GameObject("GolfSimZA_RangeTargetCam");
            go.transform.SetParent(root.transform, false);
            targetCam = go.AddComponent<Camera>();
            targetCam.fieldOfView = 38f;
            targetCam.nearClipPlane = 0.3f;
            targetCam.farClipPlane = 6000f;
            targetCam.depth = -5f;
            targetTexture = new RenderTexture(512, 300, 24) { name = "GolfSimZA_RangeTargetView", antiAliasing = 2 };
            targetCam.targetTexture = targetTexture;
            targetCam.enabled = false;
        }

        private void OnDisable()
        {
            if (targetTexture != null) targetTexture.Release();
        }

        /// <summary>Keeps the map picture and the target camera in step with the range.</summary>
        private void LateUpdate()
        {
            if (!isRange || range == null) return;
            if (mapDirty && rangeMap != null)
            {
                mapDirty = false;
                float length = Mathf.Max(range.TargetDistance + 60f, 200f);
                rangeMap.Render(new List<Vector3> { range.TeePosition + Vector3.back * 8f, range.TeePosition + Vector3.forward * length });
            }
            if (targetCam != null)
            {
                bool on = AppSettings.Current.rangeTargetCam && !GameMenuOverlay.IsOpen;
                targetCam.enabled = on;
                if (on)
                {
                    // Behind and above the target, looking back up the range: shots drop in towards you.
                    Vector3 t = range.TargetPosition;
                    targetCam.transform.position = t + new Vector3(0f, 16f, 44f);
                    targetCam.transform.LookAt(t + new Vector3(0f, 0f, -38f));
                }
            }
        }

        /// <summary>Adds a finished shot to the session list.</summary>
        private void RecordRangeShot(ShotData shot)
        {
            if (flight == null || range == null || !shot.IsValid) return;
            Vector3 rest = flight.Ball.position;
            Vector3 aimFrom = range.TeePosition;
            Vector3 aimTo = aimPointer != null && aimPointer.Visible ? aimPointer.Point : range.TargetPosition;
            Vector3 dir = aimTo - aimFrom;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
            Vector3 side = new Vector3(dir.z, 0f, -dir.x);
            Vector3 d = rest - aimFrom;
            d.y = 0f;
            Vector3 toTarget = rest - range.TargetPosition;
            toTarget.y = 0f;
            float radius = Mathf.Max(range.GreenWidth * 0.5f, 4f);
            session.Add(new RangeShot
            {
                Number = ++sessionNumber,
                Club = string.IsNullOrWhiteSpace(shot.ClubName) ? ActiveClub.Resolve() : shot.ClubName,
                Carry = shot.CarryMeters,
                Total = shot.TotalMeters,
                Offline = Vector3.Dot(d, side),
                ToTarget = toTarget.magnitude,
                Landing = flight.LandingPosition,
                Rest = rest,
                Hit = toTarget.magnitude <= radius
            });
        }

        private List<RangeShot> ClubShots(string club)
        {
            var list = new List<RangeShot>();
            foreach (RangeShot s in session)
                if (string.Equals(s.Club, club, System.StringComparison.OrdinalIgnoreCase)) list.Add(s);
            return list;
        }

        // ------------------------------------------------------------ Layout

        private void EnsureHudStyles()
        {
            if (tmTileCaption != null) return;
            tmPanel = Panel(new Color(0.07f, 0.09f, 0.11f, 0.90f), null);
            tmPanelDark = Panel(new Color(0.04f, 0.05f, 0.07f, 0.94f), null);
            tmRow = Panel(new Color(0.12f, 0.15f, 0.19f, 0.95f), null);
            tmRowActive = Panel(new Color(0.10f, 0.22f, 0.34f, 0.98f), Blue);
            tmWhite = GolfSimTheme.Tex(Color.white);
            tmCircle = GolfSimTheme.Rounded(Color.white, 32);
            tmTileCaption = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter, normal = { textColor = Orange } };
            tmTileValue = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            tmHeader = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white }, clipping = TextClipping.Clip };
            tmBig = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            tmSmall = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            tmMuted = new GUIStyle(tmSmall) { fontStyle = FontStyle.Normal, normal = { textColor = new Color(0.72f, 0.76f, 0.80f) } };
            tmRowText = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } };
            tmMapLabel = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 1f, 1f, 0.9f) } };
            tmChip = new GUIStyle(GUI.skin.box) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, border = new RectOffset(8, 8, 8, 8), normal = { background = GolfSimTheme.Rounded(new Color(0.10f, 0.13f, 0.17f, 1f), 8, new Color(1f, 1f, 1f, 0.5f)), textColor = Color.white } };
        }

        /// <summary>Rounded panel that keeps its corners at any size (9-slice).</summary>
        private static GUIStyle Panel(Color fill, Color? outline)
        {
            Texture2D tex = outline.HasValue ? GolfSimTheme.Rounded(fill, 6, outline.Value) : GolfSimTheme.Rounded(fill, 6);
            return new GUIStyle(GUI.skin.box) { border = new RectOffset(7, 7, 7, 7), padding = new RectOffset(0, 0, 0, 0), normal = { background = tex } };
        }

        private static void Box(Rect r, GUIStyle style) => GUI.Box(r, GUIContent.none, style);

        private void DrawRangeHud(AppSettings settings, bool flying)
        {
            EnsureHudStyles();
            float m = Mathf.Clamp(Screen.width * 0.015f, 10f, 24f);

            // Header: player and range settings (the MENU button sits left of it).
            float hx = m + 130f;
            Rect header = new Rect(hx, m, 300f, 38f);
            Box(header, tmPanelDark);
            Color pc = GolfSimZA.Players.PlayerRoster.ColorFor(ActiveClub.Player);
            GUI.color = pc;
            GUI.DrawTexture(new Rect(header.x + 8f, header.y + 7f, 24f, 24f), tmCircle);
            GUI.color = Color.white;
            GUI.Label(new Rect(header.x + 40f, header.y, header.width - 48f, header.height), GolfSimTheme.Ellipsize(ActiveClub.Player.ToUpperInvariant() + "   •   DRIVING RANGE", tmHeader, header.width - 48f), tmHeader);
            DrawRangePanel(header.xMax + 8f, m + 1f);

            float headerEnd = header.xMax + 8f + 230f;
            DrawWindAt(Mathf.Max(Screen.width * 0.5f - 115f, headerEnd + 12f), m);
            GUI.Label(new Rect(Screen.width * 0.5f - 150f, m + 42f, 300f, 20f), status, new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } });

            // Shot data tiles, top-left.
            float tilesBottom = DrawRangeTiles(m, m + 48f, simulator != null ? simulator.LastShot : default(ShotData));

            // Session panel, bottom-left.
            float sessionW = 244f;
            DrawSession(new Rect(m, tilesBottom + 10f, sessionW, Screen.height - m - tilesBottom - 10f));

            // Club selector and the target distance, bottom.
            bool clubBar = settings.clubSelectorMode == 0 || (settings.clubSelectorMode == 2 && !flying);
            float clubX = m + sessionW + 12f;
            if (clubBar) ClubBar.Draw(clubX, Screen.height - m);
            else ClubBar.Close();
            Rect pillRect = new Rect(clubX + (clubBar ? 180f : 0f), Screen.height - m - 44f, 170f, 40f);
            GUI.Box(pillRect, "TARGET  " + Units.DistanceText(TargetOrAimDistance()), new GUIStyle(tmChip) { fontSize = 18 });

            if (settings.showBallReady && !flying && status == "READY")
            {
                Rect ready = new Rect(Screen.width * 0.5f - 90f, Screen.height - m - 44f - 50f, 180f, 32f);
                GUI.Box(ready, "BALL READY", pill);
            }

            // Right: target view and range map.
            DrawRightPanel(m, settings);
        }

        private float TargetOrAimDistance()
        {
            if (aimPointer != null && aimPointer.Visible && aimMoved) return aimPointer.Distance;
            return range.TargetDistance;
        }

        private void DrawWindAt(float x, float y)
        {
            Rect r = new Rect(x, y, 230f, 38f);
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

        /// <summary>Two columns of tiles: orange caption, big white value (the first 8 chosen data tiles).</summary>
        private float DrawRangeTiles(float x, float y, ShotData shot)
        {
            const float w = 119f, h = 64f, gap = 6f;
            if (GUI.Button(new Rect(x, y, 2f * w + gap, 22f), GameOptions.ShowDataTiles ? "HIDE DATA  ▲" : "SHOW DATA  ▼", GolfSimTheme.SmallButton))
                GameOptions.ShowDataTiles = !GameOptions.ShowDataTiles;
            y += 28f;
            if (!GameOptions.ShowDataTiles) return y;
            List<string> ids = ShotDataTiles.Selected;
            int count = Mathf.Min(8, ids.Count);
            int rows = (count + 1) / 2;
            float maxRowsH = Screen.height * 0.42f;
            float tileH = Mathf.Min(h, (maxRowsH - rows * gap) / Mathf.Max(1, rows));
            for (int i = 0; i < count; i++)
            {
                ShotDataTiles.TileDef def = ShotDataTiles.Find(ids[i]);
                if (def == null) continue;
                Rect r = new Rect(x + (i % 2) * (w + gap), y + (i / 2) * (tileH + gap), w, tileH);
                Box(r, tmPanel);
                GUI.Label(new Rect(r.x, r.y + 5f, r.width, 16f), def.Caption(), tmTileCaption);
                GUI.Label(new Rect(r.x, r.y + 16f, r.width, r.height - 16f), def.Value(shot), new GUIStyle(tmTileValue) { fontSize = tileH >= 58f ? 26 : 20 });
            }
            return y + rows * (tileH + gap);
        }

        /// <summary>Session for the selected club: average (total / carry), target hits and every shot.</summary>
        private void DrawSession(Rect r)
        {
            if (r.height < 120f) return;
            Box(r, tmPanelDark);
            string club = ActiveClub.Resolve();
            List<RangeShot> shots = ClubShots(club);
            bool carry = AppSettings.Current.rangeShowCarry;

            // Club badge and target chip.
            Rect badge = new Rect(r.x + 10f, r.y + 10f, 30f, 30f);
            GUI.color = Blue;
            GUI.DrawTexture(badge, tmCircle);
            GUI.color = Color.white;
            GUI.Label(badge, ActiveClub.ShortName(club), new GUIStyle(tmSmall) { fontSize = 11 });
            GUI.Box(new Rect(r.x + 48f, r.y + 13f, 118f, 24f), "TARGET " + Units.DistanceText(range.TargetDistance), tmChip);
            if (session.Count > 0 && GUI.Button(new Rect(r.xMax - 70f, r.y + 13f, 60f, 24f), "CLEAR", GolfSimTheme.SmallButton))
            {
                session.RemoveAll(s => string.Equals(s.Club, club, System.StringComparison.OrdinalIgnoreCase));
                return;
            }

            float avg = 0f;
            int hits = 0;
            foreach (RangeShot s in shots) { avg += carry ? s.Carry : s.Total; if (s.Hit) hits++; }
            if (shots.Count > 0) avg /= shots.Count;
            float col = (r.width - 20f) / 2f;
            GUI.Label(new Rect(r.x + 10f, r.y + 48f, col, 16f), carry ? "AVG CARRY" : "AVG TOTAL", tmMuted);
            GUI.Label(new Rect(r.x + 10f, r.y + 64f, col, 24f), shots.Count > 0 ? Units.DistanceText(avg, "0.0") : "—", tmBig);
            GUI.Label(new Rect(r.x + 10f + col, r.y + 48f, col, 16f), "TARGET HITS", tmMuted);
            GUI.Label(new Rect(r.x + 10f + col, r.y + 64f, col, 24f), hits + "/" + shots.Count, tmBig);

            // Shot list, newest first.
            Rect list = new Rect(r.x + 8f, r.y + 98f, r.width - 16f, r.height - 98f - 40f);
            const float rowH = 32f;
            Rect content = new Rect(0, 0, list.width - (shots.Count * (rowH + 4f) > list.height ? 16f : 0f), Mathf.Max(list.height, shots.Count * (rowH + 4f)));
            sessionScroll = GUI.BeginScrollView(list, sessionScroll, content);
            int remove = -1;
            for (int i = shots.Count - 1, row = 0; i >= 0; i--, row++)
            {
                RangeShot s = shots[i];
                Rect rr = new Rect(0, row * (rowH + 4f), content.width, rowH);
                Box(rr, i == shots.Count - 1 ? tmRowActive : tmRow);
                GUI.Label(new Rect(rr.x + 8f, rr.y, 44f, rr.height), "#" + s.Number, new GUIStyle(tmRowText) { fontSize = 12, normal = { textColor = new Color(0.75f, 0.8f, 0.85f) } });
                GUI.Label(new Rect(rr.x + 52f, rr.y, 100f, rr.height), Units.DistanceText(carry ? s.Carry : s.Total, "0.0"), tmRowText);
                GUI.color = s.Hit ? new Color(0.3f, 0.9f, 0.4f) : new Color(1f, 1f, 1f, 0.25f);
                GUI.DrawTexture(new Rect(rr.xMax - 58f, rr.y + 11f, 10f, 10f), tmCircle);
                GUI.color = Color.white;
                if (GUI.Button(new Rect(rr.xMax - 34f, rr.y + 4f, 28f, rr.height - 8f), "✕", GolfSimTheme.SmallButton)) remove = i;
            }
            GUI.EndScrollView();
            if (remove >= 0) session.Remove(shots[remove]);
            if (shots.Count == 0) GUI.Label(new Rect(list.x, list.y + 6f, list.width, 40f), "Hit shots with this club -\nthey are listed here.", new GUIStyle(tmMuted) { wordWrap = true });

            // Parameter: total / carry.
            Rect foot = new Rect(r.x + 8f, r.yMax - 36f, r.width - 16f, 28f);
            if (GUI.Button(foot, GUIContent.none, GolfSimTheme.SmallButton))
            {
                AppSettings.Current.rangeShowCarry = !carry;
                AppSettings.Current.Save();
            }
            GUI.Label(new Rect(foot.x + 10f, foot.y, foot.width - 20f, foot.height), "PARAMETER   " + (carry ? "CARRY" : "TOTAL") + "   ›", new GUIStyle(tmSmall) { alignment = TextAnchor.MiddleLeft });
        }

        /// <summary>Target view (live picture) and the top-down range map with every shot.</summary>
        private void DrawRightPanel(float m, AppSettings settings)
        {
            float w = Mathf.Clamp(Screen.width * 0.2f, 220f, 320f);
            bool cam = settings.rangeTargetCam && targetTexture != null;
            float camH = cam ? w * 0.586f : 0f;
            float headerH = 30f, footH = 28f;
            float mapH = w * 1.5f;
            float total = (cam ? camH + 8f : 0f) + headerH + mapH + footH;
            float available = Screen.height - 2f * m - ClubBar.Height * 0f;
            if (total > available)
            {
                float scale = available / total;
                w *= scale; camH *= scale; mapH *= scale;
            }
            float x = Screen.width - m - w, y = m;

            if (cam)
            {
                Rect cr = new Rect(x, y, w, camH);
                Box(new Rect(cr.x - 2f, cr.y - 2f, cr.width + 4f, cr.height + 4f), tmPanelDark);
                GUI.DrawTexture(cr, targetTexture, ScaleMode.ScaleAndCrop);
                GUI.Label(new Rect(cr.x + 6f, cr.y + 4f, 120f, 16f), "TARGET VIEW", new GUIStyle(tmSmall) { alignment = TextAnchor.MiddleLeft, fontSize = 10 });
                y = cr.yMax + 8f;
            }

            // Header: last ball to the target and offline.
            RangeShot last = session.Count > 0 ? session[session.Count - 1] : null;
            Rect hr = new Rect(x, y, w, headerH);
            Box(hr, tmPanelDark);
            GUI.Label(new Rect(hr.x, hr.y, w * 0.5f, headerH), "◎  " + (last != null ? Units.DistanceText(last.ToTarget, "0.0") : "—"), tmSmall);
            GUI.Label(new Rect(hr.x + w * 0.5f, hr.y, w * 0.5f, headerH), "↔  " + (last != null ? ShotDataTiles.Side(Units.Distance(last.Offline), "R", "L") + " " + Units.DistanceUnit : "—"), tmSmall);
            y = hr.yMax;

            Rect map = new Rect(x, y, w, mapH);
            DrawRangeMap(map);
            y = map.yMax;

            Rect fr = new Rect(x, y, w, footH);
            Box(fr, tmPanelDark);
            GUI.Label(fr, "TARGET " + Units.DistanceText(range.TargetDistance) + (last != null ? "     LAST " + Units.DistanceText(AppSettings.Current.rangeShowCarry ? last.Carry : last.Total) : ""), tmSmall);
        }

        private void DrawRangeMap(Rect area)
        {
            Box(area, tmPanelDark);
            if (rangeMap == null || !rangeMap.HasImage) return;
            GUI.DrawTexture(area, rangeMap.Texture, ScaleMode.StretchToFill);

            // Click / drag on the map: put the aim target there.
            Event e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && area.Contains(e.mousePosition)
                && aimPointer != null && aimPointer.CanInteract != null && aimPointer.CanInteract()
                && rangeMap.FromMap(e.mousePosition, area, out Vector3 point))
            {
                aimPointer.Set(range.TeePosition, point);
                aimMoved = true;
                ApplyRangeAim(false);
                e.Use();
            }

            GUI.BeginClip(area);
            Vector2 o = new Vector2(area.x, area.y);
            System.Func<Vector3, Vector2> to = wpos => rangeMap.ToMap(wpos, area) - o;
            bool yards = !Units.Metric;
            float unit = yards ? 0.9144f : 1f;

            // Distance arcs every 20 (m or yd) with labels on the left.
            Vector3 tee = range.TeePosition;
            for (int k = 1; k <= 20; k++)
            {
                float dist = k * 20f * unit;
                Vector2 centre = to(tee + Vector3.forward * dist);
                if (centre.y < 0f) break;
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
                for (float a = -40f; a <= 40f; a += 1.6f)
                {
                    Vector2 p = to(tee + Quaternion.Euler(0f, a, 0f) * Vector3.forward * dist);
                    if (p.x < 0f || p.x > area.width) continue;
                    GUI.DrawTexture(new Rect(p.x - 1f, p.y - 1f, 2f, 2f), tmWhite);
                }
                GUI.color = Color.white;
                Vector2 lp = to(tee + Quaternion.Euler(0f, -38f, 0f) * Vector3.forward * dist);
                GUI.Label(new Rect(Mathf.Max(2f, lp.x) - 2f, lp.y - 8f, 28f, 16f), (k * 20).ToString(), tmMapLabel);
            }

            // Side markers at the bottom.
            float[] sides = yards ? new[] { 11f, 22f, 33f } : new[] { 10f, 20f, 30f };
            Vector2 teeMap = to(tee);
            float spacing = Mathf.Abs(to(tee + Vector3.right * sides[0] * unit).x - teeMap.x);
            foreach (float s in sides)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    if (spacing < 16f && s == sides[0]) continue; // too close together on a small map
                    Vector2 p = to(tee + Vector3.right * sign * s * unit);
                    if (p.x < 4f || p.x > area.width - 4f) continue;
                    GUI.Label(new Rect(p.x - 12f, area.height - 18f, 24f, 14f), s.ToString("0"), tmMapLabel);
                }
            GUI.color = Blue;
            GUI.DrawTexture(new Rect(teeMap.x - 4f, Mathf.Min(teeMap.y, area.height - 24f) - 4f, 8f, 8f), tmCircle);
            GUI.color = Color.white;

            // Aim line (dotted) to the aim target.
            Vector3 aimTo = aimPointer != null && aimPointer.Visible ? aimPointer.Point : range.TargetPosition;
            Vector2 a0 = to(tee), a1 = to(aimTo);
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            int dots = Mathf.Clamp(Mathf.RoundToInt(Vector2.Distance(a0, a1) / 5f), 2, 120);
            for (int i = 0; i <= dots; i++)
            {
                Vector2 p = Vector2.Lerp(a0, a1, i / (float)dots);
                GUI.DrawTexture(new Rect(p.x - 1f, p.y - 1f, 2f, 2f), tmWhite);
            }

            // Target circle.
            float radius = Mathf.Max(range.GreenWidth * 0.5f, 4f);
            Vector2 tc = to(range.TargetPosition);
            float pr = Vector2.Distance(tc, to(range.TargetPosition + Vector3.right * radius));
            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            for (float a = 0f; a < 360f; a += 8f)
            {
                Vector2 p = tc + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * pr;
                GUI.DrawTexture(new Rect(p.x - 1.5f, p.y - 1.5f, 3f, 3f), tmWhite);
            }
            GUI.DrawTexture(new Rect(tc.x - 2f, tc.y - 2f, 4f, 4f), tmCircle);

            // Shots of the selected club: every ball (latest bigger, ringed) and the spread.
            List<RangeShot> shots = ClubShots(ActiveClub.Resolve());
            bool carry = AppSettings.Current.rangeShowCarry;
            if (shots.Count >= 2)
            {
                Vector2 mean = Vector2.zero;
                foreach (RangeShot s in shots) { Vector3 p = carry ? s.Landing : s.Rest; mean += new Vector2(p.x, p.z); }
                mean /= shots.Count;
                float sx = 0f, sz = 0f;
                foreach (RangeShot s in shots) { Vector3 p = carry ? s.Landing : s.Rest; sx += (p.x - mean.x) * (p.x - mean.x); sz += (p.z - mean.y) * (p.z - mean.y); }
                sx = Mathf.Max(2f, Mathf.Sqrt(sx / (shots.Count - 1)) * 1.5f);
                sz = Mathf.Max(2f, Mathf.Sqrt(sz / (shots.Count - 1)) * 1.5f);
                GUI.color = new Color(0.35f, 0.85f, 1f, 0.9f);
                for (float a = 0f; a < 360f; a += 6f)
                {
                    Vector3 w = new Vector3(mean.x + Mathf.Cos(a * Mathf.Deg2Rad) * sx, 0f, mean.y + Mathf.Sin(a * Mathf.Deg2Rad) * sz);
                    Vector2 p = to(w);
                    GUI.DrawTexture(new Rect(p.x - 1f, p.y - 1f, 2f, 2f), tmWhite);
                }
            }
            for (int i = 0; i < shots.Count; i++)
            {
                RangeShot s = shots[i];
                Vector2 p = to(carry ? s.Landing : s.Rest);
                bool latest = i == shots.Count - 1;
                float size = latest ? 10f : 7f;
                if (latest)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.9f);
                    GUI.DrawTexture(new Rect(p.x - 9f, p.y - 9f, 18f, 18f), tmCircle);
                    GUI.color = new Color(0.08f, 0.10f, 0.12f, 1f);
                    GUI.DrawTexture(new Rect(p.x - 7f, p.y - 7f, 14f, 14f), tmCircle);
                }
                GUI.color = s.Hit ? new Color(0.35f, 0.95f, 0.45f) : Color.white;
                GUI.DrawTexture(new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size), tmCircle);
            }
            GUI.color = Color.white;
            GUI.EndClip();
        }
    }
}
