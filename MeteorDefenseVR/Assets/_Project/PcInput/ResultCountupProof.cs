using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Visual;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.PcInput
{
    public static class ResultCountupProof
    {
        public static IEnumerator Run(GazeInputRouter router, string output, Action<bool> complete)
        {
            var notes = new List<string> { "Synthetic score/failure snapshots and UI button events in real scene; not human/HMD input." };
            bool passed = true;
            var flow = GameFlowManager.Instance;
            var result = Object.FindAnyObjectByType<ResultController>();
            var reset = Object.FindAnyObjectByType<OperationsResetController>();
            var stats = Object.FindAnyObjectByType<GameSessionStats>();
            var lobby = Object.FindAnyObjectByType<HangarLobbyController>();
            var actions = Object.FindAnyObjectByType<ResultActionsView>();
            var camera = Camera.main;
            var presentation = camera.transform.Find("ResultText").GetComponent<WorldTextPresentation>();
            var display = presentation.Display;
            var motion = presentation.GetComponent<WorldTextMotion>();
            lobby.Ranking.Initialize(flow, result, new LocalRankingStore(null));
            Directory.CreateDirectory(output);
            try
            {
                foreach (var size in new[] { new Vector2Int(800,600),new Vector2Int(1024,768),new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(2560,1080) })
                using (var proof = new PcStabilityProbe(camera,size.x,size.y))
                {
                    camera.GetComponent<WorldUiSafeArea>().Apply();
                    foreach (int score in new[] { 0, 138940, int.MaxValue })
                    foreach (bool failed in new[] { false, true })
                    {
                        reset.ResetExperience(); yield return null; yield return null; stats.SetScore(score); flow.ShowResult(); reset.CancelResultTimer();yield return null;yield return null;
                        // Explicit boundary fixture: bypass only the flight delay, not presentation.
                        result.Snapshot.MissionFailed=failed;
                        yield return null; yield return null; string rank=result.Rank;
                        int prior=-1; float began=Time.time;
                        foreach (float age in new[] { .05f, .4f, .85f })
                        {
                            while(Time.time-began<age) yield return null;
                            display.ForceMeshUpdate(true,true); actions.Present(); Canvas.ForceUpdateCanvases();
                            PcStabilityRenderDriver.RenderForProof(camera);
                            string parsed=display.GetParsedText();
                            bool valid=parsed.Contains("점수"+score.ToString("N0")) && result.Snapshot.Score==score && stats.Score==score && result.Rank==rank;
                            var visibleScore=GameObject.Find("ResultScore")?.GetComponent<TMPro.TMP_Text>();
                            var visibleHeading=GameObject.Find("ResultHeading")?.GetComponent<TMPro.TMP_Text>();
                            if(visibleScore!=null)valid &= visibleScore.GetParsedText()==presentation.DisplayedCount.ToString("N0")&&visibleHeading.GetParsedText()==(failed?"다시 도전해요!":"이번 임무의 기록");
                            valid &= presentation.DisplayedCount>=prior && presentation.DisplayedCount<=score;
                            prior=presentation.DisplayedCount;
                            valid &= motion.Opacity>=.999f && motion.VisibleLines==int.MaxValue && actions.Visible;
                            valid &= !display.isTextTruncated && display.textBounds.min.x>=display.rectTransform.rect.xMin-.01f && display.textBounds.max.x<=display.rectTransform.rect.xMax+.01f && display.textBounds.size.y<=display.rectTransform.rect.height+.01f;
                            var glyphs=display.textInfo.characterInfo.Take(display.textInfo.characterCount).Where(c=>c.isVisible).ToArray();
                            valid &= glyphs.All(c=>c.character!='□' && display.font.HasCharacter(c.character,true));
                            for(int line=0;line<display.textInfo.lineCount-1;line++)
                            {
                                var row=glyphs.Where(c=>c.lineNumber==line).ToArray();var next=glyphs.Where(c=>c.lineNumber==line+1).ToArray();
                                if(row.Length>0&&next.Length>0)valid &= row.Min(c=>c.bottomLeft.y)>next.Max(c=>c.topLeft.y);
                            }
                            foreach(var c in glyphs)
                            foreach(var p in new[]{c.bottomLeft,c.topRight})
                            {var v=camera.WorldToViewportPoint(display.transform.TransformPoint(p));valid &= v.x>=.02f&&v.x<=.98f&&v.y>=.02f&&v.y<=.98f;}
                            if(age<.8f) valid &= presentation.ScoreCountupActive && parsed.StartsWith("집계 중 ");
                            else valid &= !presentation.ScoreCountupActive && presentation.DisplayedCount==score && parsed.StartsWith(failed?"다시 도전해요!":"이번 임무의 기록");
                            string folder=Path.Combine(output,$"{size.x}x{size.y}-{score}-{failed}-{age:F2}");
                            Directory.CreateDirectory(folder);proof.SaveGameOnlyProof(folder,router);
                            notes.Add($"{size}/{score}/{failed}/{age} count={presentation.DisplayedCount} valid={valid} text={parsed.Replace('\n','|')}");passed &= valid;
                        }
                    }
                }
                camera.GetComponent<WorldUiSafeArea>().Apply();
                // Keep the batch renderer alive: headless Editor can defer LateUpdate without a render target.
                using var interactionProof = new PcStabilityProbe(camera,1280,720);
                for(int i=0;i<6;i++)
                {
                    reset.ResetExperience();yield return null;yield return null;stats.SetScore(int.MaxValue);flow.ShowResult();reset.CancelResultTimer();yield return null;yield return null;
                    float until=Time.time+.1f;while(Time.time<until)yield return null;
                    bool counting=presentation.ScoreCountupActive;
                    actions.Present();
                    (i%2==0?actions.Menu:actions.Replay).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    bool accepted=i%2==0?lobby.IsMenuReady:lobby.Phase==HangarLobbyController.LobbyPhase.ReplayPreparing;
                    yield return null; passed &= counting&&accepted&&!result.IsVisible&&!presentation.ScoreCountupActive;
                    notes.Add($"UI button event at0.1s cycle={i} counting={counting} accepted={accepted} count={presentation.DisplayedCount}");
                }
                reset.ResetExperience();yield return null;yield return null;stats.SetScore(138940);flow.ShowResult();reset.CancelResultTimer();yield return null;yield return null;
                float pauseUntil=Time.time+.1f;while(Time.time<pauseUntil)yield return null;
                Time.timeScale=0;yield return null;int frozen=presentation.DisplayedCount;
                yield return new WaitForSecondsRealtime(.3f);
                bool paused=presentation.DisplayedCount==frozen&&presentation.ScoreCountupActive;passed &= paused;
                notes.Add("scaled-time pause freezes count="+paused);Time.timeScale=1;
                var overlay=Object.FindAnyObjectByType<PcTestOverlay>();
                overlay.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b=>b.name=="LookSettings").onClick.Invoke();
                yield return new WaitForSecondsRealtime(.2f);
                bool modal=overlay.LookSettingsOpen&&!presentation.ScoreCountupActive&&presentation.DisplayedCount==138940;passed &= modal;
                overlay.TryCloseLookSettings();yield return null;
                passed &= !presentation.ScoreCountupActive;notes.Add("settings finishes count without restarting="+modal);
                reset.ResetExperience();yield return null;yield return null;flow.ShowResult();reset.CancelResultTimer();yield return null;yield return null;float freshUntil=Time.time+.1f;while(Time.time<freshUntil)yield return null;
                bool fresh=presentation.DisplayedCount==0&&presentation.ScoreCountupActive&&!display.GetParsedText().Contains("138,940");passed &= fresh;
                notes.Add("reset starts fresh zero count="+fresh);
                passed &= PcStabilityProbe.InspectSubscriptions().duplicateCallbacks==0;
            }
            finally
            {
                Time.timeScale=1;reset.ResetExperience();camera.GetComponent<WorldUiSafeArea>().Apply();
                notes.Add("passed="+passed);File.WriteAllLines(Path.Combine(output,"verification.txt"),notes);
            }
            complete(passed);
        }
    }
}
