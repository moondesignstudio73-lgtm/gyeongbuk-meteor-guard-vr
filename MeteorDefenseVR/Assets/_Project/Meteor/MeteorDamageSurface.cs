using System.Collections.Generic;
using MeteorDefenseVR.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace MeteorDefenseVR.Meteor
{
    /// <summary>Persistent, health-driven scars on existing rock meshes, never on reticles.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(MeteorController))]
    public sealed class MeteorDamageSurface : MonoBehaviour
    {
        private static Material sharedSurface;
        private static readonly int DamageId = Shader.PropertyToID("_Damage");
        private MeteorController meteor;
        private MeshRenderer[] surfaces;
        private MaterialPropertyBlock block;
        public float DamageFraction { get; private set; }
        public int SurfaceCount => surfaces?.Length ?? 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetShared()
        {
            if (sharedSurface != null) Destroy(sharedSurface);
            sharedSurface = null;
        }

        private void Awake()
        {
            meteor = GetComponent<MeteorController>(); block = new MaterialPropertyBlock();
            if (sharedSurface == null)
            {
                var shader = Resources.Load<Shader>("MeteorDamageSurface");
                if (shader == null) { surfaces = System.Array.Empty<MeshRenderer>(); return; }
                sharedSurface = new Material(shader) { name="Shared meteor damage surface", hideFlags=HideFlags.DontSave };
            }
            var source = GetComponentsInChildren<MeshFilter>(true);
            var created = new List<MeshRenderer>();
            var bySource = new Dictionary<Renderer,Renderer>();
            foreach (var filter in source)
            {
                if (filter.sharedMesh == null || filter.GetComponentInParent<LockOnReticleView>() != null) continue;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled) continue;
                var child = new GameObject("SurfaceDamage") { layer=filter.gameObject.layer };
                child.transform.SetParent(filter.transform,false);
                child.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var overlay = child.AddComponent<MeshRenderer>();
                overlay.sharedMaterial=sharedSurface;overlay.shadowCastingMode=ShadowCastingMode.Off;
                overlay.receiveShadows=false;overlay.lightProbeUsage=LightProbeUsage.Off;
                overlay.reflectionProbeUsage=ReflectionProbeUsage.Off;overlay.enabled=false;
                created.Add(overlay);
                bySource.Add(renderer,overlay);
            }
            surfaces=created.ToArray();
            // Follow each authored distance level; never draw all LOD shells at once.
            foreach(var group in GetComponentsInChildren<LODGroup>(true))
            {
                var lods=group.GetLODs();
                for(int i=0;i<lods.Length;i++)
                {
                    var renderers=new List<Renderer>(lods[i].renderers);
                    foreach(var renderer in lods[i].renderers)
                        if(renderer!=null && bySource.TryGetValue(renderer,out var overlay))renderers.Add(overlay);
                    lods[i].renderers=renderers.ToArray();
                }
                group.SetLODs(lods);
            }
        }

        private void OnEnable()
        {
            meteor.Spawned += Refresh;meteor.Damaged += Damaged;meteor.Destroyed += Clear;
            meteor.ReachedPlayerEvent += Reached;meteor.ResetPerformed += Clear;
            Refresh(meteor);
        }

        private void OnDisable()
        {
            meteor.Spawned -= Refresh;meteor.Damaged -= Damaged;meteor.Destroyed -= Clear;
            meteor.ReachedPlayerEvent -= Reached;meteor.ResetPerformed -= Clear;
            SetDamage(0);
        }

        private void Damaged(MeteorController source,float amount) => Refresh(source);
        private void Reached(MeteorController source,int amount) => Clear(source);
        private void Clear(MeteorController source) => SetDamage(0);
        private void Refresh(MeteorController source)
        {
            bool alive = source.MeteorType != MeteorType.Boss && source.State == MeteorLifecycleState.Active && source.CurrentHealth > 0;
            SetDamage(alive ? Mathf.Clamp01(1-source.CurrentHealth/source.MaxHealth) : 0);
        }

        private void SetDamage(float fraction)
        {
            DamageFraction=fraction;block.SetFloat(DamageId,fraction);
            foreach (var surface in surfaces)
            {
                if (surface == null) continue;
                surface.enabled=fraction>0;surface.SetPropertyBlock(block);
            }
        }
    }
}
