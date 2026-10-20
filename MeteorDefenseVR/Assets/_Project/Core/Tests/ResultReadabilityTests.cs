using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Visual;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.Tests
{
    public sealed class ResultReadabilityTests
    {
        private const string Evidence = "Docs/DeliveryQA/result-values-20261021/After";
        [UnityTearDown] public IEnumerator Cleanup()
        { Time.timeScale = 1; if (Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest, Timeout(150000)]
        public IEnumerator FullIntegerValuesKeepReadableSizeAcrossFiveResolutions()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            var router = Object.FindAnyObjectByType<GazeInputRouter>();
            router.InputSuspended = true;
            var director = Object.FindAnyObjectByType<SfPresentationDirector>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (director.IsBootPresenting && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.IsBootPresenting, Is.False);
            var flow = GameFlowManager.Instance;
            var results = Object.FindAnyObjectByType<ResultController>();
            Object.FindAnyObjectByType<HangarLobbyController>().Ranking.Initialize(flow, results, new LocalRankingStore(null));
            flow.ShowResult(); yield return new WaitForSecondsRealtime(1.7f);
            Time.timeScale = 0;
            var camera = Camera.main;
            var display = camera.transform.Find("ResultText").GetComponent<WorldTextPresentation>().Display;
            var notes = new List<string> { "Real scene; explicit result fixtures; no human/HMD input." };
            foreach (var size in new[] { new Vector2Int(1920,1080), new Vector2Int(1280,720),
                new Vector2Int(1024,768), new Vector2Int(800,600), new Vector2Int(2560,1080) })
            {
                using var proof = new PcStabilityProbe(camera, size.x, size.y);
                camera.GetComponent<WorldUiSafeArea>().Apply();
                foreach (int score in new[] { 0, 138940, int.MaxValue })
                foreach (bool failed in new[] { false, true })
                {
                    display.text = WorldTextPresentation.FormatResult(new GameSessionSnapshot {
                        Score=score, DestroyedMeteors=int.MaxValue, Shots=int.MaxValue, SuccessfulHits=int.MaxValue,
                        MissionFailed=failed, Difficulty=DifficultyLevel.Experience, AllStagesCleared=!failed }, "S");
                    display.ForceMeshUpdate(true, true); camera.Render();
                    string folder = $"{Evidence}/{size.x}x{size.y}/{score}-{(failed?"failed":"won")}";
                    Directory.CreateDirectory(folder); proof.SaveGameOnlyProof(folder, router);
                    float scoreSize = MinDigitSize(display, 1), countSize = MinDigitSize(display, 2);
                    float pixels = MinDigitHeight(display, camera, 1);
                    notes.Add($"{size} score={score}, failed={failed}, scorePointSize={scoreSize}, countPointSize={countSize}, scoreDigitPixels={pixels}, bounds={display.textBounds}, rect={display.rectTransform.rect}, text={display.GetParsedText().Replace('\n','|')}");
                    Assert.That(scoreSize, Is.EqualTo(countSize).Within(.001f), "Score must not shrink as it gains digits");
                    Assert.That(scoreSize, Is.GreaterThanOrEqualTo(2.3f));
                    Assert.That(pixels, Is.GreaterThanOrEqualTo(16), $"{size} full score glyph height");
                    Assert.That(display.GetParsedText(), Does.Contain(score.ToString("N0")));
                    Assert.That(display.GetParsedText(), Does.Contain(int.MaxValue.ToString("N0")));
                    Assert.That(display.GetParsedText(), Does.Contain("100%"));
                    Assert.That(display.isTextTruncated, Is.False);
                    Assert.That(display.textBounds.min.x, Is.GreaterThanOrEqualTo(display.rectTransform.rect.xMin-.01f));
                    Assert.That(display.textBounds.max.x, Is.LessThanOrEqualTo(display.rectTransform.rect.xMax+.01f));
                    Assert.That(display.textBounds.size.y, Is.LessThanOrEqualTo(display.rectTransform.rect.height+.01f));
                    // TMP line boxes may touch; visible glyphs must have a real gap.
                    var glyphs = display.textInfo.characterInfo.Take(display.textInfo.characterCount).Where(c=>c.isVisible);
                    for (int line=0; line<display.textInfo.lineCount-1; line++)
                        Assert.That(glyphs.Where(c=>c.lineNumber==line).Min(c=>c.bottomLeft.y),
                            Is.GreaterThan(glyphs.Where(c=>c.lineNumber==line+1).Max(c=>c.topLeft.y)), "Result glyph rows overlap");
                    var corners = new Vector3[4]; display.rectTransform.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        Vector3 point = camera.WorldToViewportPoint(corner);
                        Assert.That(point.x, Is.InRange(.02f,.98f));
                        Assert.That(point.y, Is.InRange(.02f,.98f));
                    }
                }
            }
            Directory.CreateDirectory(Evidence); File.WriteAllLines(Path.Combine(Evidence,"measurements.txt"),notes);
        }
        private static float MinDigitSize(TMP_Text text, int line) => text.textInfo.characterInfo
            .Take(text.textInfo.characterCount).Where(c=>c.lineNumber==line&&char.IsDigit(c.character)).Min(c=>c.pointSize);
        private static float MinDigitHeight(TMP_Text text, Camera camera, int line) => text.textInfo.characterInfo
            .Take(text.textInfo.characterCount).Where(c=>c.lineNumber==line&&char.IsDigit(c.character))
            .Min(c=>Mathf.Abs(camera.WorldToScreenPoint(text.transform.TransformPoint(c.topLeft)).y-camera.WorldToScreenPoint(text.transform.TransformPoint(c.bottomLeft)).y));
    }
}
