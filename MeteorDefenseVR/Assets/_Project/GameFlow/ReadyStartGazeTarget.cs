using MeteorDefenseVR.EyeTracking;
using UnityEngine;

namespace MeteorDefenseVR.GameFlow
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class ReadyStartGazeTarget : MonoBehaviour, IGazeTarget
    {
        [SerializeField] private ReadyScreenController controller;
        [SerializeField, Min(0.2f)] private float dwellDuration = 0.8f;
        private float progress;

        public float Progress => progress;

        public void Configure(ReadyScreenController readyController, float duration = 0.8f)
        {
            controller = readyController;
            dwellDuration = Mathf.Max(0.2f, duration);
            progress = 0f;
        }

        public void OnGazeEnter(RaycastHit _) { }
        public void OnGazeStay(RaycastHit _, float deltaTime) => Advance(deltaTime);
        public void OnGazeExit() => progress = 0f;

        public void Advance(float deltaTime)
        {
            if (controller == null || !controller.IsReadyVisible || deltaTime <= 0f) return;
            progress = Mathf.Clamp01(progress + deltaTime / dwellDuration);
            if (progress < 1f) return;
            progress = 0f;
            controller.StartExperience();
        }
    }
}
