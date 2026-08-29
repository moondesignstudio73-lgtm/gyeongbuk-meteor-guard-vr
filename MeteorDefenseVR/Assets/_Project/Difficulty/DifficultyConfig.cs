using System;
using MeteorDefenseVR.Core;
using UnityEngine;

namespace MeteorDefenseVR.Difficulty
{
    // Numeric values are deliberately preserved for local-ranking/save compatibility.
    // The former Extreme slot (3) is now the short EXPERIENCE operation mode.
    public enum DifficultyLevel { Easy = 0, Normal = 1, Hard = 2, Experience = 3, Extreme = Experience }
    public enum SpawnDirectionMode { FrontOnly, FrontAndSides, FullSphere, ExperienceArc }

    // Immutable per-play value copy. Editing an asset cannot change an ongoing session.
    public readonly struct DifficultyBalance
    {
        public readonly DifficultyLevel Level;
        public readonly float Speed, Health, Interval, Size, Spread, CenterBias, LockSeconds, Grace, Decay, AssistRadius, ColliderExpansion;
        public readonly float Damage, BossHealth, BossSpeed, Score;
        public readonly int MaxConcurrent;
        public readonly bool AllDirections;
        public readonly SpawnDirectionMode SpawnDirections;
        public readonly float SpawnMin, SpawnMax, ReactionSeconds;
        public readonly int WarningCount;
        public readonly bool WarningDistance, WarningDirectionText;
        public readonly float WarningHorizon, SpatialCueInterval;
        public DifficultyBalance(DifficultyConfig c, DifficultyLevel level)
        {
            Level = level;
            Speed = Safe(c.meteorSpeed, .4f, 2f, 1f); Health = Safe(c.meteorHealth, .5f, 2f, 1f);
            Interval = Safe(c.spawnInterval, .5f, 2f, 1f); Size = Safe(c.meteorSize, .8f, 1.3f, 1f);
            Spread = Safe(c.spawnSpread, .4f, 1.2f, 1f); CenterBias = Safe(c.centerBias, 0f, 1f, 0f);
            LockSeconds = Safe(c.lockDuration, .4f, .9f, .65f); Grace = Safe(c.gracePeriod, .05f, .4f, .25f);
            Decay = Safe(c.lockDecay, .5f, 3f, 1.5f); AssistRadius = Safe(c.targetAssist, 0f, .3f, .15f);
            ColliderExpansion = Safe(c.colliderExpansion, 1f, 1.6f, 1.25f);
            Damage = Safe(c.meteorDamage, .25f, 1.5f, 1f); BossHealth = Safe(c.bossHealth, .5f, 2f, 1f);
            BossSpeed = Safe(c.bossSpeed, .4f, 1.4f, 1f); Score = Safe(c.scoreMultiplier, .5f, 2f, 1f);
            MaxConcurrent = Mathf.Clamp(c.maxConcurrentMeteors, 0, 6);
            SpawnDirections = c.spawnDirections;
            AllDirections = SpawnDirections == SpawnDirectionMode.FullSphere;
            SpawnMin = Safe(c.minimumSpawnDistance, 18, 80, 28);
            SpawnMax = Safe(c.maximumSpawnDistance, SpawnMin, 90, 42);
            ReactionSeconds = Safe(c.minimumReactionSeconds, 4, 16, 9);
            WarningCount = Mathf.Clamp(c.maximumWarningIndicators, 0, 5);
            WarningDistance = c.showWarningDistance; WarningDirectionText = c.showWarningDirectionText;
            WarningHorizon = Safe(c.warningTimeHorizon, 1, 60, 30);
            SpatialCueInterval = Safe(c.spatialWarningInterval, .8f, 8, 3);
            if (AllDirections && MaxConcurrent == 0) MaxConcurrent = 4;
        }
        private static float Safe(float v, float min, float max, float fallback) => RuntimeValueGuard.Clamp(v, min, max, fallback);
        public int ScaleScore(int value) => ScaleInteger(value, Score);
        public int ScaleDamage(int value) => ScaleInteger(value, Damage);
        private static int ScaleInteger(int value, float factor) => value <= 0 ? 0 : (int)Math.Min(int.MaxValue, Math.Max(1d, Math.Round((double)value * factor, MidpointRounding.AwayFromZero)));
        public float SpawnCoordinate(float input) => Mathf.Lerp(input, input * Mathf.Abs(input), CenterBias) * Spread;
    }

    [CreateAssetMenu(menuName = "Meteor Defense/Difficulty Config")]
    public sealed class DifficultyConfig : ScriptableObject
    {
        [Header("Main game — relative to existing operation tuning")]
        [Range(.4f, 2f)] public float meteorSpeed = 1f;
        [Range(.5f, 2f)] public float meteorHealth = 1f;
        [Range(.5f, 2f)] public float spawnInterval = 1f;
        [Tooltip("0 preserves the existing uncapped NORMAL schedule.")]
        [Range(0, 6)] public int maxConcurrentMeteors;
        [Range(.8f, 1.3f)] public float meteorSize = 1f;
        [Range(.4f, 1.2f)] public float spawnSpread = 1f;
        [Range(0f, 1f)] public float centerBias;
        [Header("Gaze — seconds and world-space assist radius")]
        [Range(.4f, .9f)] public float lockDuration = .65f;
        [Range(.05f, .4f)] public float gracePeriod = .25f;
        [Range(.5f, 3f)] public float lockDecay = 1.5f;
        [Range(0f, .3f)] public float targetAssist = .15f;
        [Range(1f, 1.6f)] public float colliderExpansion = 1.25f;
        [Header("Damage / boss / score")]
        [Range(.25f, 1.5f)] public float meteorDamage = 1f;
        [Range(.5f, 2f)] public float bossHealth = 1f;
        [Range(.4f, 1.4f)] public float bossSpeed = 1f;
        [Range(.5f, 2f)] public float scoreMultiplier = 1f;
        [Header("360 degree encounter (metres; shared detection range is 100m)")]
        [Tooltip("Legacy inspector flag. FullSphere is authoritative; retained for old assets.")]
        public bool allDirections;
        public SpawnDirectionMode spawnDirections = SpawnDirectionMode.FrontAndSides;
        [Range(18f, 80f)] public float minimumSpawnDistance = 28f;
        [Range(18f, 90f)] public float maximumSpawnDistance = 42f;
        [Range(4f, 16f)] public float minimumReactionSeconds = 9f;
        [Header("Exploration guidance — independent of meteor strength")]
        [Range(0, 5)] public int maximumWarningIndicators = 4;
        public bool showWarningDistance = true;
        public bool showWarningDirectionText = true;
        [Range(1f, 60f)] public float warningTimeHorizon = 30;
        [Range(.8f, 8f)] public float spatialWarningInterval = 3;
        public DifficultyBalance Capture(DifficultyLevel level) => new DifficultyBalance(this, level);
        public static string Label(DifficultyLevel level) => level == DifficultyLevel.Experience ? "체험" : level == DifficultyLevel.Easy ? "쉬움" : level == DifficultyLevel.Hard ? "어려움" : "보통";
        public static string Code(DifficultyLevel level) => level == DifficultyLevel.Experience ? "EXPERIENCE" : level.ToString().ToUpperInvariant();
    }
}
