using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Visual;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.PcInput
{
    // Opt-in Windows soak preflight. Deliberate abnormal-state injection is outside timed rounds.
    public static class PcStabilityPreflight
    {
        [Serializable] public sealed class Report
        {
            public bool passed, publicUiHidden, publicRejectsF5, pausedKeyboardReset, allocationFallbackVerified;
            public List<string> resetStages = new List<string>();
        }
        public static IEnumerator Run(GazeInputRouter router, Report report)
        {
            var config = Object.FindAnyObjectByType<ExperienceConfiguration>();
            var controls = Object.FindAnyObjectByType<PcTestControls>();
            var flow = GameFlowManager.Instance;
            var oldMode = config.Settings.experienceMode;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                config.Settings.experienceMode = ExperienceMode.Development; config.Apply();
                router.Webcam.ShowWebcamPreview = router.Webcam.ShowFaceDebug = true;
                yield return null;
                config.Settings.experienceMode = ExperienceMode.PublicExperience; config.Apply();
                yield return null; yield return null;
                var ui = Object.FindAnyObjectByType<PcTestOverlay>().GetComponentsInChildren<Transform>(true);
                report.publicUiHidden = !router.Webcam.ShowWebcamPreview && !router.Webcam.ShowFaceDebug && !controls.DebugKeysEnabled
                    && new[] { "InputMenu", "Reset", "Exit", "Keys", "DebugStatus", "WebcamPreview", "StartPC" }
                    .All(name => ui.Any(t => t.name == name && !t.gameObject.activeInHierarchy));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F5));
                yield return null; yield return null;
                report.publicRejectsF5 = flow.CurrentState == GameState.Boot;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                flow.StartMainGame(); yield return null;
                router.InputSuspended = router.CalibrationInProgress = true;
                Time.timeScale = 0; AudioListener.pause = true;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                yield return null; yield return null;
                report.pausedKeyboardReset = flow.CurrentState == GameState.Boot && Time.timeScale == 1
                    && !AudioListener.pause && !router.InputSuspended && !router.CalibrationInProgress;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                controls.ResetExperience();
                foreach (var state in new[] { GameState.EyeCalibration, GameState.Tutorial, GameState.Practice, GameState.Launch, GameState.BossMeteor, GameState.MissionComplete, GameState.Result })
                {
                    flow.ChangeState(state); yield return null;
                    controls.ResetExperience(); controls.ResetExperience();
                    yield return new WaitForSecondsRealtime(2f);
                    var subscriptions = PcStabilityProbe.InspectSubscriptions();
                    if (flow.CurrentState == GameState.Boot && subscriptions.duplicateCallbacks == 0 && subscriptions.coroutineHandles == 0
                        && Object.FindAnyObjectByType<PremiumCombatVfx>().ActiveParticleCount == 0)
                        report.resetStages.Add(state.ToString());
                }
                long before = GC.GetAllocatedBytesForCurrentThread();
                byte[] allocationProof = new byte[4096];
                report.allocationFallbackVerified = GC.GetAllocatedBytesForCurrentThread() - before >= 4096;
                GC.KeepAlive(allocationProof);
                report.passed = report.publicUiHidden && report.publicRejectsF5 && report.pausedKeyboardReset
                    && report.resetStages.Count == 7 && report.allocationFallbackVerified;
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                controls.ResetExperience();
                config.Settings.experienceMode = oldMode; config.Apply();
            }
        }
    }
}
