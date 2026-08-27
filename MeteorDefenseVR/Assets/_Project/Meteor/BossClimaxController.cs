using System;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.UI;
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
        private void Update() => Tick(Time.unscaledDeltaTime);

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
            if (IsRunning || IsComplete) return;
            IsRunning = true;
            IsComplete = false;
            hudBossRegistered = false;
            SetStage(BossClimaxStage.Warning, "WARNING\nLARGE METEOR DETECTED");
        }

        public void ResetClimax()
        {
            GameObject bossObject = ActiveBoss != null ? ActiveBoss.gameObject : null;
            UnsubscribeBoss();
            ActiveBoss = null;
            if (bossObject != null)
            {
                if (Application.isPlaying) Destroy(bossObject);
                else DestroyImmediate(bossObject);
            }
            IsRunning = false;
            IsComplete = false;
            hudBossRegistered = false;
            SetStage(BossClimaxStage.Idle, string.Empty);
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!IsRunning) return;
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
            Vector3 originPosition = spawnOrigin != null ? spawnOrigin.position : transform.position;
            Vector3 forward = spawnOrigin != null ? spawnOrigin.forward : transform.forward;
            Vector3 position = originPosition + forward * spawnDistance + Vector3.up * 0.35f;
            GameObject bossObject;
            if (bossPrefab != null) bossObject = Instantiate(bossPrefab, position, Quaternion.identity, transform);
            else
            {
                bossObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bossObject.name = "BossMeteor_Fallback";
                bossObject.transform.SetParent(transform);
                bossObject.transform.position = position;
            }

            ActiveBoss = bossObject.GetComponent<MeteorController>();
            if (ActiveBoss == null) ActiveBoss = bossObject.AddComponent<MeteorController>();
            ActiveBoss.Configure(MeteorType.Boss, bossHealth, bossSpeed, bossDamage, bossScore, bossSize);
            ActiveBoss.SetMovementTarget(movementTarget);
            GazeLockOnTarget lockTarget = bossObject.GetComponent<GazeLockOnTarget>();
            if (lockTarget == null) lockTarget = bossObject.AddComponent<GazeLockOnTarget>();
            lockTarget.Configure(ActiveBoss, 0.85f);
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
            missionHud?.AddScore(bossScore);
            SetStage(BossClimaxStage.Complete, string.Empty);
            StrongExplosionRequested?.Invoke(position);
            BossDestroyed?.Invoke();
            gameFlow?.CompleteMission();
        }

        private void HandleBossReachedPlayer(MeteorController boss, int damage)
        {
            playerHealth?.ApplyDamage(damage > 0 ? damage : bossDamage);
            UnsubscribeBoss();
            ActiveBoss = null;
            SetStage(BossClimaxStage.RetryDelay, "대형 운석 재접근 중...");
        }

        private void SubscribeSpawner()
        {
            if (!isActiveAndEnabled || mainSpawner == null) return;
            mainSpawner.AllSpawnsCompleted -= BeginClimax;
            mainSpawner.AllSpawnsCompleted += BeginClimax;
        }

        private void UnsubscribeSpawner()
        {
            if (mainSpawner != null) mainSpawner.AllSpawnsCompleted -= BeginClimax;
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
