using UnityEngine;

namespace MeteorDefenseVR.EyeTracking
{
    public interface IGazeProvider
    {
        bool IsTrackingValid { get; }
        Ray GetGazeRay();
        Vector3 GetGazeOrigin();
        Vector3 GetGazeDirection();
    }
}
