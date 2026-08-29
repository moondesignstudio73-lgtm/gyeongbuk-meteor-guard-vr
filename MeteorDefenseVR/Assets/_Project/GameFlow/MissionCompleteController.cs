using System;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.UI;
using UnityEngine;

namespace MeteorDefenseVR.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class MissionCompleteController : MonoBehaviour
    {
        [SerializeField] private MeteorSpawner spawner;
        [SerializeField] private MissionHudController missionHud;
        [SerializeField, Range(3f, 5f)] private float resultDelay = 4f;
        [SerializeField, Min(0.05f)] private float meteorCleanupDuration = 0.65f;
        [SerializeField] private bool autoStartFromGameFlow = true;

        private GameFlowManager gameFlow;
        private float elapsed;
        private bool presentationStarted;
        public const float PresentationDelay = 0.9f;
        public float PresentationProgress => IsRunning ? Mathf.Clamp01((elapsed - PresentationDelay) / .45f) : 0f;

        public bool IsRunning { get; private set; }
        public bool IsComplete { get; private set; }
        public string StatusMessage { get; private set; } = string.Empty;

        public event Action<string> StatusChanged;
        public event Action SequenceStarted;
        public event Action PresentationStarted;
        public event Action SequenceCompleted;

        private void Awake() => gameFlow = GameFlowManager.Instance;
        private void OnEnable() => BindGameFlow(GameFlowManager.Instance);
        private void Update() { if (Time.timeScale > 0) Tick(Time.unscaledDeltaTime); }

        private void OnDisable()
        {
            if (gameFlow != null) gameFlow.OnStateChanged -= HandleStateChanged;
            IsRunning = false;
        }

        public void Configure(MeteorSpawner meteorSpawner, MissionHudController hud)
        {
            spawner = meteorSpawner;
            missionHud = hud;
        }

        public void BindGameFlow(GameFlowManager flow)
        {
            if (gameFlow != null) gameFlow.OnStateChanged -= HandleStateChanged;
            gameFlow = flow;
            if (isActiveAndEnabled && gameFlow != null) gameFlow.OnStateChanged += HandleStateChanged;
        }

        public void SetResultDelay(float seconds) => resultDelay = Mathf.Clamp(seconds, 3f, 5f);

        public void BeginMissionComplete()
        {
            if (IsRunning) return;
            IsRunning = true;
            IsComplete = false;
            elapsed = 0f;
            presentationStarted = false;
            spawner?.StopSpawning(false);
            CleanupRemainingMeteors();
            SetStatus("MISSION COMPLETE\n지구 방어 성공!");
            missionHud?.ShowWarning(string.Empty, 0f);
            SequenceStarted?.Invoke();
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!IsRunning) return;
            elapsed += Mathf.Max(0f, unscaledDeltaTime);
            if (!presentationStarted && elapsed >= PresentationDelay)
            {
                presentationStarted = true;
                PresentationStarted?.Invoke();
            }
            if (elapsed < resultDelay) return;
            IsRunning = false;
            IsComplete = true;
            SetStatus(string.Empty);
            SequenceCompleted?.Invoke();
            gameFlow?.ShowResult();
        }

        public void ResetSequence()
        {
            IsRunning = false;
            IsComplete = false;
            elapsed = 0f;
            SetStatus(string.Empty);
        }

        private void CleanupRemainingMeteors()
        {
            if (spawner == null) return;
            MeteorController[] remaining = spawner.DetachActiveMeteorsForCleanup();
            foreach (MeteorController meteor in remaining)
            {
                if (meteor == null) continue;
                meteor.ResetMeteor(false);
                MeteorCleanupFade fade = meteor.GetComponent<MeteorCleanupFade>();
                if (fade == null) fade = meteor.gameObject.AddComponent<MeteorCleanupFade>();
                fade.Begin(meteorCleanupDuration);
            }
        }

        private void HandleStateChanged(GameState state)
        {
            if (!autoStartFromGameFlow) return;
            if (state == GameState.MissionComplete) BeginMissionComplete();
            else if (state != GameState.Result && IsRunning) ResetSequence();
        }

        private void SetStatus(string message)
        {
            StatusMessage = message ?? string.Empty;
            StatusChanged?.Invoke(StatusMessage);
        }
    }
}
