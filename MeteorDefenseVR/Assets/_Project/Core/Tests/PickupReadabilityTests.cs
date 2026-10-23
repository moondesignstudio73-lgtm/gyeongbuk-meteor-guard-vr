using System.Collections;
using MeteorDefenseVR.PcInput;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
namespace MeteorDefenseVR.Tests {
public sealed class PickupReadabilityTests {
 [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();}
 [UnityTest,Timeout(180000)]public IEnumerator DestructionPhasesAndAcquisition(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return PickupReadabilityProof.Run(System.Environment.GetEnvironmentVariable("PICKUP_QA_OUTPUT")??"Docs/DeliveryQA/pickup-explosion-readability-20261024/After");}
}}
