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
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "DrivingRange_Ground";
            ground.transform.localScale = new Vector3(20f, 1f, 20f);

            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "GolfBall";
            ball.transform.position = new Vector3(0f, 0.04f, 0f);
            ball.transform.localScale = Vector3.one * 0.04267f;

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

            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 2.2f, -5.5f);
            cameraObject.transform.rotation = Quaternion.Euler(14f, 0f, 0f);
            camera.fieldOfView = 55f;

            GameObject lightObject = new GameObject("Sun");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            string directory = "Assets/Scenes";
            if (!AssetDatabase.IsValidFolder(directory))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/GolfSimZA_0_1_2_DrivingRange.unity");
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/GolfSimZA_0_1_2_DrivingRange.unity", true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GolfSimZA] Phase 0.1.2 driving range created. Press Play, select a club with 1-8, then press Space.");
        }
    }
}
#endif
