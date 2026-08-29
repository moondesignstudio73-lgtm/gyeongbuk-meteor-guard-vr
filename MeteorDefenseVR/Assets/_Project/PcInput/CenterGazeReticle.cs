using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    [DisallowMultipleComponent]
    public sealed class CenterGazeReticle : MonoBehaviour
    {
        private WindowsVRSimulation controller;
        private ViewDirectionProvider view;
        private RectTransform root;
        private Canvas canvas;
        private GameFlowManager flow;
        public void Configure(WindowsVRSimulation owner, ViewDirectionProvider direction) { controller = owner; view = direction; }
        private void Start()
        {
            if (controller == null || view == null) { enabled = false; return; }
            flow = GameFlowManager.Instance;
            var go = new GameObject("CenterViewPoint", typeof(RectTransform), typeof(Canvas));
            root = (RectTransform)go.transform; root.SetParent(view.Camera.transform, false);
            go.layer = LayerMask.NameToLayer("Player HUD");
            canvas = go.GetComponent<Canvas>(); canvas.worldCamera = view.Camera; canvas.sortingOrder = 26; canvas.planeDistance = 2;
            var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            var point = new GameObject("Point", typeof(RectTransform), typeof(UnityEngine.UI.Image)); point.transform.SetParent(root, false); point.layer = go.layer;
            var rect = (RectTransform)point.transform; rect.anchorMin = rect.anchorMax = Vector2.one * .5f; rect.anchoredPosition = Vector2.zero; rect.sizeDelta = Vector2.one * 3;
            var image = point.GetComponent<UnityEngine.UI.Image>(); image.color = new Color(.35f, .95f, 1, .85f); image.raycastTarget = false;
        }
        private void LateUpdate()
        {
            if (root == null) return;
            bool visible = controller.isActiveAndEnabled && controller.ShowCenterPoint && !controller.IsPaused && flow != null && LookInputPolicy.GameplayLook(flow.CurrentState);
            root.gameObject.SetActive(visible);
            if (!visible) return;
            bool xr = controller.HasRunningHmd(); canvas.renderMode = xr ? RenderMode.WorldSpace : RenderMode.ScreenSpaceCamera;
            if (xr)
            {
                root.localPosition = Vector3.forward * 2; root.localRotation = Quaternion.identity;
                root.sizeDelta = new Vector2(1280, 720); root.localScale = Vector3.one * .003f;
            }
        }
        private void OnDisable() { if (root != null) root.gameObject.SetActive(false); }
        private void OnDestroy() { if (root != null) Destroy(root.gameObject); }
    }
}
