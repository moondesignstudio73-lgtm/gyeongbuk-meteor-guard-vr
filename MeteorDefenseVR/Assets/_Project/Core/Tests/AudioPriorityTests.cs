using System.Collections;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
namespace MeteorDefenseVR.Tests
{
    public sealed class AudioPriorityTests
    {
        [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;AudioListener.pause=false;if(Application.isPlaying)yield return new ExitPlayMode();}
        [UnityTest,Timeout(180000)]public IEnumerator ExistingEffectsBoundedAndCriticalWarningsAdmitted()
        {EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return Verify();}
        private static IEnumerator Verify()
        {yield return new WaitForSecondsRealtime(3);bool passed=false;yield return AudioPriorityProof.Run("Docs/DeliveryQA/bgm-duck-20261022/Editor",p=>passed=p);Assert.That(passed,Is.True);}
    }
}
