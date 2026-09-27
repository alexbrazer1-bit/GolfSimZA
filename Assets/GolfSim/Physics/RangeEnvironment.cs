using System.Collections.Generic;
using GolfSimZA.Core;
using UnityEngine;

namespace GolfSimZA.Physics
{
    /// <summary>
    /// Builds the GolfSim ZA driving range: mown fairway bands, distance lines every 25 m,
    /// a target green with flag at the chosen distance, a green aim line from the tee,
    /// hills in the distance and a sky. Rebuilt whenever the range settings change.
    /// </summary>
    public sealed class RangeEnvironment : MonoBehaviour
    {
        public const float MinTarget = 20f;
        public const float MaxTarget = 350f;

        private readonly List<GameObject> built = new List<GameObject>();
        private readonly List<GameObject> targetParts = new List<GameObject>();
        private Material grass, bandLight, bandDark, line, green, flagRed, white, tee, hill;
        private LineRenderer aimLine;

        public Vector3 TeePosition => Vector3.zero;
        public Vector3 TargetPosition { get; private set; }
        public float TargetDistance => TargetPosition.magnitude;
        public float FairwayWidth { get; private set; }
        public float GreenWidth { get; private set; }

        /// <summary>Distance lines along the range, in metres from the tee (for on-screen labels).</summary>
        public readonly List<float> MarkerDistances = new List<float>();

        public void Build(float targetMeters, float fairwayWidth, float greenWidth)
        {
            EnsureMaterials();
            foreach (GameObject go in built) if (go != null) Destroy(go);
            built.Clear();
            MarkerDistances.Clear();

            FairwayWidth = Mathf.Clamp(fairwayWidth, 15f, 120f);
            GreenWidth = Mathf.Clamp(greenWidth, 4f, 40f);
            float length = MaxTarget + 80f;

            // Ground (the only collider - the ball lands and rolls on it).
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Range_Ground";
            ground.transform.SetParent(transform, false);
            ground.transform.localScale = new Vector3(260f, 1f, 260f);
            ground.GetComponent<Renderer>().sharedMaterial = grass;
            built.Add(ground);

            // Mown bands.
            const float band = 15f;
            for (int i = 0; i * band < length; i++)
                Decor(PrimitiveType.Cube, "Range_Band", new Vector3(0f, 0.004f, i * band + band * 0.5f), new Vector3(FairwayWidth, 0.005f, band), i % 2 == 0 ? bandLight : bandDark);

            // Distance lines every 25 m, stronger every 50 m.
            for (float d = 25f; d <= MaxTarget + 1f; d += 25f)
            {
                bool major = Mathf.Approximately(d % 50f, 0f);
                Decor(PrimitiveType.Cube, "Range_Line_" + d, new Vector3(0f, 0.012f, d), new Vector3(FairwayWidth, 0.006f, major ? 0.35f : 0.15f), line);
                if (major) MarkerDistances.Add(d);
            }

            // Tee mat.
            Decor(PrimitiveType.Cube, "Range_TeeMat", new Vector3(0f, 0.01f, 0.3f), new Vector3(1.6f, 0.02f, 1.6f), tee);

            // Hills far away for depth.
            float[] hillX = { -520f, -250f, 40f, 330f, 600f };
            for (int i = 0; i < hillX.Length; i++)
                Decor(PrimitiveType.Sphere, "Range_Hill", new Vector3(hillX[i], -40f, 900f + (i % 2) * 120f), new Vector3(420f + i * 40f, 190f + (i % 3) * 50f, 260f), hill);

            // Aim line.
            GameObject lineObject = new GameObject("Range_AimLine");
            lineObject.transform.SetParent(transform, false);
            lineObject.layer = GroundProbe.IgnoreRaycastLayer;
            aimLine = lineObject.AddComponent<LineRenderer>();
            Shader unlit = Shader.Find("Sprites/Default");
            aimLine.material = unlit != null ? new Material(unlit) : green;
            aimLine.startColor = new Color(0.55f, 1f, 0.70f, 0.95f);
            aimLine.endColor = new Color(0.55f, 1f, 0.70f, 0.55f);
            aimLine.startWidth = 0.08f;
            aimLine.endWidth = 0.35f;
            aimLine.alignment = LineAlignment.View;
            aimLine.positionCount = 2;
            aimLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            built.Add(lineObject);

            SetTarget(new Vector3(0f, 0f, Mathf.Clamp(targetMeters, MinTarget, MaxTarget)));
            ApplySky();
        }

