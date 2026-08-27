using System;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using UnityEngine;

namespace MeteorDefenseVR.Launch
{
    [DisallowMultipleComponent]
    public sealed class LaunchSequenceController : MonoBehaviour
    {
        [Header("Sequence Durations")]
        [SerializeField, Min(0f)] private float hangarDuration = 0.6f;
        [SerializeField, Min(0f)] private float doorDuration = 0.8f;
        [SerializeField, Min(0f)] private float engineDuration = 0.8f;
        [SerializeField, Min(0f)] private float countdownInterval = 0.8f;
        [SerializeField, Min(0f)] private float launchDuration = 1f;
        [SerializeField, Min(0f)] private float spaceDuration = 0.5f;
        [SerializeField, Min(0f)] private float missionStartDuration = 1f;
        [SerializeField] private bool autoStartFromGameFlow = true;

        private GameFlowManager gameFlow;
        private float stageElapsed;

        public LaunchStage CurrentStage { get; private set; } = LaunchStage.Idle;
        public string StatusMessage { get; private set; } = string.Empty;
        public bool IsRunning { get; private set; }
        public bool IsComplete { get; private set; }
        public float DoorOpenAmount { get; private set; }
        public float EngineAmount { get; private set; }
        public float SpeedAmount { get; private set; }

        public event Action<LaunchStage, string> StageChanged;
        public event Action<float, float, float> VisualsChanged;
        public event Action<LaunchAudioCue> AudioCueRequested;
        public event Action SequenceCompleted;

        private void Awake() => gameFlow = GameFlowManager.Instance;

        private void OnEnable()
        {
            BindGameFlow(GameFlowManager.Instance);
        }

        private void Start()
        {
            if (autoStartFromGameFlow && gameFlow != null && gameFlow.CurrentState == GameState.Launch)
                BeginLaunch();
        }

        private void Update()
        {
            if (IsRunning) Tick(Time.unscaledDeltaTime);
        }

        private void OnDisable()
        {
            if (gameFlow != null) gameFlow.OnStateChanged -= HandleStateChanged;
            IsRunning = false;
        }

        public void SetDurations(
            float hangar,
            float door,
            float engine,
            float countdown,
            float launch,
            float space,
            float missionStart)
        {
            hangarDuration = Mathf.Max(0f, hangar);
            doorDuration = Mathf.Max(0f, door);
            engineDuration = Mathf.Max(0f, engine);
            countdownInterval = Mathf.Max(0f, countdown);
            launchDuration = Mathf.Max(0f, launch);
            spaceDuration = Mathf.Max(0f, space);
            missionStartDuration = Mathf.Max(0f, missionStart);
        }

        public void BindGameFlow(GameFlowManager flow)
        {
            if (gameFlow != null) gameFlow.OnStateChanged -= HandleStateChanged;
            gameFlow = flow;
            if (isActiveAndEnabled && gameFlow != null)
                gameFlow.OnStateChanged += HandleStateChanged;
        }

        public void BeginLaunch()
        {
            IsRunning = true;
            IsComplete = false;
            stageElapsed = 0f;
            SetVisuals(0f, 0f, 0f);
            SetStage(LaunchStage.Hangar, "HANGAR");
        }

        public void ResetLaunch()
        {
            IsRunning = false;
            IsComplete = false;
            stageElapsed = 0f;
            CurrentStage = LaunchStage.Idle;
            StatusMessage = string.Empty;
            SetVisuals(0f, 0f, 0f);
            StageChanged?.Invoke(CurrentStage, StatusMessage);
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!IsRunning) return;
            float delta = Mathf.Max(0f, unscaledDeltaTime);
            stageElapsed += delta;

