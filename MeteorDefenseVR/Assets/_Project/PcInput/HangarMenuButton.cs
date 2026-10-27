using System;
using MeteorDefenseVR.EyeTracking;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    public sealed class HangarMenuButton : MonoBehaviour, IDirectGazeTarget
    {
        private UIInteractionAnimator feedback;
        private CalibrationProgressRing ring;
        private UnityEngine.UI.Image border;
        private UnityEngine.UI.Button button;
        private Action action;
        private bool consumed;
        private Color idleOutline = new Color(.1f,.4f,.5f,.8f);
        private Color focusedOutline = HangarLobbyView.Cyan;
        private readonly GazeMenuSelectionGate selectionGate = new GazeMenuSelectionGate();
        private void OnEnable() => selectionGate.OpenScreen();
        public float Progress { get; private set; }
        public bool AllowImmediateClick { get; set; }
        public void SetOutlineColors(Color idle, Color focused)
        {
            idleOutline = idle; focusedOutline = focused; Focus(false);
        }
        public void Configure(UIInteractionAnimator animator, CalibrationProgressRing progress,
            UnityEngine.UI.Image outline, Action selected)
        {
            feedback = animator; ring = progress; border = outline; action = selected;
            button = GetComponent<UnityEngine.UI.Button>(); button.onClick.AddListener(Click);
        }
        private bool Available => isActiveAndEnabled && button != null && button.interactable;
        public void SetAvailable(bool value)
        {
            button.interactable = value; feedback.SetAvailable(value);
            if (!value) OnGazeExit();
        }
        public void OnGazeEnter(RaycastHit _) { selectionGate.Enter(transform.position); if (Available && selectionGate.IsOpen) Focus(true); }
        public void OnGazeStay(RaycastHit _, float deltaTime)
        {
            if (!Available || !selectionGate.IsOpen || consumed || !Core.RuntimeValueGuard.IsFinite(deltaTime) || deltaTime <= 0) return;
            Progress = Mathf.Clamp01(Progress + deltaTime / .7f); Focus(true);
            if (Progress >= 1) Choose();
        }
        public void OnGazeExit() { selectionGate.Exit(); consumed = false; Progress = 0; Focus(false); }
        public void Click() => Commit(AllowImmediateClick);
        public void Choose() => Commit(false);
        private void Commit(bool immediate)
        {
            if (!Available || (!immediate && !selectionGate.IsOpen) || consumed) return;
            selectionGate.Confirm(transform.position);
            consumed = true; Progress = 1; Focus(true); feedback.Confirm(); action?.Invoke();
        }
        private void Focus(bool value)
        {
            feedback?.SetGaze(value, Progress);
            if (border != null) border.color = value ? focusedOutline : idleOutline;
            if (ring != null) ring.PresentTinted(Progress, consumed ? new Color(.4f,1,.75f) : HangarLobbyView.Cyan, HangarLobbyView.Cyan);
        }
        private void OnDisable() => OnGazeExit();
        private void OnDestroy() { if (button != null) button.onClick.RemoveListener(Click); }
    }
}
