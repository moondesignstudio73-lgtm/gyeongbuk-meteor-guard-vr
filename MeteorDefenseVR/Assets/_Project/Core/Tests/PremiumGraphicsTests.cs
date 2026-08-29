using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Visual;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using System.Collections;
using UnityEditor.SceneManagement;
using UnityEngine.TestTools;

namespace MeteorDefenseVR.Tests
{
    public sealed class PremiumGraphicsTests
    {
        [UnityTearDown]
        public IEnumerator TearDown(){if(Application.isPlaying)yield return new ExitPlayMode();}
        [UnityTest]
        public IEnumerator PooledEffectsAdvanceWithoutManualSimulationAndExpire()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity",OpenSceneMode.Single);
            yield return new EnterPlayMode();
            Object.FindAnyObjectByType<MeteorDefenseVR.PcInput.GazeInputRouter>().SetMode(MeteorDefenseVR.PcInput.PcGazeMode.MouseGaze);
            var effects=Object.FindAnyObjectByType<PremiumCombatVfx>();
            Assert.That(effects.SlotCount,Is.EqualTo(8));Assert.That(effects.ActiveParticleCount,Is.Zero);
            Object.FindAnyObjectByType<MeteorDefenseVR.VFX.CombatVfxController>().PlayBossExplosion(new Vector3(0,0,8));
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(effects.ActiveParticleCount,Is.GreaterThan(0));Assert.That(effects.ActiveParticleCount,Is.LessThanOrEqualTo(100));
            yield return new WaitForSecondsRealtime(2f);
            Assert.That(effects.ActiveParticleCount,Is.Zero,"Effects must expire in real Play Mode without calling Simulate");
        }
        [TestCase("Normal")][TestCase("Fast")][TestCase("Large")][TestCase("Boss")][TestCase("Tutorial")]
        public void UpgradedPrefabPreservesGameplayComponentsAndUsesAuthoredMesh(string kind)
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Meteor/Prefabs/Meteor_"+kind+".prefab");
            Assert.That(root,Is.Not.Null);Assert.That(root.GetComponent<MeteorController>(),Is.Not.Null);
            Assert.That(root.GetComponent<GazeLockOnTarget>(),Is.Not.Null);Assert.That(root.GetComponent<Collider>(),Is.Not.Null);
            var mesh=root.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(AssetDatabase.GetAssetPath(mesh),Does.StartWith("Assets/Art/Premium/Meshes/"));
            Assert.That(mesh.triangles.Length/3,Is.LessThanOrEqualTo(3000));
            Assert.That(root.GetComponent<Renderer>().sharedMaterial.shader.name,Is.EqualTo("MeteorDefense/Premium Asteroid"));
            Assert.That(root.GetComponentInChildren<PremiumReticlePresentation>(true),Is.Not.Null);
            foreach(Transform child in root.transform)if(child.name.StartsWith("RockLobe"))Assert.That(child.GetComponent<Renderer>().enabled,Is.False);
        }
        [TestCase("AvionicsAtlas")][TestCase("BasaltAlbedo")][TestCase("GunmetalAlbedo")][TestCase("ExplosionFlipbook")]
        public void NewTextureImportsAreBounded(string name)
        {
            var importer=AssetImporter.GetAtPath("Assets/Art/Premium/Textures/"+name+".png") as TextureImporter;
            Assert.That(importer,Is.Not.Null);Assert.That(importer.maxTextureSize,Is.LessThanOrEqualTo(2048));Assert.That(importer.mipmapEnabled,Is.True);
        }
        [TestCase("MeteorDefense/Premium Asteroid")][TestCase("MeteorDefense/Orbital Sky")]
        [TestCase("MeteorDefense/Soft Energy")][TestCase("MeteorDefense/Explosion Flipbook")][TestCase("MeteorDefense/Vertex Glow")]
        public void IntegratedShadersHaveNoCompilerErrors(string name)
        {var shader=Shader.Find(name);Assert.That(shader,Is.Not.Null);Assert.That(ShaderUtil.ShaderHasError(shader),Is.False,name);}
    }
}
