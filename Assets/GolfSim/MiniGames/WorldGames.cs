using System.Collections;
using System.Collections.Generic;
using GolfSimZA.Core;
using UnityEngine;

namespace GolfSimZA.MiniGames
{
    // =====================================================================================
    //  JUNKYARD BLAST
    // =====================================================================================

    /// <summary>
    /// A junkyard of wrecked cars and coloured blast barrels. A ball landing next to a barrel sets
    /// it off, and every barrel of the same colour within 30 m of an exploding one goes up too
    /// (chain reaction). 100 points per barrel times the chain length.
    /// </summary>
    public sealed class JunkyardBlastGame : MiniGame
    {
        private sealed class BarrelSpot
        {
            public Vector3 Pos;
            public int Colour;
            public GameObject Go;
            public bool Gone;
        }

        private const float ChainRange = 30f;
        private static readonly Color[] Colours =
        {
            new Color(0.92f, 0.20f, 0.15f), new Color(0.20f, 0.45f, 0.95f), new Color(0.22f, 0.80f, 0.30f), new Color(1f, 0.80f, 0.10f)
        };
        private static readonly string[] ColourNames = { "RED", "BLUE", "GREEN", "YELLOW" };
        private readonly List<BarrelSpot> barrels = new List<BarrelSpot>();
        private Transform barrelRoot;
        private readonly List<Vector3> clusters = new List<Vector3>();

        public override Vector3 Focus => new Vector3(0f, 0f, 150f);
        public override float MapLength => 260f;

        public override void Clear()
        {
            barrels.Clear();
            barrelRoot = null;
        }

