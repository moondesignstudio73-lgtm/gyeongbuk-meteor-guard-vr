using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Difficulty;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace MeteorDefenseVR.PcInput
{
    public sealed class PcTestControls : MonoBehaviour
    {
        [SerializeField] private GazeInputRouter router;
        [SerializeField] private Camera gameCamera;
        [SerializeField] private bool enableDebugKeys = true;
        [SerializeField] private bool allowRightDragLook = true;
        [SerializeField] private bool showPcUi = true;
        [SerializeField] private bool hideUiInVr = true;
        [SerializeField] private bool showGazeCursor = true;
        [SerializeField, Range(.5f, .8f)] private float pcLockDuration = .65f;
        [SerializeField, Range(.1f, .4f)] private float targetGracePeriod = .25f;
        [SerializeField, Range(1f, 1.5f)] private float targetColliderExpansion = 1.25f;
        [SerializeField, Range(0f, .3f)] private float gazeMagnetism = .15f;
        private OperationsResetController operations;
        private GazeRaycaster raycaster;
        private float nextAssistScan;
        private float originalMagnetism;
        private ExperienceConfiguration experience;
        private DifficultyManager difficulty;
        private readonly Dictionary<GazeLockOnTarget, Vector3> originalTuning = new Dictionary<GazeLockOnTarget, Vector3>();
        private readonly List<GazeLockOnTarget> retiredTargets = new List<GazeLockOnTarget>();
        public bool ShowUi => showPcUi && (!hideUiInVr || router.ActiveMode != PcGazeMode.VREyeTracking);
        public bool ShowGazeCursor { get => showGazeCursor; set => showGazeCursor = value; }
        public bool DebugKeysEnabled => enableDebugKeys && (experience == null || experience.Mode == ExperienceMode.Development);
        public bool ShowDebugUi => experience == null || experience.ShowDebug;
        public bool ShowOperatorUi => experience == null || experience.ShowOperator;
        public bool OperatorControlsEnabled => experience == null || experience.Settings.operatorControls;
        public void SetExperienceConfiguration(ExperienceConfiguration configuration)
        {
            experience = configuration;
            pcLockDuration = configuration.Settings.lockDuration;
            bool assist = configuration.Settings.webcamGazeAssist;
            targetGracePeriod = assist ? .25f : .15f;
            targetColliderExpansion = assist ? 1.25f : 1f;
            gazeMagnetism = assist ? .15f : 0f;
        }
        public void Configure(GazeInputRouter input, Camera camera) { router = input; gameCamera = camera; }
        private void Start()
        {
            operations = Object.FindAnyObjectByType<OperationsResetController>();
            difficulty = Object.FindAnyObjectByType<DifficultyManager>();
            operations?.SetOperatorKeysEnabled(false);
            raycaster = Object.FindAnyObjectByType<GazeRaycaster>();
            if (raycaster != null) originalMagnetism = raycaster.GazeMagnetismRadius;
        }
        private void Update()
        {
            // Also cover focus loss / submit within the registration screen: a key edge
            // can outlive the focused field for the rest of the current input update.
            if (router != null && router.TextEntryOpen) return;
            // Typing a participant name/number must not trigger R reset or F-key scene jumps.
            var selected = UnityEngine.EventSystems.EventSystem.current != null ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && selected.GetComponent<TMPro.TMP_InputField>() is { isFocused: true }) return;
            Keyboard keyboard = Keyboard.current;
            // Emergency controls stay available in Public mode without showing their legend.
            if (OperatorControlsEnabled && keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame) { ResetExperience(); return; }
                // ESC belongs to the unified look/pause controller, never Application.Quit.
            }
            if (router.Simulation != null && router.Simulation.IsPaused) return;
            if (router.ActiveMode == PcGazeMode.VREyeTracking)
            {
                RestoreTargetTuning();
                return;
            }
            if (Time.unscaledTime >= nextAssistScan)
            {
                nextAssistScan = Time.unscaledTime + .5f;
                retiredTargets.Clear();
                foreach (var entry in originalTuning) if (entry.Key == null) retiredTargets.Add(entry.Key);
                foreach (var retired in retiredTargets) originalTuning.Remove(retired);
                if (raycaster != null) raycaster.GazeMagnetismRadius = difficulty != null && difficulty.CombatActive ? difficulty.Session.AssistRadius : gazeMagnetism;
                foreach (GazeLockOnTarget target in Object.FindObjectsByType<GazeLockOnTarget>())
                {
                    if (target.HasDifficultyTuning) { originalTuning.Remove(target); continue; }
                    if (!originalTuning.ContainsKey(target)) originalTuning[target] = new Vector3(target.LockOnDuration, target.GracePeriod, target.ColliderExpansion);
                    target.LockOnDuration = pcLockDuration;
                    target.GracePeriod = targetGracePeriod;
                    target.ConfigureComfortTolerance(targetColliderExpansion);
                }
            }
            if (DebugKeysEnabled && keyboard != null)
            {
                if (keyboard.f1Key.wasPressedThisFrame) JumpTo(GameState.Intro);
                else if (keyboard.f2Key.wasPressedThisFrame) JumpTo(GameState.EyeCalibration);
                else if (keyboard.f3Key.wasPressedThisFrame) JumpTo(GameState.Tutorial);
                else if (keyboard.f4Key.wasPressedThisFrame) JumpTo(GameState.Practice);
                else if (keyboard.f5Key.wasPressedThisFrame) JumpTo(GameState.Playing);
                else if (keyboard.f6Key.wasPressedThisFrame) JumpTo(GameState.BossMeteor);
                else if (keyboard.f7Key.wasPressedThisFrame) JumpTo(GameState.Result);
            }
            // No camera writes here: look and reset pose have one owner.
        }
        private void RestoreTargetTuning()
        {
            foreach (var entry in originalTuning)
            {
                if (entry.Key == null || entry.Key.HasDifficultyTuning) continue;
                entry.Key.LockOnDuration = entry.Value.x;
                entry.Key.GracePeriod = entry.Value.y;
                entry.Key.ConfigureComfortTolerance(entry.Value.z);
            }
            originalTuning.Clear();
            if (raycaster != null) raycaster.GazeMagnetismRadius = difficulty != null && difficulty.CombatActive ? difficulty.Session.AssistRadius : originalMagnetism;
        }
        private void OnDisable() => RestoreTargetTuning();
        public void ResetExperience() { operations?.ResetExperience(); router.Simulation?.Recenter(); }
        public void JumpTo(GameState state)
        {
            if (!DebugKeysEnabled) return;
            operations?.ResetExperience();
            if (state == GameState.BossMeteor) operations?.HandleOperatorCommand(OperatorCommand.Boss);
            else GameFlowManager.Instance?.ChangeState(state);
        }
        public void Exit()
        {
            router.Webcam?.StopCapture();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
