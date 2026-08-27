using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using NUnit.Framework;
using UnityEngine;

namespace MeteorDefenseVR.Tests
{
    public sealed class OperationsResetControllerTests
    {
        private GameObject root;
        private GameObject flowObject;
        private OperationsResetController operations;
        private ReadyScreenController ready;
        private GameFlowManager gameFlow;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("OperationsResetTest");
            flowObject = new GameObject("OperationsFlowTest");
            gameFlow = flowObject.AddComponent<GameFlowManager>();
            operations = root.AddComponent<OperationsResetController>();
            operations.BindGameFlow(gameFlow);
            operations.SetResultDuration(7f);
            ready = root.AddComponent<ReadyScreenController>();
            ready.BindGameFlow(gameFlow);
            ready.SetVisible(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (flowObject != null) Object.DestroyImmediate(flowObject);
        }

        [Test]
        public void ResultAutomaticallyResetsToBootAfterConfiguredDuration()
        {
            gameFlow.ChangeState(GameState.Result);
            Assert.That(operations.ResultTimerRunning, Is.True);

            operations.Tick(7f);

            Assert.That(gameFlow.CurrentState, Is.EqualTo(GameState.Boot));
            Assert.That(operations.ResultTimerRunning, Is.False);
            Assert.That(ready.IsReadyVisible, Is.True);
        }

        [TestCase(OperatorCommand.Intro, GameState.Intro)]
        [TestCase(OperatorCommand.Calibration, GameState.EyeCalibration)]
        [TestCase(OperatorCommand.Tutorial, GameState.Tutorial)]
        [TestCase(OperatorCommand.MainGame, GameState.Playing)]
        [TestCase(OperatorCommand.Result, GameState.Result)]
        public void OperatorCommandsJumpToExpectedState(OperatorCommand command, GameState expected)
        {
            operations.HandleOperatorCommand(command);
            Assert.That(gameFlow.CurrentState, Is.EqualTo(expected));
        }

        [Test]
        public void ReadyScreenStartsIntroAndHides()
        {
            ready.StartExperience();
            Assert.That(gameFlow.CurrentState, Is.EqualTo(GameState.Intro));
            Assert.That(ready.IsReadyVisible, Is.False);
        }

        [Test]
        public void GazeDwellStartsReadyExperience()
        {
            GameObject button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.transform.SetParent(root.transform);
            ReadyStartGazeTarget gaze = button.AddComponent<ReadyStartGazeTarget>();
            gaze.Configure(ready, 0.8f);

            gaze.Advance(0.8f);

            Assert.That(gameFlow.CurrentState, Is.EqualTo(GameState.Intro));
            Assert.That(gaze.Progress, Is.Zero);
        }

        [Test]
        public void OperatorKeysCanBeDisabledForReleaseBuild()
        {
            operations.SetOperatorKeysEnabled(false);
            Assert.That(operations.OperatorKeysEnabled, Is.False);
        }
    }
}
