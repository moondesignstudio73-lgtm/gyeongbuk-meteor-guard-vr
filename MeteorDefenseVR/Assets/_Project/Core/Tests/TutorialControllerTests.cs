using System.Collections;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Tutorial;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeteorDefenseVR.Tests
{
    public sealed class TutorialControllerTests
    {
        private GameObject root;
        private GameObject meteorSource;
        private TutorialController controller;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("TutorialTestRoot");
            controller = root.AddComponent<TutorialController>();
            meteorSource = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            meteorSource.name = "TutorialMeteorSource";
            meteorSource.transform.SetParent(root.transform);
            MeteorController sourceMeteor = meteorSource.AddComponent<MeteorController>();
            sourceMeteor.Configure(MeteorType.Tutorial, 1f, 0f, 0, 0, 1f);
            GazeLockOnTarget target = meteorSource.AddComponent<GazeLockOnTarget>();
            target.Configure(sourceMeteor, 0.1f);
            controller.Configure(meteorSource, root.transform);
            controller.SuccessDisplayDuration = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void BeginTutorial_SpawnsSafeStationaryTutorialMeteor()
        {
            controller.BeginTutorial();

            Assert.That(controller.IsRunning, Is.True);
            Assert.That(controller.Instruction, Is.EqualTo("운석을 바라보세요."));
            Assert.That(controller.ActiveMeteor, Is.Not.Null);
            Assert.That(controller.ActiveMeteor.MeteorType, Is.EqualTo(MeteorType.Tutorial));
            Assert.That(controller.ActiveMeteor.Damage, Is.Zero);
            Assert.That(controller.ActiveMeteor.Score, Is.Zero);
            Assert.That(controller.ActiveMeteor.Speed, Is.Zero);
        }

        [Test]
        public void TickAfterDelay_ShowsReminderAndPulse()
        {
            controller.ReminderDelay = 2f;
            int reminders = 0;
            controller.ReminderActivated += () => reminders++;
            controller.BeginTutorial();

            controller.Tick(2.1f);

            Assert.That(controller.Instruction, Is.EqualTo("운석을 바라봐 주세요."));
            Assert.That(reminders, Is.EqualTo(1));
            Assert.That(controller.ActiveMeteor.GetComponent<TutorialPulseView>().IsPulsing, Is.True);
        }

        [UnityTest]
        public IEnumerator DestroyingTutorialMeteor_CompletesWithoutFailureAndShowsSuccess()
        {
            int successes = 0;
            controller.TutorialSucceeded += () => successes++;
            controller.BeginTutorial();

            controller.ActiveMeteor.ReceiveDamage(1f);
            yield return null;

            Assert.That(controller.IsComplete, Is.True);
            Assert.That(controller.IsRunning, Is.False);
            Assert.That(successes, Is.EqualTo(1));
            Assert.That(controller.Instruction, Does.StartWith("좋아요!"));
        }
    }
}
