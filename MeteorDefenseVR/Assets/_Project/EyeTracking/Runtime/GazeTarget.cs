using UnityEngine;
using UnityEngine.Events;

namespace MeteorDefenseVR.EyeTracking
{
    [DisallowMultipleComponent]
    public sealed class GazeTarget : MonoBehaviour, IGazeTarget
    {
        [SerializeField] private UnityEvent onGazeEntered;
        [SerializeField] private UnityEvent onGazeStayed;
        [SerializeField] private UnityEvent onGazeExited;

        public bool IsGazedAt { get; private set; }
        public float GazeDuration { get; private set; }

        public void OnGazeEnter(RaycastHit hit)
        {
            IsGazedAt = true;
            GazeDuration = 0f;
            onGazeEntered?.Invoke();
        }

        public void OnGazeStay(RaycastHit hit, float deltaTime)
        {
            if (!IsGazedAt) OnGazeEnter(hit);
            GazeDuration += Mathf.Max(0f, deltaTime);
            onGazeStayed?.Invoke();
        }

        public void OnGazeExit()
        {
            if (!IsGazedAt) return;
            IsGazedAt = false;
            GazeDuration = 0f;
            onGazeExited?.Invoke();
        }
    }
}
