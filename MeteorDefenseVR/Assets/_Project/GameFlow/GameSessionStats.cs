using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.UI;
using UnityEngine;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;

namespace MeteorDefenseVR.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class GameSessionStats : MonoBehaviour
    {
        [SerializeField] private MeteorSpawner spawner;
        [SerializeField] private BossClimaxController bossClimax;
        [SerializeField] private LaserWeapon laserWeapon;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private MissionHudController missionHud;

        private int currentCombo;
        private bool sessionRunning;
        private bool bossCounted;
        private bool suppressNextStageBudget;
        private SessionCheckpoint checkpoint;

        private struct SessionCheckpoint
        {
            public int Score, Destroyed, Total, Shots, Locks, Hits, Damage, Combo, MaxCombo, Bosses, Cleared, Stage, MaxStage;
            public float Time;
        }

        public int Score { get; private set; }
        public DifficultyLevel Difficulty { get; private set; } = DifficultyLevel.Normal;
        public void SetDifficulty(DifficultyLevel level) => Difficulty = level;
        public int DestroyedMeteors { get; private set; }
        public int TotalMeteors { get; private set; }
        public int Shots { get; private set; }
        public int Locks { get; private set; }
        public int SuccessfulHits { get; private set; }
        public int DamageTaken { get; private set; }
        public int MaxCombo { get; private set; }
        public float PlayTime { get; private set; }
        public int CurrentStage { get; private set; } = 1;
        public int MaxStage { get; private set; } = 1;
        public int BossesDestroyed { get; private set; }
        public int ClearedStages { get; private set; }
        public bool CampaignMode { get; private set; }
        public bool AllStagesCleared { get; private set; }
        public float Accuracy => Shots > 0 ? Mathf.Clamp01((float)SuccessfulHits / Shots) : 0f;

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void Update() { if (Time.timeScale > 0) Tick(Time.unscaledDeltaTime); }

        public void Configure(
            MeteorSpawner meteorSpawner,
            BossClimaxController boss,
            LaserWeapon weapon,
            PlayerHealth health,
            MissionHudController hud)
        {
            Unsubscribe();
            spawner = meteorSpawner;
            bossClimax = boss;
            laserWeapon = weapon;
            playerHealth = health;
            missionHud = hud;
            Subscribe();
        }

        public void ResetSession(int totalMeteors = 0)
        {
            Score = 0;
            DestroyedMeteors = 0;
            TotalMeteors = Mathf.Max(0, totalMeteors);
            Shots = 0;
            Locks = 0;
            SuccessfulHits = 0;
            DamageTaken = 0;
            currentCombo = 0;
            MaxCombo = 0;
            PlayTime = 0f;
            bossCounted = false;
            CurrentStage = MaxStage = 1;
            BossesDestroyed = ClearedStages = 0;
            CampaignMode = false;
            AllStagesCleared = false;
            sessionRunning = true;
        }

        public void SetCampaignStage(int stage, bool campaign)
        {
            CampaignMode = campaign;
            CurrentStage = Mathf.Max(1, stage);
            MaxStage = Mathf.Max(MaxStage, CurrentStage);
            bossCounted = false;
        }

        public void AddStageMeteorBudget(int regularCount)
        {
            TotalMeteors = RuntimeValueGuard.Add(TotalMeteors, Mathf.Max(0, regularCount));
            bossCounted = false;
        }

        public void RecordStageCleared(bool final)
        {
            ClearedStages = Mathf.Max(ClearedStages, CurrentStage);
            AllStagesCleared = final;
        }

        public void RestoreStageCheckpoint()
        {
            Score = checkpoint.Score; DestroyedMeteors = checkpoint.Destroyed; TotalMeteors = checkpoint.Total;
            Shots = checkpoint.Shots; Locks = checkpoint.Locks; SuccessfulHits = checkpoint.Hits; DamageTaken = checkpoint.Damage;
            currentCombo = checkpoint.Combo; MaxCombo = checkpoint.MaxCombo; BossesDestroyed = checkpoint.Bosses; ClearedStages = checkpoint.Cleared;
            CurrentStage = checkpoint.Stage; MaxStage = checkpoint.MaxStage; PlayTime = checkpoint.Time;
            bossCounted = false; suppressNextStageBudget = true; missionHud?.RestoreScore(Score);
        }

        private void CaptureStageCheckpoint()
        {
            checkpoint = new SessionCheckpoint { Score = Score, Destroyed = DestroyedMeteors, Total = TotalMeteors,
                Shots = Shots, Locks = Locks, Hits = SuccessfulHits, Damage = DamageTaken, Combo = currentCombo, MaxCombo = MaxCombo,
                Bosses = BossesDestroyed, Cleared = ClearedStages, Stage = CurrentStage, MaxStage = MaxStage, Time = PlayTime };
        }

        public void StopSession() => sessionRunning = false;
        public void SetScore(int score) => Score = Mathf.Max(0, score);
        public void RecordShotAndLock() { Shots = RuntimeValueGuard.Add(Shots, 1); Locks = RuntimeValueGuard.Add(Locks, 1); }
        public void RecordSuccessfulHit() => SuccessfulHits = RuntimeValueGuard.Add(SuccessfulHits, 1);
        public void RecordDamage(int damage) => DamageTaken = RuntimeValueGuard.Add(DamageTaken, damage);

        public void RecordMeteorDestroyed()
        {
            DestroyedMeteors = RuntimeValueGuard.Add(DestroyedMeteors, 1);
            currentCombo = RuntimeValueGuard.Add(currentCombo, 1);
            MaxCombo = Mathf.Max(MaxCombo, currentCombo);
        }

        public void RecordMeteorMissed() => currentCombo = 0;

        public void Tick(float deltaTime)
        {
            if (sessionRunning) PlayTime = Mathf.Min(float.MaxValue, PlayTime + RuntimeValueGuard.Delta(deltaTime));
        }

        public GameSessionSnapshot CreateSnapshot()
        {
            return new GameSessionSnapshot
            {
                Score = Score,
                MissionFailed = playerHealth != null && playerHealth.IsMissionFailed,
                Difficulty = Difficulty,
                DestroyedMeteors = DestroyedMeteors,
                TotalMeteors = TotalMeteors,
                Shots = Shots,
                Locks = Locks,
                SuccessfulHits = SuccessfulHits,
                DamageTaken = DamageTaken,
                RemainingHP = playerHealth != null ? playerHealth.CurrentHealth : 0f,
                MaxCombo = MaxCombo,
                PlayTime = PlayTime,
                CurrentStage = CurrentStage,
                MaxStage = MaxStage,
                BossesDestroyed = BossesDestroyed,
                ClearedStages = ClearedStages,
                CampaignMode = CampaignMode,
                AllStagesCleared = AllStagesCleared
            };
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled) return;
            if (spawner != null)
            {
                spawner.SpawningStarted -= HandleSpawningStarted;
                spawner.MeteorCompleted -= HandleMeteorCompleted;
                spawner.SpawningStarted += HandleSpawningStarted;
                spawner.MeteorCompleted += HandleMeteorCompleted;
                spawner.StageSpawningStarted -= HandleStageSpawningStarted;
                spawner.StageSpawningStarted += HandleStageSpawningStarted;
            }
            if (bossClimax != null)
            {
                bossClimax.BossSpawned -= HandleBossSpawned;
                bossClimax.BossDestroyed -= HandleBossDestroyed;
                bossClimax.BossSpawned += HandleBossSpawned;
                bossClimax.BossDestroyed += HandleBossDestroyed;
            }
            if (laserWeapon != null)
            {
                laserWeapon.Fired -= HandleFired;
                laserWeapon.Hit -= HandleHit;
                laserWeapon.Fired += HandleFired;
                laserWeapon.Hit += HandleHit;
            }
            if (playerHealth != null)
            {
                playerHealth.DamageTaken -= HandleDamage;
                playerHealth.DamageTaken += HandleDamage;
            }
            if (missionHud != null)
            {
                missionHud.ScoreChanged -= SetScore;
                missionHud.ScoreChanged += SetScore;
            }
        }

        private void Unsubscribe()
        {
            if (spawner != null)
            {
                spawner.SpawningStarted -= HandleSpawningStarted;
                spawner.MeteorCompleted -= HandleMeteorCompleted;
                spawner.StageSpawningStarted -= HandleStageSpawningStarted;
            }
            if (bossClimax != null)
            {
                bossClimax.BossSpawned -= HandleBossSpawned;
                bossClimax.BossDestroyed -= HandleBossDestroyed;
            }
            if (laserWeapon != null)
            {
                laserWeapon.Fired -= HandleFired;
                laserWeapon.Hit -= HandleHit;
            }
            if (playerHealth != null) playerHealth.DamageTaken -= HandleDamage;
            if (missionHud != null) missionHud.ScoreChanged -= SetScore;
        }

        private void HandleSpawningStarted(int total)
        {
            ResetSession(total);
            if (spawner != null && spawner.HasCampaignStage) SetCampaignStage(spawner.CampaignStageNumber, true);
            CaptureStageCheckpoint();
        }
        private void HandleStageSpawningStarted(int stage, int total)
        {
            SetCampaignStage(stage, true);
            if (suppressNextStageBudget) { suppressNextStageBudget = false; return; }
            AddStageMeteorBudget(total); CaptureStageCheckpoint();
        }

        private void HandleMeteorCompleted(MeteorController meteor)
        {
            if (meteor != null && meteor.State == MeteorLifecycleState.Destroyed)
            {
                RecordMeteorDestroyed();
                if (currentCombo >= 3) missionHud?.AddScore(Mathf.Min(250, (currentCombo - 2) * 15));
            }
            else RecordMeteorMissed();
        }

        private void HandleBossSpawned(MeteorController _)
        {
            if (bossCounted) return;
            bossCounted = true;
            TotalMeteors++;
        }

        private void HandleBossDestroyed() { RecordMeteorDestroyed(); BossesDestroyed = RuntimeValueGuard.Add(BossesDestroyed, 1); bossCounted = false; }
        private void HandleFired(GazeLockOnTarget _, MeteorController __) => RecordShotAndLock();
        private void HandleHit(MeteorController _, Vector3 __) => RecordSuccessfulHit();
        private void HandleDamage(int amount, float _) => RecordDamage(amount);
    }
}
