using System.Collections;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.PcInput;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
namespace MeteorDefenseVR.Tests {
public sealed class ExperienceTipTests {

 [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();System.Environment.SetEnvironmentVariable("TIP_QA_SCENARIO",null);}
 IEnumerator Run(string scenario,int w,int h){EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");System.Environment.SetEnvironmentVariable("TIP_QA_SCENARIO",scenario);yield return new EnterPlayMode();yield return ExperienceTipProof.Run((System.Environment.GetEnvironmentVariable("TIP_QA_OUTPUT")??"Docs/DeliveryQA/experience-tip-20261024/Before")+"/"+w+"x"+h+"/"+scenario,scenario,w,h,System.Environment.GetEnvironmentVariable("TIP_QA_VALIDATE")=="1");}
 [UnityTest,Timeout(120000)]public IEnumerator LockedWide()=>Run("locked-default",2560,1080);
 [UnityTest,Timeout(120000)]public IEnumerator LobbyWide()=>Run("lobby-select",2560,1080);
 [UnityTest,Timeout(120000)]public IEnumerator Locked800()=>Run("locked-default",800,600);
 [UnityTest,Timeout(120000)]public IEnumerator Locked1280()=>Run("locked-default",1280,720);
 [UnityTest,Timeout(120000)]public IEnumerator Select800()=>Run("start-select",800,600);
 [UnityTest,Timeout(120000)]public IEnumerator Select1280()=>Run("start-select",1280,720);
 [UnityTest,Timeout(120000)]public IEnumerator Lobby800()=>Run("lobby-select",800,600);
 [UnityTest,Timeout(120000)]public IEnumerator Lobby1280()=>Run("lobby-select",1280,720);
}}


