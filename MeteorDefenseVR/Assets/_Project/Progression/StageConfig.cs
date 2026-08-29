using System;
using MeteorDefenseVR.Core;
using UnityEngine;

namespace MeteorDefenseVR.Progression
{
    public readonly struct StageBalance
    {
        public readonly int Number, RegularMeteorCount, MaxConcurrent, BossFragmentCount;
        public readonly float Speed, SpawnRate, SizeVariation, PatternVariation, BossHealth, BossScale, BossSpeed, Score, Repair;

        public StageBalance(StageConfig config)
        {
            Number = Mathf.Clamp(config.stageNumber, 1, 99);
            RegularMeteorCount = Mathf.Clamp(config.regularMeteorCount, 1, 200);
            Speed = RuntimeValueGuard.Clamp(config.speedMultiplier, .5f, 2.25f, 1f);
            SpawnRate = RuntimeValueGuard.Clamp(config.spawnRateMultiplier, .5f, 2.25f, 1f);
            MaxConcurrent = Mathf.Clamp(config.maxSimultaneousAsteroids, 1, 8);
            SizeVariation = RuntimeValueGuard.Clamp(config.sizeVariation, 0f, .45f, .08f);
            PatternVariation = RuntimeValueGuard.Clamp(config.patternVariation, 0f, 1f, 0f);
            BossHealth = RuntimeValueGuard.Clamp(config.bossHealthMultiplier, .5f, 12f, 1f);
            BossScale = RuntimeValueGuard.Clamp(config.bossScale, 3f, 5f, 3.4f);
            BossSpeed = RuntimeValueGuard.Clamp(config.bossSpeedMultiplier, .5f, 2f, 1f);
            BossFragmentCount = Mathf.Clamp(config.bossFragmentCount, 0, 8);
            Score = RuntimeValueGuard.Clamp(config.scoreMultiplier, 1f, 2.5f, 1f);
            Repair = RuntimeValueGuard.Clamp(config.stageClearRepair, 0f, .5f, .25f);
        }

        public int ScaleScore(int value) => value <= 0 ? 0 : (int)Math.Min(int.MaxValue, Math.Round(value * (double)Score, MidpointRounding.AwayFromZero));
    }

    [CreateAssetMenu(menuName = "Meteor Defense/Stage Config")]
    public sealed class StageConfig : ScriptableObject
    {
        [Range(1, 99)] public int stageNumber = 1;
        [Range(1, 200)] public int regularMeteorCount = 20;
        [Header("Stage pressure — independent from spawn direction")]
        [Range(.5f, 2.25f)] public float speedMultiplier = 1f;
        [Range(.5f, 2.25f)] public float spawnRateMultiplier = 1f;
        [Range(1, 8)] public int maxSimultaneousAsteroids = 2;
        [Range(0f, .45f)] public float sizeVariation = .08f;
        [Range(0f, 1f)] public float patternVariation;
        [Header("21st asteroid")]
        [Range(.5f, 12f)] public float bossHealthMultiplier = 1f;
        [Range(3f, 5f)] public float bossScale = 3.4f;
        [Range(.5f, 2f)] public float bossSpeedMultiplier = 1f;
        [Range(0, 8)] public int bossFragmentCount;
        [Header("Reward")]
        [Range(1f, 2.5f)] public float scoreMultiplier = 1f;
        [Range(0f, .5f)] public float stageClearRepair = .25f;
        public StageBalance Capture() => new StageBalance(this);
    }
}
