using UnityEngine;
using System.Collections.Generic;
using MeteorDefenseVR.Combat;

namespace MeteorDefenseVR.EyeTracking
{
    [DisallowMultipleComponent]
    public sealed class GazeRaycaster : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour gazeProviderComponent;
        [SerializeField] private LayerMask targetLayers = ~0;
        [SerializeField, Min(0.1f)] private float maximumDistance = 100f;
        [SerializeField, Range(0f, 0.5f)] private float gazeMagnetismRadius = 0.15f;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;
        [SerializeField] private bool drawDebugRay = true;
        [SerializeField] private Color validRayColor = Color.cyan;
        [SerializeField] private Color hitRayColor = Color.green;

        private IGazeProvider gazeProvider;
        private IGazeTarget currentTarget;
        private readonly Dictionary<Collider, IGazeTarget> targetCache = new Dictionary<Collider, IGazeTarget>();
        private static readonly HashSet<GazeRaycaster> activeRaycasters = new HashSet<GazeRaycaster>();

        public IGazeProvider Provider => gazeProvider;
        public IGazeTarget CurrentTarget => currentTarget;
        public bool HasHit { get; private set; }
        public RaycastHit CurrentHit { get; private set; }
        public float MaximumDistance { get => maximumDistance; set => maximumDistance = Mathf.Clamp(value, .1f, 1000); }
        public float GazeMagnetismRadius { get => gazeMagnetismRadius; set => gazeMagnetismRadius = Mathf.Clamp(value, 0f, 0.5f); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => activeRaycasters.Clear();

        private void Awake() { activeRaycasters.Add(this); ResolveProvider(); }

        private void OnEnable()
        {
            activeRaycasters.Add(this);
            GazeLockOnTarget.GlobalReleased += HandleTargetReleased;
        }

        private void OnValidate()
        {
            if (gazeProviderComponent != null && !(gazeProviderComponent is IGazeProvider))
                gazeProviderComponent = null;
        }

        private void Update() => Tick(Time.deltaTime);

        private void OnDisable()
        {
            GazeLockOnTarget.GlobalReleased -= HandleTargetReleased;
            activeRaycasters.Remove(this);
            ClearCurrentTarget();
        }

        public void SetProvider(IGazeProvider provider)
        {
            activeRaycasters.Add(this);
            ClearCurrentTarget();
            gazeProvider = provider;
            gazeProviderComponent = provider as MonoBehaviour;
        }

        public bool ReleaseTarget(IGazeTarget target)
        {
            if (ReferenceEquals(target, null) || !ReferenceEquals(currentTarget, target)) return false;
            if (TargetExists(currentTarget)) currentTarget.OnGazeExit();
            currentTarget = null;
            HasHit = false;
            CurrentHit = default;
            targetCache.Clear();
            return true;
        }

        public static void ReleaseGlobally(IGazeTarget target)
        {
            if (ReferenceEquals(target, null)) return;
            foreach (GazeRaycaster raycaster in activeRaycasters)
                if (raycaster != null) raycaster.ReleaseTarget(target);
        }

        public void Tick(float deltaTime)
        {
            // Interfaces bypass UnityEngine.Object's destroyed-object null operator.
            if (!ReferenceEquals(currentTarget, null) && !TargetExists(currentTarget))
            { currentTarget = null; targetCache.Clear(); }
            if (gazeProvider == null) ResolveProvider();
            if (gazeProvider == null || !gazeProvider.IsTrackingValid)
            {
                HasHit = false;
                ClearCurrentTarget();
                return;
            }

            Ray ray = gazeProvider.GetGazeRay();
            HasHit = Physics.Raycast(ray, out RaycastHit hit, maximumDistance, targetLayers, triggerInteraction);
            if (!HasHit && gazeMagnetismRadius > 0f)
                HasHit = Physics.SphereCast(ray, gazeMagnetismRadius, out hit, maximumDistance, targetLayers, triggerInteraction);
            CurrentHit = hit;
            IGazeTarget nextTarget = HasHit ? FindTargetCached(hit.collider) : null;

            if (!ReferenceEquals(nextTarget, currentTarget))
            {
                if (TargetExists(currentTarget)) currentTarget.OnGazeExit();
                currentTarget = nextTarget;
                currentTarget?.OnGazeEnter(hit);
            }

            currentTarget?.OnGazeStay(hit, deltaTime);

            if (drawDebugRay)
            {
                float length = HasHit ? hit.distance : maximumDistance;
                Debug.DrawRay(ray.origin, ray.direction * length, HasHit ? hitRayColor : validRayColor);
            }
        }

        private void ResolveProvider()
        {
            gazeProvider = gazeProviderComponent as IGazeProvider;
            if (gazeProvider != null) return;

            MonoBehaviour[] candidates = GetComponents<MonoBehaviour>();
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] is IGazeProvider provider)
                {
                    SetProvider(provider);
                    return;
                }
            }
        }

        private IGazeTarget FindTargetCached(Collider collider)
        {
            if (collider == null) return null;
            if (targetCache.TryGetValue(collider, out IGazeTarget cached))
            {
                // Preserve negative caching for cockpit/environment colliders as well.
                if (ReferenceEquals(cached, null)) return null;
                if (TargetExists(cached)) return TargetEnabled(cached) ? cached : null;
            }
            MonoBehaviour[] behaviours = collider.GetComponentsInParent<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (!(behaviours[i] is IGazeTarget target)) continue;
                targetCache[collider] = target;
                return TargetEnabled(target) ? target : null;
            }
            targetCache[collider] = null;
            return null;
        }

        private void ClearCurrentTarget()
        {
            if (TargetExists(currentTarget)) currentTarget.OnGazeExit();
            currentTarget = null;
            HasHit = false; CurrentHit = default;
            targetCache.Clear();
        }

        private void HandleTargetReleased(GazeLockOnTarget target) => ReleaseTarget(target);

        private static bool TargetExists(IGazeTarget target)
            => target != null && (!(target is Object unityObject) || unityObject != null);
        private static bool TargetEnabled(IGazeTarget target)
            => TargetExists(target) && (!(target is Behaviour behaviour) || behaviour.isActiveAndEnabled);
    }
}
