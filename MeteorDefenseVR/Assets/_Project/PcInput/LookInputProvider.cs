using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    // Providers return a local look rotation. Only WindowsVRSimulation (the shared
    // controller, retained to preserve scene/launcher references) may apply it.
    public interface ILookInputProvider
    {
        bool TryGetRotation(float deltaTime, out Quaternion rotation);
    }

    public interface IHeadRotationSource
    {
        bool HasFreshHeadRotation { get; }
        Quaternion HeadRotation { get; }
    }

    public enum CameraShakeLevel { Off, Weak, Strong }

    public static class LookInputPolicy
    {
        public static bool HasRequiredWebcamTracking(bool headSteering, bool headFresh, bool eyeFresh)
            => headSteering ? headFresh : eyeFresh;

        public static bool GameplayLook(MeteorDefenseVR.Core.GameState state) =>
            state == MeteorDefenseVR.Core.GameState.Intro || state == MeteorDefenseVR.Core.GameState.MissionBriefing ||
            state == MeteorDefenseVR.Core.GameState.Tutorial || state == MeteorDefenseVR.Core.GameState.Practice ||
            state == MeteorDefenseVR.Core.GameState.Launch || state == MeteorDefenseVR.Core.GameState.Countdown ||
            state == MeteorDefenseVR.Core.GameState.Playing || state == MeteorDefenseVR.Core.GameState.BossMeteor ||
            state == MeteorDefenseVR.Core.GameState.MissionComplete;

        public static float ShakeDegrees(CameraShakeLevel level, bool hmd) => hmd ? 0 : level == CameraShakeLevel.Strong ? .12f : level == CameraShakeLevel.Weak ? .04f : 0;
    }
}
