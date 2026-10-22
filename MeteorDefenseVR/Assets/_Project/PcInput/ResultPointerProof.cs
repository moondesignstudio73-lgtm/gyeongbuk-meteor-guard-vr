using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.GameFlow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;

namespace MeteorDefenseVR.PcInput
{
    // Opt-in standalone verification only. No action methods or UI events are invoked by this harness.
    public sealed class ResultPointerProof : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(Environment.GetCommandLineArgs().Contains("--pc-result-pointer-qa"))
                new GameObject("QA_ResultPointer").AddComponent<ResultPointerProof>();
        }
        private IEnumerator Start()
        {
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"..","QA","ResultPointer"));
            Directory.CreateDirectory(output);
            var notes=new List<string>{"QA-only standalone harness. Synthetic InputSystem mouse, not OS/human/HMD input. Explicit result state fixtures; no Button.onClick.Invoke, ExecuteEvents, Choose or ChooseFromResult calls."};
            bool passed=true,completed=false;void Check(bool value,string message){passed &= value;notes.Add(message+"="+value);File.WriteAllLines(Path.Combine(output,"verification.txt"),notes);}
            yield return null;
            GazeInputRouter router=null;
            var warm=FindAnyObjectByType<ReadyPrewarmController>();
            float deadline=Time.realtimeSinceStartup+40;
            while((warm==null||!warm.IsComplete)&&Time.realtimeSinceStartup<deadline){yield return null;warm=FindAnyObjectByType<ReadyPrewarmController>();}
            router=FindAnyObjectByType<GazeInputRouter>();
            var flow=GameFlowManager.Instance;var result=FindAnyObjectByType<ResultController>();
            var reset=FindAnyObjectByType<OperationsResetController>();var stats=FindAnyObjectByType<GameSessionStats>();
            var lobby=FindAnyObjectByType<HangarLobbyController>();var actions=FindAnyObjectByType<ResultActionsView>();
            var overlay=FindAnyObjectByType<PcTestOverlay>();
            lobby.Ranking.Initialize(flow,result,new LocalRankingStore(null));
            var physical=InputSystem.devices.Where(d=>d.enabled&&(d is Mouse||d is Keyboard)).ToArray();
            foreach(var device in physical)InputSystem.DisableDevice(device);
            var mouse=InputSystem.AddDevice<Mouse>();
            var background=InputSystem.settings.backgroundBehavior;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            int hidden=0;Action onHidden=()=>hidden++;result.ResultHidden+=onHidden;
            var replay=actions.Replay.gameObject.AddComponent<ResultPointerObserver>();
            var menu=actions.Menu.gameObject.AddComponent<ResultPointerObserver>();
            float shownAt=0;float clickedAt=0;
            IEnumerator Delay(float seconds){float end=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<end)yield return null;}
            Vector2 Point(UnityEngine.UI.Button button)
            {var rect=(RectTransform)button.transform;var canvas=button.GetComponentInParent<Canvas>();return RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center));}
            IEnumerator Pointer(UnityEngine.UI.Button button,bool blocked=false)
            {
                Canvas.ForceUpdateCanvases();Vector2 point=Point(button);
                var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
                var first=hits.FirstOrDefault().gameObject;
                bool target=first!=null&&first.GetComponentInParent<UnityEngine.UI.Button>()==button;
                Check(blocked?!target:target,$"hit-test {button.name} at {point}, top={first?.name}, blocked={blocked}");
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return Delay(.025f);
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=1});yield return Delay(.025f);
                Check(mouse.leftButton.isPressed,"InputSystem consumed mouse down");
                clickedAt=Time.realtimeSinceStartup;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return Delay(.035f);
                Check(!mouse.leftButton.isPressed,"InputSystem consumed mouse up");
            }
            IEnumerator Show()
            {
                reset.ResetExperience();yield return Delay(.05f);stats.SetScore(138940);
                router.InputSuspended=true; // Disable world gaze/click shortcut so only the UI input module can select.
                flow.ShowResult();reset.CancelResultTimer();shownAt=Time.realtimeSinceStartup;
                yield return Delay(.1f);
            }
            try
            {
                Check(!Application.isEditor,"standalone required");
                Check(EventSystem.current.currentInputModule is InputSystemUIInputModule,"InputSystemUIInputModule active");
                Check(FindObjectsByType<HangarMenuButton>(FindObjectsInactive.Include).Where(b=>b!=actions.Replay&&b!=actions.Menu).All(b=>!b.AllowImmediateClick),"only result buttons opt in to immediate clicks");
                for(int i=0;i<4;i++)
                {
                    yield return Show();if(i>=2)router.InputSuspended=false;var observer=i%2==0?menu:replay;var target=i%2==0?actions.Menu:actions.Replay;
                    int clicks=observer.Clicks,downs=observer.Downs,ups=observer.Ups,beforeHidden=hidden;
                    yield return Pointer(target.GetComponent<UnityEngine.UI.Button>());
                    float age=observer.LastClickAt-shownAt;
                    Check(age>=.1f&&age<=.3f,$"cycle{i} pointer release age={age:F4}s");
                    Check(observer.Clicks-clicks==1&&observer.Downs-downs==1&&observer.Ups-ups==1,$"cycle{i} actual IPointer down/up/click once");
                    Check(hidden-beforeHidden==1&&!result.IsVisible,$"cycle{i} one result exit");
                    Check(i%2==0?lobby.IsMenuReady:lobby.Phase==HangarLobbyController.LobbyPhase.ReplayPreparing,$"cycle{i} requested destination");
                    yield return Delay(.15f);Check(hidden-beforeHidden==1,$"cycle{i} no duplicate exit after release");
                }
                yield return Show();
                var settings=overlay.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.name=="LookSettings");
                yield return Pointer(settings);Check(overlay.LookSettingsOpen&&router.UiModalOpen,"pointer opens settings");
                int blockedHidden=hidden,blockedClicks=replay.Clicks;
                yield return Pointer(actions.Replay.GetComponent<UnityEngine.UI.Button>(),true);
                Check(result.IsVisible&&hidden==blockedHidden&&replay.Clicks==blockedClicks,"modal intercepts pointer before result button");
                var panel=overlay.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="LookSettingsPanel");
                var close=panel.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.name=="Close");
                yield return Pointer(close);Check(!overlay.LookSettingsOpen&&!router.UiModalOpen,"pointer closes settings");
                yield return Pointer(actions.Menu.GetComponent<UnityEngine.UI.Button>());Check(lobby.IsMenuReady&&hidden==blockedHidden+1,"underlying menu still works once after modal closes");
                // Existing lobby menus remain gaze/collider driven: do not enable their UI graphics.
                var others=FindObjectsByType<HangarMenuButton>(FindObjectsInactive.Include).Where(b=>b!=actions.Replay&&b!=actions.Menu).ToArray();
                Check(others.All(b=>!b.AllowImmediateClick),"other menu pointer/gaze configuration unchanged");
                router.SetMode(PcGazeMode.MouseGaze);router.InputSuspended=false;
                var ranking=others.Single(b=>b.name=="Ranking"&&b.isActiveAndEnabled);
                yield return Pointer(ranking.GetComponent<UnityEngine.UI.Button>(),!ranking.GetComponent<UnityEngine.UI.Image>().raycastTarget);
                Check(lobby.IsMenuReady,"other lobby menu early click retains guard");
                yield return Delay(.45f);
                yield return Pointer(ranking.GetComponent<UnityEngine.UI.Button>(),!ranking.GetComponent<UnityEngine.UI.Image>().raycastTarget);yield return Delay(.3f);
                Check(lobby.Phase==HangarLobbyController.LobbyPhase.Records,"other lobby menu later click works");
                yield return Show();router.SetMode(PcGazeMode.MouseGaze);router.InputSuspended=false;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(20,20)});yield return Delay(.05f);
                InputSystem.QueueStateEvent(mouse,new MouseState{position=Point(actions.Menu.GetComponent<UnityEngine.UI.Button>())});yield return Delay(.15f);
                int gazeHidden=hidden,gazeClicks=menu.Clicks;
                Check(result.IsVisible&&actions.Menu.Progress==0&&router.IsTrackingValid,"actual mouse-gaze hover keeps early dwell guard");
                yield return Delay(1.3f);
                Check(lobby.IsMenuReady&&hidden==gazeHidden+1&&menu.Clicks==gazeClicks,"world gaze dwell selects once without pointer click");
                // Separate gate contract: immediate pointer opt-in must not open the gaze gate early.
                var gate=new GazeMenuSelectionGate();gate.OpenScreen();yield return Delay(.15f);Check(!gate.IsOpen,"gaze gate closed at0.15s");
                yield return Delay(.3f);Check(gate.IsOpen,"gaze gate opens after0.4s");
                yield return Show();
                using(var capture=new PcStabilityProbe(Camera.main,1280,720)){yield return Delay(.1f);PcStabilityRenderDriver.RenderForProof(Camera.main);capture.SaveGameOnlyProof(output,router);}
                completed=true;
            }
            finally
            {
                result.ResultHidden-=onHidden;reset.ResetExperience();router.InputSuspended=false;
                InputSystem.RemoveDevice(mouse);foreach(var device in physical)InputSystem.EnableDevice(device);InputSystem.settings.backgroundBehavior=background;
                passed &= completed;notes.Add("completed="+completed);notes.Add("passed="+passed);File.WriteAllLines(Path.Combine(output,"verification.txt"),notes);
            }
            Application.Quit(passed?0:2);
        }
    }
    public sealed class ResultPointerObserver : MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IPointerClickHandler
    {
        public int Downs,Ups,Clicks;
        public float LastClickAt;
        public void OnPointerDown(PointerEventData data)=>Downs++;
        public void OnPointerUp(PointerEventData data)=>Ups++;
        public void OnPointerClick(PointerEventData data){Clicks++;LastClickAt=Time.realtimeSinceStartup;}
    }
}
