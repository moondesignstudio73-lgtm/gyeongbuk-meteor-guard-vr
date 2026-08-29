using MeteorDefenseVR.Audio;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace MeteorDefenseVR.PcInput
{
    // Feedback only: never invokes Button.onClick or advances GameFlow.
    [DisallowMultipleComponent]
    public sealed class UIInteractionAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        public enum InteractionState { Normal, Hover, Pressed, Selected, Disabled }
        [SerializeField, Range(1f, 1.12f)] private float hoverScale = 1.04f;
        [SerializeField, Range(.85f, 1f)] private float pressedScale = .96f;
        [SerializeField, Range(.05f, .4f)] private float transitionDuration = .12f;
        [SerializeField, Range(0f, 2f)] private float glowIntensity = .6f;
        [Header("Optional existing AudioSystem cues (no generated clips)")]
        [SerializeField] private bool playHoverCue, playConfirmCue;
        [SerializeField] private AudioCue hoverCue = AudioCue.TargetFocus, confirmCue = AudioCue.LockOn;
        [SerializeField] private UnityEvent hoverSfx = new UnityEvent(), confirmSfx = new UnityEvent(), cancelSfx = new UnityEvent();
        private UnityEngine.UI.Selectable selectable;
        private UnityEngine.UI.Graphic graphic;
        private AudioManager audioManager;
        private Transform visual;
        private Vector3 baseScale;
        private Color baseColor;
        private Renderer worldRenderer;
        private MaterialPropertyBlock worldProperties;
        private Color worldBase;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private bool hovering, selected, pressed, gaze, available = true, persistentSelection;
        private float scale = 1f, pulse;
        private bool automaticPointerConfirm = true;
        public InteractionState State { get; private set; }
        public float GazeProgress { get; private set; }

        private void Awake()
        {
            selectable = GetComponent<UnityEngine.UI.Selectable>();
            graphic = selectable != null ? selectable.targetGraphic : null;
            // Animate the label, not the raycast bounds / collider.
            visual = transform.Find("Label");
            if (visual == null) visual = transform;
            baseScale = visual.localScale;
            baseColor = graphic != null ? graphic.color : new Color(.025f, .17f, .23f, 1f);
            if (graphic != null) { graphic.color = Color.white; graphic.canvasRenderer.SetColor(baseColor); }
            if (selectable != null) selectable.transition = UnityEngine.UI.Selectable.Transition.None;
            audioManager = FindAnyObjectByType<AudioManager>();
        }
        public void SetVisual(Transform target)
        {
            visual = target; baseScale = target.localScale;
            worldRenderer = target.GetComponent<Renderer>();
            if (worldRenderer != null && worldRenderer.sharedMaterial != null && worldRenderer.sharedMaterial.HasProperty(BaseColor))
            { worldProperties = new MaterialPropertyBlock(); worldBase = worldRenderer.sharedMaterial.GetColor(BaseColor); }
        }
        public void SetAvailable(bool value) { available = value; Refresh(); }
        public void SetSelected(bool value) { persistentSelection = value; Refresh(); }
        public void EnableConfirmCue(AudioCue cue) { confirmCue = cue; playConfirmCue = true; }
        public void EnableHoverCue(AudioCue cue) { hoverCue = cue; playHoverCue = true; }
        public void SetAutomaticPointerConfirm(bool value) => automaticPointerConfirm = value;
        public void SetGaze(bool focused, float progress)
        {
            bool entered = focused && !gaze;
            gaze = focused; GazeProgress = focused ? Mathf.Clamp01(progress) : 0f;
            if (entered && available) HoverSound();
            Refresh();
        }
        public void Confirm()
        {
            if (!CanInteract) return;
            pulse = .24f;
            confirmSfx.Invoke();
            if (playConfirmCue && audioManager != null) audioManager.TryPlay(confirmCue);
        }
        private bool CanInteract => available && (selectable == null || selectable.IsInteractable());
        private void Update()
        {
            Refresh();
            pulse = Mathf.Max(0f, pulse - Time.unscaledDeltaTime);
            float target = State == InteractionState.Pressed ? pressedScale :
                State == InteractionState.Hover || State == InteractionState.Selected ? hoverScale : 1f;
            if (pulse > 0f) target = 1f + .055f * Mathf.Sin((1f - pulse / .24f) * Mathf.PI);
            scale = Mathf.MoveTowards(scale, target, Time.unscaledDeltaTime * .12f / Mathf.Max(.05f, transitionDuration));
            if (visual != null && Mathf.Abs(visual.localScale.x - baseScale.x * scale) > .0001f) visual.localScale = baseScale * scale;
        }
        private void Refresh()
        {
            var next = !CanInteract ? InteractionState.Disabled : pressed ? InteractionState.Pressed :
                hovering || gaze ? InteractionState.Hover : selected || persistentSelection ? InteractionState.Selected : InteractionState.Normal;
            if (next == State) return;
            State = next;
            if (worldProperties != null && worldRenderer != null)
            {
                float intensity = next == InteractionState.Hover || next == InteractionState.Selected ? .18f * glowIntensity : next == InteractionState.Pressed ? .3f : 0f;
                Color tint = Color.Lerp(worldBase, new Color(.14f, .9f, .95f), intensity); tint.a = worldBase.a;
                worldRenderer.GetPropertyBlock(worldProperties); worldProperties.SetColor(BaseColor, tint); worldRenderer.SetPropertyBlock(worldProperties);
            }
            if (graphic == null) return;
            Color color = next == InteractionState.Disabled ? baseColor * .45f :
                next == InteractionState.Pressed ? new Color(.08f, .72f, .82f, 1f) :
                next == InteractionState.Hover || next == InteractionState.Selected ? Color.Lerp(baseColor, new Color(.06f, .52f, .63f, 1f), glowIntensity) : baseColor;
            color.a = baseColor.a;
            // CanvasRenderer tint, no text mesh/layout regeneration.
            graphic.CrossFadeColor(color, transitionDuration, true, false);
        }
        private void HoverSound() { hoverSfx.Invoke(); if (playHoverCue && audioManager != null) audioManager.TryPlay(hoverCue); }
        public void OnPointerEnter(PointerEventData _) { hovering = true; if (CanInteract) HoverSound(); Refresh(); }
        public void OnPointerExit(PointerEventData _) { hovering = pressed = false; Refresh(); }
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left && CanInteract) pressed = true; Refresh(); }
        public void OnPointerUp(PointerEventData _) { pressed = false; Refresh(); }
        public void OnPointerClick(PointerEventData e) { if (automaticPointerConfirm && e.button == PointerEventData.InputButton.Left) Confirm(); }
        public void OnSelect(BaseEventData _) { selected = true; Refresh(); }
        public void OnDeselect(BaseEventData _) { selected = false; Refresh(); }
        public void OnSubmit(BaseEventData _) { if (automaticPointerConfirm) Confirm(); }
        public void CancelFeedback() { pressed = selected = hovering = gaze = false; cancelSfx.Invoke(); Refresh(); }
        private void OnDisable()
        {
            hovering = selected = pressed = gaze = false; pulse = GazeProgress = 0f; scale = 1f; State = InteractionState.Normal;
            if (visual != null) visual.localScale = baseScale;
            if (graphic != null) graphic.canvasRenderer.SetColor(baseColor);
            if (worldRenderer != null) worldRenderer.SetPropertyBlock(null);
        }
    }
}
