using System.Collections.Generic;
using GolfSimZA.Physics;
using UnityEngine;

namespace GolfSimZA.MiniGames
{
    /// <summary>
    /// A mini game's own world (instead of the driving range): real ground built from a height
    /// function and a colour function, optional water, and the map's own scenery and trees.
    /// The tee is always at (0, 0, 0) on flat ground.
    /// </summary>
    public abstract class GameMap
    {
        public Transform Root;
        protected System.Random Rnd;
        protected float Seed;
        /// <summary>Water surface height (no water when -infinity).</summary>
        public float WaterLevel = float.NegativeInfinity;
        /// <summary>The detailed part of the ground (the rest out to the horizon is coarser).</summary>
        protected Rect Inner = new Rect(-260f, -80f, 520f, 600f);
        protected float InnerStep = 3f;

        public abstract float H(float x, float z);
        public abstract Color C(float x, float z, float y);
        /// <summary>Where the 3D grass grows.</summary>
        public virtual bool Grass(Vector3 p) => true;
        public virtual void Plant(TreeField field) { field.Clear(); field.Commit(); }
        protected virtual void Decorate() { }

        public bool IsWater(Vector3 p) => !float.IsNegativeInfinity(WaterLevel) && Height(p.x, p.z) < WaterLevel - 0.05f;

        /// <summary>The map's height with the tee area always flat at 0 (the mat never sinks into the ground).</summary>
        public float Height(float x, float z) => Mathf.Lerp(H(x, z), 0f, MapKit.Blend(MapKit.Dist(x, z, Vector3.zero), 8f, 14f));

        public void Build(Transform parent, int seed)
        {
            Rnd = new System.Random(seed);
            Seed = seed * 0.0137f;
            Root = new GameObject(GetType().Name).transform;
            Root.SetParent(parent, false);
            // Detailed ground round the play area, coarse ground to the horizon (sunk under the detailed part).
            MapKit.Terrain(Root, "Map_Ground", Inner.xMin, Inner.xMax, Inner.yMin, Inner.yMax, InnerStep, Height, C);
            Rect hole = new Rect(Inner.x + 10f, Inner.y + 10f, Inner.width - 20f, Inner.height - 20f);
            MapKit.Terrain(Root, "Map_GroundFar", -2600f, 2600f, -1400f, 3000f, 50f,
                (x, z) => Height(x, z) - (hole.Contains(new Vector2(x, z)) ? 6f : 0.5f), C);
            if (!float.IsNegativeInfinity(WaterLevel))
                MapKit.WaterSurface(Root, WaterLevel, Inner.xMin, Inner.xMax, Inner.yMin, Inner.yMax, (x, z) => Height(x, z) < WaterLevel, 3f);
            UnityEngine.Physics.SyncTransforms();
            Decorate();
        }

        // ---- shared shaping helpers

