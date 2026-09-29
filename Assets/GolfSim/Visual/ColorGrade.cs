using GolfSimZA.Core;
using UnityEngine;

namespace GolfSimZA.Visual
{
    /// <summary>
    /// COLOUR BOOST (Settings → VISUAL SETTINGS → COURSE LOOK): screen colour grading that
    /// makes fairways and rough a richer, lusher green and the sky bluer.
    /// OFF / NATURAL / LUSH (like a sunny summer course photo) / VIVID. Also does the filmic tone
    /// mapping (PHOTO LIGHTING) and the mild sharpening / vignette (CRISP PICTURE).
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
        private static readonly int Exposure = Shader.PropertyToID("_Exposure");
        private static readonly int Tonemap = Shader.PropertyToID("_Tonemap");
        private static readonly int Sharpen = Shader.PropertyToID("_Sharpen");
        private static readonly int Vignette = Shader.PropertyToID("_Vignette");
        private bool active;

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
            AppSettings st = AppSettings.Current;
            preset = Mathf.Clamp(st.colourBoost, 0, 3);
            GraphicsQuality.ApplyCamera(GetComponent<Camera>());
            active = preset > 0 || st.photoLighting || st.sharpen;
            if (!active) return;
            if (material == null)
            {
                Shader shader = Resources.Load<Shader>("GolfSimZA_ColorGrade");
                if (shader == null || !shader.isSupported)
                {
                    Debug.LogWarning("[GolfSimZA] Colour boost shader not available on this PC.");
                    active = false;
                    return;
                }
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            //                 sat   vib   contrast bright  greenSat shift   greenLift blue
            // Tuned in linear colour space with photo lighting: the course textures already carry
            // their colour, so the boost is gentle (NATURAL ~ untouched, VIVID ~ TV-broadcast greens).
            float[] natural = { 1.02f, 0.05f, 1.03f, 1.00f, 1.08f, -0.003f, 0.98f, 1.05f };
            float[] lush = { 1.04f, 0.10f, 1.05f, 1.00f, 1.16f, -0.006f, 0.95f, 1.10f };
            float[] vivid = { 1.08f, 0.18f, 1.08f, 0.99f, 1.28f, -0.010f, 0.92f, 1.18f };
            float[] off = { 1f, 0f, 1f, 1f, 1f, 0f, 1f, 1f };
            float[] p = preset == 0 ? off : preset == 1 ? natural : preset == 2 ? lush : vivid;
            material.SetFloat(Saturation, p[0]);
            material.SetFloat(Vibrance, p[1]);
            material.SetFloat(Contrast, p[2]);
            material.SetFloat(Brightness, p[3]);
            material.SetFloat(GreenBoost, p[4]);
            material.SetFloat(GreenShift, p[5]);
            material.SetFloat(GreenLift, p[6]);
            material.SetFloat(BlueBoost, p[7]);
            // HDR photo lighting: filmic curve with a little extra exposure so midtones stay bright.
            material.SetFloat(Tonemap, st.photoLighting ? 1f : 0f);
            material.SetFloat(Exposure, st.photoLighting ? 1.25f : 1f);
            material.SetFloat(Sharpen, st.sharpen ? 0.35f : 0f);
            material.SetFloat(Vignette, st.sharpen ? 0.22f : 0f);
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!active || material == null)
            {
                Graphics.Blit(source, destination);
                return;
            }
            Graphics.Blit(source, destination, material);
        }
    }
}
