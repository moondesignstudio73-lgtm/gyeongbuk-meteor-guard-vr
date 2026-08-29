using System;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using UnityEngine;
using UnityEngine.SceneManagement;
using MeteorDefenseVR.Progression;

namespace MeteorDefenseVR.Difficulty
{
    [DefaultExecutionOrder(-500), DisallowMultipleComponent]
    public sealed class DifficultyManager : MonoBehaviour
    {
        [SerializeField] private DifficultySettings settings;
        private GameFlowManager flow;
        private MeteorSpawner spawner;
        private BossClimaxController boss;
        private GameSessionStats stats;
        private GazeRaycaster raycaster;
        private StageProgressionController progression;
        private float baselineAssist;
        private bool initialized;
        public DifficultySettings Settings => settings;
        public DifficultyLevel Selected { get; private set; } = DifficultyLevel.Normal;
        public DifficultyBalance Session { get; private set; }
        public bool SessionFrozen { get; private set; }
        private bool lobbySelection;
        public void SetLobbySelection(bool value) { lobbySelection = value; SelectionChanged?.Invoke(); }
        public bool CanSelect => initialized && flow != null && (flow.CurrentState == GameState.Boot || (lobbySelection && flow.CurrentState == GameState.Result)) && !settings.lockDifficultySelection;
        public bool CombatActive => flow != null && (flow.CurrentState == GameState.Playing || flow.CurrentState == GameState.BossMeteor);
        public event Action SelectionChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install() { SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded; }
        private static void Loaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var ready = root.GetComponentInChildren<ReadyScreenController>(true);
                if (ready == null) continue;
                var manager = ready.GetComponent<DifficultyManager>();
                if (manager == null) manager = ready.gameObject.AddComponent<DifficultyManager>();
                manager.Initialize();
                return;
            }
        }
        private void Start() => Initialize();
        public void Initialize()
        {
            if (initialized) return;
            if (settings == null) settings = Resources.Load<DifficultySettings>("DifficultySettings");
            if (settings == null || settings.easy == null || settings.normal == null || settings.hard == null)
            { Debug.LogError("[Difficulty] Missing difficulty assets; baseline gameplay remains available."); enabled = false; return; }
            flow = GameFlowManager.Instance;
            spawner = FindAnyObjectByType<MeteorSpawner>(FindObjectsInactive.Include);
            // All selection variants are ready before the first combat; the pool still warms over multiple frames.
            int maximumConcurrent = Mathf.Max(settings.easy.maxConcurrentMeteors, settings.normal.maxConcurrentMeteors);
            maximumConcurrent = Mathf.Max(maximumConcurrent, settings.hard.maxConcurrentMeteors);
            if (settings.extreme != null) maximumConcurrent = Mathf.Max(maximumConcurrent, settings.extreme.maxConcurrentMeteors);
            spawner?.SetPrewarmCapacity(maximumConcurrent);
            boss = FindAnyObjectByType<BossClimaxController>(FindObjectsInactive.Include);
            stats = FindAnyObjectByType<GameSessionStats>(FindObjectsInactive.Include);
            raycaster = FindAnyObjectByType<GazeRaycaster>(FindObjectsInactive.Include);
            progression = StageProgressionController.Instance ?? FindAnyObjectByType<StageProgressionController>(FindObjectsInactive.Include);
            if (raycaster != null) baselineAssist = raycaster.GazeMagnetismRadius;
            initialized = true; Selected = settings.Default;
            Bind(); ApplySelection();
            if (flow != null && flow.CurrentState != GameState.Boot) Transition(GameState.Boot, flow.CurrentState);
        }
        private void OnEnable() { if (initialized) Bind(); }
        private void Bind() { if (flow != null) { flow.OnStateTransitioned -= Transition; flow.OnStateTransitioned += Transition; } }
        private void OnDisable()
        {
            if (flow != null) flow.OnStateTransitioned -= Transition;
            if (raycaster != null) raycaster.GazeMagnetismRadius = baselineAssist;
        }
        public bool Select(DifficultyLevel level)
        {
            if (!CanSelect || !DifficultySettings.Valid(level) || Selected == level) return false;
            Selected = level; ApplySelection(); return true;
        }
        [ContextMenu("Apply Operator Defaults (Ready only)")]
        public void ApplyOperatorDefaults()
        {
            if (!initialized || flow == null || flow.CurrentState != GameState.Boot) return;
            Selected = settings.Default; ApplySelection();
        }
        private void ApplySelection()
        {
            Session = settings.Get(Selected).Capture(Selected);
            stats?.SetDifficulty(Selected);
            SelectionChanged?.Invoke();
        }
        private void Transition(GameState from, GameState to)
        {
            // Runs before OnStateChanged consumers spawn meteors or reset session stats.
            if (to == GameState.Reset || to == GameState.Boot)
            {
                lobbySelection = false;
                SessionFrozen = false;
                if (settings.resetDifficultyAfterSession || settings.lockDifficultySelection) Selected = settings.Default;
                ApplySelection();
                spawner?.ClearDifficulty(); boss?.ClearDifficulty();
                progression?.ResetProgressionState();
            }
            else if (!SessionFrozen)
            {
                if (settings.lockDifficultySelection) Selected = settings.Default;
                ApplySelection(); SessionFrozen = true;
            }
            if (to == GameState.Playing || to == GameState.BossMeteor)
            {
                spawner?.SetDifficulty(Session); boss?.SetDifficulty(Session);
                if (to == GameState.Playing)
                {
                    progression ??= StageProgressionController.Instance ?? FindAnyObjectByType<StageProgressionController>(FindObjectsInactive.Include);
                    progression?.PrepareForPlaying(Session, from == GameState.BossMeteor);
                }
            }
            if (raycaster != null) raycaster.GazeMagnetismRadius = CombatActive ? Session.AssistRadius : baselineAssist;
        }
    }
}
