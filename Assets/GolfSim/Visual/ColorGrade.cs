using GolfSimZA.Core;
using UnityEngine;

namespace GolfSimZA.Visual
{
    /// <summary>
    /// COLOUR BOOST (Settings → VISUAL SETTINGS → COURSE LOOK): screen colour grading that
    /// makes fairways and rough a richer, lusher green and the sky bluer.
    /// OFF / NATURAL / LUSH (like a sunny summer course photo) / VIVID.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ColorGrade : MonoBehaviour
    {
        public static readonly string[] PresetNames = { "OFF", "NATURAL", "LUSH", "VIVID" };

        private Material material;
        private int preset = -1;

        private static readonly int Saturation = Shader.PropertyToID("_Saturation");
        private static readonly int Vibrance = Shader.PropertyToID("_Vibrance");
        private static readonly int Contrast = Shader.PropertyToID("_Contrast");
        private static readonly int Brightness = Shader.PropertyToID("_Brightness");
        private static readonly int GreenBoost = Shader.PropertyToID("_GreenBoost");
        private static readonly int GreenShift = Shader.PropertyToID("_GreenShift");
        private static readonly int GreenLift = Shader.PropertyToID("_GreenLift");
        private static readonly int BlueBoost = Shader.PropertyToID("_BlueBoost");

        /// <summary>Puts the effect on the main camera (once).</summary>
        public static void Attach(Camera camera)
        {
            if (camera != null && camera.GetComponent<ColorGrade>() == null) camera.gameObject.AddComponent<ColorGrade>();
        }

        private void OnEnable()
        {
            AppSettings.Changed += Apply;
            Apply();
        }

        private void OnDisable() => AppSettings.Changed -= Apply;

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }

        private void Apply()
        {
            preset = Mathf.Clamp(AppSettings.Current.colourBoost, 0, 3);
            if (preset == 0) return;
            if (material == null)
            {
                Shader shader = Resources.Load<Shader>("GolfSimZA_ColorGrade");
                if (shader == null || !shader.isSupported)
                {
                    Debug.LogWarning("[GolfSimZA] Colour boost shader not available on this PC.");
                    preset = 0;
                    return;
                }
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            //                 sat   vib   contrast bright  greenSat shift   greenLift blue
            // Tuned against a summer course photo: deep, saturated fairway / rough greens (a touch
            // darker, not lime), clean blue sky, a little more contrast.
            float[] natural = { 1.06f, 0.15f, 1.06f, 0.99f, 1.30f, -0.008f, 0.94f, 1.10f };
            float[] lush = { 1.10f, 0.28f, 1.12f, 0.98f, 1.60f, -0.012f, 0.86f, 1.22f };
            float[] vivid = { 1.18f, 0.40f, 1.15f, 0.98f, 1.90f, -0.015f, 0.84f, 1.32f };
            float[] p = preset == 1 ? natural : preset == 2 ? lush : vivid;
            material.SetFloat(Saturation, p[0]);
            material.SetFloat(Vibrance, p[1]);
            material.SetFloat(Contrast, p[2]);
            material.SetFloat(Brightness, p[3]);
            material.SetFloat(GreenBoost, p[4]);
            material.SetFloat(GreenShift, p[5]);
            material.SetFloat(GreenLift, p[6]);
            material.SetFloat(BlueBoost, p[7]);
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (preset <= 0 || material == null)
            {
                Graphics.Blit(source, destination);
                return;
            }
            Graphics.Blit(source, destination, material);
        }
    }
}
