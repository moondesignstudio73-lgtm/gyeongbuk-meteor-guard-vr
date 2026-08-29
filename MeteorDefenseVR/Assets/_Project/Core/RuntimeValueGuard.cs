using UnityEngine;

namespace MeteorDefenseVR.Core
{
    // Input-boundary protection only; normal tuning values and gameplay rules remain unchanged.
    public static class RuntimeValueGuard
    {
        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static float Clamp(float value, float minimum, float maximum, float fallback)
            => Mathf.Clamp(IsFinite(value) ? value : fallback, minimum, maximum);
        public static float Delta(float value) => IsFinite(value) ? Mathf.Max(0, value) : 0;
        public static int Add(int current, int amount) => (int)System.Math.Min(int.MaxValue, (long)System.Math.Max(0,current) + System.Math.Max(0,amount));
    }
}
