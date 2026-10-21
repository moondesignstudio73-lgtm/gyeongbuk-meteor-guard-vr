using System.Collections;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
namespace MeteorDefenseVR.Tests
{
    public sealed class AudioControlsTests
    {
        [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;AudioListener.pause=false;if(Application.isPlaying)yield return new ExitPlayMode();}
        [UnityTest,Timeout(240000)]public IEnumerator FourChannelsAndContinuousSettingsInput()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return Verify();
        }
        private static IEnumerator Verify()
        {
            var router=Object.FindAnyObjectByType<GazeInputRouter>();router.SetMode(PcGazeMode.MouseGaze);
            float end=Time.realtimeSinceStartup+30;while(Object.FindAnyObjectByType<SfPresentationDirector>().IsBootPresenting&&Time.realtimeSinceStartup<end)yield return null;
            bool passed=false;yield return AudioControlsProof.Run(router,"Docs/DeliveryQA/audio-controls-20261022/Editor",value=>passed=value);Assert.That(passed,Is.True);
        }
    }
}
