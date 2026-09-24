#if UNITY_EDITOR
using GolfSimZA.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfSimZA.Editor
{
    public static class CreateCourseRoundFlow
    {
        private const string SceneDirectory = "Assets/Scenes";
        private const string CourseScene = "Assets/Scenes/GolfSimZA_0_5_CourseSelection.unity";
        private const string RoundScene = "Assets/Scenes/GolfSimZA_0_5_RoundSettings.unity";
        private const string PlayersScene = "Assets/Scenes/GolfSimZA_0_5_Players.unity";
        private const string PlayScene = "Assets/Scenes/GolfSimZA_0_6_PlayRound.unity";

        [MenuItem("GolfSimZA/Create 0.5 Course → Round → Players Flow")]
        public static void CreateFlow()
        {
            EnsureSceneDirectory();
            CreateCourseScene();
            CreateRoundScene();
            CreatePlayersScene();
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GolfSimZA] 0.5 flow created: Course List → Round Settings → Players → 0.6 Play Round.");
        }

        private static void CreateCourseScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            new GameObject("CourseSelection").AddComponent<CourseSelectionUI>();
            EditorSceneManager.SaveScene(scene, CourseScene);
        }

        private static void CreateRoundScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            new GameObject("RoundSettings").AddComponent<RoundSettingsUI>();
            EditorSceneManager.SaveScene(scene, RoundScene);
        }

        private static void CreatePlayersScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            new GameObject("PlayerSetup").AddComponent<PlayerSetupUI>();
            EditorSceneManager.SaveScene(scene, PlayersScene);
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.10f, 0.14f);
            camera.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void EnsureSceneDirectory()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(CourseScene, true),
                new EditorBuildSettingsScene(RoundScene, true),
                new EditorBuildSettingsScene(PlayersScene, true),
                new EditorBuildSettingsScene(PlayScene, true),
                new EditorBuildSettingsScene("Assets/Scenes/GolfSimZA_0_4_SimulatorPresentationRange.unity", true)
            };
        }
    }
}
#endif
