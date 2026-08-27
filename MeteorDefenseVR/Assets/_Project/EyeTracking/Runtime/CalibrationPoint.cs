using System;
using UnityEngine;

namespace MeteorDefenseVR.EyeTracking
{
    public enum CalibrationPointState
    {
        Inactive,
        Waiting,
        Focusing,
        Complete
    }

    [DisallowMultipleComponent]
    public sealed class CalibrationPoint : MonoBehaviour, IGazeTarget
    {
        [SerializeField, Min(0.1f)] private float dwellDuration = 0.65f;
        [SerializeField, Min(0f)] private float decaySpeed = 1.5f;
        [SerializeField] private bool resetProgressOnExit;
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Color waitingColor = new Color(0.1f, 0.85f, 1f);
        [SerializeField] private Color focusingColor = new Color(0.3f, 1f, 0.65f);
        [SerializeField] private Color completeColor = Color.green;

        private MaterialPropertyBlock propertyBlock;
        private bool isGazedAt;

        public CalibrationPointState State { get; private set; } = CalibrationPointState.Inactive;
        public float Progress { get; private set; }
        public bool IsComplete => State == CalibrationPointState.Complete;
        public float DwellDuration { get => dwellDuration; set => dwellDuration = Mathf.Max(0.1f, value); }
        public float DecaySpeed { get => decaySpeed; set => decaySpeed = Mathf.Max(0f, value); }

        public event Action<CalibrationPoint, float> ProgressChanged;
        public event Action<CalibrationPoint> Completed;

        private void Awake()
        {
            if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();
            propertyBlock = new MaterialPropertyBlock();
            RefreshVisual();
        }

        private void Update()
        {
            if (State == CalibrationPointState.Inactive || State == CalibrationPointState.Complete || isGazedAt) return;
            if (Progress <= 0f || decaySpeed <= 0f) return;
            SetProgress(Progress - decaySpeed * Time.deltaTime / dwellDuration);
            if (Progress <= 0f) SetState(CalibrationPointState.Waiting);
        }

        public void Activate()
        {
            gameObject.SetActive(true);
            isGazedAt = false;
            SetProgress(0f);
            SetState(CalibrationPointState.Waiting);
        }

        public void ResetPoint(bool hide = true)
        {
            isGazedAt = false;
            Progress = 0f;
            State = CalibrationPointState.Inactive;
            RefreshVisual();
            if (hide) gameObject.SetActive(false);
        }

        public void Advance(float deltaTime)
        {
            if (State == CalibrationPointState.Inactive || State == CalibrationPointState.Complete) return;
            SetState(CalibrationPointState.Focusing);
            SetProgress(Progress + Mathf.Max(0f, deltaTime) / dwellDuration);
            if (Progress < 1f) return;
            SetState(CalibrationPointState.Complete);
            Completed?.Invoke(this);
        }

        public void OnGazeEnter(RaycastHit hit)
        {
            if (State == CalibrationPointState.Inactive || State == CalibrationPointState.Complete) return;
            isGazedAt = true;
            SetState(CalibrationPointState.Focusing);
        }

        public void OnGazeStay(RaycastHit hit, float deltaTime) => Advance(deltaTime);

        public void OnGazeExit()
        {
            isGazedAt = false;
            if (State == CalibrationPointState.Inactive || State == CalibrationPointState.Complete) return;
            if (resetProgressOnExit) SetProgress(0f);
            SetState(CalibrationPointState.Waiting);
        }

        private void SetProgress(float value)
        {
            float next = Mathf.Clamp01(value);
            if (Mathf.Approximately(next, Progress)) return;
            Progress = next;
            ProgressChanged?.Invoke(this, Progress);
            RefreshVisual();
        }

        private void SetState(CalibrationPointState state)
        {
            if (State == state) return;
            State = state;
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1.2f, Progress);
            if (targetRenderer == null) return;

            propertyBlock ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            Color color = State == CalibrationPointState.Complete
                ? completeColor
                : State == CalibrationPointState.Focusing ? focusingColor : waitingColor;
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
