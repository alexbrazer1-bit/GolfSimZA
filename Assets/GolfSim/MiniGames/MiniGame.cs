using System.Collections.Generic;
using GolfSimZA.Core;
using GolfSimZA.Physics;
using UnityEngine;

namespace GolfSimZA.MiniGames
{
    /// <summary>A golfer in a mini game.</summary>
    public sealed class MiniPlayer
    {
        public string Name;
        public Color Color;
        public int Shots;
        /// <summary>Points (target / capture / blast games).</summary>
        public float Score;
        /// <summary>Best single shot (closest to the pin: smallest; long drive: longest).</summary>
        public float Best = float.NaN;
        public int Flags;
        public bool Out;
        public int OutRound;
        public int Caught;
        public string Last = "";
        public readonly List<string> Log = new List<string>();
        public float MissTotal;
    }

    /// <summary>What a shot did: the words for the banner and the points it earned.</summary>
    public struct ShotOutcome
    {
        public string Text;
        public float Points;
        public bool Good;
        public ShotOutcome(string text, float points, bool good) { Text = text; Points = points; Good = good; }
    }

    /// <summary>
    /// Base for every mini game: builds its targets on the range, scores each shot, decides who
    /// plays next and when the game is over, and ranks the players.
    /// </summary>
    public abstract class MiniGame
    {
        public MiniGameInfo Info;
        public MiniGameRunner Runner;
        public RangeEnvironment Range;
        public Transform Root;
        public int ShotsPerPlayer;
        public bool Explore;

        public List<MiniPlayer> Players => Runner.Players;

        /// <summary>Where the target camera looks and the aim target starts.</summary>
        public virtual Vector3 Focus => new Vector3(0f, 0f, 150f);
        /// <summary>Keep the range's own green and flag (closest to the pin).</summary>
        public virtual bool ShowRangeTarget => false;
        /// <summary>Lowest score wins (closest to the pin).</summary>
        public virtual bool LowerIsBetter => false;

        public abstract void Build();
        public abstract ShotOutcome Score(MiniPlayer p, Vector3 landing, Vector3 rest, ShotData shot);

        /// <summary>Called every frame (moving creatures, effects).</summary>
        public virtual void Tick() { }

        /// <summary>Labels over the targets.</summary>
        public virtual void DrawLabels(Camera cam) { }

        /// <summary>Next player to hit after <paramref name="current"/>, or -1 when the game is over.</summary>
        public virtual int NextPlayer(int current)
        {
            int n = Players.Count;
            for (int k = 1; k <= n; k++)
            {
                int i = (current + k) % n;
                if (Explore || Players[i].Shots < ShotsPerPlayer) return i;
            }
            return -1;
        }

        /// <summary>Sort key: bigger is better (unless LowerIsBetter).</summary>
        public virtual float RankKey(MiniPlayer p) => p.Score;
        public virtual string ScoreText(MiniPlayer p) => p.Score.ToString("0");
        public virtual string ScoreCaption => "POINTS";
        public virtual string StatusLine() => "";
        public virtual string ShotLabel(MiniPlayer p) => Explore ? "SHOT " + (p.Shots + 1) + "  •  EXPLORE" : "SHOT " + Mathf.Min(p.Shots + 1, ShotsPerPlayer) + " OF " + ShotsPerPlayer;

        /// <summary>A good point to aim at for the player up (used by the automatic mini game test).</summary>
        public virtual Vector3 AimHint(MiniPlayer p) => Focus;

        /// <summary>The game brings its own map (the driving range is hidden while it is played).</summary>
        public virtual bool HasOwnMap => false;
        /// <summary>Trees for the game's own map (default: none).</summary>
        public virtual void PlantTrees(GolfSimZA.Physics.TreeField field)
        {
            field.Clear();
            field.Commit();
        }

