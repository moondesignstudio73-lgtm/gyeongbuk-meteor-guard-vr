using MeteorDefenseVR.Audio;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Launch;
using MeteorDefenseVR.Visual;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using MeteorDefenseVR.Progression;

namespace MeteorDefenseVR.PcInput
{
    // Presentation substates of Result/Boot. Core combat and the first-time tutorial are unchanged.
    [DefaultExecutionOrder(1200), DisallowMultipleComponent]
    public sealed class HangarLobbyController : MonoBehaviour
    {
        public enum LobbyPhase { Hidden, Returning, Menu, Difficulty, Calibrating, ReplayPreparing, Departing, Standby, Records }
        public enum MenuAction { Replay, Difficulty, Recalibrate, Standby, Back, StartNewPlayer, Ranking, RecentRecords, TopRecords, BestRecords, NextRecordPage, RetryStage }
        [SerializeField, Range(2,4)] private float returnDuration = 3f;
        [SerializeField] private Transform hangar, leftDoor, rightDoor;
        [SerializeField] private TMP_FontAsset font;
        private GameFlowManager flow;
        private OperationsResetController reset;
        private ResultController result;
        private ReadyScreenController ready;
        private DifficultyManager difficulty;
        private DifficultySelectionView difficultyView;
        private GazeInputRouter router;
        private EyeTracking.GazeRaycaster gaze;
        private AudioManager audio;
        private SfPresentationDirector presentation;
        private LaunchSequenceController launch;
        private CinematicEnvironmentView environment;
        private HangarLobbyView view;
        private LocalRankingController ranking;
        private StageProgressionController progression;
        private LobbyPhase recordsReturnPhase;
        private float elapsed, commandAt;
        private bool initialized, internalReset;
        private MenuAction? pending;
        private DifficultyLevel replayDifficulty;
        public LobbyPhase Phase { get; private set; }
        public bool IsMenuReady => Phase == LobbyPhase.Menu;
        public float ReturnDuration => returnDuration;
        public GameSessionSnapshot LastResult { get; private set; }
        public HangarLobbyView View => view;
        public LocalRankingController Ranking => ranking;
        public void Configure(Transform room, Transform left, Transform right, TMP_FontAsset typeface)
        { hangar=room; leftDoor=left; rightDoor=right; font=typeface; }
        private void Start()
        {
            flow=GameFlowManager.Instance; reset=FindAnyObjectByType<OperationsResetController>();
            result=FindAnyObjectByType<ResultController>(); ready=GetComponent<ReadyScreenController>();
            difficulty=GetComponent<DifficultyManager>(); difficultyView=GetComponent<DifficultySelectionView>();
            router=FindAnyObjectByType<GazeInputRouter>(); gaze=FindAnyObjectByType<EyeTracking.GazeRaycaster>();
            audio=FindAnyObjectByType<AudioManager>(); presentation=GetComponent<SfPresentationDirector>();
            launch=FindAnyObjectByType<LaunchSequenceController>(); environment=FindAnyObjectByType<CinematicEnvironmentView>();
            progression=StageProgressionController.Instance??FindAnyObjectByType<StageProgressionController>();
            if (flow==null || reset==null || ready==null || font==null || hangar==null || Camera.main==null) { enabled=false; return; }
            ranking=GetComponent<LocalRankingController>()??gameObject.AddComponent<LocalRankingController>();
            ranking.Initialize(flow,result);
            view=gameObject.AddComponent<HangarLobbyView>();
            view.Build(Camera.main,hangar,leftDoor,rightDoor,font,Queue);
            initialized=true; Bind();
        }
        private void Bind()
        {
            flow.OnStateChanged-=StateChanged; flow.OnStateChanged+=StateChanged;
            reset.ResultTimeoutHandler=BeginReturn;
        }
        private void OnEnable() { if(initialized) Bind(); }
        private void OnDisable()
        {
            if(!initialized) return;
            flow.OnStateChanged-=StateChanged;
            if(reset!=null && reset.ResultTimeoutHandler==BeginReturn) reset.ResultTimeoutHandler=null;
            Hide();
            if(flow!=null && flow.CurrentState==GameState.Result) { result?.ShowResult(); reset?.ResumeResultTimer(); }
        }
        private void OnDestroy() { if(view!=null) Destroy(view); }
        public bool BeginReturn()
        {
            if(!isActiveAndEnabled || !initialized || flow.CurrentState!=GameState.Result) return false;
            if(Phase!=LobbyPhase.Hidden) return true;
            LastResult=result.Snapshot; result.HideResult(); reset.CancelResultTimer();
            SetPhase(LobbyPhase.Returning); audio?.TryPlay(AudioCue.EngineStart); return true;
        }
        public void Queue(MenuAction action)
        {
            if(pending.HasValue || !Allowed(action)) return;
            pending=action; commandAt=Time.unscaledTime+.24f;
        }
        private bool Allowed(MenuAction action) => action switch
        {
            MenuAction.Replay or MenuAction.Difficulty or MenuAction.Recalibrate or MenuAction.Standby => Phase==LobbyPhase.Menu,
            MenuAction.RetryStage => Phase==LobbyPhase.Menu && LastResult != null && LastResult.MissionFailed && progression != null && progression.AllowRetryStage,
            MenuAction.Ranking => Phase==LobbyPhase.Menu || Phase==LobbyPhase.Standby,
            MenuAction.RecentRecords or MenuAction.TopRecords or MenuAction.BestRecords or MenuAction.NextRecordPage => Phase==LobbyPhase.Records,
            MenuAction.Back => Phase==LobbyPhase.Difficulty || Phase==LobbyPhase.Calibrating || Phase==LobbyPhase.Records,
            MenuAction.StartNewPlayer => Phase==LobbyPhase.Standby,
            _ => false
        };
        private void Execute(MenuAction action)
        {
            if(!Allowed(action)) return;
            switch(action)
            {
                case MenuAction.Difficulty:
                    SetPhase(LobbyPhase.Difficulty); difficulty.SetLobbySelection(true);
                    difficultyView.SetLobbyContext(view.DifficultyAnchor); break;
                case MenuAction.Back:
                    if(Phase==LobbyPhase.Records && recordsReturnPhase==LobbyPhase.Standby) SetPhase(LobbyPhase.Standby);
                    else if(Phase==LobbyPhase.Calibrating) flow.ChangeState(GameState.Result);
                    else EnterMenu(); break;
                case MenuAction.Ranking:
                    recordsReturnPhase=Phase; SetPhase(LobbyPhase.Records);
                    view.Records.Present(ranking.Store,HangarRecordView.RecordTab.Recent); break;
                case MenuAction.RecentRecords: view.Records.Present(ranking.Store,HangarRecordView.RecordTab.Recent); break;
                case MenuAction.TopRecords: view.Records.Present(ranking.Store,HangarRecordView.RecordTab.TopTen); break;
                case MenuAction.BestRecords: view.Records.Present(ranking.Store,HangarRecordView.RecordTab.Best); break;
                case MenuAction.NextRecordPage: view.Records.NextPage(ranking.Store); break;
                case MenuAction.Recalibrate:
                    SetPhase(LobbyPhase.Calibrating); flow.StartCalibration(GameState.Result); break;
                case MenuAction.Replay:
                    replayDifficulty=difficulty.Selected;
                    SetPhase(LobbyPhase.ReplayPreparing);
                    ResetInsideLobby(true);
                    difficulty.Select(replayDifficulty); break;
                case MenuAction.RetryStage:
                    if (progression != null && progression.RetryStage()) Hide();
                    break;
                case MenuAction.Standby:
                    SetPhase(LobbyPhase.Standby); LastResult=null; ResetInsideLobby(false); break;
                case MenuAction.StartNewPlayer:
                    Hide(); ready.SetVisible(true); ready.StartExperience(); break;
            }
        }
        private void ResetInsideLobby(bool preserveCalibration)
        {
            internalReset=true; router.PreserveCalibrationDuringReset=preserveCalibration;
            try { reset.ResetExperience(); }
            finally { internalReset=false; router.PreserveCalibrationDuringReset=false; }
            ready.SetVisible(false); presentation?.DismissBootForLobby();
        }
        private void EnterMenu()
        {
            reset.CancelResultTimer(); result.HideResult();
            difficultyView.SetLobbyContext(null); difficulty.SetLobbySelection(false);
            SetPhase(LobbyPhase.Menu); audio?.TryPlay(AudioCue.CalibrationComplete);
            view.SetRetryAvailable(LastResult != null && LastResult.MissionFailed && progression != null && progression.AllowRetryStage);
        }
        private void SetPhase(LobbyPhase phase)
        {
            Phase=phase; elapsed=0; pending=null;
            view.Show(phase,LastResult,difficulty.Settings.lockDifficultySelection,difficulty.Selected);
        }
        private void StateChanged(GameState state)
        {
            if(internalReset) return;
            if(Phase==LobbyPhase.Hidden) return;
            if(Phase==LobbyPhase.Calibrating && state==GameState.Result) { EnterMenu(); return; }
            if(Phase==LobbyPhase.Calibrating && state==GameState.EyeCalibration) return;
            if(Phase==LobbyPhase.Departing && (state==GameState.Launch || state==GameState.Countdown)) return;
            Hide();
        }
        private void Hide()
        {
            bool wasVisible=Phase!=LobbyPhase.Hidden;
            Phase=LobbyPhase.Hidden; pending=null; LastResult=null;
            difficultyView?.SetLobbyContext(null); difficulty?.SetLobbySelection(false);
            if(view!=null && wasVisible) view.Hide();
            if(wasVisible && environment!=null && flow!=null) environment.ApplyState(flow.CurrentState);
        }
        private void LateUpdate()
        {
            if(!initialized || Phase==LobbyPhase.Hidden || Time.timeScale == 0) return;
            elapsed+=Time.unscaledDeltaTime;
            if(Mouse.current!=null && Mouse.current.leftButton.wasPressedThisFrame && gaze.CurrentTarget is HangarMenuButton button) button.Choose();
            if(pending.HasValue && Time.unscaledTime>=commandAt) { var action=pending.Value; pending=null; Execute(action); }
            if(Phase==LobbyPhase.Hidden) return;
            if(Phase==LobbyPhase.Returning && elapsed>=returnDuration) EnterMenu();
            if(Phase==LobbyPhase.ReplayPreparing && elapsed>=.35f && (audio==null || !audio.IsBgmTransitioning))
            {
                SetPhase(LobbyPhase.Departing); flow.StartLaunch();
            }
            if(Phase==LobbyPhase.ReplayPreparing || Phase==LobbyPhase.Standby || (Phase==LobbyPhase.Records && flow.CurrentState==GameState.Boot)) ready.SetVisible(false);
            if(Phase==LobbyPhase.Records) view.Records.RefreshPersistenceNotice(ranking.Store);
            view.Animate(Phase,elapsed/returnDuration,launch.DoorOpenAmount,launch.SpeedAmount);
        }
    }
}
