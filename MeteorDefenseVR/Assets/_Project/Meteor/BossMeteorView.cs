using UnityEngine;
using UnityEngine.Events;

namespace MeteorDefenseVR.Meteor
{
    [DisallowMultipleComponent]
    public sealed class BossMeteorView : MonoBehaviour
    {
        [SerializeField] private MeteorController meteor;
        [SerializeField] private Renderer[] crackRenderers;
        [SerializeField] private Light crackLight;
        [SerializeField] private AudioSource destructionAudioSource;
        [SerializeField] private UnityEvent onStrongExplosion;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock propertyBlock;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            if (meteor == null) meteor = GetComponent<MeteorController>();
            if (crackRenderers == null || crackRenderers.Length == 0) crackRenderers = GetComponentsInChildren<Renderer>();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        public void Configure(MeteorController controller, Renderer[] renderers, Light light, AudioSource audioSource)
        {
            Unsubscribe();
            meteor = controller;
            crackRenderers = renderers;
            crackLight = light;
            destructionAudioSource = audioSource;
            Subscribe();
            SetCrackIntensity(0.35f);
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled || meteor == null) return;
            meteor.Spawned -= HandleSpawned;
            meteor.Damaged -= HandleDamaged;
            meteor.Destroyed -= HandleDestroyed;
            meteor.Spawned += HandleSpawned;
            meteor.Damaged += HandleDamaged;
            meteor.Destroyed += HandleDestroyed;
        }

        private void Unsubscribe()
        {
            if (meteor == null) return;
            meteor.Spawned -= HandleSpawned;
            meteor.Damaged -= HandleDamaged;
            meteor.Destroyed -= HandleDestroyed;
        }

        private void HandleSpawned(MeteorController _) => SetCrackIntensity(0.35f);

        private void HandleDamaged(MeteorController target, float _)
        {
            float damageProgress = target.MaxHealth > 0f ? 1f - target.CurrentHealth / target.MaxHealth : 1f;
            SetCrackIntensity(Mathf.Lerp(0.8f, 4.5f, damageProgress));
        }

        private void HandleDestroyed(MeteorController target)
        {
            SetCrackIntensity(6f);
            if (destructionAudioSource != null && destructionAudioSource.clip != null)
                AudioSource.PlayClipAtPoint(destructionAudioSource.clip, target.transform.position);
            if (Application.isPlaying) BossExplosionEffect.Spawn(target.transform.position);
            onStrongExplosion?.Invoke();
        }

        private void SetCrackIntensity(float intensity)
        {
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            Color emission = new Color(1f, 0.18f, 0.015f) * Mathf.Max(0f, intensity);
            if (crackRenderers != null)
            {
                foreach (Renderer targetRenderer in crackRenderers)
                {
                    if (targetRenderer == null) continue;
                    targetRenderer.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor(EmissionColorId, emission);
                    targetRenderer.SetPropertyBlock(propertyBlock);
                }
            }
            if (crackLight != null)
            {
                crackLight.color = new Color(1f, 0.05f, 0.01f);
                crackLight.intensity = intensity;
            }
        }
    }
}