        /// <summary>How far down the range the map shows.</summary>
        public virtual float MapLength => 250f;
        /// <summary>Results show what each player collected (creature games).</summary>
        public virtual bool HasCollection => false;
        public virtual string Collection(MiniPlayer p) => "";
        /// <summary>Extra marks on the top-down map (creatures, barrels).</summary>
        public virtual void DrawMapMarks(System.Func<Vector3, Vector2> to, Texture2D dot) { }

        /// <summary>Hides things that move (creatures) while the map picture is taken - they are drawn as live dots instead.</summary>
        public virtual void ShowMoving(bool visible) { }

        /// <summary>Game-specific pieces cleared before a PLAY AGAIN rebuild.</summary>
        public virtual void Clear() { }

        protected static float Flat(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        protected static string Dist(float meters, string format = "0.0") => Units.DistanceText(meters, format);
    }

    // =====================================================================================
    //  CLOSEST TO THE PIN
    // =====================================================================================

    public sealed class ClosestToPinGame : MiniGame
    {
        private Vector3 pin;
        public override Vector3 Focus => pin;
        public override bool ShowRangeTarget => true;
        public override bool LowerIsBetter => true;
        public override string ScoreCaption => "CLOSEST";
        public override float MapLength => pin.z + 50f;
        public override Vector3 AimHint(MiniPlayer p) => pin;

        public override void Build()
        {
            float lo = 60f, hi = 200f;
            switch (MiniGameSession.Distance)
            {
                case 1: lo = 60f; hi = 100f; break;
                case 2: lo = 100f; hi = 150f; break;
                case 3: lo = 150f; hi = 200f; break;
            }
            pin = new Vector3(Random.Range(-8f, 8f), 0f, Random.Range(lo, hi));
            Range.SetTarget(pin);
            Range.SetTargetVisible(true);
            // Distance rings round the pin: 3, 6 and 10 m.
            MiniGameWorld.Ring(Root, pin, 2.9f, 3.1f, new Color(1f, 1f, 1f, 0.55f), 0.07f);
            MiniGameWorld.Ring(Root, pin, 5.9f, 6.1f, new Color(1f, 1f, 1f, 0.40f), 0.07f);
            MiniGameWorld.Ring(Root, pin, 9.85f, 10.15f, new Color(1f, 1f, 1f, 0.30f), 0.07f);
        }

        public override ShotOutcome Score(MiniPlayer p, Vector3 landing, Vector3 rest, ShotData shot)
        {
            bool holed = Runner.BallHoled;
            float d = holed ? 0f : Flat(rest, pin);
            bool best = float.IsNaN(p.Best) || d < p.Best;
            if (best) p.Best = d;
            p.Log.Add(holed ? "ACE" : Dist(d));
            if (holed) { GolfSimAudio.PlayWin(); return new ShotOutcome("HOLE IN ONE!  ACE!", 0f, true); }
            return new ShotOutcome(Dist(d) + " FROM THE PIN" + (best && p.Shots > 0 ? "  •  NEW BEST" : ""), 0f, d < 5f);
        }

        public override float RankKey(MiniPlayer p) => float.IsNaN(p.Best) ? 99999f : p.Best;
        public override string ScoreText(MiniPlayer p) => float.IsNaN(p.Best) ? "—" : p.Best <= 0.001f ? "ACE" : Dist(p.Best);
        public override string StatusLine() => "PIN  " + Dist(pin.magnitude, "0");

        public override void DrawLabels(Camera cam) => Runner.WorldLabel(cam, pin + Vector3.up * 5f, Dist(pin.magnitude, "0"), Color.white, 16, pin + Vector3.up * 2.6f);
    }

    // =====================================================================================
    //  LONG DRIVE
    // =====================================================================================

    public sealed class LongDriveGame : MiniGame
    {
        private const float HalfWidth = 20f;
        public override Vector3 Focus => new Vector3(0f, 0f, 250f);
        public override string ScoreCaption => "LONGEST";
        public override float MapLength => 390f;
        public override Vector3 AimHint(MiniPlayer p) => new Vector3(0f, 0f, 240f);

