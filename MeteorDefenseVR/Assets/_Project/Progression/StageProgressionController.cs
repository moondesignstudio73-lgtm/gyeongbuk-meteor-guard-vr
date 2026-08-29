using System;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MeteorDefenseVR.Progression
{
    public enum StageTransitionPhase { None, ExplosionQuiet, StageClear, Failed, Complete }

    [DefaultExecutionOrder(-450), DisallowMultipleComponent]
    public sealed class StageProgressionController : MonoBehaviour
    {
        [SerializeField] private StageProgressionSettings settings;
        [SerializeField] private MeteorSpawner spawner;
        [SerializeField] private BossClimaxController boss;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private MissionHudController hud;
        [SerializeField] private GameSessionStats stats;

        private GameFlowManager flow;
        private DifficultyBalance difficulty;
        private float phaseElapsed;
        private int stageStartShots, stageStartHits, stageStartDamage;
        private float stageStartHealth;
        private bool initialized, campaignRunning, resumeRequested;

        public static StageProgressionController Instance { get; private set; }
        public int CurrentStage { get; private set; } = 1;
        public int TotalStages => settings != null && settings.Count > 0 ? settings.Count : 10;
        public bool IsCampaign => campaignRunning;
        public bool AllowRetryStage => settings != null && settings.allowRetryStage;
        public StageTransitionPhase Phase { get; private set; }
        public event Action<int, StageBalance> StageStarted;
        public event Action<int, int, int, int> StageCleared;
        public event Action<int> CampaignCompleted;
        public event Action<int> CampaignFailed;
        public event Action<StageTransitionPhase> PhaseChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install() { SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded; }
        private static void Loaded(Scene scene, LoadSceneMode _)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var ready = root.GetComponentInChildren<ReadyScreenController>(true);
                if (ready == null) continue;
                var controller = ready.GetComponent<StageProgressionController>();
                if (controller == null) controller = ready.gameObject.AddComponent<StageProgressionController>();
                controller.Initialize();
                return;
            }
        }

        private void Awake() { if (Instance == null || Instance == this) Instance = this; }
        private void Start() => Initialize();
        private void Update()
        {
            if (Time.timeScale <= 0 || (Phase != StageTransitionPhase.ExplosionQuiet && Phase != StageTransitionPhase.StageClear)) return;
            phaseElapsed += Time.unscaledDeltaTime;
            float quiet = settings != null ? settings.explosionQuietTime : 1f;
            float presentation = settings != null ? settings.stageClearPresentationTime : 2.6f;
            if (Phase == StageTransitionPhase.ExplosionQuiet && phaseElapsed >= quiet)
            {
                SetPhase(StageTransitionPhase.StageClear);
                ShowStageClearSummary();
            }
            else if (Phase == StageTransitionPhase.StageClear && phaseElapsed >= presentation) AdvanceStage();
        }

        private void OnDestroy() { Unbind(); if (Instance == this) Instance = null; }

        public void Initialize()
        {
            if (initialized) return;
            if (settings == null) settings = Resources.Load<StageProgressionSettings>("StageProgressionSettings");
            flow = GameFlowManager.Instance;
            spawner ??= FindAnyObjectByType<MeteorSpawner>(FindObjectsInactive.Include);
            boss ??= FindAnyObjectByType<BossClimaxController>(FindObjectsInactive.Include);
            playerHealth ??= FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            hud ??= FindAnyObjectByType<MissionHudController>(FindObjectsInactive.Include);
            stats ??= FindAnyObjectByType<GameSessionStats>(FindObjectsInactive.Include);
            if (settings == null || spawner == null || boss == null || playerHealth == null)
            { Debug.LogError("[StageProgression] Missing settings or gameplay connections; legacy single-session flow remains active."); enabled = false; return; }
            initialized = true;
            Bind();
        }

        public void Configure(StageProgressionSettings progressionSettings, MeteorSpawner meteorSpawner,
            BossClimaxController bossController, PlayerHealth health, MissionHudController missionHud, GameSessionStats sessionStats)
        {
            if (initialized) Unbind();
            settings = progressionSettings; spawner = meteorSpawner; boss = bossController;
            playerHealth = health; hud = missionHud; stats = sessionStats; initialized = false;
            Initialize();
        }

        private void Bind()
        {
            boss.CampaignCompletionHandler = HandleBossCompleted;
            boss.BossSpawned -= HandleBossSpawned; boss.BossSpawned += HandleBossSpawned;
            boss.BossDamaged -= HandleBossDamaged; boss.BossDamaged += HandleBossDamaged;
            playerHealth.MissionFailureHandler = HandleMissionFailure;
        }

        private void Unbind()
        {
            if (boss != null)
            {
                if (boss.CampaignCompletionHandler == HandleBossCompleted) boss.CampaignCompletionHandler = null;
                boss.BossSpawned -= HandleBossSpawned; boss.BossDamaged -= HandleBossDamaged;
            }
            if (playerHealth != null && playerHealth.MissionFailureHandler == HandleMissionFailure) playerHealth.MissionFailureHandler = null;
        }

        // Called by DifficultyManager before MeteorSpawner's Playing-state subscriber runs.
        public void PrepareForPlaying(DifficultyBalance selected, bool continuingFromBoss)
        {
            Initialize();
            if (!initialized) return;
            difficulty = selected;
            if (selected.Level == DifficultyLevel.Experience)
            {
                campaignRunning = false; CurrentStage = 1; SetPhase(StageTransitionPhase.None);
                spawner.ClearStage(); spawner.SetExperienceSession(settings.experienceMeteorCount, settings.experienceTargetSeconds);
                boss.ClearCampaignStage(); hud?.ClearCampaignStage();
                playerHealth.SetPolicy(GameOverPolicy.NoFailExperienceMode);
                return;
            }
            playerHealth.SetPolicy(GameOverPolicy.NormalMode);
            spawner.ClearExperienceSession();
            bool preserveStage = continuingFromBoss || resumeRequested;
            resumeRequested = false;
            if (!preserveStage || !campaignRunning)
            {
                campaignRunning = true; CurrentStage = 1;
            }
            ApplyStage(CurrentStage);
        }

        private void ApplyStage(int number)
        {
            StageBalance balance = settings.Get(number);
            spawner.SetStage(balance);
            boss.SetCampaignStage(balance);
            hud?.SetCampaignStage(difficulty.Level, number, balance.RegularMeteorCount);
            stats?.SetCampaignStage(number, true);
            CaptureStageBaseline();
            SetPhase(StageTransitionPhase.None);
            StageStarted?.Invoke(number, balance);
        }

        private bool HandleBossCompleted(int stageNumber)
        {
            if (!campaignRunning) return false;
            bool final = CurrentStage >= TotalStages;
            stats?.RecordStageCleared(final);
            int accuracyBonus = StageAccuracy() >= .9f ? 500 * CurrentStage : 0;
            int noHitBonus = StageDamage() == 0 ? 350 * CurrentStage : 0;
            hud?.AddScore(accuracyBonus + noHitBonus);
            playerHealth.RepairNormalized(settings.Get(CurrentStage).Repair);
            if (final)
            {
                SetPhase(StageTransitionPhase.Complete);
                hud?.ShowWarning("MISSION COMPLETE\nALL THREAT LEVELS CLEARED", 3.2f);
                CampaignCompleted?.Invoke(CurrentStage);
                flow?.CompleteMission();
                return true;
            }
            SetPhase(StageTransitionPhase.ExplosionQuiet);
            StageCleared?.Invoke(CurrentStage, stats?.Score ?? 0, Mathf.RoundToInt(StageAccuracy() * 100f), StageDamage());
            return true;
        }

        private void ShowStageClearSummary()
        {
            hud?.ShowWarning($"STAGE {CurrentStage:D2} CLEAR\nSCORE  {(stats?.Score ?? 0):N0}  ·  ACCURACY {Mathf.RoundToInt(StageAccuracy() * 100f)}%\nTHREAT LEVEL INCREASED", settings != null ? settings.stageClearPresentationTime : 2.6f);
        }

        private void AdvanceStage()
        {
            CurrentStage = Mathf.Min(TotalStages, CurrentStage + 1);
            boss.ResetClimax();
            ApplyStage(CurrentStage);
            flow?.StartMainGame();
        }

        private bool HandleMissionFailure()
        {
            if (!campaignRunning) return false;
            SetPhase(StageTransitionPhase.Failed);
            hud?.ShowWarning($"MISSION FAILED\n{DifficultyConfig.Code(difficulty.Level)} — STAGE {CurrentStage}", 3f);
            CampaignFailed?.Invoke(CurrentStage);
            flow?.ShowResult();
            return true;
        }

        public bool RetryStage()
        {
            if (!campaignRunning || Phase != StageTransitionPhase.Failed || !AllowRetryStage) return false;
            boss.ResetClimax();
            stats?.RestoreStageCheckpoint();
            playerHealth.RestoreCheckpoint(stageStartHealth);
            ApplyStage(CurrentStage);
            resumeRequested = true;
            flow?.StartMainGame();
            return true;
        }

        public void RestartMission() { campaignRunning = false; flow?.ResetGame(); }
        public void ReturnToBase() { campaignRunning = false; flow?.ResetGame(); }
        public void ResetProgressionState()
        {
            campaignRunning = false; resumeRequested = false; CurrentStage = 1; SetPhase(StageTransitionPhase.None);
            spawner?.ClearStage(); spawner?.ClearExperienceSession(); boss?.ClearCampaignStage(); hud?.ClearCampaignStage();
        }

        private void CaptureStageBaseline()
        {
            stageStartShots = stats?.Shots ?? 0; stageStartHits = stats?.SuccessfulHits ?? 0;
            stageStartDamage = stats?.DamageTaken ?? 0; stageStartHealth = playerHealth != null ? playerHealth.CurrentHealth : 100f;
        }
        private float StageAccuracy() { int shots = Mathf.Max(0, (stats?.Shots ?? 0) - stageStartShots); return shots > 0 ? Mathf.Clamp01((float)Mathf.Max(0, (stats?.SuccessfulHits ?? 0) - stageStartHits) / shots) : 0f; }
        private int StageDamage() => Mathf.Max(0, (stats?.DamageTaken ?? 0) - stageStartDamage);
        private void HandleBossSpawned(MeteorController meteor) => hud?.SetBossIncoming(true, meteor != null && meteor.MaxHealth > 0 ? meteor.CurrentHealth / meteor.MaxHealth : 1f);
        private void HandleBossDamaged(MeteorController meteor, float _) => hud?.SetBossIncoming(true, meteor != null && meteor.MaxHealth > 0 ? meteor.CurrentHealth / meteor.MaxHealth : 0f);
        private void SetPhase(StageTransitionPhase value) { Phase = value; phaseElapsed = 0f; PhaseChanged?.Invoke(value); }
    }
}
