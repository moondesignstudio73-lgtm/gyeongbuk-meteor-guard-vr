using System.Collections.Generic;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Launch;
using NUnit.Framework;
using UnityEngine;

namespace MeteorDefenseVR.Tests
{
    public sealed class LaunchSequenceControllerTests
    {
        private GameObject flowObject;
        private GameObject launchObject;
        private GameFlowManager gameFlow;
        private LaunchSequenceController controller;

        [SetUp]
        public void SetUp()
        {
            flowObject = new GameObject("LaunchFlowTest");
            gameFlow = flowObject.AddComponent<GameFlowManager>();
            launchObject = new GameObject("LaunchSequenceTest");
            controller = launchObject.AddComponent<LaunchSequenceController>();
            controller.BindGameFlow(gameFlow);
            controller.SetDurations(0f, 0f, 0f, 0f, 0f, 0f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            if (launchObject != null) Object.DestroyImmediate(launchObject);
            if (flowObject != null) Object.DestroyImmediate(flowObject);
        }

        [Test]
        public void LaunchRunsStoryboardOrderAndStartsMainGame()
        {
            var stages = new List<LaunchStage>();
            controller.StageChanged += (stage, _) => stages.Add(stage);

            controller.BeginLaunch();
            for (int i = 0; i < 10; i++) controller.Tick(1f);

            CollectionAssert.AreEqual(new[]
            {
                LaunchStage.Hangar,
                LaunchStage.DoorOpen,
                LaunchStage.EngineStart,
                LaunchStage.Countdown3,
                LaunchStage.Countdown2,
                LaunchStage.Countdown1,
                LaunchStage.Launch,
                LaunchStage.Space,
                LaunchStage.MissionStart,
                LaunchStage.Complete
            }, stages);
            Assert.That(controller.IsComplete, Is.True);
            Assert.That(gameFlow.CurrentState, Is.EqualTo(GameState.Playing));
        }

        [Test]
        public void GameFlowLaunchStateAutomaticallyStartsSequence()
        {
            gameFlow.ChangeState(GameState.Launch);
            for (int i = 0; i < 10; i++) controller.Tick(1f);

            Assert.That(controller.IsComplete, Is.True);
            Assert.That(gameFlow.PreviousState, Is.EqualTo(GameState.Countdown));
            Assert.That(gameFlow.CurrentState, Is.EqualTo(GameState.Playing));
        }

        [Test]
        public void SequenceFinishesWithDoorsEngineAndSpeedAtFullAmount()
        {
            controller.BeginLaunch();
            for (int i = 0; i < 10; i++) controller.Tick(1f);

            Assert.That(controller.DoorOpenAmount, Is.EqualTo(1f));
            Assert.That(controller.EngineAmount, Is.EqualTo(1f));
            Assert.That(controller.SpeedAmount, Is.EqualTo(1f));
        }
    }
}
