#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GolfSimZA.Editor
{
    /// <summary>
    /// Keeps the GolfSimZA editor menu aligned with the implemented project stages.
    /// The later stages are feature updates to the existing 0.5/0.6 flow, so these
    /// commands rebuild the required local scenes with the current code and open the
    /// relevant screen for testing.
    /// </summary>
    public static class GolfSimStageMenu
    {
        private const string PlayersScene = "Assets/Scenes/GolfSimZA_0_5_Players.unity";
        private const string PlayScene = "Assets/Scenes/GolfSimZA_0_6_PlayRound.unity";

        [MenuItem("GolfSimZA/Create 0.7 Realistic Gameplay HUD")]
        public static void Create07() => PreparePlayRound("0.7");

        [MenuItem("GolfSimZA/Create 0.8 Landing & Roll Physics")]
        public static void Create08() => PreparePlayRound("0.8");

        [MenuItem("GolfSimZA/Create 0.9 Golf Bag & Club Mapping")]
        public static void Create09() => PreparePlayers("0.9");

        [MenuItem("GolfSimZA/Create 0.9.2 Map My Bag")]
        public static void Create092() => PreparePlayers("0.9.2");

        [MenuItem("GolfSimZA/Create 0.9.3 Six-Shot Club Mapping")]
        public static void Create093() => PreparePlayers("0.9.3");

        [MenuItem("GolfSimZA/Create 0.9.4 Bag Distance Advisor")]
        public static void Create094() => PreparePlayRound("0.9.4");

        [MenuItem("GolfSimZA/Create 0.9.5 Mapping Results")]
        public static void Create095() => PreparePlayers("0.9.5");

        [MenuItem("GolfSimZA/Build Current 0.9.5")]
        public static void BuildCurrent095()
        {
            CreateCourseRoundFlow.CreateFlow();
            CreatePrototypeScene.CreatePlayRound();
            if (System.IO.File.Exists(PlayersScene))
                EditorSceneManager.OpenScene(PlayersScene, OpenSceneMode.Single);
            Debug.Log("[GolfSimZA] Current 0.9.5 build prepared: Course → Round Settings → Players → Play Round.");
        }

        private static void PreparePlayers(string stage)
        {
            CreateCourseRoundFlow.CreateFlow();
            if (System.IO.File.Exists(PlayersScene))
            {
                EditorSceneManager.OpenScene(PlayersScene, OpenSceneMode.Single);
                Debug.Log("[GolfSimZA] Stage " + stage + " ready. Players screen opened for testing.");
            }
        }

        private static void PreparePlayRound(string stage)
        {
            CreatePrototypeScene.CreatePlayRound();
            if (System.IO.File.Exists(PlayScene))
            {
                EditorSceneManager.OpenScene(PlayScene, OpenSceneMode.Single);
                Debug.Log("[GolfSimZA] Stage " + stage + " ready. Current Play Round scene opened for testing.");
            }
        }
    }
}
#endif
