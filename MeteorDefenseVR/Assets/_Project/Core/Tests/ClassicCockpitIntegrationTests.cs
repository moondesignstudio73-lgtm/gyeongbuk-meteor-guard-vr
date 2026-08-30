using System.Collections;
using System.Linq;
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
        public void ClassicFiveMonitorPhysicalLayoutAndClosedRearShellArePreserved()
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
            Transform rear=cockpit.Find("RestoredRearCockpit");Assert.That(rear,Is.Not.Null);Assert.That(rear.gameObject.activeSelf,Is.True);
            foreach(string part in new[]{"RearBulkhead_Port","RearBulkhead_Starboard","RearObservationGlass","CeilingSpine","FloorObservationGlass"})
                Assert.That(rear.Find(part),Is.Not.Null,part);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Premium/Prefabs/CockpitVisual.prefab"),Is.Not.Null);
        }

        [Test]
        public void CenterRadarIsLiveAndSideStatusUsesSeparateHolograms()
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
            Assert.That(serialized.FindProperty("shipHologram").objectReferenceValue,Is.Not.Null);
            Assert.That(serialized.FindProperty("threatHologram").objectReferenceValue,Is.Not.Null);
            Transform cockpit=GameObject.Find("LaunchSystem").transform.Find("CockpitGeometry/PremiumCockpit");
            foreach(string monitor in new[]{"PortSecondary","PortPrimary","StarboardPrimary","StarboardSecondary"})
                Assert.That(cockpit.Find(monitor+"/Readout").GetComponent<Renderer>().enabled,Is.True,monitor);
            Assert.That(cockpit.Find("Navigation/Readout").GetComponent<Renderer>().enabled,Is.False);
        }

        [Test]
        public void LeftHologramWorstCaseReadoutsStayInsideTheirPanel()
        {
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var telemetry=Object.FindAnyObjectByType<CockpitTelemetryPresentation>(FindObjectsInactive.Include);
            using var serialized=new SerializedObject(telemetry);
            var ship=serialized.FindProperty("shipStatus").objectReferenceValue as TMP_Text;
            var systems=serialized.FindProperty("systemsStatus").objectReferenceValue as TMP_Text;
            var panel=ship.rectTransform.parent as RectTransform;
            Assert.That(panel,Is.Not.Null);Assert.That(systems.rectTransform.parent,Is.EqualTo(panel));
            ship.text="SHIP // CRITICAL\nSHIELD    100%\nHULL      100%\nENGINE    WARNING\nDAMAGE    100";
            systems.text="SENSOR ARRAY\nGAZE       TRACKING\nENGINE     WARNING\nRADAR      99 CONTACTS\nPOWER      UNSTABLE";
            Canvas.ForceUpdateCanvases();ship.ForceMeshUpdate(true,true);systems.ForceMeshUpdate(true,true);
            Assert.That(ship.enableAutoSizing,Is.False);Assert.That(systems.enableAutoSizing,Is.False);
            Assert.That(ship.overflowMode,Is.EqualTo(TextOverflowModes.Truncate));Assert.That(systems.overflowMode,Is.EqualTo(TextOverflowModes.Truncate));
            Assert.That(ship.preferredWidth,Is.LessThanOrEqualTo(ship.rectTransform.rect.width+.5f),"Ship text width exceeds its slot.");
            Assert.That(ship.preferredHeight,Is.LessThanOrEqualTo(ship.rectTransform.rect.height+.5f),"Ship text height exceeds its slot.");
            Assert.That(systems.preferredWidth,Is.LessThanOrEqualTo(systems.rectTransform.rect.width+.5f),"Systems text width exceeds its slot.");
            Assert.That(systems.preferredHeight,Is.LessThanOrEqualTo(systems.rectTransform.rect.height+.5f),"Systems text height exceeds its slot.");
            float shipBottom=ship.rectTransform.anchoredPosition.y-ship.rectTransform.rect.height;
            float systemsTop=systems.rectTransform.anchoredPosition.y;
            float systemsBottom=systemsTop-systems.rectTransform.rect.height;
            Assert.That(shipBottom,Is.GreaterThanOrEqualTo(systemsTop),"Ship and sensor blocks overlap.");
            Assert.That(systemsBottom,Is.GreaterThan(panel.rect.yMin+8),"Sensor block crosses the hologram bottom border.");
        }

        [Test]
        public void RightHologramWorstCaseReadoutsStayInsideTheirPanel()
        {
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var telemetry=Object.FindAnyObjectByType<CockpitTelemetryPresentation>(FindObjectsInactive.Include);
            using var serialized=new SerializedObject(telemetry);
            var threat=serialized.FindProperty("threatAnalysis").objectReferenceValue as TMP_Text;
            var weapon=serialized.FindProperty("weaponStatus").objectReferenceValue as TMP_Text;
            var panel=threat.rectTransform.parent as RectTransform;
            Assert.That(panel,Is.Not.Null);Assert.That(weapon.rectTransform.parent,Is.EqualTo(panel));
            threat.text="THREAT // WARNING\nCONTACTS 12\nVECTOR REAR RIGHT\nRANGE 018m\nSCORE 99,999";
            weapon.text="WEAPON SYSTEM\nTOP TURRET READY\nBOTTOM TURRET AIMING\nTARGET LOCKING\nRANGE 018m";
            Canvas.ForceUpdateCanvases();threat.ForceMeshUpdate(true,true);weapon.ForceMeshUpdate(true,true);
            Assert.That(threat.preferredHeight,Is.LessThanOrEqualTo(threat.rectTransform.rect.height+.5f));
            Assert.That(weapon.preferredHeight,Is.LessThanOrEqualTo(weapon.rectTransform.rect.height+.5f));
            float threatBottom=threat.rectTransform.anchoredPosition.y-threat.rectTransform.rect.height;
            float weaponTop=weapon.rectTransform.anchoredPosition.y;
            float weaponBottom=weaponTop-weapon.rectTransform.rect.height;
            Assert.That(threatBottom,Is.GreaterThanOrEqualTo(weaponTop),"Threat and weapon blocks overlap.");
            Assert.That(weaponBottom,Is.GreaterThan(panel.rect.yMin+8),"Weapon block crosses the hologram bottom border.");
        }

        [Test]
        public void HologramsUseSubtleTransparentPanelsAndOneBoundedScanLine()
        {
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            foreach(string name in new[]{"ShipStatusHologram","ThreatStatusHologram"})
            {
                GameObject panel=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                    .FirstOrDefault(item=>item.name==name)?.gameObject;Assert.That(panel,Is.Not.Null,name);
                var image=panel.GetComponent<UnityEngine.UI.Image>();
                Assert.That(image.color.a,Is.LessThanOrEqualTo(.15f),name+" background alpha");
                var outline=panel.GetComponent<UnityEngine.UI.Outline>();
                Assert.That(outline.effectDistance.magnitude,Is.LessThanOrEqualTo(1.5f),name+" border thickness");
                var scan=panel.GetComponent<CockpitHologramScan>();Assert.That(scan,Is.Not.Null,name);
                Assert.That(scan.ScanLine,Is.Not.Null);Assert.That(scan.ScanLine.parent,Is.EqualTo(panel.transform));
                Assert.That(scan.Period,Is.InRange(2.5f,4.5f));
            }
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
