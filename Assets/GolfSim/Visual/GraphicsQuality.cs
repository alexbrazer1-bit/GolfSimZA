using GolfSimZA.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfSimZA.Visual
{
    /// <summary>
    /// GRAPHICS QUALITY (Settings → VISUAL SETTINGS → COURSE LOOK): sharp long-distance shadows,
    /// full-resolution textures, anisotropic filtering (fairway stripes stay crisp far away),
    /// edge smoothing and the sun / sky lighting on courses. Applied at start-up, after every
    /// settings change and whenever a course or the range is opened.
    /// </summary>
    public static class GraphicsQuality
    {
        public static readonly string[] Names = { "LOW", "MEDIUM", "HIGH", "ULTRA" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            AppSettings.Changed -= Apply;
            AppSettings.Changed += Apply;
            Apply();
        }

        public static void Apply()
        {
            int q = Mathf.Clamp(AppSettings.Current.graphicsQuality, 0, 3);
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.shadowResolution = q >= 3 ? ShadowResolution.VeryHigh : q == 2 ? ShadowResolution.High : ShadowResolution.Medium;
            QualitySettings.shadowDistance = q >= 3 ? 320f : q == 2 ? 220f : q == 1 ? 150f : 100f;
            QualitySettings.shadowCascades = q >= 1 ? 4 : 2;
            // Sharp shadows close to the golfer, softer further away.
            QualitySettings.shadowCascade4Split = new Vector3(0.04f, 0.13f, 0.38f);
            QualitySettings.shadowCascade2Split = 0.2f;
            QualitySettings.anisotropicFiltering = q >= 1 ? AnisotropicFiltering.ForceEnable : AnisotropicFiltering.Enable;
            Texture.SetGlobalAnisotropicFilteringLimits(q >= 3 ? 16 : q == 2 ? 8 : 4, 16);
            QualitySettings.globalTextureMipmapLimit = q == 0 ? 1 : 0;
            QualitySettings.antiAliasing = q >= 3 ? 8 : q == 2 ? 4 : q == 1 ? 2 : 0;
            QualitySettings.lodBias = q >= 3 ? 2f : q == 2 ? 1.5f : 1.2f;
            QualitySettings.maximumLODLevel = 0;
            QualitySettings.softParticles = q >= 2;
            QualitySettings.skinWeights = SkinWeights.FourBones;
            QualitySettings.realtimeReflectionProbes = q >= 2;
            QualitySettings.billboardsFaceCameraPosition = true;
            QualitySettings.pixelLightCount = q >= 2 ? 4 : 2;
            QualitySettings.terrainQualityOverrides = TerrainQualityOverrides.None;
            QualitySettings.vSyncCount = 1;

            Camera cam = Camera.main;
            if (cam != null) ApplyCamera(cam);
        }

        /// <summary>HDR + MSAA on the play camera (tone mapping needs HDR).</summary>
        public static void ApplyCamera(Camera cam)
        {
            if (cam == null) return;
            cam.allowHDR = AppSettings.Current.photoLighting;
            cam.allowMSAA = QualitySettings.antiAliasing > 0;
        }

        /// <summary>
        /// Sun and sky light on a freshly loaded course or the range: soft, strong sun shadows and
        /// ambient light taken from the sky so shaded sides of trees and slopes are not black or grey.
        /// </summary>
        public static void ApplySceneLighting(Camera cam)
        {
            Apply();
            ApplyCamera(cam);
            Light sun = RenderSettings.sun;
            if (sun == null || !sun.isActiveAndEnabled)
                foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional && l.isActiveAndEnabled && (sun == null || l.intensity > sun.intensity)) sun = l;
            if (sun != null)
            {
                RenderSettings.sun = sun;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = Mathf.Max(sun.shadowStrength, 0.85f);
                sun.shadowBias = 0.03f;
                sun.shadowNormalBias = 0.25f;
                sun.shadowResolution = LightShadowResolution.FromQualitySettings;
            }
            if (RenderSettings.skybox != null && RenderSettings.ambientMode == AmbientMode.Skybox)
            {
                RenderSettings.ambientIntensity = Mathf.Max(RenderSettings.ambientIntensity, 1f);
                DynamicGI.UpdateEnvironment();
            }
            else if (RenderSettings.ambientMode == AmbientMode.Flat && RenderSettings.ambientLight.maxColorComponent < 0.25f)
            {
                // Some course files have almost no ambient light: shadows go black. Use a sky / ground gradient.
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.86f);
                RenderSettings.ambientEquatorColor = new Color(0.52f, 0.58f, 0.52f);
                RenderSettings.ambientGroundColor = new Color(0.28f, 0.30f, 0.22f);
            }
        }
    }
}
