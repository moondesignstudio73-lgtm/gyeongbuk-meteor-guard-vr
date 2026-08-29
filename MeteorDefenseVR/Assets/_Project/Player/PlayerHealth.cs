using System;
using System.Collections.Generic;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.UI;
using UnityEngine;
using MeteorDefenseVR.Core;

namespace MeteorDefenseVR.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private bool useMeteorDamage = true;
        [SerializeField, Min(1)] private int defaultMeteorDamage = 10;
        [SerializeField, Min(0f)] private float invincibilityTime = 0.35f;
        [SerializeField] private GameOverPolicy gameOverPolicy = GameOverPolicy.NoFailExperienceMode;

        [Header("Connections")]
        [SerializeField] private MeteorSpawner spawner;
        [SerializeField] private MissionHudController missionHud;

        private readonly HashSet<MeteorController> trackedMeteors = new HashSet<MeteorController>();
        private GameFlowManager gameFlow;
        private float invincibilityRemaining;

        public float CurrentHealth { get; private set; }
        public float MaxHealth => RuntimeValueGuard.Clamp(maxHealth, 1, 10000, 100);
        public float HealthNormalized => Mathf.Clamp01(CurrentHealth / MaxHealth);
        public int TotalDamageTaken { get; private set; }
        public bool IsInvincible => invincibilityRemaining > 0f;
        public bool HealthDepleted { get; private set; }
        public bool IsMissionFailed { get; private set; }
        public GameOverPolicy Policy => gameOverPolicy;

        public event Action<float, float> HealthChanged;
        public event Action<int, float> DamageTaken;
        public event Action HudFlashRequested;
        public event Action ImpactFeedbackRequested;
        public event Action MissionFailed;
        public event Action HealthDepletedInNoFailMode;
        public event Action<Vector3, bool> ShipImpacted;
        public event Action HealthReset;
        public event Action<float, float> HealthRepaired;
        public Func<bool> MissionFailureHandler { get; set; }
        public Func<int, Vector3, bool> DamageInterceptor { get; set; }

        private void Awake()
        {
            gameFlow = GameFlowManager.Instance;
            CurrentHealth = MaxHealth;
        }

        private void OnEnable() => Subscribe();

        private void Update() => Tick(Time.deltaTime);

        private void OnDisable()
        {
            Unsubscribe();
            UnsubscribeMeteors();
        }

        public void Configure(
            MeteorSpawner meteorSpawner,
            MissionHudController hud,
            float maximumHealth,
            int fallbackDamage,
            float invincibilityDuration,
            GameOverPolicy policy)
        {
            Unsubscribe();
            UnsubscribeMeteors();
            spawner = meteorSpawner;
            missionHud = hud;
            maxHealth = RuntimeValueGuard.Clamp(maximumHealth, 1, 10000, 100);
            defaultMeteorDamage = Mathf.Max(1, fallbackDamage);
            invincibilityTime = RuntimeValueGuard.Clamp(invincibilityDuration, 0, 10, .35f);
            gameOverPolicy = policy;
            Subscribe();
            ResetHealth();
        }

        public void BindGameFlow(GameFlowManager flow) => gameFlow = flow;

        public void Connect(MeteorSpawner meteorSpawner, MissionHudController hud)
        {
            Unsubscribe();
            UnsubscribeMeteors();
            spawner = meteorSpawner;
            missionHud = hud;
            Subscribe();
            NotifyHealthChanged();
        }

        public void SetPolicy(GameOverPolicy policy) => gameOverPolicy = policy;

        public void ResetHealth()
        {
            UnsubscribeMeteors();
            CurrentHealth = MaxHealth;
            TotalDamageTaken = 0;
            invincibilityRemaining = 0f;
            HealthDepleted = false;
            IsMissionFailed = false;
            NotifyHealthChanged();
            HealthReset?.Invoke();
        }

        public bool ApplyDamage(int amount) => ApplyDamageInternal(amount, transform.position, false);
        public bool ApplyImpactDamage(int amount, Vector3 position) => ApplyDamageInternal(amount, position, true);

        public float RepairNormalized(float normalizedAmount)
        {
            if (IsMissionFailed || normalizedAmount <= 0f) return 0f;
            float previous = CurrentHealth;
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + MaxHealth * Mathf.Clamp01(normalizedAmount));
            float restored = CurrentHealth - previous;
            if (restored <= 0f) return 0f;
            NotifyHealthChanged();
            HealthRepaired?.Invoke(restored, CurrentHealth);
            missionHud?.ShowWarning($"STAGE CLEAR REPAIR  +{Mathf.RoundToInt(restored)}", 1.6f);
            return restored;
        }

        public void RestoreCheckpoint(float health)
        {
            CurrentHealth = Mathf.Clamp(health, 1f, MaxHealth);
            invincibilityRemaining = 0f;
            HealthDepleted = false;
            IsMissionFailed = false;
            NotifyHealthChanged();
        }

        private bool ApplyDamageInternal(int amount, Vector3 position, bool shipImpact)
        {
            if (amount <= 0 || IsMissionFailed) return false;
            if (DamageInterceptor != null && DamageInterceptor.Invoke(amount, position))
            {
                if (shipImpact) ShipImpacted?.Invoke(position, false);
                return false;
            }
            if (IsInvincible || (CurrentHealth <= 0f && HealthDepleted))
            {
                if (shipImpact) ShipImpacted?.Invoke(position, false);
                return false;
            }
            float previous = CurrentHealth;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            int applied = Mathf.RoundToInt(previous - CurrentHealth);
            if (applied <= 0 && HealthDepleted) return false;

            TotalDamageTaken = RuntimeValueGuard.Add(TotalDamageTaken, applied);
            invincibilityRemaining = RuntimeValueGuard.Clamp(invincibilityTime, 0, 10, .35f);
            NotifyHealthChanged();
            DamageTaken?.Invoke(applied, CurrentHealth);
            HudFlashRequested?.Invoke();
            ImpactFeedbackRequested?.Invoke();
            missionHud?.ShowWarning("WARNING", 0.75f);
            if (shipImpact) ShipImpacted?.Invoke(position, true);

            if (CurrentHealth <= 0f && !HealthDepleted)
                HandleHealthDepleted();
            return true;
        }

        public void Tick(float deltaTime)
        {
            invincibilityRemaining = Mathf.Max(0f, invincibilityRemaining - RuntimeValueGuard.Delta(deltaTime));
        }

        private void HandleHealthDepleted()
        {
            HealthDepleted = true;
            if (gameOverPolicy == GameOverPolicy.NoFailExperienceMode)
            {
                missionHud?.ShowWarning("HP 0 - 계속 방어하세요!", 1.5f);
                HealthDepletedInNoFailMode?.Invoke();
                return;
            }

            IsMissionFailed = true;
            missionHud?.ShowWarning("MISSION FAILED", 3f);
            spawner?.StopSpawning(true);
            MissionFailed?.Invoke();
            if (MissionFailureHandler == null || !MissionFailureHandler.Invoke()) gameFlow?.ShowResult();
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled || spawner == null) return;
            spawner.SpawningStarted -= HandleSpawningStarted;
            spawner.MeteorSpawned -= HandleMeteorSpawned;
            spawner.SpawningStarted += HandleSpawningStarted;
            spawner.MeteorSpawned += HandleMeteorSpawned;
        }

        private void Unsubscribe()
        {
            if (spawner == null) return;
            spawner.SpawningStarted -= HandleSpawningStarted;
            spawner.MeteorSpawned -= HandleMeteorSpawned;
        }

        private void HandleSpawningStarted(int _) => ResetHealth();

        private void HandleMeteorSpawned(MeteorController meteor, int _, int __)
        {
            if (meteor == null || !trackedMeteors.Add(meteor)) return;
            meteor.ReachedPlayerEvent += HandleMeteorReachedPlayer;
            meteor.Destroyed += HandleMeteorDestroyed;
        }

        private void HandleMeteorReachedPlayer(MeteorController meteor, int meteorDamage)
        {
            int damage = useMeteorDamage ? meteorDamage : defaultMeteorDamage;
            ApplyImpactDamage(damage > 0 ? damage : defaultMeteorDamage, meteor.ImpactPosition);
            UntrackMeteor(meteor);
        }

        private void HandleMeteorDestroyed(MeteorController meteor) => UntrackMeteor(meteor);

        private void UntrackMeteor(MeteorController meteor)
        {
            if (meteor == null || !trackedMeteors.Remove(meteor)) return;
            meteor.ReachedPlayerEvent -= HandleMeteorReachedPlayer;
            meteor.Destroyed -= HandleMeteorDestroyed;
        }

        private void UnsubscribeMeteors()
        {
            foreach (MeteorController meteor in trackedMeteors)
            {
                if (meteor == null) continue;
                meteor.ReachedPlayerEvent -= HandleMeteorReachedPlayer;
                meteor.Destroyed -= HandleMeteorDestroyed;
            }
            trackedMeteors.Clear();
        }

        private void NotifyHealthChanged()
        {
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            missionHud?.SetHealth(CurrentHealth, MaxHealth);
        }
    }
}
