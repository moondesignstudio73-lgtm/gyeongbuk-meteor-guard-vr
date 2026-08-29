using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.Progression;
using NUnit.Framework;
using UnityEngine;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.UI;
using System.Reflection;

namespace MeteorDefenseVR.Tests
{
    public sealed class StageProgressionTests
    {
        [Test]
        public void CampaignHasTenDataDrivenTwentyPlusBossStages()
        {
            var settings = Resources.Load<StageProgressionSettings>("StageProgressionSettings");
            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.Count, Is.EqualTo(10));
            float previousSpeed = 0, previousRate = 0, previousScore = 0;
            for (int stage = 1; stage <= 10; stage++)
            {
                StageBalance value = settings.Get(stage);
                Assert.That(value.Number, Is.EqualTo(stage));
                Assert.That(value.RegularMeteorCount, Is.EqualTo(20));
                Assert.That(value.Speed, Is.GreaterThanOrEqualTo(previousSpeed));
                Assert.That(value.SpawnRate, Is.GreaterThanOrEqualTo(previousRate));
                Assert.That(value.Score, Is.GreaterThanOrEqualTo(previousScore));
                Assert.That(value.Speed, Is.LessThanOrEqualTo(2f));
                Assert.That(value.MaxConcurrent, Is.InRange(1, 5));
                Assert.That(value.BossScale, Is.InRange(3f, 5f));
                Assert.That(value.Repair, Is.InRange(.2f, .3f));
                previousSpeed = value.Speed; previousRate = value.SpawnRate; previousScore = value.Score;
            }
            Assert.That(settings.Get(10).Score, Is.EqualTo(2f).Within(.01f));
            Assert.That(settings.allowRetryStage, Is.True);
        }

        [Test]
        public void DifficultyControlsDirectionWhileStageControlsPressure()
        {
            var settings = Resources.Load<DifficultySettings>("DifficultySettings");
            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.Get(DifficultyLevel.Experience).Capture(DifficultyLevel.Experience).SpawnDirections, Is.EqualTo(SpawnDirectionMode.ExperienceArc));
            Assert.That(settings.Get(DifficultyLevel.Easy).Capture(DifficultyLevel.Easy).SpawnDirections, Is.EqualTo(SpawnDirectionMode.FrontOnly));
            Assert.That(settings.Get(DifficultyLevel.Normal).Capture(DifficultyLevel.Normal).SpawnDirections, Is.EqualTo(SpawnDirectionMode.FrontAndSides));
            Assert.That(settings.Get(DifficultyLevel.Hard).Capture(DifficultyLevel.Hard).SpawnDirections, Is.EqualTo(SpawnDirectionMode.FullSphere));
            Assert.That(settings.Get(DifficultyLevel.Easy).meteorSpeed, Is.EqualTo(settings.Get(DifficultyLevel.Hard).meteorSpeed));
            Assert.That(settings.Get(DifficultyLevel.Easy).spawnInterval, Is.EqualTo(settings.Get(DifficultyLevel.Hard).spawnInterval));
        }

        [Test]
        public void LegacyNumericSlotThreeLoadsAsExperience()
        {
            Assert.That((int)DifficultyLevel.Experience, Is.EqualTo(3));
            Assert.That(DifficultyConfig.Label((DifficultyLevel)3), Is.EqualTo("체험"));
            Assert.That(DifficultyConfig.Code((DifficultyLevel)3), Is.EqualTo("EXPERIENCE"));
        }

        [Test]
        public void CampaignSpawnerRepeatsAuthoredPatternButStopsAtExactlyTwentyResolvedMeteors()
        {
            var root = new GameObject("StageSpawnerFixture");
            var wave = ScriptableObject.CreateInstance<MeteorWaveData>();
            var config = ScriptableObject.CreateInstance<StageConfig>();
            try
            {
                var data = new MeteorSpawnData(); data.Configure(MeteorType.Normal, null, 2, 0, 0, 2, 1, 10, 100, 1, Vector2.one);
                wave.Configure("repeatable", 0, data); config.regularMeteorCount = 20; config.maxSimultaneousAsteroids = 2;
                var spawner = root.AddComponent<MeteorSpawner>(); spawner.Configure(new[] { wave }, root.transform, root.transform); spawner.SetStage(config.Capture());
                int scheduled = 0, resolved = 0; spawner.AllSpawnsCompleted += () => scheduled++; spawner.AllMeteorsResolved += () => resolved++;
                spawner.BeginSpawning();
                for (int pass = 0; pass < 20 && resolved == 0; pass++)
                {
                    spawner.Tick(100);
                    foreach (MeteorController meteor in new System.Collections.Generic.List<MeteorController>(spawner.ActiveMeteors)) meteor.DestroyMeteor();
                    spawner.Tick(0);
                }
                Assert.That(spawner.TotalScheduled, Is.EqualTo(20)); Assert.That(spawner.SpawnedCount, Is.EqualTo(20));
                Assert.That(spawner.CompletedCount, Is.EqualTo(20)); Assert.That(scheduled, Is.EqualTo(1)); Assert.That(resolved, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(wave); Object.DestroyImmediate(config); }
        }

        [Test]
        public void TenBossCompletionsAdvanceToDistinctFinalCampaignSuccess()
        {
            var root = new GameObject("TenStageFixture");
            try
            {
                var spawner = root.AddComponent<MeteorSpawner>(); var boss = root.AddComponent<BossClimaxController>();
                var health = root.AddComponent<PlayerHealth>(); var hud = root.AddComponent<MissionHudController>();
                var stats = root.AddComponent<GameSessionStats>(); var progression = root.AddComponent<StageProgressionController>();
                progression.Configure(Resources.Load<StageProgressionSettings>("StageProgressionSettings"), spawner, boss, health, hud, stats);
                var difficulty = Resources.Load<DifficultySettings>("DifficultySettings").normal.Capture(DifficultyLevel.Normal);
                progression.PrepareForPlaying(difficulty, false); health.ResetHealth();
                int finalEvents = 0; progression.CampaignCompleted += _ => finalEvents++;
                var advance = typeof(StageProgressionController).GetMethod("AdvanceStage", BindingFlags.Instance | BindingFlags.NonPublic);
                for (int stage = 1; stage <= 10; stage++)
                {
                    Assert.That(progression.CurrentStage, Is.EqualTo(stage));
                    Assert.That(boss.CampaignCompletionHandler(stage), Is.True);
                    if (stage < 10) advance.Invoke(progression, null);
                }
                Assert.That(progression.CurrentStage, Is.EqualTo(10)); Assert.That(finalEvents, Is.EqualTo(1));
                Assert.That(progression.Phase, Is.EqualTo(StageTransitionPhase.Complete));
                Assert.That(stats.AllStagesCleared, Is.True); Assert.That(stats.ClearedStages, Is.EqualTo(10));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
