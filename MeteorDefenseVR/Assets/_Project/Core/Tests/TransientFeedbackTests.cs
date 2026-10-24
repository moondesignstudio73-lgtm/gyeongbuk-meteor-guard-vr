using System.Collections;
using MeteorDefenseVR.PcInput;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
namespace MeteorDefenseVR.Tests {
public sealed class TransientFeedbackTests {
 [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();}
 [UnityTest,Timeout(240000)]public IEnumerator RealDropFeedbackLifetime(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return TransientFeedbackProof.Run(System.Environment.GetEnvironmentVariable("TRANSIENT_FEEDBACK_OUTPUT")??"Docs/DeliveryQA/transient-feedback-20261024/Before",System.Environment.GetEnvironmentVariable("TRANSIENT_FEEDBACK_VALIDATE")=="1");}
}}
