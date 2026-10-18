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
        private Transform visualRoot, halo;
        private Renderer[] renderers;
        private TextMesh label;
        private float lifetime, dwell, collectDuration, driftSpeed, phase;
        private bool collecting;
        private string idleLabel = string.Empty;
        private int displayedPercent = -1;

        public PowerUpType Type => balance.Type;
        public float CollectionProgress => collectDuration > 0f ? Mathf.Clamp01(dwell / collectDuration) : 0f;
        public float RemainingLifetime => lifetime;
        public bool IsAvailable => gameObject.activeInHierarchy && lifetime > 0f;

        public void Prepare(PowerUpManager manager, Renderer[] itemRenderers, TextMesh itemLabel, Transform itemVisualRoot, Transform itemHalo)
        {
            owner = manager; renderers = itemRenderers; label = itemLabel; visualRoot = itemVisualRoot; halo = itemHalo;
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
            idleLabel = $"{value.Icon}  {value.ShortCode}\nLOOK TO ACQUIRE"; displayedPercent = -1;
            if (label != null) { label.text = idleLabel; label.color = value.Color; }
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
            if (visualRoot != null)
            {
                visualRoot.Rotate(22f * dt, 78f * dt, 15f * dt, Space.Self);
                visualRoot.localScale = Vector3.one * (1f + Mathf.Sin(phase * 3.4f) * .055f);
            }
            transform.position += Vector3.up * (Mathf.Sin(phase * 2.1f) * .035f * dt);
            if (driftTarget != null && driftSpeed > 0f)
            {
                Vector3 delta = driftTarget.position - transform.position;
                if (delta.sqrMagnitude > 16f) transform.position += delta.normalized * (driftSpeed * dt);
            }
            if (label != null)
            {
                Camera camera = Camera.main;
                if (camera != null)
                {
                    Quaternion facing = Quaternion.LookRotation(label.transform.position - camera.transform.position, camera.transform.up);
                    label.transform.rotation = facing;
                    if (halo != null) halo.rotation = facing;
                }
                if (halo != null)
                {
                    float acquire = collecting ? CollectionProgress : 0f;
                    float pulse = 2.05f + Mathf.Sin(phase * 2.8f) * .09f + acquire * .22f;
                    halo.localScale = Vector3.one * pulse;
                }
                if (collecting)
                {
                    int percent = Mathf.RoundToInt(CollectionProgress * 100f);
                    if (displayedPercent != percent)
                    {
                        displayedPercent = percent;
                        label.text = $"{balance.Icon}  {balance.ShortCode}\nACQUIRE  {percent:D2}%";
                    }
                }
                else if (displayedPercent >= 0)
                {
                    displayedPercent = -1;
                    label.text = idleLabel;
                }
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
            collecting = false; dwell = lifetime = 0f; driftTarget = null; displayedPercent = -1;
            gameObject.SetActive(false);
        }
    }
}
