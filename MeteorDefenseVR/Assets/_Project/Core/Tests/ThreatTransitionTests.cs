using System.Collections;
using MeteorDefenseVR.PcInput;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
namespace MeteorDefenseVR.Tests {
public sealed class ThreatTransitionTests {
[UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();}
[UnityTest]public IEnumerator Baseline(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return ThreatTransitionProof.Run("Docs/DeliveryQA/threat-transition-20261027/Before",false);}
[UnityTest]public IEnumerator Synchronized(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return ThreatTransitionProof.Run("Docs/DeliveryQA/threat-transition-20261027/After",true);}
}}
