using System.Collections.Generic;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MeteorDefenseVR.Tests
{
    public sealed class FullExperienceQaTests
    {
        private readonly List<GameObject> roots = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = roots.Count - 1; i >= 0; i--)
                if (roots[i] != null) Object.DestroyImmediate(roots[i]);
            roots.Clear();
        }

        [Test]
        public void FullStateFlow_ResetsCleanlyForTenConsecutiveSessions()
        {
            GameFlowManager flow = Create("QaFlow").AddComponent<GameFlowManager>();
            MeteorSpawner spawner = Create("QaSpawner").AddComponent<MeteorSpawner>();
            LaserWeapon weapon = Create("QaWeapon").AddComponent<LaserWeapon>();
            MissionHudController hud = Create("QaHud").AddComponent<MissionHudController>();
            hud.Configure(spawner, weapon, 100f);
            PlayerHealth health = Create("QaHealth").AddComponent<PlayerHealth>();
            health.Configure(spawner, hud, 100f, 10, 0f, GameOverPolicy.NoFailExperienceMode);
            health.BindGameFlow(flow);
            GameSessionStats stats = Create("QaStats").AddComponent<GameSessionStats>();
            stats.Configure(spawner, null, weapon, health, hud);
            ResultController result = Create("QaResult").AddComponent<ResultController>();
            result.Configure(stats, null);
            result.BindGameFlow(flow);
            OperationsResetController operations = Create("QaOperations").AddComponent<OperationsResetController>();
            operations.Configure(spawner, null, weapon, health, stats, hud, result, null, null, null, null, null);
            operations.BindGameFlow(flow);

            int transitionCount = 0;
            flow.OnStateTransitioned += (_, _) => transitionCount++;
            GameState[] experienceStates =
            {
                GameState.Intro, GameState.MissionBriefing, GameState.EyeCalibration,
                GameState.Tutorial, GameState.Practice, GameState.Launch,
                GameState.Countdown, GameState.Playing, GameState.BossMeteor,
                GameState.MissionComplete, GameState.Result
            };

            for (int cycle = 0; cycle < 10; cycle++)
            {
                int cycleStartTransitions = transitionCount;
                stats.ResetSession(25);
                hud.AddScore(150);
                stats.RecordShotAndLock();
                stats.RecordSuccessfulHit();
                health.ApplyDamage(10);

                foreach (GameState state in experienceStates)
                {
                    Assert.That(flow.ChangeState(state), Is.True, $"cycle {cycle + 1}: {state}");
                    int beforeDuplicate = transitionCount;
                    Assert.That(flow.ChangeState(state), Is.False, "duplicate state must be rejected");
                    Assert.That(transitionCount, Is.EqualTo(beforeDuplicate));
                }

                Assert.That(result.IsVisible, Is.True);
                Assert.That(result.Snapshot.Score, Is.EqualTo(150));
                Assert.That(result.Snapshot.Shots, Is.EqualTo(1));
                Assert.That(result.Snapshot.SuccessfulHits, Is.EqualTo(1));
                operations.ResetExperience();

                Assert.That(flow.CurrentState, Is.EqualTo(GameState.Boot));
                Assert.That(result.IsVisible, Is.False);
                Assert.That(hud.Score, Is.Zero);
                Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth));
                Assert.That(stats.CreateSnapshot().Score, Is.Zero);
                Assert.That(stats.CreateSnapshot().Shots, Is.Zero);
                Assert.That(transitionCount - cycleStartTransitions, Is.EqualTo(13));
            }
        }

        [Test]
        public void BuildScenesAndPrefabs_HaveNoMissingMonoScripts()
        {
            Assert.That(EditorBuildSettings.scenes.Length, Is.EqualTo(2));
            Assert.That(EditorBuildSettings.scenes[0].path, Does.EndWith("Bootstrap.unity"));
            Assert.That(EditorBuildSettings.scenes[1].path, Does.EndWith("MeteorDefense.unity"));

            foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
            {
                Assert.That(buildScene.enabled, Is.True);
                Scene scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
                foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                    AssertMissingScriptsRecursive(sceneRoot);
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" });
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab), Is.Zero, path);
            }
        }

        private GameObject Create(string name)
        {
            GameObject created = new GameObject(name);
            roots.Add(created);
            return created;
        }

        private static void AssertMissingScriptsRecursive(GameObject target)
        {
            Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(target), Is.Zero, target.name);
            for (int i = 0; i < target.transform.childCount; i++)
                AssertMissingScriptsRecursive(target.transform.GetChild(i).gameObject);
        }
    }
}