        /// <summary>Moves the target green and flag (used by the randomizer).</summary>
        public void SetTarget(Vector3 position)
        {
            foreach (GameObject go in targetParts) if (go != null) Destroy(go);
            targetParts.Clear();
            position.y = 0f;
            TargetPosition = position;

            targetParts.Add(Decor(PrimitiveType.Cylinder, "Range_Green", position + Vector3.up * 0.015f, new Vector3(GreenWidth, 0.01f, GreenWidth), green));
            targetParts.Add(Decor(PrimitiveType.Cylinder, "Range_GreenFringe", position + Vector3.up * 0.012f, new Vector3(GreenWidth + 2.5f, 0.008f, GreenWidth + 2.5f), bandLight));
            targetParts.Add(Decor(PrimitiveType.Cylinder, "Range_FlagPole", position + Vector3.up * 1.2f, new Vector3(0.05f, 1.2f, 0.05f), white));
            targetParts.Add(Decor(PrimitiveType.Cube, "Range_Flag", position + new Vector3(0.38f, 2.15f, 0f), new Vector3(0.7f, 0.42f, 0.02f), flagRed));

            if (aimLine != null)
            {
                aimLine.SetPosition(0, TeePosition + Vector3.up * 0.03f);
                aimLine.SetPosition(1, position + Vector3.up * 0.03f);
            }
        }

        public void RandomTarget(float maxDistance)
        {
            float distance = Random.Range(Mathf.Max(MinTarget, 40f), Mathf.Clamp(maxDistance, 60f, MaxTarget));
            float side = Random.Range(-FairwayWidth * 0.3f, FairwayWidth * 0.3f);
            SetTarget(new Vector3(side, 0f, distance));
        }

        private GameObject Decor(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.layer = GroundProbe.IgnoreRaycastLayer;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            built.Add(go);
            return go;
        }

        private void EnsureMaterials()
        {
            if (grass != null) return;
            grass = Mat(new Color(0.19f, 0.33f, 0.15f), 0.05f);
            bandLight = Mat(new Color(0.33f, 0.52f, 0.22f), 0.1f);
            bandDark = Mat(new Color(0.25f, 0.43f, 0.17f), 0.1f);
            line = Mat(new Color(0.92f, 0.94f, 0.92f), 0.2f);
            green = Mat(new Color(0.38f, 0.78f, 0.34f), 0.35f);
            flagRed = Mat(new Color(0.90f, 0.12f, 0.10f), 0.2f);
            white = Mat(Color.white, 0.3f);
            tee = Mat(new Color(0.08f, 0.26f, 0.14f), 0.05f);
            hill = Mat(new Color(0.30f, 0.38f, 0.27f), 0f);
        }

        private static Material Mat(Color color, float smoothness)
        {
            Shader shader = Shader.Find("Standard");
            var m = new Material(shader) { color = color };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            return m;
        }

        private static void ApplySky()
        {
            Shader sky = Shader.Find("Skybox/Procedural");
            if (sky != null && (RenderSettings.skybox == null || RenderSettings.skybox.shader != sky))
            {
                var material = new Material(sky);
                if (material.HasProperty("_AtmosphereThickness")) material.SetFloat("_AtmosphereThickness", 0.8f);
                if (material.HasProperty("_Exposure")) material.SetFloat("_Exposure", 1.25f);
                RenderSettings.skybox = material;
            }
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.farClipPlane = Mathf.Clamp(AppSettings.Current.drawDistanceMeters, 500f, 15000f);
            }
            RenderSettings.fogColor = new Color(0.72f, 0.80f, 0.86f);
            FlightPresentation.ApplyFog();
            GameOptions.ApplyLighting();
        }
    }
}
