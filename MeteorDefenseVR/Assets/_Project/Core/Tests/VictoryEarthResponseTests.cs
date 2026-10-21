using System.Collections;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
namespace MeteorDefenseVR.Tests
{
    public sealed class VictoryEarthResponseTests
    {
        [UnityTearDown] public IEnumerator Cleanup(){Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();}
        [UnityTest,Timeout(300000)] public IEnumerator RealBossCompletionAndCancellation()
        {EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return Verify();}
        private static IEnumerator Verify()
        {
            yield return null;yield return null;
            var director=Object.FindAnyObjectByType<SfPresentationDirector>();
            float until=Time.realtimeSinceStartup+30;while(director.IsBootPresenting&&Time.realtimeSinceStartup<until)yield return null;
            bool passed=false;yield return VictoryEarthProof.Run(Object.FindAnyObjectByType<GazeInputRouter>(),"Docs/DeliveryQA/victory-earth-response-20261021/After",true,v=>passed=v);
            Assert.That(passed,Is.True,"See victory timeline evidence.");
        }
    }
}
