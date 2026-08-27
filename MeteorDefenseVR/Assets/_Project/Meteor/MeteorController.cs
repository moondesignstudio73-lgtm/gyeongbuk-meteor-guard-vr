using System;
using UnityEngine;

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

        public MeteorType MeteorType => meteorType;
        public MeteorLifecycleState State { get; private set; } = MeteorLifecycleState.Inactive;
        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;
        public float Speed => speed;
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
            if (definition == null) return;
            meteorType = definition.MeteorType;
            maxHealth = definition.Health;
            speed = definition.Speed;
            damage = definition.Damage;
            score = definition.Score;
            size = definition.Size;
            isTargetable = definition.IsTargetable;
        }

        public void Configure(MeteorType type, float health, float movementSpeed, int playerDamage, int scoreValue, float scale, bool targetable = true)
        {
            definition = null;
            meteorType = type;
            maxHealth = Mathf.Max(1f, health);
            speed = Mathf.Max(0f, movementSpeed);
            damage = Mathf.Max(0, playerDamage);
            score = Mathf.Max(0, scoreValue);
            size = Mathf.Max(0.1f, scale);
            isTargetable = targetable;
        }

        public void SetMovementTarget(Transform target) => movementTarget = target;
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
            Spawned?.Invoke(this);
        }

        public void Move(float deltaTime)
        {
            if (State != MeteorLifecycleState.Active || deltaTime <= 0f) return;
            Vector3 direction = movementTarget != null
                ? movementTarget.position - transform.position
                : movementDirection;

            if (movementTarget != null && direction.sqrMagnitude <= reachDistance * reachDistance)
            {
                ReachPlayer();
                return;
            }

            if (direction.sqrMagnitude > 0.000001f)
                transform.position += direction.normalized * (speed * deltaTime);
            if (rotateWhileMoving)
                transform.Rotate(angularVelocity * deltaTime, Space.Self);
        }

        public bool ReceiveDamage(float amount)
        {
            if (State != MeteorLifecycleState.Active || amount <= 0f) return false;
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
            ReachedPlayerEvent?.Invoke(this, damage);
            CompleteLifecycle();
            return true;
        }

        public void ResetMeteor(bool deactivate = true)
        {
            completionEventSent = false;
            CurrentHealth = maxHealth;
            State = MeteorLifecycleState.Inactive;
            ResetPerformed?.Invoke(this);
            if (deactivate) gameObject.SetActive(false);
        }

        private void CompleteLifecycle()
        {
            if (disableOnComplete) gameObject.SetActive(false);
        }
    }
}
