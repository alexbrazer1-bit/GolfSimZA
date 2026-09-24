#if UNITY_EDITOR
using GolfSimZA.Core;
using GolfSimZA.LaunchMonitors;
using GolfSimZA.LaunchMonitors.GarminR10;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GolfSimZA.Editor
{
    public static class CreatePlayableR10
    {
        private const string ScenePath = "Assets/Scenes/GolfSimZA_0_6_PlayRound.unity";

        [MenuItem("GolfSimZA/Create 1.0 Playable Garmin R10 Simulator")]
        public static void Create()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                CreatePrototypeScene.CreatePlayRound();
                if (!System.IO.File.Exists(ScenePath))
                {
                    Debug.LogError("[GolfSimZA] Could not create the 0.6 Play Round scene first.");
                    return;
                }
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SimulatorController controller = Object.FindFirstObjectByType<SimulatorController>();
            TestShotProvider test = Object.FindFirstObjectByType<TestShotProvider>();
            if (controller == null || test == null)
            {
                Debug.LogError("[GolfSimZA] Play Round scene is missing the simulator controller or test provider.");
                return;
            }

            GameObject host = test.gameObject;
            GarminR10Adapter r10 = host.GetComponent<GarminR10Adapter>();
            if (r10 == null) r10 = host.AddComponent<GarminR10Adapter>();

            LaunchMonitorRouter router = host.GetComponent<LaunchMonitorRouter>();
            if (router == null) router = host.AddComponent<LaunchMonitorRouter>();

            SerializedObject routerSo = new SerializedObject(router);
            routerSo.FindProperty("realMonitorBehaviour").objectReferenceValue = r10;
            routerSo.FindProperty("testMonitorBehaviour").objectReferenceValue = test;
            routerSo.FindProperty("allowKeyboardTestShots").boolValue = true;
            routerSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("launchMonitorBehaviour").objectReferenceValue = router;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/GolfSimZA_0_5_CourseSelection.unity", true),
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene("Assets/Scenes/GolfSimZA_0_4_SimulatorPresentationRange.unity", true)
            };

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GolfSimZA] 1.0 Playable Garmin R10 Simulator ready. R10 OpenConnect receiver: 127.0.0.1:921. Keyboard test mode remains available with 1-8 + SPACE.");
        }
    }
}
#endif
