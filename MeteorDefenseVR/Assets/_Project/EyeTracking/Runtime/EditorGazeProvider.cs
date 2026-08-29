using UnityEngine;
using UnityEngine.InputSystem;

namespace MeteorDefenseVR.EyeTracking
{
    public enum EditorGazeMode
    {
        MousePosition,
        CameraCenter
    }

    [DisallowMultipleComponent]
    public sealed class EditorGazeProvider : MonoBehaviour, IGazeProvider
    {
        [SerializeField] private Camera gazeCamera;
        [SerializeField] private EditorGazeMode mode = EditorGazeMode.MousePosition;

        public bool IsTrackingValid => ResolveCamera() != null;
        public EditorGazeMode Mode { get => mode; set => mode = value; }

        public Ray GetGazeRay()
        {
            Camera camera = ResolveCamera();
            if (camera == null) return new Ray(transform.position, transform.forward);

            if (mode == EditorGazeMode.MousePosition && Mouse.current != null)
                return camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            return camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }

        public Vector3 GetGazeOrigin() => GetGazeRay().origin;
        public Vector3 GetGazeDirection() => GetGazeRay().direction;

        public void SetCamera(Camera camera) => gazeCamera = camera;

        private Camera ResolveCamera()
        {
            if (gazeCamera == null) gazeCamera = Camera.main;
            return gazeCamera;
        }
    }
}
