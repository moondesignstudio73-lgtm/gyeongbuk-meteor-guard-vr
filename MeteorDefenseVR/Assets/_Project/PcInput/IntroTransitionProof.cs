using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace MeteorDefenseVR.PcInput
{
    public static class IntroTransitionProof
    {
        public static IEnumerator Run(GazeInputRouter router,string output,bool allSizes,Action<bool> complete)
        {
            var flow=GameFlowManager.Instance;var reset=Object.FindAnyObjectByType<OperationsResetController>();
            var overlay=Object.FindAnyObjectByType<PcTestOverlay>();var director=Object.FindAnyObjectByType<SfPresentationDirector>();
            var look=router.Simulation;var camera=Camera.main;bool passed=true;
            var notes=new List<string>{"Synthetic keyboard/mouse/UI events; natural timers and weapon hits plus explicit rapid state commands. No human/webcam/HMD test."};
            var oldInput=InputSystem.settings.editorInputBehaviorInPlayMode;var oldBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();
            router.SetMode(PcGazeMode.MouseGaze);
            Button Button(string name)=>overlay.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
            void Check(bool condition,string label){passed &= condition;notes.Add(label+"="+condition);}
            IEnumerator Key(Key key)
            { InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return new WaitForSecondsRealtime(.12f);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSecondsRealtime(.12f); }
            IEnumerator Ready()
            { float end=Time.realtimeSinceStartup+30;while(director.IsBootPresenting&&Time.realtimeSinceStartup<end)yield return null;yield return new WaitForSecondsRealtime(.15f); }
            try
            {
                foreach(var size in (allSizes?new[]{new Vector2Int(800,600),new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1024,768),new Vector2Int(2560,1080)}:new[]{new Vector2Int(800,600),new Vector2Int(1280,720)}))
                {
                    using var proof=new PcStabilityProbe(camera,size.x,size.y);
                    void Capture(string label,bool previousFrame=false)
                    {
                        if(!previousFrame){Canvas.ForceUpdateCanvases();overlay.GetComponent<PausePresentationGate>().Apply();PcStabilityRenderDriver.RenderForProof(camera);}
                        string folder=Path.Combine(output,$"{size.x}x{size.y}",label);Directory.CreateDirectory(folder);proof.SaveGameOnlyProof(folder,router);
                        notes.Add($"{size}/{label}: state={flow.CurrentState}, loading={director.IsBootPresenting}, modal={router.UiModalOpen}, paused={look.IsPaused}, tracking={router.IsTrackingValid}");
                        if(!previousFrame)
                        {
                            var text=Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude).Where(t=>t.enabled&&t.color.a>.05f&&(!t.TryGetComponent<Renderer>(out var r)||r.enabled&&!r.forceRenderingOff)&&t.GetComponentsInParent<CanvasGroup>().All(g=>g.alpha>.05f));
                            File.WriteAllLines(Path.Combine(folder,"visible-text.txt"),text.Select(t=>$"{t.name}: {t.GetParsedText().Replace('\n','|')}"));
                        }
                    }
                    // Natural adjacent transitions: preserve the last rendered frame before each event.
                    reset.ResetExperience();yield return null;yield return new WaitForSecondsRealtime(.03f);Capture("loading-early");
                    yield return new WaitForSecondsRealtime(.18f);Capture("loading-middle");yield return Ready();Capture("ready");
                    var visited=new HashSet<GameState>();float changedAt=Time.realtimeSinceStartup;int shot=0;
                    Action<GameState,GameState> transition=(from,to)=>{Capture($"natural-{from}-to-{to}-before",true);visited.Add(to);changedAt=Time.realtimeSinceStartup;shot=0;};
                    flow.OnStateTransitioned+=transition;
                    try
                    {
                        yield return Key(UnityEngine.InputSystem.Key.Enter);
                        float deadline=Time.realtimeSinceStartup+65;bool pauseChecked=false;
                        while(Time.realtimeSinceStartup<deadline)
                        {
                            var target=Object.FindObjectsByType<GazeLockOnTarget>().Where(t=>t.IsAvailable).OrderBy(t=>Vector3.Distance(t.transform.position,camera.transform.position)).FirstOrDefault();
                            PcPlayerSmoke.QueueAim(mouse,look,target!=null?target.transform:null,target!=null?(Vector2)camera.WorldToScreenPoint(target.transform.position):Vector2.zero);
                            float age=Time.realtimeSinceStartup-changedAt;
                            if(shot<3&&age>=new[]{.03f,.18f,.55f}[shot]){Capture($"natural-{flow.CurrentState}-{shot}");shot++;}
                            if(flow.CurrentState==GameState.Tutorial&&age>.3f&&!pauseChecked)
                            {
                                pauseChecked=true;yield return Key(UnityEngine.InputSystem.Key.Escape);Capture("tutorial-pause");
                                Button("LookSettings").onClick.Invoke();yield return null;Capture("tutorial-settings");
                                yield return Key(UnityEngine.InputSystem.Key.Escape);Capture("tutorial-settings-closed");Check(look.IsPaused&&!overlay.LookSettingsOpen,size+" settings closes before resume");
                                yield return Key(UnityEngine.InputSystem.Key.Escape);Capture("tutorial-resumed");Check(!look.IsPaused&&!router.UiModalOpen,size+" input restored");
                            }
                            if(flow.CurrentState==GameState.Playing&&shot>=3)break;
                            yield return null;
                        }
                        foreach(var state in new[]{GameState.Intro,GameState.MissionBriefing,GameState.EyeCalibration,GameState.Tutorial,GameState.Practice,GameState.Launch,GameState.Countdown,GameState.Playing})Check(visited.Contains(state),size+" natural "+state);
                    }
                    finally{flow.OnStateTransitioned-=transition;}
                    // Fast operator progression while previous copy is still appearing; these are not natural-flow claims.
                    reset.ResetExperience();yield return Ready();
                    foreach(var state in new[]{GameState.Intro,GameState.MissionBriefing,GameState.Tutorial,GameState.Practice,GameState.Launch})
                    { Capture("rapid-before-"+state);flow.ChangeState(state);yield return null;Capture("rapid-middle-"+state);yield return new WaitForSecondsRealtime(.18f);Capture("rapid-after-"+state); }
                    reset.ResetExperience();yield return new WaitForSecondsRealtime(.18f);Capture("restart-from-launch");
                    Check(flow.CurrentState==GameState.Boot&&!look.IsPaused&&!router.UiModalOpen,size+" launch reset clears input");
                    yield return Ready();flow.StartTutorial();yield return new WaitForSecondsRealtime(.18f);look.Pause();yield return null;
                    Button("LookSettings").onClick.Invoke();yield return null;reset.ResetExperience();yield return new WaitForSecondsRealtime(.18f);Capture("restart-from-tutorial-settings");
                    Check(flow.CurrentState==GameState.Boot&&!look.IsPaused&&!overlay.LookSettingsOpen&&!router.UiModalOpen,size+" paused modal reset clears input");
                    // Unsupported input must not escape preparation or a modal.
                    reset.ResetExperience();yield return null;Capture("loading-before-space");yield return Key(UnityEngine.InputSystem.Key.Space);Capture("loading-after-space");
                    Check(flow.CurrentState==GameState.Boot,size+" loading blocks Space");
                    reset.ResetExperience();yield return Ready();Button("LookSettings").onClick.Invoke();yield return null;Capture("ready-settings-before-enter");
                    yield return Key(UnityEngine.InputSystem.Key.Enter);Capture("ready-settings-after-enter");Check(flow.CurrentState==GameState.Boot&&overlay.LookSettingsOpen,size+" settings blocks Enter");
                    if(overlay.LookSettingsOpen){yield return Key(UnityEngine.InputSystem.Key.Escape);yield return Key(UnityEngine.InputSystem.Key.Enter);Check(flow.CurrentState==GameState.Intro,size+" Enter works after settings close");}
                }
            }
            finally
            {
                reset.ResetExperience();InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.editorInputBehaviorInPlayMode=oldInput;InputSystem.settings.backgroundBehavior=oldBackground;
                Directory.CreateDirectory(output);notes.Add("passed="+passed);File.WriteAllLines(Path.Combine(output,"verification.txt"),notes);
            }
            complete(passed);
        }
    }
}
