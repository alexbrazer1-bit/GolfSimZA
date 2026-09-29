using UnityEditor;
using UnityEngine;

namespace GolfSimZA.EditorTools
{
    /// <summary>
    /// GSPro courses are authored in linear colour space. In gamma space their lighting, grass and
    /// sky look flat and washed out, so the project is switched to linear once (the builds use it
    /// too, see GolfSimRelease). Unity re-imports textures after the switch.
    /// </summary>
    [InitializeOnLoad]
    internal static class GolfSimLinearColour
    {
        static GolfSimLinearColour()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (PlayerSettings.colorSpace == ColorSpace.Linear) return;
                PlayerSettings.colorSpace = ColorSpace.Linear;
                AssetDatabase.SaveAssets();
                Debug.Log("[GolfSimZA] Project switched to Linear colour space (GSPro courses are made in linear).");
            };
        }
    }
}
