using System.Collections.Generic;
using GolfSimZA.Core;
using GolfSimZA.LaunchMonitors.GarminR10;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace GolfSimZA.Minigames
{
    /// <summary>
    /// Procedural first playable for Nimavelle Golf: Target Rings.
    /// The course geometry is generated at runtime. Live Garmin Approach R10
    /// shots score from measured carry and horizontal launch direction; SPACE
    /// remains available for local demo shots.
    /// </summary>
    public sealed class TargetRingsCourse : MonoBehaviour
    {
        private const int ShotsPerRound = 10;
        private readonly int[] distances = { 80, 100, 120, 150, 200, 250 };
        private readonly int[] basePoints = { 5, 10, 20, 50 };

        private readonly Color[] targetColours =
        {
            new Color(1f, .76f, .16f),
            new Color(.08f, .92f, .62f),
            new Color(.08f, .72f, 1f),
            new Color(1f, .25f, .30f),
            new Color(.82f, .25f, 1f),
            new Color(.10f, .85f, .96f)
        };

        private readonly List<GameObject> generated = new List<GameObject>();
        private Transform courseRoot;
        private Camera courseCamera;
        private Vector2 scroll;
        private int selectedTarget = 2;
        private int selectedDifficulty = 0;
        private int playerCount = 1;
        private int currentPlayer = 1;
        private int shotNumber;
        private int totalScore;
        private int lastScore;
        private int[] playerScores = new int[8];
        private int[] playerBestScores = new int[8];
        private string[] playerNames = new string[8];
        private int lastPlayer;
        private float power = .82f;
        private float aim = .5f;
        private bool aerialView = true;
        private bool roundFinished;
        private bool nameFieldFocused;
        private string status = "Choose a target and hit your first shot.";
        private Vector3 teePosition = new Vector3(0f, 1.2f, 0f);
        private Vector3 ballPosition;
        private LineRenderer trajectory;
        private GameObject ballObject;
        private GarminR10Adapter r10Adapter;
        private R10BridgeLauncher bridgeLauncher;

        private string DifficultyName => new[] { "BEGINNER", "INTERMEDIATE", "ADVANCED", "PRO" }[selectedDifficulty];
        private float RingScale => new[] { 1.25f, 1f, .78f, .60f }[selectedDifficulty];

        private void Start()
        {
            for (int i = 0; i < playerNames.Length; i++)
                playerNames[i] = PlayerPrefs.GetString(PlayerNameKey(i), "Player " + (i + 1));

            // Target Rings can run as its own scene, so make sure the same Garmin
            // R10 receiver/bridge used by the main simulator is present here too.
            r10Adapter = FindFirstObjectByType<GarminR10Adapter>();
            if (r10Adapter == null) r10Adapter = gameObject.AddComponent<GarminR10Adapter>();
            r10Adapter.ShotReceived += OnRealShotReceived;

            bridgeLauncher = FindFirstObjectByType<R10BridgeLauncher>();
            if (bridgeLauncher == null) bridgeLauncher = gameObject.AddComponent<R10BridgeLauncher>();
            bridgeLauncher.StartBridge();

            BuildCourse();
        }

        private void Update()
        {
            if (courseCamera == null) return;
            if (Input.GetKeyDown(KeyCode.Space) && !roundFinished && !nameFieldFocused) TakeDemoShot();
            if (Input.GetKeyDown(KeyCode.Tab)) ToggleCamera();
        }

        private void BuildCourse()
        {
            if (courseRoot != null) Destroy(courseRoot.gameObject);
            generated.Clear();
            courseRoot = new GameObject("Nimavelle Target Rings - Generated Course").transform;
            courseRoot.SetParent(transform, false);

            CreateMaterial("Deep Lake", new Color(.025f, .31f, .38f), .25f);
            CreateMaterial("Island Grass", new Color(.20f, .42f, .22f), .05f);
            CreateMaterial("Fairway", new Color(.18f, .39f, .20f), .02f);
            CreateMaterial("Rock", new Color(.30f, .34f, .34f), 0f);
            CreateMaterial("Shore Sand", new Color(.68f, .58f, .38f), 0f);
            CreateMaterial("Mountain", new Color(.15f, .27f, .19f), 0f);
            CreateMaterial("Mountain Rock", new Color(.31f, .32f, .29f), 0f);
            CreateMaterial("Palm Leaf", new Color(.075f, .23f, .12f), 0f);
            CreateMaterial("Palm Leaf Light", new Color(.13f, .31f, .16f), 0f);
            materials["Deep Lake"].SetFloat("_Glossiness", .9f);
            materials["Palm Leaf"].SetFloat("_Glossiness", .18f);
            materials["Palm Leaf Light"].SetFloat("_Glossiness", .18f);
            materials["Mountain"].SetFloat("_Glossiness", .12f);
            materials["Mountain Rock"].SetFloat("_Glossiness", .2f);
            CreateMaterial("Wood", new Color(.28f, .16f, .08f), 0f);
            CreateMaterial("White", new Color(.93f, .96f, .92f), .15f);

            MakePrimitive(PrimitiveType.Plane, "Lake Surface", new Vector3(0, -.8f, 120), new Vector3(75, 1, 75), "Deep Lake");
            MakePrimitive(PrimitiveType.Plane, "Tee Platform", new Vector3(0, .15f, -2), new Vector3(1.5f, .1f, 1.2f), "Fairway");

            // One connected ridge with asymmetric summits and exposed rock, instead
            // of a row of isolated cone-shaped props.
            MakeMountainRange();

            MakeDistantShoreline();
            for (int i = 0; i < 38; i++)
            {
                float x = -170f + i * 9.2f;
                float z = 264f + Mathf.Sin(i * .8f) * 7f;
                MakeTree(new Vector3(x, .45f + Mathf.Max(0f, z - 260f) * .13f, z), .8f + Mathf.Abs(Mathf.Cos(i)) * 1.2f);
            }

            // Boulder clusters sit partly in the shallows along the far bank.
            for (int i = 0; i < 22; i++)
            {
                float x = -175f + i * 16.5f;
                float z = 247f + Mathf.Sin(i * 1.73f) * 3f;
                float size = 1.1f + Mathf.Abs(Mathf.Cos(i * .91f)) * 1.8f;
                GameObject shoreRock = MakePrimitive(PrimitiveType.Sphere, "Shoreline Boulder " + (i + 1),
                    new Vector3(x, -.35f, z), new Vector3(size * 1.5f, size * .55f, size), "Rock");
                shoreRock.transform.rotation = Quaternion.Euler(0f, i * 29f, i % 2 == 0 ? 5f : -4f);
            }

            Vector3[] positions =
            {
                new Vector3(-28f, .2f, 75f), new Vector3(31f, .2f, 95f),
                new Vector3(-52f, .2f, 108f), new Vector3(68f, .2f, 134f),
                new Vector3(-52f, .2f, 193f), new Vector3(74f, .2f, 239f)
            };
            float[] radii = { 11f, 12f, 13f, 14f, 16f, 18f };

            for (int i = 0; i < positions.Length; i++)
            {
                CreateIsland(i, positions[i], radii[i]);
            }

            // Simple timber tee deck, aiming marks and a ball.
            MakePrimitive(PrimitiveType.Cube, "Tee Deck", new Vector3(0, .05f, -4f), new Vector3(13f, .5f, 10f), "Wood");
            MakePrimitive(PrimitiveType.Cube, "Tee Turf", new Vector3(0, .34f, -4f), new Vector3(12.2f, .16f, 9.2f), "Fairway");
            for (int i = -2; i <= 2; i++)
                MakePrimitive(PrimitiveType.Cube, "Aim Marker", new Vector3(i * 1.7f, .44f, -1f), new Vector3(.08f, .03f, 1.2f), "White");

            ballPosition = new Vector3(0f, .55f, -3f);
            ballObject = MakePrimitive(PrimitiveType.Sphere, "Practice Ball", ballPosition, Vector3.one * .42f, "White");
            if (ballObject.TryGetComponent<Collider>(out var ballCollider)) ballCollider.enabled = false;

            if (trajectory != null) Destroy(trajectory.gameObject);
            GameObject lineObject = new GameObject("Shot Trajectory Preview");
            lineObject.transform.SetParent(courseRoot, false);
            trajectory = lineObject.AddComponent<LineRenderer>();
            trajectory.positionCount = 0;
            trajectory.startWidth = .45f;
            trajectory.endWidth = .15f;
            trajectory.material = MakeUnlit(new Color(.15f, .92f, 1f, .9f));
            trajectory.numCapVertices = 4;
            trajectory.enabled = false;

            SetupCamera();
            SetupLighting();
            roundFinished = false;
            shotNumber = 0;
            totalScore = 0;
            lastScore = 0;
            System.Array.Clear(playerScores, 0, playerScores.Length);
            for (int i = 0; i < playerBestScores.Length; i++)
                playerBestScores[i] = PlayerPrefs.GetInt(BestScoreKey(i), PlayerPrefs.GetInt(LegacyBestScoreKey(i), 0));
            currentPlayer = 1;
            lastPlayer = 0;
            status = "Choose a target and hit your first shot.";
        }

        private void CreateIsland(int index, Vector3 position, float radius)
        {
            Color accent = targetColours[index];
            GameObject rock = MakePrimitive(PrimitiveType.Cylinder, "Island Rock Base " + (index + 1),
                position + Vector3.down * .55f, new Vector3(radius * 2f, .55f, radius * 2f), "Rock");
            GameObject turf = MakePrimitive(PrimitiveType.Cylinder, "Island Turf " + (index + 1),
                position + Vector3.up * .25f, new Vector3(radius * 1.88f, .38f, radius * 1.88f), "Island Grass");
            CreateTargetBand("Outer 5 Point Zone", position + Vector3.up * .705f, radius * .74f, radius * .49f, new Color(1f, .56f, .12f));
            CreateTargetBand("Middle 10 Point Zone", position + Vector3.up * .71f, radius * .49f, radius * .27f, new Color(.08f, .72f, 1f));
            CreateTargetBand("Inner 20 Point Zone", position + Vector3.up * .715f, radius * .27f, radius * .12f, new Color(.12f, .88f, .48f));
            GameObject bullseye = MakePrimitive(PrimitiveType.Cylinder, "Bullseye 50 Point Zone",
                position + Vector3.up * .69f, new Vector3(radius * .24f, .025f, radius * .24f), "White");
            SetColor(bullseye, new Color(1f, .88f, .16f));
            CreateTargetBand("Target Outer Border", position + Vector3.up * .72f, radius * .96f, radius * .89f, Color.white);

            GameObject pole = MakePrimitive(PrimitiveType.Cylinder, "Flag Pole " + (index + 1),
                position + new Vector3(0, 2.1f, 0), new Vector3(.12f, 1.35f, .12f), "White");
            GameObject flag = MakePrimitive(PrimitiveType.Cube, "Flag " + (index + 1),
                position + new Vector3(.65f, 2.9f, 0), new Vector3(1.25f, .7f, .06f), "White");
            SetColor(flag, accent);
            GameObject label = MakePrimitive(PrimitiveType.Cube, "Distance Marker " + (index + 1),
                position + new Vector3(0, 3.4f, -radius * .5f), new Vector3(2.5f, .65f, .08f), "White");
            SetColor(label, accent);
            // Keep each island's scoring zones visible from both camera views.
            pole.transform.SetParent(courseRoot, true);
            turf.name = "Target Island " + (index + 1) + " - " + distances[index] + "m";
        }

        private void MakeDistantShoreline()
        {
            const int columns = 61;
            float[] zRows = { 240f, 246f, 252f, 260f, 272f, 288f };
            float[] heights = { -.78f, -.74f, -.52f, .12f, 1.7f, 7f };
            int rows = zRows.Length;
            var vertices = new Vector3[columns * rows];
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    float x = -210f + column * 7f;
                    float z = zRows[row] + Mathf.Sin(column * .31f + row * .8f) * (row == 0 || row == rows - 1 ? 1.5f : 2.3f);
                    float y = heights[row] + Mathf.Sin(column * .22f + row) * (row < 2 ? .08f : .45f);
                    vertices[row * columns + column] = new Vector3(x, y, z);
                }
            }

            var rockTriangles = new List<int>();
            var sandTriangles = new List<int>();
            var grassTriangles = new List<int>();
            for (int row = 0; row < rows - 1; row++)
            {
                List<int> destination = row == 0 ? rockTriangles : row == 1 ? sandTriangles : grassTriangles;
                for (int column = 0; column < columns - 1; column++)
                {
                    int a = row * columns + column;
                    int b = (row + 1) * columns + column;
                    int c = a + 1;
                    int d = b + 1;
                    destination.Add(a); destination.Add(b); destination.Add(c);
                    destination.Add(c); destination.Add(b); destination.Add(d);
                }
            }

            var mesh = new Mesh { name = "Nimavelle Far Shore Mesh", vertices = vertices, subMeshCount = 3 };
            mesh.SetTriangles(rockTriangles, 0);
            mesh.SetTriangles(sandTriangles, 1);
            mesh.SetTriangles(grassTriangles, 2);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var shore = new GameObject("Nimavelle Sandy Shore and Forest Bank");
            shore.transform.SetParent(courseRoot, false);
            shore.AddComponent<MeshFilter>().sharedMesh = mesh;
            shore.AddComponent<MeshRenderer>().sharedMaterials = new[] { GetMaterial("Rock"), GetMaterial("Shore Sand"), GetMaterial("Island Grass") };
            generated.Add(shore);
        }

        private void MakeMountainRange()
        {
            const int columns = 105;
            const int rows = 33;
            const float left = -260f;
            const float width = 520f;
            const float front = 270f;
            const float depth = 132f;
            const float footHeight = 6.5f;
            const float peakHeight = 92f;
            float[] peaks = { .48f, .78f, .42f, .93f, .64f, .52f, .86f, .40f, 1f, .58f, .75f, .38f, .91f, .57f, .82f };
            var vertices = new Vector3[columns * rows];
            for (int row = 0; row < rows; row++)
            {
                float z = front + depth * row / (rows - 1f);
                float depthT = row / (rows - 1f);
                float depthShape = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(depthT * Mathf.PI)), .72f);
                for (int column = 0; column < columns; column++)
                {
                    float x = left + width * column / (columns - 1f);
                    float profile = SampleMountainProfile(x, peaks);
                    float broadRidge = (Mathf.PerlinNoise((x + 411f) * .042f, (z + 83f) * .031f) - .5f) * 12f;
                    float rockDetail = (Mathf.PerlinNoise((x - 23f) * .13f, (z + 51f) * .11f) - .5f) * 4f;
                    float y = footHeight + peakHeight * profile * depthShape + (broadRidge + rockDetail) * depthShape;
                    vertices[row * columns + column] = new Vector3(x, y, z);
                }
            }

            var forestTriangles = new List<int>();
            var rockTriangles = new List<int>();
            for (int row = 0; row < rows - 1; row++)
            {
                for (int column = 0; column < columns - 1; column++)
                {
                    int a = row * columns + column;
                    int b = (row + 1) * columns + column;
                    int c = a + 1;
                    int d = b + 1;
                    float averageHeight = (vertices[a].y + vertices[b].y + vertices[c].y + vertices[d].y) * .25f;
                    List<int> destination = averageHeight > footHeight + peakHeight * .62f ? rockTriangles : forestTriangles;
                    destination.Add(a); destination.Add(b); destination.Add(c);
                    destination.Add(c); destination.Add(b); destination.Add(d);
                }
            }

            var mesh = new Mesh { name = "Nimavelle Connected Mountain Ridge", vertices = vertices, subMeshCount = 2 };
            mesh.SetTriangles(forestTriangles, 0);
            mesh.SetTriangles(rockTriangles, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var mountainRange = new GameObject("Nimavelle Mountain Range");
            mountainRange.transform.SetParent(courseRoot, false);
            mountainRange.AddComponent<MeshFilter>().sharedMesh = mesh;
            mountainRange.AddComponent<MeshRenderer>().sharedMaterials = new[] { GetMaterial("Mountain"), GetMaterial("Mountain Rock") };
            generated.Add(mountainRange);
        }

        private float SampleMountainProfile(float x, float[] peaks)
        {
            float t = Mathf.Clamp01((x + 260f) / 520f) * (peaks.Length - 1);
            int peakIndex = Mathf.Min(Mathf.FloorToInt(t), peaks.Length - 2);
            float between = t - peakIndex;
            float saddle = Mathf.Min(peaks[peakIndex], peaks[peakIndex + 1]) * .28f;
            float height = between < .5f
                ? Mathf.Lerp(peaks[peakIndex], saddle, between * 2f)
                : Mathf.Lerp(saddle, peaks[peakIndex + 1], (between - .5f) * 2f);
            return .12f + height * .88f;
        }

        private void CreateTargetBand(string bandName, Vector3 center, float outerRadius, float innerRadius, Color colour)
        {
            const int segments = 64;
            var vertices = new Vector3[(segments + 1) * 2];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(angle), z = Mathf.Sin(angle);
                vertices[i * 2] = new Vector3(x * innerRadius, 0f, z * innerRadius);
                vertices[i * 2 + 1] = new Vector3(x * outerRadius, 0f, z * outerRadius);
            }
            int t = 0;
            for (int i = 0; i < segments; i++)
            {
                int inner = i * 2;
                int outer = inner + 1;
                int nextInner = inner + 2;
                int nextOuter = inner + 3;
                // Wind the scoring surface upward so it remains visible from the tee camera.
                triangles[t++] = inner; triangles[t++] = nextInner; triangles[t++] = outer;
                triangles[t++] = nextInner; triangles[t++] = nextOuter; triangles[t++] = outer;
            }
            Mesh mesh = new Mesh { name = bandName + " Mesh", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            GameObject band = new GameObject(bandName);
            band.transform.SetParent(courseRoot, false);
            band.transform.position = center;
            var filter = band.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = band.AddComponent<MeshRenderer>();
            renderer.material = MakeUnlit(colour);
        }

        private void MakeTree(Vector3 position, float scale)
        {
            GameObject trunk = MakePrimitive(PrimitiveType.Cylinder, "Palm Trunk", position + Vector3.up * (scale * 1.35f), new Vector3(.16f * scale, scale * 1.35f, .16f * scale), "Wood");
            trunk.transform.rotation = Quaternion.Euler(0f, scale * 37f, 3f);
            const int leaves = 9;
            var vertices = new Vector3[leaves * 6];
            var darkTriangles = new List<int>();
            var lightTriangles = new List<int>();
            Vector3 crown = position + Vector3.up * (scale * 2.55f);
            for (int leaf = 0; leaf < leaves; leaf++)
            {
                float angle = leaf * Mathf.PI * 2f / leaves + scale * .47f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 side = Vector3.Cross(direction, Vector3.up).normalized;
                int start = leaf * 6;
                vertices[start] = crown;
                Vector3 firstCentre = crown + direction * (scale * .42f) + Vector3.up * (scale * .33f);
                vertices[start + 1] = firstCentre - side * (scale * .16f);
                vertices[start + 2] = firstCentre + side * (scale * .16f);
                Vector3 secondCentre = crown + direction * (scale * 1.02f) + Vector3.down * (scale * .02f);
                vertices[start + 3] = secondCentre - side * (scale * .105f);
                vertices[start + 4] = secondCentre + side * (scale * .105f);
                vertices[start + 5] = crown + direction * (scale * 1.55f) + Vector3.down * (scale * .44f);

                List<int> indices = leaf % 2 == 0 ? darkTriangles : lightTriangles;
                indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
                indices.Add(start + 1); indices.Add(start + 3); indices.Add(start + 4);
                indices.Add(start + 1); indices.Add(start + 5); indices.Add(start + 3);
                indices.Add(start + 2); indices.Add(start + 4); indices.Add(start + 5);
            }

            var crownMesh = new Mesh { name = "Palm Fronds Mesh", vertices = vertices, subMeshCount = 2 };
            crownMesh.SetTriangles(darkTriangles, 0);
            crownMesh.SetTriangles(lightTriangles, 1);
            crownMesh.RecalculateNormals();
            crownMesh.RecalculateBounds();
            var canopy = new GameObject("Palm Fronds");
            canopy.transform.SetParent(courseRoot, false);
            canopy.transform.position = Vector3.zero;
            canopy.AddComponent<MeshFilter>().sharedMesh = crownMesh;
            canopy.AddComponent<MeshRenderer>().sharedMaterials = new[] { GetMaterial("Palm Leaf"), GetMaterial("Palm Leaf Light") };
            generated.Add(canopy);
        }

        private GameObject MakePrimitive(PrimitiveType type, string objectName, Vector3 position, Vector3 scale, string materialName)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = objectName;
            go.transform.SetParent(courseRoot, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = GetMaterial(materialName);
            generated.Add(go);
            return go;
        }

        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        private void CreateMaterial(string materialName, Color colour, float metallic)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.name = materialName;
            mat.color = colour;
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Glossiness", .62f);
            materials[materialName] = mat;
        }

        private Material GetMaterial(string materialName)
        {
            if (materials.TryGetValue(materialName, out Material mat)) return mat;
            return materials["Island Grass"];
        }

        private Material MakeUnlit(Color colour)
        {
            Shader shader = Shader.Find("Unlit/Color");
            Material material = new Material(shader != null ? shader : Shader.Find("Standard"));
            material.color = colour;
            return material;
        }

        private void SetColor(GameObject go, Color colour)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            Material material = new Material(renderer.sharedMaterial) { color = colour };
            renderer.sharedMaterial = material;
        }

        private void SetupCamera()
        {
            courseCamera = Camera.main;
            if (courseCamera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                courseCamera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }
            courseCamera.fieldOfView = 52f;
            courseCamera.nearClipPlane = .1f;
            courseCamera.farClipPlane = 800f;
            SetCameraView();
        }

        private void SetCameraView()
        {
            if (courseCamera == null) return;
            if (aerialView)
            {
                courseCamera.transform.position = new Vector3(0f, 220f, 115f);
                courseCamera.transform.rotation = Quaternion.Euler(63f, 0f, 0f);
                courseCamera.fieldOfView = 58f;
            }
            else
            {
                courseCamera.transform.position = new Vector3(0f, 34f, -24f);
                courseCamera.transform.LookAt(new Vector3(0f, 2f, 110f));
                courseCamera.fieldOfView = 65f;
            }
        }

        private void SetupLighting()
        {
            Light sun = FindObjectOfType<Light>();
            if (sun == null)
            {
                GameObject lightObject = new GameObject("Nimavelle Mountain Sun");
                sun = lightObject.AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.intensity = 1.35f;
            sun.color = new Color(1f, .91f, .76f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .72f;
            sun.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.58f, .68f, .82f);
            RenderSettings.ambientEquatorColor = new Color(.43f, .60f, .59f);
            RenderSettings.ambientGroundColor = new Color(.20f, .30f, .24f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.58f, .72f, .72f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 240f;
            RenderSettings.fogEndDistance = 600f;
        }

        private void TakeDemoShot()
        {
            if (roundFinished) return;
            lastPlayer = (shotNumber % playerCount);
            currentPlayer = lastPlayer + 1;
            shotNumber++;
            float accuracyOffset = Random.Range(-1f, 1f) * (1f - aim) * 18f;
            float distanceFactor = Mathf.Clamp(power + Random.Range(-.09f, .09f), .2f, 1.08f);
            float shotDistance = distances[selectedTarget] * distanceFactor;
            Vector3 target = TargetPosition(selectedTarget);
            Vector3 landing = teePosition + new Vector3(
                target.x * (shotDistance / distances[selectedTarget]) + accuracyOffset,
                0f,
                target.z * (shotDistance / distances[selectedTarget]));
            ballPosition = landing + Vector3.up * .35f;
            if (ballObject != null) ballObject.transform.position = ballPosition;
            DrawTrajectory(teePosition, ballPosition);

            ApplyLanding(landing, target, "Demo shot");
        }

        private void OnRealShotReceived(ShotData shot)
        {
            if (roundFinished) return;
            if (!shot.IsValid || shot.CarryMeters <= 0f)
            {
                status = "Garmin R10 shot received without carry data. Check the R10 bridge and try again.";
                return;
            }

            lastPlayer = shotNumber % playerCount;
            currentPlayer = lastPlayer + 1;
            shotNumber++;

            // The tee aims down +Z. HLA is the measured horizontal launch angle
            // relative to the monitor's alignment; use measured carry and direction
            // to locate the actual landing point against the selected target.
            float directionRadians = shot.LaunchDirectionDeg * Mathf.Deg2Rad;
            Vector3 landing = teePosition + new Vector3(
                Mathf.Sin(directionRadians) * shot.CarryMeters,
                0f,
                Mathf.Cos(directionRadians) * shot.CarryMeters);
            ballPosition = landing + Vector3.up * .35f;
            if (ballObject != null) ballObject.transform.position = ballPosition;
            DrawTrajectory(teePosition, ballPosition);

            ApplyLanding(landing, TargetPosition(selectedTarget),
                $"Garmin R10: {shot.CarryMeters:0}m carry, {shot.LaunchDirectionDeg:+0.0;-0.0;0.0}° offline");
        }

        private void ApplyLanding(Vector3 landing, Vector3 target, string source)
        {

            float miss = Vector2.Distance(new Vector2(landing.x, landing.z), new Vector2(target.x, target.z));
            float radius = new[] { 12f, 10f, 8f, 6f }[selectedDifficulty];
            lastScore = miss < radius * .12f ? 50 : miss < radius * .27f ? 20 : miss < radius * .48f ? 10 : miss < radius * .74f ? 5 : 0;
            totalScore += lastScore;
            playerScores[lastPlayer] += lastScore;
            string shooterName = DisplayName(lastPlayer);
            status = lastScore > 0
                ? $"{source}: {shooterName} landed {Mathf.RoundToInt(miss)}m from centre — +{lastScore} points!"
                : $"{source}: {shooterName} missed by {Mathf.RoundToInt(miss)}m. Adjust your shot.";
            if (shotNumber >= ShotsPerRound)
            {
                roundFinished = true;
                for (int i = 0; i < playerCount; i++)
                {
                    string key = BestScoreKey(i);
                    playerBestScores[i] = Mathf.Max(playerBestScores[i], playerScores[i]);
                    PlayerPrefs.SetInt(key, playerBestScores[i]);
                }
                PlayerPrefs.Save();
                int winner = 0;
                for (int i = 1; i < playerCount; i++)
                    if (playerScores[i] > playerScores[winner]) winner = i;
                status = playerCount == 1
                    ? $"ROUND COMPLETE — {totalScore} points. {DisplayName(0)}'s best: {playerBestScores[0]}. Press NEW ROUND to play again."
                    : $"ROUND COMPLETE — {DisplayName(winner)} wins with {playerScores[winner]} points! Total: {totalScore}. Press NEW ROUND.";
            }
            else currentPlayer = (shotNumber % playerCount) + 1;
        }

        private void DrawTrajectory(Vector3 from, Vector3 to)
        {
            if (trajectory == null) return;
            trajectory.enabled = true;
            trajectory.positionCount = 25;
            for (int i = 0; i < 25; i++)
            {
                float t = i / 24f;
                Vector3 p = Vector3.Lerp(from, to, t);
                p.y += Mathf.Sin(t * Mathf.PI) * Mathf.Clamp(Vector3.Distance(from, to) * .17f, 4f, 36f);
                trajectory.SetPosition(i, p);
            }
        }

        private Vector3 TargetPosition(int index)
        {
            Vector3[] positions =
            {
                new Vector3(-28f, 0f, 75f), new Vector3(31f, 0f, 95f),
                new Vector3(-52f, 0f, 108f), new Vector3(68f, 0f, 134f),
                new Vector3(-52f, 0f, 193f), new Vector3(74f, 0f, 239f)
            };
            return positions[Mathf.Clamp(index, 0, positions.Length - 1)];
        }

        private void ToggleCamera()
        {
            aerialView = !aerialView;
            SetCameraView();
        }

        private void StartNewRound()
        {
            shotNumber = 0;
            totalScore = 0;
            lastScore = 0;
            System.Array.Clear(playerScores, 0, playerScores.Length);
            currentPlayer = 1;
            lastPlayer = 0;
            roundFinished = false;
            if (trajectory != null) trajectory.enabled = false;
            ballPosition = new Vector3(0f, .55f, -3f);
            if (ballObject != null) ballObject.transform.position = ballPosition;
            status = "New round ready. Good luck!";
        }

        private void OnGUI()
        {
            nameFieldFocused = false;
            GUI.depth = -10;
            float width = Mathf.Min(410f, Screen.width * .33f);
            Rect panel = new Rect(18f, 18f, width, Screen.height - 36f);
            DrawPanel(panel, new Color(.025f, .055f, .075f, .94f));

            GUILayout.BeginArea(new Rect(panel.x + 18, panel.y + 12, panel.width - 36, panel.height - 24));
            GUILayout.Label("NIMAVELLE  /  GOLF", HeaderStyle(18));
            GUILayout.Label("TARGET RINGS", HeaderStyle(29, new Color(.2f, .85f, 1f)));
            GUILayout.Label("TROPICAL MOUNTAIN LAKE  •  1–8 PLAYERS", SmallStyle());
            bool r10Connected = r10Adapter != null && r10Adapter.IsConnected
                && LaunchMonitorStatus.R10Known && LaunchMonitorStatus.R10Connected;
            GUILayout.Label(r10Connected ? "● GARMIN R10 CONNECTED — LIVE SHOTS" : "○ GARMIN R10 WAITING — SPACE FOR DEMO SHOTS",
                SmallStyle(r10Connected ? new Color(.28f, .92f, .62f) : new Color(.72f, .78f, .82f)));
            GUILayout.Space(6);

            GUILayout.Label("PLAYERS", SectionStyle());
            GUILayout.BeginHorizontal();
            bool canChangePlayers = shotNumber == 0 || roundFinished;
            GUI.enabled = canChangePlayers;
            if (GUILayout.Button("−", GUILayout.Width(42), GUILayout.Height(32))) playerCount = Mathf.Max(1, playerCount - 1);
            GUILayout.Label(playerCount + (playerCount == 1 ? " PLAYER" : " PLAYERS"), HeaderStyle(16), GUILayout.Height(32));
            if (GUILayout.Button("+", GUILayout.Width(42), GUILayout.Height(32))) playerCount = Mathf.Min(8, playerCount + 1);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (!canChangePlayers) GUILayout.Label("Player count locks after the first shot.", TinyStyle());
            GUILayout.Label("PLAYER NAMES  •  CLICK TO EDIT", SectionStyle());
            for (int i = 0; i < playerCount; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("P" + (i + 1), SmallStyle(), GUILayout.Width(28));
                GUI.SetNextControlName("NimavellePlayerName" + i);
                GUI.enabled = canChangePlayers;
                string editedName = GUILayout.TextField(playerNames[i], 18, GUILayout.Height(24));
                GUI.enabled = true;
                if (editedName != playerNames[i])
                {
                    playerNames[i] = editedName;
                    PlayerPrefs.SetString(PlayerNameKey(i), playerNames[i]);
                    playerBestScores[i] = PlayerPrefs.GetInt(BestScoreKey(i), PlayerPrefs.GetInt(LegacyBestScoreKey(i), 0));
                    PlayerPrefs.Save();
                }
                if (GUI.GetNameOfFocusedControl() == "NimavellePlayerName" + i)
                    nameFieldFocused = true;
                GUILayout.EndHorizontal();
            }
            currentPlayer = (shotNumber % playerCount) + 1;
            GUILayout.Label("UP NEXT: " + DisplayName(currentPlayer - 1), SmallStyle());
            GUILayout.Space(4);

            GUILayout.Label("SELECT TARGET", SectionStyle());
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(Mathf.Min(184, panel.height * .24f)));
            for (int i = 0; i < distances.Length; i++)
            {
                GUI.backgroundColor = selectedTarget == i ? targetColours[i] : new Color(.08f, .14f, .17f);
                if (GUILayout.Button($"  {i + 1}.  {distances[i]} METRES     {(selectedTarget == i ? "SELECTED" : "TARGET ISLAND")}", GUILayout.Height(28)))
                {
                    selectedTarget = i;
                    power = Mathf.Clamp((float)distances[i] / 250f + .08f, .35f, 1f);
                }
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndScrollView();
            GUILayout.Space(4);

            GUILayout.Label("DIFFICULTY", SectionStyle());
            GUILayout.BeginHorizontal();
            string[] modes = { "BEGINNER", "INTERMEDIATE", "ADVANCED", "PRO" };
            for (int i = 0; i < modes.Length; i++)
            {
                GUI.backgroundColor = selectedDifficulty == i ? targetColours[i] : new Color(.1f, .16f, .19f);
                if (GUILayout.Button(modes[i], GUILayout.Height(30))) selectedDifficulty = i;
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            GUILayout.Label("DEMO SHOT POWER", SectionStyle());
            power = GUILayout.HorizontalSlider(power, .2f, 1.08f, GUILayout.Height(24));
            GUILayout.Label(r10Connected ? "Live carry comes from the R10" : Mathf.RoundToInt(power * distances[selectedTarget]) + "m demo carry", SmallStyle());
            GUILayout.Label("DEMO AIM CONTROL", SectionStyle());
            aim = GUILayout.HorizontalSlider(aim, 0f, 1f, GUILayout.Height(24));
            GUILayout.Label(aim < .35f ? "Left bias" : aim > .65f ? "Right bias" : "Centre aim", SmallStyle());
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("HIT SHOT  [SPACE]", GUILayout.Height(46))) TakeDemoShot();
            if (GUILayout.Button(aerialView ? "PLAY VIEW  [TAB]" : "AERIAL VIEW  [TAB]", GUILayout.Height(46))) ToggleCamera();
            GUILayout.EndHorizontal();
            if (roundFinished && GUILayout.Button("NEW ROUND", GUILayout.Height(38))) StartNewRound();
            GUILayout.Space(6);

            GUILayout.Label("ROUND HUD", SectionStyle());
            GUILayout.BeginHorizontal();
            DrawStat("SHOT", $"{Mathf.Min(shotNumber, ShotsPerRound)}/{ShotsPerRound}");
            DrawStat("LAST SHOT", $"+{lastScore}");
            DrawStat("TOTAL", totalScore.ToString());
            GUILayout.EndHorizontal();
            GUILayout.Label("PLAYER SCORES", SectionStyle());
            for (int i = 0; i < playerCount; i++)
                GUILayout.Label($"{DisplayName(i)}: {playerScores[i]} pts   •   BEST {playerBestScores[i]}", SmallStyle());
            GUILayout.Space(8);
            GUILayout.Label(status, WordWrapStyle());
            GUILayout.Space(8);
            GUILayout.Label("SCORING", SectionStyle());
            GUILayout.Label("OUTER  5    •    MIDDLE  10", SmallStyle());
            GUILayout.Label("INNER  20    •    BULLSEYE  50", SmallStyle());
            GUILayout.Label(r10Connected
                ? "Real shots use measured carry and launch direction. Aim the R10 at the course and select the island before hitting."
                : "Pair the Garmin Approach R10 and keep its GolfSimZA bridge connected. SPACE is for demo shots.", TinyStyle());
            GUILayout.EndArea();
        }

        private void DrawStat(string label, string value)
        {
            GUILayout.BeginVertical(GUILayout.Width(105));
            GUILayout.Label(label, TinyStyle());
            GUILayout.Label(value, HeaderStyle(20, new Color(.2f, .85f, 1f)));
            GUILayout.EndVertical();
        }

        private string BestScoreKey(int playerIndex)
        {
            string name = DisplayName(playerIndex).Trim().ToLowerInvariant().Replace(" ", "_");
            return "Nimavelle.TargetRings.Best." + name;
        }

        private static string LegacyBestScoreKey(int playerIndex)
        {
            return "Nimavelle.TargetRings.Best.Player" + (playerIndex + 1);
        }

        private static string PlayerNameKey(int playerIndex)
        {
            return "Nimavelle.TargetRings.PlayerName." + playerIndex;
        }

        private string DisplayName(int playerIndex)
        {
            string name = playerNames[Mathf.Clamp(playerIndex, 0, playerNames.Length - 1)];
            return string.IsNullOrWhiteSpace(name) ? "Player " + (playerIndex + 1) : name.Trim();
        }

        private void DrawPanel(Rect rect, Color colour)
        {
            Color previous = GUI.color;
            GUI.color = colour;
            GUI.Box(rect, GUIContent.none);
            GUI.color = previous;
        }

        private GUIStyle HeaderStyle(int size, Color? colour = null)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = size, fontStyle = FontStyle.Bold,
                normal = { textColor = colour ?? Color.white },
                wordWrap = true
            };
        }

        private GUIStyle SectionStyle()
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, .61f, .17f) }
            };
        }

        private GUIStyle SmallStyle(Color? colour = null)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, normal = { textColor = colour ?? new Color(.65f, .78f, .82f) },
                wordWrap = true
            };
        }

        private GUIStyle TinyStyle()
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = 9, normal = { textColor = new Color(.52f, .66f, .7f) },
                wordWrap = true
            };
        }

        private GUIStyle WordWrapStyle()
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(.45f, .95f, .75f) },
                wordWrap = true
            };
        }

        private void OnDestroy()
        {
            if (r10Adapter != null) r10Adapter.ShotReceived -= OnRealShotReceived;
            PlayerPrefs.Save();
            foreach (Material material in materials.Values)
                if (material != null) Destroy(material);
        }
    }

#if UNITY_EDITOR
    public static class TargetRingsCourseSetup
    {
        [MenuItem("Nimavelle Golf/Build Target Rings Demo Scene")]
        public static void BuildDemoScene()
        {
            const string scenePath = "Assets/Scenes/Nimavelle_TargetRings.unity";
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("Nimavelle Target Rings");
            root.AddComponent<TargetRingsCourse>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == scenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Nimavelle Golf", "Target Rings demo scene created and added to Build Settings:\n\n" + scenePath + "\n\nOpen the scene and press Play. Use SPACE to hit a demo shot and TAB to switch camera views.", "Great!");
        }
    }
#endif
}
