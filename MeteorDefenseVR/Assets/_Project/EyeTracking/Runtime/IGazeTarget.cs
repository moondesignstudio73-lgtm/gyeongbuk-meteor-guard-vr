using UnityEngine;

namespace MeteorDefenseVR.EyeTracking
{
    public interface IGazeTarget
    {
        void OnGazeEnter(RaycastHit hit);
        void OnGazeStay(RaycastHit hit, float deltaTime);
        void OnGazeExit();
    }
}
