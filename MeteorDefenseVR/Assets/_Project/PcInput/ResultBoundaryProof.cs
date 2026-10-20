using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Visual;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    // Explicit delivery-QA fixtures only; never changes the stored session result.
    public static class ResultBoundaryProof
    {
        public static bool Capture(Camera camera, GazeInputRouter router, string output)
        {
            var display = camera.transform.Find("ResultText").GetComponent<WorldTextPresentation>().Display;
            string original = display.text;
            float previousTimeScale = Time.timeScale;
            var notes = new List<string> { "Explicit result text fixtures in extracted player; not naturally earned scores." };
            bool passed = true;
            Time.timeScale = 0;
            try
            {
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
                        display.ForceMeshUpdate(true, true);
                        Canvas.ForceUpdateCanvases(); PcStabilityRenderDriver.RenderForProof(camera);
                        string folder = Path.Combine(output, $"{size.x}x{size.y}", $"{score}-{(failed?"failed":"won")}");
                        Directory.CreateDirectory(folder); proof.SaveGameOnlyProof(folder, router);
                        var characters = display.textInfo.characterInfo.Take(display.textInfo.characterCount);
                        var digits = characters.Where(c=>c.lineNumber==1 && char.IsDigit(c.character)).ToArray();
                        float pointSize = digits.Min(c=>c.pointSize);
                        float countSize = characters.Where(c=>c.lineNumber==2 && char.IsDigit(c.character)).Min(c=>c.pointSize);
                        float pixels = digits.Min(c=>Mathf.Abs(camera.WorldToScreenPoint(display.transform.TransformPoint(c.topLeft)).y
                            -camera.WorldToScreenPoint(display.transform.TransformPoint(c.bottomLeft)).y));
                        string parsed = display.GetParsedText();
                        bool valid = Mathf.Abs(pointSize-countSize)<.001f && pointSize>=2.3f && pixels>=16
                            && parsed.Contains(score.ToString("N0")) && parsed.Contains(int.MaxValue.ToString("N0"))
                            && parsed.Contains("100%") && !display.isTextTruncated
                            && display.textBounds.min.x>=display.rectTransform.rect.xMin-.01f
                            && display.textBounds.max.x<=display.rectTransform.rect.xMax+.01f
                            && display.textBounds.size.y<=display.rectTransform.rect.height+.01f;
                        var glyphs = characters.Where(c=>c.isVisible);
                        for (int line=0; line<display.textInfo.lineCount-1; line++)
                            valid &= glyphs.Where(c=>c.lineNumber==line).Min(c=>c.bottomLeft.y)
                                > glyphs.Where(c=>c.lineNumber==line+1).Max(c=>c.topLeft.y);
                        var corners = new Vector3[4]; display.rectTransform.GetWorldCorners(corners);
                        foreach (var corner in corners)
                        {
                            var point = camera.WorldToViewportPoint(corner);
                            valid &= point.x>=.02f && point.x<=.98f && point.y>=.02f && point.y<=.98f;
                        }
                        passed &= valid;
                        notes.Add($"{size} score={score} failed={failed} pointSize={pointSize} countSize={countSize} pixels={pixels} passed={valid} text={parsed.Replace('\n','|')}");
                    }
                }
            }
            finally
            {
                display.text=original; display.ForceMeshUpdate(true, true); Time.timeScale=previousTimeScale;
                camera.GetComponent<WorldUiSafeArea>().Apply();
                Directory.CreateDirectory(output); File.WriteAllLines(Path.Combine(output,"measurements.txt"),notes);
            }
            return passed;
        }
    }
}
