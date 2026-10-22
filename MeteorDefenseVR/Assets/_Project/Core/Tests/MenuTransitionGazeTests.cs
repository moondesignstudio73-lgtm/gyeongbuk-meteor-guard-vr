using System.Collections;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
namespace MeteorDefenseVR.Tests
{
    public sealed class MenuTransitionGazeTests
    {
        [UnityTearDown]public IEnumerator Cleanup(){AudioListener.pause=false;Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();}
        [UnityTest,Timeout(180000)]public IEnumerator ObserveCurrentTransitions()
        {EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return Verify();}
        private static IEnumerator Verify()
        {yield return new WaitForSecondsRealtime(3);bool passed=false;yield return MenuTransitionGazeProof.Run("Docs/DeliveryQA/menu-transition-gaze-20261023/After",true,p=>passed=p);Assert.That(passed,Is.True);}
    }
}
