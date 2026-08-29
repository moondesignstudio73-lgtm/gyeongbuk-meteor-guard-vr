using System;
using MeteorDefenseVR.EyeTracking;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Core;

namespace MeteorDefenseVR.PcInput
{
    [DefaultExecutionOrder(-200)]
    public sealed class GazeInputRouter : MonoBehaviour, IGazeProvider
    {
        [SerializeField] private PcGazeMode requestedMode = PcGazeMode.Auto;
        [SerializeField] private VREyeGazeProvider vr;
        [SerializeField] private WebcamGazeProvider webcam;
        [SerializeField] private EditorGazeProvider mouse;
        [SerializeField] private EditorGazeProvider center;
        [SerializeField] private Camera gazeCamera;
        [SerializeField, Range(1f, 10f)] private float trackingLossTimeout = 4f;
        private IGazeProvider active;
        private float lostTrackingTime;
        private bool attemptedWebcam, noWebcam;
        private GameFlowManager flow;
        public WindowsVRSimulation Simulation { get; private set; }
        private void Awake() => Simulation = GetComponent<WindowsVRSimulation>();
        public PcGazeMode RequestedMode => requestedMode;
        public PcGazeMode ActiveMode { get; private set; } = PcGazeMode.CameraCenterGaze;
        public WebcamGazeProvider Webcam => webcam;
        public bool InputSuspended { get; set; }
        // A screen-space settings/input panel must not activate world-space menus behind it.
        public bool UiModalOpen { get; set; }
        public bool EducationModalOpen { get; set; }
        public bool TextEntryOpen { get; set; }
        public bool IsMenuTrackingValid => !InputSuspended && !CalibrationInProgress && !UiModalOpen && active != null && active.IsTrackingValid;
        public bool CalibrationInProgress { get; set; }
        // Scoped by the lobby's synchronous replay Reset only; never persisted to disk.
        public bool PreserveCalibrationDuringReset { private get; set; }
        public string Notice { get; private set; } = string.Empty;
        public event Action<PcGazeMode> ModeChanged;
        public bool IsTrackingValid => !InputSuspended && !UiModalOpen && !EducationModalOpen && !CalibrationInProgress && !PointerOverUi() && active != null &&
            (Simulation != null && Simulation.UsesCenterAim
                ? Simulation.IsLooking && (ActiveMode != PcGazeMode.WebcamGaze || (webcam != null && webcam.HasFreshHeadRotation))
                : active.IsTrackingValid);
        public void SetStartupMode(PcGazeMode mode) => requestedMode = mode;
        public void Configure(Camera camera, VREyeGazeProvider vrInput, WebcamGazeProvider webcamInput, EditorGazeProvider mouseInput, EditorGazeProvider centerInput)
        { gazeCamera = camera; vr = vrInput; webcam = webcamInput; mouse = mouseInput; center = centerInput; active = mouse; }
        private void Start()
        {
            foreach (string argument in Environment.GetCommandLineArgs())
            {
                if (argument == "--no-webcam") noWebcam = true;
                if (argument == "--gaze=mouse") requestedMode = PcGazeMode.MouseGaze;
                if (argument == "--gaze=center") requestedMode = PcGazeMode.CameraCenterGaze;
                if (argument == "--gaze=simulation") requestedMode = PcGazeMode.WindowsVRSimulation;
                if (argument == "--gaze=webcam") requestedMode = PcGazeMode.WebcamGaze;
            }
            SetMode(requestedMode);
            BindFlow();
        }
        private void OnEnable() => BindFlow();
        private void BindFlow()
        {
            if (flow != null) flow.OnStateChanged -= HandleState;
            flow = GameFlowManager.Instance;
            if (flow != null) flow.OnStateChanged += HandleState;
        }
        private void OnDisable()
        {
            if (flow != null) flow.OnStateChanged -= HandleState;
            webcam?.StopCapture();
            attemptedWebcam = false;
        }
        private void HandleState(GameState state)
        {
            if (state == GameState.Boot)
            {
                InputSuspended = false;
                UiModalOpen = false;
                EducationModalOpen = false;
                TextEntryOpen = false;
                CalibrationInProgress = false;
                if (!PreserveCalibrationDuringReset) webcam?.Calibration.Reset();
            }
            if (state == GameState.Boot && webcam != null && webcam.Failed && !noWebcam
                && (requestedMode == PcGazeMode.Auto || requestedMode == PcGazeMode.WebcamGaze)) SetMode(requestedMode);
        }
        public void SetMode(PcGazeMode mode)
        {
            requestedMode = mode;
            lostTrackingTime = 0f;
            attemptedWebcam = false;
            Notice = string.Empty;
            webcam?.StopCapture();
            Evaluate();
        }
        private void Update() => Evaluate();
        private void Evaluate()
        {
            bool hmd = Simulation != null && Simulation.HasRunningHmd();
            bool eyeReady = vr != null && (vr.IsUsingEyeTracking || hmd);
            bool wantsWebcam = !hmd && (requestedMode == PcGazeMode.WebcamGaze || (requestedMode == PcGazeMode.Auto && !eyeReady));
            if (wantsWebcam && !attemptedWebcam && !noWebcam && webcam != null && Application.isPlaying)
            { attemptedWebcam = true; webcam.BeginCapture(); }
            if (!wantsWebcam && webcam != null && (webcam.IsStarting || webcam.IsStreaming))
            { webcam.StopCapture(); attemptedWebcam = false; }
            if (webcam != null && webcam.IsStreaming)
            {
                bool waitingForPlayer = flow != null && (flow.CurrentState == GameState.Boot || flow.CurrentState == GameState.Intro || flow.CurrentState == GameState.MissionBriefing);
                bool trackingFresh = LookInputPolicy.HasRequiredWebcamTracking(Simulation != null && Simulation.UsesCenterAim,
                    webcam.HasFreshHeadRotation, webcam.HasFreshTracking);
                lostTrackingTime = trackingFresh || waitingForPlayer ? 0f : lostTrackingTime + Time.unscaledDeltaTime;
                if (lostTrackingTime >= trackingLossTimeout)
                {
                    webcam.Fail("Webcam tracking lost - mouse fallback. Retry webcam when ready.");
                    Notice = webcam.Status;
                }
            }
            bool webcamReady = !noWebcam && webcam != null && !webcam.Failed && (webcam.IsStarting || webcam.IsStreaming);
            PcGazeMode selected = hmd ? PcGazeMode.VREyeTracking : GazeModePolicy.Select(requestedMode, eyeReady, webcamReady, Mouse.current != null && mouse != null);
            active = selected switch { PcGazeMode.VREyeTracking => vr, PcGazeMode.WebcamGaze => webcam, PcGazeMode.MouseGaze => mouse,
                PcGazeMode.WindowsVRSimulation => mouse != null && Mouse.current != null ? mouse : center, _ => center };
            if (webcam != null && webcam.Failed && wantsWebcam) Notice = webcam.Status;
            if (selected != ActiveMode) { ActiveMode = selected; ModeChanged?.Invoke(selected); }
        }
        public Ray GetGazeRay() => Simulation != null && Simulation.UsesCenterAim && Simulation.View != null ? Simulation.View.CenterRay : active != null ? active.GetGazeRay() : gazeCamera != null ? gazeCamera.ViewportPointToRay(new Vector3(.5f, .5f)) : new Ray(transform.position, transform.forward);
        public Vector3 GetGazeOrigin() => GetGazeRay().origin;
        public Vector3 GetGazeDirection() => GetGazeRay().direction;
        private bool PointerOverUi() => (Simulation == null || !Simulation.UsesCenterAim) && (ActiveMode == PcGazeMode.MouseGaze || ActiveMode == PcGazeMode.WindowsVRSimulation) && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