        public override void Build()
        {
            Range.SetTarget(new Vector3(0f, 0f, 150f));
            Range.SetTargetVisible(false);

            // Dirt yard patches with piles of wrecks.
            Color dirt = new Color(0.42f, 0.34f, 0.24f, 0.85f);
            Color[] paints =
            {
                new Color(0.55f, 0.12f, 0.10f), new Color(0.18f, 0.30f, 0.45f), new Color(0.75f, 0.70f, 0.60f), new Color(0.25f, 0.35f, 0.20f),
                new Color(0.60f, 0.45f, 0.15f), new Color(0.30f, 0.30f, 0.32f)
            };
            for (int i = 0; i < 9; i++)
            {
                Vector3 c = new Vector3(Random.Range(-45f, 45f), 0f, Random.Range(60f, 240f));
                MiniGameWorld.Disc(Root, c, Random.Range(9f, 14f), dirt, 0.03f, "Dirt");
                int cars = Random.Range(2, 5);
                for (int k = 0; k < cars; k++)
                {
                    Vector3 p = c + new Vector3(Random.Range(-7f, 7f), 0f, Random.Range(-7f, 7f));
                    MiniGameWorld.Car(Root, p, Random.Range(0f, 360f), paints[Random.Range(0, paints.Length)], Random.value < 0.35f);
                }
                // Scrap: tyres and crates.
                for (int k = 0; k < 4; k++)
                {
                    Vector3 p = c + new Vector3(Random.Range(-9f, 9f), 0f, Random.Range(-9f, 9f));
                    if (Random.value < 0.5f)
                    {
                        GameObject tyre = MiniGameWorld.Shape(PrimitiveType.Cylinder, Root, new Vector3(p.x, MiniGameWorld.Ground(p) + 0.2f, p.z), new Vector3(1.1f, 0.2f, 1.1f), new Color(0.07f, 0.07f, 0.07f), "Tyre");
                        tyre.transform.rotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), 0f);
                    }
                    else
                    {
                        GameObject crate = MiniGameWorld.Shape(PrimitiveType.Cube, Root, new Vector3(p.x, MiniGameWorld.Ground(p) + 0.6f, p.z), Vector3.one * 1.2f, new Color(0.55f, 0.40f, 0.22f), "Crate");
                        crate.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 90f), 0f);
                    }
                }
            }
            SpawnBarrels();
        }

        /// <summary>Clusters of 3-5 barrels in one or two colours spread down the range.</summary>
        private void SpawnBarrels()
        {
            if (barrelRoot != null) Object.Destroy(barrelRoot.gameObject);
            barrels.Clear();
            clusters.Clear();
            barrelRoot = new GameObject("Barrels").transform;
            barrelRoot.SetParent(Root, false);
            Vector2[] spots =
            {
                new Vector2(-20f, 75f), new Vector2(18f, 95f), new Vector2(-8f, 125f), new Vector2(24f, 150f),
                new Vector2(-26f, 170f), new Vector2(4f, 200f), new Vector2(-14f, 225f)
            };
            foreach (Vector2 c in spots)
            {
                Vector3 centre = new Vector3(c.x + Random.Range(-5f, 5f), 0f, c.y + Random.Range(-8f, 8f));
                clusters.Add(centre);
                int a = Random.Range(0, Colours.Length), b = Random.Range(0, Colours.Length);
                int count = Random.Range(3, 6);
                for (int i = 0; i < count; i++)
                {
                    Vector3 p = centre + new Vector3(Random.Range(-10f, 10f), 0f, Random.Range(-10f, 10f));
                    bool clash = false;
                    foreach (BarrelSpot o in barrels) if (Flat(o.Pos, p) < 3.5f) { clash = true; break; }
                    if (clash) continue;
                    int colour = Random.value < 0.65f ? a : b;
                    barrels.Add(new BarrelSpot { Pos = p, Colour = colour, Go = MiniGameWorld.Barrel(barrelRoot, p, Colours[colour]) });
                }
            }
            Runner.MapChanged();
        }

        private int Left
        {
            get
            {
                int n = 0;
                foreach (BarrelSpot b in barrels) if (!b.Gone) n++;
                return n;
            }
        }

        public override ShotOutcome Score(MiniPlayer p, Vector3 landing, Vector3 rest, ShotData shot)
        {
            BarrelSpot hit = null;
            float bestD = float.MaxValue;
            foreach (BarrelSpot b in barrels)
            {
                if (b.Gone) continue;
                float d = Flat(landing, b.Pos);
                if (d < bestD) { bestD = d; hit = b; }
            }
            float radius = 5f + landing.z * 0.02f;
            if (hit == null || bestD > radius)
            {
                p.Log.Add("0");
                return new ShotOutcome(hit == null ? "MISSED" : "MISSED  •  " + Dist(bestD) + " FROM THE NEAREST BARREL", 0f, false);
            }

            // Chain reaction: every same-colour barrel within 30 m of one that blows.
            var chain = new List<BarrelSpot> { hit };
            hit.Gone = true;
            for (int i = 0; i < chain.Count; i++)
                foreach (BarrelSpot b in barrels)
                    if (!b.Gone && b.Colour == hit.Colour && Flat(b.Pos, chain[i].Pos) <= ChainRange)
                    {
                        b.Gone = true;
                        chain.Add(b);
                    }
            int n = chain.Count;
            float pts = 100f * n * n;
            p.Score += pts;
            p.Log.Add(pts.ToString("0"));
            Runner.StartCoroutine(Blow(chain));

            string text = n == 1 ? "BOOM!  1 " + ColourNames[hit.Colour] + " BARREL  •  +" + pts.ToString("0")
                                 : "CHAIN REACTION!  " + n + " " + ColourNames[hit.Colour] + " BARRELS  •  +" + pts.ToString("0");
            if (Left < 6) Runner.StartCoroutine(Restock());
            return new ShotOutcome(text, pts, true);
        }

        private IEnumerator Blow(List<BarrelSpot> chain)
        {
            foreach (BarrelSpot b in chain)
            {
                if (b.Go != null)
                {
                    MiniGameWorld.Explosion(Root, b.Go.transform.position, Colours[b.Colour]);
                    Object.Destroy(b.Go);
                }
                GolfSimAudio.PlayBlast(b == chain[0] ? 1f : 0.7f);
                yield return new WaitForSeconds(0.2f);
            }
            Runner.MapChanged();
        }

        private IEnumerator Restock()
        {
            yield return new WaitForSeconds(2.2f);
            SpawnBarrels();
            Runner.Announce("NEW BARRELS DELIVERED");
        }

        public override string StatusLine() => Left + " BARRELS LEFT";

        public override Vector3 AimHint(MiniPlayer p)
        {
            var alive = barrels.FindAll(b => !b.Gone);
            return alive.Count > 0 ? alive[Random.Range(0, alive.Count)].Pos : Focus;
        }

        public override void DrawMapMarks(System.Func<Vector3, Vector2> to, Texture2D dot)
        {
            foreach (BarrelSpot b in barrels)
            {
                if (b.Gone) continue;
                Vector2 m = to(b.Pos);
                GUI.color = Colours[b.Colour];
                GUI.DrawTexture(new Rect(m.x - 3f, m.y - 3f, 6f, 6f), dot);
            }
            GUI.color = Color.white;
        }

        public override void DrawLabels(Camera cam)
        {
            // One distance label per barrel group that still has barrels.
            foreach (Vector3 c in clusters)
            {
                bool any = false;
                foreach (BarrelSpot b in barrels) if (!b.Gone && Flat(b.Pos, c) < 14f) { any = true; break; }
                if (any) Runner.WorldLabel(cam, c + Vector3.up * 6f, Units.Distance(c.magnitude).ToString("0") + " " + Units.DistanceUnit, Color.white, 12, c + Vector3.up * 2.6f);
            }
        }
    }

    // =====================================================================================
    //  CREATURE GAMES (shared)
    // =====================================================================================

    /// <summary>A kind of creature: points, speed, how close the ball must land, where it lives.</summary>
    public sealed class CreatureKind
    {
        public string Name;
        public string Plural;
        public int Points;
        public float Speed;
        /// <summary>Catch radius at the tee (grows 2 cm per metre away).</summary>
        public float Catch;
        /// <summary>How often it appears (relative).</summary>
        public float Weight;
        public Color Colour;
        /// <summary>0 land round the water, 1 on the water, 2 the shore, 3 flies over everything.</summary>
        public int Habitat;
        public float FlyHeight;
        /// <summary>Walks sideways (crab).</summary>
        public bool Sideways;
        /// <summary>Hops along (frog).</summary>
        public bool Hops;
        /// <summary>Only one at a time (the rare one).</summary>
        public bool Rare;
    }

    /// <summary>Base for CREATURE POND and DESERT OASIS: creatures wander round a water hole and are caught by landing the ball on them.</summary>
    public abstract class CreatureGame : MiniGame
    {
        protected sealed class Creature
        {
            public CreatureKind Kind;
            public Transform T;
            public Vector3 Pos;
            public Vector3 Goal;
            public float Wait;
            public float Phase;
            public Transform[] Wings;
            public Transform[] Segments;
            public Transform Body;
        }

        protected readonly List<Creature> live = new List<Creature>();
        private readonly List<float> respawn = new List<float>();
        protected Vector3 Centre = new Vector3(0f, 0f, 150f);
        protected float WaterRadius = 26f;
        protected const int Population = 13;

        protected abstract CreatureKind[] Kinds { get; }
        protected abstract void BuildScenery();
        /// <summary>Builds the model under <paramref name="root"/> facing +z (sets wings / segments to animate).</summary>
        protected abstract void MakeModel(Creature c, Transform root);

        public override Vector3 Focus => Centre;
        public override float MapLength => 250f;
        public override string ScoreCaption => Explore ? "CAUGHT" : "POINTS";
        public override bool HasCollection => true;

        public override void Clear()
        {
            live.Clear();
            respawn.Clear();
        }

        public override void Build()
        {
            Centre = new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(135f, 160f));
            Range.SetTarget(Centre);
            Range.SetTargetVisible(false);
            BuildScenery();
            for (int i = 0; i < Population; i++) Spawn();
        }

        private CreatureKind PickKind()
        {
            float total = 0f;
            foreach (CreatureKind k in Kinds) if (!(k.Rare && Has(k))) total += k.Weight;
            float r = Random.value * total;
            foreach (CreatureKind k in Kinds)
            {
                if (k.Rare && Has(k)) continue;
                r -= k.Weight;
                if (r <= 0f) return k;
            }
            return Kinds[0];
        }

        private bool Has(CreatureKind k)
        {
            foreach (Creature c in live) if (c.Kind == k) return true;
            return false;
        }

        private void Spawn()
        {
            CreatureKind kind = PickKind();
            var c = new Creature { Kind = kind, Phase = Random.Range(0f, 10f) };
            c.Pos = RandomSpot(kind);
            c.Goal = RandomSpot(kind);
            c.Wait = Random.Range(0f, 2f);
            var go = new GameObject(kind.Name);
            go.transform.SetParent(Root, false);
            c.T = go.transform;
            c.Body = new GameObject("Body").transform;
            c.Body.SetParent(c.T, false);
            MakeModel(c, c.Body);
            Place(c, 0f);
            live.Add(c);
            Runner.MapChanged();
        }

        /// <summary>A point where this kind lives.</summary>
        protected Vector3 RandomSpot(CreatureKind kind)
        {
            for (int tries = 0; tries < 30; tries++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float r;
                switch (kind.Habitat)
                {
                    case 1: r = Random.Range(0f, WaterRadius - 4f); break;
                    case 2: r = Random.Range(WaterRadius - 3f, WaterRadius + 12f); break;
                    case 3: r = Random.Range(0f, 75f); break;
                    default: r = Random.Range(WaterRadius + 6f, 75f); break;
                }
                Vector3 p = Centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (p.z < 55f || p.z > 245f || Mathf.Abs(p.x) > 60f) continue;
                return p;
            }
            return Centre + new Vector3(WaterRadius + 10f, 0f, 0f);
        }

        private void Place(Creature c, float t)
        {
            float ground = MiniGameWorld.Ground(c.Pos);
            float y = ground;
            if (c.Kind.Habitat == 1) y += 0.05f;
            if (c.Kind.Habitat == 3) y += c.Kind.FlyHeight + Mathf.Sin(t * 1.3f + c.Phase) * 1.2f;
            if (c.Kind.Hops && c.Wait <= 0f) y += Mathf.Abs(Mathf.Sin(t * 6f + c.Phase)) * 0.9f;
            c.T.position = new Vector3(c.Pos.x, y, c.Pos.z);
        }

        public override void Tick()
        {
            float t = Time.time, dt = Time.deltaTime;
            for (int i = respawn.Count - 1; i >= 0; i--)
                if (t >= respawn[i]) { respawn.RemoveAt(i); Spawn(); }

            foreach (Creature c in live)
            {
                if (c.T == null) continue;
                if (c.Wait > 0f)
                {
                    c.Wait -= dt;
                }
                else
                {
                    Vector3 to = c.Goal - c.Pos;
                    to.y = 0f;
                    float step = c.Kind.Speed * dt;
                    if (to.magnitude <= step)
                    {
                        c.Pos = c.Goal;
                        c.Goal = RandomSpot(c.Kind);
                        c.Wait = c.Kind.Habitat == 3 ? Random.Range(0f, 0.5f) : Random.Range(0.6f, 3.5f);
                    }
                    else
                    {
                        Vector3 dir = to.normalized;
                        c.Pos += dir * step;
                        Vector3 face = c.Kind.Sideways ? new Vector3(-dir.z, 0f, dir.x) : dir;
                        c.T.rotation = Quaternion.Slerp(c.T.rotation, Quaternion.LookRotation(face, Vector3.up), dt * 5f);
                    }
                }
                Place(c, t);
                if (c.Wings != null)
                {
                    float flap = Mathf.Sin(t * 7f + c.Phase) * 35f;
                    for (int w = 0; w < c.Wings.Length; w++)
                        c.Wings[w].localRotation = Quaternion.Euler(0f, 0f, w == 0 ? flap : -flap);
                }
                if (c.Segments != null)
                {
                    bool moving = c.Wait <= 0f;
                    for (int s = 0; s < c.Segments.Length; s++)
                    {
                        Vector3 lp = c.Segments[s].localPosition;
                        lp.x = moving ? Mathf.Sin(t * 5f - s * 0.8f + c.Phase) * 0.45f : lp.x * 0.95f;
                        c.Segments[s].localPosition = lp;
                    }
                }
            }
        }

        public override ShotOutcome Score(MiniPlayer p, Vector3 landing, Vector3 rest, ShotData shot)
        {
            Creature best = null;
            float bestD = float.MaxValue;
            foreach (Creature c in live)
            {
                float d = Flat(landing, c.Pos);
                if (d < bestD) { bestD = d; best = c; }
            }
            if (best == null)
            {
                p.Log.Add("—");
                return new ShotOutcome("MISSED", 0f, false);
            }
            float radius = best.Kind.Catch + 1f + landing.z * 0.02f;
            if (bestD > radius)
            {
                p.Log.Add("—");
                return new ShotOutcome("MISSED  •  THE " + best.Kind.Name + " WAS " + Dist(bestD) + " AWAY", 0f, false);
            }
            CreatureKind kind = best.Kind;
            live.Remove(best);
            MiniGameWorld.Sparkle(Root, best.T.position, kind.Colour);
            GolfSimAudio.PlayCatch();
            Object.Destroy(best.T.gameObject);
            respawn.Add(Time.time + 2.5f);
            Runner.MapChanged();
            p.Caught++;
            p.Log.Add(kind.Name);
            if (!Explore) p.Score += kind.Points;
            else p.Score = p.Caught;
            string article = "AEIOU".IndexOf(kind.Name[0]) >= 0 ? "AN " : "A ";
            return new ShotOutcome("CAUGHT " + article + kind.Name + "!" + (Explore ? "  •  " + p.Caught + " CAUGHT" : "  •  +" + kind.Points), Explore ? 1f : kind.Points, true);
        }

        public override Vector3 AimHint(MiniPlayer p) => live.Count > 0 ? live[Random.Range(0, live.Count)].Pos : Centre;

        public override void ShowMoving(bool visible)
        {
            foreach (Creature c in live) if (c.T != null) c.T.gameObject.SetActive(visible);
        }

        public override string ScoreText(MiniPlayer p) => Explore ? p.Caught.ToString() : p.Score.ToString("0");
        public override float RankKey(MiniPlayer p) => Explore ? p.Caught : p.Score;

        public override string StatusLine()
        {
            foreach (CreatureKind k in Kinds)
                if (k.Rare && Has(k)) return "A " + k.Name + " IS ABOUT!  (" + k.Points + ")";
            return live.Count + " CREATURES ABOUT";
        }

        /// <summary>What the player caught ("2 FROGS, 1 SWAN").</summary>
        public override string Collection(MiniPlayer p)
        {
            var parts = new List<string>();
            foreach (CreatureKind k in Kinds)
            {
                int n = 0;
                foreach (string s in p.Log) if (s == k.Name) n++;
                if (n > 0) parts.Add(n + " " + (n == 1 ? k.Name : k.Plural));
            }
            return parts.Count > 0 ? string.Join(", ", parts) : "Nothing caught";
        }

        public override void DrawLabels(Camera cam)
        {
            // The rare one first, then the nearest ones (far labels that would overlap are dropped).
            var order = new List<Creature>(live);
            Vector3 eye = cam != null ? cam.transform.position : Vector3.zero;
            order.Sort((a, b) => a.Kind.Rare != b.Kind.Rare ? (a.Kind.Rare ? -1 : 1) : (a.Pos - eye).sqrMagnitude.CompareTo((b.Pos - eye).sqrMagnitude));
            foreach (Creature c in order)
            {
                if (c.T == null) continue;
                bool rare = c.Kind.Rare;
                string text = Explore ? c.Kind.Name : c.Kind.Name + "  " + c.Kind.Points;
                Runner.WorldLabel(cam, c.T.position + Vector3.up * (c.Kind.Habitat == 3 ? 3.5f : 3.2f), text, rare ? new Color(1f, 0.75f, 0.1f) : c.Kind.Colour, rare ? 14 : 11, c.T.position + Vector3.up * 1.4f);
            }
        }

        public override void DrawMapMarks(System.Func<Vector3, Vector2> to, Texture2D dot)
        {
            foreach (Creature c in live)
            {
                Vector2 m = to(c.Pos);
                float s = c.Kind.Rare ? 9f : 6f;
                GUI.color = c.Kind.Rare ? new Color(1f, 0.85f, 0.25f) : Color.Lerp(c.Kind.Colour, Color.white, 0.3f);
                GUI.DrawTexture(new Rect(m.x - s * 0.5f, m.y - s * 0.5f, s, s), dot);
            }
            GUI.color = Color.white;
        }

        // ---- Model helpers (primitives, no colliders)

        protected static GameObject Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color colour, Vector3 euler = default, float smooth = 0.3f)
        {
            GameObject g = MiniGameWorld.Shape(type, parent, pos, scale, colour, null, smooth);
            g.transform.localRotation = Quaternion.Euler(euler);
            return g;
        }

        /// <summary>Two eyes with pupils looking forward.</summary>
        protected static void Eyes(Transform parent, Vector3 centre, float apart, float size)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 e = centre + new Vector3(s * apart * 0.5f, 0f, 0f);
                Part(PrimitiveType.Sphere, parent, e, Vector3.one * size, Color.white, default, 0.7f);
                Part(PrimitiveType.Sphere, parent, e + new Vector3(0f, 0f, size * 0.42f), Vector3.one * size * 0.5f, new Color(0.05f, 0.05f, 0.05f), default, 0.9f);
            }
        }

        /// <summary>Pair of flapping wings (pivots at the shoulders).</summary>
        protected static Transform[] Wings(Transform parent, Vector3 shoulder, float span, float depth, Color colour)
        {
            var wings = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                var pivot = new GameObject(i == 0 ? "WingL" : "WingR").transform;
                pivot.SetParent(parent, false);
                pivot.localPosition = shoulder + new Vector3(s * 0.4f, 0f, 0f);
                Part(PrimitiveType.Cube, pivot, new Vector3(s * span * 0.5f, 0f, 0f), new Vector3(span, 0.08f, depth), colour);
                Part(PrimitiveType.Cube, pivot, new Vector3(s * span * 0.85f, 0f, -depth * 0.35f), new Vector3(span * 0.45f, 0.06f, depth * 0.8f), Color.Lerp(colour, Color.black, 0.25f));
                wings[i] = pivot;
            }
            return wings;
        }
    }

    // =====================================================================================
    //  CREATURE POND
    // =====================================================================================

    public sealed class CreaturePondGame : CreatureGame
    {
        private static readonly CreatureKind Frog = new CreatureKind { Name = "FROG", Plural = "FROGS", Points = 50, Speed = 3.2f, Catch = 6f, Weight = 5f, Colour = new Color(0.30f, 0.75f, 0.25f), Habitat = 2, Hops = true };
        private static readonly CreatureKind Crab = new CreatureKind { Name = "CRAB", Plural = "CRABS", Points = 75, Speed = 2.6f, Catch = 5.5f, Weight = 4f, Colour = new Color(0.92f, 0.36f, 0.20f), Habitat = 0, Sideways = true };
        private static readonly CreatureKind Turtle = new CreatureKind { Name = "TURTLE", Plural = "TURTLES", Points = 100, Speed = 1.2f, Catch = 5.5f, Weight = 3f, Colour = new Color(0.25f, 0.50f, 0.25f), Habitat = 2 };
        private static readonly CreatureKind Swan = new CreatureKind { Name = "SWAN", Plural = "SWANS", Points = 150, Speed = 2.0f, Catch = 5f, Weight = 2f, Colour = new Color(0.97f, 0.97f, 0.97f), Habitat = 1 };
        private static readonly CreatureKind Dragon = new CreatureKind { Name = "DRAGON", Plural = "DRAGONS", Points = 400, Speed = 7f, Catch = 6f, Weight = 0.6f, Colour = new Color(0.58f, 0.28f, 0.90f), Habitat = 3, FlyHeight = 7f, Rare = true };
        private static readonly CreatureKind[] All = { Frog, Crab, Turtle, Swan, Dragon };

        protected override CreatureKind[] Kinds => All;

        protected override void BuildScenery()
        {
            WaterRadius = 26f;
            MiniGameWorld.Water(Root, Centre, WaterRadius, new Color(0.14f, 0.42f, 0.62f), new Color(0.50f, 0.46f, 0.32f, 0.9f));
            // Magic glow ring round the water.
            MiniGameWorld.Ring(Root, Centre, WaterRadius + 4.2f, WaterRadius + 4.9f, new Color(0.55f, 0.95f, 1f, 0.55f), 0.08f, "Glow");
            // Lily pads.
            for (int i = 0; i < 14; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(3f, WaterRadius - 3f);
                Vector3 p = Centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                MiniGameWorld.Disc(Root, p, Random.Range(0.9f, 1.6f), new Color(0.22f, 0.62f, 0.25f, 1f), 0.09f, "Lily");
                if (Random.value < 0.4f) MiniGameWorld.Shape(PrimitiveType.Sphere, Root, new Vector3(p.x, MiniGameWorld.Ground(p) + 0.25f, p.z), new Vector3(0.5f, 0.3f, 0.5f), new Color(1f, 0.6f, 0.8f), "Flower");
            }
            // Reeds and rocks on the bank.
            for (int i = 0; i < 26; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), r = WaterRadius + Random.Range(-1f, 3f);
                Vector3 p = Centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                float g = MiniGameWorld.Ground(p);
                if (i % 3 == 0)
                    MiniGameWorld.Shape(PrimitiveType.Sphere, Root, new Vector3(p.x, g + 0.3f, p.z), new Vector3(Random.Range(1.2f, 2.4f), Random.Range(0.6f, 1.1f), Random.Range(1.2f, 2.2f)), new Color(0.48f, 0.48f, 0.46f), "Rock");
                else
                    for (int k = 0; k < 4; k++)
                    {
                        float h = Random.Range(1.2f, 2.2f);
                        MiniGameWorld.Shape(PrimitiveType.Cylinder, Root, new Vector3(p.x + Random.Range(-0.5f, 0.5f), g + h, p.z + Random.Range(-0.5f, 0.5f)), new Vector3(0.1f, h, 0.1f), new Color(0.35f, 0.55f, 0.22f), "Reed");
                    }
            }
            // Glowing mushrooms dotted round the meadow.
            for (int i = 0; i < 18; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(WaterRadius + 8f, 70f);
                Vector3 p = Centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                float g = MiniGameWorld.Ground(p);
                MiniGameWorld.Shape(PrimitiveType.Cylinder, Root, new Vector3(p.x, g + 0.35f, p.z), new Vector3(0.25f, 0.35f, 0.25f), new Color(0.95f, 0.92f, 0.85f), "Stem");
                MiniGameWorld.Shape(PrimitiveType.Sphere, Root, new Vector3(p.x, g + 0.75f, p.z), new Vector3(1.0f, 0.5f, 1.0f), i % 2 == 0 ? new Color(0.35f, 0.85f, 1f) : new Color(0.85f, 0.40f, 1f), "Cap", 0.8f);
            }
        }

        protected override void MakeModel(Creature c, Transform b)
        {
            CreatureKind k = c.Kind;
            if (k == Frog)
            {
                Part(PrimitiveType.Sphere, b, new Vector3(0f, 0.6f, 0f), new Vector3(1.6f, 1.0f, 1.8f), k.Colour);
                Part(PrimitiveType.Sphere, b, new Vector3(0f, 0.95f, 0.8f), new Vector3(1.2f, 0.8f, 1.0f), k.Colour);
                Part(PrimitiveType.Sphere, b, new Vector3(0f, 0.55f, 0.35f), new Vector3(1.2f, 0.7f, 1.3f), new Color(0.85f, 0.90f, 0.55f));
                Eyes(b, new Vector3(0f, 1.4f, 0.95f), 0.75f, 0.45f);
                for (int s = -1; s <= 1; s += 2)
                    Part(PrimitiveType.Sphere, b, new Vector3(s * 0.8f, 0.3f, -0.4f), new Vector3(0.6f, 0.4f, 1.2f), Color.Lerp(k.Colour, Color.black, 0.2f));
            }
            else if (k == Crab)
            {
                Part(PrimitiveType.Sphere, b, new Vector3(0f, 0.8f, 0f), new Vector3(2.2f, 0.8f, 1.6f), k.Colour);
                for (int s = -1; s <= 1; s += 2)
                {
                    Part(PrimitiveType.Sphere, b, new Vector3(s * 1.5f, 0.95f, 0.9f), new Vector3(0.8f, 0.6f, 0.9f), Color.Lerp(k.Colour, Color.white, 0.15f));
                    Part(PrimitiveType.Cylinder, b, new Vector3(s * 0.35f, 1.35f, 0.55f), new Vector3(0.1f, 0.3f, 0.1f), k.Colour);
                    Part(PrimitiveType.Sphere, b, new Vector3(s * 0.35f, 1.7f, 0.6f), Vector3.one * 0.25f, Color.black);
                    for (int l = 0; l < 3; l++)
                        Part(PrimitiveType.Cube, b, new Vector3(s * 1.25f, 0.4f, -0.5f + l * 0.45f), new Vector3(0.9f, 0.1f, 0.12f), Color.Lerp(k.Colour, Color.black, 0.2f), new Vector3(0f, 0f, s * -35f));
                }
            }
            else if (k == Turtle)
            {
                Part(PrimitiveType.Sphere, b, new Vector3(0f, 0.55f, 0f), new Vector3(2.4f, 1.1f, 2.8f), k.Colour);
                Part(PrimitiveType.Sphere, b, new Vector3(0f, 0.95f, 0f), new Vector3(1.4f, 0.4f, 1.6f), Color.Lerp(k.Colour, new Color(0.6f, 0.5f, 0.2f), 0.5f));
                Part(PrimitiveType.Sphere, b, new Vector3(0f, 0.55f, 1.75f), new Vector3(0.7f, 0.6f, 0.9f), new Color(0.55f, 0.70f, 0.40f));
                Eyes(b, new Vector3(0f, 0.75f, 1.95f), 0.4f, 0.18f);
                for (int s = -1; s <= 1; s += 2)
                    for (int e = -1; e <= 1; e += 2)
                        Part(PrimitiveType.Sphere, b, new Vector3(s * 1.2f, 0.25f, e * 0.9f), new Vector3(0.7f, 0.25f, 0.5f), new Color(0.55f, 0.70f, 0.40f));
            }
            else if (k == Swan)
            {
                Part(PrimitiveType.Sphere, b, new Vector3(0f, 0.7f, 0f), new Vector3(1.5f, 1.1f, 2.4f), k.Colour, default, 0.5f);
                Part(PrimitiveType.Sphere, b, new Vector3(0f, 1.1f, -0.9f), new Vector3(1.2f, 0.7f, 0.9f), k.Colour, new Vector3(-25f, 0f, 0f), 0.5f);
                Part(PrimitiveType.Cylinder, b, new Vector3(0f, 1.8f, 0.9f), new Vector3(0.32f, 0.8f, 0.32f), k.Colour, new Vector3(12f, 0f, 0f), 0.5f);
                Part(PrimitiveType.Sphere, b, new Vector3(0f, 2.6f, 1.15f), new Vector3(0.55f, 0.5f, 0.7f), k.Colour, default, 0.5f);
                Part(PrimitiveType.Cube, b, new Vector3(0f, 2.55f, 1.6f), new Vector3(0.18f, 0.16f, 0.5f), new Color(1f, 0.55f, 0.1f));
                Eyes(b, new Vector3(0f, 2.7f, 1.3f), 0.42f, 0.12f);
            }
            else // Dragon
            {
                Color belly = new Color(0.95f, 0.75f, 0.35f);
                Part(PrimitiveType.Capsule, b, Vector3.zero, new Vector3(1.4f, 1.6f, 1.3f), k.Colour, new Vector3(90f, 0f, 0f), 0.6f);
                Part(PrimitiveType.Capsule, b, new Vector3(0f, -0.2f, 0.1f), new Vector3(1.0f, 1.2f, 0.9f), belly, new Vector3(90f, 0f, 0f));
                Part(PrimitiveType.Cylinder, b, new Vector3(0f, 0.5f, 1.6f), new Vector3(0.5f, 0.6f, 0.5f), k.Colour, new Vector3(50f, 0f, 0f));
                Part(PrimitiveType.Sphere, b, new Vector3(0f, 0.9f, 2.2f), new Vector3(0.95f, 0.8f, 1.2f), k.Colour, default, 0.6f);
                Part(PrimitiveType.Cube, b, new Vector3(0f, 0.75f, 2.8f), new Vector3(0.6f, 0.35f, 0.6f), k.Colour);
                Eyes(b, new Vector3(0f, 1.15f, 2.55f), 0.6f, 0.25f);
                for (int s = -1; s <= 1; s += 2)
                    Part(PrimitiveType.Cube, b, new Vector3(s * 0.3f, 1.45f, 2.0f), new Vector3(0.12f, 0.6f, 0.12f), new Color(0.95f, 0.92f, 0.8f), new Vector3(-30f, 0f, s * -20f));
                Part(PrimitiveType.Cylinder, b, new Vector3(0f, 0f, -2.2f), new Vector3(0.35f, 1.2f, 0.35f), k.Colour, new Vector3(100f, 0f, 0f));
                Part(PrimitiveType.Cube, b, new Vector3(0f, -0.2f, -3.4f), new Vector3(0.7f, 0.1f, 0.7f), belly, new Vector3(0f, 45f, 0f));
                c.Wings = Wings(b, new Vector3(0f, 0.5f, 0.3f), 3.4f, 1.8f, Color.Lerp(k.Colour, new Color(0.2f, 0.9f, 0.8f), 0.35f));
            }
        }
    }

    // =====================================================================================
    //  DESERT OASIS
    // =====================================================================================

    public sealed class DesertOasisGame : CreatureGame
    {
        private static readonly CreatureKind Scorpion = new CreatureKind { Name = "SCORPION", Plural = "SCORPIONS", Points = 50, Speed = 2.4f, Catch = 6f, Weight = 5f, Colour = new Color(0.30f, 0.18f, 0.10f), Habitat = 0 };
        private static readonly CreatureKind Lizard = new CreatureKind { Name = "LIZARD", Plural = "LIZARDS", Points = 75, Speed = 5.5f, Catch = 5.5f, Weight = 4f, Colour = new Color(0.40f, 0.62f, 0.30f), Habitat = 0 };
        private static readonly CreatureKind Fox = new CreatureKind { Name = "FENNEC FOX", Plural = "FENNEC FOXES", Points = 100, Speed = 3.6f, Catch = 5.5f, Weight = 3f, Colour = new Color(0.92f, 0.62f, 0.30f), Habitat = 2 };
        private static readonly CreatureKind Serpent = new CreatureKind { Name = "SAND SERPENT", Plural = "SAND SERPENTS", Points = 150, Speed = 2.2f, Catch = 5f, Weight = 2f, Colour = new Color(0.78f, 0.62f, 0.28f), Habitat = 0 };
        private static readonly CreatureKind Phoenix = new CreatureKind { Name = "GOLDEN PHOENIX", Plural = "GOLDEN PHOENIXES", Points = 400, Speed = 7.5f, Catch = 6f, Weight = 0.6f, Colour = new Color(1f, 0.72f, 0.12f), Habitat = 3, FlyHeight = 8f, Rare = true };
        private static readonly CreatureKind[] All = { Scorpion, Lizard, Fox, Serpent, Phoenix };

        protected override CreatureKind[] Kinds => All;

        protected override void BuildScenery()
        {
            WaterRadius = 16f;
            Color sand = new Color(0.86f, 0.74f, 0.50f);
            // Sand over the range round the oasis.
            GameObject sandDisc = MiniGameWorld.Disc(Root, Centre, 85f, sand, 0.025f, "Sand");
            sandDisc.GetComponent<MeshRenderer>().sharedMaterial = MiniGameWorld.Lit(sand, 0.05f);
            MiniGameWorld.Ring(Root, Centre, 85f, 92f, new Color(sand.r, sand.g, sand.b, 0.5f), 0.025f, "SandEdge");
            MiniGameWorld.Water(Root, Centre, WaterRadius, new Color(0.12f, 0.55f, 0.60f), new Color(0.45f, 0.60f, 0.30f, 0.95f));
            // Palms round the water.
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f), r = WaterRadius + Random.Range(2f, 5f);
                Palm(Centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r), Random.Range(5f, 8f));
            }
            // Dunes, rocks and cacti on the sand.
            for (int i = 0; i < 12; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(40f, 80f);
                Vector3 p = Centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                GameObject dune = MiniGameWorld.Shape(PrimitiveType.Sphere, Root, new Vector3(p.x, MiniGameWorld.Ground(p) - 0.4f, p.z), new Vector3(Random.Range(10f, 18f), Random.Range(2f, 3.5f), Random.Range(6f, 10f)), Color.Lerp(sand, Color.white, 0.08f), "Dune");
                dune.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 180f), 0f);
            }
            for (int i = 0; i < 16; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(WaterRadius + 10f, 75f);
                Vector3 p = Centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                float g = MiniGameWorld.Ground(p);
                if (i % 2 == 0)
                    MiniGameWorld.Shape(PrimitiveType.Sphere, Root, new Vector3(p.x, g + 0.4f, p.z), new Vector3(Random.Range(1.5f, 3f), Random.Range(0.8f, 1.6f), Random.Range(1.5f, 3f)), new Color(0.62f, 0.42f, 0.30f), "Rock");
                else
                    Cactus(p);
            }
        }

        private void Palm(Vector3 at, float height)
        {
            float g = MiniGameWorld.Ground(at);
            Color trunk = new Color(0.50f, 0.36f, 0.22f), leaf = new Color(0.22f, 0.55f, 0.20f);
            Vector3 lean = new Vector3(Random.Range(-0.12f, 0.12f), 1f, Random.Range(-0.12f, 0.12f)).normalized;
            int segs = 5;
            Vector3 p = new Vector3(at.x, g, at.z);
            for (int s = 0; s < segs; s++)
            {
                float h = height / segs;
                GameObject seg = MiniGameWorld.Shape(PrimitiveType.Cylinder, Root, p + lean * h * 0.5f, new Vector3(0.45f - s * 0.04f, h * 0.5f, 0.45f - s * 0.04f), trunk, "Palm");
                seg.transform.rotation = Quaternion.FromToRotation(Vector3.up, lean);
                p += lean * h;
                lean = (lean + new Vector3(lean.x, 0f, lean.z) * 0.35f).normalized;
            }
            for (int f = 0; f < 7; f++)
            {
                float yaw = f / 7f * 360f;
                GameObject frond = MiniGameWorld.Shape(PrimitiveType.Cube, Root, p, new Vector3(0.7f, 0.06f, 3.4f), leaf, "Frond");
                frond.transform.rotation = Quaternion.Euler(28f, yaw, 0f);
                frond.transform.position = p + frond.transform.forward * 1.5f;
            }
        }

        private void Cactus(Vector3 at)
        {
            float g = MiniGameWorld.Ground(at);
            Color green = new Color(0.25f, 0.52f, 0.28f);
            float h = Random.Range(1.6f, 3f);
            MiniGameWorld.Shape(PrimitiveType.Capsule, Root, new Vector3(at.x, g + h * 0.5f, at.z), new Vector3(0.6f, h * 0.5f, 0.6f), green, "Cactus");
            for (int s = -1; s <= 1; s += 2)
            {
                if (Random.value < 0.3f) continue;
                float y = g + h * Random.Range(0.45f, 0.65f);
                MiniGameWorld.Shape(PrimitiveType.Capsule, Root, new Vector3(at.x + s * 0.55f, y, at.z), new Vector3(0.35f, 0.3f, 0.35f), green, "Arm").transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                MiniGameWorld.Shape(PrimitiveType.Capsule, Root, new Vector3(at.x + s * 0.8f, y + 0.45f, at.z), new Vector3(0.35f, 0.45f, 0.35f), green, "Arm");
            }
        }

        protected override void MakeModel(Creature c, Transform b)
        {
            CreatureKind k = c.Kind;
            if (k == Scorpion)
            {
                Color shell = new Color(0.36f, 0.22f, 0.12f);
                Part(PrimitiveType.Sphere, b, new Vector3(0f, 0.5f, 0f), new Vector3(1.4f, 0.55f, 2.0f), shell, default, 0.6f);
                Vector3[] tail = { new Vector3(0f, 0.7f, -1.1f), new Vector3(0f, 1.3f, -1.45f), new Vector3(0f, 1.95f, -1.3f), new Vector3(0f, 2.35f, -0.8f) };
                for (int i = 0; i < tail.Length; i++) Part(PrimitiveType.Sphere, b, tail[i], Vector3.one * (0.6f - i * 0.07f), shell, default, 0.6f);
                Part(PrimitiveType.Sphere, b, new Vector3(0f, 2.2f, -0.35f), new Vector3(0.28f, 0.28f, 0.45f), new Color(0.85f, 0.2f, 0.12f));
                for (int s = -1; s <= 1; s += 2)
                {
                    Part(PrimitiveType.Cube, b, new Vector3(s * 0.75f, 0.45f, 0.9f), new Vector3(0.18f, 0.18f, 0.9f), shell, new Vector3(0f, s * 25f, 0f));
                    Part(PrimitiveType.Sphere, b, new Vector3(s * 0.95f, 0.5f, 1.45f), new Vector3(0.6f, 0.35f, 0.7f), shell, default, 0.6f);
                    for (int l = 0; l < 3; l++)
                        Part(PrimitiveType.Cube, b, new Vector3(s * 1.0f, 0.25f, -0.5f + l * 0.45f), new Vector3(0.9f, 0.08f, 0.1f), shell, new Vector3(0f, 0f, s * -30f));
                }
                Eyes(b, new Vector3(0f, 0.8f, 0.9f), 0.35f, 0.16f);
            }
            else if (k == Lizard)
            {
                Part(PrimitiveType.Capsule, b, new Vector3(0f, 0.4f, 0f), new Vector3(0.8f, 1.2f, 0.6f), k.Colour, new Vector3(90f, 0f, 0f));
                Part(PrimitiveType.Sphere, b, new Vector3(0f, 0.5f, 1.4f), new Vector3(0.7f, 0.5f, 0.9f), k.Colour);
                Eyes(b, new Vector3(0f, 0.7f, 1.55f), 0.5f, 0.2f);
                Part(PrimitiveType.Cylinder, b, new Vector3(0f, 0.3f, -1.9f), new Vector3(0.25f, 0.9f, 0.2f), Color.Lerp(k.Colour, Color.black, 0.15f), new Vector3(95f, 0f, 0f));
                for (int s = -1; s <= 1; s += 2)
                    for (int e = -1; e <= 1; e += 2)
                        Part(PrimitiveType.Cube, b, new Vector3(s * 0.55f, 0.18f, e * 0.6f), new Vector3(0.6f, 0.12f, 0.18f), k.Colour, new Vector3(0f, s * e * 20f, 0f));
                for (int i = 0; i < 4; i++)
                    Part(PrimitiveType.Sphere, b, new Vector3(0f, 0.72f, 0.7f - i * 0.45f), new Vector3(0.25f, 0.12f, 0.25f), new Color(0.95f, 0.55f, 0.15f));
            }
            else if (k == Fox)
            {
                Part(PrimitiveType.Capsule, b, new Vector3(0f, 1.0f, 0f), new Vector3(0.9f, 1.0f, 0.9f), k.Colour, new Vector3(90f, 0f, 0f));
                for (int s = -1; s <= 1; s += 2)
                    for (int e = -1; e <= 1; e += 2)
                        Part(PrimitiveType.Cylinder, b, new Vector3(s * 0.3f, 0.35f, e * 0.6f), new Vector3(0.18f, 0.35f, 0.18f), Color.Lerp(k.Colour, Color.black, 0.2f));
                Part(PrimitiveType.Sphere, b, new Vector3(0f, 1.5f, 1.2f), new Vector3(0.85f, 0.75f, 0.8f), k.Colour);
                Part(PrimitiveType.Cube, b, new Vector3(0f, 1.38f, 1.7f), new Vector3(0.3f, 0.25f, 0.5f), new Color(0.98f, 0.92f, 0.85f));
                Part(PrimitiveType.Sphere, b, new Vector3(0f, 1.42f, 1.97f), Vector3.one * 0.14f, Color.black);
                Eyes(b, new Vector3(0f, 1.68f, 1.5f), 0.4f, 0.17f);
                for (int s = -1; s <= 1; s += 2)
                    Part(PrimitiveType.Cube, b, new Vector3(s * 0.3f, 2.15f, 1.1f), new Vector3(0.35f, 0.75f, 0.08f), k.Colour, new Vector3(0f, 0f, s * -18f));
                Part(PrimitiveType.Sphere, b, new Vector3(0f, 1.1f, -1.35f), new Vector3(0.5f, 0.5f, 1.3f), k.Colour, new Vector3(-20f, 0f, 0f));
                Part(PrimitiveType.Sphere, b, new Vector3(0f, 1.35f, -1.95f), new Vector3(0.35f, 0.35f, 0.45f), new Color(0.98f, 0.95f, 0.9f));
            }
            else if (k == Serpent)
            {
                var segs = new List<Transform>();
                Color band = Color.Lerp(k.Colour, new Color(0.35f, 0.20f, 0.10f), 0.6f);
                for (int i = 0; i < 9; i++)
                {
                    float size = i == 0 ? 0.85f : Mathf.Lerp(0.72f, 0.35f, i / 8f);
                    GameObject seg = Part(PrimitiveType.Sphere, b, new Vector3(0f, size * 0.45f, 1.2f - i * 0.55f), new Vector3(size, size * 0.8f, size * 1.1f), i % 2 == 0 ? k.Colour : band, default, 0.5f);
                    segs.Add(seg.transform);
                }
                Eyes(segs[0], new Vector3(0f, 0.3f, 0.3f), 0.55f, 0.25f);
                Part(PrimitiveType.Cube, segs[0], new Vector3(0f, -0.1f, 0.62f), new Vector3(0.08f, 0.04f, 0.5f), new Color(0.85f, 0.15f, 0.2f));
                c.Segments = segs.ToArray();
            }
            else // Phoenix
            {
                Color flame = new Color(1f, 0.38f, 0.10f);
                Part(PrimitiveType.Sphere, b, Vector3.zero, new Vector3(1.1f, 1.0f, 1.9f), k.Colour, default, 0.8f);
                Part(PrimitiveType.Sphere, b, new Vector3(0f, 0.6f, 1.2f), new Vector3(0.75f, 0.75f, 0.8f), k.Colour, default, 0.8f);
                Part(PrimitiveType.Cube, b, new Vector3(0f, 0.5f, 1.7f), new Vector3(0.18f, 0.18f, 0.5f), new Color(0.95f, 0.85f, 0.5f), new Vector3(20f, 0f, 0f));
                Eyes(b, new Vector3(0f, 0.8f, 1.45f), 0.45f, 0.18f);
                Part(PrimitiveType.Cube, b, new Vector3(0f, 1.15f, 1.0f), new Vector3(0.1f, 0.6f, 0.3f), flame, new Vector3(-30f, 0f, 0f));
                for (int i = -1; i <= 1; i++)
                    Part(PrimitiveType.Cube, b, new Vector3(i * 0.35f, -0.1f, -1.9f), new Vector3(0.3f, 0.06f, 2.2f), i == 0 ? flame : new Color(1f, 0.85f, 0.2f), new Vector3(-12f, i * 18f, 0f));
                c.Wings = Wings(b, new Vector3(0f, 0.3f, 0.2f), 3.6f, 1.6f, Color.Lerp(k.Colour, flame, 0.45f));
            }
        }
    }
}
