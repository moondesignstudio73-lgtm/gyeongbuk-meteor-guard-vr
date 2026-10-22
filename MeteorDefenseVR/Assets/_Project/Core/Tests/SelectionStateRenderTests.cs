using System.Collections;
using MeteorDefenseVR.PcInput;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
namespace MeteorDefenseVR.Tests
{
    public sealed class SelectionStateRenderTests
    {
        [UnityTearDown] public IEnumerator Cleanup(){AudioListener.pause=false;Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();}
        [UnityTest,Timeout(180000)] public IEnumerator CaptureUncoveredStates()
        {EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return SelectionStateProof.Run("Docs/DeliveryQA/selection-state-readability-20261023/After");}
    }
}
