using System.Collections;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.TestTools;
namespace MeteorDefenseVR.Tests {
public sealed class ThreatNoticeTests {
 [UnityTearDown] public IEnumerator Cleanup(){Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();}
 [UnityTest,Timeout(180000)] public IEnumerator UpperEdgeIndicatorKeepsWarningClear(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return ThreatNoticeProof.Run("Docs/DeliveryQA/threat-notice-overlap-20261023/Verified",true);}
 [UnityTest,Timeout(180000)] public IEnumerator ExistingStageAndThreatPriorityRegression(){
  EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();
  yield return RegressionAfterEnter();
 }
 private static IEnumerator RegressionAfterEnter(){
  yield return StageBannerOverlapProof.Run("Docs/DeliveryQA/threat-notice-overlap-20261023/StageRegressionVerified");
  var director=Object.FindAnyObjectByType<SfPresentationDirector>();float deadline=Time.unscaledTime+15;while(director.IsBootPresenting&&Time.unscaledTime<deadline)yield return null;
  var router=Object.FindAnyObjectByType<GazeInputRouter>();bool passed=false;
  yield return ThreatPriorityProof.Run(router,"Docs/DeliveryQA/threat-notice-overlap-20261023/ThreatRegressionVerified",true,v=>passed=v);Assert.That(passed,Is.True);
 }
}}
