using UnityEngine;

namespace MeteorDefenseVR.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class TutorialStatusView : MonoBehaviour
    {
        [SerializeField] private TutorialController controller;
        [SerializeField] private TextMesh instructionText;

        private void OnEnable()
        {
            if (controller == null) controller = GetComponentInParent<TutorialController>();
            if (instructionText == null) instructionText = GetComponent<TextMesh>();
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
        }
    }
}
