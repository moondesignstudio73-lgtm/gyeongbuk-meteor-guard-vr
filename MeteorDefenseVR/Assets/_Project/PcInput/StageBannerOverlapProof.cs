using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.PowerUps;
using MeteorDefenseVR.Progression;
using MeteorDefenseVR.UI;
using TMPro;

using UnityEngine;
using UnityEngine.InputSystem;


using Object=UnityEngine.Object;
namespace MeteorDefenseVR.PcInput {
public static class StageBannerOverlapProof {
private static class ProofAssert {
 public static void IsFalse(bool value,string message="Expected false"){if(value)throw new InvalidOperationException(message);}
 public static void IsTrue(bool value){if(!value)throw new InvalidOperationException("Expected true");}
 public static void AreEqual(int expected,int actual){if(expected!=actual)throw new InvalidOperationException($"Expected {expected}, actual {actual}");}
 public static void GreaterOrEqual(float actual,float min){if(actual<min)throw new InvalidOperationException($"{actual} below {min}");}
 public static void LessOrEqual(float actual,float max){if(actual>max)throw new InvalidOperationException($"{actual} above {max}");}
}
public static IEnumerator Run(string output){
 var router=Object.FindAnyObjectByType<GazeInputRouter>();bool savedInput=router.InputSuspended;router.InputSuspended=true;
 var devices=InputSystem.devices.Where(d=>d.enabled&&(d is Mouse||d is Keyboard)).ToArray();foreach(var d in devices)InputSystem.DisableDevice(d);
 var caster=Object.FindAnyObjectByType<MeteorDefenseVR.EyeTracking.GazeRaycaster>();bool savedCaster=caster.enabled;caster.enabled=false;
 var director=Object.FindAnyObjectByType<SfPresentationDirector>();float deadline=Time.unscaledTime+25;
 while(director.IsBootPresenting&&Time.unscaledTime<deadline)yield return null;ProofAssert.IsFalse(director.IsBootPresenting);
 var flow=GameFlowManager.Instance;var boss=Object.FindAnyObjectByType<BossClimaxController>();
 var progression=StageProgressionController.Instance;var power=PowerUpManager.Instance;
 var health=Object.FindAnyObjectByType<PlayerHealth>();var spawner=Object.FindAnyObjectByType<MeteorSpawner>();
 var hud=Object.FindAnyObjectByType<MissionHudController>(FindObjectsInactive.Include);var reset=Object.FindAnyObjectByType<OperationsResetController>();
 Object.FindAnyObjectByType<LocalRankingController>().Initialize(flow,Object.FindAnyObjectByType<ResultController>(),new LocalRankingStore(null));
 var title=Camera.main.GetComponentsInChildren<TextMeshProUGUI>(true).Single(t=>t.name=="StageTitle");
 var card=title.transform.parent.GetComponent<RectTransform>();
 var advance=typeof(StageProgressionController).GetMethod("AdvanceStage",BindingFlags.Instance|BindingFlags.NonPublic);
 var notes=new List<string>{"Controlled state fixture; provenance is the enclosing Editor test or executable identity, not OS/HMD. Skip earlier stages using existing progression AdvanceStage; stage9 completion callback and final advance use production timers. Max buffs=3. Actual stage10 spawn schedule; no invented concurrent regular threats at boss entry."};
 try {
 foreach(var size in new[]{new Vector2Int(800,600),new Vector2Int(2560,1080)}) {
  reset.ResetExperience();router.InputSuspended=true;yield return new WaitForSecondsRealtime(2);
  Object.FindAnyObjectByType<DifficultyManager>().Select(DifficultyLevel.Normal);flow.StartMainGame();
  for(int n=1;n<9;n++){boss.BeginClimax();boss.ResetClimax();advance.Invoke(progression,null);}
  ProofAssert.AreEqual(9,progression.CurrentStage);spawner.StopSpawning(true);health.ApplyDamage(90);
  power.Activate(PowerUpType.Shield);power.Activate(PowerUpType.DamageBoost);power.Activate(PowerUpType.Overdrive);
  ProofAssert.AreEqual(3,power.ActiveBuffCount);boss.BeginClimax();boss.ResetClimax();ProofAssert.IsTrue(boss.CampaignCompletionHandler(9));
  using var proof=new PcStabilityProbe(Camera.main,size.x,size.y);
  void Save(string state){Canvas.ForceUpdateCanvases();PcStabilityRenderDriver.RenderForProof(Camera.main);var notice=Object.FindObjectsByType<TextMeshPro>().Single(t=>t.name=="FlightNotice");
   if(notice.enabled&&notice.color.a>.1f){notice.ForceMeshUpdate();var a=Camera.main.WorldToViewportPoint(notice.transform.TransformPoint(notice.textBounds.min));var b=Camera.main.WorldToViewportPoint(notice.transform.TransformPoint(notice.textBounds.max));var n=Rect.MinMaxRect(a.x,a.y,b.x,b.y);ProofAssert.IsFalse(director.IsStagePresenting&&n.Overlaps(TelemetryReadabilityProof.ViewRect(Camera.main,card)),"Notice overlaps stage card");bool critical=director.CriticalNoticeBounds!=null;ProofAssert.GreaterOrEqual(n.yMin,critical?.23f:.81f);ProofAssert.LessOrEqual(n.yMax,critical?.31f:.9f);}
   string dir=$"{output}/{size.x}x{size.y}/{state}";Directory.CreateDirectory(dir);proof.SaveGameOnlyProof(dir,router);
   var texts=Object.FindObjectsByType<TMP_Text>().Where(t=>t.isActiveAndEnabled&&!string.IsNullOrEmpty(t.text)).ToArray();
   File.WriteAllLines(Path.Combine(dir,"text.txt"),new[]{$"state={flow.CurrentState};stage={progression.CurrentStage};boss={boss.CurrentStage};buffs={power.ActiveBuffCount};meteors={spawner.ActiveMeteors.Count};stageVisible={director.IsStagePresenting};remaining={director.StageTimeRemaining};feedback={hud.FeedbackMessage}"}.Concat(texts.Select(t=>{t.ForceMeshUpdate();return $"{t.name}: font={t.fontSize} auto={t.enableAutoSizing} rect={TelemetryReadabilityProof.ViewRect(Camera.main,t.rectTransform)} preferred={t.preferredWidth}x{t.preferredHeight} available={t.rectTransform.rect.size} truncated={t.isTextTruncated} text={t.GetParsedText()}";})));
  }
  yield return new WaitForSecondsRealtime(1.1f);Save("stage9-clear-max-buffs");
  deadline=Time.unscaledTime+6;while(progression.CurrentStage!=10&&Time.unscaledTime<deadline)yield return null;ProofAssert.AreEqual(10,progression.CurrentStage);
  yield return new WaitForSecondsRealtime(.22f);Save("stage10-arrival-max-buffs");
  // A surviving pickup can be collected during Playing; acquisition replaces the shared feedback slot.
  power.Activate(PowerUpType.Overdrive);yield return new WaitForSecondsRealtime(.2f);Save("stage10-acquisition");
  yield return new WaitForSecondsRealtime(.5f);Save("stage10-settled");
  health.RestoreCheckpoint(25);yield return new WaitForSecondsRealtime(.1f);Save("stage10-low-health-warning");
  yield return new WaitForSecondsRealtime(.9f);ProofAssert.IsFalse(director.IsStagePresenting);Save("stage10-ended");
  ProofAssert.IsFalse(card.GetComponent<CanvasGroup>().blocksRaycasts);foreach(var graphic in card.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))ProofAssert.IsFalse(graphic.raycastTarget);
  // All regular meteors have resolved before a boss. Remove only fixture regulars, retain carried buffs.
  spawner.StopSpawning(true);boss.BeginClimax();yield return new WaitForSecondsRealtime(.2f);ProofAssert.IsFalse(director.IsStagePresenting);Save("boss-warning-max-buffs");
  yield return new WaitForSecondsRealtime(1.4f);Save("boss-narration-max-buffs");
  yield return new WaitForSecondsRealtime(1.3f);Save("boss-active");
  reset.ResetExperience();yield return new WaitForSecondsRealtime(.2f);ProofAssert.IsFalse(director.IsStagePresenting);Save("reset-cleanup");
  notes.Add(size+": stage10 natural timed arrival/end, boss hides stage, raycasts disabled, reset cleanup passed");
 }
 notes.Add("passed=True");File.WriteAllLines($"{output}/verification.txt",notes);
 } finally { reset.ResetExperience();router.InputSuspended=savedInput;caster.enabled=savedCaster;foreach(var d in devices)InputSystem.EnableDevice(d); }

}
}}
