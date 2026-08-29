using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Education;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.Tests
{
    public sealed class EducationEntryTests
    {
        [UnityTearDown] public IEnumerator Cleanup()
        { Time.timeScale=1;AudioListener.pause=false;if(Application.isPlaying)yield return new ExitPlayMode(); }

        [UnityTest,Timeout(150000)]
        public IEnumerator RegistrationTextGazeSelectionAndFirstVisitTutorial()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity",OpenSceneMode.Single);
            yield return new EnterPlayMode();yield return null;yield return null;
            // Captured locals must be allocated after the domain reload above.
            {
                var manager=Object.FindAnyObjectByType<EducationSessionManager>();
                var router=Object.FindAnyObjectByType<GazeInputRouter>();router.SetMode(PcGazeMode.MouseGaze);
                var view=Object.FindAnyObjectByType<EducationView>();var flow=GameFlowManager.Instance;
                var prior=InputSystem.settings.editorInputBehaviorInPlayMode;var background=InputSystem.settings.backgroundBehavior;
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                var mouse=InputSystem.AddDevice<Mouse>();var keyboard=InputSystem.AddDevice<Keyboard>();
                try
                {
                    float warmUntil=Time.realtimeSinceStartup+30;
                    var entry=view.Root.transform.Find("Entry/EducationStart");
                    while(!entry.gameObject.activeInHierarchy&&Time.realtimeSinceStartup<warmUntil)yield return null;
                    Assert.That(entry.gameObject.activeInHierarchy,Is.True);
                    float openUntil=Time.realtimeSinceStartup+4;
                    while(manager.Phase==EducationPhase.Idle&&Time.realtimeSinceStartup<openUntil)
                    {InputSystem.QueueStateEvent(mouse,new MouseState{position=Camera.main.WorldToScreenPoint(entry.position)});yield return null;}
                    Assert.That(manager.Phase,Is.EqualTo(EducationPhase.Registration),"Real mouse gaze must open the education panel.");
                    var panel=view.Root.transform.Find("ParticipantRegistration");
                    var skip=panel.Find("SkipTutorial").GetComponent<UnityEngine.UI.Button>();
                    skip.onClick.Invoke();Assert.That(skip.GetComponentInChildren<TextMeshProUGUI>().text,Does.EndWith("ON"));
                    manager.CloseMenu();manager.OpenRegistration();
                    Assert.That(skip.GetComponentInChildren<TextMeshProUGUI>().text,Does.EndWith("OFF"),"New participant must not inherit tutorial skip.");
                    var name=panel.Find("ParticipantName").GetComponent<TMP_InputField>();
                    EventSystem.current.SetSelectedGameObject(name.gameObject);name.ActivateInputField();yield return null;yield return null;
                    Assert.That(name.isFocused,Is.True);
                    // TMP 3.x consumes native Event.PopEvent, not InputSystem.onTextInput.
                    // Exercise its public native-event path while also delivering the R shortcut state.
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.R));
                    name.ProcessEvent(new Event{type=EventType.KeyDown,keyCode=KeyCode.R,character='R'});
                    yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                    name.ProcessEvent(new Event{type=EventType.KeyDown,keyCode=KeyCode.A,character='A'});yield return null;
                    Assert.That(name.text,Is.EqualTo("RA"));Assert.That(flow.CurrentState,Is.EqualTo(GameState.Boot));
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));yield return null;
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
                    Assert.That(flow.CurrentState,Is.EqualTo(GameState.Boot),"Typing/Enter must not activate the old Ready START.");
                    name.DeactivateInputField();EventSystem.current.SetSelectedGameObject(null);
                    yield return new WaitForSecondsRealtime(.2f);
                    Assert.That(manager.Phase,Is.EqualTo(EducationPhase.Registration),"Shortcut protection must survive text-field focus loss.");
                    panel.Find("ParticipantNumber").GetComponent<TMP_InputField>().text="01";
                    var start=panel.Find("BeginTraining");float startUntil=Time.realtimeSinceStartup+4;
                    while(manager.Phase==EducationPhase.Registration&&Time.realtimeSinceStartup<startUntil)
                    {InputSystem.QueueStateEvent(mouse,new MouseState{position=Camera.main.WorldToScreenPoint(start.position)});yield return null;}
                    Assert.That(manager.Phase,Is.EqualTo(EducationPhase.Preparing));
                    var visited=new HashSet<GameState>();float tutorialUntil=Time.realtimeSinceStartup+60;
                    while(flow.CurrentState!=GameState.Playing&&Time.realtimeSinceStartup<tutorialUntil)
                    {
                        visited.Add(flow.CurrentState);
                        var target=Object.FindObjectsByType<GazeLockOnTarget>().FirstOrDefault(t=>t.IsAvailable);
                        var point=target!=null?(Vector2)Camera.main.WorldToScreenPoint(target.transform.position):new Vector2(Screen.width*.5f,Screen.height*.45f);
                        UnifiedLookTests.QueueAim(mouse,router.Simulation,target!=null?target.transform:null,point);yield return null;
                    }
                    Assert.That(flow.CurrentState,Is.EqualTo(GameState.Playing));
                    foreach(var state in new[]{GameState.Intro,GameState.MissionBriefing,GameState.EyeCalibration,GameState.Tutorial,GameState.Practice,GameState.Launch})
                        Assert.That(visited.Contains(state),Is.True,"First visit must keep the existing "+state+" stage.");
                    Assert.That(manager.Phase,Is.EqualTo(EducationPhase.Search));Assert.That(manager.Session.Participant.Name,Is.EqualTo("RA"));
                    Assert.That(manager.Session.Threats.Count,Is.EqualTo(2),"Tutorial objects must not pollute educational detection statistics.");
                    manager.ReturnToReady();Assert.That(manager.Store.Records.Count,Is.EqualTo(1));
                    Assert.That(router.TextEntryOpen,Is.False);
                    Assert.That(manager.Store.Records[0].Aborted,Is.True);
                }
                finally
                {InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);InputSystem.settings.editorInputBehaviorInPlayMode=prior;InputSystem.settings.backgroundBehavior=background;}
            }
        }
    }
}
