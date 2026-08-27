using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using NUnit.Framework;
using UnityEngine;

namespace MeteorDefenseVR.Tests
{
    public sealed class GameSessionStatsTests
    {
        private GameObject root;
        private GameObject flowObject;
        private GameSessionStats stats;
        private ResultController result;
        private SessionRankSettings ranks;
        private GameFlowManager gameFlow;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("GameSessionStatsTest");
            flowObject = new GameObject("ResultFlowTest");
            stats = root.AddComponent<GameSessionStats>();
            result = root.AddComponent<ResultController>();
            gameFlow = flowObject.AddComponent<GameFlowManager>();
            ranks = ScriptableObject.CreateInstance<SessionRankSettings>();
            ranks.Configure(3000, 2400, 1500);
            result.Configure(stats, ranks);
            result.BindGameFlow(gameFlow);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (flowObject != null) Object.DestroyImmediate(flowObject);
            if (ranks != null) Object.DestroyImmediate(ranks);
        }

        [Test]
        public void SnapshotRecordsEveryRequiredMetricAndAccuracy()
        {
            stats.ResetSession(26);
            stats.SetScore(2800);
            stats.RecordShotAndLock();
            stats.RecordShotAndLock();
            stats.RecordSuccessfulHit();
            stats.RecordMeteorDestroyed();
            stats.RecordMeteorDestroyed();
            stats.RecordDamage(20);
            stats.Tick(42.5f);
            GameSessionSnapshot snapshot = stats.CreateSnapshot();

            Assert.That(snapshot.Score, Is.EqualTo(2800));
            Assert.That(snapshot.DestroyedMeteors, Is.EqualTo(2));
            Assert.That(snapshot.TotalMeteors, Is.EqualTo(26));
            Assert.That(snapshot.Shots, Is.EqualTo(2));
            Assert.That(snapshot.Locks, Is.EqualTo(2));
            Assert.That(snapshot.SuccessfulHits, Is.EqualTo(1));
            Assert.That(snapshot.AccuracyPercent, Is.EqualTo(50));
            Assert.That(snapshot.DamageTaken, Is.EqualTo(20));
            Assert.That(snapshot.MaxCombo, Is.EqualTo(2));
            Assert.That(snapshot.PlayTime, Is.EqualTo(42.5f));
        }

        [TestCase(3100, "S")]
        [TestCase(2500, "A")]
        [TestCase(1600, "B")]
        [TestCase(900, "C")]
        public void RankThresholdsAreConfigurable(int score, string expected)
        {
            Assert.That(ranks.Evaluate(new GameSessionSnapshot { Score = score }), Is.EqualTo(expected));
        }

        [Test]
        public void ResultStateFreezesSnapshotAndShowsFormattedResult()
        {
            stats.ResetSession(10);
            stats.SetScore(2500);
            stats.RecordShotAndLock();
            stats.RecordSuccessfulHit();
            stats.RecordMeteorDestroyed();

            gameFlow.ChangeState(GameState.Result);

            Assert.That(result.IsVisible, Is.True);
            Assert.That(result.Rank, Is.EqualTo("A"));
            string text = ResultView.Format(result.Snapshot, result.Rank);
            Assert.That(text, Does.Contain("MISSION RESULT"));
            Assert.That(text, Does.Contain("2,500"));
            Assert.That(text, Does.Contain("100%"));
            Assert.That(text, Does.Contain("RANK      A"));
        }
    }
}
