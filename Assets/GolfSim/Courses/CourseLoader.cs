using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfSimZA.Courses
{
    /// <summary>
    /// Loads the imported course selected for the round into the Play Round scene.
    /// Added automatically by RoundGameplayUI. When no imported course is selected
    /// (demo course or range) it does nothing.
    /// </summary>
    public sealed class CourseLoader : MonoBehaviour
    {
        private static readonly string[] DemoObjects = { "CourseGround", "Fairway", "HoleGreen", "GolfSimZA_DrivingRange" };

        private AssetBundle bundle;
        private Scene courseScene;
        private GolfSimZA.Courses.Osm.OsmCourseScene builtCourse;

        public CourseDefinition Course { get; private set; }
        public bool IsLoading { get; private set; }
        public bool IsLoaded { get; private set; }
        public float Progress { get; private set; }
        public string StatusText { get; private set; } = "";
        public string Error { get; private set; }

        public event Action<CourseDefinition> Loaded;
        public event Action<string> Failed;

        private IEnumerator Start()
        {
            // Map My Bag always runs on the flat range, never on a course.
            if (!CourseSession.IsImportedCourse || GolfSimZA.Players.ClubMappingSession.IsActive)
                yield break;

            CourseDefinition course = CourseLibrary.Get(CourseSession.CourseId);
            if (course == null)
            {
                Fail("The selected course is no longer in the course library. Import it again.");
                yield break;
            }

            if (!CourseLibrary.IsAvailable(course))
            {
                Fail(course.IsOpenStreetMap
                    ? "The course's ground files are missing. Build it again under IMPORT COURSES → OPENSTREETMAP."
                    : $"Course file not found:\n{course.bundleFile}\nCopy the course folder back to that location or import it again.");
                yield break;
            }

            if (course.IsOpenStreetMap)
            {
                yield return LoadBuiltCourse(course);
                yield break;
            }

            Course = course;
            IsLoading = true;
            StatusText = "Opening course file...";
            ShaderRepair.CacheBuiltinShaders();

            // A previous round may have left this course file open.
            foreach (AssetBundle open in AssetBundle.GetAllLoadedAssetBundles())
                if (open != null && string.Equals(open.name, Path.GetFileName(course.bundleFile), StringComparison.OrdinalIgnoreCase))
                    open.Unload(true);

            AssetBundleCreateRequest bundleRequest;
            try
            {
                bundleRequest = AssetBundle.LoadFromFileAsync(course.bundleFile);
            }
            catch (Exception ex)
            {
                Fail("Could not open the course file: " + ex.Message);
                yield break;
            }

            while (!bundleRequest.isDone)
            {
                Progress = bundleRequest.progress * 0.4f;
                yield return null;
            }

            bundle = bundleRequest.assetBundle;
            if (bundle == null)
            {
                Fail("This course file could not be opened by GolfSimZA. It may be built with a Unity version that is too old, or be damaged.");
                yield break;
            }

            string[] scenes = bundle.GetAllScenePaths();
            if (scenes == null || scenes.Length == 0)
            {
                Fail("The course file does not contain a course scene.");
                yield break;
            }

            StatusText = "Loading course...";
            string scenePath = scenes[0];
            AsyncOperation sceneRequest;
            try
            {
                sceneRequest = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            }
            catch (Exception ex)
            {
                Fail("Could not load the course scene: " + ex.Message);
                yield break;
            }

            if (sceneRequest == null)
            {
                Fail("Could not load the course scene.");
                yield break;
            }

            while (!sceneRequest.isDone)
            {
                Progress = 0.4f + sceneRequest.progress * 0.5f;
                yield return null;
            }

            courseScene = SceneManager.GetSceneByPath(scenePath);
            if (!courseScene.IsValid())
                courseScene = SceneManager.GetSceneByName(Path.GetFileNameWithoutExtension(scenePath));
            if (!courseScene.IsValid() || !courseScene.isLoaded)
            {
                Fail("The course scene did not load.");
                yield break;
            }

            StatusText = "Preparing course...";
            Progress = 0.95f;
            yield return null;

            PrepareCourseScene();
            UnityEngine.Physics.SyncTransforms();

            IsLoading = false;
            IsLoaded = true;
            Progress = 1f;
            StatusText = "";
            Debug.Log($"[GolfSimZA] Course '{course.name}' loaded from {course.bundleFile}");
            Loaded?.Invoke(course);
        }

        /// <summary>A course GolfSimZA built from OpenStreetMap: the 3D course is made from its saved files.</summary>
        private IEnumerator LoadBuiltCourse(CourseDefinition course)
        {
            Course = course;
            IsLoading = true;
            StatusText = "Building the course...";
            builtCourse = new GolfSimZA.Courses.Osm.OsmCourseScene();
            yield return builtCourse.Build(course, (status, progress) => { StatusText = status; Progress = progress * 0.95f; });
            if (builtCourse.Error != null)
            {
                Fail(builtCourse.Error);
                yield break;
            }
            courseScene = builtCourse.Scene;

            StatusText = "Preparing course...";
            Progress = 0.96f;
            yield return null;
            Camera ourCamera = Camera.main;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                foreach (string demo in DemoObjects)
                    if (root.name == demo) root.SetActive(false);
            if (ourCamera != null)
            {
                ourCamera.farClipPlane = Mathf.Clamp(GolfSimZA.Core.AppSettings.Current.drawDistanceMeters, 500f, 15000f);
                ourCamera.nearClipPlane = Mathf.Min(ourCamera.nearClipPlane, 0.1f);
                if (RenderSettings.skybox != null) ourCamera.clearFlags = CameraClearFlags.Skybox;
            }
            GolfSimZA.Physics.FlightPresentation.ApplyFog();
            Debug.Log("[GolfSimZA] Course lighting: " + GolfSimZA.Visual.CourseLighting.Apply(courseScene));
            // The built ground is lit by the sun and sky like the range: less sky fill than imported courses need.
            RenderSettings.ambientIntensity = 0.85f;
            // The course water shades by depth (clear at the bank, darker further in): it reads the camera depth.
            if (ourCamera != null) ourCamera.depthTextureMode |= DepthTextureMode.Depth;
            try
            {
                Debug.Log("[GolfSimZA] Course look: " + CourseScenery.ImproveCourse(courseScene, Course, gameObject, ourCamera));
                GolfSimZA.Physics.TreeField field = GetComponent<GolfSimZA.Physics.TreeField>();
                if (field != null)
                {
                    int mapped = builtCourse.PlantTrees(field);
                    field.Commit();
                    Debug.Log("[GolfSimZA] " + mapped + " OpenStreetMap trees planted");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GolfSimZA] Course look could not be applied: " + ex);
            }
            UnityEngine.Physics.SyncTransforms();

            IsLoading = false;
            IsLoaded = true;
            Progress = 1f;
            StatusText = "";
            Debug.Log($"[GolfSimZA] Course '{course.name}' built from OpenStreetMap: {builtCourse.Summary}");
            Loaded?.Invoke(course);
        }

        private void PrepareCourseScene()
        {
            Camera ourCamera = Camera.main;
            Scene ourScene = gameObject.scene;
            bool courseHasSun = false;

            foreach (GameObject root in courseScene.GetRootGameObjects())
            {
                // The course brings its own cameras, audio listeners and editor helpers.
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                    if (camera != ourCamera) camera.gameObject.SetActive(false);
                foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
                    listener.enabled = false;
                foreach (Light light in root.GetComponentsInChildren<Light>(true))
                    if (light.type == LightType.Directional && light.isActiveAndEnabled) courseHasSun = true;
            }

            // Use the course's sky, fog and ambient light.
            SceneManager.SetActiveScene(courseScene);

            GolfSimZA.Physics.FlightPresentation.ApplyFog();

            ShaderRepair.Report report = ShaderRepair.Repair(courseScene);
            Debug.Log("[GolfSimZA] Course water: " + GolfSimZA.Physics.WaterSurface.Prepare(courseScene));
            Debug.Log("[GolfSimZA] Course shaders: " + report);
            Debug.Log("[GolfSimZA] Course lighting: " + GolfSimZA.Visual.CourseLighting.Apply(courseScene));

            foreach (GameObject root in ourScene.GetRootGameObjects())
            {
                foreach (string demo in DemoObjects)
                    if (root.name == demo) root.SetActive(false);
                if (courseHasSun && root.name == "Sun") root.SetActive(false);
            }

            if (ourCamera != null)
            {
                // The single audio listener lives on the GolfSimZA audio object (it follows the camera).
                AudioListener own = ourCamera.GetComponent<AudioListener>();
                if (own != null) Destroy(own);
                _ = GolfSimZA.Core.GolfSimAudio.Instance;
                ourCamera.farClipPlane = Mathf.Clamp(GolfSimZA.Core.AppSettings.Current.drawDistanceMeters, 500f, 15000f);
                ourCamera.nearClipPlane = Mathf.Min(ourCamera.nearClipPlane, 0.1f);
                if (RenderSettings.skybox != null) ourCamera.clearFlags = CameraClearFlags.Skybox;
            }

            // Trees, grass distance, clouds and colour boost (Settings → VISUAL → COURSE LOOK).
            try
            {
                Debug.Log("[GolfSimZA] Course look: " + CourseScenery.ImproveCourse(courseScene, Course, gameObject, ourCamera));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GolfSimZA] Course look could not be applied: " + ex);
            }
        }

        private void Fail(string message)
        {
            IsLoading = false;
            IsLoaded = false;
            Error = message;
            StatusText = "";
            Debug.LogError("[GolfSimZA] Course load failed: " + message);
            Failed?.Invoke(message);
        }

        private void OnDestroy()
        {
            builtCourse?.Dispose();
            builtCourse = null;
            // The course scene is unloaded with the round; release the course file too.
            if (bundle != null)
            {
                bundle.Unload(true);
                bundle = null;
            }
        }
    }
}
