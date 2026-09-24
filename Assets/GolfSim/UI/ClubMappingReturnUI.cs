using System.Collections.Generic;
using System.Reflection;
using GolfSimZA.Players;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfSimZA.UI
{
    public sealed class ClubMappingReturnUI : MonoBehaviour
    {
        private const string ReturnPlayerKey = "GolfSimZA.MapReturnPlayer";
        private bool handled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<ClubMappingReturnUI>() != null) return;
            GameObject go = new GameObject("GolfSimZA_ClubMappingReturnUI");
            DontDestroyOnLoad(go);
            go.AddComponent<ClubMappingReturnUI>();
        }

        private void Update()
        {
            if (handled || SceneManager.GetActiveScene().name != "GolfSimZA_0_5_Players") return;
            string returnPlayer = PlayerPrefs.GetString(ReturnPlayerKey, "");
            if (string.IsNullOrWhiteSpace(returnPlayer)) return;

            PlayerSetupUI setup = FindFirstObjectByType<PlayerSetupUI>();
            if (setup == null) return;

            System.Type type = typeof(PlayerSetupUI);
            FieldInfo playersField = type.GetField("players", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo selectedField = type.GetField("selectedPlayerIndex", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo openBag = type.GetMethod("OpenBagEditor", BindingFlags.Instance | BindingFlags.NonPublic);
            if (playersField == null || selectedField == null || openBag == null) return;

            List<string> players = playersField.GetValue(setup) as List<string>;
            if (players == null) return;

            int index = players.FindIndex(p => string.Equals(p, returnPlayer, System.StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                if (players.Count == 1 && string.Equals(players[0], "Player 1", System.StringComparison.OrdinalIgnoreCase))
                    players[0] = returnPlayer;
                else
                    return;
                index = 0;
            }

            selectedField.SetValue(setup, index);
            PlayerPrefs.DeleteKey(ReturnPlayerKey);
            PlayerPrefs.Save();
            handled = true;
            openBag.Invoke(setup, null);
        }
    }
}
