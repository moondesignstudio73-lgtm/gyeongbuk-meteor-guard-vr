using UnityEngine;
using UnityEngine.Events;
using System.Linq;

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
        private BossShellPresentation shell;
        private bool destructionHandled;
        public BossShellPresentation Shell=>shell;
        public int ExplosionCount {get;private set;}

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            if (meteor == null) meteor = GetComponent<MeteorController>();
            if (crackRenderers == null || crackRenderers.Length == 0) crackRenderers = GetComponentsInChildren<Renderer>();
            EnsureShell();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() { Unsubscribe();shell?.Reset(); }
        private void Update(){if(Time.timeScale>0)shell?.Tick(Time.deltaTime);}
        private void OnDestroy()=>shell?.Dispose();
        private void EnsureShell()
        {
            if(shell!=null||!Application.isPlaying)return;
            var filters=GetComponentsInChildren<MeshFilter>(true).Where(f=>f.transform==transform||f.name.StartsWith("LOD_")).ToArray();
            var source=filters.FirstOrDefault(f=>f.name=="LOD_Mid")??filters.FirstOrDefault();
            if(source==null||source.sharedMesh==null||!source.sharedMesh.isReadable)return;
            shell=new BossShellPresentation(transform,source,filters.Select(f=>f.GetComponent<Renderer>()).Where(r=>r!=null).ToArray());
        }

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
            meteor.ResetPerformed -= HandleSpawned;
            meteor.Spawned += HandleSpawned;
            meteor.Damaged += HandleDamaged;
            meteor.Destroyed += HandleDestroyed;
            meteor.ResetPerformed += HandleSpawned;
        }

        private void Unsubscribe()
        {
            if (meteor == null) return;
            meteor.Spawned -= HandleSpawned;
            meteor.Damaged -= HandleDamaged;
            meteor.Destroyed -= HandleDestroyed;
            meteor.ResetPerformed -= HandleSpawned;
        }

        private void HandleSpawned(MeteorController _) {destructionHandled=false;ExplosionCount=0;EnsureShell();shell?.Reset();SetCrackIntensity(0.35f);}

        private void HandleDamaged(MeteorController target, float _)
        {
            float damageProgress = target.MaxHealth > 0f ? 1f - target.CurrentHealth / target.MaxHealth : 1f;
            SetCrackIntensity(Mathf.Lerp(0.8f, 4.5f, damageProgress));
            shell?.ApplyHealth(target.CurrentHealth,target.MaxHealth);
        }

        private void HandleDestroyed(MeteorController target)
        {
            if(destructionHandled)return;destructionHandled=true;ExplosionCount++;
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
            propertyBlock.Clear();propertyBlock.SetColor(EmissionColorId,emission);shell?.SetEmission(propertyBlock);
        }
    }
}
