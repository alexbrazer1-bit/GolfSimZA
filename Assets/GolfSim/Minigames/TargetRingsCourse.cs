using System.Collections.Generic;
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
    /// The course geometry and test-shot loop are generated at runtime so no
    /// external art packages are required. Replace the demo shot provider with
    /// the simulator's approved launch-monitor/ShotData adapter when integrating.
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
        private float power = .82f;
        private float aim = .5f;
        private bool aerialView = true;
        private bool roundFinished;
        private string status = "Choose a target and hit your first shot.";
        private Vector3 teePosition = new Vector3(0f, 1.2f, 0f);
        private Vector3 ballPosition;
        private LineRenderer trajectory;
        private GameObject ballObject;

        private string DifficultyName => new[] { "BEGINNER", "INTERMEDIATE", "ADVANCED", "PRO" }[selectedDifficulty];
        private float RingScale => new[] { 1.25f, 1f, .78f, .60f }[selectedDifficulty];

        private void Start()
        {
            BuildCourse();
        }

        private void Update()
        {
            if (courseCamera == null) return;
            if (Input.GetKeyDown(KeyCode.Space) && !roundFinished) TakeDemoShot();
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
            CreateMaterial("Wood", new Color(.28f, .16f, .08f), 0f);
            CreateMaterial("White", new Color(.93f, .96f, .92f), .15f);

            MakePrimitive(PrimitiveType.Plane, "Lake Surface", new Vector3(0, -.8f, 120), new Vector3(75, 1, 75), "Deep Lake");
            MakePrimitive(PrimitiveType.Plane, "Tee Platform", new Vector3(0, .15f, -2), new Vector3(1.5f, .1f, 1.2f), "Fairway");

            // A layered mountain bowl creates a readable tropical-alpine horizon without assets.
            for (int i = 0; i < 17; i++)
            {
                float x = -180 + i * 22.5f;
                float z = 285 + Mathf.Sin(i * 1.17f) * 18f;
                float h = 42 + Mathf.Abs(Mathf.Sin(i * 2.1f)) * 48f;
                GameObject mountain = MakePrimitive(PrimitiveType.Sphere, "Mountain Ridge " + (i + 1),
                    new Vector3(x, h * .36f - 5f, z), new Vector3(24f + (i % 3) * 7f, h, 23f + (i % 4) * 4f),
                    i % 3 == 0 ? "White" : "Rock");
                mountain.transform.localScale = new Vector3(mountain.transform.localScale.x, h, mountain.transform.localScale.z);
            }

            // Distant tree lines and small shore rocks frame the course.
            for (int i = 0; i < 46; i++)
            {
                float x = -170f + i * 7.5f;
                float z = 20f + Mathf.Abs(Mathf.Sin(i * 1.4f)) * 22f;
                if (Mathf.Abs(x) < 22f) continue;
                MakeTree(new Vector3(x, 0f, z), .75f + Mathf.Abs(Mathf.Sin(i * .7f)) * 1.1f);
            }
            for (int i = 0; i < 38; i++)
            {
                float x = -170f + i * 9.2f;
                MakeTree(new Vector3(x, 0f, 264f + Mathf.Sin(i * .8f) * 12f), .8f + Mathf.Abs(Mathf.Cos(i)) * 1.2f);
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
            status = "Choose a target and hit your first shot.";
        }

        private void CreateIsland(int index, Vector3 position, float radius)
        {
            Color accent = targetColours[index];
            GameObject rock = MakePrimitive(PrimitiveType.Cylinder, "Island Rock Base " + (index + 1),
                position + Vector3.down * .55f, new Vector3(radius * 2f, 1.8f, radius * 2f), "Rock");
            GameObject turf = MakePrimitive(PrimitiveType.Cylinder, "Island Turf " + (index + 1),
                position + Vector3.up * .25f, new Vector3(radius * 1.88f, .38f, radius * 1.88f), "Island Grass");
            MakePrimitive(PrimitiveType.Cylinder, "Target Outer Ring " + (index + 1),
                position + Vector3.up * .53f, new Vector3(radius * .95f, .08f, radius * .95f), "White");
            CreateRing("Outer 5 Point Ring", position + Vector3.up * .64f, radius * .74f, .65f, targetColours[0]);
            CreateRing("Middle 10 Point Ring", position + Vector3.up * .70f, radius * .48f, .65f, targetColours[1]);
            CreateRing("Inner 20 Point Ring", position + Vector3.up * .76f, radius * .27f, .65f, targetColours[2]);
            CreateRing("Bullseye 50 Point Ring", position + Vector3.up * .82f, radius * .12f, .65f, targetColours[3]);

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

        private void CreateRing(string ringName, Vector3 center, float radius, float thickness, Color colour)
        {
            const int segments = 96;
            const int sides = 8;
            var vertices = new Vector3[(segments + 1) * sides];
            var triangles = new int[segments * sides * 6];
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                for (int j = 0; j < sides; j++)
                {
                    float sideAngle = j * Mathf.PI * 2f / sides;
                    float r = radius + Mathf.Cos(sideAngle) * thickness * .5f;
                    float y = Mathf.Sin(sideAngle) * thickness * .5f;
                    vertices[i * sides + j] = new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
                }
            }
            int t = 0;
            for (int i = 0; i < segments; i++)
            for (int j = 0; j < sides; j++)
            {
                int a = i * sides + j;
                int b = i * sides + (j + 1) % sides;
                int c = (i + 1) * sides + j;
                int d = (i + 1) * sides + (j + 1) % sides;
                triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
            }
            Mesh mesh = new Mesh { name = ringName + " Mesh", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            GameObject ring = new GameObject(ringName);
            ring.transform.SetParent(courseRoot, false);
            ring.transform.position = center;
            var filter = ring.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = ring.AddComponent<MeshRenderer>();
            renderer.material = MakeUnlit(colour);
        }

        private void MakeTree(Vector3 position, float scale)
        {
            GameObject trunk = MakePrimitive(PrimitiveType.Cylinder, "Pine Trunk", position + Vector3.up * scale, new Vector3(.22f, scale, .22f), "Wood");
            GameObject canopy = MakePrimitive(PrimitiveType.Sphere, "Pine Canopy", position + Vector3.up * (scale * 2.3f), new Vector3(scale * 1.25f, scale * 1.8f, scale * 1.25f), "Island Grass");
            SetColor(canopy, new Color(.08f, .24f, .17f));
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
                courseCamera.transform.position = new Vector3(0f, 5.5f, -17f);
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
            sun.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
            RenderSettings.ambientLight = new Color(.42f, .53f, .61f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.48f, .65f, .69f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 170f;
            RenderSettings.fogEndDistance = 430f;
        }

        private void TakeDemoShot()
        {
            if (roundFinished) return;
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

            float miss = Vector2.Distance(new Vector2(landing.x, landing.z), new Vector2(target.x, target.z));
            float radius = new[] { 12f, 10f, 8f, 6f }[selectedDifficulty];
            lastScore = miss < radius * .12f ? 50 : miss < radius * .27f ? 20 : miss < radius * .48f ? 10 : miss < radius * .74f ? 5 : 0;
            totalScore += lastScore;
            status = lastScore > 0
                ? $"Landed {Mathf.RoundToInt(miss)}m from centre — +{lastScore} points!"
                : $"Missed the target by {Mathf.RoundToInt(miss)}m. Adjust power and aim.";
            if (shotNumber >= ShotsPerRound)
            {
                roundFinished = true;
                status = $"ROUND COMPLETE — {totalScore} points. Press NEW ROUND to play again.";
            }
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
            roundFinished = false;
            if (trajectory != null) trajectory.enabled = false;
            ballPosition = new Vector3(0f, .55f, -3f);
            if (ballObject != null) ballObject.transform.position = ballPosition;
            status = "New round ready. Good luck!";
        }

        private void OnGUI()
        {
            GUI.depth = -10;
            float width = Mathf.Min(410f, Screen.width * .33f);
            Rect panel = new Rect(18f, 18f, width, Screen.height - 36f);
            DrawPanel(panel, new Color(.025f, .055f, .075f, .94f));

            GUILayout.BeginArea(new Rect(panel.x + 18, panel.y + 12, panel.width - 36, panel.height - 24));
            GUILayout.Label("NIMAVELLE  /  GOLF", HeaderStyle(18));
            GUILayout.Label("TARGET RINGS", HeaderStyle(29, new Color(.2f, .85f, 1f)));
            GUILayout.Label("TROPICAL MOUNTAIN LAKE  •  1–8 PLAYERS", SmallStyle());
            GUILayout.Space(14);

            GUILayout.Label("PLAYERS", SectionStyle());
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("−", GUILayout.Width(42), GUILayout.Height(32))) playerCount = Mathf.Max(1, playerCount - 1);
            GUILayout.Label(playerCount + (playerCount == 1 ? " PLAYER" : " PLAYERS"), HeaderStyle(16), GUILayout.Height(32));
            if (GUILayout.Button("+", GUILayout.Width(42), GUILayout.Height(32))) playerCount = Mathf.Min(8, playerCount + 1);
            GUILayout.EndHorizontal();
            GUILayout.Space(12);

            GUILayout.Label("SELECT TARGET", SectionStyle());
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(Mathf.Min(220, panel.height * .30f)));
            for (int i = 0; i < distances.Length; i++)
            {
                GUI.backgroundColor = selectedTarget == i ? targetColours[i] : new Color(.08f, .14f, .17f);
                if (GUILayout.Button($"  {i + 1}.  {distances[i]} METRES     {(selectedTarget == i ? "SELECTED" : "TARGET ISLAND")}", GUILayout.Height(34)))
                {
                    selectedTarget = i;
                    power = Mathf.Clamp((float)distances[i] / 250f + .08f, .35f, 1f);
                }
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndScrollView();
            GUILayout.Space(10);

            GUILayout.Label("DIFFICULTY", SectionStyle());
            GUILayout.BeginHorizontal();
            string[] modes = { "BEGINNER", "INTERMEDIATE", "ADVANCED", "PRO" };
            for (int i = 0; i < modes.Length; i++)
            {
                GUI.backgroundColor = selectedDifficulty == i ? targetColours[i] : new Color(.1f, .16f, .19f);
                if (GUILayout.Button(modes[i], GUILayout.Height(34))) selectedDifficulty = i;
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            GUILayout.Label("SHOT POWER", SectionStyle());
            power = GUILayout.HorizontalSlider(power, .2f, 1.08f, GUILayout.Height(24));
            GUILayout.Label(Mathf.RoundToInt(power * distances[selectedTarget]) + "m estimated carry", SmallStyle());
            GUILayout.Label("AIM CONTROL", SectionStyle());
            aim = GUILayout.HorizontalSlider(aim, 0f, 1f, GUILayout.Height(24));
            GUILayout.Label(aim < .35f ? "Left bias" : aim > .65f ? "Right bias" : "Centre aim", SmallStyle());
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("HIT SHOT  [SPACE]", GUILayout.Height(46))) TakeDemoShot();
            if (GUILayout.Button(aerialView ? "PLAY VIEW  [TAB]" : "AERIAL VIEW  [TAB]", GUILayout.Height(46))) ToggleCamera();
            GUILayout.EndHorizontal();
            if (roundFinished && GUILayout.Button("NEW ROUND", GUILayout.Height(38))) StartNewRound();
            GUILayout.Space(12);

            GUILayout.Label("ROUND HUD", SectionStyle());
            GUILayout.BeginHorizontal();
            DrawStat("SHOT", $"{Mathf.Min(shotNumber, ShotsPerRound)}/{ShotsPerRound}");
            DrawStat("LAST SHOT", $"+{lastScore}");
            DrawStat("TOTAL", totalScore.ToString());
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            GUILayout.Label(status, WordWrapStyle());
            GUILayout.Space(8);
            GUILayout.Label("SCORING", SectionStyle());
            GUILayout.Label("OUTER  5    •    MIDDLE  10", SmallStyle());
            GUILayout.Label("INNER  20    •    BULLSEYE  50", SmallStyle());
            GUILayout.Label("Demo shot input is currently simulated. Connect the approved launch-monitor adapter for real ball data.", TinyStyle());
            GUILayout.EndArea();
        }

        private void DrawStat(string label, string value)
        {
            GUILayout.BeginVertical(GUILayout.Width(105));
            GUILayout.Label(label, TinyStyle());
            GUILayout.Label(value, HeaderStyle(20, new Color(.2f, .85f, 1f)));
            GUILayout.EndVertical();
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

        private GUIStyle SmallStyle()
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, normal = { textColor = new Color(.65f, .78f, .82f) },
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