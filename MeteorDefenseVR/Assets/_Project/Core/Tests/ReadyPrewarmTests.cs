using System.Collections;
using System.Collections.Generic;
using MeteorDefenseVR.Audio;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Tutorial;
using MeteorDefenseVR.Visual;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeteorDefenseVR.Tests
{
    public sealed class ReadyPrewarmTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        private static IEnumerator WaitForPreparation()
        {
            yield return GameplaySceneBootstrapTests.WaitForGameplayScene();
            Object.FindAnyObjectByType<GazeInputRouter>().SetMode(PcGazeMode.MouseGaze);
            var warm = Object.FindAnyObjectByType<ReadyPrewarmController>();
            Assert.That(warm, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + 20;
            while (!warm.IsComplete && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(warm.IsComplete, Is.True);
        }

        [UnityTest]
        public IEnumerator ReadyPreparesWithoutPlayingAudioOrAdvancingGameFlow()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return WaitForPreparation();
            var audio = Object.FindAnyObjectByType<AudioManager>();
            var warm = Object.FindAnyObjectByType<ReadyPrewarmController>();
            var spawner = Object.FindAnyObjectByType<MeteorSpawner>();
            Assert.That(GameFlowManager.Instance.CurrentState, Is.EqualTo(GameState.Boot));
            Assert.That(audio.PreloadedClipCount, Is.GreaterThanOrEqualTo(5));
            Assert.That(audio.CurrentBgmClip, Is.Null);
            Assert.That(audio.BgmPlaybackStarts, Is.Zero);
            Assert.That(warm.PreparedTextCount, Is.GreaterThan(0));
            foreach (var bridge in Object.FindObjectsByType<WorldTextPresentation>(FindObjectsInactive.Include))
            {
                if (!WorldTextMotion.Supports(bridge.name)) continue;
                Assert.That(bridge.GetComponent<WorldTextMotion>(), Is.Not.Null, "Inactive presentation must be prepared before its first state: " + bridge.name);
                Assert.That(bridge.Display.transform.parent, Is.EqualTo(bridge.transform), "Prewarm must restore the authored hierarchy");
            }
            Assert.That(spawner.PoolCount, Is.GreaterThan(0));
            Assert.That(spawner.ActiveCount, Is.Zero);
            Assert.That(Object.FindAnyObjectByType<TutorialController>().PreparedMeteorCount, Is.EqualTo(1));
            Assert.That(Object.FindAnyObjectByType<PracticeController>().PreparedMeteorCount, Is.EqualTo(2));
            Assert.That(Object.FindAnyObjectByType<BossClimaxController>().PreparedMeteorCount, Is.EqualTo(1));
            Assert.That(Object.FindAnyObjectByType<PremiumCombatVfx>().ActiveParticleCount, Is.Zero);
            int loaded = audio.PreloadedClipCount;
            int sources = Object.FindObjectsByType<AudioSource>().Length;
            yield return audio.PreloadClips();
            Assert.That(audio.PreloadedClipCount, Is.EqualTo(loaded), "Repeated preload is idempotent");
            Assert.That(Object.FindObjectsByType<AudioSource>().Length, Is.EqualTo(sources));
            Assert.That(audio.BgmPlaybackStarts, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ThirtyResetsRetainPreparedPoolsWithoutDuplicateInstances()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return WaitForPreparation();
            var spawner = Object.FindAnyObjectByType<MeteorSpawner>();
            var reset = Object.FindAnyObjectByType<OperationsResetController>();
            int created = spawner.InstantiatedCount;
            int cached = Object.FindObjectsByType<PreparedMeteorMember>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            for (int i = 0; i < 30; i++)
            {
                GameFlowManager.Instance.StartMainGame();
                // Respect the authored wave delay; do not assume the first spawn is within 1 second.
                for (int step = 0; step < 20 && spawner.SpawnedCount == 0; step++) spawner.Tick(.5f);
                Assert.That(spawner.ActiveCount, Is.GreaterThan(0));
                Assert.That(spawner.InstantiatedCount, Is.EqualTo(created), "First wave must borrow a Ready-prepared meteor");
                reset.ResetExperience();
                yield return null;
                Assert.That(GameFlowManager.Instance.CurrentState, Is.EqualTo(GameState.Boot));
                Assert.That(spawner.ActiveCount, Is.Zero);
                Assert.That(Object.FindObjectsByType<PreparedMeteorMember>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(cached));
            }
            Assert.That(Object.FindObjectsByType<ReadyPrewarmController>().Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ZeroDelayPracticeUsesTwoSlotsWithoutDeactivatingNextMeteor()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return WaitForPreparation();
            var practice = Object.FindAnyObjectByType<PracticeController>();
            var reset = Object.FindAnyObjectByType<OperationsResetController>();
            practice.InterMeteorDelay = 0;
            practice.ReadyDisplayDuration = 10;
            practice.MeteorCount = 3;
            var identities = new HashSet<MeteorController>();
            for (int run = 0; run < 30; run++)
            {
                GameFlowManager.Instance.StartPractice();
                for (int shot = 0; shot < 3; shot++)
                {
                    var meteor = practice.ActiveMeteor;
                    Assert.That(meteor, Is.Not.Null);
                    Assert.That(meteor.gameObject.activeInHierarchy, Is.True, "Previous death callback must not deactivate a respawn");
                    identities.Add(meteor);
                    meteor.ReceiveDamage(1000);
                }
                Assert.That(practice.DestroyedMeteors, Is.EqualTo(3));
                Assert.That(practice.IsComplete, Is.True);
                reset.ResetExperience();
                yield return null;
            }
            Assert.That(identities.Count, Is.EqualTo(2));
            Assert.That(practice.PreparedMeteorCount, Is.EqualTo(2));
        }
    }
}
