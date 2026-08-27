using System.IO;
using MeteorDefenseVR.GameFlow;
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

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true)
            };

            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] Bootstrap and MeteorDefense scenes are ready.");
        }
    }
}
