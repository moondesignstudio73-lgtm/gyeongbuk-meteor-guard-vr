using System.Collections;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
namespace MeteorDefenseVR.Tests
{
    public sealed class IntroTransitionReviewTests
    {
        [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();}
        [UnityTest,Timeout(600000)]public IEnumerator AuditAdjacentTransitions()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return Verify();
        }
        private static IEnumerator Verify()
        {
            yield return null;yield return null;
            var router=Object.FindAnyObjectByType<GazeInputRouter>();
            bool passed=false;yield return IntroTransitionProof.Run(router,"Docs/DeliveryQA/intro-transition-review-20261021/After",true,v=>passed=v);
            Assert.That(passed,Is.True,"Inspect transition evidence and input gates.");
        }
    }
}
