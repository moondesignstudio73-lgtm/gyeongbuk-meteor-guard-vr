using UnityEngine;

namespace MeteorDefenseVR.EyeTracking
{
    [CreateAssetMenu(menuName = "Meteor Defense/VR UX Settings", fileName = "VrUxSettings")]
    public sealed class VrUxSettings : ScriptableObject
    {
        [SerializeField, Range(0.08f, 0.3f)] private float gazeMagnetismRadius = 0.15f;
        [SerializeField, Range(0.45f, 0.9f)] private float standardLockDuration = 0.62f;
        [SerializeField, Range(0.6f, 1f)] private float bossLockDuration = 0.78f;
        [SerializeField, Range(20f, 40f)] private float horizontalSpawnLimit = 32f;
        [SerializeField, Range(15f, 30f)] private float verticalSpawnLimit = 20f;
        [SerializeField, Range(0.025f, 0.06f)] private float minimumTextSize = 0.032f;

        public float GazeMagnetismRadius => gazeMagnetismRadius;
        public float StandardLockDuration => standardLockDuration;
        public float BossLockDuration => bossLockDuration;
        public float HorizontalSpawnLimit => horizontalSpawnLimit;
        public float VerticalSpawnLimit => verticalSpawnLimit;
        public float MinimumTextSize => minimumTextSize;

        public void Configure(float magnetism, float lockDuration, float bossDuration, float horizontal, float vertical, float textSize)
        {
            gazeMagnetismRadius = Mathf.Clamp(magnetism, 0.08f, 0.3f);
            standardLockDuration = Mathf.Clamp(lockDuration, 0.45f, 0.9f);
            bossLockDuration = Mathf.Clamp(bossDuration, 0.6f, 1f);
            horizontalSpawnLimit = Mathf.Clamp(horizontal, 20f, 40f);
            verticalSpawnLimit = Mathf.Clamp(vertical, 15f, 30f);
            minimumTextSize = Mathf.Clamp(textSize, 0.025f, 0.06f);
        }
    }
}
