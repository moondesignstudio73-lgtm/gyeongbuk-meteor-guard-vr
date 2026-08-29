using System;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    // QA-only explicit render: a hidden Windows player otherwise skips automatic camera rendering.
    [DefaultExecutionOrder(1500)]
    public sealed class PcStabilityRenderDriver : MonoBehaviour
    {
        public Camera GameCamera;
        public event Action RenderCompleted;
        private long previousAllocation;
        public long LastFrameMainThreadAllocation { get; private set; }
        private void Awake() => previousAllocation = GC.GetAllocatedBytesForCurrentThread();
        private void LateUpdate()
        {
            if (GameCamera != null && GameCamera.targetTexture != null)
            {
                GameCamera.Render();
                RenderCompleted?.Invoke();
            }
            long current = GC.GetAllocatedBytesForCurrentThread();
            LastFrameMainThreadAllocation = Math.Max(0, current - previousAllocation);
            previousAllocation = current;
        }
    }
}
