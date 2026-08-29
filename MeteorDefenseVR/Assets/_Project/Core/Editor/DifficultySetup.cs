using MeteorDefenseVR.Difficulty;
using UnityEditor;
using UnityEngine;

namespace MeteorDefenseVR.Editor
{
    public static class DifficultySetup
    {
        [MenuItem("Meteor Defense/Ensure Difficulty Assets")]
        public static void EnsureAssets()
        {
            const string root = "Assets/_Project/Difficulty";
            if (!AssetDatabase.IsValidFolder(root + "/Resources")) AssetDatabase.CreateFolder(root, "Resources");
            var easy = Ensure(root + "/Difficulty_Easy.asset", DifficultyLevel.Easy);
            var normal = Ensure(root + "/Difficulty_Normal.asset", DifficultyLevel.Normal);
            var hard = Ensure(root + "/Difficulty_Hard.asset", DifficultyLevel.Hard);
            var experience = Ensure(root + "/Difficulty_Experience.asset", DifficultyLevel.Experience);
            const string settingsPath = root + "/Resources/DifficultySettings.asset";
            if (AssetDatabase.LoadAssetAtPath<DifficultySettings>(settingsPath) == null)
            {
                var settings = ScriptableObject.CreateInstance<DifficultySettings>();
                settings.easy = easy; settings.normal = normal; settings.hard = hard;
                AssetDatabase.CreateAsset(settings, settingsPath);
            }
            var current = AssetDatabase.LoadAssetAtPath<DifficultySettings>(settingsPath);
            current.easy = easy; current.normal = normal; current.hard = hard; current.extreme = experience;
            ApplyCurrentDirectionModel(experience, DifficultyLevel.Experience);
            ApplyCurrentDirectionModel(easy, DifficultyLevel.Easy);
            ApplyCurrentDirectionModel(normal, DifficultyLevel.Normal);
            ApplyCurrentDirectionModel(hard, DifficultyLevel.Hard);
            EditorUtility.SetDirty(current);
            AssetDatabase.SaveAssets();
            Debug.Log("[Difficulty] Assets ready; existing operator edits preserved. Scenes not regenerated.");
        }
        private static DifficultyConfig Ensure(string path, DifficultyLevel level)
        {
            var existing = AssetDatabase.LoadAssetAtPath<DifficultyConfig>(path);
            if (existing != null) return existing;
            var config = ScriptableObject.CreateInstance<DifficultyConfig>();
            if (level == DifficultyLevel.Experience)
            {
                config.allDirections = true; config.meteorSpeed = 1.35f; config.spawnInterval = .7f;
                config.maxConcurrentMeteors = 5; config.lockDuration = .8f; config.gracePeriod = .18f;
                config.targetAssist = .12f; config.colliderExpansion = 1.2f; config.meteorDamage = 1.5f;
                config.bossHealth = 1.5f; config.bossSpeed = 1.15f; config.scoreMultiplier = 2;
                config.meteorSize = 1.25f;
                config.minimumReactionSeconds = 8; config.maximumWarningIndicators = 1; config.showWarningDistance = false;
                config.showWarningDirectionText = false; config.warningTimeHorizon = 10; config.spatialWarningInterval = 2;
            }
            else if (level == DifficultyLevel.Easy)
            {
                config.meteorSpeed = .75f; config.meteorHealth = .8f; config.spawnInterval = 1.25f;
                config.maxConcurrentMeteors = 2; config.meteorSize = 1.15f; config.spawnSpread = .8f; config.centerBias = .65f;
                config.lockDuration = .5f; config.gracePeriod = .35f; config.lockDecay = .8f;
                config.targetAssist = .24f; config.colliderExpansion = 1.45f; config.meteorDamage = .5f;
                config.bossHealth = 2f / 3f; config.bossSpeed = .7f; config.scoreMultiplier = .8f;
            }
            else if (level == DifficultyLevel.Hard)
            {
                config.meteorSpeed = 1.3f; config.meteorHealth = 1.2f; config.spawnInterval = .8f;
                config.allDirections = true; config.maxConcurrentMeteors = 4; config.lockDuration = .8f; config.gracePeriod = .1f; config.lockDecay = 2f;
                config.targetAssist = .05f; config.colliderExpansion = 1.05f; config.meteorDamage = 1.25f;
                config.bossHealth = 5f / 3f; config.bossSpeed = 1.15f; config.scoreMultiplier = 1.5f;
                config.maximumWarningIndicators = 3; config.showWarningDistance = false; config.warningTimeHorizon = 16; config.spatialWarningInterval = 2.5f;
            }
            else { config.allDirections = true; config.maxConcurrentMeteors = 3; config.minimumReactionSeconds = 12; }
            AssetDatabase.CreateAsset(config, path); return config;
        }

        private static void ApplyCurrentDirectionModel(DifficultyConfig config, DifficultyLevel level)
        {
            if (config == null) return;
            config.meteorSpeed = level == DifficultyLevel.Experience ? .75f : 1f;
            config.spawnInterval = level == DifficultyLevel.Experience ? 1.25f : 1f;
            config.meteorHealth = 1f; config.meteorDamage = level == DifficultyLevel.Experience ? .5f : 1f;
            config.bossHealth = 1f; config.bossSpeed = 1f; config.scoreMultiplier = level == DifficultyLevel.Experience ? .8f : 1f;
            config.spawnDirections = level == DifficultyLevel.Hard ? SpawnDirectionMode.FullSphere
                : level == DifficultyLevel.Normal ? SpawnDirectionMode.FrontAndSides
                : level == DifficultyLevel.Easy ? SpawnDirectionMode.FrontOnly : SpawnDirectionMode.ExperienceArc;
            config.allDirections = config.spawnDirections == SpawnDirectionMode.FullSphere;
            config.maxConcurrentMeteors = level == DifficultyLevel.Experience ? 2 : 5;
            config.maximumWarningIndicators = level == DifficultyLevel.Hard ? 5 : level == DifficultyLevel.Normal ? 3 : 1;
            config.showWarningDistance = level != DifficultyLevel.Experience;
            config.showWarningDirectionText = level == DifficultyLevel.Hard;
            EditorUtility.SetDirty(config);
        }
    }
}
