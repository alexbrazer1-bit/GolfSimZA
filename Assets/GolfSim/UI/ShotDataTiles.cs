using GolfSimZA.Core;
using UnityEngine;

namespace GolfSimZA.UI
{
    /// <summary>Shot data tiles, two columns, top-right of the play screen.</summary>
    public static class ShotDataTiles
    {
        private static GUIStyle tile, value, label, toggle;
        public const float TileWidth = 118f;
        public const float TileHeight = 56f;

        private static void Ensure()
        {
            GolfSimTheme.Ensure();
            if (tile != null) return;
            tile = new GUIStyle(GUI.skin.box) { border = new RectOffset(8, 8, 8, 8), normal = { background = GolfSimTheme.Rounded(new Color(0.03f, 0.07f, 0.09f, 0.86f), 8) } };
            value = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            label = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = GolfSimTheme.Muted } };
            toggle = new GUIStyle(GolfSimTheme.SmallButton) { fixedHeight = 34, fontSize = 13 };
        }

        /// <summary>Draws the toggle button and (when shown) the tiles, anchored at the top-right corner.</summary>
        /// <param name="maxBottom">Lowest screen y the tiles may use (tiles shrink to fit); 0 = screen bottom.</param>
        public static void Draw(float right, float top, ShotData shot, float maxBottom = 0f)
        {
            Ensure();
            if (GUI.Button(new Rect(right - 110f, top, 110f, 34f), GameOptions.ShowDataTiles ? "HIDE DATA" : "SHOW DATA", toggle))
                GameOptions.ShowDataTiles = !GameOptions.ShowDataTiles;
            LastBottom = top + 34f;
            if (!GameOptions.ShowDataTiles) return;

            bool has = shot.IsValid;
            string[,] cells =
            {
                { has ? Units.Speed(shot.BallSpeedMps).ToString("0.0") : "—", "BALL SPEED " + Units.SpeedUnit, has && shot.HasClubData ? Units.Speed(shot.ClubSpeedMps).ToString("0.0") : "—", "CLUB SPEED " + Units.SpeedUnit },
                { has ? Units.Distance(shot.CarryMeters).ToString("0.0") : "—", "CARRY " + Units.DistanceUnit.ToUpperInvariant(), has ? Units.Distance(shot.TotalMeters).ToString("0.0") : "—", "TOTAL " + Units.DistanceUnit.ToUpperInvariant() },
                { has ? shot.LaunchAngleDeg.ToString("0.0") + "°" : "—", "LAUNCH (VLA)", has ? Side(shot.LaunchDirectionDeg, "R", "L") : "—", "DIRECTION (HLA)" },
                { has ? shot.BackSpinRpm.ToString("0") : "—", "BACK SPIN RPM", has ? Side(shot.SpinAxisDeg, "R", "L") : "—", "SPIN AXIS" },
                { has && shot.IsComplete ? Units.Distance(shot.PeakHeightMeters).ToString("0.0") : "—", "PEAK HEIGHT " + Units.DistanceUnit.ToUpperInvariant(), has && shot.IsComplete ? shot.DescentAngleDeg.ToString("0.0") + "°" : "—", "DESCENT ANGLE" },
                { has && shot.HasClubPath ? Side(shot.ClubPathDeg, "I-O", "O-I") : "—", "CLUB PATH", has && shot.HasClubPath ? Side(shot.FaceToTargetDeg, "O", "C") : "—", "FACE TO TARGET" },
                { has && shot.IsComplete ? Side(Units.Distance(shot.OfflineMeters), "R", "L") : "—", "OFFLINE " + Units.DistanceUnit.ToUpperInvariant(), Smash(shot), "SMASH FACTOR" }
            };

            int rows = cells.GetLength(0);
            if (maxBottom <= 0f) maxBottom = Screen.height;
            float available = maxBottom - (top + 40f);
            float tileH = Mathf.Clamp(available / rows - 4f, 38f, TileHeight);
            float tileW = TileWidth;
            value.fontSize = tileH >= 50f ? 22 : 17;
            float x0 = right - tileW * 2f - 4f;
            float y = top + 40f;
            for (int row = 0; row < rows; row++)
            {
                DrawTile(new Rect(x0, y, tileW, tileH), cells[row, 0], cells[row, 1]);
                DrawTile(new Rect(x0 + tileW + 4f, y, tileW, tileH), cells[row, 2], cells[row, 3]);
                y += tileH + 4f;
            }
            LastBottom = y;
        }

        /// <summary>Bottom edge of the tiles drawn last frame (the toggle button when hidden).</summary>
        public static float LastBottom { get; private set; }

        private static void DrawTile(Rect r, string text, string caption)
        {
            GUI.Box(r, GUIContent.none, tile);
            GUI.Label(new Rect(r.x, r.y + 2f, r.width, r.height - 16f), text, value);
            GUI.Label(new Rect(r.x, r.yMax - 16f, r.width, 14f), caption, label);
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
