using System.Collections;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
namespace MeteorDefenseVR.Tests
{
    public sealed class ResultCountupTests
    {
        [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();}
        [UnityTest,Timeout(300000)]public IEnumerator CountupBoundariesAndEarlyActions()
        {EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return Verify();}
        private static IEnumerator Verify()
        {
            var router=Object.FindAnyObjectByType<GazeInputRouter>();router.InputSuspended=true;yield return null;yield return null;
            var warm=Object.FindAnyObjectByType<ReadyPrewarmController>();float deadline=Time.realtimeSinceStartup+30;
            while(!warm.IsComplete&&Time.realtimeSinceStartup<deadline)yield return null;
            bool passed=false;yield return ResultCountupProof.Run(router,System.Environment.GetEnvironmentVariable("RESULT_COUNTUP_OUTPUT")??"Docs/DeliveryQA/result-countup-20261022/After",v=>passed=v);Assert.That(passed,Is.True);
        }
    }
}
