using System.Collections.Generic;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Launch;
using MeteorDefenseVR.UI;
using TMPro;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    // Read-only presentation bridge. MissionHudController remains the single source of gameplay data.
    [DefaultExecutionOrder(990), DisallowMultipleComponent]
    public sealed class GameplayHudOverlayView : MonoBehaviour
    {
        [SerializeField] private MissionHudController controller;
        [SerializeField] private GameObject legacyHudRoot;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private GameObject telemetryRoot;
        [SerializeField] private GameObject feedbackPanel;
        [SerializeField] private TextMeshProUGUI healthValue;
        [SerializeField] private TextMeshProUGUI difficultyStageValue;
        [SerializeField] private TextMeshProUGUI asteroidLabel;
        [SerializeField] private TextMeshProUGUI asteroidValue;
        [SerializeField] private TextMeshProUGUI scoreValue;
        [SerializeField] private TextMeshProUGUI feedbackValue;
        [SerializeField] private LaunchSequenceController launch;
        [SerializeField] private MissionCompleteController missionComplete;
        [SerializeField] private Renderer[] legacyRenderers;
        [SerializeField, Range(1f, 2.5f)] private float vrDepth = 1.6f;
        [SerializeField, Range(.0005f, .0015f)] private float vrScale = .00085f;

        private static readonly Color Cyan = new Color32(47, 231, 246, 255);
        private static readonly Color Green = new Color32(114, 255, 149, 255);
        private static readonly Color Warning = new Color32(255, 123, 73, 255);
        private bool? worldMode;
        private string presentedMessage = string.Empty;
        private DifficultyManager difficultyManager;
        private DifficultyLevel? presentedDifficulty;
        private int presentedStage = -1;

        public Canvas HudCanvas => canvas;
        public GameObject TelemetryRoot => telemetryRoot;
        public GameObject FeedbackPanel => feedbackPanel;
        public TextMeshProUGUI HealthValue => healthValue;
        public TextMeshProUGUI DifficultyStageValue => difficultyStageValue;
        public TextMeshProUGUI AsteroidLabel => asteroidLabel;
        public TextMeshProUGUI AsteroidValue => asteroidValue;
        public TextMeshProUGUI ScoreValue => scoreValue;
        public TextMeshProUGUI FeedbackValue => feedbackValue;
        public float VrDepth => vrDepth;
        public IReadOnlyList<Renderer> LegacyRenderers => legacyRenderers;

        public void Configure(
            MissionHudController missionHud,
            GameObject sourceRoot,
            Camera camera,
            Canvas targetCanvas,
            CanvasGroup group,
            GameObject statsRoot,
            GameObject messagePanel,
            TextMeshProUGUI health,
            TextMeshProUGUI difficultyStage,
            TextMeshProUGUI asteroidsLabel,
            TextMeshProUGUI asteroids,
            TextMeshProUGUI score,
            TextMeshProUGUI feedback,
            LaunchSequenceController launchController,
            MissionCompleteController completeController,
            Renderer[] renderersToSuppress)
        {
            Unbind();
            controller = missionHud;
            legacyHudRoot = sourceRoot;
            playerCamera = camera;
            canvas = targetCanvas;
            canvasGroup = group;
            telemetryRoot = statsRoot;
            feedbackPanel = messagePanel;
            ProtectTelemetryContrast();
            healthValue = health;
            difficultyStageValue = difficultyStage;
            presentedDifficulty = null;
            asteroidLabel = asteroidsLabel;
            asteroidValue = asteroids;
            scoreValue = score;
            feedbackValue = feedback;
            launch = launchController;
            missionComplete = completeController;
            legacyRenderers = renderersToSuppress;
            worldMode = null;
            ApplyCanvasMode(playerCamera != null && playerCamera.stereoEnabled);
            Bind();
            RefreshAll();
            HideLegacyRenderers();
        }

        private void OnEnable()
        {
            ProtectTelemetryContrast();
            if (Application.isPlaying && GetComponent<ReadableTelemetryLayout>() == null)
                gameObject.AddComponent<ReadableTelemetryLayout>();
            if (Application.isPlaying && GetComponent<PracticeHudSafeOverlay>() == null)
                gameObject.AddComponent<PracticeHudSafeOverlay>();
            if (controller == null) controller = FindAnyObjectByType<MissionHudController>(FindObjectsInactive.Include);
            if (playerCamera == null) playerCamera = Camera.main;
            if (launch == null) launch = FindAnyObjectByType<LaunchSequenceController>(FindObjectsInactive.Include);
            if (missionComplete == null) missionComplete = FindAnyObjectByType<MissionCompleteController>(FindObjectsInactive.Include);
            Bind();
            RefreshAll();
            HideLegacyRenderers();
        }

        private void OnDisable() => Unbind();

        private void ProtectTelemetryContrast()
        {
            // Transient reward/status text needs the same world-effect isolation
            // while its existing controller owns visibility and lifetime.
            if (feedbackPanel != null)
                feedbackPanel.GetComponent<ChamferedHudPanel>()?.SetFillOpacity(1f);
            if (telemetryRoot == null) return;
            // World pickup effects must not show through mandatory mission information.
            foreach (var panel in telemetryRoot.GetComponentsInChildren<ChamferedHudPanel>(true))
                panel.SetFillOpacity(1f);
        }

        private void LateUpdate()
        {
            RefreshPresentation();
        }

        public void RefreshPresentation()
        {
            HideLegacyRenderers();
            PresentDifficultyStage();
            if (playerCamera != null) ApplyCanvasMode(playerCamera.stereoEnabled);
            bool telemetryVisible = legacyHudRoot != null && legacyHudRoot.activeInHierarchy;
            if (telemetryRoot != null && telemetryRoot.activeSelf != telemetryVisible) telemetryRoot.SetActive(telemetryVisible);

            string message = ResolveSystemMessage(telemetryVisible);
            PresentFeedback(message);
            bool visible = telemetryVisible || !string.IsNullOrEmpty(message);
            if (canvas != null) canvas.enabled = visible;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        public void ApplyCanvasMode(bool stereo)
        {
            if (canvas == null || playerCamera == null || worldMode == stereo) return;
            worldMode = stereo;
            RectTransform root = (RectTransform)canvas.transform;
            canvas.worldCamera = playerCamera;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 220;
            if (stereo)
            {
                root.SetParent(playerCamera.transform, false);
                canvas.renderMode = RenderMode.WorldSpace;
                root.anchorMin = root.anchorMax = Vector2.zero;
                root.pivot = Vector2.one * .5f;
                root.sizeDelta = new Vector2(1920f, 1080f);
                root.localPosition = Vector3.forward * vrDepth;
                root.localRotation = Quaternion.identity;
                root.localScale = Vector3.one * vrScale;
            }
            else
            {
                root.SetParent(null, false);
                root.anchorMin = Vector2.zero;
                root.anchorMax = Vector2.one;
                root.offsetMin = root.offsetMax = Vector2.zero;
                root.localPosition = Vector3.zero;
                root.localRotation = Quaternion.identity;
                root.localScale = Vector3.one;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.planeDistance = .45f;
            }
            Canvas.ForceUpdateCanvases();
        }

        public void RefreshAll()
        {
            if (controller == null) return;
            PresentHealth(controller.CurrentHealth, controller.MaxHealth);
            PresentMeteorProgress(controller.RemainingMeteors, controller.TotalMeteors);
            PresentScore(controller.Score);
            PresentFeedback(controller.FeedbackMessage);
        }

        public static string FormatHealthValue(float current, float maximum)
        {
            float normalized = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
            int filled = Mathf.RoundToInt(normalized * 8f);
            return new string('■', filled) + new string('□', 8 - filled);
        }

        public static string FormatDifficultyStage(DifficultyLevel difficulty, int stage) =>
            $"{DifficultyConfig.Label(difficulty)}  ·  구역 {Mathf.Max(0, stage):D2}";

        public static string FormatAsteroidValue(int remaining, int total) =>
            $"{Mathf.Max(0, remaining)} / {Mathf.Max(0, total)}";

        public static string FormatScoreValue(int score) => Mathf.Max(0, score).ToString("N0");

        private void Bind()
        {
            Unbind();
            if (controller != null)
            {
                controller.HealthChanged += PresentHealth;
                controller.MeteorProgressChanged += PresentMeteorProgress;
                controller.ScoreChanged += PresentScore;
                controller.FeedbackChanged += HandleFeedbackChanged;
            }
            if (launch != null) launch.StageChanged += HandleLaunchStageChanged;
            if (missionComplete != null) missionComplete.StatusChanged += HandleMissionCompleteChanged;
        }

        private void Unbind()
        {
            if (controller != null)
            {
                controller.HealthChanged -= PresentHealth;
                controller.MeteorProgressChanged -= PresentMeteorProgress;
                controller.ScoreChanged -= PresentScore;
                controller.FeedbackChanged -= HandleFeedbackChanged;
            }
            if (launch != null) launch.StageChanged -= HandleLaunchStageChanged;
            if (missionComplete != null) missionComplete.StatusChanged -= HandleMissionCompleteChanged;
        }

        private void PresentHealth(float current, float maximum) => SetText(healthValue, FormatHealthValue(current, maximum));

        private void PresentMeteorProgress(int remaining, int total)
        {
            PresentDifficultyStage();
            if (controller != null && controller.CampaignStage > 0)
            {
                SetText(asteroidValue, controller.BossIncoming
                    ? $"보스 운석  {Mathf.RoundToInt(controller.BossHealthNormalized * 100f):D3}%"
                    : $"남은 운석  {FormatAsteroidValue(remaining, total)}");
            }
            else
            {
                SetText(asteroidValue, $"남은 운석  {FormatAsteroidValue(remaining, total)}");
            }
        }

        private void PresentDifficultyStage()
        {
            if (difficultyStageValue == null) return;
            if (difficultyManager == null)
                difficultyManager = FindAnyObjectByType<DifficultyManager>(FindObjectsInactive.Include);
            bool campaign = controller != null && controller.CampaignStage > 0;
            DifficultyLevel level = campaign ? controller.CampaignDifficulty
                : difficultyManager != null ? difficultyManager.Session.Level : DifficultyLevel.Normal;
            int stage = campaign ? controller.CampaignStage : 1;
            // Read the frozen session in combat, and the refreshed session after reset.
            // Polling the value also handles selection/reset without a meteor-count event.
            if (presentedDifficulty == level && presentedStage == stage) return;
            presentedDifficulty = level;
            presentedStage = stage;
            SetText(difficultyStageValue, FormatDifficultyStage(level, stage));
        }

        private void PresentScore(int score) => SetText(scoreValue, FormatScoreValue(score));
        private void HandleFeedbackChanged(string _) => PresentFeedback(ResolveSystemMessage(legacyHudRoot != null && legacyHudRoot.activeInHierarchy));
        private void HandleLaunchStageChanged(LaunchStage _, string __) => PresentFeedback(ResolveSystemMessage(false));
        private void HandleMissionCompleteChanged(string _) => PresentFeedback(ResolveSystemMessage(false));

        private string ResolveSystemMessage(bool telemetryVisible)
        {
            GameFlowManager flow = GameFlowManager.Instance;
            // A direct result transition can precede the completion controller's cleanup.
            // The result panel owns this screen; stale status must not cover its score.
            if (flow != null && flow.CurrentState == GameState.Result) return string.Empty;
            if (missionComplete != null && missionComplete.IsRunning && missionComplete.PresentationProgress > 0f && !string.IsNullOrWhiteSpace(missionComplete.StatusMessage))
                return missionComplete.StatusMessage;
            if (launch != null && launch.IsRunning && IsLaunchMessageStage(launch.CurrentStage))
                return launch.StatusMessage;
            if (flow != null)
            {
                if (flow.CurrentState == GameState.Intro)
                    return "WARNING\n운석이 지구로 접근합니다";
                if (flow.CurrentState == GameState.MissionBriefing)
                    return "MISSION\n운석을 파괴하고 지구를 지키세요";
            }
            return telemetryVisible && controller != null ? controller.FeedbackMessage : string.Empty;
        }

        private static bool IsLaunchMessageStage(LaunchStage stage) =>
            stage == LaunchStage.Countdown3 || stage == LaunchStage.Countdown2 || stage == LaunchStage.Countdown1 || stage == LaunchStage.MissionStart;

        private float standardFeedbackFontSize = -1;
        private Vector2 standardFeedbackPanelSize, standardFeedbackTextSize;
        private float standardFeedbackLineSpacing;
        private void PresentFeedback(string message)
        {
            message ??= string.Empty;
            if (feedbackPanel != null && feedbackPanel.activeSelf != !string.IsNullOrWhiteSpace(message))
                feedbackPanel.SetActive(!string.IsNullOrWhiteSpace(message));
            if (presentedMessage == message) return;
            presentedMessage = message;
            if (feedbackValue == null) return;
            if (standardFeedbackFontSize < 0)
            {
                standardFeedbackFontSize = feedbackValue.fontSize;
                standardFeedbackPanelSize = ((RectTransform)feedbackPanel.transform).sizeDelta;
                standardFeedbackTextSize = feedbackValue.rectTransform.sizeDelta;
                standardFeedbackLineSpacing = feedbackValue.lineSpacing;
            }
            bool abilityNotice = false;
            for (int i = 0; i < 8; i++)
                abilityNotice |= message.StartsWith(MeteorDefenseVR.PowerUps.PowerUpHudView.PlayerName((MeteorDefenseVR.PowerUps.PowerUpType)i), System.StringComparison.Ordinal);
            // Completion owns a quiet screen. Give its two Korean lines a dedicated readable size
            // without changing tutorial, warning, stage or pickup message layouts.
            bool completionNotice = message == "MISSION COMPLETE\n지구 방어 성공!";
            feedbackValue.fontSize = completionNotice ? 60f : abilityNotice ? Mathf.Max(40, standardFeedbackFontSize) : standardFeedbackFontSize;
            feedbackValue.lineSpacing = completionNotice ? 8f : standardFeedbackLineSpacing;
            ((RectTransform)feedbackPanel.transform).sizeDelta = completionNotice ? new Vector2(720f, 232f) : standardFeedbackPanelSize;
            feedbackValue.rectTransform.sizeDelta = completionNotice ? new Vector2(672f, 200f) : standardFeedbackTextSize;
            feedbackValue.color = message.Contains("WARNING") || message.Contains("FAILED") || message.Contains("BOSS")
                ? Warning : message.Contains("COMPLETE") || message.Contains("CLEAR") || message.Contains("지키세요") ? Green : Cyan;
            SetText(feedbackValue, WorldTextPresentation.PlayerMessage(message));
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target == null || target.text == value) return;
            target.text = value ?? string.Empty;
            target.SetVerticesDirty();
            target.ForceMeshUpdate(true, true);
        }

        private void HideLegacyRenderers()
        {
            if (legacyRenderers == null) return;
            foreach (Renderer renderer in legacyRenderers)
            {
                if (renderer == null) continue;
                renderer.enabled = false;
                renderer.forceRenderingOff = true;
                // State transitions and legacy presentation bridges can restore Renderer flags.
                // Keep the data-bearing TextMesh/component references alive, but make the
                // renderer GameObject itself impossible for any player/spectator camera to draw.
                if (Application.isPlaying && renderer.gameObject.activeSelf) renderer.gameObject.SetActive(false);
            }
        }

#if UNITY_EDITOR
        public void PresentLayoutProbe(DifficultyLevel difficulty, int stage, int remaining, int total, int score)
        {
            SetText(difficultyStageValue, FormatDifficultyStage(difficulty, stage));
            SetText(asteroidValue, $"남은 운석  {FormatAsteroidValue(remaining, total)}");
            SetText(scoreValue, FormatScoreValue(score));
            SetText(healthValue, FormatHealthValue(100f, 100f));
            Canvas.ForceUpdateCanvases();
        }

        public void PresentRawLayoutProbe(string difficultyCode, int stage, int remaining, int total, int score)
        {
            SetText(difficultyStageValue, $"{difficultyCode}  ·  STAGE {Mathf.Max(0, stage):D2}");
            SetText(asteroidValue, $"남은 운석  {FormatAsteroidValue(remaining, total)}");
            SetText(scoreValue, FormatScoreValue(score));
            SetText(healthValue, FormatHealthValue(100f, 100f));
            Canvas.ForceUpdateCanvases();
        }
#endif
    }
}
