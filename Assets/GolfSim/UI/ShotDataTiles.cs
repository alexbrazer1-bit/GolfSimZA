using GolfSimZA.Core;
using UnityEngine;

namespace GolfSimZA.UI
{
    /// <summary>Shot data tiles, two columns, top-right of the play screen.</summary>
    public static class ShotDataTiles
    {
        private static GUIStyle tile, value, label, toggle;
        /// <summary>Tile size grows with the screen (large, easy-to-read numbers on a big display).</summary>
        public static float TileWidth => Mathf.Clamp(Screen.width * 0.085f, 140f, 210f);
        public static float TileHeight => Mathf.Round(TileWidth * 0.56f);

        private static void Ensure()
        {
            GolfSimTheme.Ensure();
            if (tile != null) return;
            tile = new GUIStyle(GUI.skin.box) { border = new RectOffset(8, 8, 8, 8), normal = { background = GolfSimTheme.Rounded(new Color(0.03f, 0.07f, 0.09f, 0.86f), 8) } };
            value = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            label = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = GolfSimTheme.Muted } };
            toggle = new GUIStyle(GolfSimTheme.SmallButton) { fixedHeight = 34, fontSize = 13 };
        }

        /// <summary>Draws the toggle button and (when shown) the tiles, anchored at the top-right corner.</summary>
        /// <param name="maxBottom">Lowest screen y the tiles may use (tiles shrink to fit); 0 = screen bottom.</param>
        /// <summary>Smallest height the tiles need below 'top' (button plus rows at the smallest tile size).</summary>
        public static float MinimumHeight
        {
            get
            {
                if (!GameOptions.ShowDataTiles) return 34f;
                int rows = (Selected.Count + 1) / 2;
                return 40f + rows * 48f;
            }
        }

        public static void Draw(float right, float top, ShotData shot, float maxBottom = 0f)
        {
            Ensure();
            if (GUI.Button(new Rect(right - 110f, top, 110f, 34f), GameOptions.ShowDataTiles ? "HIDE DATA" : "SHOW DATA", toggle))
                GameOptions.ShowDataTiles = !GameOptions.ShowDataTiles;
            LastBottom = top + 34f;
            if (!GameOptions.ShowDataTiles) return;

            var ids = Selected;
            int count = ids.Count;
            if (count == 0) return;
            int rows = (count + 1) / 2;
            if (maxBottom <= 0f) maxBottom = Screen.height;
            float available = maxBottom - (top + 40f);
            float tileH = Mathf.Clamp(available / rows - 4f, 44f, TileHeight);
            float tileW = TileWidth;
            // Numbers as big as the tile allows; captions a little bigger on large tiles.
            value.fontSize = Mathf.Clamp(Mathf.RoundToInt(tileH * 0.46f), 18, 44);
            label.fontSize = tileH >= 70f ? 14 : tileH >= 54f ? 12 : 10;
            float x0 = right - tileW * 2f - 4f;
            float y = top + 40f;
            for (int i = 0; i < count; i++)
            {
                TileDef def = Find(ids[i]);
                if (def == null) continue;
                int row = i / 2, col = i % 2;
                DrawTile(new Rect(x0 + col * (tileW + 4f), y + row * (tileH + 4f), tileW, tileH), def.Value(shot), def.Caption());
            }
            y += rows * (tileH + 4f);
            LastBottom = y;
        }

        // ------------------------------------------------------------ Tile catalogue

        public sealed class TileDef
        {
            public string Id;
            public System.Func<string> Caption;
            public System.Func<ShotData, string> Value;
        }

        private static string D(ShotData s, float v, string f = "0.0") => s.IsValid ? v.ToString(f) : "—";

        /// <summary>Every tile that can be shown (Settings → VISUAL SETTINGS → USER INTERFACE → SHOT DATA TILES).</summary>
        public static readonly TileDef[] All =
        {
            new TileDef { Id = "BALL_SPEED", Caption = () => "BALL SPEED " + Units.SpeedUnit, Value = s => D(s, Units.Speed(s.BallSpeedMps)) },
            new TileDef { Id = "CLUB_SPEED", Caption = () => "CLUB SPEED " + Units.SpeedUnit, Value = s => s.IsValid && s.HasClubData ? Units.Speed(s.ClubSpeedMps).ToString("0.0") : "—" },
            new TileDef { Id = "CARRY", Caption = () => "CARRY " + Units.DistanceUnit.ToUpperInvariant(), Value = s => D(s, Units.Distance(s.CarryMeters)) },
            new TileDef { Id = "TOTAL", Caption = () => "TOTAL " + Units.DistanceUnit.ToUpperInvariant(), Value = s => D(s, Units.Distance(s.TotalMeters)) },
            new TileDef { Id = "VLA", Caption = () => "LAUNCH (VLA)", Value = s => s.IsValid ? s.LaunchAngleDeg.ToString("0.0") + "°" : "—" },
            new TileDef { Id = "HLA", Caption = () => "DIRECTION (HLA)", Value = s => s.IsValid ? Side(s.LaunchDirectionDeg, "R", "L") : "—" },
            new TileDef { Id = "BACK_SPIN", Caption = () => "BACK SPIN RPM", Value = s => D(s, s.BackSpinRpm, "0") },
            new TileDef { Id = "SIDE_SPIN", Caption = () => "SIDE SPIN RPM", Value = s => !s.IsValid ? "—" : Mathf.Abs(s.SideSpinRpm) < 0.5f ? "0" : Mathf.Abs(s.SideSpinRpm).ToString("0") + (s.SideSpinRpm > 0f ? " R" : " L") },
            new TileDef { Id = "TOTAL_SPIN", Caption = () => "TOTAL SPIN RPM", Value = s => D(s, Mathf.Sqrt(s.BackSpinRpm * s.BackSpinRpm + s.SideSpinRpm * s.SideSpinRpm), "0") },
            new TileDef { Id = "SPIN_AXIS", Caption = () => "SPIN AXIS", Value = s => s.IsValid ? Side(s.SpinAxisDeg, "R", "L") : "—" },
            new TileDef { Id = "PEAK", Caption = () => "PEAK HEIGHT " + Units.DistanceUnit.ToUpperInvariant(), Value = s => s.IsValid && s.IsComplete ? Units.Distance(s.PeakHeightMeters).ToString("0.0") : "—" },
            new TileDef { Id = "DESCENT", Caption = () => "DESCENT ANGLE", Value = s => s.IsValid && s.IsComplete ? s.DescentAngleDeg.ToString("0.0") + "°" : "—" },
            new TileDef { Id = "CLUB_PATH", Caption = () => "CLUB PATH", Value = s => s.IsValid && s.HasClubPath ? Side(s.ClubPathDeg, "I-O", "O-I") : "—" },
            new TileDef { Id = "FACE_TARGET", Caption = () => "FACE TO TARGET", Value = s => s.IsValid && s.HasClubPath ? Side(s.FaceToTargetDeg, "O", "C") : "—" },
            new TileDef { Id = "FACE_PATH", Caption = () => "FACE TO PATH", Value = s => s.IsValid && s.HasClubPath ? Side(s.FaceToTargetDeg - s.ClubPathDeg, "O", "C") : "—" },
            new TileDef { Id = "AOA", Caption = () => "ATTACK ANGLE", Value = s => s.IsValid && s.HasClubPath ? Side(s.AttackAngleDeg, "UP", "DN") : "—" },
            new TileDef { Id = "DYN_LOFT", Caption = () => "DYNAMIC LOFT", Value = s => s.IsValid && s.HasClubPath && s.DynamicLoftDeg > 0f ? s.DynamicLoftDeg.ToString("0.0") + "°" : "—" },
            new TileDef { Id = "SPIN_LOFT", Caption = () => "SPIN LOFT", Value = s => s.IsValid && s.HasClubPath && s.DynamicLoftDeg > 0f ? (s.DynamicLoftDeg - s.AttackAngleDeg).ToString("0.0") + "°" : "—" },
            new TileDef { Id = "OFFLINE", Caption = () => "OFFLINE " + Units.DistanceUnit.ToUpperInvariant(), Value = s => s.IsValid && s.IsComplete ? Side(Units.Distance(s.OfflineMeters), "R", "L") : "—" },
            new TileDef { Id = "SMASH", Caption = () => "SMASH FACTOR", Value = Smash },
        };

        public static readonly string[] DefaultTiles =
        {
            "BALL_SPEED", "CLUB_SPEED", "CARRY", "TOTAL", "VLA", "HLA", "BACK_SPIN", "SPIN_AXIS",
            "PEAK", "DESCENT", "CLUB_PATH", "FACE_TARGET", "FACE_PATH", "SMASH"
        };

        public const int MaxTiles = 16;

        public static TileDef Find(string id)
        {
            foreach (TileDef t in All) if (t.Id == id) return t;
            return null;
        }

        /// <summary>Tiles to show, in order (the saved choice, or the default set).</summary>
        public static System.Collections.Generic.List<string> Selected
        {
            get
            {
                var saved = AppSettings.Current.dataTiles;
                var list = new System.Collections.Generic.List<string>();
                if (saved != null) foreach (string id in saved) if (Find(id) != null && !list.Contains(id)) list.Add(id);
                if (list.Count == 0) list.AddRange(DefaultTiles);
                return list;
            }
        }

        /// <summary>Adds or removes a tile from the saved choice.</summary>
        public static void Toggle(string id)
        {
            var list = Selected;
            if (!list.Remove(id))
            {
                if (list.Count >= MaxTiles) return;
                list.Add(id);
            }
            if (list.Count == 0) return; // keep at least one tile
            AppSettings.Current.dataTiles = list;
            AppSettings.Current.Save();
        }

        /// <summary>Bottom edge of the tiles drawn last frame (the toggle button when hidden).</summary>
        public static float LastBottom { get; private set; }

        private static void DrawTile(Rect r, string text, string caption)
        {
            GUI.Box(r, GUIContent.none, tile);
            float capH = label.fontSize + 6f;
            GUI.Label(new Rect(r.x, r.y + 2f, r.width, r.height - capH), text, value);
            GUI.Label(new Rect(r.x, r.yMax - capH - 1f, r.width, capH), caption, label);
        }

        /// <summary>"2.6 R" style value with a direction letter (no letter at zero).</summary>
        public static string Side(float v, string positive, string negative)
        {
            if (Mathf.Abs(v) < 0.05f) return "0.0";
            return Mathf.Abs(v).ToString("0.0") + " " + (v > 0 ? positive : negative);
        }

        private static string Smash(ShotData shot)
        {
            return shot.IsValid && shot.HasClubData && shot.ClubSpeedMps > 0.1f ? (shot.BallSpeedMps / shot.ClubSpeedMps).ToString("0.00") : "—";
        }
    }
}
