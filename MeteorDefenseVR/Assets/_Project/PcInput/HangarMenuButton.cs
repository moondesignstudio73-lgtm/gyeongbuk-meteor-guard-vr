using System;
using MeteorDefenseVR.EyeTracking;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    public sealed class HangarMenuButton : MonoBehaviour, IGazeTarget
    {
        private UIInteractionAnimator feedback;
        private CalibrationProgressRing ring;
        private UnityEngine.UI.Image border;
        private UnityEngine.UI.Button button;
        private Action action;
        private bool consumed;
        public float Progress { get; private set; }
        public void Configure(UIInteractionAnimator animator, CalibrationProgressRing progress,
            UnityEngine.UI.Image outline, Action selected)
        {
            feedback = animator; ring = progress; border = outline; action = selected;
            button = GetComponent<UnityEngine.UI.Button>(); button.onClick.AddListener(Choose);
        }
        private bool Available => isActiveAndEnabled && button != null && button.interactable;
        public void SetAvailable(bool value)
        {
            button.interactable = value; feedback.SetAvailable(value);
            if (!value) OnGazeExit();
        }
        public void OnGazeEnter(RaycastHit _) { if (Available) Focus(true); }
        public void OnGazeStay(RaycastHit _, float deltaTime)
        {
            if (!Available || consumed || !Core.RuntimeValueGuard.IsFinite(deltaTime) || deltaTime <= 0) return;
            Progress = Mathf.Clamp01(Progress + deltaTime / .7f); Focus(true);
            if (Progress >= 1) Choose();
        }
        public void OnGazeExit() { consumed = false; Progress = 0; Focus(false); }
        public void Choose()
        {
            if (!Available || consumed) return;
            consumed = true; Progress = 1; Focus(true); feedback.Confirm(); action?.Invoke();
        }
        private void Focus(bool value)
        {
            feedback?.SetGaze(value, Progress);
            if (border != null) border.color = value ? HangarLobbyView.Cyan : new Color(.1f,.4f,.5f,.8f);
            if (ring != null) ring.PresentTinted(Progress, consumed ? new Color(.4f,1,.75f) : HangarLobbyView.Cyan, HangarLobbyView.Cyan);
        }
        private void OnDisable() => OnGazeExit();
        private void OnDestroy() { if (button != null) button.onClick.RemoveListener(Choose); }
    }
}
