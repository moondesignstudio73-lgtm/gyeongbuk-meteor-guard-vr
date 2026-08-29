using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Visual;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MeteorDefenseVR.Tests
{
    public sealed class CommercialVisualPolishTests
    {
        [SetUp]
        public void OpenScene()=>EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity",OpenSceneMode.Single);

        [Test]
        public void CockpitUsesCanopyAndLiveReadOnlyAvionics()
        {
            Transform cockpit=GameObject.Find("LaunchSystem").transform.Find("CockpitGeometry/PremiumCockpit");
            Assert.That(cockpit.Find("CommercialMilitaryDetail"),Is.Not.Null);
            var glass=cockpit.Find("CommercialCanopy/CanopyGlass_Port").GetComponent<Renderer>();
            Assert.That(glass.sharedMaterial.shader.name,Is.EqualTo("MeteorDefense/Cockpit Canopy"));
            var telemetry=cockpit.GetComponentInChildren<CockpitTelemetryPresentation>(true);
            Assert.That(telemetry,Is.Not.Null);Assert.That(telemetry.RefreshRate,Is.InRange(2,10));
        }

        [TestCase("Normal")][TestCase("Fast")][TestCase("Large")][TestCase("Boss")][TestCase("Tutorial")]
        public void MeteorUsesBoundedLodAndPooledLifecycleTrail(string kind)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Meteor/Prefabs/Meteor_"+kind+".prefab");
            var lod=prefab.GetComponent<LODGroup>();Assert.That(lod,Is.Not.Null);Assert.That(lod.GetLODs().Length,Is.EqualTo(3));
            var motion=prefab.GetComponentInChildren<MeteorMotionTrail>(true);Assert.That(motion,Is.Not.Null);Assert.That(motion.Trail,Is.Not.Null);
            Assert.That(motion.Trail.autodestruct,Is.False);Assert.That(motion.Trail.sharedMaterial.shader.name,Is.EqualTo("MeteorDefense/Soft Energy"));
            Assert.That(prefab.GetComponentsInChildren<TrailRenderer>(true).Length,Is.EqualTo(1));
        }

        [TestCase("MeteorDefense/Cockpit Canopy")][TestCase("MeteorDefense/Orbital Earth")]
        public void CommercialShadersCompile(string shaderName)
        {
            Shader shader=Shader.Find(shaderName);Assert.That(shader,Is.Not.Null);Assert.That(ShaderUtil.ShaderHasError(shader),Is.False,shaderName);
        }
    }
}
