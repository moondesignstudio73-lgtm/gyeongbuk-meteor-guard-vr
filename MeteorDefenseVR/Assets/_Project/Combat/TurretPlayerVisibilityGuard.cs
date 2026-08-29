using UnityEngine;
using UnityEngine.Rendering;

namespace MeteorDefenseVR.Combat
{
    /// <summary>
    /// Keeps exterior turret presentation out of the HMD/player camera while leaving the
    /// transforms, muzzle lights and laser firing solution fully active for gameplay.
    /// Spectator cameras opt the hidden layer back in through DesktopSpectatorController.
    /// </summary>
    [DefaultExecutionOrder(1490), DisallowMultipleComponent]
    public sealed class TurretPlayerVisibilityGuard : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private string hiddenLayerName = TurretAimingSystem.PlayerHiddenLayerName;

        private int hiddenLayer = -1;
        private int hiddenMask;

        public Transform VisualRoot => visualRoot;
        public Camera PlayerCamera => playerCamera;
        public int HiddenLayer => hiddenLayer;

        public void Configure(Transform root, Camera camera)
        {
            visualRoot = root != null ? root : transform;
            playerCamera = camera;
            ResolveLayer();
            ApplyRendererLayers();
            ExcludeFromPlayer(playerCamera);
        }

        private void Awake()
        {
            if (visualRoot == null) visualRoot = transform;
            if (playerCamera == null) playerCamera = Camera.main;
            ResolveLayer();
            ApplyRendererLayers();
            ExcludeFromPlayer(playerCamera);
        }

        private void OnEnable()
        {
            Camera.onPreCull += HandleCameraPreCull;
            RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
            ExcludeFromPlayer(playerCamera);
        }

        private void LateUpdate()
        {
            if (playerCamera == null) playerCamera = Camera.main;
            ExcludeFromPlayer(playerCamera);
        }

        private void OnTransformChildrenChanged()
        {
            ApplyRendererLayers();
        }

        private void OnDisable()
        {
            Camera.onPreCull -= HandleCameraPreCull;
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
        }

        private void HandleCameraPreCull(Camera camera)
        {
            if (camera == playerCamera) ExcludeFromPlayer(camera);
        }

        private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == playerCamera) ExcludeFromPlayer(camera);
        }

        public void ApplyNow()
        {
            ResolveLayer();
            ApplyRendererLayers();
            ExcludeFromPlayer(playerCamera);
        }

        private void ResolveLayer()
        {
            hiddenLayer = LayerMask.NameToLayer(hiddenLayerName);
            hiddenMask = hiddenLayer >= 0 ? 1 << hiddenLayer : 0;
        }

        private void ApplyRendererLayers()
        {
            if (visualRoot == null || hiddenLayer < 0) return;
            foreach (Renderer renderer in visualRoot.GetComponentsInChildren<Renderer>(true))
                renderer.gameObject.layer = hiddenLayer;
        }

        private void ExcludeFromPlayer(Camera camera)
        {
            if (camera == null || hiddenMask == 0) return;
            camera.cullingMask &= ~hiddenMask;
        }
    }
}
