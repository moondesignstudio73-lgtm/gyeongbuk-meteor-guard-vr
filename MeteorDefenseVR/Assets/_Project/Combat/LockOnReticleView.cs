using UnityEngine;

namespace MeteorDefenseVR.Combat
{
    [DisallowMultipleComponent]
    public sealed class LockOnReticleView : MonoBehaviour
    {
        [SerializeField] private GazeLockOnTarget target;
        [SerializeField] private LineRenderer progressRing;
        [SerializeField] private TextMesh statusText;
        [SerializeField, Min(8)] private int ringSegments = 48;
        [SerializeField, Min(0.05f)] private float ringRadius = 0.75f;
        [SerializeField] private Color focusingColor = new Color(0.1f, 0.95f, 1f);
        [SerializeField] private Color lockedColor = new Color(0.2f, 1f, 0.35f);

        private void OnEnable()
        {
            if (target == null) target = GetComponentInParent<GazeLockOnTarget>();
            if (target == null) return;
            target.FocusStarted += HandleFocusStarted;
            target.ProgressChanged += HandleProgressChanged;
            target.Locked += HandleLocked;
            target.LockLost += HandleLockLost;
            Refresh(target.Progress, target.State);
        }

        private void OnDisable()
        {
            if (target == null) return;
            target.FocusStarted -= HandleFocusStarted;
            target.ProgressChanged -= HandleProgressChanged;
            target.Locked -= HandleLocked;
            target.LockLost -= HandleLockLost;
        }

        public void Configure(GazeLockOnTarget lockTarget, LineRenderer ring, TextMesh text)
        {
            target = lockTarget;
            progressRing = ring;
            statusText = text;
            Refresh(target != null ? target.Progress : 0f, target != null ? target.State : LockOnState.Idle);
        }

        private void HandleFocusStarted(GazeLockOnTarget _) => Refresh(target.Progress, LockOnState.Focusing);
        private void HandleProgressChanged(GazeLockOnTarget _, float progress) => Refresh(progress, target.State);
        private void HandleLocked(GazeLockOnTarget _) => Refresh(1f, LockOnState.Locked);
        private void HandleLockLost(GazeLockOnTarget _) => Refresh(0f, LockOnState.Idle);

        private void Refresh(float progress, LockOnState state)
        {
            bool visible = state == LockOnState.Focusing || state == LockOnState.Locked;
            if (progressRing != null)
            {
                progressRing.enabled = visible;
                progressRing.loop = state == LockOnState.Locked;
                progressRing.useWorldSpace = false;
                int count = state == LockOnState.Locked
                    ? ringSegments + 1
                    : Mathf.Max(2, Mathf.CeilToInt(ringSegments * Mathf.Clamp01(progress)) + 1);
                progressRing.positionCount = count;
                float arc = state == LockOnState.Locked ? 1f : Mathf.Clamp01(progress);
                for (int i = 0; i < count; i++)
                {
                    float angle = Mathf.PI * 2f * arc * i / ringSegments;
                    progressRing.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * ringRadius);
                }
                Color color = state == LockOnState.Locked ? lockedColor : focusingColor;
                progressRing.startColor = color;
                progressRing.endColor = color;
            }

            if (statusText != null)
            {
                statusText.gameObject.SetActive(visible);
                statusText.text = state == LockOnState.Locked ? "LOCK ON" : "LOCKING";
                statusText.color = state == LockOnState.Locked ? lockedColor : focusingColor;
            }
        }
    }
}
