using System;
using MeteorDefenseVR.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MeteorDefenseVR.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class GameFlowManager : MonoBehaviour
    {
        public static GameFlowManager Instance { get; private set; }

        [Header("Startup")]
        [SerializeField] private bool persistBetweenScenes = true;

        [Header("Debug")]
        [SerializeField] private bool debugMode;
        [SerializeField] private GameState debugStartState = GameState.Boot;
        [SerializeField] private bool enableDebugInput = true;

        public GameState CurrentState { get; private set; } = GameState.Boot;
        public GameState PreviousState { get; private set; } = GameState.Boot;
        public bool IsTransitioning { get; private set; }

        public event Action<GameState> OnStateChanged;
        public event Action<GameState, GameState> OnStateTransitioned;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (persistBetweenScenes)
            {
                DontDestroyOnLoad(gameObject);
            }

            CurrentState = GameState.Boot;
            PreviousState = GameState.Boot;
        }

        private void Start()
        {
            if (debugMode && debugStartState != GameState.Boot)
            {
                ChangeState(debugStartState);
            }
            else
            {
                Debug.Log("[GameFlow] Boot");
            }
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!debugMode || !enableDebugInput) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f1Key.wasPressedThisFrame) ChangeState(GameState.Intro);
            else if (keyboard.f2Key.wasPressedThisFrame) StartCalibration();
            else if (keyboard.f3Key.wasPressedThisFrame) StartTutorial();
            else if (keyboard.f4Key.wasPressedThisFrame) StartMainGame();
            else if (keyboard.f5Key.wasPressedThisFrame) StartBoss();
            else if (keyboard.f6Key.wasPressedThisFrame) ShowResult();
            else if (keyboard.rKey.wasPressedThisFrame) ResetGame();
#endif
        }

        public bool ChangeState(GameState nextState)
        {
            if (IsTransitioning || CurrentState == nextState) return false;

            IsTransitioning = true;
            GameState fromState = CurrentState;

            try
            {
                PreviousState = fromState;
                CurrentState = nextState;
                Debug.Log($"[GameFlow] {fromState} -> {nextState}");
                OnStateTransitioned?.Invoke(fromState, nextState);
                OnStateChanged?.Invoke(nextState);
                return true;
            }
            finally
            {
                IsTransitioning = false;
            }
        }

        public void StartGame() => ChangeState(GameState.Intro);
        public void StartMissionBriefing() => ChangeState(GameState.MissionBriefing);
        public void StartCalibration() => ChangeState(GameState.EyeCalibration);
        public void StartTutorial() => ChangeState(GameState.Tutorial);
        public void StartPractice() => ChangeState(GameState.Practice);
        public void StartLaunch() => ChangeState(GameState.Launch);
        public void StartCountdown() => ChangeState(GameState.Countdown);
        public void StartMainGame() => ChangeState(GameState.Playing);
        public void StartBoss() => ChangeState(GameState.BossMeteor);
        public void CompleteMission() => ChangeState(GameState.MissionComplete);
        public void ShowResult() => ChangeState(GameState.Result);

        public void ResetGame()
        {
            if (CurrentState != GameState.Reset) ChangeState(GameState.Reset);
            ChangeState(GameState.Boot);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