        public override void Build()
        {
            Range.SetTarget(new Vector3(0f, 0f, 300f));
            Range.SetTargetVisible(false);
            Color line = new Color(1f, 1f, 1f, 0.75f);
            MiniGameWorld.GroundLine(Root, new Vector3(-HalfWidth, 0f, 60f), new Vector3(-HalfWidth, 0f, 380f), 0.5f, new Color(1f, 0.85f, 0.2f, 0.9f));
            MiniGameWorld.GroundLine(Root, new Vector3(HalfWidth, 0f, 60f), new Vector3(HalfWidth, 0f, 380f), 0.5f, new Color(1f, 0.85f, 0.2f, 0.9f));
            for (float z = 100f; z <= 375f; z += 25f)
                MiniGameWorld.GroundLine(Root, new Vector3(-HalfWidth, 0f, z), new Vector3(HalfWidth, 0f, z), z % 50f == 0f ? 0.45f : 0.22f, line);
        }

        public override ShotOutcome Score(MiniPlayer p, Vector3 landing, Vector3 rest, ShotData shot)
        {
            float total = Flat(rest, Vector3.zero);
            bool inGrid = Mathf.Abs(rest.x) <= HalfWidth && rest.z > 0f;
            p.Log.Add(inGrid ? Dist(total) : "OUT");
            if (!inGrid) return new ShotOutcome("OUT OF THE GRID  •  " + Dist(total) + "  •  " + (rest.x > 0f ? "RIGHT" : "LEFT"), 0f, false);
            bool best = float.IsNaN(p.Best) || total > p.Best;
            if (best) p.Best = total;
            p.Score = p.Best;
            return new ShotOutcome(Dist(total) + " IN THE GRID" + (best ? "  •  NEW BEST" : ""), 0f, true);
        }

        public override float RankKey(MiniPlayer p) => float.IsNaN(p.Best) ? 0f : p.Best;
        public override string ScoreText(MiniPlayer p) => float.IsNaN(p.Best) ? "—" : Dist(p.Best);
        public override string StatusLine() => "GRID  " + Dist(HalfWidth * 2f, "0") + " WIDE";

        public override void DrawLabels(Camera cam)
        {
            for (float z = 100f; z <= 350f; z += 50f)
                Runner.WorldLabel(cam, new Vector3(HalfWidth + 3f, 0.5f, z), Units.Distance(z).ToString("0"), new Color(1f, 1f, 1f, 0.9f), 13);
        }
    }

    // =====================================================================================
    //  TARGET RINGS
    // =====================================================================================

    public sealed class TargetRingsGame : MiniGame
    {
        private static readonly float[] Radii = { 2.5f, 5.5f, 9f, 13f };
        private static readonly int[] Points = { 100, 50, 25, 10 };
        private static readonly Color[] RingColours =
        {
            new Color(1f, 0.80f, 0.15f, 0.95f), new Color(0.95f, 0.25f, 0.22f, 0.9f), new Color(1f, 1f, 1f, 0.85f), new Color(0.25f, 0.55f, 1f, 0.85f)
        };
        private readonly Vector3[] targets = new Vector3[3];
        private readonly float[] multiplier = { 1f, 1.5f, 2f };
        private static readonly string[] TargetNames = { "SHORT", "MIDDLE", "LONG" };

        public override Vector3 Focus => targets[1];
        public override Vector3 AimHint(MiniPlayer p) => targets[p.Shots % targets.Length];

        public override void Build()
        {
            targets[0] = new Vector3(Random.Range(-16f, -6f), 0f, Random.Range(75f, 95f));
            targets[1] = new Vector3(Random.Range(6f, 16f), 0f, Random.Range(125f, 145f));
            targets[2] = new Vector3(Random.Range(-10f, 4f), 0f, Random.Range(175f, 200f));
            Range.SetTarget(targets[1]);
            Range.SetTargetVisible(false);
            foreach (Vector3 t in targets)
            {
                float inner = 0f;
                for (int r = 0; r < Radii.Length; r++)
                {
                    MiniGameWorld.Ring(Root, t, inner, Radii[r], RingColours[r], 0.06f + 0.002f * r);
                    inner = Radii[r];
                }
                // Centre marker post.
                MiniGameWorld.Shape(PrimitiveType.Cylinder, Root, new Vector3(t.x, MiniGameWorld.Ground(t) + 1.5f, t.z), new Vector3(0.25f, 1.5f, 0.25f), Color.white);
            }
        }

