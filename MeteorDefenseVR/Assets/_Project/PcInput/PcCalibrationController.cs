using MeteorDefenseVR.Core;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    // Adapts the PC calibration presentation without introducing camera APIs into the existing sequence.
    [DefaultExecutionOrder(100)]
    public sealed class PcCalibrationController : MonoBehaviour
    {
        [SerializeField] private GazeInputRouter router;
        [SerializeField] private CalibrationSequenceController legacyCalibration;
        [SerializeField, Range(.6f, 3f)] private float sampleDuration = 1.2f;
        [SerializeField, Range(.2f, 2f)] private float settleDuration = .65f;
        [SerializeField, Range(5f, 30f)] private float pointTimeout = 15f;
        private GameFlowManager flow;
        private bool pending, completing;
        private float elapsed, stableTime, completeTime;
        private float lastAcceptedTime;
        private float pointCompleteUntil;
        private int lastSample, sampleCount;
        private Vector2 sum, sumSquares;
        private readonly Vector2[] means = new Vector2[5];
        public bool IsVisible { get; private set; }
        public bool IsWebcamCalibration { get; private set; }
        public int PointIndex { get; private set; }
        public float Progress => completing ? 1f : Mathf.Clamp01(stableTime / sampleDuration);
        // Presentation reads the existing acceptance state; a full timer alone is not acceptance.
        public bool IsPointComplete => completing || pointCompleteUntil > 0f;
        public Vector2 Target => WebcamCalibrationMap.Targets[Mathf.Clamp(PointIndex, 0, 4)];
        public string Message { get; private set; } = string.Empty;
        public void Configure(GazeInputRouter input, CalibrationSequenceController sequence) { router = input; legacyCalibration = sequence; }
        private void Start() => Bind();
        private void OnEnable() => Bind();
        private void Bind()
        {
            if (flow != null) flow.OnStateChanged -= HandleState;
            flow = GameFlowManager.Instance;
            if (flow != null) flow.OnStateChanged += HandleState;
            if (router != null) { router.ModeChanged -= HandleMode; router.ModeChanged += HandleMode; }
            if (flow != null) HandleState(flow.CurrentState);
        }
        private void OnDisable()
        {
            if (flow != null) flow.OnStateChanged -= HandleState;
            if (router != null) { router.ModeChanged -= HandleMode; router.CalibrationInProgress = false; }
        }
        private void HandleMode(PcGazeMode _) { if (flow != null && flow.CurrentState == GameState.EyeCalibration) pending = true; }
        private void HandleState(GameState state)
        {
            pending = state == GameState.EyeCalibration;
            IsVisible = false;
            completing = false;
            if (router != null) router.CalibrationInProgress = false;
            if (state == GameState.Boot || state == GameState.Reset)
            {
                System.Array.Clear(means, 0, means.Length);
                ResetSamples(true); lastSample = 0; PointIndex = 0; pointCompleteUntil = 0;
            }
        }
        private void LateUpdate()
        {
            if (pending) { pending = false; Begin(); }
            if (!IsVisible || router.InputSuspended) return;
            if (completing)
            {
                completeTime += Time.unscaledDeltaTime;
                if (completeTime >= 1.1f) flow?.CompleteCalibration();
                return;
            }
            if (!IsWebcamCalibration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed >= 2f) Complete();
                return;
            }
            if (pointCompleteUntil > 0f)
            {
                if (Time.unscaledTime < pointCompleteUntil) return;
                pointCompleteUntil = 0f;
                PointIndex++;
                ResetSamples(true);
            }
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= pointTimeout) { FallBack("눈을 인식하기 어려워요.\n마우스로 계속할게요."); return; }
            WebcamGazeProvider webcam = router.Webcam;
            if (elapsed < settleDuration || !webcam.HasFreshTracking || webcam.SampleNumber == lastSample) return;
            lastSample = webcam.SampleNumber;
            Vector2 sample = webcam.RawFeatures;
            if (sampleCount > 0 && Time.unscaledTime - lastAcceptedTime > .4f) ResetSamples(false);
            sampleCount++;
            sum += sample;
            sumSquares += Vector2.Scale(sample, sample);
            stableTime += sampleCount > 1 ? Mathf.Clamp(Time.unscaledTime - lastAcceptedTime, 0f, .25f) : 0f;
            lastAcceptedTime = Time.unscaledTime;
            if (stableTime < sampleDuration || sampleCount < 8) return;
            Vector2 mean = sum / sampleCount;
            Vector2 variance = sumSquares / sampleCount - Vector2.Scale(mean, mean);
            if (variance.x + variance.y > .012f)
            { Message = "고개를 편안히 고정하고 표시점을 바라봐 주세요."; ResetSamples(false); return; }
            means[PointIndex] = mean;
            if (PointIndex < 4) { pointCompleteUntil = Time.unscaledTime + .3f; return; }
            if (!webcam.Calibration.Fit(means)) { FallBack("눈을 인식하기 어려워요.\n마우스로 계속할게요."); return; }
            Complete();
        }
        private void Begin()
        {
            pointCompleteUntil = 0f;
            if (router.ActiveMode == PcGazeMode.VREyeTracking)
            {
                IsVisible = false;
                completing = false;
                router.CalibrationInProgress = false;
                legacyCalibration?.BeginCalibration();
                return;
            }
            legacyCalibration?.ResetSequence();
            if (legacyCalibration != null)
                foreach (var view in legacyCalibration.GetComponentsInChildren<CalibrationStatusView>(true)) view.SetMessage(string.Empty);
            router.CalibrationInProgress = true;
            IsVisible = true;
            completing = false;
            PointIndex = 0;
            IsWebcamCalibration = router.ActiveMode == PcGazeMode.WebcamGaze;
            ResetSamples(true);
            Message = IsWebcamCalibration ? "눈의 위치를 맞출게요.\n고개를 편하게 두고 빛나는 점을 바라보세요." : AimInstructions();
            if (IsWebcamCalibration) router.Webcam.Calibration.Reset();
        }
        private void ResetSamples(bool resetElapsed)
        { if (resetElapsed) elapsed = 0f; stableTime = 0f; sampleCount = 0; sum = sumSquares = Vector2.zero; }
        private string AimInstructions()
        {
            if (router.Simulation != null && router.Simulation.OwnsDesktopLook)
                return "마우스로 주변을 둘러보세요.\n가운데 조준점을 운석에 맞추면 자동으로 발사돼요.";
            return router.ActiveMode == PcGazeMode.MouseGaze
                ? "마우스 포인터를 운석 위에 올려 보세요.\n잠시 기다리면 자동으로 발사돼요."
                : "가운데 조준점을 운석에 맞춰 보세요.\n잠시 기다리면 자동으로 발사돼요.";
        }
        private void Complete() { completing = true; completeTime = 0f; Message = IsWebcamCalibration ? "준비됐어요!\n이제 운석을 조준해 볼까요?" : "준비됐어요!\n운석을 조준해 볼까요?"; }
        private void FallBack(string message)
        {
            router.SetMode(PcGazeMode.MouseGaze);
            pending = false;
            IsWebcamCalibration = false;
            Complete();
            Message = message;
        }
        public void RetryWebcam()
        {
            router.SetMode(PcGazeMode.WebcamGaze);
            // Restart from the ready state so gameplay hazards never run during calibration.
            Object.FindAnyObjectByType<OperationsResetController>()?.ResetExperience();
            Message = "카메라에 얼굴이 보이도록 앉은 뒤\n시작을 선택하세요.";
        }
    }
}
