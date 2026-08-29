using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    // Rate steering keeps the operator facing the monitor while allowing repeated
    // full turns. Eye calibration coordinates are NOT reused as head orientation.
    public sealed class WebcamHeadTrackingProvider : ILookInputProvider
    {
        private readonly IHeadRotationSource source;
        private Quaternion neutral;
        private bool centered;
        public Vector2 Angles { get; private set; }
        public float Deadzone = 4, FullTurnAngle = 25, DegreesPerSecond = 100;
        public float MinimumPitch = -80, MaximumPitch = 80;
        public WebcamHeadTrackingProvider(IHeadRotationSource source) => this.source = source;
        public void Seed(Vector2 angles) { Angles = angles; centered = false; }
        public void CenterHead() { if (source != null && source.HasFreshHeadRotation) { neutral = source.HeadRotation; centered = true; } }
        public bool TryGetRotation(float deltaTime, out Quaternion rotation)
        {
            rotation = Quaternion.Euler(Angles.y, Angles.x, 0);
            if (source == null || !source.HasFreshHeadRotation) return false;
            if (!centered) CenterHead();
            Vector3 forward = (Quaternion.Inverse(neutral) * source.HeadRotation) * Vector3.forward;
            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(forward.y, new Vector2(forward.x, forward.z).magnitude) * Mathf.Rad2Deg;
            var rate = new Vector2(TurnRate(yaw), TurnRate(pitch));
            Vector2 next = Angles + rate * Mathf.Clamp(deltaTime, 0, .1f);
            next.y = Mathf.Clamp(next.y, MinimumPitch, MaximumPitch);
            if (Mathf.Abs(next.x) > 720) next.x -= Mathf.Floor(next.x / 360) * 360;
            Angles = next; rotation = Quaternion.Euler(next.y, next.x, 0); return true;
        }
        private float TurnRate(float angle) => Mathf.Sign(angle) * Mathf.Clamp01((Mathf.Abs(angle) - Deadzone) / Mathf.Max(1, FullTurnAngle - Deadzone)) * DegreesPerSecond;
        public static bool TryConvertFaceMatrix(Matrix4x4 matrix, out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            // MediaPipe metric space is right-handed, looking down -Z. S R S,
            // S=diag(1,1,-1), converts the rotation into Unity left-handed space.
            Vector3 forward = new Vector3(-matrix.m02, -matrix.m12, matrix.m22);
            Vector3 up = new Vector3(matrix.m01, matrix.m11, -matrix.m21);
            if (!Finite(forward) || !Finite(up) || forward.sqrMagnitude < .01f || Vector3.Cross(forward, up).sqrMagnitude < .001f) return false;
            rotation = Quaternion.LookRotation(forward, up); return true;
        }
        private static bool Finite(Vector3 v) => WebcamCalibrationMap.Finite(new Vector2(v.x, v.y)) && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
    }
}
