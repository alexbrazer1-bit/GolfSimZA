#if UNITY_EDITOR
using GolfSimZA.Core;
using GolfSimZA.LaunchMonitors;
using GolfSimZA.Physics;
using GolfSimZA.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfSimZA.Editor
{
    public static class CreatePrototypeScene
    {
        [MenuItem("GolfSimZA/Create 0.1.2 Driving Range")]
        public static void Create()
        {
            CreateScene(false, false);
        }

        [MenuItem("GolfSimZA/Create 0.2 Visual Driving Range")]
        public static void CreateVisualDrivingRange()
        {
            CreateScene(true, false);
        }

        [MenuItem("GolfSimZA/Create 0.4 Simulator Presentation Range")]
        public static void CreatePresentationDrivingRange()
        {
            CreateScene(true, true);
        }

        private static void CreateScene(bool visual, bool presentation)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "DrivingRange_Ground";
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
            if (visual)
                ApplyMaterial(ground, new Color(0.12f, 0.42f, 0.16f));

            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "GolfBall";
            ball.transform.position = new Vector3(0f, 0.04f, 0f);
            ball.transform.localScale = Vector3.one * 0.04267f;
            ApplyMaterial(ball, Color.white);

            GameObject range = new GameObject("GolfSimZA_Range");
            TestShotProvider provider = range.AddComponent<TestShotProvider>();

            BallFlightSimulator flight = range.AddComponent<BallFlightSimulator>();
            SerializedObject flightSo = new SerializedObject(flight);
            flightSo.FindProperty("ball").objectReferenceValue = ball.transform;
            flightSo.ApplyModifiedPropertiesWithoutUndo();

            SimulatorController controller = range.AddComponent<SimulatorController>();
            SerializedObject controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("launchMonitorBehaviour").objectReferenceValue = provider;
            controllerSo.FindProperty("ballFlightSimulator").objectReferenceValue = flight;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            SimulatorHUD hud = range.AddComponent<SimulatorHUD>();
            SerializedObject hudSo = new SerializedObject(hud);
            hudSo.FindProperty("simulatorController").objectReferenceValue = controller;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            if (visual)
            {
                ShotTracer tracer = range.AddComponent<ShotTracer>();
                SerializedObject tracerSo = new SerializedObject(tracer);
                tracerSo.FindProperty("ball").objectReferenceValue = ball.transform;
                tracerSo.ApplyModifiedPropertiesWithoutUndo();
                BuildVisualRange();
            }

            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = visual ? new Vector3(0f, 3.0f, -8.5f) : new Vector3(0f, 2.2f, -5.5f);
            cameraObject.transform.rotation = visual ? Quaternion.Euler(9f, 0f, 0f) : Quaternion.Euler(14f, 0f, 0f);
            camera.fieldOfView = visual ? 62f : 55f;
            if (visual)
            {
                camera.backgroundColor = new Color(0.52f, 0.75f, 0.95f);
                camera.clearFlags = CameraClearFlags.SolidColor;
            }

            if (presentation)
            {
                FlightPresentation presentationController = range.AddComponent<FlightPresentation>();
                SerializedObject presentationSo = new SerializedObject(presentationController);
                presentationSo.FindProperty("flight").objectReferenceValue = flight;
                presentationSo.FindProperty("ball").objectReferenceValue = ball.transform;
                presentationSo.FindProperty("followCamera").objectReferenceValue = camera;
                presentationSo.ApplyModifiedPropertiesWithoutUndo();
            }

            GameObject lightObject = new GameObject("Sun");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = visual ? 1.4f : 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            string directory = "Assets/Scenes";
            if (!AssetDatabase.IsValidFolder(directory))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            string scenePath = presentation
                ? "Assets/Scenes/GolfSimZA_0_4_SimulatorPresentationRange.unity"
                : visual
                    ? "Assets/Scenes/GolfSimZA_0_2_VisualDrivingRange.unity"
                    : "Assets/Scenes/GolfSimZA_0_1_2_DrivingRange.unity";

            EditorSceneManager.SaveScene(scene, scenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(scenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(presentation
                ? "[GolfSimZA] Phase 0.4 simulator presentation range created. Press Play, select a club with 1-8, then press Space."
                : visual
                    ? "[GolfSimZA] Phase 0.2 visual driving range created. Press Play, select a club with 1-8, then press Space."
                    : "[GolfSimZA] Phase 0.1.2 driving range created. Press Play, select a club with 1-8, then press Space.");
        }

        private static void BuildVisualRange()
        {
            CreateStrip("Fairway", new Vector3(0f, 0.012f, 42f), new Vector3(18f, 0.02f, 84f), new Color(0.20f, 0.55f, 0.20f));
            CreateStrip("TargetGreen_50m", new Vector3(0f, 0.025f, 50f), new Vector3(11f, 0.04f, 7f), new Color(0.25f, 0.62f, 0.24f));
            CreateStrip("TargetGreen_100m", new Vector3(0f, 0.025f, 100f), new Vector3(13f, 0.04f, 8f), new Color(0.25f, 0.62f, 0.24f));
            CreateStrip("TargetGreen_150m", new Vector3(0f, 0.025f, 150f), new Vector3(15f, 0.04f, 9f), new Color(0.25f, 0.62f, 0.24f));
            CreateStrip("TargetGreen_200m", new Vector3(0f, 0.025f, 200f), new Vector3(17f, 0.04f, 10f), new Color(0.25f, 0.62f, 0.24f));

            for (int distance = 25; distance <= 200; distance += 25)
                CreateDistanceMarker(distance);

            CreateFlag("Flag_100m", new Vector3(0f, 0.05f, 100f));
            CreateFlag("Flag_200m", new Vector3(0f, 0.05f, 200f));
        }

        private static void CreateDistanceMarker(int distance)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Distance_" + distance + "m";
            marker.transform.position = new Vector3(9.2f, 0.08f, distance);
            marker.transform.localScale = new Vector3(0.35f, 0.16f, 0.08f);
            ApplyMaterial(marker, new Color(0.95f, 0.88f, 0.18f));
        }

        private static void CreateFlag(string name, Vector3 position)
        {
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = name + "_Pole";
            pole.transform.position = position + Vector3.up * 1.25f;
            pole.transform.localScale = new Vector3(0.035f, 1.25f, 0.035f);
            ApplyMaterial(pole, Color.white);

            GameObject flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flag.name = name + "_Flag";
            flag.transform.position = position + new Vector3(0.45f, 2.1f, 0f);
            flag.transform.localScale = new Vector3(0.8f, 0.45f, 0.025f);
            ApplyMaterial(flag, new Color(0.9f, 0.08f, 0.08f));
        }

        private static void CreateStrip(string name, Vector3 position, Vector3 scale, Color color)
        {
            GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = name;
            strip.transform.position = position;
            strip.transform.localScale = scale;
            ApplyMaterial(strip, color);
        }

        private static void ApplyMaterial(GameObject obj, Color color)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer == null)
                return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                return;

            Material material = new Material(shader);
            material.color = color;
            renderer.sharedMaterial = material;
        }
    }
}
#endif
