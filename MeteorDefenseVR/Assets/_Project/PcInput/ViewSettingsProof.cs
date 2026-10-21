using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;
namespace MeteorDefenseVR.PcInput
{
    public static class ViewSettingsProof
    {
        public static IEnumerator Run(GazeInputRouter router,string output,bool validate,Action<bool> complete)
        {
            var flow=GameFlowManager.Instance;var reset=Object.FindAnyObjectByType<OperationsResetController>();
            var result=Object.FindAnyObjectByType<ResultController>();var overlay=Object.FindAnyObjectByType<PcTestOverlay>();
            var look=router.Simulation;var camera=Camera.main;var notes=new List<string>{"Actual scene/UI events; explicit states, no human/HMD/webcam input."};
            bool passed=true;float sensitivity=look.Sensitivity;bool invert=look.InvertY,center=look.ShowCenterPoint;var shake=look.Shake;
            Object.FindAnyObjectByType<HangarLobbyController>().Ranking.Initialize(flow,result,new LocalRankingStore(null));
            UnityEngine.UI.Button Button(string name)=>overlay.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.name==name);
            var entry=Button("LookSettings");var panel=overlay.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="LookSettingsPanel");
            var oldInput=InputSystem.settings.editorInputBehaviorInPlayMode;var oldBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard=InputSystem.AddDevice<Keyboard>();
            IEnumerator Ready(){float limit=Time.realtimeSinceStartup+15;while(Object.FindAnyObjectByType<SfPresentationDirector>().IsBootPresenting&&Time.realtimeSinceStartup<limit)yield return null;yield return new WaitForSecondsRealtime(.15f);}
            try
            {
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(800,600),new Vector2Int(2560,1080)})
                {
                    using var proof=new PcStabilityProbe(camera,size.x,size.y);
                    IEnumerator Capture(string state)
                    {
                        yield return new WaitForSecondsRealtime(.18f);Canvas.ForceUpdateCanvases();camera.GetComponent<WorldUiSafeArea>().Apply();PcStabilityRenderDriver.RenderForProof(camera);
                        string folder=Path.Combine(output,$"{size.x}x{size.y}",state);Directory.CreateDirectory(folder);proof.SaveGameOnlyProof(folder,router);
                        var labels=(panel.gameObject.activeInHierarchy?panel.GetComponentsInChildren<TextMeshProUGUI>():entry.gameObject.activeInHierarchy?entry.GetComponentsInChildren<TextMeshProUGUI>().Concat(overlay.GetComponentsInChildren<TextMeshProUGUI>().Where(t=>t.name=="SimulationHelp"||t.transform.parent.name=="ResumeSimulation")).ToArray():Array.Empty<TextMeshProUGUI>());
                        foreach(var label in labels)
                        {
                            label.ForceMeshUpdate(true,true);var glyphs=label.textInfo.characterInfo.Take(label.textInfo.characterCount).Where(c=>c.isVisible).ToArray();
                            float pixels=glyphs.Where(c=>char.IsLetterOrDigit(c.character)).Select(c=>Mathf.Abs(camera.WorldToScreenPoint(label.transform.TransformPoint(c.topLeft)).y-camera.WorldToScreenPoint(label.transform.TransformPoint(c.bottomLeft)).y)).DefaultIfEmpty(100).Min();
                            bool fits=!label.isTextTruncated&&!label.GetParsedText().Contains('□')&&label.textBounds.size.x<=label.rectTransform.rect.width+.1f&&label.textBounds.size.y<=label.rectTransform.rect.height+.1f&&pixels>=12;
                            foreach(var c in glyphs)foreach(var p in new[]{c.bottomLeft,c.topRight}){var s=camera.WorldToScreenPoint(label.transform.TransformPoint(p));fits &= s.x>=12&&s.x<=size.x-12&&s.y>=12&&s.y<=size.y-12;}
                            notes.Add($"{size}/{state}/{label.name} px={pixels:F2} fits={fits} bounds={label.textBounds.size} rect={label.rectTransform.rect.size} text={label.GetParsedText().Replace('\n','|')}");if(validate)passed &= fits;
                        }
                        foreach(var button in (panel.gameObject.activeInHierarchy?panel.GetComponentsInChildren<UnityEngine.UI.Button>():entry.gameObject.activeInHierarchy?new[]{entry}:Array.Empty<UnityEngine.UI.Button>()))
                        {
                            var corners=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(corners);var a=camera.WorldToScreenPoint(corners[0]);var b=camera.WorldToScreenPoint(corners[2]);bool target=b.x-a.x>=44&&b.y-a.y>=44;notes.Add($"{state}/{button.name} target={b-a} available={button.interactable} adequate={target}");if(validate)passed &= target;
                        }
                    }
                    reset.ResetExperience();yield return null;yield return Ready();yield return Capture("boot-entry");entry.onClick.Invoke();yield return Capture("boot-open");
                    Button("InvertY").onClick.Invoke();Button("CenterPoint").onClick.Invoke();Button("CameraShake").onClick.Invoke();yield return Capture("changed-values");
                    look.Sensitivity=.1f;yield return Capture("minimum-sensitivity");if(validate)passed &= !Button("SensitivityDown").interactable;
                    Button("SensitivityUp").onClick.Invoke();if(validate)passed &= Mathf.Abs(look.Sensitivity-.2f)<.001f;
                    look.Sensitivity=2;yield return Capture("maximum-sensitivity");if(validate)passed &= !Button("SensitivityUp").interactable;
                    Button("Close").onClick.Invoke();yield return Capture("boot-closed");
                    flow.StartMainGame();yield return new WaitForSecondsRealtime(.2f);yield return Capture("combat-entry-hidden");if(validate)passed &= !entry.gameObject.activeInHierarchy;
                    look.Pause();yield return Capture("paused-entry");entry.onClick.Invoke();yield return Capture("paused-open");if(validate){router.InputSuspended=false;passed &= router.UiModalOpen&&!router.IsTrackingValid;router.InputSuspended=true;}
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));yield return new WaitForSecondsRealtime(.12f);notes.Add($"Key observed: current={Keyboard.current?.deviceId} test={keyboard.deviceId} pressed={keyboard.escapeKey.isPressed} state={flow.CurrentState} looking={look.IsLooking} paused={look.IsPaused}");InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Capture("back-from-settings");
                    if(validate)passed &= look.IsPaused&&!panel.gameObject.activeInHierarchy;
                    if(validate)
                    {
                        notes.Add("first Escape closes settings and keeps paused="+(look.IsPaused&&!panel.gameObject.activeInHierarchy));
                        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));yield return new WaitForSecondsRealtime(.12f);notes.Add($"Key observed: current={Keyboard.current?.deviceId} test={keyboard.deviceId} pressed={keyboard.escapeKey.isPressed} state={flow.CurrentState} looking={look.IsLooking} paused={look.IsPaused}");
                        InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSecondsRealtime(.12f);
                        notes.Add("second Escape resumes="+!look.IsPaused);passed &= !look.IsPaused;
                    }
                    look.Resume();reset.ResetExperience();Object.FindAnyObjectByType<GameSessionStats>().SetScore(1499);flow.ShowResult();yield return Capture("result-entry");
                    entry.onClick.Invoke();yield return Capture("result-open");if(validate){reset.Tick(20);passed &= router.UiModalOpen&&result.IsVisible&&!Object.FindAnyObjectByType<HangarLobbyController>().ChooseFromResult(true);notes.Add("result modal blocks timer and replay="+(router.UiModalOpen&&result.IsVisible));}
                    Button("Close").onClick.Invoke();yield return Capture("result-closed");if(validate)passed &= !router.UiModalOpen;
                    entry.onClick.Invoke();yield return null;reset.ResetExperience();yield return Capture("reset-cleared");if(validate)passed &= !panel.gameObject.activeInHierarchy&&!router.UiModalOpen;
                    if(validate)
                    {
                        yield return Ready();
                        Vector2 Position(UnityEngine.UI.Button b)=>camera.WorldToScreenPoint(b.transform.TransformPoint(((RectTransform)b.transform).rect.center));
                        for(int i=0;i<10;i++)overlay.TickSettingsGaze(Position(entry),true,.1f);
                        yield return Capture("gaze-open");passed &= overlay.LookSettingsOpen;
                        bool previousInvert=look.InvertY;for(int i=0;i<10;i++)overlay.TickSettingsGaze(Position(Button("InvertY")),true,.1f);
                        passed &= look.InvertY!=previousInvert;
                        for(int i=0;i<20;i++)overlay.TickSettingsGaze(Position(Button("InvertY")),true,.1f);
                        passed &= look.InvertY!=previousInvert;
                        overlay.TickSettingsGaze(Vector2.zero,false,.1f);
                        for(int i=0;i<10;i++)overlay.TickSettingsGaze(Position(Button("Close")),true,.1f);
                        yield return Capture("gaze-closed");passed &= !overlay.LookSettingsOpen;
                        notes.Add("screen-space synthetic gaze open/change/hold/close checked");
                    }
                }
            }
            finally{InputSystem.RemoveDevice(keyboard);InputSystem.settings.editorInputBehaviorInPlayMode=oldInput;InputSystem.settings.backgroundBehavior=oldBackground;reset.ResetExperience();look.Sensitivity=sensitivity;look.InvertY=invert;look.ShowCenterPoint=center;look.Shake=shake;Directory.CreateDirectory(output);notes.Add("passed="+passed);File.WriteAllLines(Path.Combine(output,"verification.txt"),notes);}
            complete(passed);
        }
    }
}
