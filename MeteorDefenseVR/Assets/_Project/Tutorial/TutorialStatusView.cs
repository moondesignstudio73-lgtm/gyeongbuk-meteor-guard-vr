using UnityEngine;

namespace MeteorDefenseVR.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class TutorialStatusView : MonoBehaviour
    {
        [SerializeField] private TutorialController controller;
        [SerializeField] private TextMesh instructionText;
        private Renderer[] worldRenderers;

        private void OnEnable()
        {
            if (controller == null) controller = GetComponentInParent<TutorialController>();
            if (instructionText == null) instructionText = GetComponent<TextMesh>();
            SuppressWorldPresentation();
            if (controller == null) return;
            controller.InstructionChanged += SetInstruction;
            SetInstruction(controller.Instruction);
        }

        private void OnDisable()
        {
            if (controller != null) controller.InstructionChanged -= SetInstruction;
        }

        public void Configure(TutorialController tutorialController, TextMesh text)
        {
            controller = tutorialController;
            instructionText = text;
        }

        public void SetInstruction(string instruction)
        {
            if (instructionText != null) instructionText.text = instruction ?? string.Empty;
            SuppressWorldPresentation();
        }

        public void SuppressWorldPresentation()
        {
            worldRenderers ??= GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in worldRenderers)
            {
                if (renderer == null) continue;
                renderer.enabled = false;
                renderer.forceRenderingOff = true;
            }
        }
    }
}
