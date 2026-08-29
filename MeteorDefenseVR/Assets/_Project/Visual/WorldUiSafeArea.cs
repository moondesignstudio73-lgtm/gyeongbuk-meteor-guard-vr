using UnityEngine;

namespace MeteorDefenseVR.Visual
{
    // Keep world-space depth and VR angular placement. Fit XY only for narrow desktop windows.
    [DefaultExecutionOrder(950), DisallowMultipleComponent]
    public sealed class WorldUiSafeArea : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform[] roots;
        private Vector3[] positions, scales;
        private Quaternion[] rotations;
        private Vector3 referencePosition;
        private Quaternion referenceRotation;
        public bool ShipLocked { get; set; }
        public void Configure(Camera camera, Transform[] uiRoots) { viewCamera = camera; roots = uiRoots; }
        private void Awake() => Capture();
        private void LateUpdate() => Apply();
        private void Capture()
        {
            if (roots == null) roots = System.Array.Empty<Transform>();
            positions = new Vector3[roots.Length]; scales = new Vector3[roots.Length];
            rotations = new Quaternion[roots.Length];
            if (viewCamera != null) { referencePosition = viewCamera.transform.position; referenceRotation = viewCamera.transform.rotation; }
            for (int i = 0; i < roots.Length; i++)
                if (roots[i] != null) { positions[i] = viewCamera.transform.InverseTransformPoint(roots[i].position); scales[i] = roots[i].localScale; rotations[i] = roots[i].rotation; }
        }
        public void Apply()
        {
            if (viewCamera == null || roots == null) return;
            if (positions == null) Capture();
            float fit = viewCamera.stereoEnabled ? 1f : Mathf.Min(1f, viewCamera.aspect / 1.6f);
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] == null) continue;
                Vector3 p = positions[i], s = scales[i];
                Vector3 fitted = new Vector3(p.x * fit, p.y * fit, p.z);
                bool anchoredMenu = ShipLocked && roots[i].name == "ReadyScreen";
                roots[i].position = anchoredMenu ? referencePosition + referenceRotation * fitted : viewCamera.transform.TransformPoint(fitted);
                // Menus can be aimed at; read-only combat HUD follows the head and faces the viewer.
                if (ShipLocked) roots[i].rotation = anchoredMenu ? rotations[i] : viewCamera.transform.rotation * Quaternion.Inverse(referenceRotation) * rotations[i];
                else roots[i].rotation = viewCamera.transform.rotation * Quaternion.Inverse(referenceRotation) * rotations[i];
                roots[i].localScale = new Vector3(s.x * fit, s.y * fit, s.z);
            }
        }
    }
}