        public override ShotOutcome Score(MiniPlayer p, Vector3 landing, Vector3 rest, ShotData shot)
        {
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < targets.Length; i++)
            {
                float d = Flat(landing, targets[i]);
                if (d < bestD) { bestD = d; best = i; }
            }
            int ring = -1;
            for (int r = 0; r < Radii.Length; r++) if (bestD <= Radii[r]) { ring = r; break; }
            if (ring < 0)
            {
                p.Log.Add("0");
                return new ShotOutcome("MISSED  •  " + Dist(bestD) + " FROM THE " + TargetNames[best] + " TARGET", 0f, false);
            }
            float pts = Points[ring] * multiplier[best];
            p.Score += pts;
            // The ball mark stays in the player's colour where it landed.
            MiniGameWorld.Shape(PrimitiveType.Sphere, Root, new Vector3(landing.x, MiniGameWorld.Ground(landing) + 0.3f, landing.z), Vector3.one * 0.6f, p.Color, "BallMark", 0.6f);
            p.Log.Add(pts.ToString("0"));
            string[] ringNames = { "BULLSEYE!", "RED RING", "WHITE RING", "BLUE RING" };
            return new ShotOutcome("+" + pts.ToString("0") + "  •  " + ringNames[ring] + "  •  " + TargetNames[best] + " TARGET" + (multiplier[best] > 1f ? " x" + multiplier[best].ToString("0.#") : ""), pts, true);
        }

        public override void DrawLabels(Camera cam)
        {
            for (int i = 0; i < targets.Length; i++)
                Runner.WorldLabel(cam, targets[i] + Vector3.up * 5f, Units.Distance(targets[i].magnitude).ToString("0") + " " + Units.DistanceUnit + "  x" + multiplier[i].ToString("0.#"), Color.white, 14, targets[i] + Vector3.up * 3f);
        }
    }

    // =====================================================================================
    //  EDGE KNOCKOUT
    // =====================================================================================

    public sealed class EdgeKnockoutGame : MiniGame
    {
        private const float HalfWidth = 30f;
        private float edge, zone;
        private int round = 1;
        private readonly Dictionary<MiniPlayer, float> roundGap = new Dictionary<MiniPlayer, float>();
        private readonly List<GameObject> marks = new List<GameObject>();
        private bool over;
        private string roundResult;

        public override Vector3 Focus => new Vector3(0f, 0f, edge - zone * 0.5f);
        public override string ScoreCaption => Solo ? "ROUNDS SURVIVED" : "STATUS";
        private bool Solo => Players.Count == 1;
        public override Vector3 AimHint(MiniPlayer p) => new Vector3(0f, CanyonEdgeMap.TopY(edge - zone * 0.5f, edge), edge - zone * 0.5f);
        public override bool HasOwnMap => true;
        public override float MapLength => edge + 70f;
        private int mapSeed;

        public override void Build()
        {
            round = 1;
            over = false;
            // A real cliff: the edge stays where it is for the whole game; the zone shrinks every round.
            edge = Random.Range(110f, 170f);
            zone = 24f;
            mapSeed = Random.Range(1, 9999);
            CanyonEdgeMap.Build(Root, edge, mapSeed);
            UnityEngine.Physics.SyncTransforms();
            Range.SetTarget(new Vector3(0f, 0f, edge));
            Range.SetTargetVisible(false);
            DrawZone();
        }

        public override void Clear() { roundGap.Clear(); marks.Clear(); }

        public override void PlantTrees(GolfSimZA.Physics.TreeField field) => CanyonEdgeMap.PlantTrees(field, edge, mapSeed);

