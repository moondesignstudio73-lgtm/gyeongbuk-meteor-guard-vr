using System;
using MeteorDefenseVR.Core;
using UnityEngine;

namespace MeteorDefenseVR.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class ResultController : MonoBehaviour
    {
        [SerializeField] private GameSessionStats stats;
        [SerializeField] private SessionRankSettings rankSettings;
        [SerializeField, Range(7f, 10f)] private float displayDuration = 8f;
        private GameFlowManager gameFlow;

        public GameSessionSnapshot Snapshot { get; private set; }
        public string Rank { get; private set; } = "C";
        public bool IsVisible { get; private set; }
        public float DisplayDuration => displayDuration;

        public event Action<GameSessionSnapshot, string> ResultShown;
        public event Action ResultHidden;

        private void Awake() => gameFlow = GameFlowManager.Instance;
        private void OnEnable() => BindGameFlow(GameFlowManager.Instance);

        private void OnDisable()
        {
            if (gameFlow != null) gameFlow.OnStateChanged -= HandleStateChanged;
        }

        public void Configure(GameSessionStats sessionStats, SessionRankSettings settings)
        {
            stats = sessionStats;
            rankSettings = settings;
        }

        public void BindGameFlow(GameFlowManager flow)
        {
            if (gameFlow != null) gameFlow.OnStateChanged -= HandleStateChanged;
            gameFlow = flow;
            if (isActiveAndEnabled && gameFlow != null) gameFlow.OnStateChanged += HandleStateChanged;
        }

        public void ShowResult()
        {
            stats?.StopSession();
            Snapshot = stats != null ? stats.CreateSnapshot() : new GameSessionSnapshot();
            Rank = rankSettings != null ? rankSettings.Evaluate(Snapshot) : "C";
            IsVisible = true;
            ResultShown?.Invoke(Snapshot, Rank);
        }

        public void HideResult()
        {
            if (!IsVisible) return;
            IsVisible = false;
            ResultHidden?.Invoke();
        }

        private void HandleStateChanged(GameState state)
        {
            if (state == GameState.Result) ShowResult();
            else HideResult();
        }
    }
}
