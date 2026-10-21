using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Audio;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Progression;
using MeteorDefenseVR.Visual;
using TMPro;
using UnityEngine;
using Object=UnityEngine.Object;
namespace MeteorDefenseVR.PcInput
{
    public static class CampaignAudioContractProof
    {
        public static IEnumerator Run(string output,Action<bool> complete)
        {
            Directory.CreateDirectory(output);var notes=new List<string>();var rows=new List<string>{"seconds,stage,phase,state,bgm,source,playing,volume,rms"};bool passed=true;
            var flow=GameFlowManager.Instance;var router=Object.FindAnyObjectByType<GazeInputRouter>();var audio=Object.FindAnyObjectByType<AudioManager>();
            var spawner=Object.FindAnyObjectByType<MeteorSpawner>();var boss=Object.FindAnyObjectByType<BossClimaxController>();
            var campaign=StageProgressionController.Instance;var result=Object.FindAnyObjectByType<ResultController>();var stats=Object.FindAnyObjectByType<GameSessionStats>();
            var telemetry=Object.FindAnyObjectByType<CockpitTelemetryPresentation>();var reset=Object.FindAnyObjectByType<OperationsResetController>();
            var handler=boss.CampaignCompletionHandler;var sources=Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include);var filters=Object.FindObjectsByType<AudioChannelFilter>();
            int resultCount=0,completeCount=0,warningsOutsideCombat=0,damageCalls=0;var starts=new List<int>();var clears=new List<int>();
            float origin=Time.realtimeSinceStartup,nextSample=0;int fixedChecks=0;string lastCapture="";float nextCapture=0;
            void Check(bool value,string message){passed &= value;notes.Add(message+"="+value);}
            void Result(GameSessionSnapshot snapshot,string rank){resultCount++;notes.Add($"RESULT stage={campaign.CurrentStage} count={resultCount} allCleared={stats.AllStagesCleared}");}
            void Started(int stage,StageBalance balance){starts.Add(stage);notes.Add("START "+stage);}
            void Cleared(int stage,int score,int accuracy,int damage){clears.Add(stage);notes.Add("CLEAR "+stage);}
            void Complete(int stage){completeCount++;notes.Add("CAMPAIGN COMPLETE "+stage);}
            void Cue(AudioCue cue,AudioCategory category){notes.Add($"CUE t={Time.realtimeSinceStartup-origin:F3} stage={campaign.CurrentStage} phase={campaign.Phase} state={flow.CurrentState} cue={cue}");if((cue==AudioCue.Warning||cue==AudioCue.BossWarning)&&(flow.CurrentState==GameState.MissionComplete||flow.CurrentState==GameState.Result||flow.CurrentState==GameState.Boot))warningsOutsideCombat++;}
            var recorder=Camera.main.gameObject.AddComponent<CampaignOutputRecorder>();recorder.Begin();
            using var render=new PcStabilityProbe(Camera.main,800,600);
            void Capture(string name)
            {
                Canvas.ForceUpdateCanvases();PcStabilityRenderDriver.RenderForProof(Camera.main);string folder=Path.Combine(output,name);Directory.CreateDirectory(folder);render.SaveGameOnlyProof(folder,router);
                foreach(var label in new[]{telemetry.ShipReadout,telemetry.ThreatReadout})
                {
                    if(!label.gameObject.activeInHierarchy)continue;label.ForceMeshUpdate();
                    bool glyphs=label.GetParsedText().Where(c=>!char.IsControl(c)&&!char.IsWhiteSpace(c)).All(c=>label.font.HasCharacter(c,true));
                    Check(glyphs&&!label.isTextTruncated&&label.textBounds.size.x<=label.rectTransform.rect.width+.1f&&label.textBounds.size.y<=label.rectTransform.rect.height+.1f,"HUD readable "+name+"/"+label.name);
                }
            }
            try
            {
                notes.Add("Real scene in Editor or QA Windows player, default campaign callback retained. Spawner.Tick(3) advances spawn schedule each frame; ReceiveDamage(MaxHealth) accelerates real hits. Boss/transition/result timers remain real-time. No direct success state or completion callback invocation. No human listening/HMD; source RMS timeline is not final speaker recording.");
                result.ResultShown+=Result;campaign.StageStarted+=Started;campaign.StageCleared+=Cleared;campaign.CampaignCompleted+=Complete;audio.CuePlaybackRequested+=Cue;
                foreach(var f in filters){f.Measure=true;f.ResetMeasurement();}
                router.SetMode(PcGazeMode.MouseGaze);reset.ResetExperience();yield return new WaitForSecondsRealtime(3);
                Object.FindAnyObjectByType<DifficultyManager>().Select(DifficultyLevel.Normal);
                flow.StartMainGame(); // Start only; every subsequent stage and success transition comes from real damage/events.
                float deadline=Time.realtimeSinceStartup+240;
                while(!result.IsVisible&&Time.realtimeSinceStartup<deadline)
                {
                    if(flow.CurrentState==GameState.Playing){spawner.Tick(3);foreach(var meteor in spawner.ActiveMeteors.ToArray())if(meteor.IsTargetable){meteor.ReceiveDamage(meteor.MaxHealth);damageCalls++;}}
                    if(boss.ActiveBoss!=null&&boss.ActiveBoss.IsTargetable){boss.ActiveBoss.ReceiveDamage(boss.ActiveBoss.MaxHealth);damageCalls++;}
                    if(boss.CampaignCompletionHandler!=handler){Check(false,"campaign handler unchanged");break;}
                    if(Time.realtimeSinceStartup>=nextSample)
                    {
                        nextSample=Time.realtimeSinceStartup+.1f;
                        foreach(var source in sources)rows.Add(FormattableString.Invariant($"{Time.realtimeSinceStartup-origin:F3},{campaign.CurrentStage},{campaign.Phase},{flow.CurrentState},{audio.TargetBgmCue},{source.name},{source.isPlaying},{source.volume},{source.GetComponent<AudioChannelFilter>()?.OutputRms??0}"));
                        AudioCue? expected=GameAudioEventRouter.BgmForState(flow.CurrentState);if(audio.TargetBgmCue!=expected)Check(false,"BGM target matches state "+flow.CurrentState);
                        if(flow.CurrentState==GameState.Playing||flow.CurrentState==GameState.BossMeteor)
                            foreach(var label in new[]{telemetry.ShipReadout,telemetry.ThreatReadout}){var rect=(RectTransform)label.transform.parent;fixedChecks++;if(Mathf.Abs(rect.anchorMin.y-.03f)>.0001f)Check(false,"fixed HUD anchor");}
                    }
                    string key=$"stage-{campaign.CurrentStage:D2}-{campaign.Phase}-{flow.CurrentState}";
                    if(key!=lastCapture){lastCapture=key;nextCapture=Time.realtimeSinceStartup+.3f;}
                    if(nextCapture>0&&Time.realtimeSinceStartup>=nextCapture){Capture(key);nextCapture=0;}
                    yield return null;
                }
                Check(result.IsVisible,"natural final result visible");Check(starts.SequenceEqual(Enumerable.Range(1,10)),"stages 1 through 10 in order");Check(clears.SequenceEqual(Enumerable.Range(1,9)),"nine intermediate clears in order");
                Check(completeCount==1&&resultCount==1&&stats.AllStagesCleared&&stats.ClearedStages==10,"one final victory and one result after ten stages");
                Capture("final-result");yield return new WaitForSecondsRealtime(2);Capture("final-result-settled");
                var display=Camera.main.transform.Find("ResultText").GetComponent<WorldTextPresentation>().Display;display.ForceMeshUpdate(true,true);
                Check(!display.isTextTruncated&&display.textBounds.size.x<=display.rectTransform.rect.width+.1f&&display.textBounds.size.y<=display.rectTransform.rect.height+.1f,"final result text fits");
                Check(display.GetParsedText().Where(c=>!char.IsWhiteSpace(c)&&!char.IsControl(c)).All(c=>display.font.HasCharacter(c,true)),"final result glyphs present");
                notes.Add("Final result text: "+display.GetParsedText().Replace('\n','|'));
                Check(resultCount==1,"no duplicate result");Check(warningsOutsideCombat==0,"no warning events after combat");
                Check(Object.FindAnyObjectByType<HangarLobbyController>().ChooseFromResult(true),"actual replay action accepted");
                deadline=Time.realtimeSinceStartup+35;while(flow.CurrentState!=GameState.Playing&&Time.realtimeSinceStartup<deadline)yield return null;
                Check(flow.CurrentState==GameState.Playing&&campaign.CurrentStage==1&&!stats.AllStagesCleared&&stats.ClearedStages==0&&!result.IsVisible,"replay resets to stage one");
                Capture("replay-stage-one");Check(boss.CampaignCompletionHandler==handler,"original campaign handler retained throughout");notes.Add($"damageCalls={damageCalls} fixedHudSamples={fixedChecks}");
            }
            finally
            {
                result.ResultShown-=Result;campaign.StageStarted-=Started;campaign.StageCleared-=Cleared;campaign.CampaignCompleted-=Complete;audio.CuePlaybackRequested-=Cue;
                foreach(var f in filters)if(f!=null)f.Measure=false;reset.ResetExperience();notes.Add(recorder.Save(Path.Combine(output,"listener-output.wav")));Object.Destroy(recorder);notes.Add("passed="+passed);File.WriteAllLines(Path.Combine(output,"verification.txt"),notes);File.WriteAllLines(Path.Combine(output,"source-timeline.csv"),rows);
            }
            complete(passed);
        }
    }
}
