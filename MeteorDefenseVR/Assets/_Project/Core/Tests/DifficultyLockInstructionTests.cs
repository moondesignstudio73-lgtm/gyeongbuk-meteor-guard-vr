using System.Collections;
using MeteorDefenseVR.PcInput;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
namespace MeteorDefenseVR.Tests {
public sealed class DifficultyLockInstructionTests {
 [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();}
 [UnityTest,Timeout(240000)]public IEnumerator LockInstructionAndGaze(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return DifficultyLockInstructionProof.Run("Docs/DeliveryQA/difficulty-lock-instruction-20261024/EditorAfter");}
}}

