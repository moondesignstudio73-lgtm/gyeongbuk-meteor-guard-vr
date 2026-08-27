using System.Collections;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Tutorial;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeteorDefenseVR.Tests
{
    public sealed class PracticeControllerTests
    {
        private GameObject root;
        private GameObject meteorSource;
        private PracticeController controller;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("PracticeTestRoot");
            controller = root.AddComponent<PracticeController>();
            controller.MeteorCount = 2;
            controller.InterMeteorDelay = 0f;
            controller.ReadyDisplayDuration = 0f;

            meteorSource = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            meteorSource.transform.SetParent(root.transform);
            MeteorController sourceMeteor = meteorSource.AddComponent<MeteorController>();
            sourceMeteor.Configure(MeteorType.Normal, 1f, 0f, 0, 100, 1f);
            GazeLockOnTarget lockTarget = meteorSource.AddComponent<GazeLockOnTarget>();
            lockTarget.Configure(sourceMeteor, 0.1f);
            controller.Configure(meteorSource, new[] { root.transform, root.transform });
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void BeginPractice_SpawnsFirstMeteorAndInitializesHudData()
        {
            controller.BeginPractice();

            Assert.That(controller.IsRunning, Is.True);
            Assert.That(controller.TotalMeteors, Is.EqualTo(2));
            Assert.That(controller.RemainingMeteors, Is.EqualTo(2));
            Assert.That(controller.PracticeScore, Is.Zero);
            Assert.That(controller.ActiveMeteor, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator DestroyingConfiguredCount_CompletesPractice()
        {
            controller.BeginPractice();
            controller.ActiveMeteor.ReceiveDamage(1f);
            yield return null;
            Assert.That(controller.DestroyedMeteors, Is.EqualTo(1));
            Assert.That(controller.ActiveMeteor, Is.Not.Null);

            controller.ActiveMeteor.ReceiveDamage(1f);
            yield return null;

            Assert.That(controller.IsComplete, Is.True);
            Assert.That(controller.RemainingMeteors, Is.Zero);
            Assert.That(controller.PracticeScore, Is.EqualTo(200));
            Assert.That(controller.StatusMessage, Is.EqualTo("준비 완료!"));
        }

        [UnityTest]
        public IEnumerator PracticeScore_IsExcludedFromSessionByDefaultAndOptional()
        {
            controller.BeginPractice();
            controller.ActiveMeteor.ReceiveDamage(1f);
            yield return null;

            Assert.That(controller.PracticeScore, Is.EqualTo(100));
            Assert.That(controller.ScoreForSession, Is.Zero);
            controller.IncludePracticeScoreInSession = true;
            Assert.That(controller.ScoreForSession, Is.EqualTo(100));
        }
    }
}