        private void DrawZone()
        {
            foreach (GameObject g in marks) if (g != null) Object.Destroy(g);
            marks.Clear();
            float start = edge - zone;
            // Paint stays a little back from the cliff lip (nothing hangs over the drop).
            float fillEnd = edge - 0.4f, fillW = fillEnd - start;
            for (float x = -HalfWidth; x < HalfWidth; x += 6f)
                marks.Add(MiniGameWorld.GroundLine(Root, new Vector3(x, 0f, start + fillW * 0.5f), new Vector3(x + 6f, 0f, start + fillW * 0.5f), fillW, new Color(0.25f, 0.9f, 0.45f, 0.28f), "Zone"));
            marks.Add(MiniGameWorld.GroundLine(Root, new Vector3(-HalfWidth, 0f, start), new Vector3(HalfWidth, 0f, start), 0.5f, new Color(1f, 1f, 1f, 0.9f), "ZoneStart"));
            marks.Add(MiniGameWorld.GroundLine(Root, new Vector3(-HalfWidth, 0f, edge - 0.75f), new Vector3(HalfWidth, 0f, edge - 0.75f), 1.2f, new Color(0.95f, 0.15f, 0.12f, 0.95f), "Edge"));
            // Posts at the ends of the edge line.
            marks.Add(MiniGameWorld.Shape(PrimitiveType.Cylinder, Root, new Vector3(-HalfWidth, CanyonEdgeMap.TopY(edge, edge) + 1.5f, edge - 0.75f), new Vector3(0.4f, 1.5f, 0.4f), new Color(0.95f, 0.15f, 0.12f)));
            marks.Add(MiniGameWorld.Shape(PrimitiveType.Cylinder, Root, new Vector3(HalfWidth, CanyonEdgeMap.TopY(edge, edge) + 1.5f, edge - 0.75f), new Vector3(0.4f, 1.5f, 0.4f), new Color(0.95f, 0.15f, 0.12f)));
            Runner.MapChanged();
        }

        public override ShotOutcome Score(MiniPlayer p, Vector3 landing, Vector3 rest, ShotData shot)
        {
            float gap = edge - rest.z;
            bool inside = gap >= 0f && gap <= zone && Mathf.Abs(rest.x) <= HalfWidth;
            roundGap[p] = inside ? gap : float.PositiveInfinity;
            bool fell = rest.y < CanyonEdgeMap.TopY(Mathf.Min(rest.z, edge), edge) - 3f;
            p.Log.Add(inside ? Dist(gap) : gap < 0f ? "OVER" : fell ? "FELL" : "OUT");
            if (inside) return new ShotOutcome("IN THE ZONE  •  " + Dist(gap) + " FROM THE EDGE", 0f, true);
            if (fell && Mathf.Abs(rest.x) > 50f) return new ShotOutcome("OFF THE SIDE OF THE CLIFF!  •  DOWN INTO THE CANYON", 0f, false);
            if (gap < 0f) return new ShotOutcome(fell ? "OVER THE EDGE!  •  DOWN INTO THE CANYON" : "OVER THE EDGE!  •  " + Dist(-gap) + " TOO FAR", 0f, false);
            if (fell) return new ShotOutcome("OFF THE SIDE OF THE CLIFF!", 0f, false);
            if (Mathf.Abs(rest.x) > HalfWidth) return new ShotOutcome("WIDE OF THE ZONE", 0f, false);
            return new ShotOutcome("SHORT OF THE ZONE  •  " + Dist(gap - zone) + " SHORT", 0f, false);
        }

        public override int NextPlayer(int current)
        {
            if (over) return -1;
            int n = Players.Count;
            for (int k = 1; k <= n; k++)
            {
                int i = (current + k) % n;
                MiniPlayer p = Players[i];
                if (!p.Out && !roundGap.ContainsKey(p)) return i;
            }
            ResolveRound();
            if (over) return -1;
            for (int i = 0; i < n; i++) if (!Players[i].Out) return i;
            return -1;
        }

