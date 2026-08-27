using System;
using MeteorDefenseVR.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MeteorDefenseVR.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class ReadyScreenController : MonoBehaviour
    {
        [SerializeField] private bool allowKeyboardStart = true;
        private GameFlowManager gameFlow;

        public bool IsReadyVisible { get; private set; }
        public event Action<bool> VisibilityChanged;

        private void Awake() => gameFlow = GameFlowManager.Instance;
        private void OnEnable() => BindGameFlow(GameFlowManager.Instance);

        private void Start()
        {
            SetVisible(gameFlow == null || gameFlow.CurrentState == GameState.Boot);
        }

        private void Update()
        {
            if (!allowKeyboardStart || !IsReadyVisible) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                StartExperience();
        }

        private void OnDisable()
        {
            if (gameFlow != null) gameFlow.OnStateChanged -= HandleStateChanged;
        }

        public void BindGameFlow(GameFlowManager flow)
        {
            if (gameFlow != null) gameFlow.OnStateChanged -= HandleStateChanged;
            gameFlow = flow;
            if (isActiveAndEnabled && gameFlow != null) gameFlow.OnStateChanged += HandleStateChanged;
        }

        public void StartExperience()
        {
            if (!IsReadyVisible) return;
            SetVisible(false);
            gameFlow?.StartGame();
        }

        public void SetVisible(bool visible)
        {
            if (IsReadyVisible == visible) return;
            IsReadyVisible = visible;
            VisibilityChanged?.Invoke(IsReadyVisible);
        }

        private void HandleStateChanged(GameState state) => SetVisible(state == GameState.Boot);
    }
}
