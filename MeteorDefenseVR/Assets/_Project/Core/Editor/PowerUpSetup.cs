using MeteorDefenseVR.PowerUps;
using UnityEditor;
using UnityEngine;

namespace MeteorDefenseVR.Editor
{
    public static class PowerUpSetup
    {
        [MenuItem("Meteor Defense/Ensure Power-Up Assets")]
        public static void EnsureAssets()
        {
            const string root = "Assets/_Project/PowerUps";
            if (!AssetDatabase.IsValidFolder(root)) AssetDatabase.CreateFolder("Assets/_Project", "PowerUps");
            if (!AssetDatabase.IsValidFolder(root + "/Definitions")) AssetDatabase.CreateFolder(root, "Definitions");
            if (!AssetDatabase.IsValidFolder(root + "/Resources")) AssetDatabase.CreateFolder(root, "Resources");

            PowerUpDefinition[] definitions =
            {
                Ensure("Heal", PowerUpType.Heal, 1, .18f, 1.5f, 0f, .2f, PowerUpStackRule.Instant, "HULL REPAIR", "HEAL", "＋", new Color(.2f,1f,.35f)),
                Ensure("Shield", PowerUpType.Shield, 1, .16f, 1.35f, 10f, 1f, PowerUpStackRule.AddShieldHit, "ENERGY SHIELD", "SHIELD", "◇", new Color(.12f,.48f,1f)),
                Ensure("DamageBoost", PowerUpType.DamageBoost, 4, .14f, 1.2f, 10f, 3f, PowerUpStackRule.ExtendDuration, "POWER UP", "POWER", "*", new Color(.1f,1f,1f)),
                Ensure("RapidFire", PowerUpType.RapidFire, 4, .13f, 1f, 9f, .45f, PowerUpStackRule.ExtendDuration, "RAPID FIRE", "RAPID", "»", new Color(1f,.78f,.08f)),
                Ensure("MultiLaser", PowerUpType.MultiLaser, 5, .11f, .9f, 9f, 1f, PowerUpStackRule.ExtendDuration, "MULTI LASER", "MULTI", "Ψ", new Color(.1f,.9f,1f)),
                Ensure("Overdrive", PowerUpType.Overdrive, 7, .08f, .55f, 7f, 1f, PowerUpStackRule.ExtendDuration, "OVERDRIVE", "OVERDRIVE", "◎", new Color(.76f,.24f,1f)),
                Ensure("SlowTime", PowerUpType.SlowTime, 7, .08f, .55f, 6f, .58f, PowerUpStackRule.ExtendDuration, "ASTEROID SLOW", "SLOW", "~", new Color(.35f,.65f,1f)),
                Ensure("EMP", PowerUpType.EMP, 8, .05f, .3f, 0f, .2f, PowerUpStackRule.Instant, "EMP", "EMP", "◉", Color.white)
            };
            string path = root + "/Resources/PowerUpSettings.asset";
            PowerUpSettings settings = AssetDatabase.LoadAssetAtPath<PowerUpSettings>(path);
            if (settings == null) { settings = ScriptableObject.CreateInstance<PowerUpSettings>(); AssetDatabase.CreateAsset(settings, path); }
            settings.definitions = definitions;
            settings.regularDropChance = .09f; settings.pityStartsAfter = 10; settings.pityGuaranteedAt = 15; settings.pityChancePerMiss = .035f;
            settings.bossGuaranteedDrop = true; settings.maximumWorldItems = 3; settings.itemLifetime = 7f; settings.gazeCollectDuration = .55f;
            settings.driftSpeed = .35f; settings.maximumActiveBuffs = 3; settings.shieldMode = ShieldConsumptionMode.ImpactCount; settings.shieldImpactCount = 3;
            settings.blinkLaserCooldown = .42f; settings.overdriveMaximumTargets = 6; settings.overdriveConeDegrees = 10f;
            settings.multiLaserAdditionalTargets = 2; settings.multiLaserConeDegrees = 15f; settings.empBossHealthFraction = .2f;
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
            Debug.Log("[PowerUp] Eight data-driven power-ups and safe defaults are ready.");
        }

        private static PowerUpDefinition Ensure(string file, PowerUpType type, int stage, float chance, float weight, float duration,
            float power, PowerUpStackRule stack, string display, string code, string icon, Color color)
        {
            string path = $"Assets/_Project/PowerUps/Definitions/PowerUp_{file}.asset";
            PowerUpDefinition definition = AssetDatabase.LoadAssetAtPath<PowerUpDefinition>(path);
            if (definition == null) { definition = ScriptableObject.CreateInstance<PowerUpDefinition>(); AssetDatabase.CreateAsset(definition, path); }
            definition.type = type; definition.spawnStage = stage; definition.dropChance = chance; definition.dropWeight = weight; definition.duration = duration;
            definition.power = power; definition.stackRule = stack; definition.maximumDuration = Mathf.Max(duration + 5f, duration * 2f);
            definition.displayName = display; definition.shortCode = code; definition.icon = icon; definition.itemColor = color;
            EditorUtility.SetDirty(definition); return definition;
        }
    }
}
