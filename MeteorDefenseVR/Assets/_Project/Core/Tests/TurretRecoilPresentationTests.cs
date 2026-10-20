using System.Collections;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeteorDefenseVR.Tests
{
    public sealed class TurretRecoilPresentationTests
    {
        [UnityTearDown] public IEnumerator Cleanup() { Time.timeScale = 1; if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest, Timeout(150000)] public IEnumerator ActualShotRecoilAndReturn()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity"); yield return new EnterPlayMode();
            yield return Verify();
        }
        private static IEnumerator Verify()
        {
            var router = Object.FindAnyObjectByType<GazeInputRouter>(); router.InputSuspended = true;
            yield return null; yield return null;
            var director = Object.FindAnyObjectByType<SfPresentationDirector>(FindObjectsInactive.Include);
            float deadline = Time.realtimeSinceStartup + 30;
            while (director.IsBootPresenting && Time.realtimeSinceStartup < deadline) yield return null;
            bool passed = false;
            yield return TurretRecoilProof.Run(router, "Docs/DeliveryQA/turret-recoil-20261021/After", true, result => passed = result);
            Assert.That(passed, Is.True);
        }
    }
}
