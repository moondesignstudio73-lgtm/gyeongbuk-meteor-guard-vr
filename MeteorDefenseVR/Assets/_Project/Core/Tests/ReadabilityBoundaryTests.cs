using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Progression;
using MeteorDefenseVR.Tutorial;
using MeteorDefenseVR.Visual;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.Tests
{
    public sealed class ReadabilityBoundaryTests
    {
        private const string Evidence = "Docs/DeliveryQA/readability-20261021/After";
        private static readonly Vector2Int[] Sizes = {
            new Vector2Int(1920,1080), new Vector2Int(1280,720), new Vector2Int(1024,768),
            new Vector2Int(800,600), new Vector2Int(2560,1080) };

        [UnityTearDown] public IEnumerator Cleanup()
        { Time.timeScale = 1; if (Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest, Timeout(180000)]
        public IEnumerator CaptureSupportedBoundariesAndRealPracticeTarget()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            var router = Object.FindAnyObjectByType<GazeInputRouter>();
            router.SetMode(PcGazeMode.MouseGaze); router.InputSuspended = true;
            var director = Object.FindAnyObjectByType<SfPresentationDirector>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (director.IsBootPresenting && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.IsBootPresenting, Is.False);
            var camera = Camera.main;
            var flow = GameFlowManager.Instance;
            var lobby = Object.FindAnyObjectByType<HangarLobbyController>();
            var results = Object.FindAnyObjectByType<ResultController>();
            lobby.Ranking.Initialize(flow, results, new LocalRankingStore(null));
            var measurements = new List<string> { "Diagnostic renders; state/score/focus fixtures, not physical player input." };
            flow.StartPractice();
            yield return new WaitForSecondsRealtime(.3f);
            var practice = Object.FindAnyObjectByType<PracticeController>();
            var hud = Object.FindAnyObjectByType<PracticeHudSafeOverlay>();
            var meteor = practice.ActiveMeteor;
            Assert.That(meteor, Is.Not.Null);
            foreach (var size in Sizes)
            {
                using var proof = new PcStabilityProbe(camera, size.x, size.y);
                foreach (bool behindPanel in new[] { false, true })
                {
                    Vector3 aim = meteor.transform.position - camera.transform.position;
                    float yaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
                    float pitch = -Mathf.Atan2(aim.y, new Vector2(aim.x, aim.z).magnitude) * Mathf.Rad2Deg;
                    // Put the real practice meteor at the existing instruction band's center.
                    float offset = behindPanel ? Mathf.Atan(.58f * Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad)) * Mathf.Rad2Deg : 0;
                    router.Simulation.Recenter();
                    router.Simulation.ApplyLook(new Vector2(yaw/.1f, -(pitch+offset)/.09f), 1);
                    yield return new WaitForSecondsRealtime(.15f);
                    // Show the production ring as a visual fixture without firing or moving the meteor.
                    var target = meteor.GetComponent<GazeLockOnTarget>();
                    target.LockOnDuration = 10;
                    target.OnGazeEnter(default); target.OnGazeStay(default, .2f);
                    Canvas.ForceUpdateCanvases(); camera.Render();
                    Vector3 point = camera.WorldToViewportPoint(meteor.transform.position);
                    measurements.Add($"{size} target={point} instruction={ScreenRect(hud.SafeStatus.rectTransform, camera)}");
                    Rect targetBounds = TargetBounds(meteor.gameObject, camera);
                    Assert.That(ScreenRect((RectTransform)hud.SafeStatus.transform.parent, camera).Overlaps(targetBounds), Is.False,
                        $"{size}: instruction must clear the real meteor and aim ring bounds {targetBounds}");
                    Assert.That(ScreenRect((RectTransform)hud.SafeText.transform.parent, camera).Overlaps(targetBounds), Is.False,
                        $"{size}: telemetry must clear the real meteor and aim ring");
                    Assert.That(hud.SafeText.fontSize, Is.EqualTo(32));
                    Assert.That(hud.SafeStatus.fontSize, Is.EqualTo(40));
                    Assert.That(hud.SafeStatus.preferredWidth, Is.LessThanOrEqualTo(hud.SafeStatus.rectTransform.rect.width + .1f));
                    Save(proof, router, $"{size.x}x{size.y}/practice-{(behindPanel ? "panel-crossing" : "center")}");
                    var images = hud.SafeCanvas.GetComponentsInChildren<UnityEngine.UI.Image>();
                    foreach (var image in images) image.enabled = false;
                    camera.Render();
                    Save(proof, router, $"{size.x}x{size.y}/practice-{(behindPanel ? "panel-crossing" : "center")}-without-backing");
                    foreach (var image in images) image.enabled = true;
                    target.OnGazeExit();
                }
            }
            flow.StartMainGame();
            yield return new WaitForSecondsRealtime(.2f);
            var title = camera.GetComponentsInChildren<TextMeshProUGUI>(true).Single(t => t.name == "StageTitle");
            var subtitle = camera.GetComponentsInChildren<TextMeshProUGUI>(true).Single(t => t.name == "StageSubtitle");
            foreach (var size in Sizes)
            {
                using var proof = new PcStabilityProbe(camera, size.x, size.y);
                yield return null; Time.timeScale = 0;
                title.text = $"스테이지 {StageProgressionController.Instance.TotalStages}";
                subtitle.text = "새로운 운석 무리에 대비하세요";
                title.transform.parent.gameObject.SetActive(true);
                title.transform.parent.GetComponent<CanvasGroup>().alpha = 1;
                title.transform.parent.localScale = Vector3.one;
                Describe(title, camera, measurements, size + " stage");
                Describe(subtitle, camera, measurements, size + " stage subtitle");
                Assert.That(title.isTextTruncated || subtitle.isTextTruncated, Is.False);
                Assert.That(subtitle.preferredWidth, Is.LessThanOrEqualTo(subtitle.rectTransform.rect.width));
                Canvas.ForceUpdateCanvases(); camera.Render();
                Save(proof, router, $"{size.x}x{size.y}/last-stage-longest-copy");
                Time.timeScale = 1;
            }
            flow.ShowResult();
            yield return new WaitForSecondsRealtime(1.7f);
            Time.timeScale = 0;
            var resultText = camera.transform.Find("ResultText").GetComponent<WorldTextPresentation>().Display;
            foreach (var size in Sizes)
            {
                using var proof = new PcStabilityProbe(camera, size.x, size.y);
                camera.GetComponent<WorldUiSafeArea>().Apply();
                foreach (int score in new[] { 138940, int.MaxValue })
                {
                    resultText.text = WorldTextPresentation.FormatResult(new GameSessionSnapshot {
                        Score = score, DestroyedMeteors = 210, MissionFailed = true,
                        Difficulty = DifficultyLevel.Experience, PlayTime = 86400, MaxStage = 10 }, "S");
                    Describe(resultText, camera, measurements, size + " result " + score);
                    Assert.That(resultText.isTextTruncated, Is.False);
                    Assert.That(resultText.textBounds.max.x, Is.LessThanOrEqualTo(resultText.rectTransform.rect.xMax + .01f));
                    camera.Render(); Save(proof, router, $"{size.x}x{size.y}/result-{score}");
                }
            }
            Time.timeScale = 1;
            lobby.BeginReturn(); yield return new WaitForSecondsRealtime(3.3f);
            lobby.Queue(HangarLobbyController.MenuAction.Ranking);
            yield return new WaitForSecondsRealtime(.3f);
            var store = new LocalRankingStore(null);
            Assert.That(store.Add(LocalMissionRecord.FromResult(new GameSessionSnapshot {
                Score = int.MaxValue, PlayTime = 86400, DestroyedMeteors = 210, MaxCombo = 210,
                MaxStage = 10, Difficulty = DifficultyLevel.Experience }, "S",
                Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow)), Is.True);
            lobby.View.Records.Present(store, HangarRecordView.RecordTab.Recent);
            foreach (var size in Sizes)
            {
                using var proof = new PcStabilityProbe(camera, size.x, size.y);
                yield return null;
                foreach (var text in Object.FindObjectsByType<TextMeshPro>())
                    if (text.transform.parent.name == "RecentRecord")
                    {
                        Describe(text, camera, measurements, size + " record " + text.name);
                        Assert.That(text.isTextTruncated, Is.False, text.name);
                        Assert.That(text.preferredWidth, Is.LessThanOrEqualTo(text.rectTransform.rect.width), text.name);
                        if (text.name == "Metric4") Assert.That(text.text, Does.Contain("24:00:00"));
                        if (text.name == "Score") Assert.That(text.text, Does.Contain("2,147,483,647"));
                    }
                camera.Render(); Save(proof, router, $"{size.x}x{size.y}/record-max-score-24hours");
            }
            Directory.CreateDirectory(Evidence);
            File.WriteAllLines(Path.Combine(Evidence, "measurements.txt"), measurements);
        }

        private static void Describe(TMP_Text text, Camera camera, List<string> output, string label)
        {
            text.ForceMeshUpdate(true, true);
            float minPixels = float.MaxValue;
            float minDigitPixels = float.MaxValue;
            foreach (var c in text.textInfo.characterInfo.Take(text.textInfo.characterCount))
            {
                if (!c.isVisible) continue;
                float height = Mathf.Abs(camera.WorldToScreenPoint(text.transform.TransformPoint(c.topLeft)).y
                    - camera.WorldToScreenPoint(text.transform.TransformPoint(c.bottomLeft)).y);
                minPixels = Mathf.Min(minPixels, height);
                if (char.IsDigit(c.character)) minDigitPixels = Mathf.Min(minDigitPixels, height);
            }
            output.Add($"{label}: rect={text.rectTransform.rect}; preferred={text.preferredWidth}x{text.preferredHeight}; bounds={text.textBounds}; minGlyphPixels={minPixels}; minDigitPixels={minDigitPixels}; truncated={text.isTextTruncated}; text={text.GetParsedText().Replace('\n','|')}");
        }
        private static Rect TargetBounds(GameObject target, Camera camera)
        {
            Vector2 min = Vector2.one * float.MaxValue, max = Vector2.one * float.MinValue;
            foreach (var renderer in target.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer is ParticleSystemRenderer) continue;
                Bounds bounds = renderer.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i&1)==0?-1:1, (i&2)==0?-1:1, (i&4)==0?-1:1));
                    Vector3 point = camera.WorldToViewportPoint(corner);
                    if (point.z <= 0) continue;
                    min = Vector2.Min(min, point); max = Vector2.Max(max, point);
                }
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private static Rect ScreenRect(RectTransform rect, Camera camera)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector3 a = camera.WorldToViewportPoint(corners[0]), b = camera.WorldToViewportPoint(corners[2]);
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }
        private static void Save(PcStabilityProbe proof, GazeInputRouter router, string label)
        { string path = Path.Combine(Evidence, label); Directory.CreateDirectory(path); proof.SaveGameOnlyProof(path, router); }
    }
}
