using System.Collections.Generic;
using TMPro;
using UnityEngine;
using MeteorDefenseVR.Visual;

namespace MeteorDefenseVR.PcInput
{
    // Hide presentation only: gameplay controllers, active states and timers remain intact.
    [DefaultExecutionOrder(1400)]
    public sealed class PausePresentationGate : MonoBehaviour
    {
        private WindowsVRSimulation simulation;
        private Canvas controls;
        private PcTestOverlay overlay;
        private readonly Dictionary<Renderer, bool> renderers = new();
        private readonly Dictionary<CanvasGroup, float> groups = new();
        private readonly HashSet<CanvasGroup> ownedGroups = new();
        public bool IsSuspended { get; private set; }
        public void Configure(WindowsVRSimulation look, Canvas playerControls)
        { simulation = look; controls = playerControls; overlay = playerControls.GetComponentInParent<PcTestOverlay>(); }
        private void LateUpdate() => Apply();
        public void Apply()
        {
            bool paused = simulation != null && simulation.IsPaused || overlay != null && overlay.LookSettingsOpen;
            if (paused && !IsSuspended)
            {
                var safe = FindAnyObjectByType<WorldUiSafeArea>();
                if (safe != null && safe.UiRoots != null)
                    foreach (var root in safe.UiRoots)
                        if (root != null) foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) Remember(renderer);
                foreach (var target in FindObjectsByType<MeteorDefenseVR.Combat.GazeLockOnTarget>(FindObjectsInactive.Include))
                    foreach (var renderer in target.GetComponentsInChildren<Renderer>(true)) Remember(renderer);
                // Includes transient outgoing text and dynamically created world menus.
                foreach (var text in FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include))
                    if (text.TryGetComponent<Renderer>(out var renderer)) Remember(renderer);
                foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
                {
                    if (canvas == controls || canvas.transform.IsChildOf(controls.transform)) continue;
                    var group = canvas.GetComponent<CanvasGroup>();
                    if (group == null) { group = canvas.gameObject.AddComponent<CanvasGroup>(); ownedGroups.Add(group); }
                    groups[group] = group.alpha;
                }
            }
            if (paused)
            {
                foreach (var pair in renderers) if (pair.Key != null) pair.Key.forceRenderingOff = true;
                foreach (var pair in groups) if (pair.Key != null) pair.Key.alpha = 0;
            }
            else Restore();
            IsSuspended = paused;
        }
        private void Remember(Renderer renderer)
        { if (!renderers.ContainsKey(renderer)) renderers.Add(renderer, renderer.forceRenderingOff); }
        private void Restore()
        {
            foreach (var pair in renderers) if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
            renderers.Clear();
            foreach (var pair in groups) if (pair.Key != null) pair.Key.alpha = pair.Value;
            groups.Clear();
            IsSuspended = false;
        }
        private void OnDisable() => Restore();
        private void OnDestroy()
        { Restore(); foreach (var group in ownedGroups) if (group != null) Destroy(group); }
    }
}
