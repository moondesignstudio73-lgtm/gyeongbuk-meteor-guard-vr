using System.IO;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.EyeTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MeteorDefenseVR.Editor
{
    [InitializeOnLoad]
    internal static class ProjectSceneSetup
    {
        private const string SceneDirectory = "Assets/_Project/Scenes";
        private const string BootstrapScenePath = SceneDirectory + "/Bootstrap.unity";
        private const string GameScenePath = SceneDirectory + "/MeteorDefense.unity";
        private const string SetupSessionKey = "MeteorDefenseVR.ProjectSceneSetup.Completed";

        static ProjectSceneSetup()
        {
            if (!SessionState.GetBool(SetupSessionKey, false))
                EditorApplication.delayCall += EnsureProjectScenes;
        }

        [MenuItem("Meteor Defense/Setup Project Scenes")]
        public static void EnsureProjectScenes()
        {
            SessionState.SetBool(SetupSessionKey, true);
            Directory.CreateDirectory(SceneDirectory);

            if (!File.Exists(BootstrapScenePath))
            {
                Scene bootstrap = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Bootstrap").AddComponent<GameFlowManager>();
                EditorSceneManager.SaveScene(bootstrap, BootstrapScenePath);
            }

            if (!File.Exists(GameScenePath))
            {
                Scene game = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                GameObject root = new GameObject("MeteorDefense");

                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(root.transform);
                cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();

                GameObject lightObject = new GameObject("Directional Light");
                lightObject.transform.SetParent(root.transform);
                lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;

                EditorSceneManager.SaveScene(game, GameScenePath);
            }

            EnsureGazeSystem();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true)
            };

            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] Bootstrap and MeteorDefense scenes are ready.");
        }

        private static void EnsureGazeSystem()
        {
            Scene game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Camera camera = Camera.main;
            if (camera == null)
            {
                Debug.LogWarning("[ProjectSetup] Main Camera was not found; gaze setup was skipped.");
                return;
            }

            GameObject gazeSystem = GameObject.Find("GazeSystem");
            if (gazeSystem == null) gazeSystem = new GameObject("GazeSystem");

            EditorGazeProvider provider = gazeSystem.GetComponent<EditorGazeProvider>();
            if (provider == null) provider = gazeSystem.AddComponent<EditorGazeProvider>();
            provider.SetCamera(camera);

            GazeRaycaster raycaster = gazeSystem.GetComponent<GazeRaycaster>();
            if (raycaster == null) raycaster = gazeSystem.AddComponent<GazeRaycaster>();
            raycaster.SetProvider(provider);

            EnsureCalibrationSystem(provider);

            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);
        }

        private static void EnsureCalibrationSystem(EditorGazeProvider gazeProvider)
        {
            GameObject root = GameObject.Find("CalibrationSystem");
            if (root == null) root = new GameObject("CalibrationSystem");
            CalibrationSequenceController controller = root.GetComponent<CalibrationSequenceController>();
            if (controller == null) controller = root.AddComponent<CalibrationSequenceController>();

            Vector3[] positions =
            {
                new Vector3(0f, 1.6f, 4f),
                new Vector3(-1.35f, 1.6f, 4f),
                new Vector3(1.35f, 1.6f, 4f),
                new Vector3(0f, 2.45f, 4f),
                new Vector3(0f, 0.75f, 4f)
            };
            string[] names = { "Center", "Left", "Right", "Top", "Bottom" };
            CalibrationPoint[] points = new CalibrationPoint[positions.Length];

            for (int i = 0; i < positions.Length; i++)
            {
                Transform existing = root.transform.Find("Point_" + names[i]);
                GameObject pointObject = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pointObject.name = "Point_" + names[i];
                pointObject.transform.SetParent(root.transform);
                pointObject.transform.position = positions[i];
                pointObject.transform.localScale = Vector3.one * 0.18f;
                points[i] = pointObject.GetComponent<CalibrationPoint>();
                if (points[i] == null) points[i] = pointObject.AddComponent<CalibrationPoint>();
            }

            Transform textTransform = root.transform.Find("StatusText");
            GameObject textObject = textTransform != null ? textTransform.gameObject : new GameObject("StatusText");
            textObject.transform.SetParent(root.transform);
            textObject.transform.position = new Vector3(0f, 2.9f, 4f);
            textObject.transform.rotation = Quaternion.identity;
            TextMesh text = textObject.GetComponent<TextMesh>();
            if (text == null) text = textObject.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = 0.035f;
            text.color = new Color(0.75f, 0.95f, 1f);

            CalibrationStatusView view = textObject.GetComponent<CalibrationStatusView>();
            if (view == null) view = textObject.AddComponent<CalibrationStatusView>();
            view.Configure(controller, text);
            controller.Configure(points, gazeProvider);
            controller.ResetSequence();
        }
    }
}
