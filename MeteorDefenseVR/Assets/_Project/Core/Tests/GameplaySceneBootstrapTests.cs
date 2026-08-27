using System.Collections;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools;
using UnityEngine;
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
                Assert.That(components.arraySize, Is.EqualTo(3));
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
            yield return new WaitForSecondsRealtime(3.2f);
            Assert.That(first.CurrentState, Is.EqualTo(GameState.MissionBriefing));
            yield return new WaitForSecondsRealtime(4.2f);
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
