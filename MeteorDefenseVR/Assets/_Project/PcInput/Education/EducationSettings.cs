using UnityEngine;

namespace MeteorDefenseVR.Education
{
    [CreateAssetMenu(menuName = "Meteor Defense/Education Settings")]
    public sealed class EducationSettings : ScriptableObject
    {
        public bool enabledForExperience = true;
        [Tooltip("Stage is the learning objective, never the selected difficulty.")]
        public float[] stageSeconds = { 150, 180, 150, 180 };
        [Range(.3f, .8f)] public float detectionDwell = .4f;
        [Range(3, 7)] public float detectionConeDegrees = 5;
        [Range(8, 35)] public float observationDeadline = 18;
        [Range(10, 40)] public float questionDeadline = 22;
        [Range(1, 6)] public float feedbackSeconds = 3;
        [Range(30, 500)] public int maximumStoredSessions = 200;
        [Range(1, 365)] public int retentionDays = 30;
        public EducationWeights weights = new EducationWeights();
        public ThreatRiskSettings risk = new ThreatRiskSettings();
        public EmergencyTrainingSettings emergency = new EmergencyTrainingSettings();
        public TMPro.TMP_FontAsset interfaceFont, participantFont;
        public float Duration(EducationStage stage) => stageSeconds != null && stageSeconds.Length > (int)stage ? Mathf.Clamp(stageSeconds[(int)stage], 5, 240) : 150;
    }
}
