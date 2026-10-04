using System.Collections.Generic;
using GolfSimZA.Core;
using UnityEngine;

namespace GolfSimZA.MiniGames
{
    /// <summary>
    /// The numbered, coloured target patches of the TARGET ARENA (301 and HORSE): irregular blobs
    /// on the grass, each with a bright inner part (full value) and an outer part (half value).
    /// A shot counts where the ball comes to rest (after bounce and roll).
    /// </summary>
    public sealed class ArenaTarget
    {
        public int Number;
        public Vector3 Centre;
        public float Radius;
        public int Value;
        public Color Colour;
        public float Phase;

        /// <summary>Edge distance from the centre at this angle (wobbly blob outline).</summary>
        public float EdgeAt(float angle) => Radius * (1f + 0.18f * Mathf.Sin(3f * angle + Phase) + 0.09f * Mathf.Sin(5f * angle + Phase * 1.7f));

        /// <summary>0 = miss, 1 = outer part, 2 = inner part.</summary>
        public int Hit(Vector3 p)
        {
            float dx = p.x - Centre.x, dz = p.z - Centre.z;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            float e = EdgeAt(Mathf.Atan2(dz, dx));
            return d <= e * 0.5f ? 2 : d <= e ? 1 : 0;
        }

        public float DistanceTo(Vector3 p)
        {
            float dx = p.x - Centre.x, dz = p.z - Centre.z;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            return Mathf.Max(0f, d - EdgeAt(Mathf.Atan2(dz, dx)));
        }
    }

    public static class ArenaTargets
    {
        private static readonly Color[] Palette =
        {
            new Color(0.62f, 0.50f, 0.90f), new Color(0.90f, 0.55f, 0.85f), new Color(0.25f, 0.62f, 0.95f), new Color(0.98f, 0.92f, 0.55f),
            new Color(0.95f, 0.38f, 0.38f), new Color(0.98f, 0.62f, 0.25f), new Color(0.95f, 0.45f, 0.40f), new Color(0.70f, 0.90f, 0.30f),
            new Color(0.98f, 0.75f, 0.35f), new Color(0.98f, 0.90f, 0.30f), new Color(0.45f, 0.90f, 0.55f), new Color(0.40f, 0.90f, 0.95f)
        };

        /// <summary>Twelve targets from 55 to 230 m: bigger and lower-valued near the tee, small and valuable far out.</summary>
        public static List<ArenaTarget> Make()
        {
            var spots = new (float x, float z, float r, int v)[]
            {
                (-12f, 58f, 13f, 10), (15f, 72f, 10f, 15), (-18f, 92f, 9f, 20), (2f, 102f, 4.5f, 50),
                (0f, 122f, 10f, 25), (-16f, 142f, 8f, 30), (17f, 147f, 8f, 30), (-9f, 168f, 7f, 40),
                (12f, 175f, 7f, 40), (-6f, 196f, 6f, 50), (10f, 205f, 6f, 50), (0f, 230f, 5.5f, 60)
            };
            var list = new List<ArenaTarget>();
            for (int i = 0; i < spots.Length; i++)
                list.Add(new ArenaTarget
                {
                    Number = i + 1, Centre = new Vector3(spots[i].x, 0f, spots[i].z), Radius = spots[i].r, Value = spots[i].v,
                    Colour = Palette[i % Palette.Length], Phase = Random.Range(0f, 6.28f)
                });
            return list;
        }

        /// <summary>Paints a target on the ground: outer blob, brighter inner blob, white rim.</summary>
        public static void Paint(Transform root, ArenaTarget t)
        {
            Blob(root, t, 1f, Color.Lerp(t.Colour, Color.white, 0.15f) * 0.85f, 0.05f);
            Blob(root, t, 0.5f, Color.Lerp(t.Colour, Color.white, 0.35f), 0.07f);
            Rim(root, t, 1f, 0.35f, new Color(1f, 1f, 1f, 0.9f), 0.075f);
        }

