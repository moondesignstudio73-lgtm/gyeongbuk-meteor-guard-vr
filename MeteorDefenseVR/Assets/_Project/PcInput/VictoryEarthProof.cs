using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.Progression;
using MeteorDefenseVR.UI;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MeteorDefenseVR.PcInput
{
    public static class VictoryEarthProof
    {
        public static IEnumerator Run(GazeInputRouter router,string output,bool allSizes,Action<bool> complete)
        {
            var powers=Object.FindAnyObjectByType<MeteorDefenseVR.PowerUps.PowerUpManager>();
            var flow=GameFlowManager.Instance;var reset=Object.FindAnyObjectByType<OperationsResetController>();
            var victory=Object.FindAnyObjectByType<VictoryEarthResponse>();var boss=Object.FindAnyObjectByType<BossClimaxController>();
            var sequence=Object.FindAnyObjectByType<MissionCompleteController>();var health=Object.FindAnyObjectByType<PlayerHealth>();
            var stats=Object.FindAnyObjectByType<GameSessionStats>();var spawner=Object.FindAnyObjectByType<MeteorSpawner>();
            var difficulty=Object.FindAnyObjectByType<DifficultyManager>();var progression=StageProgressionController.Instance;
            var result=Object.FindAnyObjectByType<ResultController>();var lobby=Object.FindAnyObjectByType<HangarLobbyController>();
            var actions=Object.FindAnyObjectByType<ResultActionsView>();var hud=Object.FindAnyObjectByType<MissionHudController>();
            var original=Resources.Load<StageProgressionSettings>("StageProgressionSettings");var fast=Object.Instantiate(original);
            fast.explosionQuietTime=.01f;fast.stageClearPresentationTime=.01f;
            var notes=new List<string>{"Explicit state setup and direct real boss damage events; not a human playthrough. Campaign stage callbacks are real; only between-stage timers accelerated in an isolated settings clone."};
            var oldDifficulty=difficulty.Selected;
            bool passed=true;bool oldSuspended=router.InputSuspended;router.InputSuspended=true;
            var planet=Object.FindAnyObjectByType<MeteorDefenseVR.Visual.CinematicEnvironmentView>().Earth;var originalRotation=planet.rotation;
            var camera=Camera.main;var cameraPosition=camera.transform.position;var cameraRotation=camera.transform.rotation;
            using var cpu=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"MD.VictoryEarthResponse",128);
            var timings=new FrameTiming[1];double cpuMax=0,gpuMax=0;int cpuSamples=0,gpuSamples=0;
            void Check(bool value,string name){passed &= value;notes.Add(name+"="+value);}
            void Configure(StageProgressionSettings settings)=>progression.Configure(settings,spawner,boss,health,hud,stats);
            void SpawnBoss(){spawner.StopSpawning(false);boss.SetDurations(0,0,0);boss.BeginClimax();boss.Tick(0);boss.Tick(0);}
            void KillBoss(){var target=boss.ActiveBoss;Check(target!=null,"boss exists");if(target!=null){target.ReceiveDamage(target.MaxHealth);Check(!target.DestroyMeteor(),"duplicate destruction rejected");}}
            IEnumerator WaitState(GameState state,float seconds)
            {float end=Time.realtimeSinceStartup+seconds;while(flow.CurrentState!=state&&Time.realtimeSinceStartup<end)yield return null;Check(flow.CurrentState==state,"reached "+state);}
            try
            {
                Configure(fast);
                foreach(var size in allSizes?new[]{new Vector2Int(800,600),new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1024,768),new Vector2Int(2560,1080)}:new[]{new Vector2Int(800,600),new Vector2Int(1280,720)})
                {
                    using var render=new PcStabilityProbe(camera,size.x,size.y);
                    void Capture(string label)
                    {
                        Canvas.ForceUpdateCanvases();Object.FindAnyObjectByType<PausePresentationGate>()?.Apply();PcStabilityRenderDriver.RenderForProof(camera);
                        string dir=Path.Combine(output,$"{size.x}x{size.y}",label);Directory.CreateDirectory(dir);render.SaveGameOnlyProof(dir,router);
                        notes.Add($"{size}/{label}: state={flow.CurrentState} response={victory.IsPresenting} ring={victory.RingVisible} lights={lobby.View.VictoryGuidanceVisible} age={victory.Age:F3} count={victory.PresentationCount} score={stats.Score}");
                    }
                    reset.ResetExperience();difficulty.Select(DifficultyLevel.Normal);flow.StartMainGame();spawner.StopSpawning(false);
                    for(int stage=1;stage<=progression.TotalStages;stage++)
                    {
                        SpawnBoss();if(stage==progression.TotalStages){powers.TryDrop(boss.ActiveBoss,true,0);Check(powers.ActiveWorldItems>0,size+" pickup present before final hit");Capture("before-final-hit");}KillBoss();
                        if(stage<progression.TotalStages){Check(!victory.IsPresenting,$"{size} intermediate {stage} has no success");yield return WaitState(GameState.Playing,2);}
                    }
                    Check(stats.AllStagesCleared&&flow.CurrentState==GameState.MissionComplete,size+" actual campaign complete");
                    Check(powers.ActiveWorldItems==0,size+" final transition clears pickups including late boss callback");
                    int score=stats.Score;Capture("death-0");Check(!victory.IsPresenting,size+" debris wait");
                    yield return new WaitForSecondsRealtime(.45f);Capture("debris-045");Check(!victory.RingVisible,size+" quiet before presentation");
                    yield return new WaitForSecondsRealtime(.55f);Capture("response-100");
                    Check(victory.IsPresenting&&victory.PresentationCount==1,size+" final success once");
                    yield return new WaitForSecondsRealtime(.45f);Capture("response-145");
                    Check(victory.RingVisible&&lobby.View.VictoryGuidanceVisible,size+" ring and guide lights visible");
                    // Measure the new CPU marker only if the runtime exposes it; GPU frame timings are optional.
                    for(int i=0;i<12;i++)
                    {FrameTimingManager.CaptureFrameTimings();yield return null;if(cpu.Valid&&cpu.LastValue>0){cpuSamples++;cpuMax=Math.Max(cpuMax,cpu.LastValue/1000000.0);}if(FrameTimingManager.GetLatestTimings(1,timings)>0&&timings[0].gpuFrameTime>0){gpuSamples++;gpuMax=Math.Max(gpuMax,timings[0].gpuFrameTime);}}
                    var notice=Object.FindAnyObjectByType<GameplayHudOverlayView>().FeedbackValue;
                    notice.ForceMeshUpdate(true,true);float glyphHeight=1000;
                    for(int j=0;j<notice.textInfo.characterCount;j++){var c=notice.textInfo.characterInfo[j];if(c.isVisible&&c.character>='가'&&c.character<='힣')glyphHeight=Mathf.Min(glyphHeight,Mathf.Abs(camera.WorldToScreenPoint(notice.transform.TransformPoint(c.topLeft)).y-camera.WorldToScreenPoint(notice.transform.TransformPoint(c.bottomLeft)).y));}
                    Check(!notice.isTextTruncated&&notice.textInfo.lineCount==2&&notice.preferredHeight<=notice.rectTransform.rect.height&&notice.preferredWidth<=notice.rectTransform.rect.width&&glyphHeight>=20,size+" success two complete lines, glyph quad pixels="+glyphHeight.ToString("F2")+" preferred="+notice.preferredWidth+"x"+notice.preferredHeight);
                    float normalizedRadius=victory.PlanetRadius/Mathf.Max(planet.lossyScale.x,planet.lossyScale.y,planet.lossyScale.z);
                    planet.Rotate(0,45,0);yield return null;Capture("rotated-planet");
                    Check(Mathf.Abs(victory.PlanetRadius/Mathf.Max(planet.lossyScale.x,planet.lossyScale.y,planet.lossyScale.z)-normalizedRadius)<.0001f,size+" rotation preserves sphere radius");
                    router.Simulation.Pause();float age=victory.Age;yield return new WaitForSecondsRealtime(.2f);Capture("paused");
                    Check(!victory.RingVisible&&!lobby.View.VictoryGuidanceVisible&&Mathf.Approximately(age,victory.Age),size+" paused hidden and frozen");
                    router.Simulation.Resume();yield return new WaitForSecondsRealtime(.1f);Capture("resumed");Check(victory.RingVisible,size+" resumes");
                    yield return WaitState(GameState.Result,5);yield return new WaitForSecondsRealtime(1);Capture("result");
                    Check(!victory.IsPresenting&&!victory.RingVisible&&result.IsVisible&&result.Snapshot.Score==score&&actions.Visible,size+" result data and actions preserved");
                    yield return new WaitForSecondsRealtime(.5f);var button=size.x==800?actions.Replay:actions.Menu;
                    button.OnGazeExit();button.OnGazeEnter(default);button.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();yield return null;Capture("result-action");
                    Check(!result.IsVisible&&!victory.RingVisible,size+" result action immediate");
                    reset.ResetExperience();difficulty.Select(DifficultyLevel.Experience);flow.StartMainGame();SpawnBoss();KillBoss();
                    yield return new WaitForSecondsRealtime(1.1f);Check(victory.IsPresenting,size+" single-session final boss");reset.ResetExperience();yield return null;Capture("immediate-restart");Check(!victory.RingVisible&&!lobby.View.VictoryGuidanceVisible,size+" restart clears response");
                    flow.StartMainGame();health.ApplyImpactDamage(100,health.transform.position);yield return new WaitForSecondsRealtime(.15f);Capture("failure");Check(!victory.IsPresenting&&victory.PresentationCount==0,size+" failure never celebrates");
                    reset.ResetExperience();flow.CompleteMission();yield return new WaitForSecondsRealtime(1.1f);Capture("unqualified-completion");Check(!victory.IsPresenting && sequence.PresentationProgress==0,size+" state without boss death cannot activate response or success message");
                }
                reset.ResetExperience();difficulty.Select(DifficultyLevel.Experience);
                int beforeRings=Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include).Count(r=>r.name=="VictoryEarthProtectionRing");
                int beforeMaterials=Resources.FindObjectsOfTypeAll<Material>().Count(m=>m.name=="Victory Earth protection");
                for(int i=0;i<30;i++)
                {flow.StartMainGame();SpawnBoss();KillBoss();sequence.Tick(1);yield return null;reset.ResetExperience();yield return null;Check(!victory.RingVisible,"repeat reset "+i);}
                Check(Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include).Count(r=>r.name=="VictoryEarthProtectionRing")==beforeRings&&beforeRings==1,"30 resets one pooled ring");
                Check(Resources.FindObjectsOfTypeAll<Material>().Count(m=>m.name=="Victory Earth protection")==beforeMaterials&&beforeMaterials==1,"30 resets one shared material");
                Check(camera.transform.position==cameraPosition&&camera.transform.rotation==cameraRotation,"tracked camera unchanged");
            }
            finally
            {
                reset.ResetExperience();planet.rotation=originalRotation;Configure(original);difficulty.Select(oldDifficulty);Object.Destroy(fast);router.InputSuspended=oldSuspended;
                notes.Add($"CPU marker samples={cpuSamples} maxMs={cpuMax:F5}; GPU frame samples={gpuSamples} maxMs={gpuMax:F3}. Zero samples means unavailable, not zero cost. No baseline speedup claim.");
                Directory.CreateDirectory(output);notes.Add("passed="+passed);File.WriteAllLines(Path.Combine(output,"verification.txt"),notes);
            }
            complete(passed);
        }
    }
}
