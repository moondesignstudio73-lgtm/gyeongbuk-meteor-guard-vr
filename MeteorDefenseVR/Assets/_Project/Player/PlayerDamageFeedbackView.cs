using UnityEngine;

namespace MeteorDefenseVR.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerDamageFeedbackView : MonoBehaviour
    {
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private Light cockpitDamageLight;
        [SerializeField] private AudioSource impactAudioSource;
        [SerializeField, Min(0f)] private float flashDuration = 0.22f;
        [SerializeField, Min(0f)] private float flashIntensity = 3.5f;

        private float flashRemaining;

        private void OnEnable() => Subscribe();
        private void Update() => Tick(Time.unscaledDeltaTime);

        private void OnDisable()
        {
            Unsubscribe();
            flashRemaining = 0f;
            SetLight(false, 0f);
        }

        public void Configure(PlayerHealth health, Light damageLight, AudioSource impactSource)
        {
            Unsubscribe();
            playerHealth = health;
            cockpitDamageLight = damageLight;
            impactAudioSource = impactSource;
            Subscribe();
            SetLight(false, 0f);
        }

        public void Tick(float deltaTime)
        {
            if (flashRemaining <= 0f) return;
            flashRemaining = Mathf.Max(0f, flashRemaining - Mathf.Max(0f, deltaTime));
            float normalized = flashDuration > 0f ? flashRemaining / flashDuration : 0f;
            SetLight(flashRemaining > 0f, flashIntensity * normalized);
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled || playerHealth == null) return;
            playerHealth.ImpactFeedbackRequested -= PlayImpact;
            playerHealth.ImpactFeedbackRequested += PlayImpact;
            playerHealth.HealthReset -= Clear;
            playerHealth.HealthReset += Clear;
        }

        private void Unsubscribe()
        {
            if (playerHealth != null) { playerHealth.ImpactFeedbackRequested -= PlayImpact; playerHealth.HealthReset -= Clear; }
        }

        private void Clear() { flashRemaining = 0f; SetLight(false, 0f); impactAudioSource?.Stop(); }

        private void PlayImpact()
        {
            flashRemaining = flashDuration;
            SetLight(true, flashIntensity);
            if (impactAudioSource != null && impactAudioSource.clip != null) impactAudioSource.Play();
        }

        private void SetLight(bool enabled, float intensity)
        {
            if (cockpitDamageLight == null) return;
            cockpitDamageLight.enabled = enabled;
            cockpitDamageLight.intensity = Mathf.Max(0f, intensity);
        }
    }
}