        public static GameObject Highlight(Transform root, ArenaTarget t, Color colour) => Rim(root, t, 1.12f, 0.7f, colour, 0.09f);

        private static GameObject Blob(Transform root, ArenaTarget t, float scale, Color c, float lift)
        {
            const int seg = 64;
            var v = new List<Vector3> { new Vector3(t.Centre.x, MiniGameWorld.Ground(t.Centre) + lift, t.Centre.z) };
            var col = new List<Color> { MapKit.Lin(c) };
            var tri = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                float e = t.EdgeAt(a) * scale;
                var p = new Vector3(t.Centre.x + Mathf.Cos(a) * e, 0f, t.Centre.z + Mathf.Sin(a) * e);
                p.y = MiniGameWorld.Ground(p) + lift;
                v.Add(p);
                col.Add(MapKit.Lin(c));
                if (i > 0) tri.AddRange(new[] { 0, i + 1, i });
            }
            GameObject g = MapKit.Surface(root, "Target" + t.Number, MapKit.Mesh("Target", v, null, col, tri), MapKit.VertexLit, false);
            g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        private static GameObject Rim(Transform root, ArenaTarget t, float scale, float width, Color c, float lift)
        {
            const int seg = 64;
            var v = new List<Vector3>();
            var col = new List<Color>();
            var tri = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                float e = t.EdgeAt(a) * scale;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 inner = t.Centre + d * (e - width), outer = t.Centre + d * e;
                inner.y = MiniGameWorld.Ground(inner) + lift;
                outer.y = MiniGameWorld.Ground(outer) + lift;
                v.Add(inner);
                v.Add(outer);
                col.Add(c);
                col.Add(c);
                if (i < seg)
                {
                    int b = i * 2;
                    tri.AddRange(new[] { b, b + 2, b + 1, b + 2, b + 3, b + 1 });
                }
            }
            GameObject g = MapKit.Surface(root, "TargetRim", MapKit.Mesh("TargetRim", v, null, col, tri), MiniGameWorld.VertexUnlit, false);
            g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        /// <summary>Target at the ball's rest point (or null) and the part hit.</summary>
        public static ArenaTarget Find(List<ArenaTarget> list, Vector3 landing, out int part)
        {
            part = 0;
            foreach (ArenaTarget t in list)
            {
                int h = t.Hit(landing);
                if (h > part) { part = h; return t; }
            }
            return null;
        }

        public static ArenaTarget Nearest(List<ArenaTarget> list, Vector3 p, out float gap)
        {
            ArenaTarget best = null;
            gap = float.MaxValue;
            foreach (ArenaTarget t in list)
            {
                float d = t.DistanceTo(p);
                if (d < gap) { gap = d; best = t; }
            }
            return best;
        }

        public static void Labels(MiniGameRunner runner, Camera cam, List<ArenaTarget> list, ArenaTarget focus)
        {
            // The target to match first, then the rest nearest to farthest.
            if (focus != null)
                runner.WorldLabel(cam, focus.Centre + Vector3.up * 4.5f, "MATCH  #" + focus.Number + "  •  " + Units.Distance(focus.Centre.magnitude).ToString("0") + " " + Units.DistanceUnit, new Color(1f, 0.75f, 0.1f), 15, focus.Centre + Vector3.up * 0.3f);
            foreach (ArenaTarget t in list)
            {
                if (t == focus) continue;
                runner.WorldLabel(cam, t.Centre + Vector3.up * 3f, "#" + t.Number + "  " + t.Value + "  •  " + Units.Distance(t.Centre.magnitude).ToString("0"), t.Colour, 12, t.Centre + Vector3.up * 0.3f);
            }
        }

