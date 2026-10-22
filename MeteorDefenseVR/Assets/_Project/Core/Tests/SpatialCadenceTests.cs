using System.Collections;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
namespace MeteorDefenseVR.Tests
{
    public sealed class SpatialCadenceTests
    {
        [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;AudioListener.pause=false;if(Application.isPlaying)yield return new ExitPlayMode();}
        [UnityTest,Timeout(180000)]public IEnumerator RealApproachCadenceAndCleanup()
        {EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return Verify();}
        private static IEnumerator Verify()
        {yield return new WaitForSecondsRealtime(3);bool passed=false;yield return SpatialCadenceProof.Run("Docs/DeliveryQA/spatial-cadence-20261022/After",true,p=>passed=p);Assert.That(passed,Is.True);}
        [TestCase(2f)][TestCase(2.5f)][TestCase(3f)]
        public void DifficultyBoundedMonotonic(float baseline)
        {
            float previous=1.5f;
            for(float t=0;t<=20;t+=.1f){float gap=ThreatIndicatorView.SpatialInterval(t,baseline);Assert.That(gap,Is.InRange(1.5f,baseline));Assert.That(gap,Is.GreaterThanOrEqualTo(previous));previous=gap;}
        }
    }
}
