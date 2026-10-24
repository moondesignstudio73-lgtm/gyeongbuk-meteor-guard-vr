using System.Collections;
using MeteorDefenseVR.PcInput;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
namespace MeteorDefenseVR.Tests {
public sealed class PickupMotionTests {
 [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;Time.captureDeltaTime=0;if(Application.isPlaying)yield return new ExitPlayMode();}
 [UnityTest,Timeout(300000)]public IEnumerator ContinuousCameraBoundary(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return PickupMotionProof.Run(System.Environment.GetEnvironmentVariable("PICKUP_MOTION_OUTPUT")??"Docs/DeliveryQA/pickup-motion-stability-20261024/Before",System.Environment.GetEnvironmentVariable("PICKUP_MOTION_VALIDATE")=="1");}
}}
