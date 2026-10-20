using UnityEngine;

namespace MeteorDefenseVR.Combat
{
    [DisallowMultipleComponent]
    public sealed class LockOnReticleView : MonoBehaviour
    {
        [SerializeField] private GazeLockOnTarget target;
        [SerializeField] private LineRenderer progressRing;
        [SerializeField] private TextMesh statusText;
        [SerializeField] private Renderer[] tacticalBrackets;
        [SerializeField, Min(8)] private int ringSegments = 48;
        [SerializeField, Min(0.05f)] private float ringRadius = 0.75f;
        [SerializeField] private Color focusingColor = new Color(0.1f, 0.95f, 1f);
        [SerializeField] private Color lockedColor = new Color(0.2f, 1f, 0.35f);
        private LockOnState? previousLabelState;
        private float focusAge;
        private bool previousAimReady;
        private GazeLockOnTarget subscribedTarget;
        private float arrivalAge = 10f;
        private Vector3[] bracketPositions;

        private void OnEnable()
        {
            GazeLockOnTarget.GlobalReleased -= HandleGlobalReleased;
            GazeLockOnTarget.GlobalReleased += HandleGlobalReleased;
            if (target == null) target = GetComponentInParent<GazeLockOnTarget>();
            if (target == null)
            {
                ClearPresentation();
                return;
            }
            SubscribeTarget();
            Refresh(target.Progress, target.State);
        }

        private void OnDisable()
        {
            GazeLockOnTarget.GlobalReleased -= HandleGlobalReleased;
            UnsubscribeTarget();
            ClearPresentation();
            transform.localScale = Vector3.one;
            previousLabelState = null;
        }

        private void Update()
        {
            if (target == null) return;
            arrivalAge += Time.deltaTime;
            bool focusing = target.State == LockOnState.Focusing || target.State == LockOnState.Locked;
            if (tacticalBrackets != null)
            {
                if (bracketPositions == null || bracketPositions.Length != tacticalBrackets.Length)
                {
                    bracketPositions = new Vector3[tacticalBrackets.Length];
                    for (int i = 0; i < tacticalBrackets.Length; i++) if (tacticalBrackets[i] != null) bracketPositions[i] = tacticalBrackets[i].transform.localPosition;
                }
                float scale = Mathf.Lerp(1.3f, 1f, Mathf.SmoothStep(0, 1, focusAge / .12f));
                for (int i = 0; i < tacticalBrackets.Length; i++)
                {
                    var bracket = tacticalBrackets[i]; if (bracket == null) continue;
                    bracket.enabled = target.IsAvailable && (focusing || arrivalAge < .35f);
                    bracket.transform.localPosition = bracketPositions[i] * (focusing ? scale : 1.12f);
                    if (!focusing && bracket is LineRenderer line)
                    {
                        Color tint = focusingColor; tint.a = .45f * Mathf.Clamp01(1 - arrivalAge / .35f);
                        line.startColor = line.endColor = tint;
                    }
                }
            }
            if (progressRing == null || !progressRing.enabled) return;
            bool aimReady=PresentationLocked(target.State);
            if(aimReady!=previousAimReady){previousAimReady=aimReady;Refresh(target.Progress,target.State);}
            focusAge += Time.unscaledDeltaTime;
            float direction = aimReady ? -1f : 1f;
            progressRing.transform.Rotate(0f, 0f, direction * 55f * Time.unscaledDeltaTime, Space.Self);
            float pulse = aimReady
                ? 1f + Mathf.Sin(Time.unscaledTime * 10f) * 0.055f
                : 1f + target.Progress * 0.035f;
            progressRing.transform.localScale = Vector3.one * pulse * Mathf.SmoothStep(.85f, 1f, Mathf.Clamp01(focusAge / .16f));
        }

        public void Configure(GazeLockOnTarget lockTarget, LineRenderer ring, TextMesh text)
        {
            UnsubscribeTarget();
            target = lockTarget;
            progressRing = ring;
            statusText = text;
            if (isActiveAndEnabled) SubscribeTarget();
            Refresh(target != null ? target.Progress : 0f, target != null ? target.State : LockOnState.Idle);
        }

