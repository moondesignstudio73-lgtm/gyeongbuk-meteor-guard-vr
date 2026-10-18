using MeteorDefenseVR.Audio;
using UnityEngine;
using UnityEngine.Rendering;

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
        [SerializeField] private float hangarExitTravel = 14f;

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
        private Vector3 hangarBasePosition;
        private float speedAmount;
        private float presentationSpeed;
        private float streakAccumulator;
        private bool positionsCaptured;
        private bool thrustStage;
        private Transform hangarRoot;
        private Camera playerCamera;
        private ParticleSystem starStreaks;
        private Material starStreakMaterial;
        private AudioManager audioManager;

        public int StarStreakCapacity => starStreaks != null ? starStreaks.main.maxParticles : 0;
        public float MaximumCockpitTravel => .004f;

        private void Awake()
        {
            ResolvePresentationReferences();
            CapturePositions();
            BuildStarStreaks();
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            Unsubscribe();
            RestoreCockpitPosition();
            ResetPresentation();
        }

        private void OnDestroy()
        {
            if (starStreakMaterial != null) Destroy(starStreakMaterial);
        }

        private void Update()
        {
            float target = thrustStage ? speedAmount : 0f;
            presentationSpeed = Mathf.MoveTowards(presentationSpeed, target, Time.unscaledDeltaTime * (thrustStage ? 2.2f : 2.8f));
            AnimateHangarExit();
            AnimateStarfield();
            AnimateStarStreaks();
            AnimateCockpit();
            if (audioManager != null) audioManager.SetPresentationSfxPitch(Mathf.Lerp(1f, 1.18f, presentationSpeed));
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
            ResolvePresentationReferences();
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
                    engineLight.intensity = Mathf.Lerp(0f, 3.2f, engine) + speed * 1.8f;
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

        private void HandleStageChanged(LaunchStage stage, string message)
        {
            thrustStage = stage == LaunchStage.Launch;
            if (statusText != null) statusText.text = message ?? string.Empty;
            if (stage == LaunchStage.Idle || stage == LaunchStage.Hangar) ResetPresentation();
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
            if (hangarRoot != null) hangarBasePosition = hangarRoot.localPosition;
            positionsCaptured = true;
        }

        private void ResolvePresentationReferences()
        {
            if (hangarRoot == null) hangarRoot = transform.Find("HangarVisuals");
            if (playerCamera == null) playerCamera = Camera.main;
            if (audioManager == null) audioManager = FindAnyObjectByType<AudioManager>(FindObjectsInactive.Include);
        }

        private void AnimateHangarExit()
        {
            float travel = hangarExitTravel * presentationSpeed;
            if (hangarRoot != null) hangarRoot.localPosition = hangarBasePosition + Vector3.back * travel;
            if (leftDoor != null) leftDoor.localPosition = leftDoorClosed + Vector3.left * (doorTravel * DoorAmount()) + Vector3.back * travel;
            if (rightDoor != null) rightDoor.localPosition = rightDoorClosed + Vector3.right * (doorTravel * DoorAmount()) + Vector3.back * travel;
        }

        private float DoorAmount() => controller != null ? controller.DoorOpenAmount : 0f;

        private void AnimateStarfield()
        {
            if (starfieldRoot == null || presentationSpeed <= 0f) return;
            float travel = starSpeed * presentationSpeed * Time.unscaledDeltaTime;
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
            float amplitude = Mathf.Lerp(0f, 0.004f, presentationSpeed);
            float phase = Time.unscaledTime * 26f;
            cockpitRoot.localPosition = cockpitBasePosition + new Vector3(Mathf.Sin(phase), Mathf.Sin(phase * .73f) * .45f, 0f) * amplitude;
        }

        private void BuildStarStreaks()
        {
            if (starStreaks != null) return;
            var root = new GameObject("LaunchStarStreaks_Pooled");
            root.transform.SetParent(transform, false);
            starStreaks = root.AddComponent<ParticleSystem>();
            var main = starStreaks.main;
            main.playOnAwake = false; main.loop = false; main.maxParticles = 56;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.useUnscaledTime = true;
            main.startLifetime = .55f; main.startSpeed = 0f; main.startSize = .025f;
            var emission = starStreaks.emission; emission.enabled = false; emission.rateOverTime = 0f; emission.rateOverDistance = 0f;
            var renderer = starStreaks.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.velocityScale = .09f; renderer.lengthScale = 5.5f; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                starStreakMaterial = new Material(shader) { name = "LaunchStarStreak_Runtime" };
                starStreakMaterial.SetColor("_BaseColor", new Color(.28f, .83f, 1f, .52f));
                renderer.sharedMaterial = starStreakMaterial;
            }
            starStreaks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void AnimateStarStreaks()
        {
            if (starStreaks == null || presentationSpeed < .12f) return;
            if (playerCamera == null) playerCamera = Camera.main;
            if (playerCamera == null) return;
            streakAccumulator += Time.unscaledDeltaTime * Mathf.Lerp(10f, 44f, presentationSpeed);
            int count = Mathf.Min(4, Mathf.FloorToInt(streakAccumulator));
            streakAccumulator -= count;
            for (int i = 0; i < count; i++)
            {
                float depth = Random.Range(8f, 19f);
                Vector3 position = playerCamera.transform.position + playerCamera.transform.forward * depth
                    + playerCamera.transform.right * Random.Range(-7f, 7f) + playerCamera.transform.up * Random.Range(-4f, 4f);
                var emit = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = -playerCamera.transform.forward * Mathf.Lerp(18f, 48f, presentationSpeed),
                    startLifetime = Mathf.Lerp(.34f, .62f, presentationSpeed),
                    startSize = Random.Range(.012f, .035f),
                    startColor = new Color(.35f, .86f, 1f, Mathf.Lerp(.18f, .58f, presentationSpeed))
                };
                starStreaks.Emit(emit, 1);
            }
        }

        private void ResetPresentation()
        {
            presentationSpeed = speedAmount = streakAccumulator = 0f;
            thrustStage = false;
            if (hangarRoot != null && positionsCaptured) hangarRoot.localPosition = hangarBasePosition;
            if (starStreaks != null) starStreaks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (audioManager != null) audioManager.SetPresentationSfxPitch(1f);
        }

        private void RestoreCockpitPosition()
        {
            if (cockpitRoot != null && positionsCaptured) cockpitRoot.localPosition = cockpitBasePosition;
        }
    }
}
