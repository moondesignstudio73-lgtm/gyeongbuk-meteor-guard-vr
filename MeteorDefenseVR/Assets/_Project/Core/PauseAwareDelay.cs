using UnityEngine;

namespace MeteorDefenseVR.Core
{
    // Preserve real-time presentation durations, but do not advance during an explicit pause.
    public sealed class PauseAwareDelay : CustomYieldInstruction
    {
        private float remaining;
        private int lastFrame = -1;
        public PauseAwareDelay(float seconds) => remaining = Mathf.Max(0, seconds);
        public override bool keepWaiting
        {
            get
            {
                if (lastFrame != Time.frameCount)
                {
                    lastFrame = Time.frameCount;
                    if (Time.timeScale > 0) remaining -= Time.unscaledDeltaTime;
                }
                return remaining > 0;
            }
        }
    }
}
