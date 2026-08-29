using MeteorDefenseVR.Meteor;
using UnityEngine;

namespace MeteorDefenseVR.Visual
{
    public enum ThreatRadarRisk { Safe, Approaching, Danger, Imminent }

    /// <summary>Allocation-free shared threat maths used by the cockpit radar and edge warnings.</summary>
    public static class ThreatRadarMath
    {
        public static float TimeToCollision(Vector3 shipCenter, MeteorController meteor, float boundaryRadius)
        {
            if (meteor == null) return float.PositiveInfinity;
            Vector3 relative = meteor.transform.position - shipCenter;
            float distance = relative.magnitude;
            if (distance <= .001f) return 0f;
            float closingSpeed = -Vector3.Dot(relative / distance, meteor.Velocity);
            if (closingSpeed <= .001f) return float.PositiveInfinity;
            return Mathf.Max(0f, distance - boundaryRadius - meteor.CollisionRadius) / closingSpeed;
        }

        public static float DistanceRadius(float distance, float maximumRange)
        {
            // Square-root spacing keeps close threats readable while preserving exact ring distances.
            return Mathf.Sqrt(Mathf.Clamp01(distance / Mathf.Max(1f, maximumRange)));
        }

        public static Vector2 Project(Vector3 worldPosition, Vector3 shipCenter, Quaternion shipRotation,
            float horizontalRadius, float verticalRadius, float maximumRange)
        {
            Vector3 local = Quaternion.Inverse(shipRotation) * (worldPosition - shipCenter);
            Vector2 planar = new Vector2(local.x, local.z);
            if (planar.sqrMagnitude < .000001f) return Vector2.zero;
            float radius = DistanceRadius((worldPosition - shipCenter).magnitude, maximumRange);
            planar.Normalize();
            return new Vector2(planar.x * horizontalRadius * radius, planar.y * verticalRadius * radius);
        }

        public static ThreatRadarRisk Risk(float timeToCollision, float distance)
        {
            if (timeToCollision <= 3f || distance <= 10f) return ThreatRadarRisk.Imminent;
            if (timeToCollision <= 7f || distance <= 20f) return ThreatRadarRisk.Danger;
            if (timeToCollision <= 13f || distance <= 34f) return ThreatRadarRisk.Approaching;
            return ThreatRadarRisk.Safe;
        }

        public static bool IsInView(Camera camera, Vector3 worldPosition, float margin = .04f)
        {
            if (camera == null) return false;
            Vector3 viewport = camera.WorldToViewportPoint(worldPosition);
            return viewport.z > 0f && viewport.x > margin && viewport.x < 1f - margin &&
                   viewport.y > margin && viewport.y < 1f - margin;
        }

        public static float LookYaw(Quaternion shipRotation, Vector3 worldForward)
        {
            Vector3 local = Quaternion.Inverse(shipRotation) * worldForward;
            return Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        }

        public static float LookPitch(Quaternion shipRotation, Vector3 worldForward)
        {
            Vector3 local = Quaternion.Inverse(shipRotation) * worldForward;
            return Mathf.Asin(Mathf.Clamp(local.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        public static int DisplayLimit(MeteorDefenseVR.Difficulty.DifficultyLevel level)
            => level == MeteorDefenseVR.Difficulty.DifficultyLevel.Experience ? 3 :
               level == MeteorDefenseVR.Difficulty.DifficultyLevel.Easy ? 6 :
               level == MeteorDefenseVR.Difficulty.DifficultyLevel.Normal ? 10 : 16;
    }
}
