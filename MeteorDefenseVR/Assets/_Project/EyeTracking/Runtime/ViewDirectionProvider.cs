using UnityEngine;

namespace MeteorDefenseVR.EyeTracking
{
    public interface IViewDirectionProvider
    {
        Vector3 Position { get; }
        Quaternion Rotation { get; }
        Ray CenterRay { get; }
    }

    // Read-only: observes either the real HMD pose or the desktop simulator pose.
    [DisallowMultipleComponent]
    public sealed class ViewDirectionProvider : MonoBehaviour, IViewDirectionProvider
    {
        [SerializeField] private Camera view;
        public Camera Camera => view;
        public Vector3 Position => view.transform.position;
        public Quaternion Rotation => view.transform.rotation;
        public Ray CenterRay => view.ViewportPointToRay(new Vector3(.5f, .5f));
        // Shared extension point for discovery/dwell; combat retains its existing
        // GazeRaycaster/LockOn target and score pipeline (no duplicate fire logic).
        public float AngleTo(Vector3 worldPosition) => Vector3.Angle(Rotation * Vector3.forward, worldPosition - Position);
        public bool IsWithinCenterCone(Vector3 worldPosition, float degrees = 5) => AngleTo(worldPosition) <= Mathf.Clamp(degrees, 0, 90);
        public bool TryCenterHit(out RaycastHit hit, LayerMask layers, float distance = 100) => Physics.Raycast(CenterRay, out hit, distance, layers, QueryTriggerInteraction.Collide);
        public void Configure(Camera camera) => view = camera;
    }
}
