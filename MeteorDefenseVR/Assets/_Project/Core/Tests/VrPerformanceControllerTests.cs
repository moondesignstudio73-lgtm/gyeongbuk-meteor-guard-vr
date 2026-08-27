using MeteorDefenseVR.Core;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.VFX;
using NUnit.Framework;
using UnityEngine;

namespace MeteorDefenseVR.Tests
{
    public sealed class VrPerformanceControllerTests
    {
        private GameObject root;

        [SetUp]
        public void SetUp() => root = new GameObject("VrPerformanceTests");

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void Configure_TracksPoolingAndReusableVfxDependencies()
        {
            MeteorSpawner spawner = root.AddComponent<MeteorSpawner>();
            CombatVfxController vfx = root.AddComponent<CombatVfxController>();
            VrPerformanceController performance = root.AddComponent<VrPerformanceController>();

            performance.Configure(spawner, vfx, 90);

            Assert.That(performance.UsesMeteorPooling, Is.True);
            Assert.That(performance.UsesReusableCombatVfx, Is.True);
            Assert.That(performance.TargetFrameRate, Is.EqualTo(90));
        }

        [TestCase(60, 72)]
        [TestCase(90, 90)]
        [TestCase(144, 120)]
        public void Configure_ClampsTargetFrameRateToVrRange(int requested, int expected)
        {
            VrPerformanceController performance = root.AddComponent<VrPerformanceController>();

            performance.Configure(null, null, requested);

            Assert.That(performance.TargetFrameRate, Is.EqualTo(expected));
            Assert.That(Physics.reuseCollisionCallbacks, Is.True);
        }
    }
}
