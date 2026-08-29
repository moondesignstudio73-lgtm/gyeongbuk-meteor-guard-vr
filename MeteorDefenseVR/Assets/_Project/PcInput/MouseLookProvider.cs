using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace MeteorDefenseVR.PcInput
{
    public sealed class MouseLookProvider : ILookInputProvider
    {
        public Vector2 Angles { get; private set; }
        public float Horizontal = .1f, Vertical = .09f, Sensitivity = 1f;
        public bool InvertY;
        public float MinimumPitch = -80, MaximumPitch = 80;
        private uint consumedUpdate;
        private bool consumed;
        public void Seed(Vector2 angles) => Angles = angles;
        public void AddDelta(Vector2 delta)
        {
            if (!WebcamCalibrationMap.Finite(delta)) return;
            Vector2 next = Angles;
            next.x += delta.x * Horizontal * Mathf.Clamp(Sensitivity, .1f, 2f);
            next.y = Mathf.Clamp(next.y + delta.y * Vertical * Mathf.Clamp(Sensitivity, .1f, 2f) * (InvertY ? 1 : -1), MinimumPitch, MaximumPitch);
            // Rebase, not clamp: unlimited turns without growing float error.
            if (Mathf.Abs(next.x) > 720) next.x -= Mathf.Floor(next.x / 360) * 360;
            Angles = next;
        }
        public bool TryGetRotation(float deltaTime, out Quaternion rotation)
        {
            // A relative sample is a displacement, not a velocity. Consume once
            // even if a manual/test player loop ticks twice for one input update.
            if (!consumed || consumedUpdate != InputState.updateCount)
            {
                consumed = true; consumedUpdate = InputState.updateCount;
                AddDelta(Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero);
            }
            rotation = Quaternion.Euler(Angles.y, Angles.x, 0);
            return true;
        }
    }
}
