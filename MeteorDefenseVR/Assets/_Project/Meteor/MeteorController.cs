using System;
using UnityEngine;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Player;

namespace MeteorDefenseVR.Meteor
{
    public enum MeteorLifecycleState
    {
        Inactive,
        Active,
        Destroyed,
        ReachedPlayer
    }

    [DisallowMultipleComponent]
    public sealed class MeteorController : MonoBehaviour
    {
        [Header("Definition")]
        [SerializeField] private MeteorDefinition definition;
        [SerializeField] private MeteorType meteorType = MeteorType.Normal;
        [SerializeField, Min(1f)] private float maxHealth = 1f;
        [SerializeField, Min(0f)] private float speed = 2f;
        [SerializeField, Min(0)] private int damage = 10;
        [SerializeField, Min(0)] private int score = 100;
        [SerializeField, Min(0.1f)] private float size = 1f;
        [SerializeField] private bool isTargetable = true;

        [Header("Movement")]
        [SerializeField] private Transform movementTarget;
        [SerializeField] private Vector3 movementDirection = Vector3.back;
        [SerializeField, Min(0.01f)] private float reachDistance = 0.25f;
        [SerializeField] private bool rotateWhileMoving = true;
        [SerializeField] private Vector3 angularVelocity = new Vector3(15f, 25f, 10f);

        [Header("Lifecycle")]
        [SerializeField] private bool spawnOnEnable;
        [SerializeField] private bool disableOnComplete = true;

        private bool completionEventSent;
        private float runtimeSpeedMultiplier = 1f;
        private ShipCollisionBoundary impactBoundary;
        private MeshFilter[] visualMeshes;
        public float CollisionRadius { get; private set; }
        public Vector3 ImpactPosition { get; private set; }
        public bool HasBoundaryImpact { get; private set; }

        public MeteorType MeteorType => meteorType;
        public MeteorLifecycleState State { get; private set; } = MeteorLifecycleState.Inactive;
        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;
        public float Speed => speed * runtimeSpeedMultiplier;
        public Vector3 Velocity => (movementTarget != null ? (movementTarget.position - transform.position).normalized : movementDirection.normalized) * Speed;
        public int Damage => damage;
        public int Score => score;
        public float Size => size;
        public bool IsTargetable => isTargetable && State == MeteorLifecycleState.Active;

        public event Action<MeteorController> Spawned;
        public event Action<MeteorController, float> Damaged;
        public event Action<MeteorController> Destroyed;
        public event Action<MeteorController, int> ScoreAwarded;
        public event Action<MeteorController, int> ReachedPlayerEvent;
        public event Action<MeteorController> ResetPerformed;

        private void Awake() => ApplyDefinition();

        private void OnEnable()
        {
            if (spawnOnEnable) Spawn();
        }

        private void Update()
        {
            if (State != MeteorLifecycleState.Active) return;
            Move(Time.deltaTime);
        }

        public void ApplyDefinition()
        {
            if (definition != null)
            {
                meteorType = definition.MeteorType;
                maxHealth = definition.Health;
                speed = definition.Speed;
                damage = definition.Damage;
                score = definition.Score;
                size = definition.Size;
                isTargetable = definition.IsTargetable;
            }
            maxHealth = RuntimeValueGuard.Clamp(maxHealth, 1, 10000, 1);
            speed = RuntimeValueGuard.Clamp(speed, 0, 100, 2);
            size = RuntimeValueGuard.Clamp(size, .1f, 20, 1);
            damage = Mathf.Max(0, damage); score = Mathf.Max(0, score);
        }

        public void Configure(MeteorType type, float health, float movementSpeed, int playerDamage, int scoreValue, float scale, bool targetable = true)
        {
            definition = null;
            meteorType = type;
            maxHealth = RuntimeValueGuard.Clamp(health, 1, 10000, 1);
            speed = RuntimeValueGuard.Clamp(movementSpeed, 0, 100, 2);
            damage = Mathf.Max(0, playerDamage);
            score = Mathf.Max(0, scoreValue);
            size = RuntimeValueGuard.Clamp(scale, .1f, 20, 1);
            isTargetable = targetable;
        }

