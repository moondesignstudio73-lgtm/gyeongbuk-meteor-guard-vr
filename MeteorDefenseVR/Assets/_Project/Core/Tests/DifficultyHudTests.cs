using System.Collections;
using MeteorDefenseVR.PcInput;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
namespace MeteorDefenseVR.Tests {
public sealed class DifficultyHudTests {
 [UnityTest,Timeout(120000)] public IEnumerator Display800()=>Run(800,600);
 [UnityTest,Timeout(120000)] public IEnumerator Display1280()=>Run(1280,720);
 [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();}
 IEnumerator Run(int w,int h){EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return DifficultyHudProof.Run((System.Environment.GetEnvironmentVariable("HUD_QA_OUTPUT")??"Docs/DeliveryQA/difficulty-hud-20261025/Before")+"/"+w+"x"+h,w,h,System.Environment.GetEnvironmentVariable("HUD_QA_VALIDATE")=="1");}
}}
