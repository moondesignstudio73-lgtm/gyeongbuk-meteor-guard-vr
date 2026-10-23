using System.Collections;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.TestTools;
namespace MeteorDefenseVR.Tests {
public sealed class WarningContrastTests {
 [UnityTearDown] public IEnumerator Cleanup(){Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();}
 [UnityTest,Timeout(180000)] public IEnumerator CaptureWarningPhases(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return WarningContrastProof.Run("Docs/DeliveryQA/warning-contrast-20261023/Verified");}
}}





