using UnityEngine;

namespace MeteorDefenseVR.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class ReadyScreenView : MonoBehaviour
    {
        [SerializeField] private ReadyScreenController controller;
        [SerializeField] private TextMesh titleText;
        [SerializeField] private GameObject startButton;

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        public void Configure(ReadyScreenController readyController, TextMesh text, GameObject button)
        {
            Unsubscribe();
            controller = readyController;
            titleText = text;
            startButton = button;
            Subscribe();
            SetVisible(controller == null || controller.IsReadyVisible);
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled || controller == null) return;
            controller.VisibilityChanged -= SetVisible;
            controller.VisibilityChanged += SetVisible;
        }

        private void Unsubscribe()
        {
            if (controller != null) controller.VisibilityChanged -= SetVisible;
        }

        private void SetVisible(bool visible)
        {
            if (titleText != null)
            {
                titleText.gameObject.SetActive(visible);
                titleText.text = visible ? "METEOR DEFENSE\n\nSTART" : string.Empty;
            }
            if (startButton != null) startButton.SetActive(visible);
        }
    }
}
