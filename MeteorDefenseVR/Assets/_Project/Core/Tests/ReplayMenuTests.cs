using System.Collections;
using MeteorDefenseVR.PcInput;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
namespace MeteorDefenseVR.Tests {
public sealed class ReplayMenuTests {
[UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();}
[UnityTest]public IEnumerator Baseline(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return ReplayMenuProof.Run("Docs/DeliveryQA/replay-menu-20261027/Before",false);}
[UnityTest]public IEnumerator Gated(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return ReplayMenuProof.Run("Docs/DeliveryQA/replay-menu-20261027/After",true);}
}}