        private void ResolveRound()
        {
            var alive = new List<MiniPlayer>();
            foreach (MiniPlayer p in Players) if (!p.Out) alive.Add(p);
            var failed = alive.FindAll(p => float.IsPositiveInfinity(roundGap[p]));
            if (Solo)
            {
                MiniPlayer p = alive[0];
                if (failed.Count > 0) { p.Out = true; p.OutRound = round; over = true; roundResult = "GAME OVER  •  " + (round - 1) + " ROUNDS SURVIVED"; }
                else { p.Score = round; roundResult = "ROUND " + round + " SURVIVED"; }
            }
            else if (failed.Count > 0 && failed.Count < alive.Count)
            {
                foreach (MiniPlayer p in failed) { p.Out = true; p.OutRound = round; }
                roundResult = string.Join(", ", failed.ConvertAll(p => p.Name.ToUpperInvariant())) + " KNOCKED OUT";
            }
            else if (failed.Count == 0)
            {
                MiniPlayer worst = alive[0];
                foreach (MiniPlayer p in alive) if (roundGap[p] > roundGap[worst]) worst = p;
                worst.Out = true;
                worst.OutRound = round;
                roundResult = worst.Name.ToUpperInvariant() + " KNOCKED OUT (FURTHEST FROM THE EDGE)";
            }
            else roundResult = "EVERYONE MISSED  •  NOBODY OUT";

            foreach (MiniPlayer p in Players) if (!p.Out) p.Score = round;
            int left = 0;
            foreach (MiniPlayer p in Players) if (!p.Out) left++;
            if (!Solo && left <= 1) over = true;
            if (round >= ShotsPerPlayer) over = true;
            Runner.Announce(roundResult);
            if (over) return;
            round++;
            roundGap.Clear();
            zone = Mathf.Max(4f, zone * 0.8f);
            DrawZone();
        }

        public override float RankKey(MiniPlayer p) => p.Out ? p.OutRound : 1000f + round;
        public override string ScoreText(MiniPlayer p) => Solo ? (p.Out ? p.OutRound - 1 : p.Score).ToString("0") : p.Out ? "OUT R" + p.OutRound : "IN";
        public override string StatusLine() => "ROUND " + round + "  •  EDGE " + Dist(edge, "0") + "  •  ZONE " + Dist(zone, "0");
        public override string ShotLabel(MiniPlayer p) => "ROUND " + round + (ShotsPerPlayer < 99 ? " OF " + ShotsPerPlayer : "");

        public override void DrawLabels(Camera cam)
        {
            float lip = CanyonEdgeMap.TopY(edge, edge), back = CanyonEdgeMap.TopY(edge - zone, edge);
            Runner.WorldLabel(cam, new Vector3(HalfWidth, lip + 5f, edge - 0.75f), "EDGE " + Units.Distance(edge).ToString("0"), new Color(0.95f, 0.2f, 0.15f), 14, new Vector3(HalfWidth, lip + 3f, edge - 0.75f));
            Runner.WorldLabel(cam, new Vector3(-HalfWidth, back + 3f, edge - zone), "ZONE " + Units.Distance(zone).ToString("0"), new Color(0.3f, 0.85f, 0.45f), 13, new Vector3(-HalfWidth, back, edge - zone));
        }
    }

    // =====================================================================================
    //  CAPTURE THE FLAGS
    // =====================================================================================

    /// <summary>
    /// Nine numbered flags, each in a circle. Land inside a circle to take the flag: the circle fills
    /// with your colour and your ball mark stays where it landed. Another player takes it from you
    /// only by landing inside AND closer to the flag than your ball.
    /// </summary>
    public sealed class CaptureFlagsGame : MiniGame
    {
        private sealed class FlagSpot
        {
            public int Number;
            public Vector3 Pos;
            public float Radius;
            public int Owner = -1;
            /// <summary>Distance of the holder's ball from the flag.</summary>
            public float Best = float.MaxValue;
            public Vector3 Mark;
            public Renderer Cloth;
            public GameObject Ring, Fill, MarkObject;
        }