            switch (CurrentStage)
            {
                case LaunchStage.Hangar:
                    if (HasStageFinished(hangarDuration))
                        AdvanceTo(LaunchStage.DoorOpen, "DOOR OPEN", LaunchAudioCue.Door);
                    break;
                case LaunchStage.DoorOpen:
                    SetVisuals(StageProgress(doorDuration), EngineAmount, SpeedAmount);
                    if (HasStageFinished(doorDuration))
                    {
                        SetVisuals(1f, EngineAmount, SpeedAmount);
                        AdvanceTo(LaunchStage.EngineStart, "ENGINE START", LaunchAudioCue.Engine);
                    }
                    break;
                case LaunchStage.EngineStart:
                    SetVisuals(DoorOpenAmount, StageProgress(engineDuration), SpeedAmount);
                    if (HasStageFinished(engineDuration))
                    {
                        SetVisuals(DoorOpenAmount, 1f, SpeedAmount);
                        gameFlow?.StartCountdown();
                        AdvanceTo(LaunchStage.Countdown3, "3", LaunchAudioCue.Countdown);
                    }
                    break;
                case LaunchStage.Countdown3:
                    if (HasStageFinished(countdownInterval))
                        AdvanceTo(LaunchStage.Countdown2, "2", LaunchAudioCue.Countdown);
                    break;
                case LaunchStage.Countdown2:
                    if (HasStageFinished(countdownInterval))
                        AdvanceTo(LaunchStage.Countdown1, "1", LaunchAudioCue.Countdown);
                    break;
                case LaunchStage.Countdown1:
                    if (HasStageFinished(countdownInterval))
                        AdvanceTo(LaunchStage.Launch, "LAUNCH", LaunchAudioCue.Launch);
                    break;
                case LaunchStage.Launch:
                    SetVisuals(DoorOpenAmount, EngineAmount, StageProgress(launchDuration));
                    if (HasStageFinished(launchDuration))
                    {
                        SetVisuals(DoorOpenAmount, EngineAmount, 1f);
                        AdvanceTo(LaunchStage.Space, "SPACE");
                    }
                    break;
                case LaunchStage.Space:
                    if (HasStageFinished(spaceDuration))
                        AdvanceTo(LaunchStage.MissionStart, "MISSION START", LaunchAudioCue.MissionStart);
                    break;
                case LaunchStage.MissionStart:
                    if (HasStageFinished(missionStartDuration)) CompleteSequence();
                    break;
            }
        }

        private float StageProgress(float duration)
        {
            return duration <= 0f ? 1f : Mathf.Clamp01(stageElapsed / duration);
        }

        private bool HasStageFinished(float duration) => duration <= 0f || stageElapsed >= duration;

        private void AdvanceTo(LaunchStage stage, string message, LaunchAudioCue? cue = null)
        {
            stageElapsed = 0f;
            SetStage(stage, message, cue);
        }

        private void CompleteSequence()
        {
            IsRunning = false;
            IsComplete = true;
            stageElapsed = 0f;
            SetStage(LaunchStage.Complete, string.Empty);
            SequenceCompleted?.Invoke();
            gameFlow?.StartMainGame();
        }

        private void HandleStateChanged(GameState state)
        {
            if (!autoStartFromGameFlow) return;
            if (state == GameState.Launch)
            {
                BeginLaunch();
                return;
            }

            if (state != GameState.Countdown && state != GameState.Playing && IsRunning)
                ResetLaunch();
        }

        private void SetStage(LaunchStage stage, string message, LaunchAudioCue? cue = null)
        {
            CurrentStage = stage;
            StatusMessage = message ?? string.Empty;
            StageChanged?.Invoke(CurrentStage, StatusMessage);
            if (cue.HasValue) AudioCueRequested?.Invoke(cue.Value);
        }

        private void SetVisuals(float door, float engine, float speed)
        {
            DoorOpenAmount = Mathf.Clamp01(door);
            EngineAmount = Mathf.Clamp01(engine);
            SpeedAmount = Mathf.Clamp01(speed);
            VisualsChanged?.Invoke(DoorOpenAmount, EngineAmount, SpeedAmount);
        }

    }
}
