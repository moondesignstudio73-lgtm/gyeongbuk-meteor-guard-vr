using System;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.Meteor;
using UnityEngine;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;

namespace MeteorDefenseVR.Combat
{
    public enum LockOnState
    {
        Idle,
        Focusing,
        Locked,
        Destroyed
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class GazeLockOnTarget : MonoBehaviour, IGazeTarget
    {
        [Header("Lock On")]
        [SerializeField, Min(0.1f)] private float lockOnDuration = 0.62f;
        [SerializeField, Min(0f)] private float lockDecaySpeed = 1.5f;
        [SerializeField, Min(0.1f)] private float maximumTargetDistance = 50f;

        [Header("Assist")]
        [SerializeField, Range(1f, 2f)] private float targetColliderExpansion = 1.25f;
        [SerializeField, Min(0f)] private float gracePeriod = 0.15f;

        [Header("Dependencies")]
        [SerializeField] private MeteorController meteor;

        private static GazeLockOnTarget activeTarget;
        private bool hasGaze;
        private float timeSinceGazeExit;
        private bool colliderExpanded;
        private MeteorController subscribedMeteor;

        public LockOnState State { get; private set; } = LockOnState.Idle;
        public float Progress { get; private set; }
        public bool IsLocked => State == LockOnState.Locked;
        public bool IsAvailable => isActiveAndEnabled && (meteor == null || meteor.IsTargetable);
        public MeteorController Meteor => meteor;
        public float LockOnDuration { get => RuntimeValueGuard.Clamp(lockOnDuration, .1f, 10, .62f); set => lockOnDuration = RuntimeValueGuard.Clamp(value, .1f, 10, .62f); }
        public float LockDecaySpeed { get => RuntimeValueGuard.Clamp(lockDecaySpeed, 0, 100, 1.5f); set => lockDecaySpeed = RuntimeValueGuard.Clamp(value, 0, 100, 1.5f); }
        public float GracePeriod { get => RuntimeValueGuard.Clamp(gracePeriod, 0, 10, .15f); set => gracePeriod = RuntimeValueGuard.Clamp(value, 0, 10, .15f); }
        public float ColliderExpansion => targetColliderExpansion;
        public bool HasDifficultyTuning { get; private set; }
        public static GazeLockOnTarget ActiveTarget => activeTarget != null && activeTarget.IsAvailable ? activeTarget : null;

        public event Action<GazeLockOnTarget> FocusStarted;
        public event Action<GazeLockOnTarget, float> ProgressChanged;
        public event Action<GazeLockOnTarget> Locked;
        public event Action<GazeLockOnTarget> LockLost;

        public static event Action<GazeLockOnTarget> GlobalFocusStarted;
        public static event Action<GazeLockOnTarget> GlobalLocked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            activeTarget = null;
            GlobalFocusStarted = null;
            GlobalLocked = null;
        }

        private void Awake()
        {
            if (meteor == null) meteor = GetComponentInParent<MeteorController>();
            targetColliderExpansion = RuntimeValueGuard.Clamp(targetColliderExpansion, 1, 2, 1.25f);
            ExpandTargetCollider();
        }

        private void OnEnable()
        {
            SubscribeMeteor();
        }

        private void Update()
        {
            if (!hasGaze) TickWithoutGaze(Time.deltaTime);
        }

        private void OnDisable()
        {
            UnsubscribeMeteor();
            ForceRelease();
        }

        public void Configure(MeteorController meteorController, float duration = 0.62f, float decay = 1.5f, float grace = 0.18f)
        {
            HasDifficultyTuning = false;
            UnsubscribeMeteor();
            meteor = meteorController;
            LockOnDuration = duration;
            LockDecaySpeed = decay;
            GracePeriod = grace;
            if (isActiveAndEnabled) SubscribeMeteor();
        }

        public void ApplyDifficulty(DifficultyBalance balance)
        {
            HasDifficultyTuning = true;
            LockOnDuration = balance.LockSeconds; GracePeriod = balance.Grace; LockDecaySpeed = balance.Decay;
            ConfigureComfortTolerance(balance.ColliderExpansion);
        }

        public void ConfigureComfortTolerance(float colliderExpansion, float maxDistance = 50f)
        {
            float nextExpansion = RuntimeValueGuard.Clamp(colliderExpansion, 1, 2, 1.25f);
            if (colliderExpanded && !Mathf.Approximately(nextExpansion, targetColliderExpansion))
            {
                float ratio = nextExpansion / Mathf.Max(1f, targetColliderExpansion);
                Collider collider = GetComponent<Collider>();
                if (collider is SphereCollider sphere) sphere.radius *= ratio;
                else if (collider is BoxCollider box) box.size *= ratio;
                else if (collider is CapsuleCollider capsule) { capsule.radius *= ratio; capsule.height *= ratio; }
            }
            targetColliderExpansion = nextExpansion;
            if (!colliderExpanded) ExpandTargetCollider();
            maximumTargetDistance = RuntimeValueGuard.Clamp(maxDistance, .1f, 1000, 50);
        }

