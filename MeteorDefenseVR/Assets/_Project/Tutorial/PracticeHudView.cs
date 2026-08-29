using UnityEngine;

namespace MeteorDefenseVR.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class PracticeHudView : MonoBehaviour
    {
        [SerializeField] private PracticeController controller;
        [SerializeField] private TextMesh hudText;
        [SerializeField] private TextMesh statusText;

        private void LateUpdate()
        {
            bool visible = controller != null && (controller.IsRunning || controller.IsComplete);
            if (hudText != null && hudText.TryGetComponent<Renderer>(out var hudRenderer)) hudRenderer.enabled = visible;
            if (statusText != null && statusText.TryGetComponent<Renderer>(out var statusRenderer)) statusRenderer.enabled = visible;
        }

        private void OnEnable()
        {
            if (controller == null) controller = GetComponentInParent<PracticeController>();
            if (controller == null) return;
            controller.ProgressChanged += SetProgress;
            controller.StatusChanged += SetStatus;
            SetProgress(controller.RemainingMeteors, controller.TotalMeteors, controller.PracticeScore);
            SetStatus(controller.StatusMessage);
        }

        private void OnDisable()
        {
            if (controller == null) return;
            controller.ProgressChanged -= SetProgress;
            controller.StatusChanged -= SetStatus;
        }

        public void Configure(PracticeController practiceController, TextMesh hud, TextMesh status)
        {
            controller = practiceController;
            hudText = hud;
            statusText = status;
        }

        public void SetProgress(int remaining, int total, int score)
        {
            if (hudText != null)
                hudText.text = $"HP  ████████     남은 운석  {remaining}/{total}     SCORE  {score:N0}";
        }

        public void SetStatus(string status)
        {
            if (statusText != null) statusText.text = status ?? string.Empty;
        }
    }
}
