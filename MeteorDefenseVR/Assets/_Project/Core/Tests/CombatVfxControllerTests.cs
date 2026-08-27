using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.VFX;
using NUnit.Framework;
using UnityEngine;

namespace MeteorDefenseVR.Tests
{
    public sealed class CombatVfxControllerTests
    {
        private GameObject root;
        private CombatVfxController controller;
        private Transform hit;
        private Transform explosion;
        private Transform boss;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("CombatVfxTests");
            controller = root.AddComponent<CombatVfxController>();
            hit = NewEffect("Hit");
            explosion = NewEffect("Explosion");
            boss = NewEffect("Boss");
            controller.Configure(null, null, hit, explosion, boss);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void DestroyedMeteor_PlaysHitAndExplosionSequence()
        {
            GameObject meteorObject = new GameObject("Meteor");
            meteorObject.transform.SetParent(root.transform);
            MeteorController meteor = meteorObject.AddComponent<MeteorController>();
            meteor.Configure(MeteorType.Normal, 1f, 0f, 10, 100, 1f);
            meteor.Spawn();
            meteor.DestroyMeteor();

            controller.PlayHitFeedback(meteor, Vector3.forward * 4f);

            Assert.That(controller.HitActive, Is.True);
            Assert.That(controller.ExplosionActive, Is.True);
            Assert.That(hit.gameObject.activeSelf, Is.True);
            Assert.That(explosion.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void Effects_AutoHideAfterLifetime()
        {
            controller.PlayBossExplosion(Vector3.one);
            Assert.That(controller.BossExplosionActive, Is.True);

            controller.Tick(2f);

            Assert.That(controller.BossExplosionActive, Is.False);
            Assert.That(boss.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void ResetEffects_ImmediatelyClearsAllVisuals()
        {
            controller.PlayHitFeedback(null, Vector3.zero);
            controller.PlayBossExplosion(Vector3.zero);

            controller.ResetEffects();

            Assert.That(controller.HitActive, Is.False);
            Assert.That(controller.BossExplosionActive, Is.False);
            Assert.That(hit.gameObject.activeSelf, Is.False);
            Assert.That(boss.gameObject.activeSelf, Is.False);
        }

        private Transform NewEffect(string name)
        {
            GameObject effect = new GameObject(name);
            effect.transform.SetParent(root.transform);
            return effect.transform;
        }
    }
}
