using System.Collections;
using MeteorDefenseVR.PcInput;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
namespace MeteorDefenseVR.Tests {
public sealed class PickupMultipleTests {
 [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();}
 [UnityTest,Timeout(180000)]public IEnumerator MultipleDropEdges(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return PickupMultipleProof.Run(System.Environment.GetEnvironmentVariable("PICKUP_MULTI_OUTPUT")??"Docs/DeliveryQA/pickup-multiple-edge-20261024/Before",System.Environment.GetEnvironmentVariable("PICKUP_MULTI_VALIDATE")=="1");}
}}
