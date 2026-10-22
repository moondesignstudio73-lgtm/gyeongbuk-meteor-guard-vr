using System.Collections;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
namespace MeteorDefenseVR.Tests
{
    public sealed class BgmCrossfadeTests
    {
        [UnityTearDown]public IEnumerator Cleanup(){AudioListener.pause=false;Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();}
        [UnityTest,Timeout(180000)]public IEnumerator ActualExistingTracksOverlapAndCleanup()
        {EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return Verify();}
        private static IEnumerator Verify()
        {yield return new WaitForSecondsRealtime(3);bool passed=false;yield return BgmCrossfadeProof.Run("Docs/DeliveryQA/bgm-crossfade-20261022/After",true,p=>passed=p);Assert.That(passed,Is.True);}
    }
}
