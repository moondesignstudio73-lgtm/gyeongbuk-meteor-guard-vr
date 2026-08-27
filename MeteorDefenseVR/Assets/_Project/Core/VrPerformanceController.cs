using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.VFX;
using UnityEngine;

namespace MeteorDefenseVR.Core
{
    [DisallowMultipleComponent]
    public sealed class VrPerformanceController : MonoBehaviour
    {
        [SerializeField, Range(72, 120)] private int targetFrameRate = 90;
        [SerializeField] private MeteorSpawner pooledMeteorSpawner;
        [SerializeField] private CombatVfxController reusableCombatVfx;

        public int TargetFrameRate => targetFrameRate;
        public bool UsesMeteorPooling => pooledMeteorSpawner != null;
        public bool UsesReusableCombatVfx => reusableCombatVfx != null;

        private void Awake() => ApplyPerformancePolicy();

        public void Configure(MeteorSpawner spawner, CombatVfxController combatVfx, int targetFps = 90)
        {
            pooledMeteorSpawner = spawner;
            reusableCombatVfx = combatVfx;
            targetFrameRate = Mathf.Clamp(targetFps, 72, 120);
            ApplyPerformancePolicy();
        }

        public void ApplyPerformancePolicy()
        {
            Physics.reuseCollisionCallbacks = true;
            if (Application.isPlaying)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = targetFrameRate;
            }
        }
    }
}
