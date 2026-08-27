using UnityEngine;

namespace MeteorDefenseVR.UI
{
    [DisallowMultipleComponent]
    public sealed class MissionHudView : MonoBehaviour
    {
        [SerializeField] private MissionHudController controller;
        [SerializeField] private TextMesh healthText;
        [SerializeField] private TextMesh remainingText;
        [SerializeField] private TextMesh scoreText;
        [SerializeField] private TextMesh feedbackText;

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        public void Configure(
            MissionHudController hudController,
            TextMesh health,
            TextMesh remaining,
            TextMesh score,
            TextMesh feedback)
        {
            Unsubscribe();
            controller = hudController;
            healthText = health;
            remainingText = remaining;
            scoreText = score;
            feedbackText = feedback;
            Subscribe();
            RefreshAll();
        }

        public void RefreshAll()
        {
            if (controller == null) return;
            SetHealth(controller.CurrentHealth, controller.MaxHealth);
            SetMeteorProgress(controller.RemainingMeteors, controller.TotalMeteors);
            SetScore(controller.Score);
            SetFeedback(controller.FeedbackMessage);
        }

        public static string FormatHealth(float current, float maximum)
        {
            float normalized = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
            int filled = Mathf.RoundToInt(normalized * 8f);
            return "HP\n" + new string('█', filled) + new string('□', 8 - filled);
        }

        public static string FormatMeteorProgress(int remaining, int total) =>
            $"남은 운석\n{Mathf.Max(0, remaining)} / {Mathf.Max(0, total)}";

        public static string FormatScore(int score) => $"SCORE\n{Mathf.Max(0, score):N0}";

        private void Subscribe()
        {
            if (!isActiveAndEnabled || controller == null) return;
            controller.HealthChanged -= SetHealth;
            controller.MeteorProgressChanged -= SetMeteorProgress;
            controller.ScoreChanged -= SetScore;
            controller.FeedbackChanged -= SetFeedback;
            controller.HealthChanged += SetHealth;
            controller.MeteorProgressChanged += SetMeteorProgress;
            controller.ScoreChanged += SetScore;
            controller.FeedbackChanged += SetFeedback;
        }

        private void Unsubscribe()
        {
            if (controller == null) return;
            controller.HealthChanged -= SetHealth;
            controller.MeteorProgressChanged -= SetMeteorProgress;
            controller.ScoreChanged -= SetScore;
            controller.FeedbackChanged -= SetFeedback;
        }

        private void SetHealth(float current, float maximum)
        {
            if (healthText != null)
            {
                float normalized = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
                healthText.text = $"HP  {Mathf.RoundToInt(normalized * 100f):D3}%";
            }
        }

        private void SetMeteorProgress(int remaining, int total)
        {
            if (remainingText != null) remainingText.text = $"METEORS  {Mathf.Max(0, remaining):D2} / {Mathf.Max(0, total):D2}";
        }

        private void SetScore(int score)
        {
            if (scoreText != null) scoreText.text = $"SCORE  {Mathf.Max(0, score):N0}";
        }

        private void SetFeedback(string message)
        {
            if (feedbackText != null) feedbackText.text = message ?? string.Empty;
        }
    }
}
