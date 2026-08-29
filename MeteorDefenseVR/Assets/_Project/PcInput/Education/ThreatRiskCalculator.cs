using System;
using UnityEngine;

namespace MeteorDefenseVR.Education
{
    [Serializable] public sealed class ThreatRiskSettings
    {
        [Min(.1f)] public float horizonSeconds = 90;
        [Range(0, 1)] public float timeWeight = .85f;
        [Range(0, 1)] public float impactWeight = .15f;
    }
    public readonly struct ThreatRisk
    {
        public readonly float Distance, RelativeSpeed, ClosingSpeed, Ttc, Danger;
        public readonly bool CanCollide;
        public readonly Vector3 ApproachDirection;
        public ThreatRisk(float distance, float speed, float closing, float ttc, float danger, bool collision, Vector3 direction)
        { Distance = distance; RelativeSpeed = speed; ClosingSpeed = closing; Ttc = ttc; Danger = danger; CanCollide = collision; ApproachDirection = direction; }
    }
    public static class ThreatRiskCalculator
    {
        // Solve |relativePosition + relativeVelocity*t|² = combinedRadius².
        // Distance/speed alone incorrectly labels passing and receding objects as collision threats.
        public static ThreatRisk Evaluate(Vector3 relativePosition, Vector3 relativeVelocity, float combinedRadius, float impact, ThreatRiskSettings settings)
        {
            float distance = relativePosition.magnitude, speed = relativeVelocity.magnitude;
            float closing = distance > .001f ? -Vector3.Dot(relativePosition / distance, relativeVelocity) : 0;
            float c = relativePosition.sqrMagnitude - combinedRadius * combinedRadius;
            float a = relativeVelocity.sqrMagnitude, b = Vector3.Dot(relativePosition, relativeVelocity);
            float ttc = -1;
            if (!Finite(distance) || !Finite(speed) || !Finite(combinedRadius) || combinedRadius < 0) return new ThreatRisk(0, 0, 0, -1, 0, false, Vector3.zero);
            if (c <= 0) ttc = 0;
            else if (a > .000001f && b < 0)
            {
                float discriminant = b * b - a * c;
                if (discriminant >= 0) ttc = Mathf.Max(0, (-b - Mathf.Sqrt(discriminant)) / a);
            }
            settings ??= new ThreatRiskSettings();
            float timeUrgency = ttc >= 0 ? 1f / (1 + ttc) : 0;
            float danger = ttc >= 0 && ttc <= settings.horizonSeconds
                ? timeUrgency * (Mathf.Max(0, settings.timeWeight) + Mathf.Max(0, settings.impactWeight) * Mathf.Clamp01(impact / 40f)) : 0;
            return new ThreatRisk(distance, speed, closing, ttc, danger, ttc >= 0, relativePosition.normalized);
        }
        public static ThreatDirection Direction(Vector3 shipLocal)
        {
            if (Mathf.Abs(shipLocal.y) > Mathf.Max(Mathf.Abs(shipLocal.x), Mathf.Abs(shipLocal.z))) return shipLocal.y > 0 ? ThreatDirection.Above : ThreatDirection.Below;
            if (Mathf.Abs(shipLocal.x) > Mathf.Abs(shipLocal.z)) return shipLocal.x > 0 ? ThreatDirection.Right : ThreatDirection.Left;
            return shipLocal.z >= 0 ? ThreatDirection.Front : ThreatDirection.Rear;
        }
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
