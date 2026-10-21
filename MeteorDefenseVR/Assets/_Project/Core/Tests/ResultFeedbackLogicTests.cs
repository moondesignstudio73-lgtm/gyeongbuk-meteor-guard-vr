using MeteorDefenseVR.GameFlow;
using NUnit.Framework;
using UnityEngine;
namespace MeteorDefenseVR.Tests
{
    public sealed class ResultFeedbackLogicTests
    {
        [TestCase(0,"C","다음 B등급까지 1,500점")]
        [TestCase(1499,"C","다음 B등급까지 1점")]
        [TestCase(1500,"B","다음 A등급까지 900점")]
        [TestCase(1501,"B","다음 A등급까지 899점")]
        [TestCase(2399,"B","다음 A등급까지 1점")]
        [TestCase(2400,"A","다음 S등급까지 600점")]
        [TestCase(2401,"A","다음 S등급까지 599점")]
        [TestCase(2999,"A","다음 S등급까지 1점")]
        [TestCase(3000,"S","최고 S등급!")]
        [TestCase(3001,"S","최고 S등급!")]
        [TestCase(int.MaxValue,"S","최고 S등급!")]
        public void GoalsMatchRankBoundaries(int score,string rank,string goal)
        {
            var settings=ScriptableObject.CreateInstance<SessionRankSettings>();
            try{var s=new GameSessionSnapshot{Score=score};Assert.That(settings.Evaluate(s),Is.EqualTo(rank));Assert.That(settings.NextGoal(s),Is.EqualTo(goal));}
            finally{Object.DestroyImmediate(settings);}
        }
        [Test]public void ConfiguredThresholdsDriveGoals()
        {var settings=ScriptableObject.CreateInstance<SessionRankSettings>();try{settings.Configure(8000,5000,2000);Assert.That(settings.NextGoal(new GameSessionSnapshot{Score=2000}),Is.EqualTo("다음 A등급까지 3,000점"));}finally{Object.DestroyImmediate(settings);}}
        [Test]public void FeedbackNeverInventsSuccessOrStreak()
        {
            var s=new GameSessionSnapshot{MissionFailed=true,AllStagesCleared=true,SuccessfulHits=2,DestroyedMeteors=1};
            Assert.That(ResultFeedback.Achievement(s),Is.EqualTo("운석 1개 격파!"));
            s.DestroyedMeteors=0;Assert.That(ResultFeedback.Achievement(s),Is.EqualTo("공격 2회 명중!"));
            s.SuccessfulHits=0;s.MaxCombo=100;Assert.That(ResultFeedback.Achievement(s),Is.EqualTo("첫 격파에 도전해요!"));
            s.MissionFailed=false;s.SuccessfulHits=1;Assert.That(ResultFeedback.Achievement(s),Is.EqualTo("피해 없이 임무 완료!"));
            s.DamageTaken=1;Assert.That(ResultFeedback.Achievement(s),Is.EqualTo("공격 1회 명중!"));
        }
    }
}
