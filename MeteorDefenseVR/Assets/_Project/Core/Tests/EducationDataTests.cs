using System;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.Education;
using NUnit.Framework;
using UnityEngine;

namespace MeteorDefenseVR.Tests
{
    public sealed class EducationDataTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);
        private static EducationSessionLog NewLog() => new EducationSessionLog(EducationSessionResult.Create(new ParticipantInfo { Name = "테스트", Number = "01" }, DifficultyLevel.Normal, Now));
        private static ThreatRisk Risk(float distance = 50, float speed = 2) => ThreatRiskCalculator.Evaluate(Vector3.forward * distance, Vector3.back * speed, 5, 10, new ThreatRiskSettings());
        [Test] public void FarFastThreatCanBeMoreUrgentThanNearSlowThreat()
        { var slow = Risk(50, .5f); var fast = Risk(110, 8); Assert.That(fast.Ttc, Is.LessThan(slow.Ttc)); Assert.That(fast.Danger, Is.GreaterThan(slow.Danger)); }
        [Test] public void PassingAndRecedingObjectsAreNotCollisionThreats()
        {
            Assert.That(ThreatRiskCalculator.Evaluate(new Vector3(20, 0, 50), Vector3.back * 8, 5, 10, null).CanCollide, Is.False);
            Assert.That(ThreatRiskCalculator.Evaluate(Vector3.forward * 50, Vector3.forward, 5, 10, null).CanCollide, Is.False);
            Assert.That(ThreatRiskCalculator.Evaluate(Vector3.zero, Vector3.zero, 5, 10, null).Ttc, Is.Zero);
        }
        [Test] public void RealLogDeterminesDetectionReactionDefenseAndFirstAnswerOnly()
        {
            var log = NewLog(); var a = log.AddThreat(EducationStage.Orientation, 1, ThreatDirection.Left, true, false, false, 1, Risk());
            var b = log.AddThreat(EducationStage.Prediction, 2, ThreatDirection.Rear, true, true, false, 3, Risk());
            log.Detect(a, 3); log.Resolve(a, ThreatOutcome.Observed, 3); log.WrongDetection(b, 4);
            log.Action(b, 4); log.Resolve(b, ThreatOutcome.Collision, 10);
            Assert.That(log.Resolve(b, ThreatOutcome.Defended, 11), Is.False);
            var q = log.Question(EducationStage.Diagnosis, 3, EducationQuestionKind.Emergency, "hull", 10);
            log.Answer(q, 1, 0, 12); log.Answer(q, 0, 0, 14); log.Close(true, 15, 999999, Now.AddSeconds(15));
            var result = EducationAssessment.Calculate(log.Result);
            Assert.That(result.DetectionAccuracy, Is.EqualTo(1f / 3).Within(.0001));
            Assert.That(result.MeanReaction, Is.EqualTo(2)); Assert.That(result.ReactionSamples, Is.EqualTo(1));
            Assert.That(result.DefenseRate, Is.Zero); Assert.That(result.Collisions, Is.EqualTo(1)); Assert.That(result.EmergencyAccuracy, Is.Zero);
            Assert.That(result.Score, Is.EqualTo(10).Within(.001), "999999 game points must not inflate education score");
            Assert.That(q.Attempts, Is.EqualTo(2)); Assert.That(q.Selected, Is.EqualTo(1));
        }
        [Test] public void MissingEvidenceNeverDisplaysInventedReactionOrPerfectScore()
        { var log = NewLog(); log.Close(false, 0, 0, Now); var a = EducationAssessment.Calculate(log.Result); Assert.That(a.MeanReaction, Is.EqualTo(-1)); Assert.That(a.Score, Is.Zero); Assert.That(a.Weakest, Is.EqualTo("자료 없음")); }
        [Test] public void DemonstrationAndCancelledThreatsCannotGrantDefenseSuccess()
        {
            var log = NewLog(); var t = log.AddThreat(EducationStage.Diagnosis, 1, ThreatDirection.Front, true, true, true, 0, Risk());
            log.Resolve(t, ThreatOutcome.Collision, 3); var cancelled = log.AddThreat(EducationStage.Prediction, 2, ThreatDirection.Rear, true, true, false, 4, Risk());
            log.Close(false, 5, 0, Now.AddSeconds(5)); var a = EducationAssessment.Calculate(log.Result);
            Assert.That(cancelled.Outcome, Is.EqualTo(ThreatOutcome.Cancelled)); Assert.That(a.DefenseRequired, Is.Zero); Assert.That(a.Threats, Is.Zero);
        }
        [Test] public void DirectionalWeaknessUsesMeasuredDirectionsOnly()
        {
            var log = NewLog(); var front = log.AddThreat(EducationStage.Orientation, 1, ThreatDirection.Front, true, false, false, 0, Risk());
            var rear = log.AddThreat(EducationStage.Orientation, 2, ThreatDirection.Rear, true, false, false, 5, Risk());
            log.Detect(front, 1); log.Resolve(front, ThreatOutcome.Observed, 1); log.Resolve(rear, ThreatOutcome.Missed, 12); log.Close(true, 12, 0, Now.AddSeconds(12));
            var a = EducationAssessment.Calculate(log.Result); Assert.That(a.Weakest, Is.EqualTo("후방 위험 탐지")); Assert.That(a.Strongest, Is.EqualTo("전방 위험 탐지"));
            Assert.That(LearningFeedbackGenerator.Generate(log.Result, a).Count, Is.InRange(1, 3));
        }
        [TestCase("=2+2")] [TestCase("+cmd")] [TestCase("@SUM(A1)")] [TestCase("-1+2")] [TestCase("  =1")]
        public void CsvTreatsParticipantValuesAsText(string input) => Assert.That(EducationCsvExporter.Cell(input), Does.StartWith("\"'"));
        [Test] public void StoreReloadBackupFutureVersionRetentionAndCsvBom()
        {
            string dir = Path.GetFullPath("Docs/EducationQA/TestStorage/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "records.json");
            try
            {
                var first = NewLog(); first.Close(true, 10, 20, Now.AddSeconds(10));
                var second = NewLog(); second.Result.StartedAt = Now.AddSeconds(30).ToString("O"); second.Close(true, 10, 30, Now.AddSeconds(40));
                var store = new EducationResultStore(path, 2, 30); store.Load(Now); Assert.That(store.Add(first.Result, Now), Is.True); Assert.That(store.Add(first.Result, Now), Is.False); store.Add(second.Result, Now);
                var loaded = new EducationResultStore(path); loaded.Load(Now); Assert.That(loaded.Records.Count, Is.EqualTo(2));
                first.Result.Participant.Name = "변경"; Assert.That(loaded.Records.All(r => r.Participant.Name == "테스트"), Is.True);
                File.WriteAllText(path, "{broken"); loaded.Load(Now); Assert.That(loaded.Recovery, Is.EqualTo(EducationResultStore.RecoveryStatus.Backup)); Assert.That(loaded.Records.Count, Is.EqualTo(1));
                Assert.That(EducationCsvExporter.Write(Path.Combine(dir, "output.csv"), loaded.Records), Is.True); byte[] bytes = File.ReadAllBytes(Path.Combine(dir, "output.csv")); Assert.That(bytes.Take(3), Is.EqualTo(new byte[] { 239, 187, 191 }));
                Assert.That(loaded.Clear(), Is.True); loaded.Load(Now); Assert.That(loaded.Records.Count, Is.Zero);
                File.WriteAllText(path, "{\"Version\":99,\"Sessions\":[]}"); loaded.Load(Now); Assert.That(loaded.Recovery, Is.EqualTo(EducationResultStore.RecoveryStatus.FutureVersion)); Assert.That(loaded.Clear(), Is.False);
                var memory = new EducationResultStore(null, 1, 1); memory.Add(first.Result, Now.AddDays(3)); Assert.That(memory.Records.Count, Is.Zero);
            }
            finally { foreach (string file in new[] { path, path + ".bak", path + ".tmp", Path.Combine(dir, "output.csv") }) if (File.Exists(file)) File.Delete(file); Directory.Delete(dir); }
        }
    }
}
