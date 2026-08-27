using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using NUnit.Framework;
using UnityEngine;

namespace MeteorDefenseVR.Tests
{
    public sealed class MissionCompleteControllerTests
    {
        private GameObject root;
        private GameObject target;
        private GameObject flowObject;
        private MeteorSpawner spawner;
        private MissionCompleteController controller;
        private GameFlowManager gameFlow;
        private MeteorWaveData wave;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("MissionCompleteTest");
            target = new GameObject("MissionCompleteTarget");
            flowObject = new GameObject("MissionCompleteFlow");
            gameFlow = flowObject.AddComponent<GameFlowManager>();
            spawner = root.AddComponent<MeteorSpawner>();
            controller = root.AddComponent<MissionCompleteController>();

            var entry = new MeteorSpawnData();
            entry.Configure(MeteorType.Normal, null, 1, 0f, 0f, 0f, 1f, 10, 100, 1f, new Vector2(20f, 15f));
            wave = ScriptableObject.CreateInstance<MeteorWaveData>();
            wave.Configure("Complete Test", 0f, entry);
            spawner.Configure(new[] { wave }, root.transform, target.transform);
            controller.Configure(spawner, null);
            controller.BindGameFlow(gameFlow);
            controller.SetResultDelay(3f);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (target != null) Object.DestroyImmediate(target);
            if (flowObject != null) Object.DestroyImmediate(flowObject);
            if (wave != null) Object.DestroyImmediate(wave);
        }

        [Test]
        public void MissionCompleteStateStartsSequenceAndMovesToResultAfterDelay()
        {
            gameFlow.ChangeState(GameState.MissionComplete);
            Assert.That(controller.IsRunning, Is.True);
            Assert.That(controller.StatusMessage, Does.Contain("MISSION COMPLETE"));

            controller.Tick(3f);

            Assert.That(controller.IsComplete, Is.True);
            Assert.That(gameFlow.CurrentState, Is.EqualTo(GameState.Result));
        }

        [Test]
        public void RemainingMeteorsAreDetachedStoppedAndFaded()
        {
            spawner.BeginSpawning();
            spawner.Tick(0f);
            MeteorController meteor = spawner.ActiveMeteors[0];

            controller.BeginMissionComplete();

            Assert.That(spawner.IsSpawning, Is.False);
            Assert.That(spawner.ActiveCount, Is.Zero);
            Assert.That(meteor.State, Is.EqualTo(MeteorLifecycleState.Inactive));
            Assert.That(meteor.GetComponent<MeteorCleanupFade>(), Is.Not.Null);
        }

        [Test]
        public void CleanupFadeShrinksMeteorToZero()
        {
            GameObject meteorObject = new GameObject("FadeMeteor");
            meteorObject.transform.SetParent(root.transform);
            meteorObject.transform.localScale = Vector3.one * 2f;
            MeteorCleanupFade fade = meteorObject.AddComponent<MeteorCleanupFade>();
            fade.Begin(0.5f);
            fade.Tick(0.5f);

            Assert.That(fade.IsComplete, Is.True);
            Assert.That(meteorObject.transform.localScale, Is.EqualTo(Vector3.zero));
        }
    }
}
