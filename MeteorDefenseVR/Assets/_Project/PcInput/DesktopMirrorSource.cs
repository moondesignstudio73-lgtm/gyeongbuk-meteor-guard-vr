using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace MeteorDefenseVR.PcInput
{
    // Output adapter only: never starts/stops XR, tracking, cameras, or a network/casting service.
    public interface IDesktopMirrorSource : IDisposable
    {
        bool IsRunning { get; }
        void Refresh();
    }

    public sealed class DesktopMirrorSource : IDesktopMirrorSource
    {
        private readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        private XRDisplaySubsystem current;
        private int previousMode, appliedMode;
        public bool IsRunning => current != null && current.running;

        public void Refresh()
        {
            displays.Clear();
            SubsystemManager.GetSubsystems(displays);
            XRDisplaySubsystem next = null;
            foreach (var display in displays) if (display.running) { next = display; break; }
            if (next == current) return;
            Restore();
            current = next;
            if (current == null) return;
            previousMode = current.GetPreferredMirrorBlitMode();
            appliedMode = XRMirrorViewBlitMode.Default;
            var descriptor = current.subsystemDescriptor;
            if (descriptor != null)
                for (int i = 0; i < descriptor.GetAvailableMirrorBlitModeCount(); i++)
                {
                    descriptor.GetMirrorBlitModeByIndex(i, out var mode);
                    if (mode.blitMode == XRMirrorViewBlitMode.LeftEye) { appliedMode = mode.blitMode; break; }
                }
            // URP blits the existing XR image. No second scene render and no eye texture readback.
            current.SetPreferredMirrorBlitMode(appliedMode);
        }

        private void Restore()
        {
            if (current != null && current.running && current.GetPreferredMirrorBlitMode() == appliedMode)
                current.SetPreferredMirrorBlitMode(previousMode);
            current = null;
        }
        public void Dispose() { Restore(); displays.Clear(); }
    }
}
