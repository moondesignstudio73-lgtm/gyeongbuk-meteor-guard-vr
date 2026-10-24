#if REPLAY_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.PcInput
{
    public static class ReplayContinuationProof
    {
        public static IEnumerator Run(string output)
        {
            Directory.CreateDirectory(output);
            var notes = new List<string> { "Private REPLAY_QA build; public product1723e35 plus QA hooks. Initial/final result explicitly forced. Synthetic InputSystem pointer and relative aim; no OS/HMD. No action/Queue/Choose/ExecuteEvents or state writes between replay click and observed combat hit." };
            void Log(string text) { notes.Add(text); File.WriteAllLines(Path.Combine(output,"verification.txt"),notes); }
            void Check(bool condition,string message) { Log(message+"="+condition); if(!condition)throw new InvalidOperationException(message); }
            float deadline=Time.realtimeSinceStartup+40;
            var warm=Object.FindAnyObjectByType<ReadyPrewarmController>();
            while((warm==null||!warm.IsComplete)&&Time.realtimeSinceStartup<deadline){yield return null;warm=Object.FindAnyObjectByType<ReadyPrewarmController>();}
            Check(warm!=null&&warm.IsComplete,"prewarm complete");
            var flow=GameFlowManager.Instance;var result=Object.FindAnyObjectByType<ResultController>();
            var manager=Object.FindAnyObjectByType<DifficultyManager>();var stats=Object.FindAnyObjectByType<GameSessionStats>();
            var reset=Object.FindAnyObjectByType<OperationsResetController>();var router=Object.FindAnyObjectByType<GazeInputRouter>();
            var lobby=Object.FindAnyObjectByType<HangarLobbyController>();var actions=Object.FindAnyObjectByType<ResultActionsView>();
            var hud=Object.FindAnyObjectByType<MissionHudController>(FindObjectsInactive.Include);
            var view=Object.FindAnyObjectByType<GameplayHudOverlayView>(FindObjectsInactive.Include);
            var caster=Object.FindAnyObjectByType<GazeRaycaster>();var camera=Camera.main;
            lobby.Ranking.Initialize(flow,result,new LocalRankingStore(null));
            var devices=InputSystem.devices.Where(d=>d.enabled&&(d is Mouse||d is Keyboard)).ToArray();
            foreach(var d in devices)InputSystem.DisableDevice(d);
            var mouse=InputSystem.AddDevice<Mouse>();
            var background=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            bool suspended=router.InputSuspended;
            var observer=actions.Replay.gameObject.AddComponent<ResultPointerObserver>();
            int hidden=0;Action onHidden=()=>hidden++;result.ResultHidden+=onHidden;
            var transitions=new List<GameState>();
            Action<GameState> onState=s=>{transitions.Add(s);Log($"transition={s}; time={Time.realtimeSinceStartup:F4}; phase={lobby.Phase}");};
            flow.OnStateChanged+=onState;
            using var probe=new PcStabilityProbe(camera,1280,720);
            IEnumerator Delay(float duration){float until=Time.realtimeSinceStartup+duration;while(Time.realtimeSinceStartup<until)yield return null;}
            IEnumerator Capture(string label)
            {
                var driver=Object.FindAnyObjectByType<PcStabilityRenderDriver>();bool done=false;Exception failure=null;
                Action save=()=>{try{
                    string folder=Path.Combine(output,label);Directory.CreateDirectory(folder);
                    Canvas.ForceUpdateCanvases();PcStabilityRenderDriver.RenderForProof(camera);probe.SaveGameOnlyProof(folder,router);
                    var texts=Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include);
                    string transient=string.Join("\n",texts.Where(t=>t.name=="FlightNotice"||t.name=="CombatPraise").Select(t=>$"{t.name}: enabled={t.enabled}; active={t.gameObject.activeInHierarchy}; alpha={t.color.a}; text={t.text}"));
                    File.WriteAllText(Path.Combine(folder,"state.txt"),$"Instrumented only. time={Time.realtimeSinceStartup}; state={flow.CurrentState}; phase={lobby.Phase}; selected={manager.Selected}; session={manager.Session.Level}; frozen={manager.SessionFrozen}; hud={view.DifficultyStageValue.text}; resultDifficulty={result.Snapshot?.Difficulty}\n"+
                        $"resultVisible={result.IsVisible}; resultActions={actions.Visible}; inputSuspended={router.InputSuspended}; modal={router.UiModalOpen}; looking={router.Simulation.IsLooking}; shots={stats.Shots}; hits={stats.SuccessfulHits}; score={stats.Score}; target={(caster.CurrentTarget as Component)?.name}; feedback={hud.FeedbackMessage}; remaining={hud.FeedbackRemaining}\n"+transient);
                }catch(Exception e){failure=e;}done=true;};
                driver.RenderCompleted+=save;try{while(!done)yield return null;}finally{driver.RenderCompleted-=save;}
                if(failure!=null)throw failure;
            }
            try
            {
                Check(EventSystem.current.currentInputModule is InputSystemUIInputModule,"real UI input module");
                foreach(var level in new[]{DifficultyLevel.Experience,DifficultyLevel.Easy})
                {
                    string prefix=level.ToString();
                    reset.ResetExperience();router.SetMode(PcGazeMode.MouseGaze);router.InputSuspended=false;
                    yield return Delay(.1f);manager.Select(level);flow.StartMainGame();yield return Delay(.35f);
                    stats.SetScore(138940);hud.AddScore(50);flow.ShowResult();reset.CancelResultTimer();yield return Delay(.15f);
                    Check(result.IsVisible&&result.Snapshot.Difficulty==level,prefix+" forced initial result");
                    yield return Capture(prefix+"-initial-result");
                    var button=actions.Replay.GetComponent<UnityEngine.UI.Button>();var rect=(RectTransform)button.transform;
                    var canvas=button.GetComponentInParent<Canvas>();
                    Vector2 point=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,rect.TransformPoint(rect.rect.center));
                    var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
                    Check(hits.FirstOrDefault().gameObject?.GetComponentInParent<UnityEngine.UI.Button>()==button,prefix+" replay top raycast");
                    int clicks=observer.Clicks,downs=observer.Downs,ups=observer.Ups,oldHidden=hidden;
                    transitions.Clear();
                    InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return Delay(.025f);
                    InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=1});yield return Delay(.025f);
                    InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return Delay(.035f);
                    Check(observer.Clicks-clicks==1&&observer.Downs-downs==1&&observer.Ups-ups==1,prefix+" actual pointer down/up/click exactly once");
                    Check(hidden-oldHidden==1&&!result.IsVisible,prefix+" result hidden once");
                    Check(lobby.Phase==HangarLobbyController.LobbyPhase.ReplayPreparing,prefix+" ReplayPreparing reached");
                    yield return Capture(prefix+"-preparing");
                    Check(stats.Score==0&&stats.Shots==0&&hud.FeedbackRemaining==0&&!actions.Visible,prefix+" old score/reward/result cleared");
                    Check(manager.Session.Level==level,prefix+" replay retains selected difficulty");
                    deadline=Time.realtimeSinceStartup+30;
                    while(flow.CurrentState!=GameState.Playing&&Time.realtimeSinceStartup<deadline)yield return null;
                    Check(flow.CurrentState==GameState.Playing,prefix+" automatic preparation through Playing");
                    Check(transitions.Count(s=>s==GameState.Launch)==1&&transitions.Count(s=>s==GameState.Countdown)==1&&transitions.Count(s=>s==GameState.Playing)==1,prefix+" one launch/countdown/playing transition");
                    yield return Capture(prefix+"-playing-before-aim");
                    Check(!router.InputSuspended&&!router.UiModalOpen&&!result.IsVisible&&!actions.Visible,prefix+" combat input unblocked");
                    Check(manager.Session.Level==level&&view.DifficultyStageValue.text==GameplayHudOverlayView.FormatDifficultyStage(level,1),prefix+" combat difficulty and HUD agree");
                    deadline=Time.realtimeSinceStartup+20;bool acquired=false;
                    while(stats.SuccessfulHits==0&&Time.realtimeSinceStartup<deadline)
                    {
                        var target=Object.FindObjectsByType<GazeLockOnTarget>().Where(t=>t.IsAvailable).OrderBy(t=>(t.transform.position-camera.transform.position).sqrMagnitude).FirstOrDefault();
                        PcPlayerSmoke.QueueAim(mouse,router.Simulation,target!=null?target.transform:null,new Vector2(Screen.width*.5f,Screen.height*.5f));
                        acquired|=caster.CurrentTarget is GazeLockOnTarget;
                        yield return null;
                    }
                    Check(acquired&&stats.Shots>0&&stats.SuccessfulHits>0,prefix+" relative mouse aim acquired and weapon hit through normal combat");
                    Check(observer.Clicks-clicks==1&&hidden-oldHidden==1,prefix+" no duplicate replay action during preparation/combat");
                    yield return Capture(prefix+"-combat-hit");
                    // End only this observation; this is explicitly not a completed victory round.
                    flow.ShowResult();reset.CancelResultTimer();yield return Delay(.3f);
                    Check(result.IsVisible&&result.Snapshot.Difficulty==level,prefix+" forced post-hit result agrees");
                    yield return Capture(prefix+"-post-hit-result");
                }
                Log("passed=True");
            }
            finally
            {
                flow.OnStateChanged-=onState;result.ResultHidden-=onHidden;
                router.InputSuspended=suspended;InputSystem.RemoveDevice(mouse);
                foreach(var d in devices)InputSystem.EnableDevice(d);
                InputSystem.settings.backgroundBehavior=background;
            }
        }
    }
}
#endif
