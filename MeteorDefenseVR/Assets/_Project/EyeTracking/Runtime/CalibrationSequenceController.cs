using System;
using System.Collections;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using UnityEngine;

namespace MeteorDefenseVR.EyeTracking
{
    [DisallowMultipleComponent]
    public sealed class CalibrationSequenceController : MonoBehaviour
    {
        [SerializeField] private CalibrationPoint[] points = Array.Empty<CalibrationPoint>();
        [SerializeField, Min(0f)] private float pointSuccessDelay = 0.15f;
        [SerializeField, Min(0f)] private float completionDisplayDuration = 1f;
        [SerializeField, Min(1f)] private float trackingFailureTimeout = 2f;
        [SerializeField] private MonoBehaviour gazeProviderComponent;
        [SerializeField] private bool autoStartFromGameFlow = true;

        private IGazeProvider gazeProvider;
        private GameFlowManager gameFlow;
        private Coroutine sequenceRoutine;
        private float invalidTrackingTime;

        public int CurrentPointIndex { get; private set; } = -1;
        public bool IsRunning { get; private set; }
        public bool IsComplete { get; private set; }
        public bool IsTrackingFailed { get; private set; }
        public string StatusMessage { get; private set; } = string.Empty;
        public int PointCount => points?.Length ?? 0;
        public float PointSuccessDelay { get => pointSuccessDelay; set => pointSuccessDelay = Mathf.Max(0f, value); }
        public float CompletionDisplayDuration { get => completionDisplayDuration; set => completionDisplayDuration = Mathf.Max(0f, value); }

        public event Action<string> StatusMessageChanged;
        public event Action<int, CalibrationPoint> PointActivated;
        public event Action<int, CalibrationPoint> PointCompleted;
        public event Action CalibrationCompleted;
        public event Action TrackingFailed;

        private void Awake() => ResolveDependencies();

        private void OnEnable()
        {
            ResolveDependencies();
            if (gameFlow != null) gameFlow.OnStateChanged += HandleStateChanged;
        }

        private void Start()
        {
            if (autoStartFromGameFlow && gameFlow != null && gameFlow.CurrentState == GameState.EyeCalibration)
                BeginCalibration();
        }

        private void Update()
        {
            if (!IsRunning || IsComplete) return;
            bool valid = gazeProvider != null && gazeProvider.IsTrackingValid;
            invalidTrackingTime = valid ? 0f : invalidTrackingTime + Time.unscaledDeltaTime;
            if (!valid && !IsTrackingFailed && invalidTrackingTime >= trackingFailureTimeout)
            {
                IsTrackingFailed = true;
                SetStatus("시선 인식에 실패했습니다. 다시 시도하거나 Head Gaze Mode를 사용하세요.");
                TrackingFailed?.Invoke();
            }
        }

        private void OnDisable()
        {
            if (gameFlow != null) gameFlow.OnStateChanged -= HandleStateChanged;
            UnsubscribePoints();
        }

        public void Configure(CalibrationPoint[] calibrationPoints, IGazeProvider provider = null)
        {
            UnsubscribePoints();
            points = calibrationPoints ?? Array.Empty<CalibrationPoint>();
            gazeProvider = provider ?? gazeProviderComponent as IGazeProvider;
            gazeProviderComponent = gazeProvider as MonoBehaviour;
        }

        public void BeginCalibration()
        {
            CancelRoutine();
            ResetSequence();
            if (points == null || points.Length == 0)
            {
                SetStatus("시선 인식 지점이 설정되지 않았습니다.");
                return;
            }

            IsRunning = true;
            SubscribePoints();
            SetStatus("시선 인식을 시작합니다. 화면에 나타나는 점을 바라봐 주세요.");
            ActivatePoint(0);
        }

        public void RetryCalibration() => BeginCalibration();

        public void UseHeadGazeMode()
        {
            VREyeGazeProvider vrProvider = gazeProvider as VREyeGazeProvider;
            if (vrProvider != null) vrProvider.ForceHeadGazeMode();
            IsTrackingFailed = false;
            invalidTrackingTime = 0f;
            SetStatus("Head Gaze Mode로 진행합니다. 화면의 점을 바라봐 주세요.");
        }

        public void ResetSequence()
        {
            CancelRoutine();
            UnsubscribePoints();
            CurrentPointIndex = -1;
            IsRunning = false;
            IsComplete = false;
            IsTrackingFailed = false;
            invalidTrackingTime = 0f;
            if (points == null) return;
            for (int i = 0; i < points.Length; i++)
                if (points[i] != null) points[i].ResetPoint();
        }

        private void HandleStateChanged(GameState state)
        {
            if (!autoStartFromGameFlow) return;
            if (state == GameState.EyeCalibration) BeginCalibration();
            else if (IsRunning) ResetSequence();
        }

        private void HandlePointCompleted(CalibrationPoint point)
        {
            if (!IsRunning || CurrentPointIndex < 0 || points[CurrentPointIndex] != point) return;
            PointCompleted?.Invoke(CurrentPointIndex, point);
            sequenceRoutine = StartCoroutine(AdvanceAfterDelay());
        }

        private IEnumerator AdvanceAfterDelay()
        {
            if (pointSuccessDelay > 0f) yield return new WaitForSecondsRealtime(pointSuccessDelay);
            int next = CurrentPointIndex + 1;
            if (next < points.Length)
            {
                ActivatePoint(next);
                yield break;
            }

            IsRunning = false;
            IsComplete = true;
            CurrentPointIndex = points.Length;
            SetStatus("시선 인식 완료!");
            CalibrationCompleted?.Invoke();
            if (completionDisplayDuration > 0f) yield return new WaitForSecondsRealtime(completionDisplayDuration);
            gameFlow?.StartTutorial();
            sequenceRoutine = null;
        }

        private void ActivatePoint(int index)
        {
            if (index < 0 || index >= points.Length || points[index] == null) return;
            if (CurrentPointIndex >= 0 && CurrentPointIndex < points.Length && points[CurrentPointIndex] != null)
                points[CurrentPointIndex].gameObject.SetActive(false);
            CurrentPointIndex = index;
            points[index].Activate();
            PointActivated?.Invoke(index, points[index]);
        }

        private void ResolveDependencies()
        {
            gazeProvider = gazeProviderComponent as IGazeProvider;
            if (gazeProvider == null)
            {
                MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                for (int i = 0; i < behaviours.Length; i++)
                    if (behaviours[i] is IGazeProvider provider) { gazeProvider = provider; gazeProviderComponent = behaviours[i]; break; }
            }
            gameFlow = GameFlowManager.Instance;
        }

        private void SubscribePoints()
        {
            if (points == null) return;
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] == null) continue;
                points[i].Completed -= HandlePointCompleted;
                points[i].Completed += HandlePointCompleted;
            }
        }

        private void UnsubscribePoints()
        {
            if (points == null) return;
            for (int i = 0; i < points.Length; i++)
                if (points[i] != null) points[i].Completed -= HandlePointCompleted;
        }

        private void CancelRoutine()
        {
            if (sequenceRoutine == null) return;
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        private void SetStatus(string message)
        {
            StatusMessage = message ?? string.Empty;
            StatusMessageChanged?.Invoke(StatusMessage);
        }
    }
}
