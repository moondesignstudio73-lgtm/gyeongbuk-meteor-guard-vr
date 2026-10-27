using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Visual;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeteorDefenseVR.Tests
{
    public sealed class ThreatDirectionReviewTests
    {
        [UnityTearDown] public IEnumerator Cleanup()
        { Time.timeScale=1; if(Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest, Timeout(240000)] public IEnumerator DirectionTrace()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");
            yield return new EnterPlayMode();
            yield return ThreatDirectionProof.Run("Docs/DeliveryQA/threat-direction-20261027/Final");
        }

    }
}
