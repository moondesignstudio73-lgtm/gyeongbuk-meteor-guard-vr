using System;
using System.IO;
using MeteorDefenseVR.Audio;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Launch;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Visual;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.PcInput
{
    public enum ExperienceMode { PublicExperience, Operator, Development }
    public enum MeteorDifficulty { Gentle, Standard }

    [Serializable]
    public sealed class ExperienceSettings
    {
        public ExperienceMode experienceMode = ExperienceMode.PublicExperience;
        public PcGazeMode inputMode = PcGazeMode.Auto;
        [Range(.5f, .8f)] public float lockDuration = .65f;
        [Tooltip("Main meteor-wave duration; excludes briefing, calibration and results.")]
        [Range(40f, 60f)] public float gameDuration = 46f;
        public MeteorDifficulty meteorDifficulty = MeteorDifficulty.Gentle;
        public bool webcamGazeAssist = true;
        [Range(0f, .7f)] public float audioVolume = .65f;
        public bool debugUI = true;
        public bool operatorControls = true;

        public void Validate()
        {
            if (!Enum.IsDefined(typeof(ExperienceMode), experienceMode)) experienceMode = ExperienceMode.PublicExperience;
            if (!Enum.IsDefined(typeof(PcGazeMode), inputMode)) inputMode = PcGazeMode.Auto;
            if (!Enum.IsDefined(typeof(MeteorDifficulty), meteorDifficulty)) meteorDifficulty = MeteorDifficulty.Gentle;
            lockDuration = FiniteClamp(lockDuration, .5f, .8f, .65f);
            gameDuration = FiniteClamp(gameDuration, 40f, 60f, 46f);
            audioVolume = FiniteClamp(audioVolume, 0f, .7f, .65f);
        }
        private static float FiniteClamp(float value, float min, float max, float fallback)
            => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
    }

    [DefaultExecutionOrder(300), DisallowMultipleComponent]
    public sealed class ExperienceConfiguration : MonoBehaviour
    {
        public const string FileName = "MeteorDefense-settings.json";
        [SerializeField] private ExperienceSettings settings = new ExperienceSettings();
        [SerializeField] private GazeInputRouter router;
        [SerializeField] private PcTestControls controls;
        public ExperienceSettings Settings => settings;
        public ExperienceMode Mode => settings.experienceMode;
        public bool ShowDebug => Mode == ExperienceMode.Development && settings.debugUI;
        public bool ShowOperator => Mode != ExperienceMode.PublicExperience && settings.operatorControls;
        public void Configure(GazeInputRouter input, PcTestControls pcControls) { router = input; controls = pcControls; }

        private void Awake()
        {
            // Local, optional file. Never creates or changes settings on a normal player launch.
            if (!Application.isEditor)
            {
                string path = Path.Combine(Application.dataPath, "..", FileName);
                try { if (File.Exists(path)) JsonUtility.FromJsonOverwrite(File.ReadAllText(path), settings); }
                catch (Exception ex) { Debug.LogWarning("[Experience] Settings could not be read; Inspector defaults used: " + ex.GetType().Name); }
            }
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                if (arg == "--experience=public") settings.experienceMode = ExperienceMode.PublicExperience;
                if (arg == "--experience=operator") settings.experienceMode = ExperienceMode.Operator;
                if (arg == "--experience=development") settings.experienceMode = ExperienceMode.Development;
            }
            settings.Validate();
            router?.SetStartupMode(settings.inputMode);
            controls?.SetExperienceConfiguration(this);
        }

        private void Start() => Apply();
        [ContextMenu("Apply operation tuning (Play Mode)")]
        public void Apply()
        {
            settings.Validate();
            if (settings.experienceMode != ExperienceMode.Development && router != null && router.Webcam != null)
            {
                router.Webcam.ShowWebcamPreview = false;
                router.Webcam.ShowFaceDebug = false;
            }
            Object.FindAnyObjectByType<CinematicEnvironmentView>()?.SetDurations(5f, 5f);
            // Respect the authored Launch timings; reapplying PC settings must not overwrite them.
            Object.FindAnyObjectByType<MissionCompleteController>()?.SetResultDelay(4.2f);
            Object.FindAnyObjectByType<OperationsResetController>()?.SetResultDuration(8f);
            Object.FindAnyObjectByType<AudioManager>()?.SetMasterVolume(settings.audioVolume);
            bool gentle = settings.meteorDifficulty == MeteorDifficulty.Gentle;
            Object.FindAnyObjectByType<MeteorSpawner>()?.SetOperationTuning(settings.gameDuration, gentle ? .85f : 1f, gentle ? 1.1f : 1f);
            Object.FindAnyObjectByType<MeteorSpawner>()?.SetSpawnLimits(gentle ? 28f : 35f, gentle ? 20f : 25f);
            controls?.SetExperienceConfiguration(this);
        }
    }
}
