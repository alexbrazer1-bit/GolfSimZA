using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfSimZA.Core
{
    /// <summary>
    /// GolfSim ZA sounds (Settings → GAME → SOUNDS). All sounds are generated in code, so
    /// nothing is copied from another program:
    ///  * golf sounds – club strike (louder with club speed), landing thud, ball in the cup
    ///  * menu sounds – a soft click whenever a button, slider or text box is pressed
    ///  * ball ready  – a two-note chime when the next shot can be hit
    ///  * course sounds – the volume of ambient audio inside imported course scenes
    ///  * master – overall volume
    /// </summary>
    public sealed class GolfSimAudio : MonoBehaviour
    {
        private const int Rate = 44100;
        private static GolfSimAudio instance;

        private AudioSource source;
        private AudioClip strike, putt, land, cup, click, ready;
        private int lastHotControl;
        private readonly Dictionary<AudioSource, float> courseVolumes = new Dictionary<AudioSource, float>();

        public static GolfSimAudio Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("GolfSimZA_Audio");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<GolfSimAudio>();
                }
                return instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() => _ = Instance;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            // The one audio listener for every scene; it follows the main camera (LateUpdate).
            gameObject.AddComponent<AudioListener>();
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            strike = Make("strike", 0.18f, Strike);
            putt = Make("putt", 0.10f, Putt);
            land = Make("land", 0.22f, Land);
            cup = Make("cup", 0.45f, Cup);
            click = Make("click", 0.035f, Click);
            ready = Make("ready", 0.55f, Ready);

            ApplyVolumes();
            AppSettings.Changed += ApplyVolumes;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            AppSettings.Changed -= ApplyVolumes;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive) ApplyCourseVolume(scene);
            if (AudioListener.pause) AudioListener.pause = false;
        }

        public void ApplyVolumes()
        {
            AudioListener.volume = Mathf.Clamp01(AppSettings.Current.volumeMaster);
            foreach (KeyValuePair<AudioSource, float> pair in courseVolumes)
                if (pair.Key != null) pair.Key.volume = pair.Value * Mathf.Clamp01(AppSettings.Current.volumeCourse);
        }

        /// <summary>Scales every audio source in an imported course scene by the COURSE SOUNDS volume.</summary>
        public void ApplyCourseVolume(Scene scene)
        {
            if (!scene.IsValid()) return;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (AudioSource a in root.GetComponentsInChildren<AudioSource>(true))
                {
                    if (!courseVolumes.ContainsKey(a)) courseVolumes[a] = a.volume;
                    a.volume = courseVolumes[a] * Mathf.Clamp01(AppSettings.Current.volumeCourse);
                }
        }

        // ------------------------------------------------------------ Play

        /// <summary>Club strike; clubSpeedMps sets how loud and sharp it is.</summary>
        public static void PlayStrike(float ballSpeedMps, bool isPutt)
        {
            GolfSimAudio a = Instance;
            float loud = isPutt ? 0.45f : Mathf.Lerp(0.45f, 1f, Mathf.InverseLerp(20f, 75f, ballSpeedMps));
            a.Play(isPutt ? a.putt : a.strike, AppSettings.Current.volumeGolf * loud, isPutt ? 1f : Mathf.Lerp(0.9f, 1.1f, Mathf.InverseLerp(20f, 75f, ballSpeedMps)));
        }

        public static void PlayLanding(float impactSpeed)
        {
            GolfSimAudio a = Instance;
            a.Play(a.land, AppSettings.Current.volumeGolf * Mathf.Lerp(0.25f, 0.8f, Mathf.InverseLerp(5f, 40f, impactSpeed)), 1f);
        }

        public static void PlayCup() { GolfSimAudio a = Instance; a.Play(a.cup, AppSettings.Current.volumeGolf, 1f); }

        public static void PlayReady()
        {
            if (!AppSettings.Current.ballReadySound) return;
            GolfSimAudio a = Instance;
            a.Play(a.ready, AppSettings.Current.volumeGolf * 0.6f, 1f);
        }

        private void Play(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || source == null || volume <= 0.001f) return;
            source.pitch = pitch;
            source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam != null) transform.SetPositionAndRotation(cam.transform.position, cam.transform.rotation);

            // Menu click: an IMGUI control (button, slider, text box) was just pressed.
            int hot = GUIUtility.hotControl;
            if (hot != 0 && lastHotControl == 0 && AppSettings.Current.volumeMenu > 0.001f)
                Play(click, AppSettings.Current.volumeMenu * 0.5f, 1f);
            lastHotControl = hot;
        }

        // ------------------------------------------------------------ Synthesis

        private delegate float Wave(float t, float duration, System.Random random);

        private static AudioClip Make(string name, float seconds, Wave wave)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            var random = new System.Random(name.GetHashCode());
            float peak = 0.0001f;
            for (int i = 0; i < n; i++)
            {
                data[i] = wave(i / (float)Rate, seconds, random);
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.9f;
            AudioClip clip = AudioClip.Create("GolfSimZA_" + name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

        // Sharp crack + short metallic ring.
        private static float Strike(float t, float d, System.Random r)
        {
            float crack = Noise(r) * Mathf.Exp(-t * 90f);
            float ring = Mathf.Sin(2f * Mathf.PI * 2350f * t) * 0.35f * Mathf.Exp(-t * 38f)
                       + Mathf.Sin(2f * Mathf.PI * 3900f * t) * 0.18f * Mathf.Exp(-t * 55f);
            float body = Mathf.Sin(2f * Mathf.PI * 520f * t) * 0.4f * Mathf.Exp(-t * 45f);
            return crack + ring + body;
        }

        // Soft "tock".
        private static float Putt(float t, float d, System.Random r)
        {
            return Mathf.Sin(2f * Mathf.PI * 1250f * t) * Mathf.Exp(-t * 70f) + Noise(r) * 0.25f * Mathf.Exp(-t * 160f);
        }

        // Low thud on grass.
        private static float Land(float t, float d, System.Random r)
        {
            float thump = Mathf.Sin(2f * Mathf.PI * (140f - 60f * t / d) * t) * Mathf.Exp(-t * 22f);
            return thump + Noise(r) * 0.3f * Mathf.Exp(-t * 35f);
        }

        // Ball rattling into the cup: three plastic knocks.
        private static float Cup(float t, float d, System.Random r)
        {
            float v = 0f;
            float[] hits = { 0f, 0.11f, 0.19f };
            float[] level = { 1f, 0.6f, 0.35f };
            for (int i = 0; i < hits.Length; i++)
            {
                float u = t - hits[i];
                if (u < 0f) continue;
                v += level[i] * (Mathf.Sin(2f * Mathf.PI * 820f * u) * 0.8f + Noise(r) * 0.4f) * Mathf.Exp(-u * 60f);
            }
            return v;
        }

        private static float Click(float t, float d, System.Random r)
        {
            return Mathf.Sin(2f * Mathf.PI * 1800f * t) * Mathf.Exp(-t * 180f);
        }

        // Two soft notes (G5, C6).
        private static float Ready(float t, float d, System.Random r)
        {
            float a = Mathf.Sin(2f * Mathf.PI * 784f * t) * Mathf.Exp(-t * 6f) * (t < 0.2f ? 1f : 0.3f);
            float u = t - 0.16f;
            float b = u > 0f ? Mathf.Sin(2f * Mathf.PI * 1047f * u) * Mathf.Exp(-u * 5f) : 0f;
            return (a + b) * Mathf.Min(1f, t * 400f);
        }
    }
}
