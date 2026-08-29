using MeteorDefenseVR.Progression;
using UnityEditor;
using UnityEngine;

namespace MeteorDefenseVR.Editor
{
    public static class StageProgressionSetup
    {
        private static readonly float[] Speed = { 1f, 1.08f, 1.16f, 1.25f, 1.35f, 1.46f, 1.58f, 1.71f, 1.85f, 2f };
        private static readonly float[] Rate = { 1f, 1.08f, 1.16f, 1.25f, 1.35f, 1.45f, 1.55f, 1.65f, 1.78f, 1.95f };
        private static readonly int[] Concurrent = { 2, 2, 2, 3, 3, 3, 4, 4, 4, 5 };

        [MenuItem("Meteor Defense/Ensure Stage Progression Assets")]
        public static void EnsureAssets()
        {
            const string root = "Assets/_Project/Progression";
            if (!AssetDatabase.IsValidFolder(root)) AssetDatabase.CreateFolder("Assets/_Project", "Progression");
            if (!AssetDatabase.IsValidFolder(root + "/Resources")) AssetDatabase.CreateFolder(root, "Resources");
            StageConfig[] stages = new StageConfig[10];
            for (int i = 0; i < stages.Length; i++)
            {
                string path = $"{root}/Stage_{i + 1:D2}.asset";
                stages[i] = AssetDatabase.LoadAssetAtPath<StageConfig>(path);
                if (stages[i] == null) { stages[i] = ScriptableObject.CreateInstance<StageConfig>(); AssetDatabase.CreateAsset(stages[i], path); }
                stages[i].stageNumber = i + 1; stages[i].regularMeteorCount = 20;
                stages[i].speedMultiplier = Speed[i]; stages[i].spawnRateMultiplier = Rate[i]; stages[i].maxSimultaneousAsteroids = Concurrent[i];
                stages[i].sizeVariation = Mathf.Lerp(.06f, .24f, i / 9f); stages[i].patternVariation = Mathf.Lerp(0, 1, i / 9f);
                stages[i].bossHealthMultiplier = Mathf.Lerp(1, 4.25f, i / 9f); stages[i].bossScale = Mathf.Lerp(3.25f, 5f, i / 9f);
                stages[i].bossSpeedMultiplier = Mathf.Lerp(.85f, 1.3f, i / 9f);
                stages[i].bossFragmentCount = i < 6 ? 0 : i < 9 ? 2 + (i - 6) : 6;
                stages[i].scoreMultiplier = 1f + i / 9f; stages[i].stageClearRepair = .25f;
                EditorUtility.SetDirty(stages[i]);
            }
            string settingsPath = root + "/Resources/StageProgressionSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<StageProgressionSettings>(settingsPath);
            if (settings == null) { settings = ScriptableObject.CreateInstance<StageProgressionSettings>(); AssetDatabase.CreateAsset(settings, settingsPath); }
            settings.stages = stages; settings.allowRetryStage = true; settings.explosionQuietTime = 1f; settings.stageClearPresentationTime = 2.6f;
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
            Debug.Log("[StageProgression] EXPERIENCE plus ten campaign stages are ready. Scene hierarchy preserved.");
        }
    }
}