        private readonly List<FlagSpot> flags = new List<FlagSpot>();
        public override string ScoreCaption => "FLAGS";

        public override void Clear() => flags.Clear();

        public override Vector3 AimHint(MiniPlayer p)
        {
            int me = Players.IndexOf(p);
            var free = flags.FindAll(f => f.Owner != me);
            return free.Count > 0 ? free[Random.Range(0, free.Count)].Pos : Focus;
        }

        public override void Build()
        {
            Range.SetTarget(new Vector3(0f, 0f, 150f));
            Range.SetTargetVisible(false);
            Vector2[] spots =
            {
                new Vector2(-18f, 70f), new Vector2(16f, 75f), new Vector2(0f, 100f), new Vector2(-22f, 128f), new Vector2(22f, 132f),
                new Vector2(0f, 160f), new Vector2(-17f, 192f), new Vector2(17f, 196f), new Vector2(0f, 228f)
            };
            int n = 1;
            foreach (Vector2 s in spots)
            {
                var f = new FlagSpot { Number = n++, Pos = new Vector3(s.x + Random.Range(-3f, 3f), 0f, s.y + Random.Range(-6f, 6f)) };
                f.Radius = 7f + f.Pos.z * 0.02f;
                MiniGameWorld.Flag(Root, f.Pos, Color.white, out f.Cloth);
                Paint(f, Color.white);
                flags.Add(f);
            }
        }

        /// <summary>The circle: a light tint and white rim when free, filled with the holder's colour when taken.</summary>
        private void Paint(FlagSpot f, Color owner)
        {
            if (f.Ring != null) Object.Destroy(f.Ring);
            if (f.Fill != null) Object.Destroy(f.Fill);
            bool taken = f.Owner >= 0;
            f.Fill = MiniGameWorld.Disc(Root, f.Pos, f.Radius - 0.35f, taken ? new Color(owner.r, owner.g, owner.b, 0.78f) : new Color(1f, 1f, 1f, 0.12f), 0.06f, "FlagFill");
            f.Ring = MiniGameWorld.Ring(Root, f.Pos, f.Radius - 0.35f, f.Radius, new Color(1f, 1f, 1f, 0.95f), 0.075f, "FlagRing");
        }

        public override ShotOutcome Score(MiniPlayer p, Vector3 landing, Vector3 rest, ShotData shot)
        {
            int me = Players.IndexOf(p);
            FlagSpot hit = null;
            float bestD = float.MaxValue;
            foreach (FlagSpot f in flags)
            {
                float d = Flat(landing, f.Pos);
                if (d < bestD) { bestD = d; hit = f; }
            }
            p.MissTotal += bestD;
            if (hit == null || bestD > hit.Radius)
            {
                p.Log.Add("—");
                return new ShotOutcome("MISSED  •  " + Dist(bestD) + " FROM FLAG " + (hit != null ? hit.Number.ToString() : ""), 0f, false);
            }
            if (hit.Owner == me)
            {
                if (bestD < hit.Best)
                {
                    hit.Best = bestD;
                    SetMark(hit, landing, p.Color);
                    p.Log.Add("=");
                    return new ShotOutcome("FLAG " + hit.Number + " MADE SAFER  •  " + Dist(bestD) + " FROM IT", 0f, true);
                }
                p.Log.Add("=");
                return new ShotOutcome("ALREADY YOUR FLAG " + hit.Number + "  •  " + Dist(bestD), 0f, true);
            }
            if (hit.Owner >= 0 && bestD >= hit.Best)
            {
                MiniPlayer holder = Players[hit.Owner];
                p.Log.Add("—");
                return new ShotOutcome("IN THE CIRCLE BUT NOT CLOSER  •  " + Dist(bestD) + " vs " + holder.Name.ToUpperInvariant() + " " + Dist(hit.Best), 0f, false);
            }
            string text;
            if (hit.Owner >= 0)
            {
                MiniPlayer from = Players[hit.Owner];
                from.Flags--;
                from.Score = from.Flags;
                text = "FLAG " + hit.Number + " STOLEN FROM " + from.Name.ToUpperInvariant() + "!";
            }
            else text = "FLAG " + hit.Number + " CAPTURED!";
            hit.Owner = me;
            hit.Best = bestD;
            p.Flags++;
            p.Score = p.Flags;
            MiniGameWorld.Recolour(hit.Cloth, p.Color);
            Paint(hit, p.Color);
            SetMark(hit, landing, p.Color);
            MiniGameWorld.Sparkle(Root, hit.Pos, p.Color);
            GolfSimAudio.PlayCatch();
            Runner.MapChanged();
            p.Log.Add("F" + hit.Number);
            return new ShotOutcome(text + "  •  " + Dist(bestD) + " FROM IT  •  " + p.Flags + (p.Flags == 1 ? " FLAG" : " FLAGS"), 1f, true);
        }

