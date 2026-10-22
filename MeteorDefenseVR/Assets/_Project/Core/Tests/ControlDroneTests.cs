using System.Collections;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
namespace MeteorDefenseVR.Tests
{
    public sealed class ControlDroneTests
    {
        [UnityTearDown] public IEnumerator Cleanup(){Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();}
        [UnityTest,Timeout(300000)] public IEnumerator NonCombatGesturesAndCancellation()
        {EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return Verify();}
        private static IEnumerator Verify()
        {
            yield return null;yield return null;
            var director=Object.FindAnyObjectByType<SfPresentationDirector>();
            float until=Time.realtimeSinceStartup+30;while(director.IsBootPresenting&&Time.realtimeSinceStartup<until)yield return null;
            bool passed=false;yield return ControlDroneProof.Run(Object.FindAnyObjectByType<GazeInputRouter>(),"Docs/DeliveryQA/control-drone-20261022/Final",v=>passed=v);
            Assert.That(passed,Is.True,"See control drone render evidence.");
            passed=false;yield return ControlReturnScanProof.Run(Object.FindAnyObjectByType<GazeInputRouter>(),"Docs/DeliveryQA/control-return-scan-20261022/Editor",v=>passed=v);
            Assert.That(passed,Is.True,"See return scan evidence.");
        }
    }
}
