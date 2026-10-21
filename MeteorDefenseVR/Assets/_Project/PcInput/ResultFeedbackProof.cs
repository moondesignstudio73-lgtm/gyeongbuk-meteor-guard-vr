using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Visual;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.PcInput
{
    public static class ResultFeedbackProof
    {
        public static IEnumerator Run(GazeInputRouter router, string output, bool validate, Action<bool> complete)
        {
            var notes = new List<string> { "Explicit result snapshots in real scene, not earned gameplay; no human/HMD input." };
            bool passed = true;
            var flow = GameFlowManager.Instance;
            var result = Object.FindAnyObjectByType<ResultController>();
            var reset = Object.FindAnyObjectByType<OperationsResetController>();
            var lobby = Object.FindAnyObjectByType<HangarLobbyController>();
            lobby.Ranking.Initialize(flow,result,new LocalRankingStore(null));
            reset.ResetExperience(); flow.ShowResult(); reset.CancelResultTimer();
            yield return new WaitForSecondsRealtime(1.7f);
            var camera = Camera.main;
            var display = camera.transform.Find("ResultText").GetComponent<WorldTextPresentation>().Display;
            string original = display.text;
            float timeScale = Time.timeScale; Time.timeScale = 0;
            try
            {
                var settings = result.RankSettings;
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(800,600),new Vector2Int(2560,1080)})
                {
                    using var proof = new PcStabilityProbe(camera,size.x,size.y);
                    camera.GetComponent<WorldUiSafeArea>().Apply();
                    var actions=Object.FindAnyObjectByType<ResultActionsView>();actions?.Present();
                    foreach(int score in new[]{0,settings.RankBScore-1,settings.RankBScore,settings.RankBScore+1,settings.RankAScore-1,settings.RankAScore,settings.RankAScore+1,settings.RankSScore-1,settings.RankSScore,settings.RankSScore+1,int.MaxValue})
                    foreach(bool failed in new[]{false,true})
                    {
                        var snapshot = new GameSessionSnapshot { Score=score,DestroyedMeteors=score==0?0:score==int.MaxValue?int.MaxValue:21,Shots=score==0?0:24,SuccessfulHits=score==0?0:24,DamageTaken=failed?100:0,MissionFailed=failed,AllStagesCleared=!failed };
                        display.text=WorldTextPresentation.FormatResult(snapshot,settings.Evaluate(snapshot),settings);
                        display.ForceMeshUpdate(true,true);Canvas.ForceUpdateCanvases();PcStabilityRenderDriver.RenderForProof(camera);
                        string folder=Path.Combine(output,$"{size.x}x{size.y}",$"{score}-{(failed?"failed":"won")}");Directory.CreateDirectory(folder);proof.SaveGameOnlyProof(folder,router);
                        var glyphs=display.textInfo.characterInfo.Take(display.textInfo.characterCount).Where(c=>c.isVisible).ToArray();
                        float pixels=glyphs.Where(c=>char.IsLetterOrDigit(c.character)).Min(c=>Mathf.Abs(camera.WorldToScreenPoint(display.transform.TransformPoint(c.topLeft)).y-camera.WorldToScreenPoint(display.transform.TransformPoint(c.bottomLeft)).y));
                        bool valid=!display.isTextTruncated && display.textBounds.min.x>=display.rectTransform.rect.xMin-.01f && display.textBounds.max.x<=display.rectTransform.rect.xMax+.01f && display.textBounds.size.y<=display.rectTransform.rect.height+.01f && pixels>=12;
                        for(int line=0;line<display.textInfo.lineCount-1;line++)
                        {
                            var row=glyphs.Where(c=>c.lineNumber==line).ToArray();var next=glyphs.Where(c=>c.lineNumber==line+1).ToArray();
                            if(row.Length>0&&next.Length>0)valid &= row.Min(c=>c.bottomLeft.y)>next.Max(c=>c.topLeft.y);
                        }
                        foreach(var c in glyphs)
                        foreach(var p in new[]{c.bottomLeft,c.topRight})
                        {var v=camera.WorldToViewportPoint(display.transform.TransformPoint(p));valid &= v.x>=.02f&&v.x<=.98f&&v.y>=.02f&&v.y<=.98f;}
                        string parsed=display.GetParsedText();valid &= parsed.Contains(score.ToString("N0"))&&parsed.Contains(snapshot.DestroyedMeteors.ToString("N0"))&&parsed.Contains(snapshot.AccuracyPercent+"%");
                        valid &= parsed.Contains(ResultFeedback.Achievement(snapshot))&&parsed.Contains(settings.NextGoal(snapshot));
                        if(failed)valid &= !parsed.Contains("지구를 지켜냈")&&!parsed.Contains("임무를 마쳤");
                        if(actions!=null)
                        foreach(var label in new[]{actions.Replay,actions.Menu}.Select(b=>b.GetComponentInChildren<TMPro.TextMeshPro>()))
                        {
                            label.ForceMeshUpdate(true,true);var cg=label.textInfo.characterInfo.Take(label.textInfo.characterCount).Where(c=>c.isVisible);
                            float bottom=1,top=0;
                            foreach(var c in cg){var b=camera.WorldToViewportPoint(label.transform.TransformPoint(c.bottomLeft));var t=camera.WorldToViewportPoint(label.transform.TransformPoint(c.topRight));valid &= b.x>=.02f&&t.x<=.98f&&b.y>=.02f&&t.y<=.98f&&(t.y-b.y)*size.y>=12;bottom=Mathf.Min(bottom,b.y);top=Mathf.Max(top,t.y);}
                            float textBottom=glyphs.Min(c=>camera.WorldToViewportPoint(display.transform.TransformPoint(c.bottomLeft)).y);
                            valid &= top<textBottom&&!label.isTextTruncated;
                            var buttonCorners=new Vector3[4];((RectTransform)label.transform.parent).GetWorldCorners(buttonCorners);
                            valid &= buttonCorners.Max(p=>camera.WorldToViewportPoint(p).y)<textBottom-.005f;
                        }
                        if(validate)passed &= valid;
                        notes.Add($"{size}/{score}/{failed} pixels={pixels:F2} bounds={display.textBounds} valid={valid} text={parsed.Replace('\n','|')}");
                    }
                    yield return null;
                }
            }
            finally{Time.timeScale=timeScale;display.text=original;display.ForceMeshUpdate(true,true);camera.GetComponent<WorldUiSafeArea>().Apply();Directory.CreateDirectory(output);notes.Add("passed="+passed);File.WriteAllLines(Path.Combine(output,"verification.txt"),notes);}
            if(validate)
            {
                var stats=Object.FindAnyObjectByType<GameSessionStats>();
                var actions=Object.FindAnyObjectByType<ResultActionsView>();
                for(int i=0;i<6;i++)
                {
                    reset.ResetExperience();stats.RecordMeteorDestroyed();stats.SetScore(1499);flow.ShowResult();reset.CancelResultTimer();
                    yield return new WaitForSecondsRealtime(.5f);
                    passed &= display.GetParsedText().Contains("운석 1개 격파!")&&display.GetParsedText().Contains("다음 B등급까지 1점");
                    if(i%2==0)actions.Menu.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();else actions.Replay.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    bool accepted=i%2==0?lobby.IsMenuReady:lobby.Phase==HangarLobbyController.LobbyPhase.ReplayPreparing;
                    passed &= accepted&&!result.IsVisible;
                    notes.Add($"result UI click at0.5s before reveal ends cycle{i} accepted={accepted}");
                    reset.ResetExperience();flow.ShowResult();reset.CancelResultTimer();yield return new WaitForSecondsRealtime(.1f);
                    bool fresh=display.GetParsedText().Contains("첫 격파에 도전해요!")&&!display.GetParsedText().Contains("운석 1개 격파!");passed &= fresh;notes.Add("restart clears achievement="+fresh);
                }
                for(int i=0;i<30;i++){reset.ResetExperience();flow.ShowResult();passed &= lobby.ChooseFromResult(i%2==0);}
                passed &= PcStabilityProbe.InspectSubscriptions().duplicateCallbacks==0;
                notes.Add("30 immediate result actions; duplicate subscriptions="+PcStabilityProbe.InspectSubscriptions().duplicateCallbacks);
                reset.ResetExperience();notes.Add("final passed="+passed);File.WriteAllLines(Path.Combine(output,"verification.txt"),notes);
            }
            complete(passed);
        }
    }
}

