using System;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.Meteor;
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

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
        [SerializeField] private TurretAimingSystem turretAiming;

        [Header("Asset Hooks")]
        [SerializeField] private ParticleSystem[] muzzleFlashes = Array.Empty<ParticleSystem>();
        [SerializeField] private ParticleSystem hitEffect;
        [SerializeField] private AudioSource laserAudioSource;
        [SerializeField] private UnityEvent onCameraFeedbackRequested;

        private float cooldownRemaining;
        private int nextCannonIndex;
        private int lastCannonIndex;
        private float powerUpDamageMultiplier = 1f;
        private float powerUpCooldownMultiplier = 1f;

        public float DamagePerShot { get => damagePerShot; set => damagePerShot = Mathf.Max(0.1f, value); }
        public bool AutoFireOnLock { get => autoFireOnLock; set => autoFireOnLock = value; }
        public float EffectiveDamage => damagePerShot * powerUpDamageMultiplier;
        public TurretAimingSystem TurretAiming => turretAiming;

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
        public void SetTurretAiming(TurretAimingSystem aiming) => turretAiming = aiming;

        public void TickCooldown(float deltaTime) => cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Mathf.Max(0f, deltaTime));

        public bool TryFire(GazeLockOnTarget target)
        {
            if (target == null || !target.IsLocked || cooldownRemaining > 0f) return false;
            MeteorController meteor = target.Meteor;
            if (meteor == null || !meteor.IsTargetable) return false;

            Vector3 origin;int secondaryIndex=-1;Vector3 secondaryOrigin=default;
            if(turretAiming!=null&&turretAiming.HasTracked(target))
            {
                if(!turretAiming.TryGetFiringSolution(target,out TurretFiringSolution solution))return false;
                lastCannonIndex=solution.PrimaryIndex;origin=solution.PrimaryOrigin;secondaryIndex=solution.SecondaryIndex;secondaryOrigin=solution.SecondaryOrigin;
            }
            else origin=SelectMuzzlePosition();
            Vector3 destination = ResolveHitPoint(meteor,origin);
            PlayFeedback(origin, destination,lastCannonIndex,true);
            turretAiming?.NotifyFired(lastCannonIndex);
            if(secondaryIndex>=0)
            {PlayFeedback(secondaryOrigin,destination,secondaryIndex,false);turretAiming?.NotifyFired(secondaryIndex);}
            Fired?.Invoke(target, meteor);

            bool accepted = meteor.ReceiveDamage(EffectiveDamage);
            if (!accepted) return false;
            Hit?.Invoke(meteor, destination);
            if (meteor.State == MeteorLifecycleState.Destroyed && meteor.Score > 0)
                ScorePopupRequested?.Invoke(destination, meteor.Score);
            else
                target.UnlockAfterAttack();

            cooldownRemaining = fireCooldown * powerUpCooldownMultiplier;
            return true;
        }

        public void SetPowerUpModifiers(float damageMultiplier, float cooldownMultiplier)
        {
            powerUpDamageMultiplier = Mathf.Clamp(damageMultiplier, 1f, 20f);
            powerUpCooldownMultiplier = Mathf.Clamp(cooldownMultiplier, .1f, 1f);
        }

        public bool TryAuxiliaryHit(MeteorController meteor, float damage)
        {
            if (meteor == null || !meteor.IsTargetable || damage <= 0f) return false;
            Vector3 destination = meteor.transform.position;
            PlayFeedback(SelectMuzzlePosition(), destination,lastCannonIndex,true);
            bool accepted = meteor.ReceiveDamage(damage);
            if (accepted) Hit?.Invoke(meteor, destination);
            return accepted;
        }

        public int TryPowerBeam(IReadOnlyList<MeteorController> candidates, float damage, float bossDamageMultiplier, int maximumTargets)
        {
            if (candidates == null || damage <= 0f || maximumTargets <= 0) return 0;
            int hitCount = 0; GazeLockOnTarget firstTarget = null; MeteorController firstMeteor = null;
            for (int i = 0; i < candidates.Count && hitCount < maximumTargets; i++)
            {
                MeteorController meteor = candidates[i]; if (meteor == null || !meteor.IsTargetable) continue;
                Vector3 destination = meteor.transform.position; PlayFeedback(SelectMuzzlePosition(), destination,lastCannonIndex,true);
                float applied = meteor.MeteorType == MeteorType.Boss ? damage * Mathf.Max(1f, bossDamageMultiplier) : damage;
                if (!meteor.ReceiveDamage(applied)) continue;
                firstMeteor ??= meteor; firstTarget ??= meteor.GetComponent<GazeLockOnTarget>();
                Hit?.Invoke(meteor, destination); hitCount++;
            }
            if (hitCount > 0) Fired?.Invoke(firstTarget, firstMeteor);
            return hitCount;
        }

        public void ResetWeapon()
        {
            cooldownRemaining = 0f;
            nextCannonIndex = 0;
            lastCannonIndex = 0;
            powerUpDamageMultiplier = 1f;
            powerUpCooldownMultiplier = 1f;
            for (int i = 0; i < beamViews.Length; i++) beamViews[i]?.Stop();
            turretAiming?.ResetTurrets();
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

        private static Vector3 ResolveHitPoint(MeteorController meteor,Vector3 origin)
        {
            Collider collider=meteor!=null?meteor.GetComponentInChildren<Collider>():null;
            return collider!=null?collider.ClosestPoint(origin):meteor!=null?meteor.transform.position:origin;
        }

        private void PlayFeedback(Vector3 origin, Vector3 destination,int cannonIndex,bool fullFeedback)
        {
            if (beamViews != null && beamViews.Length > 0)
            {
                int beamIndex = Mathf.Abs(cannonIndex) % beamViews.Length;
                beamViews[beamIndex]?.Play(origin, destination);
            }
            if (muzzleFlashes != null && muzzleFlashes.Length > 0)
            {
                int flashIndex = Mathf.Abs(cannonIndex) % muzzleFlashes.Length;
                muzzleFlashes[flashIndex]?.Play(true);
            }
            if(!fullFeedback)return;
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
