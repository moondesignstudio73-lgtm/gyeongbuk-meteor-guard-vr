using System;
using System.Collections.Generic;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Meteor;
using UnityEngine;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;

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
        private int extraMeteors;
        private int extraCompleted;
        private bool campaignMode;
        private bool bossReserved;

        public float CurrentHealth { get; private set; }
        public float MaxHealth { get; private set; }
        public float HealthNormalized => MaxHealth > 0f ? Mathf.Clamp01(CurrentHealth / MaxHealth) : 0f;
        public int RemainingMeteors { get; private set; }
        public int TotalMeteors { get; private set; }
        public int Score { get; private set; }
        public string FeedbackMessage { get; private set; } = string.Empty;
        public int CampaignStage { get; private set; }
        public DifficultyLevel CampaignDifficulty { get; private set; } = DifficultyLevel.Normal;
        public bool BossIncoming { get; private set; }
        public float BossHealthNormalized { get; private set; }

        public event Action<float, float> HealthChanged;
        public event Action<int, int> MeteorProgressChanged;
        public event Action<int> ScoreChanged;
        public event Action<string> FeedbackChanged;

        private void Awake()
        {
            MaxHealth = RuntimeValueGuard.Clamp(defaultMaxHealth, 1, 10000, 100);
            CurrentHealth = MaxHealth;
        }

        private void OnEnable()
        {
            Subscribe();
            // The display can activate after SpawningStarted in the same state transition.
            RecalculateMeteorProgress();
            if (spawner != null) foreach (MeteorController meteor in spawner.ActiveMeteors) TrackMeteor(meteor);
        }

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
            defaultMaxHealth = RuntimeValueGuard.Clamp(maxHealth, 1, 10000, 100);
            MaxHealth = defaultMaxHealth;
            CurrentHealth = MaxHealth;
            Subscribe();
            ResetSession();
        }

        public void ResetSession()
        {
            UnsubscribeMeteors();
            MaxHealth = RuntimeValueGuard.Clamp(defaultMaxHealth, 1, 10000, 100);
            CurrentHealth = MaxHealth;
            Score = 0;
            TotalMeteors = spawner != null ? spawner.TotalScheduled : 0;
            RemainingMeteors = TotalMeteors;
            extraMeteors = 0;
            extraCompleted = 0;
            SetFeedback(string.Empty, 0f);
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            MeteorProgressChanged?.Invoke(RemainingMeteors, TotalMeteors);
            ScoreChanged?.Invoke(Score);
        }

        public void SetHealth(float current, float maximum)
        {
            MaxHealth = RuntimeValueGuard.Clamp(maximum, 1, 10000, 100);
            CurrentHealth = RuntimeValueGuard.Clamp(current, 0, MaxHealth, MaxHealth);
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        public void AddScore(int amount)
        {
            if (amount <= 0) return;
            Score = RuntimeValueGuard.Add(Score, amount);
            ScoreChanged?.Invoke(Score);
            string praise = amount >= 150 ? "NICE!" : "GOOD!";
            SetFeedback($"+{amount:N0}  {praise}", feedbackDuration);
        }

        public void RestoreScore(int value)
        {
            Score = Mathf.Max(0, value);
            ScoreChanged?.Invoke(Score);
        }

        public void SetCampaignStage(DifficultyLevel difficulty, int stage, int regularMeteorCount)
        {
            campaignMode = true;
            CampaignDifficulty = difficulty;
            CampaignStage = Mathf.Max(1, stage);
            bossReserved = true;
            BossIncoming = false;
            BossHealthNormalized = 1f;
            extraMeteors = 1;
            extraCompleted = 0;
            TotalMeteors = Mathf.Max(1, regularMeteorCount) + 1;
            RemainingMeteors = TotalMeteors;
            MeteorProgressChanged?.Invoke(RemainingMeteors, TotalMeteors);
        }

        public void ClearCampaignStage()
        {
            campaignMode = false; bossReserved = false; CampaignStage = 0; BossIncoming = false; BossHealthNormalized = 0f;
        }

        public void SetBossIncoming(bool active, float normalizedHealth = 1f)
        {
            BossIncoming = active;
            BossHealthNormalized = Mathf.Clamp01(normalizedHealth);
            MeteorProgressChanged?.Invoke(RemainingMeteors, TotalMeteors);
        }

        public void ShowLockOn()
        {
            SetFeedback("LOCK ON", Mathf.Min(0.6f, feedbackDuration));
        }

        public void ShowWarning(string message, float duration = 0.8f)
        {
            SetFeedback(message, Mathf.Max(0f, duration));
        }

        public void RegisterExtraMeteor()
        {
            if (!bossReserved) extraMeteors++;
            bossReserved = false;
            BossIncoming = true;
            RecalculateMeteorProgress();
        }

        public void CompleteExtraMeteor()
        {
            extraCompleted = Mathf.Min(extraMeteors, extraCompleted + 1);
            BossIncoming = false;
            RecalculateMeteorProgress();
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
                spawner.StageSpawningStarted -= HandleStageSpawningStarted;
                spawner.StageSpawningStarted += HandleStageSpawningStarted;
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
                spawner.StageSpawningStarted -= HandleStageSpawningStarted;
            }
            if (laserWeapon != null) laserWeapon.Fired -= HandleWeaponFired;
        }

        private void HandleSpawningStarted(int total)
        {
            ResetSession();
            TotalMeteors = Mathf.Max(0, total);
            extraMeteors = campaignMode ? 1 : 0;
            bossReserved = campaignMode;
            extraCompleted = 0;
            TotalMeteors += extraMeteors;
            RemainingMeteors = TotalMeteors;
            MeteorProgressChanged?.Invoke(RemainingMeteors, TotalMeteors);
        }

        private void HandleStageSpawningStarted(int _, int total)
        {
            UnsubscribeMeteors();
            extraMeteors = campaignMode ? 1 : 0;
            bossReserved = campaignMode;
            extraCompleted = 0;
            BossIncoming = false;
            TotalMeteors = Mathf.Max(0, total) + extraMeteors;
            RemainingMeteors = TotalMeteors;
            MeteorProgressChanged?.Invoke(RemainingMeteors, TotalMeteors);
        }

        private void HandleMeteorSpawned(MeteorController meteor, int _, int total)
        {
            TotalMeteors = Mathf.Max(TotalMeteors, total + extraMeteors);
            RecalculateMeteorProgress();
            TrackMeteor(meteor);
        }

        private void HandleMeteorCompleted(MeteorController meteor)
        {
            RecalculateMeteorProgress();
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

        private void RecalculateMeteorProgress()
        {
            int baseTotal = spawner != null ? spawner.TotalScheduled : Mathf.Max(0, TotalMeteors - extraMeteors);
            int baseCompleted = spawner != null ? spawner.CompletedCount : 0;
            TotalMeteors = baseTotal + extraMeteors;
            RemainingMeteors = Mathf.Max(0, TotalMeteors - baseCompleted - extraCompleted);
            MeteorProgressChanged?.Invoke(RemainingMeteors, TotalMeteors);
        }
    }
}
