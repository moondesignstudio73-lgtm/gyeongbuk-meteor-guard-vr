using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.PowerUps;
using MeteorDefenseVR.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.Tests
{
    public sealed class AbilityWarningSequenceTests
    {
        [UnityTearDown] public IEnumerator Cleanup() { Time.timeScale = 1; if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest, Timeout(240000)] public IEnumerator ActualAbilitiesAndLowHealthWarning()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");
            yield return new EnterPlayMode(); yield return AbilityWarningSequenceProof.Run("Docs/DeliveryQA/ability-warning-sequence-20261027/Editor");
        }
    }
}
