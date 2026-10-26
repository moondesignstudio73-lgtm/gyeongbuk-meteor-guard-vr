using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
namespace MeteorDefenseVR.Tests {
public sealed class LowHealthIntegrationTests {
 [UnityTearDown] public IEnumerator Cleanup(){Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();}
 [UnityTest,Timeout(180000)] public IEnumerator CriticalNoticeThreatClearance(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return ThreatNoticeProof.Run("Docs/DeliveryQA/integrated-ui-20261027/EditorThreat",true);}
 [UnityTest,Timeout(180000)] public IEnumerator CriticalNoticeStageClearance(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return WarningContrastProof.Run("Docs/DeliveryQA/integrated-ui-20261027/EditorStages");}
 [UnityTest,Timeout(300000)] public IEnumerator ConcurrentBossFiveSizes(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return LoadingBossReviewProof.Run("Docs/DeliveryQA/integrated-ui-20261027/EditorBoss",true);}
 [UnityTest,Timeout(180000)] public IEnumerator ActualMeteorImpactDrivesMatchingHealthWarning(){
  EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return Run();
 }
 private static IEnumerator Run(){
  while(Object.FindAnyObjectByType<GazeInputRouter>()==null)yield return null;yield return null;
  var router=Object.FindAnyObjectByType<GazeInputRouter>();var director=Object.FindAnyObjectByType<SfPresentationDirector>();
  var reset=Object.FindAnyObjectByType<OperationsResetController>();var flow=GameFlowManager.Instance;var camera=Camera.main;
  var health=Object.FindAnyObjectByType<PlayerHealth>();var hud=Object.FindAnyObjectByType<MissionHudController>(FindObjectsInactive.Include);var spawner=Object.FindAnyObjectByType<MeteorSpawner>();
  Object.FindAnyObjectByType<MeteorDefenseVR.EyeTracking.GazeRaycaster>().enabled=false;router.InputSuspended=true;
  string output="Docs/DeliveryQA/integrated-ui-20261027/EditorImpact";Directory.CreateDirectory(output);
  var notes=new List<string>{"Editor current working source; exact source recorded in enclosing report. Explicit StartMainGame/scenario placement; real pooled meteor movement and collision damage. No direct health assignment, ShowNotice call or simulated damage callback. Not public executable or natural full-round evidence."};
  foreach(var size in new[]{new Vector2Int(800,600),new Vector2Int(1024,768),new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(2560,1080)}){
   reset.ResetExperience();float deadline=Time.realtimeSinceStartup+35;while((director.IsBootPresenting||!Object.FindAnyObjectByType<ReadyPrewarmController>().IsComplete)&&Time.realtimeSinceStartup<deadline)yield return null;
   Object.FindAnyObjectByType<DifficultyManager>().Select(DifficultyLevel.Normal);flow.StartMainGame();router.InputSuspended=true;router.Simulation.Recenter();yield return new WaitForSecondsRealtime(2);
   spawner.BeginScenario(1);Assert.AreEqual(100,health.CurrentHealth);int damageEvents=0;void Damage(int amount,float remaining){damageEvents++;notes.Add($"{size}: damage={amount}; remaining={remaining}");}health.DamageTaken+=Damage;
   using(var probe=new PcStabilityProbe(camera,size.x,size.y)){
    var meteor=spawner.SpawnScenario(spawner.ShipCenter+Vector3.forward*25,Vector3.back*35,1,75);Assert.NotNull(meteor);
    deadline=Time.realtimeSinceStartup+5;while(health.CurrentHealth==100&&Time.realtimeSinceStartup<deadline)yield return null;
    Assert.AreEqual(25,health.CurrentHealth);Assert.AreEqual(25,hud.CurrentHealth);Assert.AreEqual(1,damageEvents);Assert.AreEqual(75,health.TotalDamageTaken);
    yield return new WaitForSecondsRealtime(.2f);
    var notice=Object.FindObjectsByType<TextMeshPro>().Single(t=>t.name=="FlightNotice");Assert.IsTrue(notice.enabled);Assert.AreEqual("방어막이 약해졌어요! 운석을 막아 주세요.",notice.text);Assert.GreaterOrEqual(notice.color.a,.99f);
    bool saved=false;var driver=Object.FindAnyObjectByType<PcStabilityRenderDriver>();string folder=$"{output}/{size.x}x{size.y}";Directory.CreateDirectory(folder);
    void Save(){Canvas.ForceUpdateCanvases();PcStabilityRenderDriver.RenderForProof(camera);probe.SaveGameOnlyProof(folder,router);saved=true;}
    driver.RenderCompleted+=Save;while(!saved)yield return null;driver.RenderCompleted-=Save;
    notice.ForceMeshUpdate();Assert.IsFalse(notice.isTextTruncated);
    var heights=notice.textInfo.characterInfo.Take(notice.textInfo.characterCount).Where(c=>c.isVisible&&char.IsLetterOrDigit(c.character)).Select(c=>camera.WorldToScreenPoint(notice.transform.TransformPoint(c.topRight)).y-camera.WorldToScreenPoint(notice.transform.TransformPoint(c.bottomLeft)).y).ToArray();
    var bounds=TelemetryReadabilityProof.ViewRect(camera,director.CriticalNoticeBounds);Assert.IsFalse(bounds.Overlaps(new Rect(.35f,.35f,.3f,.3f)));Assert.GreaterOrEqual(heights.Min(),17f);
    var overlay=Object.FindAnyObjectByType<GameplayHudOverlayView>();Assert.IsFalse(bounds.Overlaps(TelemetryReadabilityProof.ViewRect(camera,(RectTransform)overlay.FeedbackPanel.transform)),"Critical notice overlaps actual impact feedback");
    foreach(var name in new[]{"HealthPanel","StagePanel","ScorePanel"})Assert.IsFalse(bounds.Overlaps(TelemetryReadabilityProof.ViewRect(camera,(RectTransform)overlay.TelemetryRoot.transform.Find(name))),"Critical notice overlaps telemetry");
    var telemetry=Object.FindAnyObjectByType<MeteorDefenseVR.Visual.CockpitTelemetryPresentation>();foreach(var label in new[]{telemetry.ShipReadout,telemetry.ThreatReadout})Assert.IsFalse(bounds.Overlaps(TelemetryReadabilityProof.ViewRect(camera,(RectTransform)label.transform.parent)),"Critical notice overlaps lower HUD");
    if(notice.fontSharedMaterial.HasProperty("_ZTestMode"))Assert.AreEqual((float)UnityEngine.Rendering.CompareFunction.Always,notice.fontSharedMaterial.GetFloat("_ZTestMode"));Assert.GreaterOrEqual(notice.fontSharedMaterial.renderQueue,4000);Assert.AreEqual(222,notice.GetComponent<MeshRenderer>().sortingOrder);
    File.WriteAllText(folder+"/state.txt",$"health={health.CurrentHealth}; HUD={hud.CurrentHealth}; totalDamage={health.TotalDamageTaken}; damageEvents={damageEvents}; state={flow.CurrentState}; text={notice.text}; tint={notice.color}; minGlyphPixels={heights.Min()}; maxGlyphPixels={heights.Max()}; warningBounds={bounds}; timeScale={Time.timeScale}");
   }
   health.DamageTaken-=Damage;spawner.EndScenario();reset.ResetExperience();
  }
  notes.Add("passed=True");File.WriteAllLines(output+"/verification.txt",notes);
 }
}}
