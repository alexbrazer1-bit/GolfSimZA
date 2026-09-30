using System.Collections.Generic;
using UnityEngine;

namespace GolfSimZA.MiniGames
{
    /// <summary>
    /// Builds the mini games' things on the range out of simple shapes: ringed targets, flags,
    /// barrels, wrecked cars, water and creatures, plus the explosion / capture effects. Nothing
    /// has a collider (the ball flies through; hits are measured from where it lands).
    /// </summary>
    public static class MiniGameWorld
    {
        private static readonly Dictionary<Color, Material> lit = new Dictionary<Color, Material>();
        private static Material vertexUnlit;

        /// <summary>A lit, slightly matt material of this colour (shared).</summary>
        public static Material Lit(Color c, float smooth = 0.2f)
        {
            if (lit.TryGetValue(c, out Material m) && m != null) return m;
            m = new Material(Shader.Find("Standard")) { color = c };
            m.SetFloat("_Glossiness", smooth);
            lit[c] = m;
            return m;
        }

        /// <summary>Unlit, vertex-coloured, see-through material for rings and effects.</summary>
        public static Material VertexUnlit
        {
            get
            {
                if (vertexUnlit != null) return vertexUnlit;
                Shader s = Shader.Find("Sprites/Default");
                if (s == null) s = Shader.Find("Unlit/Color");
                vertexUnlit = new Material(s) { renderQueue = 3000 };
                return vertexUnlit;
            }
        }

        public static float Ground(Vector3 p) => GolfSimZA.Physics.GroundProbe.HeightAt(p);

        /// <summary>A primitive with no collider and no shadows.</summary>
        public static GameObject Shape(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Color color, string name = null, float smooth = 0.2f)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = Lit(color, smooth);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        // ------------------------------------------------------------ Flat rings and discs

        /// <summary>Flat ring (inner..outer radius) lying on the ground, vertex coloured.</summary>
        public static GameObject Ring(Transform parent, Vector3 centre, float inner, float outer, Color color, float lift = 0.06f, string name = "Ring")
        {
            const int seg = 72;
            var v = new Vector3[(seg + 1) * 2];
            var col = new Color[v.Length];
            var tri = new int[seg * 6];
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v[i * 2] = d * inner;
                v[i * 2 + 1] = d * outer;
                col[i * 2] = col[i * 2 + 1] = color;
                if (i < seg)
                {
                    int k = i * 6, b = i * 2;
                    tri[k] = b; tri[k + 1] = b + 2; tri[k + 2] = b + 1;
                    tri[k + 3] = b + 2; tri[k + 4] = b + 3; tri[k + 5] = b + 1;
                }
            }
            var mesh = new Mesh { name = name, vertices = v, colors = col, triangles = tri };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(centre.x, Ground(centre) + lift, centre.z);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = VertexUnlit;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        public static GameObject Disc(Transform parent, Vector3 centre, float radius, Color color, float lift = 0.05f, string name = "Disc")
            => Ring(parent, centre, 0f, radius, color, lift, name);

        /// <summary>Straight painted line on the ground from a to b.</summary>
        public static GameObject GroundLine(Transform parent, Vector3 a, Vector3 b, float width, Color color, string name = "Line")
        {
            Vector3 d = b - a;
            d.y = 0f;
            Vector3 side = new Vector3(d.z, 0f, -d.x).normalized * width * 0.5f;
            var v = new[] { a - side, a + side, b - side, b + side };
            for (int i = 0; i < 4; i++) v[i].y = Ground(v[i]) + 0.07f;
            var mesh = new Mesh { name = name, vertices = v, colors = new[] { color, color, color, color }, triangles = new[] { 0, 2, 1, 1, 2, 3 } };
            mesh.RecalculateNormals();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = VertexUnlit;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // ------------------------------------------------------------ Objects

        /// <summary>Tall flag: pole and cloth (colour changes when claimed).</summary>
        public static GameObject Flag(Transform parent, Vector3 at, Color cloth, out Renderer clothRenderer)
        {
            var root = new GameObject("MiniFlag");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(at.x, Ground(at), at.z);
            Shape(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 2.5f, 0f), new Vector3(0.14f, 2.5f, 0.14f), new Color(0.92f, 0.92f, 0.9f));
            GameObject c = Shape(PrimitiveType.Cube, root.transform, new Vector3(0.95f, 4.3f, 0f), new Vector3(1.8f, 1.1f, 0.06f), cloth, "Cloth");
            clothRenderer = c.GetComponent<Renderer>();
            return root;
        }

