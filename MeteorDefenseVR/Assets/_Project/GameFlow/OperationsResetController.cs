using MeteorDefenseVR.Core;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.Launch;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.Tutorial;
using MeteorDefenseVR.UI;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.VFX;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MeteorDefenseVR.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class OperationsResetController : MonoBehaviour
    {
        [SerializeField] private bool enableOperatorKeys = true;
        [SerializeField, Range(7f, 10f)] private float resultDisplayDuration = 8f;
        [SerializeField] private MeteorSpawner spawner;
        [SerializeField] private BossClimaxController boss;
        [SerializeField] private LaserWeapon laserWeapon;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private GameSessionStats stats;
        [SerializeField] private MissionHudController hud;
        [SerializeField] private ResultController result;
        [SerializeField] private TutorialController tutorial;
        [SerializeField] private PracticeController practice;
        [SerializeField] private CalibrationSequenceController calibration;
        [SerializeField] private LaunchSequenceController launch;
        [SerializeField] private MissionCompleteController missionComplete;

        private GameFlowManager gameFlow;
        private float resultElapsed;
        private bool resultTimerRunning;

        public bool ResultTimerRunning => resultTimerRunning;
        public bool OperatorKeysEnabled => enableOperatorKeys;

        private void Awake() => gameFlow = GameFlowManager.Instance;
        private void OnEnable() => BindGameFlow(GameFlowManager.Instance);
        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
            PollOperatorKeys();
        }

        private void OnDisable()
        {
            if (gameFlow != null) gameFlow.OnStateChanged -= HandleStateChanged;
        }

        public void Configure(
            MeteorSpawner meteorSpawner, BossClimaxController bossController, LaserWeapon weapon,
            PlayerHealth health, GameSessionStats sessionStats, MissionHudController missionHud,
            ResultController resultController, TutorialController tutorialController,
            PracticeController practiceController, CalibrationSequenceController calibrationController,
            LaunchSequenceController launchController, MissionCompleteController completeController)
        {
            spawner = meteorSpawner;
            boss = bossController;
            laserWeapon = weapon;
            playerHealth = health;
            stats = sessionStats;
            hud = missionHud;
            result = resultController;
            tutorial = tutorialController;
            practice = practiceController;
            calibration = calibrationController;
            launch = launchController;
            missionComplete = completeController;
        }

        public void BindGameFlow(GameFlowManager flow)
        {
            if (gameFlow != null) gameFlow.OnStateChanged -= HandleStateChanged;
            gameFlow = flow;
            if (isActiveAndEnabled && gameFlow != null) gameFlow.OnStateChanged += HandleStateChanged;
        }

        public void SetOperatorKeysEnabled(bool enabled) => enableOperatorKeys = enabled;
        public void SetResultDuration(float duration) => resultDisplayDuration = Mathf.Clamp(duration, 7f, 10f);

        public void Tick(float unscaledDeltaTime)
        {
            if (!resultTimerRunning) return;
            resultElapsed += Mathf.Max(0f, unscaledDeltaTime);
            if (resultElapsed >= resultDisplayDuration) ResetExperience();
        }

        public void ResetExperience()
        {
            resultTimerRunning = false;
            resultElapsed = 0f;
            spawner?.StopSpawning(true);
            boss?.ResetClimax();
            DestroyRuntimeEffects();
            laserWeapon?.ResetWeapon();
            playerHealth?.ResetHealth();
            stats?.ResetSession(0);
            stats?.StopSession();
            hud?.ResetSession();
            result?.HideResult();
            tutorial?.ResetTutorial();
            practice?.ResetPractice();
            calibration?.ResetSequence();
            launch?.ResetLaunch();
            missionComplete?.ResetSequence();
            CombatVfxController vfx = FindFirstObjectByType<CombatVfxController>(FindObjectsInactive.Include);
            vfx?.ResetEffects();
            StopAllAudio();
            gameFlow?.ResetGame();
        }

        public void HandleOperatorCommand(OperatorCommand command)
        {
            switch (command)
            {
                case OperatorCommand.Reset: ResetExperience(); break;
                case OperatorCommand.Intro: gameFlow?.StartGame(); break;
                case OperatorCommand.Calibration: gameFlow?.StartCalibration(); break;
                case OperatorCommand.Tutorial: gameFlow?.StartTutorial(); break;
                case OperatorCommand.MainGame: gameFlow?.StartMainGame(); break;
                case OperatorCommand.Boss:
                    gameFlow?.StartBoss();
                    boss?.BeginClimax();
                    break;
                case OperatorCommand.Result: gameFlow?.ShowResult(); break;
            }
        }

        private void HandleStateChanged(GameState state)
        {
            resultTimerRunning = state == GameState.Result;
            resultElapsed = 0f;
        }

        private void PollOperatorKeys()
        {
            if (!enableOperatorKeys) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.rKey.wasPressedThisFrame) HandleOperatorCommand(OperatorCommand.Reset);
            else if (keyboard.digit1Key.wasPressedThisFrame) HandleOperatorCommand(OperatorCommand.Intro);
            else if (keyboard.digit2Key.wasPressedThisFrame) HandleOperatorCommand(OperatorCommand.Calibration);
            else if (keyboard.digit3Key.wasPressedThisFrame) HandleOperatorCommand(OperatorCommand.Tutorial);
            else if (keyboard.digit4Key.wasPressedThisFrame) HandleOperatorCommand(OperatorCommand.MainGame);
            else if (keyboard.digit5Key.wasPressedThisFrame) HandleOperatorCommand(OperatorCommand.Boss);
            else if (keyboard.digit6Key.wasPressedThisFrame) HandleOperatorCommand(OperatorCommand.Result);
        }

        private static void DestroyRuntimeEffects()
        {
            MeteorController[] meteors = FindObjectsByType<MeteorController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (MeteorController meteor in meteors)
            {
                if (meteor != null && meteor.GetComponentInParent<MeteorSpawner>(true) != null) continue;
                DestroySceneObject(meteor != null ? meteor.gameObject : null);
            }
            BossExplosionEffect[] explosions = FindObjectsByType<BossExplosionEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (BossExplosionEffect effect in explosions) DestroySceneObject(effect != null ? effect.gameObject : null);
        }

        private static void StopAllAudio()
        {
            AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (AudioSource source in sources) if (source != null) source.Stop();
        }

        private static void DestroySceneObject(GameObject target)
        {
            if (target == null || !target.scene.IsValid()) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
