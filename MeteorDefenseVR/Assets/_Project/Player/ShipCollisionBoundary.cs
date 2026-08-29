using UnityEngine;

namespace MeteorDefenseVR.Player
{
    // Ship-fixed plane, not a physics trigger: transform-driven meteors need swept tests.
    [DisallowMultipleComponent]
    public sealed class ShipCollisionBoundary : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float clearance = .12f;
        public float Clearance => clearance;
        [SerializeField] private bool spherical;
        [SerializeField, Min(1)] private float shipRadius = 4.2f;
        public bool IsSpherical => spherical;
        public float ShipRadius => shipRadius;
        public void ConfigureSphere(float radius) { spherical = true; shipRadius = Mathf.Clamp(radius, 2, 10); }

        public bool Sweep(Vector3 from, Vector3 to, float radius, out Vector3 safeCenter, out Vector3 impact)
        {
            if (spherical) return SweepSphere(from, to, radius, out safeCenter, out impact);
            Vector3 normal = transform.forward;
            float margin = Mathf.Max(0f, radius) + clearance;
            float start = Vector3.Dot(from - transform.position, normal) - margin;
            float end = Vector3.Dot(to - transform.position, normal) - margin;
            safeCenter = to;
            impact = to;
            if (start > 0f && end > 0f) return false;
            float fraction = start > 0f ? Mathf.Clamp01(start / (start - end)) : 0f;
            safeCenter = Vector3.Lerp(from, to, fraction);
            // Also repairs an already-overlapping spawn without displaying it inside the ship.
            safeCenter += normal * (margin - Vector3.Dot(safeCenter - transform.position, normal));
            impact = safeCenter - normal * margin;
            return true;
        }

        private bool SweepSphere(Vector3 from, Vector3 to, float radius, out Vector3 safe, out Vector3 impact)
        {
            float expanded = shipRadius + Mathf.Max(0, radius) + clearance;
            Vector3 start = from - transform.position, step = to - from;
            safe = to; impact = to;
            float c = start.sqrMagnitude - expanded * expanded;
            float t = 0;
            if (c > 0)
            {
                float a = step.sqrMagnitude, b = Vector3.Dot(start, step);
                float discriminant = b * b - a * c;
                if (a < .000001f || b >= 0 || discriminant < 0) return false;
                t = (-b - Mathf.Sqrt(discriminant)) / a;
                if (t < 0 || t > 1) return false;
            }
            Vector3 normal = start + step * t;
            if (normal.sqrMagnitude < .000001f) normal = -step;
            if (normal.sqrMagnitude < .000001f) normal = transform.forward;
            normal.Normalize();
            safe = transform.position + normal * expanded;
            impact = transform.position + normal * shipRadius;
            return true;
        }

        private void OnValidate() => clearance = Mathf.Clamp(clearance, .01f, 1f);
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, .35f, .1f, .6f);
            Gizmos.matrix = transform.localToWorldMatrix;
            if (spherical) { Gizmos.DrawWireSphere(Vector3.zero, shipRadius); return; }
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(6f, 4f, .02f));
            Gizmos.DrawRay(Vector3.zero, Vector3.forward);
        }
    }
}
