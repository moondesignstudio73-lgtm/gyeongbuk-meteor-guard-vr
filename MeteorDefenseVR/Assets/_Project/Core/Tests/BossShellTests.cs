using MeteorDefenseVR.Meteor;
using NUnit.Framework;
using UnityEngine;

namespace MeteorDefenseVR.Tests
{
    public sealed class BossShellTests
    {
        private GameObject root;private BossShellPresentation shell;private MeteorController meteor;
        [SetUp]public void Setup()
        {
            root=GameObject.CreatePrimitive(PrimitiveType.Sphere);meteor=root.AddComponent<MeteorController>();
            meteor.Configure(MeteorType.Boss,9,0,30,3000,3);meteor.Spawn();
            shell=new BossShellPresentation(root.transform,root.GetComponent<MeshFilter>(),new[]{root.GetComponent<Renderer>()});
            meteor.Damaged+=(m,d)=>shell.ApplyHealth(m.CurrentHealth,m.MaxHealth);meteor.ResetPerformed+=m=>shell.Reset();
        }
        [TearDown]public void Cleanup(){shell.Dispose();Object.DestroyImmediate(root);}
        [Test]public void InvalidHitsAndTimeDoNotAdvanceShell()
        {
            foreach(float hit in new[]{0,-1,float.NaN,float.PositiveInfinity})Assert.That(meteor.ReceiveDamage(hit),Is.False);
            shell.Tick(10);Assert.That(shell.Stage,Is.Zero);Assert.That(shell.CoreVisible,Is.False);
            meteor.ReceiveDamage(1);shell.Tick(10);Assert.That(shell.Stage,Is.Zero);
            meteor.ReceiveDamage(2);Assert.That(shell.Stage,Is.EqualTo(1));Assert.That(shell.ShedCount,Is.EqualTo(2));
            shell.Tick(1);meteor.ReceiveDamage(1);Assert.That(shell.Stage,Is.EqualTo(1));Assert.That(shell.ActiveFragments,Is.Zero);
        }
        [Test]public void LargeHitSkipsToCorrectStageAndLethalHitDoesNotEmitMoreShell()
        {
            meteor.ReceiveDamage(6);Assert.That(shell.Stage,Is.EqualTo(2));Assert.That(shell.ShedCount,Is.EqualTo(4));
            Assert.That(shell.ActiveFragments,Is.EqualTo(4));shell.Tick(.66f);Assert.That(shell.ActiveFragments,Is.Zero);
            int deaths=0;meteor.Destroyed+=m=>deaths++;meteor.ReceiveDamage(3);
            Assert.That(meteor.ReceiveDamage(1),Is.False);Assert.That(meteor.DestroyMeteor(),Is.False);Assert.That(deaths,Is.EqualTo(1));
            Assert.That(shell.ActiveFragments,Is.Zero);
        }
        [Test]public void FiftyResetsReusePiecesAndRestoreOriginalSurface()
        {
            var mesh=root.GetComponent<MeshFilter>().sharedMesh;int objects=root.GetComponentsInChildren<Transform>(true).Length;
            for(int i=0;i<50;i++){meteor.ReceiveDamage(6);meteor.ResetMeteor(false);meteor.Spawn();Assert.That(shell.Stage,Is.Zero);Assert.That(shell.ActiveFragments,Is.Zero);Assert.That(shell.CoreVisible,Is.False);}
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length,Is.EqualTo(objects));
            Assert.That(root.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(mesh));Assert.That(root.GetComponent<Renderer>().forceRenderingOff,Is.False);
        }
        [Test]public void LethalFirstHitDoesNotStartIntermediateFragments()
        {
            int deaths=0;meteor.Destroyed+=m=>deaths++;meteor.ReceiveDamage(999);
            Assert.That(shell.Stage,Is.Zero);Assert.That(shell.ActiveFragments,Is.Zero);Assert.That(shell.CoreVisible,Is.False);
            Assert.That(meteor.DestroyMeteor(),Is.False);Assert.That(deaths,Is.EqualTo(1));
        }
    }
}
