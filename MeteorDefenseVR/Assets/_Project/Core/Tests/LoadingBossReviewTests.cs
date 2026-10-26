using System.Collections;
using MeteorDefenseVR.PcInput;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
namespace MeteorDefenseVR.Tests {
public sealed class LoadingBossReviewTests {
 [UnityTest, NUnit.Framework.Timeout(300000)]public IEnumerator LoadingAndActualBossWarnings(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return LoadingBossReviewProof.Run(System.Environment.GetEnvironmentVariable("PRESENTATION_OUTPUT"),System.Environment.GetEnvironmentVariable("PRESENTATION_VALIDATE")=="1");}
 [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();}
}}
