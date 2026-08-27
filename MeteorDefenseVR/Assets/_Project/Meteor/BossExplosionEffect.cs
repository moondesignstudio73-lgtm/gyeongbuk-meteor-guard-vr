using UnityEngine;

namespace MeteorDefenseVR.Meteor
{
    [DisallowMultipleComponent]
    public sealed class BossExplosionEffect : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float duration = 1.2f;
        [SerializeField, Min(0.1f)] private float maximumScale = 7f;
        [SerializeField] private Light flashLight;
        private float elapsed;

        public static BossExplosionEffect Spawn(Vector3 position)
        {
            GameObject effectObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            effectObject.name = "BossExplosion_VFX";
            effectObject.transform.position = position;
            effectObject.transform.localScale = Vector3.one * 0.1f;
            Collider collider = effectObject.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            BossExplosionEffect effect = effectObject.AddComponent<BossExplosionEffect>();
            Light light = effectObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.18f, 0.02f);
            light.range = 14f;
            light.intensity = 8f;
            effect.flashLight = light;
            return effect;
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            transform.localScale = Vector3.one * Mathf.Lerp(0.1f, maximumScale, progress);
            if (flashLight != null) flashLight.intensity = Mathf.Lerp(8f, 0f, progress);
            if (progress >= 1f) Destroy(gameObject);
        }
    }
}
