using MeteorDefenseVR.EyeTracking;
using UnityEngine;

namespace MeteorDefenseVR.PowerUps
{
    [DisallowMultipleComponent, RequireComponent(typeof(SphereCollider))]
    public sealed class PowerUpItem : MonoBehaviour, IGazeTarget
    {
        private PowerUpManager owner;
        private PowerUpBalance balance;
        private Transform driftTarget;
        private Renderer[] renderers;
        private TextMesh label;
        private float lifetime, dwell, collectDuration, driftSpeed, phase;
        private bool collecting;

        public PowerUpType Type => balance.Type;
        public float CollectionProgress => collectDuration > 0f ? Mathf.Clamp01(dwell / collectDuration) : 0f;
        public float RemainingLifetime => lifetime;
        public bool IsAvailable => gameObject.activeInHierarchy && lifetime > 0f;

        public void Prepare(PowerUpManager manager, Renderer[] itemRenderers, TextMesh itemLabel)
        {
            owner = manager; renderers = itemRenderers; label = itemLabel;
            SphereCollider collider = GetComponent<SphereCollider>();
            collider.isTrigger = true; collider.radius = .75f;
            gameObject.SetActive(false);
        }

        public void Spawn(PowerUpBalance value, Vector3 position, Transform target, float seconds, float gazeSeconds, float movementSpeed)
        {
            balance = value; driftTarget = target; lifetime = Mathf.Max(.1f, seconds);
            collectDuration = Mathf.Max(.1f, gazeSeconds); driftSpeed = Mathf.Max(0f, movementSpeed);
            dwell = 0f; collecting = false; phase = Random.value * 10f;
            transform.SetPositionAndRotation(position, Quaternion.identity);
            if (label != null) { label.text = value.Icon + "\n" + value.ShortCode; label.color = value.Color; }
            if (renderers != null) foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", value.Color); block.SetColor("_EmissionColor", value.Color * 3f);
                block.SetColor("_Color", value.Color); renderer.SetPropertyBlock(block);
            }
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!IsAvailable) return;
            float dt = Time.deltaTime;
            lifetime -= dt;
            phase += dt;
            transform.Rotate(0f, 75f * dt, 18f * dt, Space.Self);
            transform.position += Vector3.up * (Mathf.Sin(phase * 2.1f) * .035f * dt);
            if (driftTarget != null && driftSpeed > 0f)
            {
                Vector3 delta = driftTarget.position - transform.position;
                if (delta.sqrMagnitude > 16f) transform.position += delta.normalized * (driftSpeed * dt);
            }
            if (label != null)
            {
                Camera camera = Camera.main;
                if (camera != null) label.transform.rotation = Quaternion.LookRotation(label.transform.position - camera.transform.position, camera.transform.up);
                label.text = collecting ? $"{balance.Icon}  {Mathf.RoundToInt(CollectionProgress * 100f):D2}%\n{balance.ShortCode}" : $"{balance.Icon}\n{balance.ShortCode}";
            }
            if (lifetime <= 0f) owner?.Release(this);
        }

        public void OnGazeEnter(RaycastHit _) { if (IsAvailable) collecting = true; }
        public void OnGazeStay(RaycastHit _, float deltaTime)
        {
            if (!IsAvailable) return;
            collecting = true; dwell += Mathf.Max(0f, deltaTime);
            if (dwell >= collectDuration) owner?.Collect(this);
        }
        public void OnGazeExit() { collecting = false; dwell = Mathf.Max(0f, dwell - collectDuration * .35f); }

        public void Despawn()
        {
            collecting = false; dwell = lifetime = 0f; driftTarget = null;
            gameObject.SetActive(false);
        }
    }
}
