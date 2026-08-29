using System;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.Education;
using NUnit.Framework;

namespace MeteorDefenseVR.Tests
{
    public sealed class EmergencyTrainingTests
    {
        [TestCase(ThreatDirection.Front,TrainingFault.HullPressure)]
        [TestCase(ThreatDirection.Left,TrainingFault.Electrical)]
        [TestCase(ThreatDirection.Right,TrainingFault.Communication)]
        [TestCase(ThreatDirection.Above,TrainingFault.Oxygen)]
        [TestCase(ThreatDirection.Below,TrainingFault.HullPressure)]
        [TestCase(ThreatDirection.Rear,TrainingFault.Propulsion)]
        public void ActualImpactDirectionSelectsSubsystem(ThreatDirection direction,TrainingFault fault)
        {var state=new EmergencyTrainingState();state.Impact(direction,25);Assert.That(state.Kind,Is.EqualTo(fault));Assert.That(state.Condition,Is.EqualTo(75));}
        [Test] public void WrongActionWorsensAndCorrectActionStopsDeterioration()
        {
            var s=new EmergencyTrainingState();var settings=new EmergencyTrainingSettings();s.Impact(ThreatDirection.Front,40);
            s.Tick(5,settings);Assert.That(s.Condition,Is.LessThan(60));float before=s.Condition;s.WrongAction();Assert.That(s.Condition,Is.LessThan(before));
            s.Mitigate();before=s.Condition;s.Tick(100,settings);Assert.That(s.Condition,Is.EqualTo(before));Assert.That(s.Resolved,Is.True);
        }
        [Test] public void ImmediateImpactThenLifeSupportThenMinorFault()
        {
            var s=new EmergencyTrainingState();var settings=new EmergencyTrainingSettings();s.Impact(ThreatDirection.Front,50);
            Assert.That(s.TriageChoice(3,settings),Is.EqualTo(2));Assert.That(s.TriageChoice(15,settings),Is.Zero);
            s.Mitigate();Assert.That(s.TriageChoice(15,settings),Is.EqualTo(1));
            settings.immediateCollisionSeconds=2;Assert.That(s.TriageChoice(3,settings),Is.EqualTo(1));
        }
        [Test] public void OperatorAbortDoesNotInventUnansweredFailures()
        {
            var log=new EducationSessionLog(EducationSessionResult.Create(new ParticipantInfo{Name="테스트",Number="1"},DifficultyLevel.Normal,DateTimeOffset.UtcNow));
            var question=log.Question(EducationStage.Diagnosis,1,EducationQuestionKind.Cause,"hull",0);
            log.Close(false,3,777,DateTimeOffset.UtcNow);
            Assert.That(question.Cancelled,Is.True);Assert.That(question.Attempts,Is.Zero);Assert.That(question.TimedOut,Is.False);
            Assert.That(EducationAssessment.Calculate(log.Result).EmergencyQuestions,Is.Zero);Assert.That(log.Result.GameScore,Is.EqualTo(777));
        }
        [Test] public void ExpiredDefenseCannotInflateAvoidanceOrInventCollision()
        {
            var log=new EducationSessionLog(EducationSessionResult.Create(new ParticipantInfo{Name="테스트",Number="1"},DifficultyLevel.Normal,DateTimeOffset.UtcNow));
            var t=log.AddThreat(EducationStage.Prediction,1,ThreatDirection.Left,true,true,false,0,default);
            log.Resolve(t,ThreatOutcome.Missed,10);log.Close(true,10,0,DateTimeOffset.UtcNow);
            var a=EducationAssessment.Calculate(log.Result);Assert.That(a.DefenseRequired,Is.EqualTo(1));Assert.That(a.DefenseRate,Is.Zero);Assert.That(a.Collisions,Is.Zero);Assert.That(a.DefenseMissed,Is.EqualTo(1));
        }
        [Test] public void CorruptedCorrectFlagCannotCreateAnInventedGrade()
        {
            var log=new EducationSessionLog(EducationSessionResult.Create(new ParticipantInfo{Name="테스트",Number="1"},DifficultyLevel.Normal,DateTimeOffset.UtcNow));
            var q=log.Question(EducationStage.Diagnosis,1,EducationQuestionKind.Cause,"cause",0);log.Answer(q,1,0,1);log.Close(true,2,0,DateTimeOffset.UtcNow);
            Assert.That(EducationResultStore.Valid(log.Result),Is.True);q.Correct=true;Assert.That(EducationResultStore.Valid(log.Result),Is.False);
        }
    }
}