        public static void Recolour(Renderer r, Color c)
        {
            if (r != null) r.sharedMaterial = Lit(c);
        }

        /// <summary>Blast barrel: coloured drum with dark bands.</summary>
        public static GameObject Barrel(Transform parent, Vector3 at, Color color)
        {
            var root = new GameObject("Barrel");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(at.x, Ground(at), at.z);
            Shape(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 1.3f, 0f), new Vector3(2.0f, 1.3f, 2.0f), color, "Drum", 0.45f);
            Color band = Color.Lerp(color, Color.black, 0.55f);
            Shape(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.7f, 0f), new Vector3(2.06f, 0.08f, 2.06f), band);
            Shape(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 1.9f, 0f), new Vector3(2.06f, 0.08f, 2.06f), band);
            Shape(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 2.62f, 0f), new Vector3(1.6f, 0.03f, 1.6f), Color.Lerp(color, Color.white, 0.4f));
            return root;
        }

        /// <summary>Rusty wrecked car (body and cabin), sometimes stacked on another.</summary>
        public static GameObject Car(Transform parent, Vector3 at, float yaw, Color paint, bool stacked)
        {
            var root = new GameObject("Wreck");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(at.x, Ground(at), at.z);
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Color rust = new Color(0.45f, 0.26f, 0.15f);
            AddCar(root.transform, 0f, paint, rust);
            if (stacked) AddCar(root.transform, 1.55f, Color.Lerp(paint, rust, 0.5f), rust, 17f);
            return root;
        }

        private static void AddCar(Transform root, float y, Color paint, Color rust, float twist = 0f)
        {
            var car = new GameObject("Car");
            car.transform.SetParent(root, false);
            car.transform.localPosition = new Vector3(0f, y, 0f);
            car.transform.localRotation = Quaternion.Euler(0f, twist, 0f);
            Shape(PrimitiveType.Cube, car.transform, new Vector3(0f, 0.75f, 0f), new Vector3(2.0f, 0.9f, 4.4f), paint, "Body", 0.35f);
            Shape(PrimitiveType.Cube, car.transform, new Vector3(0f, 1.45f, -0.3f), new Vector3(1.7f, 0.6f, 2.2f), Color.Lerp(paint, rust, 0.4f), "Cabin");
            Shape(PrimitiveType.Cube, car.transform, new Vector3(0f, 1.46f, -0.3f), new Vector3(1.72f, 0.4f, 2.0f), new Color(0.1f, 0.13f, 0.15f), "Glass", 0.8f);
            for (int i = 0; i < 4; i++)
            {
                float x = i < 2 ? -0.95f : 0.95f, z = i % 2 == 0 ? -1.4f : 1.4f;
                GameObject w = Shape(PrimitiveType.Cylinder, car.transform, new Vector3(x, 0.38f, z), new Vector3(0.75f, 0.15f, 0.75f), new Color(0.08f, 0.08f, 0.08f));
                w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        /// <summary>Water surface (pond / oasis) with a sandy bank.</summary>
        public static void Water(Transform parent, Vector3 centre, float radius, Color water, Color bank)
        {
            Disc(parent, centre, radius + 4f, bank, 0.03f, "Bank");
            GameObject w = Disc(parent, centre, radius, water, 0.06f, "Water");
            w.GetComponent<MeshRenderer>().sharedMaterial = Lit(water, 0.92f);
        }

        // ------------------------------------------------------------ Effects

        /// <summary>Fireball + flying debris; removes itself.</summary>
        public static void Explosion(Transform parent, Vector3 at, Color color)
        {
            var go = new GameObject("Blast");
            go.transform.SetParent(parent, false);
            go.transform.position = at + Vector3.up * 1.2f;
            go.AddComponent<BlastEffect>().Begin(color);
        }

        /// <summary>Sparkle ring + rising glow when a creature is caught; removes itself.</summary>
        public static void Sparkle(Transform parent, Vector3 at, Color color)
        {
            var go = new GameObject("Sparkle");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            go.AddComponent<SparkleEffect>().Begin(color);
        }
    }

    /// <summary>Fireball growing and fading, debris thrown up and falling.</summary>
    public sealed class BlastEffect : MonoBehaviour
    {
        private Transform ball;
        private Material ballMat;
        private readonly List<Transform> bits = new List<Transform>();
        private readonly List<Vector3> velocity = new List<Vector3>();
        private float t;

        public void Begin(Color color)
        {
            ball = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            Destroy(ball.GetComponent<Collider>());
            ball.SetParent(transform, false);
            ballMat = new Material(MiniGameWorld.VertexUnlit) { color = new Color(1f, 0.62f, 0.15f, 0.9f) };
            Renderer r = ball.GetComponent<Renderer>();
            r.sharedMaterial = ballMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < 9; i++)
            {
                GameObject b = MiniGameWorld.Shape(PrimitiveType.Cube, transform, Vector3.zero, Vector3.one * Random.Range(0.3f, 0.7f), i % 3 == 0 ? color : new Color(0.2f, 0.18f, 0.16f));
                bits.Add(b.transform);
                velocity.Add(new Vector3(Random.Range(-9f, 9f), Random.Range(9f, 18f), Random.Range(-9f, 9f)));
            }
        }

        private void Update()
        {
            t += Time.deltaTime;
            float grow = Mathf.Clamp01(t / 0.45f);
            ball.localScale = Vector3.one * Mathf.Lerp(1f, 11f, grow);
            ballMat.color = new Color(1f, Mathf.Lerp(0.8f, 0.3f, grow), 0.12f, Mathf.Lerp(0.95f, 0f, Mathf.Clamp01((t - 0.2f) / 0.6f)));
            for (int i = 0; i < bits.Count; i++)
            {
                velocity[i] += UnityEngine.Physics.gravity * Time.deltaTime;
                bits[i].position += velocity[i] * Time.deltaTime;
                bits[i].Rotate(360f * Time.deltaTime, 200f * Time.deltaTime, 0f);
            }
            if (t > 2.2f) Destroy(gameObject);
        }
    }

    /// <summary>Expanding bright ring and a rising glow.</summary>
    public sealed class SparkleEffect : MonoBehaviour
    {
        private Transform ring, glow;
        private Material glowMat;
        private float t;
        private Color color;

        public void Begin(Color c)
        {
            color = c;
            ring = MiniGameWorld.Ring(transform, transform.position, 0.8f, 1.2f, new Color(c.r, c.g, c.b, 0.9f), 0.2f, "SparkleRing").transform;
            glow = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            Destroy(glow.GetComponent<Collider>());
            glow.SetParent(transform, false);
            glowMat = new Material(MiniGameWorld.VertexUnlit) { color = new Color(1f, 1f, 0.8f, 0.8f) };
            glow.GetComponent<Renderer>().sharedMaterial = glowMat;
        }

        private void Update()
        {
            t += Time.deltaTime;
            ring.localScale = Vector3.one * (1f + t * 9f);
            glow.position = transform.position + Vector3.up * (1f + t * 6f);
            glow.localScale = Vector3.one * Mathf.Lerp(2.5f, 0.3f, t / 1.4f);
            glowMat.color = new Color(1f, 1f, Mathf.Lerp(0.8f, 0.4f, t), Mathf.Lerp(0.8f, 0f, t / 1.4f));
            if (t > 1.4f) Destroy(gameObject);
        }
    }
}
