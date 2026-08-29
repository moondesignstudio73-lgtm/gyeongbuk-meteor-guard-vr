using System.Collections;
using System.Linq;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.UI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace MeteorDefenseVR.Tests
{
    public sealed class PublicExperienceInputTests
    {
        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest]
        public IEnumerator PublicHidesOperatorUiIgnoresStageJumpAndAllowsEmergencyReset()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return null;
            yield return VerifyPublicInput();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyPublicInput()
        {
            var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            var backgroundInput = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                Object.FindAnyObjectByType<GazeInputRouter>().SetMode(PcGazeMode.MouseGaze);
                var config = Object.FindAnyObjectByType<ExperienceConfiguration>();
                Assert.That(config.Mode, Is.EqualTo(ExperienceMode.PublicExperience));
                var ui = Object.FindAnyObjectByType<PcTestOverlay>().GetComponentsInChildren<Transform>(true);
                foreach (string name in new[] { "InputMenu", "DeveloperMenu", "Reset", "Exit", "Keys", "MouseFallback" })
                    Assert.That(ui.Single(t => t.name == name).gameObject.activeInHierarchy, Is.False, name);
                var flow = GameFlowManager.Instance;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F5));
                yield return null; yield return null;
                Assert.That(flow.CurrentState, Is.EqualTo(GameState.Boot), "Public must reject developer stage jumps");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                flow.StartMainGame(); yield return null;
                var hud = Object.FindAnyObjectByType<MissionHudController>();
                Assert.That(hud.TotalMeteors, Is.EqualTo(Object.FindAnyObjectByType<MeteorSpawner>().TotalScheduled + 1), "Campaign HUD reserves the 21st boss slot");
                hud.AddScore(100);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                yield return null; yield return null;
                Assert.That(flow.CurrentState, Is.EqualTo(GameState.Boot));
                Assert.That(hud.Score, Is.Zero);
                Assert.That(Object.FindAnyObjectByType<GameSessionStats>().Score, Is.Zero);
                Assert.That(Object.FindAnyObjectByType<ReadyScreenController>().IsReadyVisible, Is.True);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                flow.StartCalibration();
                var calibrationPanel = ui.Single(t => t.name == "Calibration").gameObject;
                // This EditMode test enters Play Mode: editor test ticks do not guarantee
                // a rendered frame. Begin runs in LateUpdate and the panel on the next Update.
                float panelDeadline = Time.realtimeSinceStartup + 2f;
                while (!calibrationPanel.activeInHierarchy && Time.realtimeSinceStartup < panelDeadline)
                    yield return null;
                Assert.That(flow.CurrentState, Is.EqualTo(GameState.EyeCalibration));
                Assert.That(Object.FindAnyObjectByType<PcCalibrationController>().IsVisible, Is.True);
                Assert.That(calibrationPanel.activeInHierarchy, Is.True,
                    "Hiding debug UI must not hide gameplay calibration");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
                InputSystem.settings.backgroundBehavior = backgroundInput;
            }
        }
    }
}
