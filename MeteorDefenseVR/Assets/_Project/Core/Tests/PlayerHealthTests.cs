using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.UI;
using NUnit.Framework;
using UnityEngine;

namespace MeteorDefenseVR.Tests
{
    public sealed class PlayerHealthTests
    {
        private GameObject root;
        private GameObject target;
        private MeteorSpawner spawner;
        private MissionHudController hud;
        private PlayerHealth health;
        private MeteorWaveData wave;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("PlayerHealthTest");
            target = new GameObject("PlayerHealthTarget");
            spawner = root.AddComponent<MeteorSpawner>();
            hud = root.AddComponent<MissionHudController>();
            health = root.AddComponent<PlayerHealth>();

            var entry = new MeteorSpawnData();
            entry.Configure(MeteorType.Normal, null, 1, 0f, 0f, 0f, 1f, 25, 100, 1f, new Vector2(20f, 15f));
            wave = ScriptableObject.CreateInstance<MeteorWaveData>();
            wave.Configure("Health Test", 0f, entry);
            spawner.Configure(new[] { wave }, root.transform, target.transform);
            hud.Configure(spawner, null, 100f);
            health.Configure(spawner, hud, 100f, 10, 0.5f, GameOverPolicy.NoFailExperienceMode);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (target != null) Object.DestroyImmediate(target);
            if (wave != null) Object.DestroyImmediate(wave);
        }

        [Test]
        public void InvincibilityPreventsRapidRepeatedDamage()
        {
            Assert.That(health.ApplyDamage(10), Is.True);
            Assert.That(health.ApplyDamage(10), Is.False);
            Assert.That(health.CurrentHealth, Is.EqualTo(90f));

            health.Tick(0.5f);
            Assert.That(health.ApplyDamage(10), Is.True);
            Assert.That(health.CurrentHealth, Is.EqualTo(80f));
        }

        [Test]
        public void MeteorReachingPlayerUsesConfiguredMeteorDamageAndUpdatesHud()
        {
            spawner.BeginSpawning();
            spawner.Tick(0f);
            spawner.ActiveMeteors[0].ReachPlayer();

            Assert.That(health.CurrentHealth, Is.EqualTo(75f));
            Assert.That(hud.CurrentHealth, Is.EqualTo(75f));
            Assert.That(hud.FeedbackMessage, Is.EqualTo("WARNING"));
        }

        [Test]
        public void NormalModeFailsOnceAtZeroHealth()
        {
            int failures = 0;
            health.SetPolicy(GameOverPolicy.NormalMode);
            health.MissionFailed += () => failures++;

            health.ApplyDamage(100);

            Assert.That(health.HealthDepleted, Is.True);
            Assert.That(health.IsMissionFailed, Is.True);
            Assert.That(failures, Is.EqualTo(1));
            Assert.That(hud.FeedbackMessage, Is.EqualTo("MISSION FAILED"));
            Assert.That(health.ApplyDamage(10), Is.False);
        }

        [Test]
        public void NoFailModeKeepsMissionActiveAtZeroHealth()
        {
            int depletedEvents = 0;
            health.HealthDepletedInNoFailMode += () => depletedEvents++;

            health.ApplyDamage(100);

            Assert.That(health.CurrentHealth, Is.Zero);
            Assert.That(health.HealthDepleted, Is.True);
            Assert.That(health.IsMissionFailed, Is.False);
            Assert.That(depletedEvents, Is.EqualTo(1));
            Assert.That(hud.FeedbackMessage, Does.Contain("계속 방어"));
        }
    }
}
