using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace MeteorDefenseVR.Tests
{
    public sealed class TitleDeveloperMenuTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest, Timeout(120000)]
        public IEnumerator TitleButton_OpensClosesByMouse_AndStaysHiddenInPublic()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return null;
            yield return VerifyTitleButton();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyTitleButton()
        {
            var previousInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            var previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                var router = Object.FindAnyObjectByType<GazeInputRouter>();
                router.SetMode(PcGazeMode.MouseGaze);
                var configuration = Object.FindAnyObjectByType<ExperienceConfiguration>();
                configuration.Settings.experienceMode = ExperienceMode.Development;
                configuration.Apply();
                yield return WaitForPlayerFrames();
                var overlay = Object.FindAnyObjectByType<PcTestOverlay>();
                var button = overlay.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b => b.name == "DeveloperMenu");
                var panel = overlay.transform.Find("PcTestCanvas/InputPanel").gameObject;
                var rect = button.GetComponent<RectTransform>();
                Canvas.ForceUpdateCanvases();
                Assert.That(button.gameObject.activeInHierarchy, Is.True);
                Assert.That(panel.activeInHierarchy, Is.False, "Title starts with the test panel closed");
                Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(.5f, 0f)));
                Assert.That(rect.pivot, Is.EqualTo(new Vector2(.5f, 0f)));
                Assert.That(Object.FindObjectsByType<EventSystem>().Length, Is.EqualTo(1));
                Assert.That(overlay.GetComponentInChildren<UnityEngine.UI.GraphicRaycaster>(), Is.Not.Null);
                Assert.That(button.GetComponent<UnityEngine.UI.Image>().raycastTarget, Is.True);
                Assert.That(button.GetComponentInChildren<TMP_Text>().raycastTarget, Is.False);
                Assert.That(button.GetComponentInChildren<TMP_Text>().font.HasCharacters("개발자 테스트 UI 닫기"), Is.True);
                Vector2 position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                Assert.That(position.x, Is.EqualTo(Screen.width * .5f).Within(2f));
                Assert.That(position.y, Is.InRange(1f, Screen.height * .12f), "Button belongs at the bottom of the title screen");
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
                Assert.That(hits.Any(hit => hit.gameObject == button.gameObject), Is.True);
                yield return SaveTitleProof("closed", router);
                yield return Click(mouse, position);
                Assert.That(panel.activeInHierarchy, Is.True, "A real pointer click must open the test UI");
                Assert.That(GameFlowManager.Instance.CurrentState, Is.EqualTo(GameState.Boot), "Clicking tools must not start the game");
                Assert.That(button.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("개발자 UI 닫기"));
                yield return SaveTitleProof("open", router);
                for (int i = 0; i < 5; i++)
                {
                    yield return Click(mouse, position);
                    Assert.That(panel.activeInHierarchy, Is.EqualTo(i % 2 != 0), "Exactly one toggle per click");
                }
                Assert.That(overlay.GetComponentsInChildren<UnityEngine.UI.Button>(true).Count(b => b.name == "DeveloperMenu"), Is.EqualTo(1));
                configuration.Settings.experienceMode = ExperienceMode.PublicExperience;
                int publicTransitionFrame = Time.frameCount;
                configuration.Apply(); yield return null; yield return null;
                TestContext.WriteLine($"Public UI check after two EditMode yields: playerFrames={Time.frameCount - publicTransitionFrame}, visible={button.gameObject.activeInHierarchy}");
                yield return WaitForPlayerFrames();
                Assert.That(button.gameObject.activeInHierarchy, Is.False);
                button.onClick.Invoke(); yield return WaitForPlayerFrames();
                Assert.That(panel.activeInHierarchy, Is.False, "Even a direct callback must not expose tools in Public");
                configuration.Settings.experienceMode = ExperienceMode.Operator;
                configuration.Apply(); yield return WaitForPlayerFrames();
                Assert.That(button.gameObject.activeInHierarchy, Is.False);
                configuration.Settings.experienceMode = ExperienceMode.Development;
                configuration.Apply(); yield return WaitForPlayerFrames();
                GameFlowManager.Instance.StartGame(); yield return WaitForPlayerFrames();
                Assert.That(button.gameObject.activeInHierarchy, Is.False, "Developer entry is title-only");
                Object.FindAnyObjectByType<OperationsResetController>().ResetExperience();
                yield return WaitForPlayerFrames();
                Assert.That(button.gameObject.activeInHierarchy, Is.True);
                Assert.That(panel.activeInHierarchy, Is.False, "Reset must not carry an open menu into the title");
                Assert.That(router.Webcam.IsStreaming, Is.False);
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                InputSystem.settings.editorInputBehaviorInPlayMode = previousInput;
                InputSystem.settings.backgroundBehavior = previousBackground;
            }
        }

        private static IEnumerator Click(Mouse mouse, Vector2 position)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return new WaitForSecondsRealtime(.08f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
            yield return new WaitForSecondsRealtime(.08f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return new WaitForSecondsRealtime(.08f);
        }

        private static IEnumerator WaitForPlayerFrames()
        {
            // EditMode test iterator ticks are not necessarily player Update ticks.
            // Check after actual frames, without changing UI state or weakening assertions.
            int targetFrame = Time.frameCount + 2;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.frameCount < targetFrame && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Time.frameCount, Is.GreaterThanOrEqualTo(targetFrame), "Player UI must receive Update frames");
        }

        private static IEnumerator SaveTitleProof(string state, GazeInputRouter router)
        {
            string directory = "Docs/TitleUiQA/" + state;
            Directory.CreateDirectory(directory);
            using (var render = new PcStabilityProbe(Camera.main))
            {
                float deadline = Time.realtimeSinceStartup + 5f;
                while (render.RenderedFrames < 2 && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(render.RenderedFrames, Is.GreaterThanOrEqualTo(2));
                render.SaveGameOnlyProof(directory, router);
            }
            yield return null;
        }
    }
}
