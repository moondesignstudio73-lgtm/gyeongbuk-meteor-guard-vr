using System.Collections;
using System.IO;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MeteorDefenseVR.Tests
{
    public sealed class GameplaySceneBootstrapTests
    {
        [Test]
        public void VisualProfile_ComponentsArePersistedAsSubAssets()
        {
            const string path = "Assets/Art/VisualIntegration/MeteorDefenseVisualProfile.asset";
            Object profile = AssetDatabase.LoadMainAssetAtPath(path);
            Assert.That(profile, Is.Not.Null);
            using (SerializedObject serialized = new SerializedObject(profile))
            {
                SerializedProperty components = serialized.FindProperty("components");
                // Bloom, Vignette, ColorAdjustments plus the requested Neutral Tonemapping.
                Assert.That(components.arraySize, Is.EqualTo(4));
                for (int i = 0; i < components.arraySize; i++)
                {
                    Object component = components.GetArrayElementAtIndex(i).objectReferenceValue;
                    Assert.That(component, Is.Not.Null, $"volume component {i}");
                    Assert.That(AssetDatabase.GetAssetPath(component), Is.EqualTo(path));
                }
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator BootstrapScene_UsesGameplayEnvironmentThroughReset()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return WaitForGameplayScene();
            AssertGameplayEnvironment();
            var router = Object.FindAnyObjectByType<GazeInputRouter>();
            router.SetMode(PcGazeMode.MouseGaze);
            yield return SaveEnvironmentProof("title", router);
            var first = GameFlowManager.Instance;
            var reset = Object.FindAnyObjectByType<OperationsResetController>();
            for (int i = 0; i < 3; i++)
            {
                first.StartMainGame();
                yield return null;
                AssertGameplayEnvironment();
                if (i == 0) yield return SaveEnvironmentProof("playing", router);
                reset.ResetExperience();
                yield return null;
                yield return null;
                Assert.That(first.CurrentState, Is.EqualTo(GameState.Boot));
                Assert.That(GameFlowManager.Instance, Is.SameAs(first));
                AssertGameplayEnvironment();
            }
            yield return SaveEnvironmentProof("reset", router);
            Assert.That(Object.FindObjectsByType<GameFlowManager>().Length, Is.EqualTo(1));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(2), "Reset must not add another gameplay scene");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator BootstrapScene_ActivatesAlreadyLoadedGameplayWithoutDuplication()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Additive);
            SceneManager.SetActiveScene(SceneManager.GetSceneByName("Bootstrap"));
            yield return new EnterPlayMode();
            yield return WaitForGameplayScene();
            AssertGameplayEnvironment();
            Assert.That(SceneManager.sceneCount, Is.EqualTo(2));
            Assert.That(Object.FindObjectsByType<GameFlowManager>().Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<ReadyScreenController>().Length, Is.EqualTo(1));
            yield return new ExitPlayMode();
        }

        internal static IEnumerator WaitForGameplayScene()
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (!SceneManager.GetSceneByName("MeteorDefense").isLoaded && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(SceneManager.GetSceneByName("MeteorDefense").isLoaded, Is.True);
            // Allow the async loader continuation and scene Start callbacks to finish.
            yield return null;
            yield return null;
        }

        internal static void AssertGameplayEnvironment()
        {
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MeteorDefense"),
                "Additive startup must hand over the active scene, including skybox and lighting");
            var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Premium/Materials/OrbitalSky.mat");
            Assert.That(sky, Is.Not.Null);
            Assert.That(RenderSettings.skybox, Is.EqualTo(sky));
            Assert.That(sky.GetTexture("_MainTex"), Is.Not.Null);
            Assert.That(sky.shader.isSupported, Is.True);
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Trilight));
            Assert.That(RenderSettings.ambientIntensity, Is.EqualTo(.62f).Within(.001f));
            Assert.That(RenderSettings.fog, Is.False);
            Assert.That(Camera.main, Is.Not.Null);
            Assert.That(Camera.main.clearFlags, Is.EqualTo(CameraClearFlags.Skybox));
        }

        private static IEnumerator SaveEnvironmentProof(string state, GazeInputRouter router)
        {
            string directory = "Docs/BootstrapSkyQA/" + state;
            Directory.CreateDirectory(directory);
            using (var probe = new PcStabilityProbe(Camera.main))
            {
                float deadline = Time.realtimeSinceStartup + 5f;
                while (probe.RenderedFrames < 2 && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(probe.RenderedFrames, Is.GreaterThanOrEqualTo(2));
                probe.SaveGameOnlyProof(directory, router);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DirectGameplayScene_CreatesExactlyOneRuntimeFlow()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return null;

            GameFlowManager first = GameFlowManager.Instance;
            Assert.That(first, Is.Not.Null);
            GameObject root = new GameObject("DirectPlayBootstrapTest");
            root.AddComponent<GameplaySceneBootstrap>();
            yield return null;

            Assert.That(GameFlowManager.Instance, Is.SameAs(first));
            Assert.That(Object.FindObjectsByType<GameFlowManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1));

            ReadyScreenController ready = Object.FindAnyObjectByType<ReadyScreenController>();
            Assert.That(ready, Is.Not.Null);
            Assert.That(ready.IsReadyVisible, Is.True);
            ready.StartExperience();
            Assert.That(first.CurrentState, Is.EqualTo(GameState.Intro));
            Assert.That(ready.transform.Find("ReadyDecorations").gameObject.activeSelf, Is.False);
            yield return new WaitForSecondsRealtime(Object.FindAnyObjectByType<MeteorDefenseVR.Visual.CinematicEnvironmentView>().IntroDuration + .15f);
            Assert.That(first.CurrentState, Is.EqualTo(GameState.MissionBriefing));
            yield return new WaitForSecondsRealtime(Object.FindAnyObjectByType<MeteorDefenseVR.Visual.CinematicEnvironmentView>().BriefingDuration + .15f);
            Assert.That(first.CurrentState, Is.EqualTo(GameState.EyeCalibration));

            first.StartMainGame();
            yield return null;
            Assert.That(Camera.main.transform.Find("MissionHUD").gameObject.activeInHierarchy, Is.True);
            first.ShowResult();
            yield return null;
            Assert.That(Camera.main.transform.Find("MissionHUD").gameObject.activeInHierarchy, Is.False);
            first.ResetGame();
            yield return null;
            Assert.That(ready.IsReadyVisible, Is.True);
            Assert.That(ready.transform.Find("ReadyDecorations").gameObject.activeSelf, Is.True);

            Object.Destroy(root);
            yield return new ExitPlayMode();
        }
    }
}
