using MeteorDefenseVR.Meteor;
using NUnit.Framework;
using UnityEngine;

namespace MeteorDefenseVR.Tests
{
    public sealed class MeteorControllerTests
    {
        private GameObject meteorObject;
        private MeteorController meteor;

        [SetUp]
        public void SetUp()
        {
            meteorObject = new GameObject("MeteorTest");
            meteor = meteorObject.AddComponent<MeteorController>();
            meteor.Configure(MeteorType.Normal, 2f, 4f, 10, 100, 1.5f);
        }

        [TearDown]
        public void TearDown()
        {
            if (meteorObject != null) Object.DestroyImmediate(meteorObject);
        }

        [Test]
        public void Spawn_InitializesRuntimeState()
        {
            meteor.Spawn();

            Assert.That(meteor.State, Is.EqualTo(MeteorLifecycleState.Active));
            Assert.That(meteor.CurrentHealth, Is.EqualTo(2f));
            Assert.That(meteor.IsTargetable, Is.True);
            Assert.That(meteor.transform.localScale, Is.EqualTo(Vector3.one * 1.5f));
        }

        [Test]
        public void ReceiveDamage_DestroysAndAwardsScoreOnce()
        {
            int destroyed = 0;
            int awardedScore = 0;
            meteor.Destroyed += _ => destroyed++;
            meteor.ScoreAwarded += (_, value) => awardedScore += value;
            meteor.Spawn();

            Assert.That(meteor.ReceiveDamage(1f), Is.True);
            Assert.That(meteor.ReceiveDamage(1f), Is.True);
            Assert.That(meteor.ReceiveDamage(1f), Is.False);

            Assert.That(meteor.State, Is.EqualTo(MeteorLifecycleState.Destroyed));
            Assert.That(destroyed, Is.EqualTo(1));
            Assert.That(awardedScore, Is.EqualTo(100));
        }

        [Test]
        public void ReachPlayer_AppliesDamageEventWithoutScore()
        {
            int playerDamage = 0;
            int scoreEvents = 0;
            meteor.ReachedPlayerEvent += (_, value) => playerDamage += value;
            meteor.ScoreAwarded += (_, __) => scoreEvents++;
            meteor.Spawn();

            Assert.That(meteor.ReachPlayer(), Is.True);
            Assert.That(meteor.ReachPlayer(), Is.False);
            Assert.That(playerDamage, Is.EqualTo(10));
            Assert.That(scoreEvents, Is.Zero);
            Assert.That(meteor.State, Is.EqualTo(MeteorLifecycleState.ReachedPlayer));
        }

        [Test]
        public void Move_UsesConfiguredSpeedAndDirection()
        {
            meteor.SetMovementDirection(Vector3.forward);
            meteor.Spawn();

            meteor.Move(0.5f);

            Assert.That(meteor.transform.position.z, Is.EqualTo(2f).Within(0.001f));
        }
    }
}