        public void SetMovementTarget(Transform target) => movementTarget = target;
        public void SetRuntimeSpeedMultiplier(float multiplier) => runtimeSpeedMultiplier = RuntimeValueGuard.Clamp(multiplier, .1f, 2f, 1f);
        public void SetImpactBoundary(ShipCollisionBoundary boundary) => impactBoundary = boundary;
        public void SetMovementDirection(Vector3 direction) => movementDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.zero;
        public void SetDefinition(MeteorDefinition meteorDefinition)
        {
            definition = meteorDefinition;
            ApplyDefinition();
        }

        public void Spawn()
        {
            ApplyDefinition();
            completionEventSent = false;
            CurrentHealth = maxHealth;
            State = MeteorLifecycleState.Active;
            transform.localScale = Vector3.one * size;
            HasBoundaryImpact = false;
            visualMeshes ??= GetComponentsInChildren<MeshFilter>(true);
            CollisionRadius = size * .5f;
            foreach (var filter in visualMeshes)
            {
                if (filter == null || filter.sharedMesh == null) continue;
                Bounds bounds = filter.sharedMesh.bounds;
                Vector3 scale = filter.transform.lossyScale;
                float radius = bounds.extents.magnitude * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                CollisionRadius = Mathf.Max(CollisionRadius, radius + Vector3.Distance(transform.position, filter.transform.TransformPoint(bounds.center)));
            }
            Spawned?.Invoke(this);
        }

        public void Move(float deltaTime)
        {
            if (State != MeteorLifecycleState.Active || !RuntimeValueGuard.IsFinite(deltaTime) || deltaTime <= 0f) return;
            Vector3 direction = movementTarget != null
                ? movementTarget.position - transform.position
                : movementDirection;

            Vector3 next = transform.position;
            float effectiveSpeed = Speed;
            if (direction.sqrMagnitude > .000001f) next += direction.normalized * (effectiveSpeed * deltaTime);
            if (impactBoundary != null && impactBoundary.isActiveAndEnabled &&
                impactBoundary.Sweep(transform.position, next, CollisionRadius, out Vector3 safe, out Vector3 hit))
            {
                transform.position = safe;
                ImpactPosition = hit;
                HasBoundaryImpact = true;
                ReachPlayer();
                return;
            }
            // Legacy/non-ship targets also cannot be skipped on a long frame.
            if (movementTarget != null && direction.magnitude <= reachDistance + effectiveSpeed * deltaTime)
            {
                transform.position = movementTarget.position - direction.normalized * reachDistance;
                ReachPlayer();
                return;
            }

            transform.position = next;
            if (rotateWhileMoving)
                transform.Rotate(angularVelocity * deltaTime, Space.Self);
        }

        public bool ReceiveDamage(float amount)
        {
            if (State != MeteorLifecycleState.Active || !RuntimeValueGuard.IsFinite(amount) || amount <= 0f) return false;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            Damaged?.Invoke(this, amount);
            if (CurrentHealth <= 0f) DestroyMeteor();
            return true;
        }

        public bool DestroyMeteor()
        {
            if (State != MeteorLifecycleState.Active || completionEventSent) return false;
            completionEventSent = true;
            State = MeteorLifecycleState.Destroyed;
            Destroyed?.Invoke(this);
            ScoreAwarded?.Invoke(this, score);
            CompleteLifecycle();
            return true;
        }

        public bool ReachPlayer()
        {
            if (State != MeteorLifecycleState.Active || completionEventSent) return false;
            completionEventSent = true;
            State = MeteorLifecycleState.ReachedPlayer;
            if (!HasBoundaryImpact) ImpactPosition = transform.position;
            ReachedPlayerEvent?.Invoke(this, damage);
            CompleteLifecycle();
            return true;
        }

        public void ResetMeteor(bool deactivate = true)
        {
            completionEventSent = false;
            CurrentHealth = maxHealth;
            State = MeteorLifecycleState.Inactive;
            HasBoundaryImpact = false;
            runtimeSpeedMultiplier = 1f;
            ResetPerformed?.Invoke(this);
            if (deactivate) gameObject.SetActive(false);
        }

        private void CompleteLifecycle()
        {
            if (disableOnComplete) gameObject.SetActive(false);
        }
    }
}
