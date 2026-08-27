using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace MeteorDefenseVR.EyeTracking
{
    [DisallowMultipleComponent]
    public sealed class VREyeGazeProvider : MonoBehaviour, IGazeProvider
    {
        [SerializeField] private Transform trackingOrigin;
        [SerializeField] private Camera headCamera;
        [SerializeField] private bool allowHeadGazeFallback = true;

        private readonly List<InputDevice> eyeTrackingDevices = new List<InputDevice>();
        private InputDevice activeDevice;
        private Ray latestRay;
        private bool hasEyeRay;
        private bool forceHeadGaze;

        public bool IsTrackingValid => (!forceHeadGaze && hasEyeRay) || (allowHeadGazeFallback && ResolveCamera() != null);
        public bool IsUsingEyeTracking => !forceHeadGaze && hasEyeRay;

        private void OnEnable()
        {
            InputDevices.deviceConnected += OnDeviceConnected;
            InputDevices.deviceDisconnected += OnDeviceDisconnected;
            FindEyeTrackingDevice();
        }

        private void OnDisable()
        {
            InputDevices.deviceConnected -= OnDeviceConnected;
            InputDevices.deviceDisconnected -= OnDeviceDisconnected;
            activeDevice = default;
            hasEyeRay = false;
        }

        private void Update() => UpdateEyeRay();

        public Ray GetGazeRay()
        {
            if (!forceHeadGaze && hasEyeRay) return latestRay;
            Camera camera = ResolveCamera();
            return camera != null
                ? new Ray(camera.transform.position, camera.transform.forward)
                : new Ray(transform.position, transform.forward);
        }

        public Vector3 GetGazeOrigin() => GetGazeRay().origin;
        public Vector3 GetGazeDirection() => GetGazeRay().direction;

        public void ForceHeadGazeMode()
        {
            forceHeadGaze = true;
            allowHeadGazeFallback = true;
        }

        public void RetryEyeTracking()
        {
            forceHeadGaze = false;
            FindEyeTrackingDevice();
        }

        private void UpdateEyeRay()
        {
            if (!activeDevice.isValid) FindEyeTrackingDevice();
            hasEyeRay = TryReadEyes(activeDevice, out latestRay);
        }

        private bool TryReadEyes(InputDevice device, out Ray ray)
        {
            ray = default;
            if (!device.isValid || !device.TryGetFeatureValue(CommonUsages.eyesData, out Eyes eyes)) return false;

            bool hasPosition = eyes.TryGetFixationPoint(out Vector3 fixationPoint);
            bool hasLeftPosition = eyes.TryGetLeftEyePosition(out Vector3 leftPosition);
            bool hasRightPosition = eyes.TryGetRightEyePosition(out Vector3 rightPosition);
            if (!hasPosition || (!hasLeftPosition && !hasRightPosition)) return false;

            Vector3 localOrigin = hasLeftPosition && hasRightPosition
                ? (leftPosition + rightPosition) * 0.5f
                : hasLeftPosition ? leftPosition : rightPosition;
            Transform origin = trackingOrigin;
            Vector3 worldOrigin = origin != null ? origin.TransformPoint(localOrigin) : localOrigin;
            Vector3 worldFixation = origin != null ? origin.TransformPoint(fixationPoint) : fixationPoint;
            Vector3 direction = worldFixation - worldOrigin;
            if (direction.sqrMagnitude < 0.000001f) return false;

            ray = new Ray(worldOrigin, direction.normalized);
            return true;
        }

        private void FindEyeTrackingDevice()
        {
            eyeTrackingDevices.Clear();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking, eyeTrackingDevices);
            activeDevice = eyeTrackingDevices.Count > 0 ? eyeTrackingDevices[0] : default;
        }

        private void OnDeviceConnected(InputDevice device)
        {
            if ((device.characteristics & InputDeviceCharacteristics.EyeTracking) != 0) activeDevice = device;
        }

        private void OnDeviceDisconnected(InputDevice device)
        {
            if (activeDevice == device) activeDevice = default;
        }

        private Camera ResolveCamera()
        {
            if (headCamera == null) headCamera = Camera.main;
            return headCamera;
        }
    }
}
