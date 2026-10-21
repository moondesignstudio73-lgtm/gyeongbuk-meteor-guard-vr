using System.Collections;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
namespace MeteorDefenseVR.Tests
{
    public sealed class AudioLifecycleTests
    {
        [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;AudioListener.pause=false;if(Application.isPlaying)yield return new ExitPlayMode();}
        [UnityTest,Timeout(180000)]public IEnumerator ContinuousWarningsAndPauseTransition()
        {EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return Verify();}
        private static IEnumerator Verify()
        {var router=Object.FindAnyObjectByType<GazeInputRouter>();router.SetMode(PcGazeMode.MouseGaze);yield return new WaitForSecondsRealtime(3);bool passed=false;yield return AudioLifecycleProof.Run(router,"Docs/DeliveryQA/audio-lifecycle-20261022/Editor",p=>passed=p);Assert.That(passed,Is.True);}
    }
}
