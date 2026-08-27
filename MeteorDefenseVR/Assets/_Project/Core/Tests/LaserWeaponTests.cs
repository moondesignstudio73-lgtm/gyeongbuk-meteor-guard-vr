using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Meteor;
using NUnit.Framework;
using UnityEngine;

namespace MeteorDefenseVR.Tests
{
    public sealed class LaserWeaponTests
    {
        private GameObject root;
        private LaserWeapon weapon;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("LaserWeaponTestRoot");
            weapon = root.AddComponent<LaserWeapon>();
            weapon.DamagePerShot = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void LockedNormalMeteor_FiresDestroysAndRequestsScorePopup()
        {
            GazeLockOnTarget target = CreateTarget(MeteorType.Normal, 1f, 250);
            int popupScore = 0;
            int hitCount = 0;
            weapon.ScorePopupRequested += (_, score) => popupScore = score;
            weapon.Hit += (_, __) => hitCount++;
            target.Advance(1f);

            bool fired = weapon.TryFire(target);

            Assert.That(fired, Is.True);
            Assert.That(target.Meteor.State, Is.EqualTo(MeteorLifecycleState.Destroyed));
            Assert.That(popupScore, Is.EqualTo(250));
            Assert.That(hitCount, Is.EqualTo(1));
        }

        [Test]
        public void MultiHitMeteor_UnlocksAfterShotForNextLock()
        {
            GazeLockOnTarget target = CreateTarget(MeteorType.Boss, 3f, 500);
            target.Advance(1f);

            Assert.That(weapon.TryFire(target), Is.True);

            Assert.That(target.Meteor.CurrentHealth, Is.EqualTo(2f));
            Assert.That(target.State, Is.EqualTo(LockOnState.Idle));
            Assert.That(target.Progress, Is.Zero);
        }

        [Test]
        public void Cooldown_PreventsDuplicateFireUntilElapsed()
        {
            GazeLockOnTarget target = CreateTarget(MeteorType.Boss, 3f, 500);
            target.Advance(1f);
            Assert.That(weapon.TryFire(target), Is.True);
            target.Advance(1f);

            Assert.That(weapon.TryFire(target), Is.False);
            weapon.TickCooldown(1f);
            Assert.That(weapon.TryFire(target), Is.True);
            Assert.That(target.Meteor.CurrentHealth, Is.EqualTo(1f));
        }

        [Test]
        public void BeamView_StopsAfterVisibleDuration()
        {
            GameObject beamObject = new GameObject("Beam");
            beamObject.transform.SetParent(root.transform);
            LineRenderer line = beamObject.AddComponent<LineRenderer>();
            LaserBeamView beam = beamObject.AddComponent<LaserBeamView>();
            beam.Configure(line, 0.1f);

            beam.Play(Vector3.zero, Vector3.forward);
            Assert.That(beam.IsPlaying, Is.True);
            Assert.That(line.enabled, Is.True);

            beam.Tick(0.2f);
            Assert.That(beam.IsPlaying, Is.False);
            Assert.That(line.enabled, Is.False);
        }

        private GazeLockOnTarget CreateTarget(MeteorType type, float health, int score)
        {
            GameObject meteorObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            meteorObject.name = type + "Meteor";
            meteorObject.transform.SetParent(root.transform);
            MeteorController meteor = meteorObject.AddComponent<MeteorController>();
            meteor.Configure(type, health, 0f, 10, score, 1f);
            GazeLockOnTarget target = meteorObject.AddComponent<GazeLockOnTarget>();
            target.Configure(meteor, 0.1f, 1f, 0.1f);
            meteor.Spawn();
            return target;
        }
    }
}
