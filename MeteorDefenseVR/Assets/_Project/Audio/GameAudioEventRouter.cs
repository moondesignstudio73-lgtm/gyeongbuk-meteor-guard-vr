using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Launch;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Player;
using UnityEngine;

namespace MeteorDefenseVR.Audio
{
    [DisallowMultipleComponent]
    public sealed class GameAudioEventRouter : MonoBehaviour
    {
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private CalibrationSequenceController calibration;
        [SerializeField] private LaserWeapon laserWeapon;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private BossClimaxController bossClimax;
        [SerializeField] private LaunchSequenceController launchSequence;
        [SerializeField] private MissionCompleteController missionComplete;
        [SerializeField] private ResultController result;

        private GameFlowManager gameFlow;

        private void OnEnable()
        {
            BindGameFlow(GameFlowManager.Instance);
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            BindGameFlow(null);
        }

        public void Configure(
            AudioManager manager,
            CalibrationSequenceController calibrationController,
            LaserWeapon weapon,
            PlayerHealth health,
            BossClimaxController boss,
            LaunchSequenceController launch,
            MissionCompleteController complete,
            ResultController resultController)
        {
            Unsubscribe();
            audioManager = manager;
            calibration = calibrationController;
            laserWeapon = weapon;
            playerHealth = health;
            bossClimax = boss;
            launchSequence = launch;
            missionComplete = complete;
            result = resultController;
            if (isActiveAndEnabled) Subscribe();
        }

        public void BindGameFlow(GameFlowManager flow)
        {
            if (gameFlow != null) gameFlow.OnStateChanged -= HandleStateChanged;
            gameFlow = flow;
            if (isActiveAndEnabled && gameFlow != null) gameFlow.OnStateChanged += HandleStateChanged;
        }

        private void Subscribe()
        {
            GazeLockOnTarget.GlobalFocusStarted -= HandleFocusStarted;
            GazeLockOnTarget.GlobalLocked -= HandleLocked;
            GazeLockOnTarget.GlobalFocusStarted += HandleFocusStarted;
            GazeLockOnTarget.GlobalLocked += HandleLocked;
            if (calibration != null)
            {
                calibration.PointCompleted -= HandleCalibrationPoint;
                calibration.CalibrationCompleted -= HandleCalibrationComplete;
                calibration.PointCompleted += HandleCalibrationPoint;
                calibration.CalibrationCompleted += HandleCalibrationComplete;
            }
            if (laserWeapon != null)
            {
                laserWeapon.Fired -= HandleLaserFired;
                laserWeapon.Hit -= HandleLaserHit;
                laserWeapon.ScorePopupRequested -= HandleScore;
                laserWeapon.Fired += HandleLaserFired;
                laserWeapon.Hit += HandleLaserHit;
                laserWeapon.ScorePopupRequested += HandleScore;
            }
            if (playerHealth != null)
            {
                playerHealth.DamageTaken -= HandlePlayerDamage;
                playerHealth.DamageTaken += HandlePlayerDamage;
            }
            if (bossClimax != null)
            {
                bossClimax.StageChanged -= HandleBossStage;
                bossClimax.BossDestroyed -= HandleBossDestroyed;
                bossClimax.StageChanged += HandleBossStage;
                bossClimax.BossDestroyed += HandleBossDestroyed;
            }
            if (launchSequence != null)
            {
                launchSequence.AudioCueRequested -= HandleLaunchCue;
                launchSequence.AudioCueRequested += HandleLaunchCue;
            }
            if (missionComplete != null)
            {
                missionComplete.SequenceStarted -= HandleMissionComplete;
                missionComplete.SequenceStarted += HandleMissionComplete;
            }
            if (result != null)
            {
                result.ResultShown -= HandleResultShown;
                result.ResultShown += HandleResultShown;
            }
        }

        private void Unsubscribe()
        {
            GazeLockOnTarget.GlobalFocusStarted -= HandleFocusStarted;
            GazeLockOnTarget.GlobalLocked -= HandleLocked;
            if (calibration != null)
            {
                calibration.PointCompleted -= HandleCalibrationPoint;
                calibration.CalibrationCompleted -= HandleCalibrationComplete;
            }
            if (laserWeapon != null)
            {
                laserWeapon.Fired -= HandleLaserFired;
                laserWeapon.Hit -= HandleLaserHit;
                laserWeapon.ScorePopupRequested -= HandleScore;
            }
            if (playerHealth != null) playerHealth.DamageTaken -= HandlePlayerDamage;
            if (bossClimax != null)
            {
                bossClimax.StageChanged -= HandleBossStage;
                bossClimax.BossDestroyed -= HandleBossDestroyed;
            }
            if (launchSequence != null) launchSequence.AudioCueRequested -= HandleLaunchCue;
            if (missionComplete != null) missionComplete.SequenceStarted -= HandleMissionComplete;
            if (result != null) result.ResultShown -= HandleResultShown;
        }

        private void HandleStateChanged(GameState state)
        {
            if (state == GameState.Intro) audioManager?.TryPlay(AudioCue.IntroSpaceAmbient);
            else if (state == GameState.Boot || state == GameState.Reset) audioManager?.StopAll();
        }

        private void HandleFocusStarted(GazeLockOnTarget _) => audioManager?.TryPlay(AudioCue.TargetFocus);
        private void HandleLocked(GazeLockOnTarget _) => audioManager?.TryPlay(AudioCue.LockOn);
        private void HandleCalibrationPoint(int _, CalibrationPoint __) => audioManager?.TryPlay(AudioCue.CalibrationPoint);
        private void HandleCalibrationComplete() => audioManager?.TryPlay(AudioCue.CalibrationComplete);
        private void HandleLaserFired(GazeLockOnTarget _, MeteorController __) => audioManager?.TryPlay(AudioCue.Laser);

        private void HandleLaserHit(MeteorController meteor, Vector3 _)
        {
            audioManager?.TryPlay(AudioCue.MeteorHit);
            if (meteor != null && meteor.State == MeteorLifecycleState.Destroyed)
                audioManager?.TryPlay(AudioCue.MeteorExplosion);
        }

        private void HandleScore(Vector3 _, int __) => audioManager?.TryPlay(AudioCue.Score);
        private void HandlePlayerDamage(int _, float __) => audioManager?.TryPlay(AudioCue.PlayerDamage);

        private void HandleBossStage(BossClimaxStage stage, string _)
        {
            if (stage != BossClimaxStage.Warning) return;
            audioManager?.TryPlay(AudioCue.Warning);
            audioManager?.TryPlay(AudioCue.BossWarning);
        }

        private void HandleBossDestroyed() => audioManager?.TryPlay(AudioCue.BossExplosion);

        private void HandleLaunchCue(LaunchAudioCue cue)
        {
            if (cue == LaunchAudioCue.Countdown) audioManager?.TryPlay(AudioCue.Countdown);
            else if (cue == LaunchAudioCue.Launch || cue == LaunchAudioCue.MissionStart)
                audioManager?.TryPlay(AudioCue.Launch);
        }

        private void HandleMissionComplete() => audioManager?.TryPlay(AudioCue.MissionComplete);
        private void HandleResultShown(GameSessionSnapshot _, string __) => audioManager?.TryPlay(AudioCue.Result);
    }
}