        /// <summary>0 near the play corridor, rising to 1 far from it.</summary>
        protected static float Away(float d, float start, float span) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - start) / span));

        protected float Hills(float x, float z, float amp) =>
            amp * (MapKit.Noise2(x, z, 0.006f, Seed * 31f) - 0.25f) + amp * 0.5f * Mathf.Pow(MapKit.Noise2(x, z, 0.02f, Seed * 7f), 2f);

        protected static Color Grass(float n) => Color.Lerp(new Color(0.30f, 0.50f, 0.18f), new Color(0.40f, 0.58f, 0.22f), n);
        protected static Color Rough(float n) => Color.Lerp(new Color(0.24f, 0.40f, 0.15f), new Color(0.34f, 0.44f, 0.18f), n);
        protected static Color RockC(float n) => Color.Lerp(new Color(0.46f, 0.40f, 0.34f), new Color(0.60f, 0.52f, 0.42f), n);
        protected float N(float x, float z, float s = 0.05f) => Mathf.PerlinNoise(x * s + Seed * 13f, z * s - Seed * 5f);
    }

    // =====================================================================================
    //  CLOSEST TO THE PIN — ISLAND GREEN
    // =====================================================================================

    /// <summary>The pin stands on an island green in a lake; trees and hills round the water. Short = splash.</summary>
    public sealed class IslandGreenMap : GameMap
    {
        private readonly Vector3 pin;
        private readonly float island;
        private readonly Vector2 lakeC, lakeHalf;

        public IslandGreenMap(Vector3 pin, float islandRadius)
        {
            this.pin = pin;
            island = islandRadius;
            float z0 = 28f, z1 = pin.z + 45f;
            lakeC = new Vector2(pin.x * 0.6f, (z0 + z1) * 0.5f);
            lakeHalf = new Vector2(Mathf.Max(55f, Mathf.Abs(pin.x) + 45f), (z1 - z0) * 0.5f);
            WaterLevel = -0.6f;
            Inner = new Rect(-240f, -80f, 480f, pin.z + 320f);
        }

        private float Lake(float x, float z)
        {
            float nx = (x - lakeC.x) / lakeHalf.x, nz = (z - lakeC.y) / lakeHalf.y;
            float wobble = 0.12f * Mathf.Sin(Mathf.Atan2(nz, nx) * 5f + Seed * 40f);
            float d = Mathf.Pow(Mathf.Pow(Mathf.Abs(nx), 2.6f) + Mathf.Pow(Mathf.Abs(nz), 2.6f), 1f / 2.6f) - wobble;
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.85f) / 0.2f));
        }

        public override float H(float x, float z)
        {
            float land = Hills(x, z, 18f) * Away(Mathf.Max(Mathf.Abs(x) - 30f, MapKit.Dist(x, z, new Vector3(lakeC.x, 0f, lakeC.y)) - Mathf.Max(lakeHalf.x, lakeHalf.y)), 10f, 120f);
            float h = Mathf.Lerp(land, -3.2f, Lake(x, z));
            float d = MapKit.Dist(x, z, pin);
            float isl = d <= island ? 0f : Mathf.Lerp(0f, -3.2f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - island) / 7f)));
            return Mathf.Max(h, isl);
        }

        public override Color C(float x, float z, float y)
        {
            float n = N(x, z);
            float d = MapKit.Dist(x, z, pin);
            if (d < island + 0.5f)
            {
                float a = Mathf.Atan2(z - pin.z, x - pin.x);
                // A bunker on the far side of the green.
                if (d > island - 3.5f && Mathf.Sin(a) > 0.35f) return new Color(0.86f, 0.80f, 0.62f);
                return d < island - 3.5f ? new Color(0.36f, 0.62f, 0.26f) : new Color(0.32f, 0.54f, 0.22f);
            }
            if (y < WaterLevel) return Color.Lerp(new Color(0.24f, 0.26f, 0.16f), new Color(0.40f, 0.36f, 0.24f), n);
            if (y < WaterLevel + 0.6f && Lake(x, z) > 0.02f) return new Color(0.72f, 0.66f, 0.48f); // sandy shore
            if (y > 12f) return Color.Lerp(Rough(n), RockC(n), Mathf.Clamp01((y - 12f) / 8f));
            return Mathf.Abs(x) < 14f && z < 28f ? Grass(n) : Rough(n);
        }

        public override bool Grass(Vector3 p) => Lake(p.x, p.z) < 0.02f && MapKit.Dist(p.x, p.z, pin) > island + 2f && !(Mathf.Abs(p.x) < 16f && p.z < 28f);

        public override void Plant(TreeField field)
        {
            field.Clear();
            var r = new System.Random(17);
            MapKit.Plant(field, 520, r, -240f, 240f, -60f, pin.z + 230f,
                p => H(p.x, p.z) > WaterLevel + 1.2f && !(Mathf.Abs(p.x) < 30f && p.z < 26f) && MapKit.Dist(p.x, p.z, pin) > island + 10f,
                TreeField.Kind.Broadleaf, 9f, 17f);
            MapKit.Plant(field, 200, r, -260f, 260f, 0f, pin.z + 280f, p => H(p.x, p.z) > 6f, TreeField.Kind.Pine, 12f, 20f, 0.55f);
            field.Commit();
        }

        protected override void Decorate()
        {
            for (int i = 0; i < 18; i++)
            {
                float a = (float)Rnd.NextDouble() * Mathf.PI * 2f;
                var p = new Vector3(lakeC.x + Mathf.Cos(a) * lakeHalf.x * 0.98f, 0f, lakeC.y + Mathf.Sin(a) * lakeHalf.y * 0.98f);
                if (Mathf.Abs(p.x) < 25f && p.z < 40f) continue;
                MapKit.Rock(Root, p, 1.2f + (float)Rnd.NextDouble() * 1.6f, new Color(0.55f, 0.52f, 0.48f), Rnd);
            }
            // A little wooden bridge-deck at the island's front edge.
            Vector3 front = pin + new Vector3(0f, 0f, -island - 1f);
            GameObject deck = MiniGameWorld.Shape(PrimitiveType.Cube, Root, new Vector3(front.x, -0.15f, front.z - 4f), new Vector3(2.6f, 0.2f, 9f), new Color(0.50f, 0.36f, 0.22f), "Deck", 0.2f);
            deck.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }

    // =====================================================================================
    //  LONG DRIVE — STADIUM
    // =====================================================================================

    /// <summary>A long mown valley with grandstands by the tee, big distance boards and hills both sides.</summary>
    public sealed class StadiumMap : GameMap
    {
        public StadiumMap()
        {
            Inner = new Rect(-220f, -80f, 440f, 620f);
            InnerStep = 3f;
        }

        public override float H(float x, float z)
        {
            float side = Mathf.Abs(x);
            float valley = Away(side, 48f, 90f) * 34f + Hills(x, z, 10f) * Away(side, 60f, 60f);
            float back = Away(z, 430f, 120f) * 30f;
            float behind = Away(-z, 40f, 80f) * 12f;
            return Mathf.Max(valley, Mathf.Max(back, behind)) + Mathf.Min(valley, back) * 0.2f;
        }

        public override Color C(float x, float z, float y)
        {
            float n = N(x, z, 0.08f);
            float side = Mathf.Abs(x);
            if (side < 26f && z > -20f && z < 440f)
                return (Mathf.FloorToInt(z / 12f) & 1) == 0 ? new Color(0.38f, 0.60f, 0.24f) : new Color(0.31f, 0.51f, 0.19f);
            if (y > 20f) return Color.Lerp(Rough(n), RockC(n), Mathf.Clamp01((y - 20f) / 10f));
            return Rough(n);
        }

        public override bool Grass(Vector3 p) => Mathf.Abs(p.x) > 28f && Mathf.Abs(p.x) < 140f;

        public override void Plant(TreeField field)
        {
            field.Clear();
            var r = new System.Random(23);
            MapKit.Plant(field, 700, r, -230f, 230f, -60f, 520f, p => Mathf.Abs(p.x) > 70f || p.z > 450f, TreeField.Kind.Pine, 12f, 22f, 0.55f);
            MapKit.Plant(field, 200, r, -230f, 230f, -60f, 520f, p => Mathf.Abs(p.x) > 62f, TreeField.Kind.Broadleaf, 9f, 15f);
            field.Commit();
        }

        protected override void Decorate()
        {
            MapKit.Grandstand(Root, new Vector3(-50f, 0f, 30f), 50f, 8, 90f, Rnd);
            MapKit.Grandstand(Root, new Vector3(50f, 0f, 30f), 50f, 8, -90f, Rnd);
            float unit = GolfSimZA.Core.Units.Metric ? 1f : 0.9144f;
            for (int d = 100; d <= 400; d += 50)
            {
                float z = d * unit;
                for (int s = -1; s <= 1; s += 2)
                    MapKit.Sign(Root, new Vector3(s * 32f, 0f, z), d.ToString(), new Color(0.72f, 0.10f, 0.10f), Color.white, 3.2f, 6.5f, 0f, 22f);
            }
            // Light towers.
            for (int s = -1; s <= 1; s += 2)
                for (float z = 80f; z <= 380f; z += 100f)
                {
                    var p = new Vector3(s * 44f, 0f, z);
                    float g = MiniGameWorld.Ground(p);
                    MiniGameWorld.Shape(PrimitiveType.Cylinder, Root, new Vector3(p.x, g + 11f, p.z), new Vector3(0.5f, 11f, 0.5f), new Color(0.6f, 0.62f, 0.65f), "LightPole");
                    MiniGameWorld.Shape(PrimitiveType.Cube, Root, new Vector3(p.x, g + 22.5f, p.z), new Vector3(3.5f, 1.8f, 0.6f), new Color(0.9f, 0.92f, 0.95f), "Lights", 0.8f);
                }
        }
    }

    // =====================================================================================
    //  TARGET RINGS — TARGET LAKE
    // =====================================================================================

    /// <summary>The three ringed targets float on rafts in a lake: land on a raft or splash.</summary>
    public sealed class TargetLakeMap : GameMap
    {
        private readonly Vector3[] targets;
        public const float RaftRadius = 14f, RaftTop = 0.15f;

        public TargetLakeMap(Vector3[] targets)
        {
            this.targets = targets;
            WaterLevel = -0.5f;
            Inner = new Rect(-240f, -80f, 480f, 560f);
        }

        private float Lake(float x, float z)
        {
            float nx = x / 85f, nz = (z - 145f) / 108f;
            float d = Mathf.Sqrt(nx * nx + nz * nz) - 0.08f * Mathf.Sin(Mathf.Atan2(nz, nx) * 6f + Seed * 20f);
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.9f) / 0.14f));
        }

        public override float H(float x, float z)
        {
            float land = Hills(x, z, 20f) * Away(MapKit.Dist(x, z, new Vector3(0f, 0f, 145f)) - 110f, 0f, 120f);
            return Mathf.Lerp(land, -3f, Lake(x, z));
        }

        public override Color C(float x, float z, float y)
        {
            float n = N(x, z);
            if (y < WaterLevel) return Color.Lerp(new Color(0.22f, 0.26f, 0.20f), new Color(0.36f, 0.34f, 0.24f), n);
            if (y < WaterLevel + 0.7f && Lake(x, z) > 0.02f) return new Color(0.76f, 0.70f, 0.52f);
            if (y > 14f) return Color.Lerp(Rough(n), RockC(n), Mathf.Clamp01((y - 14f) / 8f));
            return Mathf.Abs(x) < 14f && z < 40f ? Grass(n) : Rough(n);
        }

        public override bool Grass(Vector3 p) => Lake(p.x, p.z) < 0.02f && !(Mathf.Abs(p.x) < 16f && p.z < 40f);

        public override void Plant(TreeField field)
        {
            field.Clear();
            var r = new System.Random(29);
            MapKit.Plant(field, 600, r, -240f, 240f, -60f, 470f, p => H(p.x, p.z) > WaterLevel + 1.5f && !(Mathf.Abs(p.x) < 30f && p.z < 36f), TreeField.Kind.Broadleaf, 9f, 16f);
            MapKit.Plant(field, 220, r, -240f, 240f, -60f, 470f, p => H(p.x, p.z) > 8f, TreeField.Kind.Pine, 12f, 20f, 0.55f);
            field.Commit();
        }

        protected override void Decorate()
        {
            foreach (Vector3 t in targets)
            {
                MapKit.Platform(Root, "Map_Raft", t, RaftRadius, RaftTop, new Color(0.55f, 0.40f, 0.26f));
                // Floats round the raft.
                for (int i = 0; i < 10; i++)
                {
                    float a = i / 10f * Mathf.PI * 2f;
                    MiniGameWorld.Shape(PrimitiveType.Sphere, Root, new Vector3(t.x + Mathf.Cos(a) * (RaftRadius + 0.4f), WaterLevel + 0.1f, t.z + Mathf.Sin(a) * (RaftRadius + 0.4f)), new Vector3(1.1f, 0.7f, 1.1f), i % 2 == 0 ? new Color(0.95f, 0.30f, 0.20f) : Color.white, "Float", 0.6f);
                }
            }
            // Jetty from the tee out over the water.
            MiniGameWorld.Shape(PrimitiveType.Cube, Root, new Vector3(-6f, 0.1f, 42f), new Vector3(3f, 0.25f, 26f), new Color(0.52f, 0.38f, 0.24f), "Jetty", 0.2f);
        }
    }

    // =====================================================================================
    //  CAPTURE THE FLAGS — HIGHLAND GREENS
    // =====================================================================================

    /// <summary>Rolling highland hills; every flag stands on its own flat green; heather, gorse and pines.</summary>
    public sealed class HighlandMap : GameMap
    {
        private readonly List<Vector3> flats;
        private readonly List<float> radii;
        private readonly List<float> levels = new List<float>();

        public HighlandMap(List<Vector3> flagSpots, List<float> flagRadii)
        {
            flats = flagSpots;
            radii = flagRadii;
            Inner = new Rect(-240f, -80f, 480f, 520f);
            foreach (Vector3 f in flats) levels.Add(Base(f.x, f.z));
        }

        private float Base(float x, float z)
        {
            float rolling = 4.5f * (MapKit.Noise2(x, z, 0.011f, 3.3f) - 0.45f) + 2f * Mathf.Sin(x * 0.03f) * Mathf.Sin(z * 0.021f);
            float hills = Away(Mathf.Abs(x), 60f, 90f) * 26f * MapKit.Noise2(x, z, 0.008f, 9.1f);
            float tee = MapKit.Blend(MapKit.Dist(x, z, Vector3.zero), 18f, 30f);
            return Mathf.Lerp(rolling + hills, 0f, tee);
        }

        public override float H(float x, float z)
        {
            float h = Base(x, z);
            for (int i = 0; i < flats.Count; i++)
                h = Mathf.Lerp(h, levels[i], MapKit.Blend(MapKit.Dist(x, z, flats[i]), radii[i] + 2f, 9f));
            return h;
        }

        public override Color C(float x, float z, float y)
        {
            float n = N(x, z, 0.06f);
            for (int i = 0; i < flats.Count; i++)
                if (MapKit.Dist(x, z, flats[i]) < radii[i] + 1f) return Color.Lerp(new Color(0.34f, 0.60f, 0.25f), new Color(0.40f, 0.64f, 0.28f), n);
            float heather = Mathf.PerlinNoise(x * 0.02f + 5f, z * 0.02f);
            Color c = Rough(n);
            if (heather > 0.62f && Mathf.Abs(x) > 30f) c = Color.Lerp(c, new Color(0.46f, 0.30f, 0.42f), Mathf.Clamp01((heather - 0.62f) * 5f));
            if (y > 14f) c = Color.Lerp(c, RockC(n), Mathf.Clamp01((y - 14f) / 10f));
            return Mathf.Abs(x) < 12f && z < 30f ? Grass(n) : c;
        }

        public override bool Grass(Vector3 p)
        {
            for (int i = 0; i < flats.Count; i++) if (MapKit.Dist(p.x, p.z, flats[i]) < radii[i] + 2f) return false;
            return !(Mathf.Abs(p.x) < 14f && p.z < 30f);
        }

        public override void Plant(TreeField field)
        {
            field.Clear();
            var r = new System.Random(31);
            System.Func<Vector3, bool> clear = p =>
            {
                for (int i = 0; i < flats.Count; i++) if (MapKit.Dist(p.x, p.z, flats[i]) < radii[i] + 10f) return false;
                return !(Mathf.Abs(p.x) < 30f && p.z < 40f);
            };
            MapKit.Plant(field, 380, r, -240f, 240f, -60f, 420f, p => Mathf.Abs(p.x) > 55f && clear(p), TreeField.Kind.Pine, 10f, 20f, 0.55f);
            MapKit.Plant(field, 260, r, -200f, 200f, -40f, 380f, clear, TreeField.Kind.Bush, 1.5f, 3f, 1.5f);
            field.Commit();
        }

        protected override void Decorate()
        {
            for (int i = 0; i < 30; i++)
            {
                var p = new Vector3(Mathf.Lerp(-200f, 200f, (float)Rnd.NextDouble()), 0f, Mathf.Lerp(20f, 380f, (float)Rnd.NextDouble()));
                if (Mathf.Abs(p.x) < 40f) continue;
                MapKit.Rock(Root, p, 1f + (float)Rnd.NextDouble() * 2.5f, new Color(0.52f, 0.50f, 0.46f), Rnd);
            }
        }
    }

    // =====================================================================================
    //  JUNKYARD BLAST — SCRAPYARD
    // =====================================================================================

    /// <summary>A fenced scrapyard: dirt and oil stains, container stacks, a crane, spoil heaps outside.</summary>
    public sealed class ScrapyardMap : GameMap
    {
        private const float HalfW = 78f, Back = 275f;

        public ScrapyardMap() { Inner = new Rect(-240f, -80f, 480f, 520f); }

        public override float H(float x, float z)
        {
            float inside = (Mathf.Abs(x) < HalfW && z > -15f && z < Back) ? 1f : 0f;
            float bumps = 0.25f * (MapKit.Noise2(x, z, 0.08f, 2f) - 0.5f);
            float outside = Mathf.Max(Away(Mathf.Abs(x), HalfW + 10f, 60f), Away(z, Back + 10f, 60f)) * (14f + 10f * MapKit.Noise2(x, z, 0.015f, 6f));
            return inside > 0f ? bumps : bumps + outside;
        }

        public override Color C(float x, float z, float y)
        {
            float n = N(x, z, 0.07f);
            Color dirt = Color.Lerp(new Color(0.42f, 0.34f, 0.24f), new Color(0.52f, 0.44f, 0.32f), n);
            float oil = Mathf.PerlinNoise(x * 0.05f + 11f, z * 0.05f);
            if (oil > 0.7f) dirt = Color.Lerp(dirt, new Color(0.14f, 0.13f, 0.12f), (oil - 0.7f) * 3f);
            float gravel = Mathf.PerlinNoise(x * 0.3f, z * 0.3f);
            dirt *= 0.92f + gravel * 0.16f;
            if (y > 3f) dirt = Color.Lerp(dirt, Rough(n) * 0.9f, Mathf.Clamp01((y - 3f) / 6f));
            return dirt;
        }

        public override bool Grass(Vector3 p) => Mathf.Abs(p.x) > HalfW + 12f || p.z > Back + 12f;

        public override void Plant(TreeField field)
        {
            field.Clear();
            var r = new System.Random(37);
            MapKit.Plant(field, 260, r, -240f, 240f, -60f, 430f, p => Mathf.Abs(p.x) > HalfW + 20f || p.z > Back + 25f, TreeField.Kind.Broadleaf, 8f, 14f);
            field.Commit();
        }

        protected override void Decorate()
        {
            // Corrugated fence round the yard.
            Color[] tin = { new Color(0.55f, 0.56f, 0.58f), new Color(0.50f, 0.34f, 0.24f), new Color(0.46f, 0.48f, 0.50f) };
            int k = 0;
            for (float z = -10f; z < Back; z += 6f)
                for (int s = -1; s <= 1; s += 2)
                    MiniGameWorld.Shape(PrimitiveType.Cube, Root, new Vector3(s * HalfW, MiniGameWorld.Ground(new Vector3(s * HalfW, 0f, z)) + 1.5f, z + 3f), new Vector3(0.15f, 3f, 6f), tin[k++ % 3], "Fence", 0.5f);
            for (float x = -HalfW; x < HalfW; x += 6f)
                MiniGameWorld.Shape(PrimitiveType.Cube, Root, new Vector3(x + 3f, MiniGameWorld.Ground(new Vector3(x, 0f, Back)) + 1.5f, Back), new Vector3(6f, 3f, 0.15f), tin[k++ % 3], "Fence", 0.5f);
            // Container stacks just outside the fence.
            Color[] box = { new Color(0.75f, 0.20f, 0.15f), new Color(0.15f, 0.35f, 0.65f), new Color(0.85f, 0.55f, 0.10f), new Color(0.20f, 0.50f, 0.30f), new Color(0.55f, 0.55f, 0.58f) };
            for (int i = 0; i < 16; i++)
            {
                float s = i % 2 == 0 ? -1f : 1f;
                var p = new Vector3(s * (HalfW + 8f + (float)Rnd.NextDouble() * 12f), 0f, 20f + i * 15f);
                int stack = 1 + Rnd.Next(3);
                for (int h = 0; h < stack; h++)
                {
                    GameObject c = MiniGameWorld.Shape(PrimitiveType.Cube, Root, new Vector3(p.x, MiniGameWorld.Ground(p) + 1.3f + h * 2.6f, p.z), new Vector3(2.5f, 2.6f, 12f), box[Rnd.Next(box.Length)], "Container", 0.3f);
                    c.transform.rotation = Quaternion.Euler(0f, (float)Rnd.NextDouble() * 8f - 4f, 0f);
                }
            }
            // Crane: lattice tower and jib over the yard.
            var crane = new Vector3(-HalfW + 12f, 0f, 230f);
            float g = MiniGameWorld.Ground(crane);
            Color yellow = new Color(0.95f, 0.72f, 0.10f);
            MiniGameWorld.Shape(PrimitiveType.Cube, Root, new Vector3(crane.x, g + 18f, crane.z), new Vector3(2.2f, 36f, 2.2f), yellow, "CraneTower", 0.4f);
            MiniGameWorld.Shape(PrimitiveType.Cube, Root, new Vector3(crane.x + 18f, g + 36f, crane.z), new Vector3(40f, 1.5f, 1.5f), yellow, "CraneJib", 0.4f);
            MiniGameWorld.Shape(PrimitiveType.Cube, Root, new Vector3(crane.x - 6f, g + 35f, crane.z), new Vector3(5f, 3f, 3f), new Color(0.3f, 0.3f, 0.32f), "Counterweight");
            MiniGameWorld.Shape(PrimitiveType.Cylinder, Root, new Vector3(crane.x + 30f, g + 26f, crane.z), new Vector3(0.08f, 10f, 0.08f), Color.black, "Cable");
            MiniGameWorld.Shape(PrimitiveType.Cylinder, Root, new Vector3(crane.x + 30f, g + 15.5f, crane.z), new Vector3(3f, 0.5f, 3f), new Color(0.25f, 0.25f, 0.28f), "Magnet", 0.6f);
            // Tyre stacks.
            for (int i = 0; i < 20; i++)
            {
                var p = new Vector3(Mathf.Lerp(-HalfW + 5f, HalfW - 5f, (float)Rnd.NextDouble()), 0f, Mathf.Lerp(10f, Back - 10f, (float)Rnd.NextDouble()));
                if (Mathf.Abs(p.x) < 20f && p.z < 50f) continue;
                int n = 2 + Rnd.Next(4);
                for (int h = 0; h < n; h++)
                    MiniGameWorld.Shape(PrimitiveType.Cylinder, Root, new Vector3(p.x, MiniGameWorld.Ground(p) + 0.2f + h * 0.38f, p.z), new Vector3(1.1f, 0.19f, 1.1f), new Color(0.07f, 0.07f, 0.07f), "Tyres");
            }
        }
    }

    // =====================================================================================
    //  CREATURE POND — ENCHANTED FOREST POND
    // =====================================================================================

    /// <summary>A meadow with a real pond in a ring of forested hills.</summary>
    public sealed class ForestPondMap : GameMap
    {
        private readonly Vector3 centre;
        private readonly float radius;

        public ForestPondMap(Vector3 centre, float pondRadius)
        {
            this.centre = centre;
            radius = pondRadius;
            WaterLevel = -0.5f;
            Inner = new Rect(-240f, -80f, 480f, 480f);
        }

        private float Meadow(float x, float z)
        {
            float d = MapKit.Dist(x, z, centre);
            float corridor = Mathf.Abs(x) < 45f && z < centre.z ? 0f : 1f;
            return Mathf.Min(Away(d, 92f, 70f), corridor > 0f ? 1f : Away(Mathf.Abs(x), 45f, 40f));
        }

        public override float H(float x, float z)
        {
            float d = MapKit.Dist(x, z, centre);
            float a = Mathf.Atan2(z - centre.z, x - centre.x);
            float edge = radius * (1f + 0.1f * Mathf.Sin(a * 4f + Seed * 10f));
            float pond = Mathf.Lerp(-2.6f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - edge + 2f) / 6f)));
            float hills = Meadow(x, z) * (8f + Hills(x, z, 22f));
            float ripple = 0.25f * (MapKit.Noise2(x, z, 0.05f, 1f) - 0.5f);
            return d < edge + 6f ? pond : hills + ripple;
        }

        public override Color C(float x, float z, float y)
        {
            float n = N(x, z, 0.07f);
            if (y < WaterLevel) return Color.Lerp(new Color(0.18f, 0.24f, 0.18f), new Color(0.30f, 0.30f, 0.20f), n);
            if (y < WaterLevel + 0.5f && MapKit.Dist(x, z, centre) < radius * 1.2f + 8f) return new Color(0.58f, 0.52f, 0.36f);
            float flowers = Mathf.PerlinNoise(x * 0.4f, z * 0.4f);
            Color c = Color.Lerp(new Color(0.30f, 0.54f, 0.22f), new Color(0.40f, 0.62f, 0.26f), n);
            if (flowers > 0.78f) c = Color.Lerp(c, new Color(0.85f, 0.75f, 0.35f), 0.4f);
            if (y > 10f) c = Color.Lerp(c, new Color(0.20f, 0.34f, 0.16f), Mathf.Clamp01((y - 10f) / 10f));
            return c;
        }

        public override bool Grass(Vector3 p) => MapKit.Dist(p.x, p.z, centre) > radius * 1.2f + 8f && !(Mathf.Abs(p.x) < 14f && p.z < 40f);

        public override void Plant(TreeField field)
        {
            field.Clear();
            var r = new System.Random(41);
            System.Func<Vector3, bool> forest = p => Meadow(p.x, p.z) > 0.35f;
            MapKit.Plant(field, 900, r, -240f, 240f, -60f, 400f, forest, TreeField.Kind.Broadleaf, 10f, 18f);
            MapKit.Plant(field, 500, r, -240f, 240f, -60f, 400f, forest, TreeField.Kind.Pine, 12f, 22f, 0.55f);
            field.Commit();
        }
    }

    // =====================================================================================
    //  DESERT OASIS — DUNE SEA
    // =====================================================================================

    /// <summary>Real sand dunes all round, an oasis pool with palms in the middle.</summary>
    public sealed class DuneSeaMap : GameMap
    {
        private readonly Vector3 centre;
        private readonly float pool;

        public DuneSeaMap(Vector3 centre, float poolRadius)
        {
            this.centre = centre;
            pool = poolRadius;
            WaterLevel = -0.5f;
            Inner = new Rect(-240f, -80f, 480f, 480f);
        }

        private float Open(float x, float z)
        {
            float d = MapKit.Dist(x, z, centre);
            float c = Mathf.Abs(x) < 40f && z < centre.z ? 0f : 1f;
            return Mathf.Min(Away(d, 85f, 60f), c > 0f ? 1f : Away(Mathf.Abs(x), 40f, 40f));
        }

        public override float H(float x, float z)
        {
            float d = MapKit.Dist(x, z, centre);
            if (d < pool + 6f) return Mathf.Lerp(-2.4f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - pool + 2f) / 6f)));
            // Wind-built ridges: sharp crests, gentle backs.
            float rx = x * 0.9f + z * 0.44f;
            float ridge = Mathf.Pow(Mathf.Abs(Mathf.Sin(rx * 0.022f + MapKit.Noise2(x, z, 0.01f, 4f) * 3f)), 0.6f);
            float dunes = Open(x, z) * (5f + 9f * MapKit.Noise2(x, z, 0.006f, 8f)) * ridge;
            float ripple = 0.18f * Mathf.Sin(rx * 0.9f) * (1f - Open(x, z) * 0.5f);
            return dunes + ripple;
        }

        public override Color C(float x, float z, float y)
        {
            float n = N(x, z, 0.04f);
            float d = MapKit.Dist(x, z, centre);
            if (y < WaterLevel) return new Color(0.42f, 0.40f, 0.28f);
            if (d < pool + 7f) return Color.Lerp(new Color(0.40f, 0.56f, 0.26f), new Color(0.80f, 0.68f, 0.46f), Mathf.Clamp01((d - pool - 2f) / 5f));
            Color sand = Color.Lerp(new Color(0.84f, 0.70f, 0.48f), new Color(0.90f, 0.78f, 0.56f), n);
            return Color.Lerp(sand, new Color(0.78f, 0.60f, 0.40f), Mathf.Clamp01(y / 14f) * 0.5f);
        }

        public override bool Grass(Vector3 p) => MapKit.Dist(p.x, p.z, centre) < pool + 7f && H(p.x, p.z) > WaterLevel + 0.3f;

        public override void Plant(TreeField field)
        {
            field.Clear();
            var r = new System.Random(43);
            MapKit.Plant(field, 70, r, centre.x - 40f, centre.x + 40f, centre.z - 40f, centre.z + 40f,
                p => { float d = MapKit.Dist(p.x, p.z, centre); return d > pool + 1.5f && d < pool + 12f; }, TreeField.Kind.Palm, 8f, 14f, 0.9f);
            MapKit.Plant(field, 120, r, -240f, 240f, -40f, 400f, p => Open(p.x, p.z) > 0.6f, TreeField.Kind.Bush, 1.2f, 2.4f, 1.4f);
            field.Commit();
        }
    }

    // =====================================================================================
    //  301 / HORSE — TARGET ARENA
    // =====================================================================================

    /// <summary>A netted target range between rocky hills: tall poles and nets, big red distance boards, arches and a banner.</summary>
    public sealed class TargetArenaMap : GameMap
    {
        private readonly string banner;

        public TargetArenaMap(string banner)
        {
            this.banner = banner;
            Inner = new Rect(-240f, -80f, 480f, 520f);
        }

        public override float H(float x, float z)
        {
            float side = Mathf.Abs(x);
            float hills = Away(side, 60f, 60f) * (22f + 18f * MapKit.Noise2(x, z, 0.012f, 5f));
            float back = Away(z, 300f, 80f) * (18f + 10f * MapKit.Noise2(x, z, 0.02f, 2f));
            float bumps = 0.2f * (MapKit.Noise2(x, z, 0.06f, 7f) - 0.5f);
            return Mathf.Max(hills, back) + bumps;
        }

        public override Color C(float x, float z, float y)
        {
            float n = N(x, z, 0.06f);
            float side = Mathf.Abs(x);
            if (y > 10f) return Color.Lerp(Rough(n), Color.Lerp(new Color(0.52f, 0.30f, 0.20f), new Color(0.66f, 0.44f, 0.30f), n), Mathf.Clamp01((y - 10f) / 6f));
            if (side < 34f && z > -15f && z < 300f) return Color.Lerp(new Color(0.34f, 0.56f, 0.22f), new Color(0.40f, 0.62f, 0.26f), n);
            return Rough(n);
        }

        public override bool Grass(Vector3 p) => Mathf.Abs(p.x) > 36f;

        public override void Plant(TreeField field)
        {
            field.Clear();
            var r = new System.Random(47);
            MapKit.Plant(field, 520, r, -240f, 240f, -60f, 440f, p => Mathf.Abs(p.x) > 50f || p.z > 310f, TreeField.Kind.Broadleaf, 7f, 14f);
            MapKit.Plant(field, 160, r, -240f, 240f, -60f, 440f, p => Mathf.Abs(p.x) > 70f, TreeField.Kind.Pine, 10f, 18f, 0.55f);
            field.Commit();
        }

        protected override void Decorate()
        {
            MapKit.NetLine(Root, new Vector3(-42f, 0f, -5f), new Vector3(-42f, 0f, 300f), 22f, 25f);
            MapKit.NetLine(Root, new Vector3(42f, 0f, -5f), new Vector3(42f, 0f, 300f), 22f, 25f);
            float unit = GolfSimZA.Core.Units.Metric ? 1f : 0.9144f;
            for (int d = 50; d <= 250; d += 50)
            {
                float z = d * unit;
                for (int s = -1; s <= 1; s += 2)
                    MapKit.Sign(Root, new Vector3(s * 36f, 0f, z), d.ToString(), new Color(0.72f, 0.10f, 0.10f), Color.white, 2.6f, 5.2f, 0f, 24f);
            }
            MapKit.Sign(Root, new Vector3(0f, 0f, 262f * unit), "250", new Color(0.72f, 0.10f, 0.10f), Color.white, 2.2f, 4.5f, 0f, 24f);
            // Nested arches either side (yellow / gold).
            for (int s = -1; s <= 1; s += 2)
                for (int i = 0; i < 4; i++)
                    MapKit.Arch(Root, new Vector3(s * 30f, 0f, 120f), 14f - i * 3f, 0.9f, s < 0 ? new Color(0.78f, 0.62f, 0.12f) : new Color(1f, 0.92f, 0.20f), 90f);
            // Banner over the far end.
            MapKit.Sign(Root, new Vector3(0f, 0f, 310f), banner, new Color(0.10f, 0.08f, 0.06f), new Color(1f, 0.62f, 0.12f), 9f, 34f, 22f, 0f);
            for (int s = -1; s <= 1; s += 2)
                MiniGameWorld.Shape(PrimitiveType.Cylinder, Root, new Vector3(s * 16f, MiniGameWorld.Ground(new Vector3(s * 16f, 0f, 310f)) + 13f, 310f), new Vector3(0.6f, 13f, 0.6f), new Color(0.3f, 0.3f, 0.32f), "BannerPost");
        }
    }
}
