using MeteorDefenseVR.Core;
using UnityEngine;

namespace MeteorDefenseVR.PowerUps
{
    public enum PowerUpType { Heal, Shield, DamageBoost, RapidFire, MultiLaser, Overdrive, SlowTime, EMP }
    public enum PowerUpStackRule { Instant, ExtendDuration, AddShieldHit, RefreshDuration }
    public enum ShieldConsumptionMode { Duration, ImpactCount }

    public readonly struct PowerUpBalance
    {
        public readonly PowerUpType Type;
        public readonly float DropChance, DropWeight, Duration, Power, MaximumDuration;
        public readonly int SpawnStage;
        public readonly PowerUpStackRule StackRule;
        public readonly string DisplayName, ShortCode, Icon;
        public readonly Color Color;

        public PowerUpBalance(PowerUpDefinition source)
        {
            Type = source.type;
            DropChance = RuntimeValueGuard.Clamp(source.dropChance, 0f, 1f, .1f);
            DropWeight = RuntimeValueGuard.Clamp(source.dropWeight, 0f, 100f, 1f);
            Duration = RuntimeValueGuard.Clamp(source.duration, 0f, 60f, 8f);
            Power = RuntimeValueGuard.Clamp(source.power, 0f, 20f, 1f);
            MaximumDuration = RuntimeValueGuard.Clamp(source.maximumDuration, Duration, 120f, Mathf.Max(Duration, 15f));
            SpawnStage = Mathf.Clamp(source.spawnStage, 1, 10);
            StackRule = source.stackRule;
            DisplayName = string.IsNullOrWhiteSpace(source.displayName) ? Type.ToString().ToUpperInvariant() : source.displayName;
            ShortCode = string.IsNullOrWhiteSpace(source.shortCode) ? DisplayName : source.shortCode;
            Icon = string.IsNullOrWhiteSpace(source.icon) ? "◇" : source.icon;
            Color = source.itemColor;
        }

        public bool IsInstant => Type == PowerUpType.Heal || Type == PowerUpType.EMP;
    }

    [CreateAssetMenu(menuName = "Meteor Defense/Power-Up Definition")]
    public sealed class PowerUpDefinition : ScriptableObject
    {
        public PowerUpType type;
        [Range(0f, 1f)] public float dropChance = .1f;
        [Min(0f)] public float dropWeight = 1f;
        [Range(0f, 30f)] public float duration = 8f;
        [Range(0f, 10f)] public float power = 1f;
        public PowerUpStackRule stackRule = PowerUpStackRule.ExtendDuration;
        [Range(1, 10)] public int spawnStage = 1;
        [Range(0f, 60f)] public float maximumDuration = 18f;
        public string displayName = "POWER UP";
        public string shortCode = "POWER";
        public string icon = "◇";
        public Color itemColor = Color.cyan;
        [Header("Optional authored assets")]
        public Sprite iconSprite;
        public GameObject visualPrefab;
        public ParticleSystem pickupVfx;
        public AudioClip pickupSfx;

        public PowerUpBalance Capture() => new PowerUpBalance(this);
    }
}
