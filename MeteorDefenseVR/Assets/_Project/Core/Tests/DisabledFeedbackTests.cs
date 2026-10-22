using System.Collections;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MeteorDefenseVR.Tests
{
    public sealed class DisabledFeedbackTests
    {
        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator DisabledFeedbackRestoresCurrentLabelsAndNeverConfirms()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return Verify();
        }
        private static IEnumerator Verify()
        {
            var root = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(root.transform, false);
            label.text = "선택 유지"; label.fontSize = 28; label.color = Color.cyan;
            var button = root.GetComponent<Button>(); button.targetGraphic = root.GetComponent<Image>();
            var feedback = root.AddComponent<UIInteractionAnimator>(); feedback.SetSelected(true);
            int confirmed = 0; button.onClick.AddListener(() => confirmed++);
            var events = new GameObject("Events", typeof(EventSystem)).GetComponent<EventSystem>();
            for (int cycle = 0; cycle < 3; cycle++)
            {
                Color original = cycle == 0 ? Color.cyan : Color.green;
                label.color = original;
                button.interactable = false; feedback.SetAvailable(false);
                button.OnPointerClick(new PointerEventData(events) { button = PointerEventData.InputButton.Left });
                button.OnSubmit(new BaseEventData(events)); feedback.Confirm(); feedback.SetGaze(true, .5f);
                yield return null;
                Assert.That(confirmed, Is.Zero);
                Assert.That(feedback.State, Is.EqualTo(UIInteractionAnimator.InteractionState.Disabled));
                Assert.That(label.color, Is.Not.EqualTo(original));
                Assert.That(label.fontSize, Is.EqualTo(28)); Assert.That(label.text, Is.EqualTo("선택 유지"));
                root.SetActive(false); Assert.That(label.color, Is.EqualTo(original));
                root.SetActive(true); yield return null;
                Assert.That(feedback.State, Is.EqualTo(UIInteractionAnimator.InteractionState.Disabled));
                button.interactable = true; feedback.SetAvailable(true);
                Assert.That(feedback.State, Is.EqualTo(UIInteractionAnimator.InteractionState.Selected));
                Assert.That(label.color, Is.EqualTo(original), "Restore the latest label color, not a stale first-use color");
            }
            Object.Destroy(root); Object.Destroy(events.gameObject);
        }
    }
}
