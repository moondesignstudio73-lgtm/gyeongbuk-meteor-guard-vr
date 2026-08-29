using System;
using UnityEngine;

namespace MeteorDefenseVR.Progression
{
    [CreateAssetMenu(menuName = "Meteor Defense/Stage Progression Settings")]
    public sealed class StageProgressionSettings : ScriptableObject
    {
        public StageConfig[] stages = Array.Empty<StageConfig>();
        [Header("Presentation (unscaled seconds)")]
        [Range(.25f, 2f)] public float explosionQuietTime = 1f;
        [Range(1f, 5f)] public float stageClearPresentationTime = 2.6f;
        [Header("Operator")]
        public bool allowRetryStage = true;
        [Range(1, 50)] public int experienceMeteorCount = 20;
        [Range(120f, 300f)] public float experienceTargetSeconds = 240f;

        public int Count => stages == null ? 0 : stages.Length;
        public StageBalance Get(int oneBasedStage)
        {
            if (stages == null || stages.Length == 0 || stages[Mathf.Clamp(oneBasedStage - 1, 0, stages.Length - 1)] == null)
            {
                var fallback = CreateInstance<StageConfig>();
                fallback.stageNumber = Mathf.Max(1, oneBasedStage);
                StageBalance result = fallback.Capture();
                Destroy(fallback);
                return result;
            }
            return stages[Mathf.Clamp(oneBasedStage - 1, 0, stages.Length - 1)].Capture();
        }
    }
}
