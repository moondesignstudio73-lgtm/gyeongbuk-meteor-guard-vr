using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.UI;
using NUnit.Framework;
using UnityEngine;

namespace MeteorDefenseVR.Tests
{
    public sealed class MissionHudTests
    {
        private GameObject root;
        private GameObject target;
        private MeteorSpawner spawner;
        private MissionHudController hud;
        private MeteorWaveData wave;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("MissionHudTest");
            target = new GameObject("MissionHudTarget");
            spawner = root.AddComponent<MeteorSpawner>();
            hud = root.AddComponent<MissionHudController>();

            var entry = new MeteorSpawnData();
            entry.Configure(MeteorType.Normal, null, 1, 0f, 0f, 0f, 1f, 10, 100, 1f, new Vector2(20f, 15f));
            wave = ScriptableObject.CreateInstance<MeteorWaveData>();
            wave.Configure("HUD Test", 0f, entry);
            spawner.Configure(new[] { wave }, root.transform, target.transform);
            hud.Configure(spawner, null, 100f);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (target != null) Object.DestroyImmediate(target);
            if (wave != null) Object.DestroyImmediate(wave);
        }

        [Test]
        public void SessionDisplaysTotalRemainingAndAwardedScore()
        {
            spawner.BeginSpawning();
            spawner.Tick(0f);
            Assert.That(hud.TotalMeteors, Is.EqualTo(1));
            Assert.That(hud.RemainingMeteors, Is.EqualTo(1));

            spawner.ActiveMeteors[0].DestroyMeteor();

            Assert.That(hud.RemainingMeteors, Is.Zero);
            Assert.That(hud.Score, Is.EqualTo(100));
            Assert.That(hud.FeedbackMessage, Does.Contain("+100"));
            Assert.That(hud.FeedbackMessage, Does.Contain("GOOD!"));
        }

        [Test]
        public void HighValueScoreUsesNiceFeedbackAndExpires()
        {
            hud.AddScore(500);
            Assert.That(hud.FeedbackMessage, Is.EqualTo("+500  NICE!"));

            hud.Tick(2f);
            Assert.That(hud.FeedbackMessage, Is.Empty);
        }

        [Test]
        public void HealthIsClampedAndFormattedAsEightSegments()
        {
            hud.SetHealth(50f, 100f);
            Assert.That(hud.HealthNormalized, Is.EqualTo(0.5f));
            Assert.That(MissionHudView.FormatHealth(50f, 100f), Is.EqualTo("HP\n████□□□□"));
        }

        [Test]
        public void HudTextFormattingMatchesStoryboardLayout()
        {
            Assert.That(MissionHudView.FormatMeteorProgress(15, 15), Is.EqualTo("남은 운석\n15 / 15"));
            Assert.That(MissionHudView.FormatScore(1500), Is.EqualTo("SCORE\n1,500"));
        }
    }
}
