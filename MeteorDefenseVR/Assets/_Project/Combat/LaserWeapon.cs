using System;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.Meteor;
using UnityEngine;
using UnityEngine.Events;

namespace MeteorDefenseVR.Combat
{
    [DisallowMultipleComponent]
    public sealed class LaserWeapon : MonoBehaviour
    {
        [Header("Targeting")]
        [SerializeField] private GazeRaycaster gazeRaycaster;
        [SerializeField] private bool autoFireOnLock = true;
        [SerializeField, Min(0.1f)] private float damagePerShot = 1f;
        [SerializeField, Min(0f)] private float fireCooldown = 0.08f;

        [Header("Cannons")]
        [SerializeField] private Transform[] cannonMuzzles = Array.Empty<Transform>();
        [SerializeField] private LaserBeamView[] beamViews = Array.Empty<LaserBeamView>();

        [Header("Asset Hooks")]
        [SerializeField] private ParticleSystem[] muzzleFlashes = Array.Empty<ParticleSystem>();
        [SerializeField] private ParticleSystem hitEffect;
        [SerializeField] private AudioSource laserAudioSource;
        [SerializeField] private UnityEvent onCameraFeedbackRequested;

        private float cooldownRemaining;
        private int nextCannonIndex;
        private int lastCannonIndex;

        public float DamagePerShot { get => damagePerShot; set => damagePerShot = Mathf.Max(0.1f, value); }
        public bool AutoFireOnLock { get => autoFireOnLock; set => autoFireOnLock = value; }

        public event Action<GazeLockOnTarget, MeteorController> Fired;
        public event Action<MeteorController, Vector3> Hit;
        public event Action<Vector3, int> ScorePopupRequested;

        private void Update()
        {
            TickCooldown(Time.deltaTime);
            if (!autoFireOnLock || cooldownRemaining > 0f || gazeRaycaster == null) return;
            if (gazeRaycaster.CurrentTarget is GazeLockOnTarget target && target.IsLocked)
                TryFire(target);
        }

        public void Configure(GazeRaycaster raycaster, Transform[] muzzles, LaserBeamView[] beams)
        {
            gazeRaycaster = raycaster;
            cannonMuzzles = muzzles ?? Array.Empty<Transform>();
            beamViews = beams ?? Array.Empty<LaserBeamView>();
        }

        public void TickCooldown(float deltaTime) => cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Mathf.Max(0f, deltaTime));

        public bool TryFire(GazeLockOnTarget target)
        {
            if (target == null || !target.IsLocked || cooldownRemaining > 0f) return false;
            MeteorController meteor = target.Meteor;
            if (meteor == null || !meteor.IsTargetable) return false;

            Vector3 destination = meteor.transform.position;
            Vector3 origin = SelectMuzzlePosition();
            PlayFeedback(origin, destination);
            Fired?.Invoke(target, meteor);

            bool accepted = meteor.ReceiveDamage(damagePerShot);
            if (!accepted) return false;
            Hit?.Invoke(meteor, destination);
            if (meteor.State == MeteorLifecycleState.Destroyed && meteor.Score > 0)
                ScorePopupRequested?.Invoke(destination, meteor.Score);
            else
                target.UnlockAfterAttack();

            cooldownRemaining = fireCooldown;
            return true;
        }

        public void ResetWeapon()
        {
            cooldownRemaining = 0f;
            nextCannonIndex = 0;
            lastCannonIndex = 0;
            for (int i = 0; i < beamViews.Length; i++) beamViews[i]?.Stop();
        }

        private Vector3 SelectMuzzlePosition()
        {
            if (cannonMuzzles == null || cannonMuzzles.Length == 0) return transform.position;
            int index = nextCannonIndex % cannonMuzzles.Length;
            lastCannonIndex = index;
            nextCannonIndex = (nextCannonIndex + 1) % cannonMuzzles.Length;
            Transform muzzle = cannonMuzzles[index];
            return muzzle != null ? muzzle.position : transform.position;
        }

        private void PlayFeedback(Vector3 origin, Vector3 destination)
        {
            if (beamViews != null && beamViews.Length > 0)
            {
                int beamIndex = lastCannonIndex % beamViews.Length;
                beamViews[beamIndex]?.Play(origin, destination);
            }
            if (muzzleFlashes != null && muzzleFlashes.Length > 0)
            {
                int flashIndex = lastCannonIndex % muzzleFlashes.Length;
                muzzleFlashes[flashIndex]?.Play(true);
            }
            if (hitEffect != null)
            {
                hitEffect.transform.position = destination;
                hitEffect.Play(true);
            }
            if (laserAudioSource != null) laserAudioSource.Play();
            onCameraFeedbackRequested?.Invoke();
        }
    }
}
