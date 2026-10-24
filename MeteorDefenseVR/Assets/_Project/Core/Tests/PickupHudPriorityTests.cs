using System.Collections;
using MeteorDefenseVR.PcInput;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
namespace MeteorDefenseVR.Tests {
public sealed class PickupHudPriorityTests {
 [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();}
 [UnityTest,Timeout(180000)]public IEnumerator TopHudCrossingAndModalPriority(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return PickupHudPriorityProof.Run(System.Environment.GetEnvironmentVariable("PICKUP_HUD_OUTPUT")??"Docs/DeliveryQA/pickup-hud-priority-20261024/Before",System.Environment.GetEnvironmentVariable("PICKUP_HUD_VALIDATE")=="1");}
}}
