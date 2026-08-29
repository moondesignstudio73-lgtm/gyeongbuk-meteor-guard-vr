using MeteorDefenseVR.Combat;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.PowerUps;
using MeteorDefenseVR.UI;
using NUnit.Framework;
using UnityEngine;

namespace MeteorDefenseVR.Tests
{
    public sealed class PowerUpSystemTests
    {
        private GameObject root;
        private PowerUpManager manager;
        private PlayerHealth health;
        private MeteorSpawner spawner;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("PowerUpFixture");
            spawner = root.AddComponent<MeteorSpawner>();
            var boss = root.AddComponent<BossClimaxController>();
            var hud = root.AddComponent<MissionHudController>();
            var weapon = root.AddComponent<LaserWeapon>();
            var gaze = root.AddComponent<GazeRaycaster>();
            health = root.AddComponent<PlayerHealth>();
            health.Configure(spawner, hud, 100f, 10, 0f, GameOverPolicy.NormalMode);
            manager = root.AddComponent<PowerUpManager>();
            manager.Configure(Resources.Load<PowerUpSettings>("PowerUpSettings"), spawner, boss, health, weapon, gaze, hud, null);
        }

        [TearDown]
        public void TearDown() { if (root != null) Object.DestroyImmediate(root); }

        [Test]
        public void ConfigUnlocksItemsGraduallyAndContainsAllEightTypes()
        {
            PowerUpSettings settings = Resources.Load<PowerUpSettings>("PowerUpSettings");
            Assert.That(settings, Is.Not.Null); Assert.That(settings.definitions.Length, Is.EqualTo(8));
            foreach (PowerUpType type in System.Enum.GetValues(typeof(PowerUpType))) Assert.That(settings.Find(type), Is.Not.Null, type.ToString());
            Assert.That(settings.Find(PowerUpType.Heal).spawnStage, Is.EqualTo(1));
            Assert.That(settings.Find(PowerUpType.MultiLaser).spawnStage, Is.InRange(4, 6));
            Assert.That(settings.Find(PowerUpType.Overdrive).spawnStage, Is.InRange(7, 9));
            Assert.That(settings.Find(PowerUpType.EMP).spawnStage, Is.InRange(7, 10));
        }

        [Test]
        public void ShieldBlocksExactlyOneDamageEventAndHealNeverExceedsMaximum()
        {
            manager.Activate(PowerUpType.Shield);
            Assert.That(health.ApplyDamage(25), Is.False); Assert.That(health.CurrentHealth, Is.EqualTo(100));
            manager.ResetSystem(); Assert.That(health.ApplyDamage(50), Is.True); Assert.That(health.CurrentHealth, Is.EqualTo(50));
            Assert.That(manager.Activate(PowerUpType.Heal), Is.True); Assert.That(health.CurrentHealth, Is.EqualTo(70).Within(.01f));
            manager.Activate(PowerUpType.Heal); manager.Activate(PowerUpType.Heal); Assert.That(health.CurrentHealth, Is.LessThanOrEqualTo(100));
        }

        [Test]
        public void TimedBuffsStackButNeverExceedConfiguredThreeSlotsAndResetCleansThem()
        {
            manager.Activate(PowerUpType.DamageBoost); manager.Activate(PowerUpType.RapidFire);
            manager.Activate(PowerUpType.MultiLaser); manager.Activate(PowerUpType.Overdrive);
            Assert.That(manager.ActiveBuffCount, Is.EqualTo(3));
            manager.ResetSystem(); Assert.That(manager.ActiveBuffCount, Is.Zero); Assert.That(manager.ActiveWorldItems, Is.Zero);
        }

        [Test]
        public void PityGuaranteesAWorldItemByConfiguredMaximumMissesAndPoolBoundsWorldItems()
        {
            var meteorObject = new GameObject("DropMeteor"); meteorObject.transform.SetParent(root.transform); var meteor = meteorObject.AddComponent<MeteorController>();
            PowerUpSettings settings = Resources.Load<PowerUpSettings>("PowerUpSettings");
            for (int i = 1; i < settings.pityGuaranteedAt; i++) Assert.That(manager.TryDrop(meteor, false, 1f), Is.False, "miss " + i);
            Assert.That(manager.TryDrop(meteor, false, 1f), Is.True); Assert.That(manager.ActiveWorldItems, Is.EqualTo(1));
            manager.TryDrop(meteor, true, 0f); manager.TryDrop(meteor, true, 0f);
            Assert.That(manager.ActiveWorldItems, Is.LessThanOrEqualTo(settings.maximumWorldItems));
            Assert.That(manager.TryDrop(meteor, true, 0f), Is.True, "boss reward replaces the oldest world item when the pool is full");
            Assert.That(manager.ActiveWorldItems, Is.EqualTo(settings.maximumWorldItems));
        }

        [Test]
        public void BlinkRequiresOpenRearmWhileMouseCanTriggerAgainAfterCooldown()
        {
            bool armed = true;
            Assert.That(BlinkTriggerPolicy.ShouldFire(ref armed, true, true, false, true), Is.True);
            Assert.That(BlinkTriggerPolicy.ShouldFire(ref armed, true, true, false, true), Is.False, "closed hold must not repeat");
            Assert.That(BlinkTriggerPolicy.ShouldFire(ref armed, true, false, false, true), Is.False);
            Assert.That(BlinkTriggerPolicy.ShouldFire(ref armed, true, true, false, true), Is.True, "open state rearms the next blink");
            Assert.That(BlinkTriggerPolicy.ShouldFire(ref armed, false, false, true, true), Is.True);
            Assert.That(BlinkTriggerPolicy.ShouldFire(ref armed, false, false, true, false), Is.False, "cooldown gates click mode too");
        }

        [Test]
        public void SlowTimeOnlyChangesMeteorMovementNotGlobalTimeScale()
        {
            float original = Time.timeScale;
            manager.Activate(PowerUpType.SlowTime);
            Assert.That(manager.IsBuffActive(PowerUpType.SlowTime), Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(original));
        }
    }
}
