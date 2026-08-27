using System;
using System.Collections.Generic;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Meteor;
using UnityEngine;

namespace MeteorDefenseVR.UI
{
    [DisallowMultipleComponent]
    public sealed class MissionHudController : MonoBehaviour
    {
        [SerializeField] private MeteorSpawner spawner;
        [SerializeField] private LaserWeapon laserWeapon;
        [SerializeField, Min(1f)] private float defaultMaxHealth = 100f;
        [SerializeField, Min(0f)] private float feedbackDuration = 1.1f;

        private readonly HashSet<MeteorController> trackedMeteors = new HashSet<MeteorController>();
        private float feedbackRemaining;

        public float CurrentHealth { get; private set; }
        public float MaxHealth { get; private set; }
        public float HealthNormalized => MaxHealth > 0f ? Mathf.Clamp01(CurrentHealth / MaxHealth) : 0f;
        public int RemainingMeteors { get; private set; }
        public int TotalMeteors { get; private set; }
        public int Score { get; private set; }
        public string FeedbackMessage { get; private set; } = string.Empty;

        public event Action<float, float> HealthChanged;
        public event Action<int, int> MeteorProgressChanged;
        public event Action<int> ScoreChanged;
        public event Action<string> FeedbackChanged;

        private void Awake()
        {
            MaxHealth = Mathf.Max(1f, defaultMaxHealth);
            CurrentHealth = MaxHealth;
        }

        private void OnEnable() => Subscribe();

        private void Update() => Tick(Time.unscaledDeltaTime);

        private void OnDisable()
        {
            Unsubscribe();
            UnsubscribeMeteors();
        }

        public void Configure(MeteorSpawner meteorSpawner, LaserWeapon weapon, float maxHealth = 100f)
        {
            Unsubscribe();
            UnsubscribeMeteors();
            spawner = meteorSpawner;
            laserWeapon = weapon;
            defaultMaxHealth = Mathf.Max(1f, maxHealth);
            MaxHealth = defaultMaxHealth;
            CurrentHealth = MaxHealth;
            Subscribe();
            ResetSession();
        }

        public void ResetSession()
        {
            UnsubscribeMeteors();
            MaxHealth = Mathf.Max(1f, defaultMaxHealth);
            CurrentHealth = MaxHealth;
            Score = 0;
            TotalMeteors = spawner != null ? spawner.TotalScheduled : 0;
            RemainingMeteors = TotalMeteors;
            SetFeedback(string.Empty, 0f);
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            MeteorProgressChanged?.Invoke(RemainingMeteors, TotalMeteors);
            ScoreChanged?.Invoke(Score);
        }

        public void SetHealth(float current, float maximum)
        {
            MaxHealth = Mathf.Max(1f, maximum);
            CurrentHealth = Mathf.Clamp(current, 0f, MaxHealth);
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        public void AddScore(int amount)
        {
            if (amount <= 0) return;
            Score += amount;
            ScoreChanged?.Invoke(Score);
            string praise = amount >= 150 ? "NICE!" : "GOOD!";
            SetFeedback($"+{amount:N0}  {praise}", feedbackDuration);
        }

        public void ShowLockOn()
        {
            SetFeedback("LOCK ON", Mathf.Min(0.6f, feedbackDuration));
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (feedbackRemaining <= 0f || string.IsNullOrEmpty(FeedbackMessage)) return;
            feedbackRemaining = Mathf.Max(0f, feedbackRemaining - Mathf.Max(0f, unscaledDeltaTime));
            if (feedbackRemaining <= 0f) SetFeedback(string.Empty, 0f);
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled) return;
            if (spawner != null)
            {
                spawner.SpawningStarted -= HandleSpawningStarted;
                spawner.MeteorSpawned -= HandleMeteorSpawned;
                spawner.MeteorCompleted -= HandleMeteorCompleted;
                spawner.SpawningStarted += HandleSpawningStarted;
                spawner.MeteorSpawned += HandleMeteorSpawned;
                spawner.MeteorCompleted += HandleMeteorCompleted;
            }
            if (laserWeapon != null)
            {
                laserWeapon.Fired -= HandleWeaponFired;
                laserWeapon.Fired += HandleWeaponFired;
            }
        }

        private void Unsubscribe()
        {
            if (spawner != null)
            {
                spawner.SpawningStarted -= HandleSpawningStarted;
                spawner.MeteorSpawned -= HandleMeteorSpawned;
                spawner.MeteorCompleted -= HandleMeteorCompleted;
            }
            if (laserWeapon != null) laserWeapon.Fired -= HandleWeaponFired;
        }

        private void HandleSpawningStarted(int total)
        {
            ResetSession();
            TotalMeteors = Mathf.Max(0, total);
            RemainingMeteors = TotalMeteors;
            MeteorProgressChanged?.Invoke(RemainingMeteors, TotalMeteors);
        }

        private void HandleMeteorSpawned(MeteorController meteor, int _, int total)
        {
            TotalMeteors = Mathf.Max(TotalMeteors, total);
            RemainingMeteors = Mathf.Max(0, TotalMeteors - (spawner != null ? spawner.CompletedCount : 0));
            TrackMeteor(meteor);
            MeteorProgressChanged?.Invoke(RemainingMeteors, TotalMeteors);
        }

        private void HandleMeteorCompleted(MeteorController meteor)
        {
            RemainingMeteors = Mathf.Max(0, TotalMeteors - (spawner != null ? spawner.CompletedCount : 0));
            MeteorProgressChanged?.Invoke(RemainingMeteors, TotalMeteors);
            if (meteor != null && meteor.State != MeteorLifecycleState.Destroyed) UntrackMeteor(meteor);
        }

        private void HandleScoreAwarded(MeteorController meteor, int amount)
        {
            AddScore(amount);
            UntrackMeteor(meteor);
        }

        private void HandleWeaponFired(GazeLockOnTarget _, MeteorController __) => ShowLockOn();

        private void TrackMeteor(MeteorController meteor)
        {
            if (meteor == null || !trackedMeteors.Add(meteor)) return;
            meteor.ScoreAwarded += HandleScoreAwarded;
        }

        private void UntrackMeteor(MeteorController meteor)
        {
            if (meteor == null || !trackedMeteors.Remove(meteor)) return;
            meteor.ScoreAwarded -= HandleScoreAwarded;
        }

        private void UnsubscribeMeteors()
        {
            foreach (MeteorController meteor in trackedMeteors)
                if (meteor != null) meteor.ScoreAwarded -= HandleScoreAwarded;
            trackedMeteors.Clear();
        }

        private void SetFeedback(string message, float duration)
        {
            FeedbackMessage = message ?? string.Empty;
            feedbackRemaining = Mathf.Max(0f, duration);
            FeedbackChanged?.Invoke(FeedbackMessage);
        }
    }
}