        public static void MapMarks(System.Func<Vector3, Vector2> to, Texture2D dot, List<ArenaTarget> list, ArenaTarget focus)
        {
            foreach (ArenaTarget t in list)
            {
                Vector2 m = to(t.Centre);
                float r = Mathf.Max(4f, Mathf.Abs(to(t.Centre + Vector3.right * t.Radius).x - m.x));
                GUI.color = new Color(t.Colour.r, t.Colour.g, t.Colour.b, 0.9f);
                GUI.DrawTexture(new Rect(m.x - r, m.y - r, r * 2f, r * 2f), dot);
                if (t == focus)
                {
                    GUI.color = new Color(1f, 0.75f, 0.1f);
                    GUI.DrawTexture(new Rect(m.x - r - 3f, m.y - 1f, r * 2f + 6f, 2f), dot);
                }
            }
            GUI.color = Color.white;
        }
    }

    // =====================================================================================
    //  301 COUNTDOWN
    // =====================================================================================

    /// <summary>
    /// Everyone starts on 301. A target's value comes off your score (half for its outer part).
    /// Go below zero and you bust back to 51. First to exactly zero wins; when the shots run out
    /// the lowest score wins.
    /// </summary>
    public sealed class Countdown301Game : MiniGame
    {
        private const int Start = 301, BustTo = 51, CheckoutAt = 5;
        private List<ArenaTarget> targets;
        private bool finished;
        public override bool HasOwnMap => true;
        public override bool LowerIsBetter => true;
        public override string ScoreCaption => "LEFT";
        public override float MapLength => 270f;
        public override Vector3 Focus => new Vector3(0f, 0f, 130f);
        public override bool FixedShots => false;

        public override void Build()
        {
            finished = false;
            Map = new TargetArenaMap("301");
            Map.Build(Root, Random.Range(1, 9999));
            targets = ArenaTargets.Make();
            foreach (ArenaTarget t in targets) ArenaTargets.Paint(Root, t);
            foreach (MiniPlayer p in Players) p.Score = Start;
            Range.SetTarget(new Vector3(0f, 0f, 130f));
            Range.SetTargetVisible(false);
        }

        public override ShotOutcome Score(MiniPlayer p, Vector3 landing, Vector3 rest, ShotData shot)
        {
            ArenaTarget t = ArenaTargets.Find(targets, rest, out int part);
            if (t == null)
            {
                ArenaTargets.Nearest(targets, rest, out float gap);
                p.Log.Add("0");
                return new ShotOutcome((Runner.BallInWater ? "SPLASH  •  " : "MISSED  •  ") + Dist(gap) + " SHORT OF A TARGET  •  " + p.Score.ToString("0") + " LEFT", 0f, false);
            }
            int pts = part == 2 ? t.Value : Mathf.CeilToInt(t.Value * 0.5f);
            // Checkout: with 5 or less left (less than any target is worth) any target finishes.
            if (p.Score <= CheckoutAt) pts = (int)p.Score;
            float left = p.Score - pts;
            MiniGameWorld.Shape(PrimitiveType.Sphere, Root, new Vector3(rest.x, MiniGameWorld.Ground(rest) + 0.3f, rest.z), Vector3.one * 0.6f, p.Color, "BallMark", 0.6f);
            if (left < 0f)
            {
                p.Score = BustTo;
                p.Log.Add("BUST");
                return new ShotOutcome("BUST!  -" + pts + " WAS TOO MANY  •  BACK TO " + BustTo, 0f, false);
            }
            p.Score = left;
            p.Log.Add("-" + pts);
            if (left == 0f)
            {
                finished = true;
                GolfSimAudio.PlayWin();
                return new ShotOutcome("#" + t.Number + "  -" + pts + "  •  ZERO! " + p.Name.ToUpperInvariant() + " WINS", pts, true);
            }
            GolfSimAudio.PlayCatch();
            return new ShotOutcome("#" + t.Number + (part == 2 ? " CENTRE" : " EDGE") + "  -" + pts + "  •  " + left.ToString("0") + " LEFT", pts, true);
        }

