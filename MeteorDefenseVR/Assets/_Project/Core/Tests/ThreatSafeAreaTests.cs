using System.Collections;
using MeteorDefenseVR.PcInput;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
namespace MeteorDefenseVR.Tests {
public sealed class ThreatSafeAreaTests {
 [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();}
 [UnityTest]public IEnumerator Baseline(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return ThreatSafeAreaProof.Run("Docs/DeliveryQA/threat-safe-area-20261027/Before",false);}
 [UnityTest]public IEnumerator ProtectedLayout(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return ThreatSafeAreaProof.Run("Docs/DeliveryQA/threat-safe-area-20261027/After",true);}
}
public sealed class ThreatSafeRegressionTests {
 [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();}
 [UnityTest]public IEnumerator CriticalWarningClearance(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return ThreatNoticeProof.Run("Docs/DeliveryQA/threat-safe-area-20261027/CriticalRegression",true);}
 [UnityTest]public IEnumerator PriorityAndPoolReuse(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return Priority();}
 private static IEnumerator Priority(){var director=Object.FindAnyObjectByType<SfPresentationDirector>();while(director.IsBootPresenting)yield return null;bool passed=false;yield return ThreatPriorityProof.Run(Object.FindAnyObjectByType<GazeInputRouter>(),"Docs/DeliveryQA/threat-safe-area-20261027/PriorityRegression",true,value=>passed=value);NUnit.Framework.Assert.IsTrue(passed);}
}}
