using UnityEngine;

namespace MeteorDefenseVR.Launch
{
    [DisallowMultipleComponent]
    public sealed class LaunchEnvironmentView : MonoBehaviour
    {
        [Header("Sequence")]
        [SerializeField] private LaunchSequenceController controller;
        [SerializeField] private TextMesh statusText;

        [Header("Environment")]
        [SerializeField] private Transform leftDoor;
        [SerializeField] private Transform rightDoor;
        [SerializeField] private Transform cockpitRoot;
        [SerializeField] private Transform starfieldRoot;
        [SerializeField] private Light[] engineLights;
        [SerializeField] private float doorTravel = 2.8f;
        [SerializeField] private float starSpeed = 12f;

        [Header("Audio Hooks")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip doorClip;
        [SerializeField] private AudioClip engineClip;
        [SerializeField] private AudioClip countdownClip;
        [SerializeField] private AudioClip launchClip;
        [SerializeField] private AudioClip missionStartClip;

        private Vector3 leftDoorClosed;
        private Vector3 rightDoorClosed;
        private Vector3 cockpitBasePosition;
        private float speedAmount;
        private bool positionsCaptured;

        private void Awake() => CapturePositions();

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            Unsubscribe();
            RestoreCockpitPosition();
        }

        private void Update()
        {
            AnimateStarfield();
            AnimateCockpit();
        }

        public void Configure(
            LaunchSequenceController sequenceController,
            TextMesh text,
            Transform left,
            Transform right,
            Transform cockpit,
            Transform stars,
            Light[] lights,
            AudioSource source)
        {
            Unsubscribe();
            controller = sequenceController;
            statusText = text;
            leftDoor = left;
            rightDoor = right;
            cockpitRoot = cockpit;
            starfieldRoot = stars;
            engineLights = lights;
            audioSource = source;
            positionsCaptured = false;
            CapturePositions();
            Subscribe();
            ApplyVisuals(0f, 0f, 0f);
        }

        public void ApplyVisuals(float doorOpen, float engine, float speed)
        {
            CapturePositions();
            if (leftDoor != null)
                leftDoor.localPosition = leftDoorClosed + Vector3.left * (doorTravel * doorOpen);
            if (rightDoor != null)
                rightDoor.localPosition = rightDoorClosed + Vector3.right * (doorTravel * doorOpen);

            if (engineLights != null)
            {
                foreach (Light engineLight in engineLights)
                {
                    if (engineLight == null) continue;
                    engineLight.enabled = engine > 0.01f;
                    engineLight.intensity = Mathf.Lerp(0f, 4f, engine);
                }
            }
            speedAmount = Mathf.Clamp01(speed);
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled || controller == null) return;
            controller.StageChanged -= HandleStageChanged;
            controller.VisualsChanged -= ApplyVisuals;
            controller.AudioCueRequested -= PlayCue;
            controller.StageChanged += HandleStageChanged;
            controller.VisualsChanged += ApplyVisuals;
            controller.AudioCueRequested += PlayCue;
            HandleStageChanged(controller.CurrentStage, controller.StatusMessage);
            ApplyVisuals(controller.DoorOpenAmount, controller.EngineAmount, controller.SpeedAmount);
        }

        private void Unsubscribe()
        {
            if (controller == null) return;
            controller.StageChanged -= HandleStageChanged;
            controller.VisualsChanged -= ApplyVisuals;
            controller.AudioCueRequested -= PlayCue;
        }

        private void HandleStageChanged(LaunchStage _, string message)
        {
            if (statusText != null) statusText.text = message ?? string.Empty;
        }

        private void PlayCue(LaunchAudioCue cue)
        {
            if (audioSource == null) return;
            AudioClip clip = cue switch
            {
                LaunchAudioCue.Door => doorClip,
                LaunchAudioCue.Engine => engineClip,
                LaunchAudioCue.Countdown => countdownClip,
                LaunchAudioCue.Launch => launchClip,
                LaunchAudioCue.MissionStart => missionStartClip,
                _ => null
            };
            if (clip != null) audioSource.PlayOneShot(clip);
        }

        private void CapturePositions()
        {
            if (positionsCaptured) return;
            if (leftDoor != null) leftDoorClosed = leftDoor.localPosition;
            if (rightDoor != null) rightDoorClosed = rightDoor.localPosition;
            if (cockpitRoot != null) cockpitBasePosition = cockpitRoot.localPosition;
            positionsCaptured = true;
        }

        private void AnimateStarfield()
        {
            if (starfieldRoot == null || speedAmount <= 0f) return;
            float travel = starSpeed * speedAmount * Time.unscaledDeltaTime;
            for (int i = 0; i < starfieldRoot.childCount; i++)
            {
                Transform star = starfieldRoot.GetChild(i);
                Vector3 position = star.localPosition;
                position.z -= travel;
                if (position.z < 2f) position.z += 14f;
                star.localPosition = position;
            }
        }

        private void AnimateCockpit()
        {
            if (cockpitRoot == null) return;
            // Motion is intentionally limited to cockpit geometry; the VR camera never moves.
            float amplitude = Mathf.Lerp(0f, 0.006f, speedAmount);
            cockpitRoot.localPosition = cockpitBasePosition + Vector3.right * (Mathf.Sin(Time.unscaledTime * 24f) * amplitude);
        }

        private void RestoreCockpitPosition()
        {
            if (cockpitRoot != null && positionsCaptured) cockpitRoot.localPosition = cockpitBasePosition;
        }
    }
}