        public override int NextPlayer(int current) => finished ? -1 : base.NextPlayer(current);
        public override float RankKey(MiniPlayer p) => p.Score * 1000f + p.Shots;
        public override string ScoreText(MiniPlayer p) => p.Score.ToString("0");
        public override string StatusLine() => "START " + Start + "  •  BUST BACK TO " + BustTo;
        public override Vector3 AimHint(MiniPlayer p)
        {
            // Any target that cannot bust (the exact finish when there is one).
            if (p.Score <= CheckoutAt) return targets[Random.Range(0, 3)].Centre;
            var ok = targets.FindAll(t => t.Value <= p.Score);
            ArenaTarget exact = ok.Find(t => t.Value == p.Score);
            if (exact != null) return exact.Centre;
            return ok.Count > 0 ? ok[Random.Range(0, ok.Count)].Centre : targets[0].Centre;
        }
        public override void DrawLabels(Camera cam) => ArenaTargets.Labels(Runner, cam, targets, null);
        public override void DrawMapMarks(System.Func<Vector3, Vector2> to, Texture2D dot) => ArenaTargets.MapMarks(to, dot, targets, null);
    }

    // =====================================================================================
    //  HORSE
    // =====================================================================================

    /// <summary>
    /// Follow the leader. The setter hits any target; if it stops in one, every other player must
    /// stop in that same target or take a letter (H-O-R-S-E). If the setter misses, the next
    /// player sets. Spell HORSE and you are out; the last player left wins.
    /// </summary>
    public sealed class HorseGame : MiniGame
    {
        private const string Word = "HORSE";
        private List<ArenaTarget> targets;
        private int setter;
        private ArenaTarget challenge;
        private readonly List<int> queue = new List<int>();
        private GameObject highlight;
        private int totalShots;
        private bool over;

        public override bool HasOwnMap => true;
        public override bool LowerIsBetter => true;
        public override string ScoreCaption => "LETTERS";
        public override float MapLength => 270f;
        public override Vector3 Focus => new Vector3(0f, 0f, 130f);
        public override bool FixedShots => false;
        public override Vector3? MapTarget => challenge?.Centre;
        public override string MapTargetName => challenge != null ? "#" + challenge.Number : "TARGET";

        public override void Clear() { queue.Clear(); challenge = null; }

        public override void Build()
        {
            setter = 0;
            challenge = null;
            queue.Clear();
            totalShots = 0;
            over = false;
            Map = new TargetArenaMap("HORSE");
            Map.Build(Root, Random.Range(1, 9999));
            targets = ArenaTargets.Make();
            foreach (ArenaTarget t in targets) ArenaTargets.Paint(Root, t);
            Range.SetTarget(new Vector3(0f, 0f, 130f));
            Range.SetTargetVisible(false);
        }

        private bool Alive(int i) => !Players[i].Out;
        private int AliveCount { get { int n = 0; for (int i = 0; i < Players.Count; i++) if (Alive(i)) n++; return n; } }

        private int NextAlive(int from)
        {
            for (int k = 1; k <= Players.Count; k++)
            {
                int i = (from + k) % Players.Count;
                if (Alive(i)) return i;
            }
            return from;
        }

        private void SetChallenge(ArenaTarget t)
        {
            challenge = t;
            if (highlight != null) Object.Destroy(highlight);
            highlight = t != null ? ArenaTargets.Highlight(Root, t, new Color(1f, 0.75f, 0.1f, 0.95f)) : null;
            Runner.MapChanged();
        }

