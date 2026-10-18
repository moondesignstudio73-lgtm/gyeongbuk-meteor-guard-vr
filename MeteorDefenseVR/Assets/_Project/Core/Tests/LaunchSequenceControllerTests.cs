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
                LaunchStage.Launch,
                LaunchStage.Space,
                LaunchStage.Countdown3,
                LaunchStage.Countdown2,
                LaunchStage.Countdown1,
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

        [Test]
        public void AccelerationAndSpaceStabilizationPrecedeCountdown()
        {
            controller.SetDurations(0f, 0f, 0f, 0f, 1f, 0f, 0f);
            controller.BeginLaunch();
            controller.Tick(0f); // door
            controller.Tick(0f); // engine
            controller.Tick(0f); // thrust

            Assert.That(controller.CurrentStage, Is.EqualTo(LaunchStage.Launch));
            Assert.That(gameFlow.CurrentState, Is.Not.EqualTo(GameState.Countdown));
            controller.Tick(.5f);
            Assert.That(controller.SpeedAmount, Is.EqualTo(.5f).Within(.001f));

            controller.Tick(.5f); // space
            Assert.That(controller.CurrentStage, Is.EqualTo(LaunchStage.Space));
            controller.Tick(0f); // countdown
            Assert.That(controller.CurrentStage, Is.EqualTo(LaunchStage.Countdown3));
            Assert.That(gameFlow.CurrentState, Is.EqualTo(GameState.Countdown));
        }
    }
}
