using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    // Shared by menu targets for every gaze provider; never delays combat targets.
    public sealed class GazeMenuSelectionGate
    {
        private static float lastConfirmedAt = -10;
        private static Vector3 lastConfirmedPosition;
        private float availableAt;
        private float rearmedConfirmationAt = -10;
        private bool requiresExit;
        public bool IsOpen => Time.unscaledTime >= availableAt && !requiresExit;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { lastConfirmedAt = -10; }
        public void OpenScreen() { availableAt = Time.unscaledTime + .4f; requiresExit = false; rearmedConfirmationAt = -10; }
        public void Enter(Vector3 position)
        {
            var camera = Camera.main;
            if (camera == null || lastConfirmedAt <= rearmedConfirmationAt || Time.unscaledTime - lastConfirmedAt > 2f) return;
            requiresExit |= Vector3.Angle(position - camera.transform.position, lastConfirmedPosition - camera.transform.position) < 5f;
        }
        public void Exit()
        {
            // A modal/tracking loss clears the raycaster target without the player looking away.
            // Keep an already latched transition guard through that interruption, even past 2s.
            var caster = Object.FindAnyObjectByType<MeteorDefenseVR.EyeTracking.GazeRaycaster>();
            if (caster != null && caster.Provider != null && !caster.Provider.IsTrackingValid) return;
            if (requiresExit) rearmedConfirmationAt = lastConfirmedAt;
            requiresExit = false;
        }
        public void Confirm(Vector3 position) { lastConfirmedAt = Time.unscaledTime; lastConfirmedPosition = position; }
    }
}
