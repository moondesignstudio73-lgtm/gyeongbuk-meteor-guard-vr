using System;
using System.Collections.Generic;
using System.Reflection;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.UI;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.Tests
{
    public sealed class AutonomousAuditTests
    {
        private GameObject root;
        [SetUp] public void Setup() => root = new GameObject("AutonomousAuditFixture");
        [TearDown] public void Cleanup() { if (root != null) Object.DestroyImmediate(root); }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        [Test]
        public void InvalidHealthAndDamageCannotPoisonMeteor()
        {
            var meteor = root.AddComponent<MeteorController>();
            meteor.Configure(MeteorType.Normal, float.NaN, float.PositiveInfinity, -5, -5, float.NaN);
            Assert.That(Finite(meteor.MaxHealth) && meteor.MaxHealth >= 1, Is.True);
            Assert.That(Finite(meteor.Speed) && Finite(meteor.Size), Is.True);
            meteor.Spawn(); float before = meteor.CurrentHealth;
            Assert.That(meteor.ReceiveDamage(float.NaN), Is.False);
            Assert.That(meteor.ReceiveDamage(float.PositiveInfinity), Is.False);
            Assert.That(meteor.CurrentHealth, Is.EqualTo(before));
        }
        [Test]
        public void InvalidSpawnConfigIsFiniteAndBounded()
        {
            var data = new MeteorSpawnData();
            data.Configure(MeteorType.Normal, null, 999999, float.NaN, float.PositiveInfinity, float.NaN, float.NaN, -1, -1, float.NaN, new Vector2(float.NaN,float.PositiveInfinity));
            Assert.That(data.Count, Is.InRange(1,64));
            foreach (float value in new[] { data.Delay, data.SpawnInterval, data.Speed, data.Health, data.Size, data.SpawnArea.x, data.SpawnArea.y, data.EstimatedDuration })
                Assert.That(Finite(value), Is.True);
        }
        [Test]
        public void InvalidHealthConfigurationRemainsUsable()
        {
            var health = root.AddComponent<PlayerHealth>();
            health.Configure(null, null, float.NaN, 10, float.PositiveInfinity, GameOverPolicy.NoFailExperienceMode);
            Assert.That(Finite(health.CurrentHealth) && health.CurrentHealth >= 1, Is.True);
            health.ApplyDamage(1); health.Tick(float.NaN);
            Assert.That(Finite(health.HealthNormalized), Is.True);
            health.Tick(10); Assert.That(health.IsInvincible, Is.False);
        }
        [Test]
        public void InvalidLockDurationAndDeltaDoNotAutoFire()
        {
            root.AddComponent<SphereCollider>(); var target = root.AddComponent<GazeLockOnTarget>();
            target.LockOnDuration = float.NaN; target.GracePeriod = float.PositiveInfinity;
            target.Advance(float.NaN); target.Advance(float.PositiveInfinity);
            Assert.That(target.IsLocked, Is.False);
            Assert.That(Finite(target.Progress) && Finite(target.LockOnDuration) && Finite(target.GracePeriod), Is.True);
            target.Advance(target.LockOnDuration); Assert.That(target.IsLocked, Is.True);
        }
        [Test]
        public void ScoreCannotWrapNegativeAndAccuracyStaysInRange()
        {
            var hud = root.AddComponent<MissionHudController>();
            hud.AddScore(int.MaxValue); hud.AddScore(1);
            Assert.That(hud.Score, Is.EqualTo(int.MaxValue));
            Assert.That(new GameSessionSnapshot { Shots=1, SuccessfulHits=2 }.AccuracyPercent, Is.EqualTo(100));
            Assert.That(new GameSessionSnapshot { Shots=1, SuccessfulHits=-1 }.AccuracyPercent, Is.Zero);
            var stats = root.AddComponent<GameSessionStats>(); stats.ResetSession(); stats.Tick(float.NaN); stats.Tick(float.PositiveInfinity);
            Assert.That(Finite(stats.PlayTime), Is.True);
        }
    }
}
