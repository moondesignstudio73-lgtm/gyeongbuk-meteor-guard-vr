using System.Collections.Generic;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace MeteorDefenseVR.PcInput
{
    [DefaultExecutionOrder(-150), DisallowMultipleComponent]
    public sealed class WindowsVRSimulation : MonoBehaviour
    {
        [SerializeField] private GazeInputRouter router;
        [SerializeField] private ViewDirectionProvider view;
        [SerializeField, Range(.01f, .5f)] private float mouseSensitivity = .10f;
        [SerializeField, Range(.01f, .5f)] private float verticalSensitivity = .09f;
        [SerializeField] private bool invertY;
        [Header("Unified desktop look / player settings")]
        [SerializeField, Range(.1f, 2f)] private float sensitivity = 1f;
        [SerializeField] private bool showCenterPoint = true;
        [SerializeField] private CameraShakeLevel cameraShake = CameraShakeLevel.Off;
        [Header("Webcam head steering (neutral pose is captured on entry/resume)")]
        [SerializeField, Range(1, 10)] private float headDeadzone = 4;
        [SerializeField, Range(10, 40)] private float headFullTurnAngle = 25;
        [SerializeField, Range(30, 150)] private float headTurnSpeed = 100;
        [SerializeField, Range(0f, .2f)] private float smoothing = .035f;
        [SerializeField, Range(-89f, 0f)] private float minimumPitch = -80f;
        [SerializeField, Range(0f, 89f)] private float maximumPitch = 80f;
        private readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        private GameFlowManager flow;
        private MeteorDefenseVR.Visual.WorldUiSafeArea safeArea;
        private Quaternion initialRotation;
        private Vector2 angles;
        private Quaternion lookRotation = Quaternion.identity;
        private MouseLookProvider mouseLook;
        private WebcamHeadTrackingProvider webcamLook;
        private VRHeadLookProvider headLook;
        private ILookInputProvider provider;
        private MeteorDefenseVR.Player.PlayerHealth health;
        private float shakeRemaining;
        private PcGazeMode lastMode;
        private bool active, initialized, previousListenerPause;
        private float previousTimeScale = 1f;
        private int ignoreInputFrame;
        public bool IsPaused { get; private set; }
        public bool MenuLookSuppressed { get; set; }
        public bool IsLooking => active && !IsPaused;
        public bool WantsLockedCursor => IsLooking && !HasRunningHmd();
        public bool UsesCenterAim => active && !HasRunningHmd();
        public bool OwnsDesktopLook => initialized && !HasRunningHmd();
        public float Sensitivity { get => sensitivity; set => sensitivity = RuntimeValueGuard.Clamp(value, .1f, 2f, 1); }
        public bool InvertY { get => invertY; set => invertY = value; }
        public bool ShowCenterPoint { get => showCenterPoint; set => showCenterPoint = value; }
        public CameraShakeLevel Shake { get => cameraShake; set => cameraShake = value; }
        public Vector2 Angles => angles;
        public Vector2 RequestedAngles => provider == webcamLook ? webcamLook.Angles : mouseLook != null ? mouseLook.Angles : Vector2.zero;
        public IViewDirectionProvider View => view;
        public void Configure(GazeInputRouter input, ViewDirectionProvider direction) { router = input; view = direction; }
        private void Start()
        {
            if (view == null || view.Camera == null || router == null) { enabled = false; return; }
            initialRotation = view.Camera.transform.localRotation; initialized = true;
            mouseLook = new MouseLookProvider(); webcamLook = new WebcamHeadTrackingProvider(router.Webcam); headLook = new VRHeadLookProvider(view.Camera);
            var reticle = GetComponent<CenterGazeReticle>(); if (reticle == null) reticle = gameObject.AddComponent<CenterGazeReticle>(); reticle.Configure(this, view);
            safeArea = FindAnyObjectByType<MeteorDefenseVR.Visual.WorldUiSafeArea>();
            health = FindAnyObjectByType<MeteorDefenseVR.Player.PlayerHealth>();
            if (health != null) health.ShipImpacted += Impact;
            flow = GameFlowManager.Instance;
            if (flow != null) flow.OnStateChanged += StateChanged;
        }
        public bool HasRunningHmd()
        {
            if (XRSettings.isDeviceActive) return true;
            displays.Clear(); SubsystemManager.GetSubsystems(displays);
            foreach (var display in displays) if (display.running) return true;
            return false;
        }
        private void Update()
        {
            if (!initialized) return;
            bool xr = HasRunningHmd();
            if (xr)
            {
                Leave(false);
                // A configured XR pose driver owns HMD transforms. Without one, use
                // the tracked node pose directly, without desktop clamps/smoothing.
                if (headLook.TryGetRotation(0, out var pose) && !headLook.ExternallyDriven)
                    view.Camera.transform.localRotation = initialRotation * pose;
                return;
            }
            bool requested = router.isActiveAndEnabled && flow != null && LookInputPolicy.GameplayLook(flow.CurrentState) && !router.CalibrationInProgress && !MenuLookSuppressed;
            if (requested != active)
            {
                if (requested) Enter(); else Leave(!xr && !MenuLookSuppressed);
            }
            bool calibrating = flow != null && (flow.CurrentState == GameState.EyeCalibration || MenuLookSuppressed && LookInputPolicy.GameplayLook(flow.CurrentState));
            if (!active && !calibrating) return;
            if (active && (provider == null || lastMode != router.ActiveMode)) SelectProvider();
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            { if (IsPaused) Resume(); else Pause(); return; }
            if (!active) return;
            if (!IsPaused && !Application.isBatchMode && (!Application.isFocused || Cursor.lockState != CursorLockMode.Locked)) Pause();
            if (!IsLooking || Time.frameCount <= ignoreInputFrame) return;
            if (router.InputSuspended) return;
            UpdateSettings();
            if (provider.TryGetRotation(Time.unscaledDeltaTime, out var rotation)) ApplyRotation(rotation, Time.unscaledDeltaTime);
        }
        private void UpdateSettings()
        {
            mouseLook.Horizontal = mouseSensitivity; mouseLook.Vertical = verticalSensitivity;
            mouseLook.Sensitivity = Sensitivity; mouseLook.InvertY = invertY;
            mouseLook.MinimumPitch = webcamLook.MinimumPitch = RuntimeValueGuard.Clamp(minimumPitch, -89, 0, -80);
            mouseLook.MaximumPitch = webcamLook.MaximumPitch = RuntimeValueGuard.Clamp(maximumPitch, 0, 89, 80);
            webcamLook.Deadzone = headDeadzone; webcamLook.FullTurnAngle = headFullTurnAngle; webcamLook.DegreesPerSecond = headTurnSpeed;
        }
        private void SelectProvider()
        {
            lastMode = router.ActiveMode;
            mouseLook.Seed(angles); webcamLook.Seed(angles);
            provider = lastMode == PcGazeMode.WebcamGaze ? (ILookInputProvider)webcamLook : mouseLook;
            ignoreInputFrame = Time.frameCount;
        }
        private void Enter()
        {
            active = true; SelectProvider(); LockCursor(); ignoreInputFrame = Time.frameCount;
            if (safeArea != null) safeArea.ShipLocked = true;
        }
        public void ApplyLook(Vector2 delta, float dt)
        {
            if (!IsLooking || view == null || HasRunningHmd() || !WebcamCalibrationMap.Finite(delta)) return;
            UpdateSettings(); mouseLook.AddDelta(delta);
            ApplyRotation(Quaternion.Euler(mouseLook.Angles.y, mouseLook.Angles.x, 0), dt);
        }
        private void ApplyRotation(Quaternion rotation, float dt)
        {
            float blend = smoothing <= 0 ? 1 : 1 - Mathf.Exp(-Mathf.Max(0, dt) / smoothing);
            lookRotation = Quaternion.Slerp(lookRotation, rotation, blend);
            Vector3 euler = lookRotation.eulerAngles;
            angles = new Vector2(Mathf.DeltaAngle(0, euler.y), Mathf.DeltaAngle(0, euler.x));
            float shake = LookInputPolicy.ShakeDegrees(cameraShake, false) * Mathf.Clamp01(shakeRemaining / .1f) * Mathf.Sin(shakeRemaining * 90);
            shakeRemaining = Mathf.Max(0, shakeRemaining - dt);
            view.Camera.transform.localRotation = initialRotation * lookRotation * Quaternion.Euler(shake, 0, 0);
        }
        private void Impact(Vector3 _, bool applied) { if (applied && active && !IsPaused) shakeRemaining = .1f; }
        public void Recenter()
        {
            angles = Vector2.zero; lookRotation = Quaternion.identity; shakeRemaining = 0;
            mouseLook?.Seed(Vector2.zero); webcamLook?.Seed(Vector2.zero);
            if (initialized && view != null && view.Camera != null && !HasRunningHmd()) view.Camera.transform.localRotation = initialRotation;
            ignoreInputFrame = Time.frameCount;
        }
        public void Pause()
        {
            if ((!active && !MenuLookSuppressed && (flow == null || flow.CurrentState != GameState.EyeCalibration)) || IsPaused) return;
            IsPaused = true; previousTimeScale = Time.timeScale; previousListenerPause = AudioListener.pause;
            Time.timeScale = 0; AudioListener.pause = true; router.InputSuspended = true;
            if (!Application.isBatchMode) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
        public void Resume()
        {
            if (!IsPaused) return;
            RestorePause(); webcamLook?.CenterHead(); if (active) LockCursor(); else UnlockCursor(); ignoreInputFrame = Time.frameCount;
        }
        private void RestorePause()
        {
            if (!IsPaused) return;
            IsPaused = false; Time.timeScale = previousTimeScale; AudioListener.pause = previousListenerPause;
            if (router != null) router.InputSuspended = false;
        }
        private void LockCursor() { if (!Application.isBatchMode) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } }
        private void Leave(bool restorePose)
        {
            RestorePause();
            if (!active) { UnlockCursor(); return; }
            active = false;
            if (safeArea != null) safeArea.ShipLocked = false;
            if (restorePose) Recenter();
            UnlockCursor();
        }
        private static void UnlockCursor() { if (!Application.isBatchMode) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } }
        private void StateChanged(GameState state)
        {
            if (!LookInputPolicy.GameplayLook(state)) { Leave(!HasRunningHmd()); Recenter(); }
            else if (initialized && !active && !MenuLookSuppressed && router.isActiveAndEnabled && !HasRunningHmd()) Enter();
        }
        private void OnApplicationFocus(bool focused) { if (!focused && !Application.isBatchMode) Pause(); }
        private void OnEnable()
        {
            if (initialized && flow != null) { flow.OnStateChanged -= StateChanged; flow.OnStateChanged += StateChanged; }
            if (health != null) { health.ShipImpacted -= Impact; health.ShipImpacted += Impact; }
        }
        private void OnDisable() { if (flow != null) flow.OnStateChanged -= StateChanged; if (health != null) health.ShipImpacted -= Impact; Leave(!HasRunningHmd()); }
        private void OnApplicationQuit() { RestorePause(); UnlockCursor(); }
    }
}
