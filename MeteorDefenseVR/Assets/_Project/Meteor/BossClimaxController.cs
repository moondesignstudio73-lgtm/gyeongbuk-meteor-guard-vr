using System;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.UI;
using MeteorDefenseVR.Progression;
using UnityEngine;

namespace MeteorDefenseVR.Meteor
{
    [DisallowMultipleComponent]
    public sealed class BossClimaxController : MonoBehaviour
    {
        [Header("Connections")]
        [SerializeField] private MeteorSpawner mainSpawner;
        [SerializeField] private GameObject bossPrefab;
        [SerializeField] private Transform spawnOrigin;
        [SerializeField] private Transform movementTarget;
        [SerializeField] private ShipCollisionBoundary impactBoundary;
        public void SetImpactBoundary(ShipCollisionBoundary boundary) => impactBoundary = boundary;
        [SerializeField] private MissionHudController missionHud;
        [SerializeField] private PlayerHealth playerHealth;

        [Header("Sequence")]
        [SerializeField, Min(0f)] private float warningDuration = 1.4f;
        [SerializeField, Min(0f)] private float narrationDuration = 1.2f;
        [SerializeField, Min(0f)] private float retryDelay = 0.8f;

        [Header("Boss")]
        [SerializeField, Min(1f)] private float bossHealth = 3f;
        [SerializeField, Min(0f)] private float bossSpeed = 0.8f;
        [SerializeField, Min(0)] private int bossDamage = 30;
        [SerializeField, Min(0)] private int bossScore = 500;
        [SerializeField, Min(0.1f)] private float bossSize = 3f;
        [SerializeField, Min(1f)] private float spawnDistance = 12f;

        private GameFlowManager gameFlow;
        private float stageElapsed;
        private bool hudBossRegistered;
        private DifficultyBalance? difficulty;
        private StageBalance? campaignStage;
        public Func<int, bool> CampaignCompletionHandler { get; set; }
        public void SetDifficulty(DifficultyBalance balance) => difficulty = balance;
        public void ClearDifficulty() => difficulty = null;
        public void SetCampaignStage(StageBalance balance) => campaignStage = balance;
        public void ClearCampaignStage() => campaignStage = null;
        private readonly ReusableMeteorSlot meteorSlot = new ReusableMeteorSlot();
        public int PreparedMeteorCount => meteorSlot.CreatedCount;
        public void PrewarmMeteor() => meteorSlot.Prewarm(bossPrefab, this, "BossMeteor_Fallback");

        public BossClimaxStage CurrentStage { get; private set; } = BossClimaxStage.Idle;
        public MeteorController ActiveBoss { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsComplete { get; private set; }

        public event Action<BossClimaxStage, string> StageChanged;
        public event Action<MeteorController> BossSpawned;
        public event Action<MeteorController, float> BossDamaged;
        public event Action<Vector3> StrongExplosionRequested;
        public event Action BossDestroyed;

        private void Awake() => gameFlow = GameFlowManager.Instance;
        private void OnEnable() => SubscribeSpawner();
        private void Update() { if (Time.timeScale > 0) Tick(Time.unscaledDeltaTime); }

        private void OnDisable()
        {
            UnsubscribeSpawner();
            UnsubscribeBoss();
            IsRunning = false;
        }

        public void Configure(
            MeteorSpawner spawner,
            GameObject prefab,
            Transform origin,
            Transform target,
            MissionHudController hud,
            PlayerHealth health)
        {
            UnsubscribeSpawner();
            mainSpawner = spawner;
            bossPrefab = prefab;
            spawnOrigin = origin;
            movementTarget = target;
            missionHud = hud;
            playerHealth = health;
            SubscribeSpawner();
        }

        public void BindGameFlow(GameFlowManager flow) => gameFlow = flow;

        public void SetDurations(float warning, float narration, float retry)
        {
            warningDuration = Mathf.Max(0f, warning);
            narrationDuration = Mathf.Max(0f, narration);
            retryDelay = Mathf.Max(0f, retry);
        }

        public void BeginClimax()
        {
            if (IsRunning || IsComplete || (playerHealth != null && playerHealth.IsMissionFailed)) return;
            IsRunning = true;
            IsComplete = false;
            hudBossRegistered = false;
            gameFlow?.StartBoss();
            SetStage(BossClimaxStage.Warning, "WARNING\nMASSIVE OBJECT DETECTED\nBOSS ASTEROID APPROACHING");
        }

        public void ResetClimax()
        {
            UnsubscribeBoss();
            ActiveBoss = null;
            meteorSlot.Release();
            IsRunning = false;
            IsComplete = false;
            hudBossRegistered = false;
            SetStage(BossClimaxStage.Idle, string.Empty);
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!IsRunning) return;
            if (playerHealth != null && playerHealth.IsMissionFailed) { ResetClimax(); return; }
            stageElapsed += Mathf.Max(0f, unscaledDeltaTime);
            switch (CurrentStage)
            {
                case BossClimaxStage.Warning:
                    if (StageFinished(warningDuration))
                        SetStage(BossClimaxStage.Narration, "마지막 운석입니다!\n지구를 지켜주세요!");
                    break;
                case BossClimaxStage.Narration:
                    if (StageFinished(narrationDuration)) SpawnBoss(false);
                    break;
                case BossClimaxStage.RetryDelay:
                    if (StageFinished(retryDelay)) SpawnBoss(true);
                    break;
            }
        }

