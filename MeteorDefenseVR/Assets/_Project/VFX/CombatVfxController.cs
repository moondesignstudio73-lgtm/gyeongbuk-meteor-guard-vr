using System;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Meteor;
using UnityEngine;

namespace MeteorDefenseVR.VFX
{
    [DisallowMultipleComponent]
    public sealed class CombatVfxController : MonoBehaviour
    {
        [SerializeField] private LaserWeapon weapon;
        [SerializeField] private BossClimaxController boss;
        [SerializeField] private Transform hitFlash;
        [SerializeField] private Transform meteorExplosion;
        [SerializeField] private Transform bossExplosion;
        [SerializeField, Min(0.05f)] private float hitDuration = 0.12f;
        [SerializeField, Min(0.1f)] private float explosionDuration = 0.45f;
        [SerializeField, Min(0.2f)] private float bossDuration = 0.9f;

        private float hitRemaining;
        private float explosionRemaining;
        private float bossRemaining;

        public bool HitActive => hitRemaining > 0f;
        public bool ExplosionActive => explosionRemaining > 0f;
        public bool BossExplosionActive => bossRemaining > 0f;

        public event Action<Vector3> HitVfxPlayed;
        public event Action<Vector3> ExplosionVfxPlayed;
        public event Action<Vector3> BossExplosionVfxPlayed;

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void Update() => Tick(Time.unscaledDeltaTime);

        public void Configure(
            LaserWeapon laserWeapon,
            BossClimaxController bossController,
            Transform hit,
            Transform explosion,
            Transform bossBurst)
        {
            Unsubscribe();
            weapon = laserWeapon;
            boss = bossController;
            hitFlash = hit;
            meteorExplosion = explosion;
            bossExplosion = bossBurst;
            ResetEffects();
            if (isActiveAndEnabled) Subscribe();
        }

        public void PlayHitFeedback(MeteorController meteor, Vector3 position)
        {
            hitRemaining = hitDuration;
            Activate(hitFlash, position, 0.04f);
            HitVfxPlayed?.Invoke(position);
            // Boss destruction already raises StrongExplosionRequested; don't stack a second normal blast.
            if (meteor == null || meteor.State != MeteorLifecycleState.Destroyed || meteor.MeteorType == MeteorType.Boss) return;
            explosionRemaining = explosionDuration;
            Activate(meteorExplosion, position, 0.08f);
            ExplosionVfxPlayed?.Invoke(position);
        }

        public void PlayBossExplosion(Vector3 position)
        {
            bossRemaining = bossDuration;
            Activate(bossExplosion, position, 0.12f);
            BossExplosionVfxPlayed?.Invoke(position);
        }

        public void Tick(float unscaledDeltaTime)
        {
            float delta = Mathf.Max(0f, unscaledDeltaTime);
            Animate(ref hitRemaining, hitDuration, hitFlash, 0.04f, 0.28f, delta);
            Animate(ref explosionRemaining, explosionDuration, meteorExplosion, 0.08f, 1.15f, delta);
            Animate(ref bossRemaining, bossDuration, bossExplosion, 0.12f, 5.5f, delta);
        }

        public void ResetEffects()
        {
            hitRemaining = 0f;
            explosionRemaining = 0f;
            bossRemaining = 0f;
            SetActive(hitFlash, false);
            SetActive(meteorExplosion, false);
            SetActive(bossExplosion, false);
        }

        private void Subscribe()
        {
            if (weapon != null)
            {
                weapon.Hit -= PlayHitFeedback;
                weapon.Hit += PlayHitFeedback;
            }
            if (boss != null)
            {
                boss.StrongExplosionRequested -= PlayBossExplosion;
                boss.StrongExplosionRequested += PlayBossExplosion;
            }
        }

        private void Unsubscribe()
        {
            if (weapon != null) weapon.Hit -= PlayHitFeedback;
            if (boss != null) boss.StrongExplosionRequested -= PlayBossExplosion;
        }

        private static void Activate(Transform effect, Vector3 position, float scale)
        {
            if (effect == null) return;
            effect.position = position;
            effect.localScale = Vector3.one * scale;
            effect.gameObject.SetActive(true);
        }

        private static void Animate(
            ref float remaining, float duration, Transform effect,
            float startScale, float endScale, float delta)
        {
            if (remaining <= 0f || effect == null) return;
            remaining = Mathf.Max(0f, remaining - delta);
            float progress = duration > 0f ? 1f - remaining / duration : 1f;
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(progress), 3f);
            effect.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, eased);
            effect.Rotate(Vector3.one, 180f * delta, Space.Self);
            if (remaining <= 0f) effect.gameObject.SetActive(false);
        }

        private static void SetActive(Transform effect, bool active)
        {
            if (effect != null) effect.gameObject.SetActive(active);
        }
    }
}
