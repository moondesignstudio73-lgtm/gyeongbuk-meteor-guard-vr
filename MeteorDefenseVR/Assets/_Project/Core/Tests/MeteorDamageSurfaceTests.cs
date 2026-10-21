using System.Linq;
using MeteorDefenseVR.Meteor;
using NUnit.Framework;
using UnityEngine;

namespace MeteorDefenseVR.Tests
{
    public sealed class MeteorDamageSurfaceTests
    {
        private GameObject root;
        private MeteorController meteor;
        private MeteorDamageSurface surface;
        [SetUp] public void Setup()
        {
            root=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            meteor=root.AddComponent<MeteorController>();meteor.Configure(MeteorType.Large,4,0,20,300,2);
            meteor.Spawn();surface=root.GetComponent<MeteorDamageSurface>();
            // EditMode does not dispatch ordinary MonoBehaviour lifecycle callbacks.
            Lifecycle("Awake");Lifecycle("OnEnable");
        }
        private void Lifecycle(string method) => typeof(MeteorDamageSurface).GetMethod(method,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(surface,null);
        [TearDown] public void Cleanup(){Lifecycle("OnDisable");Object.DestroyImmediate(root);}
        [Test] public void OnlyValidSurvivingDamageChangesSurface()
        {
            Assert.That(surface.DamageFraction,Is.Zero);
            foreach(float amount in new[]{0f,-1,float.NaN,float.PositiveInfinity})Assert.That(meteor.ReceiveDamage(amount),Is.False);
            Assert.That(surface.DamageFraction,Is.Zero);
            meteor.ReceiveDamage(1);Assert.That(surface.DamageFraction,Is.EqualTo(.25f));
            meteor.ReceiveDamage(2);Assert.That(surface.DamageFraction,Is.EqualTo(.75f));
            Assert.That(meteor.IsTargetable,Is.True);
            meteor.ReceiveDamage(1);Assert.That(surface.DamageFraction,Is.Zero);
        }
        [Test] public void ResetAndReuseDoNotAccumulateMeshesOrMaterials()
        {
            var original=root.GetComponent<MeshFilter>().sharedMesh;
            var material=root.transform.Find("SurfaceDamage").GetComponent<MeshRenderer>().sharedMaterial;
            for(int i=0;i<50;i++)
            {
                meteor.ReceiveDamage(1);meteor.ResetMeteor(false);Assert.That(surface.DamageFraction,Is.Zero);
                meteor.Spawn();Assert.That(surface.DamageFraction,Is.Zero);
            }
            Assert.That(surface.SurfaceCount,Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<MeshFilter>(true).Length,Is.EqualTo(2));
            Assert.That(root.transform.Find("SurfaceDamage").GetComponent<MeshFilter>().sharedMesh,Is.SameAs(original));
            Assert.That(root.transform.Find("SurfaceDamage").GetComponent<MeshRenderer>().sharedMaterial,Is.SameAs(material));
        }
        [Test] public void BossReuseAndSingleHitMeteorsHaveNoNewDamageVisual()
        {
            meteor.Configure(MeteorType.Boss,4,0,20,300,2);meteor.Spawn();meteor.ReceiveDamage(1);
            Assert.That(surface.DamageFraction,Is.Zero);
            Assert.That(root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="SurfaceDamage").All(r=>!r.enabled),Is.True);
            meteor.Configure(MeteorType.Normal,1,0,20,100,1);meteor.Spawn();meteor.ReceiveDamage(1);
            Assert.That(surface.DamageFraction,Is.Zero);
        }
        [Test] public void WarmDamageHandlerHasNoManagedAllocation()
        {
            meteor.Configure(MeteorType.Large,10000,0,20,300,2);meteor.Spawn();meteor.ReceiveDamage(1);
            long start=System.GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<1000;i++)meteor.ReceiveDamage(1);
            long bytes=System.GC.GetAllocatedBytesForCurrentThread()-start;
            Assert.That(bytes,Is.Zero,"Warm damage path should only update cached renderer properties");
        }
    }
}
