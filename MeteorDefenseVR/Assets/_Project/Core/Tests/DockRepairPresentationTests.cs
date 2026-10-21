using System.Collections;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
namespace MeteorDefenseVR.Tests
{
    public sealed class DockRepairPresentationTests
    {
        [UnityTearDown] public IEnumerator Cleanup(){Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();}
        [UnityTest,Timeout(240000)]public IEnumerator DockRepairAndEarlyDeparture()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return Verify();
        }
        private static IEnumerator Verify()
        {
            var router=Object.FindAnyObjectByType<GazeInputRouter>();router.InputSuspended=true;yield return null;yield return null;
            var director=Object.FindAnyObjectByType<SfPresentationDirector>(FindObjectsInactive.Include);
            float deadline=Time.realtimeSinceStartup+30;while(director.IsBootPresenting && Time.realtimeSinceStartup<deadline)yield return null;
            bool passed=false;yield return DockRepairProof.Run(router,"Docs/DeliveryQA/dock-repair-20261021/After",value=>passed=value);Assert.That(passed,Is.True);
        }
    }
}
