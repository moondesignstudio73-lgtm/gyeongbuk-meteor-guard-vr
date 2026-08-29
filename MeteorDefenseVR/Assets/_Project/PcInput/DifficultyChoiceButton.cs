using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.EyeTracking;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MeteorDefenseVR.PcInput
{
    // Uses the same physics gaze ray for mouse, webcam and VR. Selection never starts the game.
    [DisallowMultipleComponent]
    public sealed class DifficultyChoiceButton : MonoBehaviour, IGazeTarget
    {
        private DifficultyManager manager;
        private DifficultyLevel level;
        private UIInteractionAnimator feedback;
        private UnityEngine.UI.Image border, progressBar;
        private GameObject check;
        private bool consumed, available, focused;
        private UnityEngine.UI.Button button;
        public float Progress { get; private set; }
        public DifficultyLevel Level => level;
        public void Configure(DifficultyManager owner, DifficultyLevel choice, UIInteractionAnimator animator,
            UnityEngine.UI.Image outline, UnityEngine.UI.Image progress, GameObject checkMark)
        {
            manager = owner; level = choice; feedback = animator; border = outline; progressBar = progress; check = checkMark;
            button = GetComponent<UnityEngine.UI.Button>();
            button.onClick.AddListener(Choose);
        }
        public void Refresh()
        {
            bool selected = manager.Selected == level;
            available = manager.CanSelect;
            button.interactable = available;
            feedback.SetAvailable(available); feedback.SetSelected(selected);
            border.color = selected ? new Color(.35f, 1f, .8f, 1f) : focused ? new Color(.2f, .9f, 1f, 1f) : new Color(.12f, .55f, .64f, .65f);
            check.SetActive(selected);
        }
        public void SetFocus(bool value)
        {
            if (focused != value) { focused = value; Refresh(); }
            feedback.SetGaze(value, Progress);
        }
        public void OnGazeEnter(RaycastHit _) { if (available) feedback.SetGaze(true, 0f); }
        public void OnGazeStay(RaycastHit _, float deltaTime)
        {
            if (!available || consumed || !Core.RuntimeValueGuard.IsFinite(deltaTime) || deltaTime <= 0f) return;
            Progress = Mathf.Clamp01(Progress + deltaTime / .7f);
            progressBar.rectTransform.localScale = new Vector3(Progress, 1f, 1f);
            feedback.SetGaze(true, Progress);
            if (Progress >= 1f) Choose();
        }
        public void OnGazeExit()
        {
            consumed = false; Progress = 0f;
            if (progressBar != null) progressBar.rectTransform.localScale = new Vector3(0, 1, 1);
            feedback?.SetGaze(false, 0);
        }
        public void Choose()
        {
            if (!available) return;
            consumed = true;
            if (manager.Select(level)) feedback.Confirm();
        }
        private void OnDestroy() { if (button != null) button.onClick.RemoveListener(Choose); }
        private void OnDisable() => OnGazeExit();
    }
}
