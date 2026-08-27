using UnityEngine;

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

        public IGazeProvider Provider => gazeProvider;
        public IGazeTarget CurrentTarget => currentTarget;
        public bool HasHit { get; private set; }
        public RaycastHit CurrentHit { get; private set; }
        public float GazeMagnetismRadius { get => gazeMagnetismRadius; set => gazeMagnetismRadius = Mathf.Clamp(value, 0f, 0.5f); }

        private void Awake() => ResolveProvider();

        private void OnValidate()
        {
            if (gazeProviderComponent != null && !(gazeProviderComponent is IGazeProvider))
                gazeProviderComponent = null;
        }

        private void Update() => Tick(Time.deltaTime);

        private void OnDisable() => ClearCurrentTarget();

        public void SetProvider(IGazeProvider provider)
        {
            ClearCurrentTarget();
            gazeProvider = provider;
            gazeProviderComponent = provider as MonoBehaviour;
        }

        public void Tick(float deltaTime)
        {
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
            IGazeTarget nextTarget = HasHit ? FindTarget(hit.collider) : null;

            if (!ReferenceEquals(nextTarget, currentTarget))
            {
                currentTarget?.OnGazeExit();
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

        private static IGazeTarget FindTarget(Collider collider)
        {
            if (collider == null) return null;
            MonoBehaviour[] behaviours = collider.GetComponentsInParent<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IGazeTarget target) return target;
            return null;
        }

        private void ClearCurrentTarget()
        {
            currentTarget?.OnGazeExit();
            currentTarget = null;
        }
    }
}
