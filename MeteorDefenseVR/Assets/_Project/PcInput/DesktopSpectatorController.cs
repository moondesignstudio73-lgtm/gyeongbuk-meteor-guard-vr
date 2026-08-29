using System;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.UI;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MeteorDefenseVR.PcInput
{
    public enum DesktopViewMode { Mirror, Spectator }

    [Serializable]
    public sealed class DesktopSpectatorSettings
    {
        public DesktopViewMode viewMode = DesktopViewMode.Mirror;
        [Tooltip("Spectator texture only. Native Mirror uses the existing game window / XR mirror resolution.")]
        [Range(640, 1920)] public int width = 1280;
        [Range(360, 1080)] public int height = 720;
        [Tooltip("Extra spectator camera only. Never changes HMD or Application.targetFrameRate.")]
        [Range(10, 60)] public int framesPerSecond = 30;
        [Range(40f, 85f)] public float fieldOfView = 65f;
        public Vector3 localPosition = new Vector3(5.5f, 2.5f, -8f);
        public Vector3 localLookAt = new Vector3(0, .2f, 16f);

        public void Validate()
        {
            if (viewMode != DesktopViewMode.Mirror && viewMode != DesktopViewMode.Spectator) viewMode = DesktopViewMode.Mirror;
            width = Mathf.Clamp(width, 640, 1920); height = Mathf.Clamp(height, 360, 1080);
            framesPerSecond = Mathf.Clamp(framesPerSecond, 10, 60);
            fieldOfView = RuntimeValueGuard.Clamp(fieldOfView, 40, 85, 65);
            localPosition = SafeVector(localPosition, new Vector3(5.5f, 2.5f, -8f));
            localLookAt = SafeVector(localLookAt, new Vector3(0, .2f, 16f));
        }
        private static Vector3 SafeVector(Vector3 value, Vector3 fallback)
            => RuntimeValueGuard.IsFinite(value.x) && RuntimeValueGuard.IsFinite(value.y) && RuntimeValueGuard.IsFinite(value.z)
                ? Vector3.ClampMagnitude(value, 100f) : fallback;
    }

    // No GameFlow/Combat writes. This component owns only the desktop presentation resources.
    [DefaultExecutionOrder(1500), DisallowMultipleComponent]
    public sealed class DesktopSpectatorController : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform spectatorAnchor;
        [SerializeField, Tooltip("Presentation layers excluded from the extra camera, not from HMD rendering.")] private LayerMask spectatorExcludedLayers;
        [SerializeField, Tooltip("Exterior details hidden from HMD but intentionally visible to the third-person spectator camera.")] private LayerMask spectatorIncludedLayers;
        [SerializeField] private MissionHudController hud;
        [SerializeField] private GazeRaycaster gaze;
        [SerializeField] private ExperienceConfiguration experience;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private DesktopSpectatorSettings settings = new DesktopSpectatorSettings();
        private static DesktopSpectatorController owner;
        private static readonly ProfilerMarker RenderMarker = new ProfilerMarker("MeteorDefense.Spectator.Render");
        private IDesktopMirrorSource mirrorSource;
        private DesktopSpectatorView view;
        private Camera observer;
        private RenderTexture texture;
        private readonly UniversalRenderPipeline.SingleCameraRequest request = new UniversalRenderPipeline.SingleCameraRequest();
        private Vector3 originPosition;
        private Quaternion originRotation;
        private double nextRender, nextProbe, nextHud, lockFlashUntil;
        private bool initialized, hasFrame;
        public DesktopSpectatorSettings Settings => settings;
        public DesktopViewMode Mode => settings.viewMode;
        public Camera ObserverCamera => observer;
        public bool ObserverRendersXr => observer != null && observer.GetUniversalAdditionalCameraData().allowXRRendering;
        public RenderTexture OutputTexture => texture;
        public DesktopSpectatorView View => view;
        public int RenderedFrames { get; private set; }
        public bool XrRunning => mirrorSource != null && mirrorSource.IsRunning;
        public bool CanOperate => experience != null && experience.ShowOperator;
        public bool DesktopSupported => SupportsPlatform(Application.platform);
        public int SpectatorIncludedMask => spectatorIncludedLayers.value;
        public static bool SupportsPlatform(RuntimePlatform platform)
            => platform == RuntimePlatform.WindowsPlayer || platform == RuntimePlatform.WindowsEditor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearOwner() => owner = null;

        public void Configure(Camera camera, MissionHudController missionHud, GazeRaycaster raycaster, ExperienceConfiguration configuration, TMP_FontAsset uiFont, LayerMask excludedLayers = default)
        { playerCamera = camera; hud = missionHud; gaze = raycaster; experience = configuration; font = uiFont; spectatorExcludedLayers = excludedLayers; }

        public void IncludeLayerForSpectator(int layer)
        { if(layer>=0&&layer<32)spectatorIncludedLayers=spectatorIncludedLayers.value|(1<<layer); }

        private void OnValidate() => settings.Validate();
        private void Start() => Initialize();
        private void Initialize()
        {
            if (initialized || !DesktopSupported || (owner != null && owner != this)) return;
            if (playerCamera == null) playerCamera = Camera.main;
            if (playerCamera == null) return;
            owner = this; settings.Validate();
            originPosition = playerCamera.transform.position; originRotation = playerCamera.transform.rotation;
            mirrorSource ??= new DesktopMirrorSource();
            mirrorSource.Refresh();
            if (hud != null) { hud.FeedbackChanged -= HandleFeedback; hud.FeedbackChanged += HandleFeedback; }
            view = new DesktopSpectatorView(transform, font, ToggleFromOperator);
            initialized = true;
        }

        private void LateUpdate()
        {
            if (!initialized) Initialize();
            if (!initialized || playerCamera == null) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now >= nextProbe) { nextProbe = now + 1; mirrorSource.Refresh(); }
            if (settings.viewMode == DesktopViewMode.Spectator && now >= nextRender)
            {
                // Never catch up missed frames: a slow frame must not queue more scene renders.
                nextRender = now + 1d / Mathf.Clamp(settings.framesPerSecond, 10, 60);
                RenderSpectator();
            }
            view.SetOutput(settings.viewMode == DesktopViewMode.Spectator && hasFrame ? texture : null);
            if (now < nextHud) return;
            nextHud = now + .1;
            var state = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentState : GameState.Boot;
            var target = gaze != null ? gaze.CurrentTarget as GazeLockOnTarget : null;
            view.Present(hud, target, state, settings.viewMode, XrRunning, CanOperate, now < lockFlashUntil);
        }

        private void HandleFeedback(string message)
        {
            // Fired publishes LOCK ON before damage/score replaces that feedback in the same frame.
            // Latch only the monitor presentation, without delaying the weapon or changing its state.
            if (message == "LOCK ON") lockFlashUntil = Time.realtimeSinceStartupAsDouble + .35;
        }

        public bool SelectFromOperator(DesktopViewMode mode)
        {
            if (!CanOperate) return false;
            SetViewMode(mode); return true;
        }
        private void ToggleFromOperator() => SelectFromOperator(Mode == DesktopViewMode.Mirror ? DesktopViewMode.Spectator : DesktopViewMode.Mirror);

        public void SetViewMode(DesktopViewMode mode)
        {
            settings.viewMode = mode; settings.Validate(); nextRender = nextHud = 0;
            // Keep one disabled camera/texture cached after first use to avoid allocation on repeated switches.
            if (Mode == DesktopViewMode.Mirror) { hasFrame = false; view?.SetOutput(null); }
        }

        public void SetMirrorSource(IDesktopMirrorSource source)
        { mirrorSource?.Dispose(); mirrorSource = source ?? new DesktopMirrorSource(); nextProbe = 0; }

        private void RenderSpectator()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { SetViewMode(DesktopViewMode.Mirror); return; }
            settings.Validate();
            try
            {
                EnsureObserver();
                EnsureTexture();
                UpdateObserverPose();
                request.destination = texture;
                if (!RenderPipeline.SupportsRenderRequest(observer, request)) { SetViewMode(DesktopViewMode.Mirror); return; }
                using (RenderMarker.Auto()) RenderPipeline.SubmitRenderRequest(observer, request);
                hasFrame = true; RenderedFrames++;
            }
            catch (Exception exception)
            {
                // Failure is isolated to the monitor; leave the original player view and flow running.
                SetViewMode(DesktopViewMode.Mirror);
                Debug.LogWarning("[Spectator] Mirror fallback: " + exception.GetType().Name);
            }
        }

        private void EnsureObserver()
        {
            if (observer != null) return;
            var root = new GameObject("DesktopSpectatorCamera"); root.transform.SetParent(transform, false);
            observer = root.AddComponent<Camera>(); observer.CopyFrom(playerCamera);
            observer.enabled = false; observer.tag = "Untagged"; observer.targetTexture = null;
            // URP ignores stereoTargetEye (and warns when it is assigned). allowXRRendering below is authoritative.
            int turretLayer=LayerMask.NameToLayer(TurretAimingSystem.PlayerHiddenLayerName);
            int runtimeIncluded=turretLayer>=0?1<<turretLayer:0;
            observer.cullingMask = (playerCamera.cullingMask & ~spectatorExcludedLayers.value) | spectatorIncludedLayers.value | runtimeIncluded;
            observer.allowMSAA = false; observer.allowDynamicResolution = false;
            observer.rect = new Rect(0, 0, 1, 1);
            var data = observer.GetUniversalAdditionalCameraData();
            data.allowXRRendering = false; data.allowHDROutput = false; data.renderType = CameraRenderType.Base;
            data.renderShadows = false; data.renderPostProcessing = false;
            data.requiresColorOption = CameraOverrideOption.Off; data.requiresDepthOption = CameraOverrideOption.Off;
            // Deliberately do not clone the player GameObject: no AudioListener, tracking, or scripts.
        }

        private void EnsureTexture()
        {
            if (texture != null && texture.width == settings.width && texture.height == settings.height && texture.IsCreated()) return;
            view.SetOutput(null); hasFrame = false; ReleaseTexture();
            texture = new RenderTexture(settings.width, settings.height, 24, RenderTextureFormat.ARGB32)
            { name = "DesktopSpectatorFrame", antiAliasing = 1, useMipMap = false, autoGenerateMips = false, filterMode = FilterMode.Bilinear };
            if (!texture.Create()) throw new InvalidOperationException("Spectator texture unavailable");
        }

        private void UpdateObserverPose()
        {
            if (spectatorAnchor != null) observer.transform.SetPositionAndRotation(spectatorAnchor.position, spectatorAnchor.rotation);
            else
            {
                // Fixed mission reference, not HMD motion. No camera shake or forced player movement.
                Vector3 position = originPosition + originRotation * settings.localPosition;
                Vector3 look = originPosition + originRotation * settings.localLookAt - position;
                observer.transform.SetPositionAndRotation(position, look.sqrMagnitude > .001f ? Quaternion.LookRotation(look, originRotation * Vector3.up) : originRotation);
            }
            observer.fieldOfView = settings.fieldOfView; observer.aspect = (float)settings.width / settings.height;
        }

        private void ReleaseTexture()
        {
            request.destination = null;
            if (texture == null) return;
            texture.Release(); DestroyOwned(texture); texture = null;
        }
        private void OnDisable()
        {
            if (owner == this) owner = null;
            if (hud != null) hud.FeedbackChanged -= HandleFeedback;
            view?.Dispose(); view = null; mirrorSource?.Dispose(); mirrorSource = null;
            ReleaseTexture(); if (observer != null) DestroyOwned(observer.gameObject); observer = null;
            initialized = hasFrame = false; nextRender = nextHud = nextProbe = lockFlashUntil = 0;
        }
        internal static void DestroyOwned(UnityEngine.Object target)
        { if (Application.isPlaying) Destroy(target); else DestroyImmediate(target); }
    }
}
