using System.Collections;
using System.Linq;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.UI;
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
        public void RightHologramKeepsReferencesForRuntimeSummary()
        {
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var telemetry=Object.FindAnyObjectByType<CockpitTelemetryPresentation>(FindObjectsInactive.Include);
            using var serialized=new SerializedObject(telemetry);
            var threat=serialized.FindProperty("threatAnalysis").objectReferenceValue as TMP_Text;
            var weapon=serialized.FindProperty("weaponStatus").objectReferenceValue as TMP_Text;
            var panel=threat.rectTransform.parent as RectTransform;
            Assert.That(panel,Is.Not.Null);Assert.That(weapon.rectTransform.parent,Is.EqualTo(panel));
            // The weapon block is retained as authored data but hidden during play.
            // TelemetryReadabilityTests covers the actual runtime text bounds at five sizes.
            Assert.That(telemetry.ThreatReadout,Is.SameAs(threat));
            Assert.That(panel.GetComponent<CanvasGroup>(),Is.Not.Null);
            Assert.That(threat.raycastTarget,Is.False);
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
            var router=Object.FindAnyObjectByType<GazeInputRouter>();router.InputSuspended=true;
            var director=Object.FindAnyObjectByType<SfPresentationDirector>();
            float deadline=Time.realtimeSinceStartup+30;
            while(director.IsBootPresenting&&Time.realtimeSinceStartup<deadline)yield return null;
            GameFlowManager.Instance.StartMainGame();yield return null;
            var telemetry=Object.FindAnyObjectByType<CockpitTelemetryPresentation>();
            var health=Object.FindAnyObjectByType<PlayerHealth>();var stats=Object.FindAnyObjectByType<GameSessionStats>();
            health.Tick(1);Assert.That(health.ApplyDamage(30),Is.True);
            Object.FindAnyObjectByType<MissionHudController>().RestoreScore(2550);
            for(int i=0;i<20;i++)yield return null;telemetry.Refresh();
            var ship=GameObject.Find("LiveShipStatus").GetComponent<TMP_Text>();
            var threat=GameObject.Find("LiveThreatAnalysis").GetComponent<TMP_Text>();
            Assert.That(telemetry.ReadableSummary,Is.True);
            Assert.That(ship.text,Does.Contain("방어막 70%").And.Contain("피해 30"));
            Assert.That(stats.Score,Is.EqualTo(2550));
            Assert.That(Object.FindAnyObjectByType<GameplayHudOverlayView>().ScoreValue.text,Is.EqualTo("2,550"));
            Assert.That(threat.text,Does.Not.Contain("SCORE"),"Score remains in the main HUD rather than duplicated on the threat panel");
            foreach(var text in ship.transform.parent.parent.GetComponentsInChildren<TMP_Text>(true))
                if(text.name=="LiveWeaponStatus"||text.name=="LiveSystemsStatus")
                    Assert.That(text.gameObject.activeSelf,Is.False,"Decorative and duplicate readouts must not compete with essential values");
        }
    }
}
