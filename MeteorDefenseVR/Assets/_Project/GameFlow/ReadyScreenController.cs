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
        public bool InputBlocked { get; set; }
        // Optional platform gate; the core game flow does not depend on the PC assembly.
        public Func<bool> StartAllowed { get; set; }
        public bool CanStart => !InputBlocked && IsReadyVisible && Time.timeScale > 0
            && (StartAllowed == null || StartAllowed());
        public event Action<bool> VisibilityChanged;

        private void Awake() => gameFlow = GameFlowManager.Instance;
        private void OnEnable() => BindGameFlow(GameFlowManager.Instance);

        private void Start()
        {
            SetVisible(gameFlow == null || gameFlow.CurrentState == GameState.Boot);
        }

        private void Update()
        {
            if (!allowKeyboardStart || !CanStart) return;
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
            if (!CanStart) return;
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
