using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using NUnit.Framework;
using UnityEngine;

namespace MeteorDefenseVR.Tests
{
    public sealed class GameFlowManagerTests
    {
        private GameObject owner;
        private GameFlowManager manager;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("GameFlowManagerTests");
            manager = owner.AddComponent<GameFlowManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(owner);
        }

        [Test]
        public void StartsInBootState()
        {
            Assert.That(manager.CurrentState, Is.EqualTo(GameState.Boot));
            Assert.That(manager.PreviousState, Is.EqualTo(GameState.Boot));
        }

        [Test]
        public void ChangeStateRecordsPreviousStateAndInvokesEventOnce()
        {
            int eventCount = 0;
            manager.OnStateChanged += _ => eventCount++;
            bool changed = manager.ChangeState(GameState.Intro);

            Assert.That(changed, Is.True);
            Assert.That(manager.CurrentState, Is.EqualTo(GameState.Intro));
            Assert.That(manager.PreviousState, Is.EqualTo(GameState.Boot));
            Assert.That(eventCount, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateStateIsIgnored()
        {
            int eventCount = 0;
            manager.OnStateChanged += _ => eventCount++;
            manager.ChangeState(GameState.Playing);
            bool changed = manager.ChangeState(GameState.Playing);

            Assert.That(changed, Is.False);
            Assert.That(eventCount, Is.EqualTo(1));
        }

        [Test]
        public void ResetReturnsToBoot()
        {
            manager.ChangeState(GameState.Result);
            manager.ResetGame();

            Assert.That(manager.CurrentState, Is.EqualTo(GameState.Boot));
            Assert.That(manager.PreviousState, Is.EqualTo(GameState.Reset));
        }

        [Test]
        public void FullExperienceSequenceReachesResultThenResetsToBoot()
        {
            GameState[] sequence =
            {
                GameState.Intro,
                GameState.MissionBriefing,
                GameState.EyeCalibration,
                GameState.Tutorial,
                GameState.Practice,
                GameState.Launch,
                GameState.Countdown,
                GameState.Playing,
                GameState.BossMeteor,
                GameState.MissionComplete,
                GameState.Result
            };

            foreach (GameState state in sequence)
            {
                Assert.That(manager.ChangeState(state), Is.True);
                Assert.That(manager.CurrentState, Is.EqualTo(state));
            }

            manager.ResetGame();
            Assert.That(manager.CurrentState, Is.EqualTo(GameState.Boot));
        }
    }
}
