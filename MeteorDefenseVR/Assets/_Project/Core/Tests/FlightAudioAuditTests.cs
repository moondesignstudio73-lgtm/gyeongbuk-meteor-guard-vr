using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Audio;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Launch;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Player;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace MeteorDefenseVR.Tests
{
    public sealed class FlightAudioAuditTests
    {
        [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;AudioListener.pause=false;if(Application.isPlaying)yield return new ExitPlayMode();}
        [UnityTest,Timeout(180000)]public IEnumerator ExistingFlightStagesAndAudioTimeline()
        {EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");yield return new EnterPlayMode();yield return Audit();}
        private static IEnumerator Audit()
        {
            const string folder="Docs/DeliveryQA/flight-audio-audit-20261022";Directory.CreateDirectory(folder);
            var flow=GameFlowManager.Instance;var launch=Object.FindAnyObjectByType<LaunchSequenceController>();var cinema=Object.FindAnyObjectByType<BaseFlightCinematic>();
            var audio=Object.FindAnyObjectByType<AudioManager>();var reset=Object.FindAnyObjectByType<OperationsResetController>();var result=Object.FindAnyObjectByType<ResultController>();var health=Object.FindAnyObjectByType<PlayerHealth>();
            var router=Object.FindAnyObjectByType<GazeInputRouter>();var sources=Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include);
            var notes=new List<string>();var rows=new List<string>{"time,case,state,launch,shot,door,engine,speed,returnProgress,bgm"};var recorder=Camera.main.gameObject.AddComponent<CampaignOutputRecorder>();recorder.Begin();
            string scenario="setup";float origin=Time.realtimeSinceStartup;bool passed=true;int countdown=0,engine=0,thrust=0,doorRequests=0;
            void Log(string message)=>notes.Add($"t={Time.realtimeSinceStartup-origin:F3} case={scenario} state={flow.CurrentState} launch={launch.CurrentStage} shot={cinema.CurrentShot} {message}");
            void Check(bool value,string message){passed &= value;Log(message+"="+value);}
            void Cue(AudioCue cue,AudioCategory category){Log("PLAY "+cue);if(cue==AudioCue.Countdown)countdown++;if(cue==AudioCue.EngineStart)engine++;if(cue==AudioCue.Launch)thrust++;}
            void Requested(LaunchAudioCue cue){Log("REQUEST "+cue);if(cue.ToString()=="Door")doorRequests++;}
            void Stage(LaunchStage stage,string message)=>Log("STAGE "+stage);
            IEnumerator Observe(string name,float seconds){scenario=name;float end=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<end){rows.Add(FormattableString.Invariant($"{Time.realtimeSinceStartup-origin:F3},{scenario},{flow.CurrentState},{launch.CurrentStage},{cinema.CurrentShot},{cinema.DoorOpening},{launch.EngineAmount},{launch.SpeedAmount},{cinema.ReturnProgress},{audio.TargetBgmCue}"));yield return new WaitForSecondsRealtime(.05f);}}
            IEnumerator Until(Func<bool> ready,float seconds){float end=Time.realtimeSinceStartup+seconds;while(!ready()&&Time.realtimeSinceStartup<end)yield return Observe(scenario,.05f);Check(ready(),"condition reached");}
            try
            {
                notes.Add("Audit only. Default authored flight timings; actual stage/request/playback events. Return geometry phase boundaries are inferred from the same animation's ReturnProgress, not independent playback timers. Controlled state/health inputs; no human listening/HMD.");
                audio.CuePlaybackRequested+=Cue;launch.AudioCueRequested+=Requested;launch.StageChanged+=Stage;
                router.SetMode(PcGazeMode.MouseGaze);yield return Observe("boot",3);reset.ResetExperience();yield return Observe("reset",2);
                scenario="departure";flow.StartLaunch();yield return Until(()=>launch.CurrentStage==LaunchStage.EngineStart,8);
                router.Simulation.Pause();var stage=launch.CurrentStage;float amount=launch.EngineAmount;yield return Observe("departure-paused",.5f);
                Check(router.Simulation.IsPaused&&launch.CurrentStage==stage&&launch.EngineAmount==amount,"departure phase freezes with pause");router.Simulation.Resume();
                scenario="departure-resumed";yield return Until(()=>flow.CurrentState==GameState.Playing,15);
                Check(doorRequests==1&&engine==1&&thrust==2&&countdown>=3,"existing departure cue counts (launch and mission-start share electronic cue)");
                yield return Observe("combat",.5f);health.RestoreCheckpoint(health.MaxHealth);health.ApplyImpactDamage(Mathf.RoundToInt(health.MaxHealth),health.transform.position);
                Check(cinema.CurrentShot==BaseFlightCinematic.Shot.Return&&result.IsPresentationPending,"failure starts return before result");
                Check(!Object.FindAnyObjectByType<HangarLobbyController>().ChooseFromResult(true),"early replay rejected while return pending");
                yield return Observe("return-approach",.8f);Time.timeScale=0;AudioListener.pause=true;float progress=cinema.ReturnProgress;yield return Observe("return-controlled-pause",.4f);
                Check(cinema.ReturnProgress==progress,"return animation freezes with controlled pause");Time.timeScale=1;AudioListener.pause=false;
                scenario="return-resumed";yield return Until(()=>result.IsVisible,7);Check(cinema.HasReturned&&cinema.DoorOpening<.001f,"result waits for landing and door close");
                yield return Observe("failed-result",1);Check(audio.TargetBgmCue==AudioCue.BgmResult,"result BGM selected");
                reset.ResetExperience();yield return Observe("cancel-setup",2);flow.StartLaunch();scenario="cancel-departure";yield return Until(()=>launch.CurrentStage==LaunchStage.EngineStart,7);reset.ResetExperience();yield return Observe("cancel-departure-after",1.2f);
                Check(cinema.CurrentShot==BaseFlightCinematic.Shot.None&&!launch.IsRunning&&!sources.Any(s=>s.isPlaying&&s.loop&&!audio.OwnsAmbientSource(s)&&!audio.OwnsBgmSource(s)),"cancel departure clears shot and effect loops");
                flow.StartMainGame();yield return Observe("failure-cancel-setup",.3f);health.RestoreCheckpoint(health.MaxHealth);health.ApplyImpactDamage(Mathf.RoundToInt(health.MaxHealth),health.transform.position);yield return Observe("cancel-return",.5f);reset.ResetExperience();yield return Observe("cancel-return-after",1.2f);
                Check(!result.IsPresentationPending&&!result.IsVisible&&cinema.CurrentShot==BaseFlightCinematic.Shot.None,"return cancellation clears pending result");
                flow.CompleteMission();scenario="controlled-success";yield return Until(()=>result.IsVisible,7);Check(cinema.CurrentShot==BaseFlightCinematic.Shot.None&&!cinema.HasReturned,"success does not play failed return");
                Check(Object.FindAnyObjectByType<HangarLobbyController>().ChooseFromResult(true),"post-result replay accepted");scenario="replay";yield return Until(()=>flow.CurrentState==GameState.Playing,25);Check(audio.TargetBgmCue==AudioCue.BgmBattle,"replay restores battle BGM");
            }
            finally
            {
                audio.CuePlaybackRequested-=Cue;launch.AudioCueRequested-=Requested;launch.StageChanged-=Stage;router.Simulation.Resume();Time.timeScale=1;AudioListener.pause=false;reset.ResetExperience();
                Log(recorder.Save(Path.Combine(folder,"listener-output.wav")));Object.Destroy(recorder);notes.Add("existing-routing-and-cleanup-passed="+passed);notes.Add("Detailed door/approach/landing audio and real radio/AI speech remain incomplete: no suitable dedicated assets. Existing Launch/Countdown are electronic cues, not spoken countdown or continuous thrust audio.");File.WriteAllLines(Path.Combine(folder,"verification.txt"),notes);File.WriteAllLines(Path.Combine(folder,"visual-audio-timeline.csv"),rows);
            }
            Assert.That(passed,Is.True);
        }
    }
}
