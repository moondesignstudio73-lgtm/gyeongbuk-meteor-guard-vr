using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Meteor;
using NUnit.Framework;
using UnityEngine;

namespace MeteorDefenseVR.Tests
{
    public sealed class GazeLockOnTargetTests
    {
        private GameObject root;
        private MeteorController meteor;
        private GazeLockOnTarget target;

        [SetUp]
        public void SetUp()
        {
            root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            meteor = root.AddComponent<MeteorController>();
            meteor.Configure(MeteorType.Normal, 1f, 0f, 10, 100, 1f);
            target = root.AddComponent<GazeLockOnTarget>();
            target.Configure(meteor, 0.7f, 2f, 0.15f);
            meteor.Spawn();
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void Dwell_ReachesLockedStateAndFiresOnce()
        {
            int lockedCount = 0;
            target.Locked += _ => lockedCount++;

            target.Advance(0.35f);
            Assert.That(target.Progress, Is.EqualTo(0.5f).Within(0.001f));
            target.Advance(0.35f);
            target.Advance(1f);

            Assert.That(target.State, Is.EqualTo(LockOnState.Locked));
            Assert.That(target.Progress, Is.EqualTo(1f));
            Assert.That(lockedCount, Is.EqualTo(1));
        }

        [Test]
        public void BriefGazeLoss_UsesGraceBeforeDecaying()
        {
            target.Advance(0.35f);
            target.OnGazeExit();

            target.TickWithoutGaze(0.1f);
            Assert.That(target.Progress, Is.EqualTo(0.5f).Within(0.001f));
            target.TickWithoutGaze(0.2f);

            Assert.That(target.Progress, Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(target.State, Is.EqualTo(LockOnState.Focusing));
        }

        [Test]
        public void FocusMovingToAnotherTarget_ReleasesPreviousTarget()
        {
            target.Advance(0.35f);
            GameObject secondObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            secondObject.transform.SetParent(root.transform);
            MeteorController secondMeteor = secondObject.AddComponent<MeteorController>();
            secondMeteor.Configure(MeteorType.Normal, 1f, 0f, 10, 100, 1f);
            GazeLockOnTarget second = secondObject.AddComponent<GazeLockOnTarget>();
            second.Configure(secondMeteor);
            secondMeteor.Spawn();

            second.OnGazeEnter(default);

            Assert.That(target.State, Is.EqualTo(LockOnState.Idle));
            Assert.That(target.Progress, Is.Zero);
            Assert.That(second.State, Is.EqualTo(LockOnState.Focusing));
        }

        [Test]
        public void DestroyedMeteor_DisablesFurtherLockProgress()
        {
            target.Advance(1f);
            meteor.ReceiveDamage(1f);
            target.Advance(1f);

            Assert.That(target.State, Is.EqualTo(LockOnState.Destroyed));
            Assert.That(target.Progress, Is.Zero);
            Assert.That(GazeLockOnTarget.ActiveTarget, Is.Null);
            Assert.That(target.IsLocked, Is.False);
        }
    }
}
