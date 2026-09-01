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
        public bool AllocationCounterAvailable { get; private set; }
        public long LastFrameMainThreadAllocation { get; private set; }
        private void Awake()
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            byte[] proof = new byte[64 * 1024];
            proof[0] = 1; proof[proof.Length - 1] = 1;
            long after = GC.GetAllocatedBytesForCurrentThread();
            AllocationCounterAvailable = after > before;
            previousAllocation = after;
            GC.KeepAlive(proof);
        }
        private void LateUpdate()
        {
            if (GameCamera != null && GameCamera.targetTexture != null)
            {
                GameCamera.Render();
                RenderCompleted?.Invoke();
            }
            if (AllocationCounterAvailable)
            {
                long current = GC.GetAllocatedBytesForCurrentThread();
                LastFrameMainThreadAllocation = Math.Max(0, current - previousAllocation);
                previousAllocation = current;
            }
            else LastFrameMainThreadAllocation = -1;
        }
    }
}
