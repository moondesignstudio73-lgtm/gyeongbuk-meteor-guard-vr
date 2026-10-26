using UnityEngine;

namespace MeteorDefenseVR.EyeTracking
{
    // Menu actions must be deliberately aimed at, never acquired by combat aim assistance.
    public interface IDirectGazeTarget : IGazeTarget { }

    public interface IGazeTarget
    {
        void OnGazeEnter(RaycastHit hit);
        void OnGazeStay(RaycastHit hit, float deltaTime);
        void OnGazeExit();
    }
}