        public override ShotOutcome Score(MiniPlayer p, Vector3 landing, Vector3 rest, ShotData shot)
        {
            totalShots++;
            int me = Players.IndexOf(p);
            ArenaTarget hit = ArenaTargets.Find(targets, rest, out _);
            if (challenge == null)
            {
                // Setting a shot.
                if (hit != null)
                {
                    SetChallenge(hit);
                    queue.Clear();
                    for (int k = 1; k < Players.Count; k++)
                    {
                        int i = (setter + k) % Players.Count;
                        if (Alive(i)) queue.Add(i);
                    }
                    p.Log.Add("#" + hit.Number);
                    GolfSimAudio.PlayCatch();
                    if (queue.Count == 0) { SetChallenge(null); setter = NextAlive(setter); }
                    return new ShotOutcome("SHOT SET!  #" + hit.Number + " (" + Dist(hit.Centre.magnitude, "0") + ")  •  EVERYONE MUST MATCH IT", 0f, true);
                }
                p.Log.Add("—");
                setter = NextAlive(setter);
                return new ShotOutcome("MISSED THE TARGETS  •  " + Players[setter].Name.ToUpperInvariant() + " SETS NEXT", 0f, false);
            }

            // Matching the challenge.
            queue.Remove(me);
            ShotOutcome outcome;
            if (hit == challenge)
            {
                p.Log.Add("✓");
                outcome = new ShotOutcome("MATCHED #" + challenge.Number + "!  •  NO LETTER", 0f, true);
            }
            else
            {
                p.Flags++;
                p.Score = p.Flags;
                p.Log.Add(Word.Substring(0, Mathf.Min(p.Flags, Word.Length)));
                if (p.Flags >= Word.Length)
                {
                    p.Out = true;
                    p.OutRound = totalShots;
                    Runner.Announce(p.Name.ToUpperInvariant() + " SPELLS H-O-R-S-E  •  OUT!");
                    outcome = new ShotOutcome("MISSED #" + challenge.Number + "  •  H-O-R-S-E  •  OUT!", 0f, false);
                }
                else outcome = new ShotOutcome("MISSED #" + challenge.Number + "  •  LETTER " + Word[p.Flags - 1] + "  •  " + Letters(p), 0f, false);
            }
            if (queue.Count == 0)
            {
                SetChallenge(null);
                setter = NextAlive(setter);
            }
            return outcome;
        }

        private static string Letters(MiniPlayer p) => p.Flags == 0 ? "—" : string.Join("-", Word.Substring(0, Mathf.Min(p.Flags, Word.Length)).ToCharArray());

        public override int NextPlayer(int current)
        {
            if (over) return -1;
            // Out of shots: the game ends once nobody still has a shot to match.
            if (AliveCount <= (Players.Count > 1 ? 1 : 0) || (queue.Count == 0 && totalShots >= ShotsPerPlayer * Mathf.Max(1, Players.Count)))
            {
                over = true;
                return -1;
            }
            if (queue.Count > 0) return queue[0];
            if (!Alive(setter)) setter = NextAlive(setter);
            return setter;
        }

        public override float RankKey(MiniPlayer p) => p.Out ? 1000f - p.OutRound : p.Flags;
        public override string ScoreText(MiniPlayer p) => p.Out ? "OUT" : Letters(p);
        public override string StatusLine() => challenge != null ? "MATCH #" + challenge.Number + "  •  SET BY " + Players[setter].Name.ToUpperInvariant() : Players[setter].Name.ToUpperInvariant() + " SETS THE SHOT";
        public override string ShotLabel(MiniPlayer p) => challenge != null && Players.IndexOf(p) != setter ? "MATCH TARGET #" + challenge.Number + "  (" + Dist(challenge.Centre.magnitude, "0") + ")" : "SET A SHOT  •  HIT ANY TARGET";

        public override Vector3 AimHint(MiniPlayer p) => challenge != null ? challenge.Centre : targets[Random.Range(0, targets.Count)].Centre;
        public override void DrawLabels(Camera cam) => ArenaTargets.Labels(Runner, cam, targets, challenge);
        public override void DrawMapMarks(System.Func<Vector3, Vector2> to, Texture2D dot) => ArenaTargets.MapMarks(to, dot, targets, challenge);
    }
}