        /// <summary>The holder's ball mark: a dot in their colour where the ball landed.</summary>
        private void SetMark(FlagSpot f, Vector3 landing, Color c)
        {
            if (f.MarkObject != null) Object.Destroy(f.MarkObject);
            f.Mark = landing;
            f.MarkObject = MiniGameWorld.Shape(PrimitiveType.Sphere, Root, new Vector3(landing.x, MiniGameWorld.Ground(landing) + 0.35f, landing.z), Vector3.one * 0.7f, c, "BallMark", 0.6f);
            MiniGameWorld.Ring(f.MarkObject.transform.parent, landing, 0.55f, 0.85f, Color.white, 0.09f, "BallMarkRing").transform.SetParent(f.MarkObject.transform, true);
        }

        public override float RankKey(MiniPlayer p) => p.Flags * 10000f - (p.Shots > 0 ? p.MissTotal / p.Shots : 9999f);
        public override string ScoreText(MiniPlayer p) => p.Flags.ToString();

        public override void DrawMapMarks(System.Func<Vector3, Vector2> to, Texture2D dot)
        {
            foreach (FlagSpot f in flags)
            {
                Vector2 m = to(f.Pos);
                Vector2 edge = to(f.Pos + Vector3.right * f.Radius);
                float r = Mathf.Max(5f, Mathf.Abs(edge.x - m.x));
                GUI.color = f.Owner >= 0 ? new Color(Players[f.Owner].Color.r, Players[f.Owner].Color.g, Players[f.Owner].Color.b, 0.85f) : new Color(1f, 1f, 1f, 0.35f);
                GUI.DrawTexture(new Rect(m.x - r, m.y - r, r * 2f, r * 2f), dot);
                if (f.Owner >= 0)
                {
                    Vector2 b = to(f.Mark);
                    GUI.color = Color.white;
                    GUI.DrawTexture(new Rect(b.x - 3f, b.y - 3f, 6f, 6f), dot);
                }
            }
            GUI.color = Color.white;
        }

        public override string StatusLine()
        {
            int free = 0;
            foreach (FlagSpot f in flags) if (f.Owner < 0) free++;
            return free + " OF " + flags.Count + " FLAGS FREE";
        }

        public override void DrawLabels(Camera cam)
        {
            foreach (FlagSpot f in flags)
            {
                Color c = f.Owner >= 0 ? Players[f.Owner].Color : Color.white;
                // Number tag on top of the pole, distance tag beside it (like a yardage board).
                Runner.WorldLabel(cam, f.Pos + Vector3.up * 6.8f, f.Number.ToString(), c, 14, f.Pos + Vector3.up * 5.2f);
                Runner.WorldLabel(cam, f.Pos + new Vector3(f.Radius * 0.55f, 1.2f, 0f), Units.Distance(f.Pos.magnitude).ToString("0") + " " + Units.DistanceUnit, Color.white, 12);
            }
        }
    }
}
