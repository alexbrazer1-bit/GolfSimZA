using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace GolfSimZA.Visual
{
    /// <summary>
    /// Light for an imported course. The course files are made to be lit by the program they were
    /// built for, so on their own every course has flat, even "ambient" light (no sky light from
    /// above, no darker undersides) and reflection probes whose pictures were never baked (black
    /// reflections on water, glass and wet surfaces). This gives the course:
    /// * ambient light from its own sky (blue from above, darker below),
    /// * a sky reflection probe so water, greens and glass reflect the sky,
    /// * the course's empty (never baked) reflection probes switched off.
    /// </summary>
    public static class CourseLighting
    {
        public static string Apply(Scene courseScene)
        {
            int emptyProbes = 0, probes = 0;
            foreach (GameObject root in courseScene.GetRootGameObjects())
                foreach (ReflectionProbe p in root.GetComponentsInChildren<ReflectionProbe>(true))
                {
                    probes++;
                    bool empty = (p.mode == ReflectionProbeMode.Baked && p.bakedTexture == null) ||
                                 (p.mode == ReflectionProbeMode.Custom && p.customBakedTexture == null);
                    if (empty && p.enabled) { p.enabled = false; emptyProbes++; }
                }

            string ambient;
            if (RenderSettings.skybox != null)
            {
                RenderSettings.ambientMode = AmbientMode.Skybox;
                RenderSettings.ambientIntensity = Mathf.Clamp(RenderSettings.ambientIntensity, 1f, 1.3f);
                ambient = "from the sky";
            }
            else
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.86f);
                RenderSettings.ambientEquatorColor = new Color(0.52f, 0.58f, 0.52f);
                RenderSettings.ambientGroundColor = new Color(0.28f, 0.30f, 0.22f);
                ambient = "sky / ground gradient";
            }
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1f;
            DynamicGI.UpdateEnvironment();

            // Sky reflections everywhere (the course's own probes, where they work, take over close by).
            var go = new GameObject("GolfSimZA_SkyReflection");
            SceneManager.MoveGameObjectToScene(go, courseScene);
            ReflectionProbe probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            probe.resolution = 128;
            probe.size = new Vector3(100000f, 20000f, 100000f);
            probe.importance = 0;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.cullingMask = 0; // the sky only: cheap, and no course object hides it
            probe.hdr = true;
            probe.RenderProbe();

            return $"ambient light {ambient}, sky reflections on, {emptyProbes} of {probes} course reflection probes were empty and are off";
        }
    }
}
