using System.Collections;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
namespace MeteorDefenseVR.Tests {
public sealed class PopulatedRecordsTests {
[UnityTearDown] public IEnumerator Cleanup(){Time.timeScale=1;if(Application.isPlaying)yield return new ExitPlayMode();}
[UnityTest,Timeout(180000)] public IEnumerator CapturePopulatedBoundaries(){EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return PopulatedRecordsProof.Run("Docs/DeliveryQA/populated-records-20261023/Before");}
}}
