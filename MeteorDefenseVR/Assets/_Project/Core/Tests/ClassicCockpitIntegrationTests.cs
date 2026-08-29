using System.Collections;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.Visual;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace MeteorDefenseVR.Tests
{
    public sealed class ClassicCockpitIntegrationTests
    {
        private const string ScenePath="Assets/_Project/Scenes/MeteorDefense.unity";

        [UnityTearDown]
        public IEnumerator Cleanup(){if(Application.isPlaying)yield return new ExitPlayMode();}

        [Test]
        public void ClassicFiveMonitorPhysicalLayoutIsPreservedAndPanoramicCageIsHidden()
        {
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            Transform geometry=GameObject.Find("LaunchSystem").transform.Find("CockpitGeometry");
            Transform cockpit=geometry.Find("PremiumCockpit");Assert.That(cockpit,Is.Not.Null);Assert.That(cockpit.gameObject.activeSelf,Is.True);
            foreach(string monitor in new[]{"PortSecondary","PortPrimary","Navigation","StarboardPrimary","StarboardSecondary"})
            {Transform housing=cockpit.Find(monitor);Assert.That(housing,Is.Not.Null,monitor);Assert.That(housing.GetComponentInChildren<Renderer>(true),Is.Not.Null,monitor);}
            Transform panoramic=geometry.Find("PanoramicCanopy");
            Assert.That(panoramic.Find("CanopyGlass360").gameObject.activeSelf,Is.True);
            Assert.That(panoramic.Find("CanopyStructure").gameObject.activeSelf,Is.False);
            Assert.That(panoramic.Find("CanopyNavigationLights").gameObject.activeSelf,Is.False);
            for(int i=0;i<3;i++)Assert.That(panoramic.Find("RearTelemetry"+i).gameObject.activeSelf,Is.False);
        }

        [Test]
        public void EveryPhysicalMonitorHasARealRuntimeReadoutBinding()
        {
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var telemetry=Object.FindAnyObjectByType<CockpitTelemetryPresentation>(FindObjectsInactive.Include);
            Assert.That(telemetry,Is.Not.Null);using var serialized=new SerializedObject(telemetry);
            foreach(string field in new[]{"shipStatus","threatAnalysis","radarReadout","weaponStatus","systemsStatus"})
            {
                var text=serialized.FindProperty(field).objectReferenceValue as TMP_Text;
                Assert.That(text,Is.Not.Null,field);Assert.That(text.rectTransform.sizeDelta.x,Is.GreaterThan(0),field);
                Assert.That(text.rectTransform.sizeDelta.y,Is.GreaterThan(0),field);
            }
            Assert.That(serialized.FindProperty("damagePresentation").objectReferenceValue,Is.Not.Null);
            Assert.That(serialized.FindProperty("weapon").objectReferenceValue,Is.Not.Null);
            Assert.That(serialized.FindProperty("turrets").objectReferenceValue,Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator ShipAndScoreReadoutsFollowRuntimeValues()
        {
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);yield return new EnterPlayMode();yield return null;
            var telemetry=Object.FindAnyObjectByType<CockpitTelemetryPresentation>();
            var health=Object.FindAnyObjectByType<PlayerHealth>();var stats=Object.FindAnyObjectByType<GameSessionStats>();
            health.Tick(1);Assert.That(health.ApplyDamage(30),Is.True);stats.SetScore(2550);
            for(int i=0;i<20;i++)yield return null;telemetry.Refresh();
            var ship=GameObject.Find("LiveShipStatus").GetComponent<TMP_Text>();
            var threat=GameObject.Find("LiveThreatAnalysis").GetComponent<TMP_Text>();
            var weapon=GameObject.Find("LiveWeaponStatus").GetComponent<TMP_Text>();
            var systems=GameObject.Find("LiveSystemsStatus").GetComponent<TMP_Text>();
            Assert.That(ship.text,Does.Not.Contain("SHIELD    100%"),"The readout must begin its short animated decrease");
            yield return new WaitForSecondsRealtime(.2f);telemetry.Refresh();
            Assert.That(ship.text,Does.Contain("SHIELD    070%"));Assert.That(ship.text,Does.Contain("DAMAGE    030"));
            Assert.That(threat.text,Does.Contain("SCORE     2,550"));
            Assert.That(weapon.text,Does.Contain("TOP TURRET").And.Contain("BOTTOM TURRET").And.Contain("TARGET"));
            Assert.That(systems.text,Does.Contain("GAZE").And.Contain("ENGINE").And.Contain("RADAR"));
        }
    }
}