        private void SpawnBoss(bool isRetry)
        {
            // Boss remains the deliberate forward climax, independent of the spectator/head direction.
            Vector3 originPosition = mainSpawner != null ? mainSpawner.ShipCenter : spawnOrigin != null ? spawnOrigin.position : transform.position;
            Vector3 forward = mainSpawner != null ? mainSpawner.ShipForward : spawnOrigin != null ? spawnOrigin.forward : transform.forward;
            Vector3 position = originPosition + forward * spawnDistance + Vector3.up * 0.35f;
            GameObject bossObject = meteorSlot.Borrow(bossPrefab, this, position, Quaternion.identity, "BossMeteor_Fallback");

            ActiveBoss = bossObject.GetComponent<MeteorController>();
            if (ActiveBoss == null) ActiveBoss = bossObject.AddComponent<MeteorController>();
            int scaledBossScore = difficulty?.ScaleScore(bossScore) ?? bossScore;
            if (campaignStage.HasValue) scaledBossScore = campaignStage.Value.ScaleScore(scaledBossScore);
            ActiveBoss.Configure(MeteorType.Boss,
                bossHealth * (difficulty?.BossHealth ?? 1f) * (campaignStage?.BossHealth ?? 1f),
                bossSpeed * (difficulty?.BossSpeed ?? 1f) * (campaignStage?.BossSpeed ?? 1f),
                difficulty?.ScaleDamage(bossDamage) ?? bossDamage, scaledBossScore,
                campaignStage?.BossScale ?? bossSize);
            ActiveBoss.SetMovementTarget(movementTarget);
            ActiveBoss.SetImpactBoundary(impactBoundary);
            GazeLockOnTarget lockTarget = bossObject.GetComponent<GazeLockOnTarget>();
            if (lockTarget == null) lockTarget = bossObject.AddComponent<GazeLockOnTarget>();
            lockTarget.Configure(ActiveBoss, 0.78f, 1.5f, 0.18f);
            lockTarget.ConfigureComfortTolerance(1.3f);
            if (difficulty.HasValue) lockTarget.ApplyDifficulty(difficulty.Value);
            var stageMotion = bossObject.GetComponent<BossStageMotion>();
            if (stageMotion == null) stageMotion = bossObject.AddComponent<BossStageMotion>();
            stageMotion.Configure(campaignStage?.PatternVariation ?? 0f, campaignStage?.BossFragmentCount ?? 0);
            ActiveBoss.Damaged += HandleBossDamaged;
            ActiveBoss.Destroyed += HandleBossDestroyed;
            ActiveBoss.ReachedPlayerEvent += HandleBossReachedPlayer;
            ActiveBoss.Spawn();

            if (!hudBossRegistered)
            {
                missionHud?.RegisterExtraMeteor();
                hudBossRegistered = true;
            }
            SetStage(BossClimaxStage.Active, isRetry ? "다시 조준하세요!" : "마지막 운석을 파괴하세요!");
            BossSpawned?.Invoke(ActiveBoss);
        }

        private void HandleBossDamaged(MeteorController boss, float damage) => BossDamaged?.Invoke(boss, damage);

        private void HandleBossDestroyed(MeteorController boss)
        {
            Vector3 position = boss != null ? boss.transform.position : transform.position;
            UnsubscribeBoss();
            ActiveBoss = null;
            IsRunning = false;
            IsComplete = true;
            missionHud?.CompleteExtraMeteor();
            missionHud?.AddScore(boss != null ? boss.Score : (difficulty?.ScaleScore(bossScore) ?? bossScore));
            SetStage(BossClimaxStage.Complete, string.Empty);
            StrongExplosionRequested?.Invoke(position);
            BossDestroyed?.Invoke();
            if (CampaignCompletionHandler == null || !CampaignCompletionHandler.Invoke(campaignStage?.Number ?? 1))
                gameFlow?.CompleteMission();
        }

        private void HandleBossReachedPlayer(MeteorController boss, int damage)
        {
            playerHealth?.ApplyImpactDamage(damage > 0 ? damage : bossDamage, boss.ImpactPosition);
            UnsubscribeBoss();
            ActiveBoss = null;
            if (playerHealth != null && playerHealth.IsMissionFailed) { ResetClimax(); return; }
            SetStage(BossClimaxStage.RetryDelay, "대형 운석 재접근 중...");
        }

        private void SubscribeSpawner()
        {
            if (!isActiveAndEnabled || mainSpawner == null) return;
            mainSpawner.AllMeteorsResolved -= BeginClimax;
            mainSpawner.AllMeteorsResolved += BeginClimax;
        }

        private void UnsubscribeSpawner()
        {
            if (mainSpawner != null) mainSpawner.AllMeteorsResolved -= BeginClimax;
        }

        private void UnsubscribeBoss()
        {
            if (ActiveBoss == null) return;
            ActiveBoss.Damaged -= HandleBossDamaged;
            ActiveBoss.Destroyed -= HandleBossDestroyed;
            ActiveBoss.ReachedPlayerEvent -= HandleBossReachedPlayer;
        }

        private bool StageFinished(float duration) => duration <= 0f || stageElapsed >= duration;

        private void SetStage(BossClimaxStage stage, string message)
        {
            CurrentStage = stage;
            stageElapsed = 0f;
            missionHud?.ShowWarning(message, stage == BossClimaxStage.Active ? 1.2f : Mathf.Max(1f, warningDuration));
            StageChanged?.Invoke(CurrentStage, message ?? string.Empty);
        }
    }
}
