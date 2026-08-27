using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using NUnit.Framework;
using UnityEngine;

namespace MeteorDefenseVR.Tests
{
    public sealed class BossClimaxControllerTests
    {
        private GameObject root;
        private GameObject target;
        private GameObject flowObject;
        private BossClimaxController controller;
        private GameFlowManager gameFlow;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("BossClimaxTest");
            target = new GameObject("BossTarget");
            flowObject = new GameObject("BossGameFlow");
            gameFlow = flowObject.AddComponent<GameFlowManager>();
            controller = root.AddComponent<BossClimaxController>();
            controller.Configure(null, null, root.transform, target.transform, null, null);
            controller.BindGameFlow(gameFlow);
            controller.SetDurations(0f, 0f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (target != null) Object.DestroyImmediate(target);
            if (flowObject != null) Object.DestroyImmediate(flowObject);
        }

        [Test]
        public void WarningAndNarrationPrecedeBossSpawn()
        {
            controller.BeginClimax();
            Assert.That(controller.CurrentStage, Is.EqualTo(BossClimaxStage.Warning));
            controller.Tick(0f);
            Assert.That(controller.CurrentStage, Is.EqualTo(BossClimaxStage.Narration));
            controller.Tick(0f);

            Assert.That(controller.CurrentStage, Is.EqualTo(BossClimaxStage.Active));
            Assert.That(controller.ActiveBoss, Is.Not.Null);
            Assert.That(controller.ActiveBoss.MaxHealth, Is.EqualTo(3f));
            Assert.That(controller.ActiveBoss.Speed, Is.EqualTo(0.8f));
            Assert.That(controller.ActiveBoss.Size, Is.EqualTo(3f));
        }

        [Test]
        public void BossRequiresThreeDamageAndThenCompletesMission()
        {
            controller.BeginClimax();
            controller.Tick(0f);
            controller.Tick(0f);
            MeteorController boss = controller.ActiveBoss;

            boss.ReceiveDamage(1f);
            Assert.That(boss.State, Is.EqualTo(MeteorLifecycleState.Active));
            boss.ReceiveDamage(1f);
            Assert.That(boss.State, Is.EqualTo(MeteorLifecycleState.Active));
            boss.ReceiveDamage(1f);

            Assert.That(controller.IsComplete, Is.True);
            Assert.That(gameFlow.CurrentState, Is.EqualTo(GameState.MissionComplete));
        }

        [Test]
        public void BossThatReachesPlayerIsRetriedInsteadOfBlockingExperience()
        {
            controller.BeginClimax();
            controller.Tick(0f);
            controller.Tick(0f);
            MeteorController firstBoss = controller.ActiveBoss;
            firstBoss.ReachPlayer();

            Assert.That(controller.CurrentStage, Is.EqualTo(BossClimaxStage.RetryDelay));
            controller.Tick(0f);
            Assert.That(controller.CurrentStage, Is.EqualTo(BossClimaxStage.Active));
            Assert.That(controller.ActiveBoss, Is.Not.Null);
            Assert.That(controller.ActiveBoss, Is.Not.SameAs(firstBoss));
        }
    }
}
