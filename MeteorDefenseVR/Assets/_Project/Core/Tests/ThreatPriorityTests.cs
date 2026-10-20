using System.Collections;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeteorDefenseVR.Tests
{
    public sealed class ThreatPriorityTests
    {
        [UnityTearDown] public IEnumerator Cleanup() { Time.timeScale=1; if(Application.isPlaying)yield return new ExitPlayMode(); }
        [UnityTest,Timeout(150000)]
        public IEnumerator ActualVelocityChangesSinglePriorityAndPoolResetClearsIt()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");
            yield return new EnterPlayMode();
            yield return VerifyAfterDomainReload();
        }
        private static IEnumerator VerifyAfterDomainReload()
        {
            var router=Object.FindAnyObjectByType<GazeInputRouter>(); router.InputSuspended=true;
            yield return null; yield return null;
            var director=Object.FindAnyObjectByType<SfPresentationDirector>(FindObjectsInactive.Include);
            float deadline=Time.realtimeSinceStartup+30;
            while(director!=null && director.IsBootPresenting && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(director!=null && !director.IsBootPresenting,Is.True);
            bool passed=false;
            yield return ThreatPriorityProof.Run(router,"Docs/DeliveryQA/threat-priority-20261021/After",true,value=>passed=value);
            Assert.That(passed,Is.True);
        }
    }
}
