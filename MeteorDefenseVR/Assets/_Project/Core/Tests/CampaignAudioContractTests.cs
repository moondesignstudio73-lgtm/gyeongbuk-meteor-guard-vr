using System.Collections;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
namespace MeteorDefenseVR.Tests
{
 public sealed class CampaignAudioContractTests
 {
  [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;AudioListener.pause=false;if(Application.isPlaying)yield return new ExitPlayMode();}
  [UnityTest,Timeout(360000)]public IEnumerator TenStagesWithOriginalCompletionHandler(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return Verify();}
  private static IEnumerator Verify(){bool passed=false;yield return CampaignAudioContractProof.Run("Docs/DeliveryQA/campaign-audio-20261022/After",p=>passed=p);Assert.That(passed,Is.True);}
 }
}
