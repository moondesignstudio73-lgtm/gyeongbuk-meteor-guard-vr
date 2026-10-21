using System.Collections;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
namespace MeteorDefenseVR.Tests
{
    public sealed class PausePriorityTests
    {
        [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();}
        [UnityTest,Timeout(300000)]public IEnumerator PauseOwnsPresentationAndRestoresRemainingTimeAcrossFiveSizes()
        {EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return Verify();}
        private static IEnumerator Verify()
        {
            var router=Object.FindAnyObjectByType<GazeInputRouter>();router.InputSuspended=true;yield return null;yield return null;
            var warm=Object.FindAnyObjectByType<ReadyPrewarmController>();float deadline=Time.realtimeSinceStartup+30;
            while(!warm.IsComplete&&Time.realtimeSinceStartup<deadline)yield return null;
            bool passed=false;yield return PausePriorityProof.Run(router,"Docs/DeliveryQA/pause-priority-20261021/After",v=>passed=v);Assert.That(passed,Is.True);
        }
    }
}
