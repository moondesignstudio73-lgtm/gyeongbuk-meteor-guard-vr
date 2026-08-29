using UnityEngine;
using UnityEngine.XR;

namespace MeteorDefenseVR.PcInput
{
    public sealed class VRHeadLookProvider : ILookInputProvider
    {
        private readonly Transform camera;
        private readonly Behaviour[] owners;
        public bool ExternallyDriven
        {
            get
            {
                foreach (var owner in owners)
                    if (owner != null && owner.isActiveAndEnabled && owner.GetType().Name == "TrackedPoseDriver") return true;
                return false;
            }
        }
        public VRHeadLookProvider(Camera camera)
        { this.camera = camera.transform; owners = camera.GetComponentsInParent<Behaviour>(true); }
        public bool TryGetRotation(float deltaTime, out Quaternion rotation)
        {
            if (ExternallyDriven) { rotation = camera.localRotation; return true; }
            var head = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            return head.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked
                ? head.TryGetFeatureValue(CommonUsages.centerEyeRotation, out rotation) || head.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation)
                : Missing(out rotation);
        }
        private static bool Missing(out Quaternion rotation) { rotation = Quaternion.identity; return false; }
    }
}
