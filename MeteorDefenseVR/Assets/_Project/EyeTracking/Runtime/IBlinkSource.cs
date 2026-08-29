namespace MeteorDefenseVR.EyeTracking
{
    public interface IBlinkSource
    {
        bool HasReliableBlink { get; }
        bool AreEyesClosed { get; }
    }

    public static class BlinkTriggerPolicy
    {
        public static bool ShouldFire(ref bool armed, bool reliableBlink, bool eyesClosed, bool mousePressed, bool cooldownReady)
        {
            if (!cooldownReady) return false;
            if (mousePressed) return true;
            if (!reliableBlink) return false;
            if (!eyesClosed) { armed = true; return false; }
            if (!armed) return false;
            armed = false;
            return true;
        }
    }
}
