using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeteorDefenseVR.Tests
{
    public sealed class CalibrationColorTests
    {
        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [Test]
        public void RingStaysRedUntilAcceptedAndResetsWithoutNewObjects()
        {
            var root = new GameObject("QA_CalibrationCanvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var ring = CalibrationProgressRing.Create(root.transform, 90f);
                Assert.That(ring.GetComponent<CanvasRenderer>(), Is.Not.Null);
                using var mesh = new UnityEngine.UI.VertexHelper();
                var populate = typeof(CalibrationProgressRing).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(UnityEngine.UI.VertexHelper) }, null);
                foreach (float progress in new[] { 0f, .25f, .5f, .999f, 1f })
                {
                    ring.Present(progress, false);
                    Assert.That(ring.color, Is.EqualTo(CalibrationProgressRing.ActiveRed));
                    Assert.That(ring.Progress, Is.EqualTo(progress));
                    populate.Invoke(ring, new object[] { mesh });
                    Assert.That(mesh.currentVertCount, Is.EqualTo(256 + Mathf.CeilToInt(progress * 64) * 4));
                }
                ring.Present(1f, true);
                Assert.That(ring.color, Is.EqualTo(Color.green));
                for (int i = 0; i < 30; i++) { ring.Present(0, false); ring.Present(.5f, false); ring.Present(1, true); }
                ring.Present(0, false);
                Assert.That(ring.color, Is.EqualTo(CalibrationProgressRing.ActiveRed));
                Assert.That(ring.Progress, Is.Zero);
                Assert.That(root.GetComponentsInChildren<CalibrationProgressRing>().Length, Is.EqualTo(1));
                Assert.That(ring.raycastTarget, Is.False, "Visual must not intercept gaze or mouse input");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator AuthoredVrPointsAndPcOverlayFollowRedProgressGreenComplete()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return VerifyVisuals();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyVisuals()
        {
            yield return new WaitForSecondsRealtime(.3f);
            var router = Object.FindAnyObjectByType<GazeInputRouter>();
            router.SetMode(PcGazeMode.MouseGaze); router.InputSuspended = true;
            var warm = Object.FindAnyObjectByType<ReadyPrewarmController>();
            float deadline = Time.realtimeSinceStartup + 20;
            while (!warm.IsComplete && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(warm.IsComplete, Is.True);
            var flow = GameFlowManager.Instance;
            flow.StartCalibration();
            yield return new WaitForSecondsRealtime(.2f);
            var calibration = Object.FindAnyObjectByType<PcCalibrationController>();
            var sequence = Object.FindAnyObjectByType<CalibrationSequenceController>();
            sequence.ResetSequence();
            Set(calibration, "<IsVisible>k__BackingField", false); // The isolated VR visual must not be covered by the PC instruction panel.
            int rings = Object.FindObjectsByType<CalibrationProgressRing>(FindObjectsInactive.Include).Length;
            // The ring renderer is also reused by the hangar menu. Count only calibration-owned visuals here,
            // while retaining the global before/after count below to detect allocations or leaked ring objects.
            var overlay = Object.FindAnyObjectByType<PcTestOverlay>();
            Assert.That(sequence.GetComponentsInChildren<CalibrationProgressRing>(true).Length, Is.EqualTo(5), "Five prepared VR visuals");
            Assert.That(overlay.GetComponentsInChildren<CalibrationProgressRing>(true).Length, Is.EqualTo(1), "One prepared PC visual");
            using var render = new PcStabilityProbe(Camera.main);
            var points = sequence.GetComponentsInChildren<CalibrationPoint>(true);
            Assert.That(points.Length, Is.EqualTo(5));
            foreach (var point in points)
            {
                var renderer = point.GetComponent<Renderer>();
                var block = new MaterialPropertyBlock();
                float dwell = point.DwellDuration;
                int events = 0; point.Completed += _ => events++;
                point.Activate(); point.DecaySpeed = 0; // Freeze this visual fixture, not the production sequence.
                renderer.GetPropertyBlock(block);
                AssertColor(block.GetColor("_BaseColor"), CalibrationProgressRing.ActiveRed, point.name);
                point.Advance(dwell * .5f);
                yield return new WaitForSecondsRealtime(.22f);
                renderer.GetPropertyBlock(block);
                AssertColor(block.GetColor("_BaseColor"), CalibrationProgressRing.ActiveRed, point.name);
                var ring = point.GetComponentInChildren<CalibrationProgressRing>();
                Assert.That(ring.Progress, Is.EqualTo(.5f).Within(.001f));
                Assert.That(ring.color, Is.EqualTo(CalibrationProgressRing.ActiveRed));
                if (point.name == "Point_Center") Save(render, router, "vr-progress");
                point.Advance(dwell * .5f);
                yield return new WaitForSecondsRealtime(.12f);
                renderer.GetPropertyBlock(block);
                AssertColor(block.GetColor("_BaseColor"), Color.green, point.name);
                Assert.That(ring.color, Is.EqualTo(Color.green));
                Assert.That(events, Is.EqualTo(1));
                Assert.That(point.DwellDuration, Is.EqualTo(dwell));
                point.ResetPoint(); point.Activate();
                yield return null;
                renderer.GetPropertyBlock(block);
                AssertColor(block.GetColor("_BaseColor"), CalibrationProgressRing.ActiveRed, point.name);
                point.ResetPoint();
            }

            // Synthetic display states only: no webcam, samples, camera image or face data.
            // Existing PcInputTests exercise the actual automatic mouse GameFlow separately.
            calibration.enabled = false;
            Set(calibration, "<IsVisible>k__BackingField", true);
            Set(calibration, "<IsWebcamCalibration>k__BackingField", true);
            Set(calibration, "<Message>k__BackingField", "시선 테스트를 시작합니다.\n화면에 나타나는 점을 바라봐 주세요.");
            var text = overlay.GetComponentsInChildren<TextMeshProUGUI>(true).Single(t => t.name == "CalibrationPoint");
            var pcRing = text.GetComponentInChildren<CalibrationProgressRing>(true);
            float sampleDuration = (float)Get(calibration, "sampleDuration");
            foreach (var state in new[] { (0, 0f, false, "pc-active"), (0, .55f, false, "pc-progress"), (0, 1f, true, "pc-complete"), (1, 0f, false, "pc-next-point") })
            {
                Set(calibration, "<PointIndex>k__BackingField", state.Item1);
                Set(calibration, "stableTime", sampleDuration * state.Item2);
                Set(calibration, "pointCompleteUntil", state.Item3 ? Time.unscaledTime + 10f : 0f);
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(text.gameObject.activeInHierarchy, Is.True);
                Assert.That(text.color, Is.EqualTo(state.Item3 ? Color.green : CalibrationProgressRing.ActiveRed));
                Assert.That(pcRing.color, Is.EqualTo(text.color));
                Assert.That(pcRing.Progress, Is.EqualTo(state.Item2).Within(.001f));
                Assert.That(text.rectTransform.anchorMin, Is.EqualTo(calibration.Target));
                Save(render, router, state.Item4);
            }
            Set(calibration, "completing", true);
            Set(calibration, "<Message>k__BackingField", "시선 인식 완료!");
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(overlay.GetComponentsInChildren<TextMeshProUGUI>().Single(t => t.name == "Instructions").text, Does.StartWith("시선 인식 완료!"));
            Assert.That(flow.CurrentState, Is.EqualTo(GameState.EyeCalibration), "Visuals cannot advance GameFlow");
            Assert.That(router.Webcam.IsStreaming, Is.False);
            Assert.That(Object.FindObjectsByType<CalibrationProgressRing>(FindObjectsInactive.Include).Length, Is.EqualTo(rings));
            calibration.enabled = true;
            Object.FindAnyObjectByType<OperationsResetController>().ResetExperience();
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(text.gameObject.activeInHierarchy, Is.False);
            Assert.That(calibration.IsPointComplete, Is.False);
            Assert.That(flow.CurrentState, Is.EqualTo(GameState.Boot));
        }

        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void AssertColor(Color actual, Color expected, string name)
        {
            for (int i = 0; i < 4; i++) Assert.That(actual[i], Is.EqualTo(expected[i]).Within(.0001f), name);
        }
        private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Save(PcStabilityProbe render, GazeInputRouter router, string stage)
        {
            // Batchmode's explicit Camera.Render does not drive the normal UI pre-render pass.
            // Flush Canvas geometry before capturing; otherwise TMP renders but UI meshes can be absent.
            Canvas.ForceUpdateCanvases();
            foreach (var ring in Object.FindObjectsByType<CalibrationProgressRing>())
            {
                var mesh = ring.canvasRenderer.GetMesh();
                Assert.That(mesh, Is.Not.Null, "Actual CanvasRenderer ring geometry: " + stage);
                Assert.That(mesh.vertexCount, Is.GreaterThanOrEqualTo(256), stage);
                Assert.That(ring.canvasRenderer.cull, Is.False);
            }
            Camera.main.Render();
            string path = "Docs/CalibrationColorQA/" + stage;
            Directory.CreateDirectory(path); render.SaveGameOnlyProof(path, router);
        }
    }
}
