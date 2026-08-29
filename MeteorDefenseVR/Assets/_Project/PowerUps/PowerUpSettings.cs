using System;
using UnityEngine;

namespace MeteorDefenseVR.PowerUps
{
    [CreateAssetMenu(menuName = "Meteor Defense/Power-Up Settings")]
    public sealed class PowerUpSettings : ScriptableObject
    {
        public PowerUpDefinition[] definitions = Array.Empty<PowerUpDefinition>();
        [Header("Drop director")]
        [Range(0f, .5f)] public float regularDropChance = .09f;
        [Range(1, 30)] public int pityStartsAfter = 10;
        [Range(1, 40)] public int pityGuaranteedAt = 15;
        [Range(0f, .2f)] public float pityChancePerMiss = .035f;
        public bool bossGuaranteedDrop = true;
        [Range(1, 6)] public int maximumWorldItems = 3;
        [Header("Collection")]
        [Range(3f, 12f)] public float itemLifetime = 7f;
        [Range(.25f, 1.2f)] public float gazeCollectDuration = .55f;
        [Range(0f, 2f)] public float driftSpeed = .35f;
        [Range(1, 5)] public int maximumActiveBuffs = 3;
        [Header("Shield / special")]
        public ShieldConsumptionMode shieldMode = ShieldConsumptionMode.ImpactCount;
        [Range(1, 8)] public int shieldImpactCount = 3;
        [Range(.2f, .8f)] public float blinkLaserCooldown = .42f;
        [Range(1, 12)] public int overdriveMaximumTargets = 6;
        [Range(2f, 18f)] public float overdriveConeDegrees = 10f;
        [Range(1, 8)] public int multiLaserAdditionalTargets = 2;
        [Range(5f, 30f)] public float multiLaserConeDegrees = 15f;
        [Range(.1f, .5f)] public float empBossHealthFraction = .2f;
        [Header("Optional gauge (item-driven is the default)")]
        public bool enableSpecialGauge;

        public PowerUpDefinition Find(PowerUpType type)
        {
            if (definitions == null) return null;
            foreach (PowerUpDefinition definition in definitions) if (definition != null && definition.type == type) return definition;
            return null;
        }
    }
}