        public void OnGazeEnter(RaycastHit hit)
        {
            if (!CanFocus(hit.distance)) return;
            AcquireExclusiveFocus();
            hasGaze = true;
            timeSinceGazeExit = 0f;
            if (State == LockOnState.Idle)
            {
                SetState(LockOnState.Focusing);
                FocusStarted?.Invoke(this);
                GlobalFocusStarted?.Invoke(this);
            }
        }

        public void OnGazeStay(RaycastHit hit, float deltaTime)
        {
            if (!CanFocus(hit.distance))
            {
                OnGazeExit();
                return;
            }
            if (!hasGaze) OnGazeEnter(hit);
            Advance(deltaTime);
        }

        public void OnGazeExit()
        {
            hasGaze = false;
            timeSinceGazeExit = 0f;
        }

        public void Advance(float deltaTime)
        {
            if (!IsAvailable || State == LockOnState.Locked || State == LockOnState.Destroyed || !RuntimeValueGuard.IsFinite(deltaTime) || deltaTime <= 0f) return;
            if (State == LockOnState.Idle)
            {
                AcquireExclusiveFocus();
                SetState(LockOnState.Focusing);
                FocusStarted?.Invoke(this);
                GlobalFocusStarted?.Invoke(this);
            }
            SetProgress(Progress + deltaTime / LockOnDuration);
            if (Progress < 1f) return;
            SetState(LockOnState.Locked);
            Locked?.Invoke(this);
            GlobalLocked?.Invoke(this);
        }

        public void TickWithoutGaze(float deltaTime)
        {
            if (State != LockOnState.Focusing || !RuntimeValueGuard.IsFinite(deltaTime) || deltaTime <= 0f) return;
            timeSinceGazeExit += deltaTime;
            if (timeSinceGazeExit <= GracePeriod) return;
            SetProgress(Progress - LockDecaySpeed * deltaTime);
            if (Progress <= 0f) ForceRelease();
        }

        public void UnlockAfterAttack()
        {
            if (State == LockOnState.Destroyed) return;
            SetProgress(0f);
            SetState(hasGaze && IsAvailable ? LockOnState.Focusing : LockOnState.Idle);
        }

        public void ForceRelease()
        {
            bool hadFocus = State == LockOnState.Focusing || State == LockOnState.Locked;
            hasGaze = false;
            timeSinceGazeExit = 0f;
            SetProgress(0f);
            if (State != LockOnState.Destroyed) SetState(LockOnState.Idle);
            if (activeTarget == this) activeTarget = null;
            if (hadFocus) LockLost?.Invoke(this);
        }

        public void ResetLock()
        {
            State = LockOnState.Idle;
            Progress = 0f;
            hasGaze = false;
            timeSinceGazeExit = 0f;
            if (activeTarget == this) activeTarget = null;
            ProgressChanged?.Invoke(this, Progress);
        }

        private bool CanFocus(float hitDistance) => IsAvailable && hitDistance <= maximumTargetDistance && State != LockOnState.Destroyed;

        private void SetProgress(float value)
        {
            float next = Mathf.Clamp01(value);
            if (Mathf.Approximately(next, Progress)) return;
            Progress = next;
            ProgressChanged?.Invoke(this, Progress);
        }

        private void SetState(LockOnState state) => State = state;

        private void HandleMeteorSpawned(MeteorController _) => ResetLock();
        private void HandleMeteorReset(MeteorController _) => ResetLock();

        private void HandleMeteorDestroyed(MeteorController _)
        {
            hasGaze = false;
            SetState(LockOnState.Destroyed);
            if (activeTarget == this) activeTarget = null;
        }

        private void ExpandTargetCollider()
        {
            if (colliderExpanded || targetColliderExpansion <= 1f) return;
            Collider targetCollider = GetComponent<Collider>();
            if (targetCollider is SphereCollider sphere) sphere.radius *= targetColliderExpansion;
            else if (targetCollider is BoxCollider box) box.size *= targetColliderExpansion;
            else if (targetCollider is CapsuleCollider capsule)
            {
                capsule.radius *= targetColliderExpansion;
                capsule.height *= targetColliderExpansion;
            }
            colliderExpanded = true;
        }

        private void AcquireExclusiveFocus()
        {
            if (activeTarget != null && activeTarget != this) activeTarget.ForceRelease();
            activeTarget = this;
        }

        private void SubscribeMeteor()
        {
            if (meteor == null || subscribedMeteor == meteor) return;
            subscribedMeteor = meteor;
            subscribedMeteor.Spawned += HandleMeteorSpawned;
            subscribedMeteor.Destroyed += HandleMeteorDestroyed;
            subscribedMeteor.ResetPerformed += HandleMeteorReset;
        }

        private void UnsubscribeMeteor()
        {
            if (subscribedMeteor == null) return;
            subscribedMeteor.Spawned -= HandleMeteorSpawned;
            subscribedMeteor.Destroyed -= HandleMeteorDestroyed;
            subscribedMeteor.ResetPerformed -= HandleMeteorReset;
            subscribedMeteor = null;
        }
    }
}