        public void Configure(GazeLockOnTarget lockTarget, LineRenderer ring, TextMesh text, Renderer[] brackets)
        {
            tacticalBrackets = brackets;
            Configure(lockTarget, ring, text);
        }

        private void HandleArrival(MeteorDefenseVR.Meteor.MeteorController _) { arrivalAge = 0f; }
        private void HandleFocusStarted(GazeLockOnTarget _) { focusAge = 0f; Refresh(target.Progress, LockOnState.Focusing); }
        private void HandleProgressChanged(GazeLockOnTarget _, float progress) => Refresh(progress, target.State);
        private void HandleLocked(GazeLockOnTarget _) => Refresh(1f, LockOnState.Locked);
        private void HandleLockLost(GazeLockOnTarget _) => Refresh(0f, target != null ? target.State : LockOnState.Idle);
        private void HandleGlobalReleased(GazeLockOnTarget released)
        {
            if (released == target) ClearPresentation();
        }

        public void ClearPresentation()
        {
            Refresh(0f, LockOnState.Destroyed);
            focusAge = 0f;
            previousAimReady = false;
        }

        private void SubscribeTarget()
        {
            if (target == null || subscribedTarget == target) return;
            subscribedTarget = target;
            subscribedTarget.FocusStarted += HandleFocusStarted;
            subscribedTarget.ProgressChanged += HandleProgressChanged;
            subscribedTarget.Locked += HandleLocked;
            subscribedTarget.LockLost += HandleLockLost;
            if (subscribedTarget.Meteor != null) subscribedTarget.Meteor.Spawned += HandleArrival;
        }

        private void UnsubscribeTarget()
        {
            if (subscribedTarget == null) return;
            subscribedTarget.FocusStarted -= HandleFocusStarted;
            subscribedTarget.ProgressChanged -= HandleProgressChanged;
            subscribedTarget.Locked -= HandleLocked;
            subscribedTarget.LockLost -= HandleLockLost;
            if (subscribedTarget.Meteor != null) subscribedTarget.Meteor.Spawned -= HandleArrival;
            subscribedTarget = null;
        }

        private void Refresh(float progress, LockOnState state)
        {
            bool locked=PresentationLocked(state);previousAimReady=locked;
            bool visible = state == LockOnState.Focusing || state == LockOnState.Locked;
            if (progressRing != null)
            {
                progressRing.enabled = visible;
                if (!visible)
                {
                    // Do not leave the last locked (green) geometry resident in the renderer.
                    // Some graphics drivers can present that buffer for one more frame.
                    progressRing.positionCount = 0;
                    progressRing.transform.localScale = Vector3.one;
                }
                else
                {
                    progressRing.loop = locked;
                    progressRing.useWorldSpace = false;
                    int count = locked
                        ? ringSegments + 1
                        : Mathf.Max(2, Mathf.CeilToInt(ringSegments * Mathf.Clamp01(progress)) + 1);
                    progressRing.positionCount = count;
                    float arc = locked ? 1f : Mathf.Clamp01(progress);
                    for (int i = 0; i < count; i++)
                    {
                        float angle = Mathf.PI * 2f * arc * i / Mathf.Max(1, count - 1);
                        progressRing.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * ringRadius);
                    }
                    Color color = locked ? lockedColor : focusingColor;
                    progressRing.startColor = color;
                    progressRing.endColor = color;
                }
            }

            if (statusText != null)
            {
                statusText.gameObject.SetActive(visible);
                statusText.text = locked ? "조준 완료" : state==LockOnState.Locked?"포탑 조준 중":"조준 중";
                previousLabelState = state;
                statusText.color = locked ? lockedColor : focusingColor;
            }


            if (tacticalBrackets != null)
            {
                Color color = locked ? lockedColor : focusingColor;
                foreach (Renderer bracket in tacticalBrackets)
                {
                    if (bracket == null) continue;
                    bracket.enabled = visible;
                    if (bracket is LineRenderer line)
                    {
                        line.startColor = color;
                        line.endColor = color;
                    }
                }
            }
        }
        private bool PresentationLocked(LockOnState state)
        {TurretAimingSystem aiming=TurretAimingSystem.Instance;return state==LockOnState.Locked&&(aiming==null||!aiming.HasTracked(target)||aiming.IsReadyFor(target));}
    }
}
