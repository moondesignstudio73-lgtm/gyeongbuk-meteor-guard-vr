using System;
using System.Collections;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using UnityEngine;

namespace MeteorDefenseVR.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class TutorialController : MonoBehaviour
    {
        [Header("Meteor")]
        [SerializeField] private GameObject tutorialMeteorPrefab;
        [SerializeField] private Transform spawnPoint;

        [Header("Timing")]
        [SerializeField, Min(1f)] private float reminderDelay = 5f;
        [SerializeField, Min(0f)] private float successDisplayDuration = 1.5f;
        [SerializeField] private bool autoStartFromGameFlow = true;

        private GameFlowManager gameFlow;
        private TutorialPulseView pulseView;
        private Coroutine completionRoutine;
        private float elapsedWithoutSuccess;
        private bool reminderShown;

        public MeteorController ActiveMeteor { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsComplete { get; private set; }
        public string Instruction { get; private set; } = string.Empty;
        public float SuccessDisplayDuration { get => successDisplayDuration; set => successDisplayDuration = Mathf.Max(0f, value); }
        public float ReminderDelay { get => reminderDelay; set => reminderDelay = Mathf.Max(1f, value); }

        public event Action<string> InstructionChanged;
        public event Action<MeteorController> TutorialMeteorSpawned;
        public event Action ReminderActivated;
        public event Action TutorialSucceeded;

        private void Awake() => gameFlow = GameFlowManager.Instance;

        private void OnEnable()
        {
            gameFlow = GameFlowManager.Instance;
            if (gameFlow != null) gameFlow.OnStateChanged += HandleStateChanged;
        }

        private void Start()
        {
            if (autoStartFromGameFlow && gameFlow != null && gameFlow.CurrentState == GameState.Tutorial)
                BeginTutorial();
        }

        private void Update() => Tick(Time.unscaledDeltaTime);

        private void OnDisable()
        {
            if (gameFlow != null) gameFlow.OnStateChanged -= HandleStateChanged;
            CleanupTutorialMeteor();
        }

        public void Configure(GameObject meteorPrefab, Transform tutorialSpawnPoint)
        {
            tutorialMeteorPrefab = meteorPrefab;
            spawnPoint = tutorialSpawnPoint;
        }

        public void BeginTutorial()
        {
            CancelCompletion();
            CleanupTutorialMeteor();
            elapsedWithoutSuccess = 0f;
            reminderShown = false;
            IsComplete = false;
            IsRunning = true;
            SetInstruction("운석을 바라보세요.");

            GameObject meteorObject = CreateTutorialMeteor();
            ActiveMeteor = meteorObject.GetComponent<MeteorController>();
            if (ActiveMeteor == null) ActiveMeteor = meteorObject.AddComponent<MeteorController>();
            ActiveMeteor.Configure(MeteorType.Tutorial, 1f, 0f, 0, 0, Mathf.Max(1f, ActiveMeteor.Size));
            ActiveMeteor.SetMovementDirection(Vector3.zero);
            ActiveMeteor.Destroyed += HandleMeteorDestroyed;

            ActiveMeteor.Spawn();
            pulseView = meteorObject.GetComponent<TutorialPulseView>();
            if (pulseView == null) pulseView = meteorObject.AddComponent<TutorialPulseView>();
            pulseView.SetPulsing(false);
            TutorialMeteorSpawned?.Invoke(ActiveMeteor);
        }

        public void Tick(float deltaTime)
        {
            if (!IsRunning || IsComplete || reminderShown) return;
            elapsedWithoutSuccess += Mathf.Max(0f, deltaTime);
            if (elapsedWithoutSuccess < reminderDelay) return;
            reminderShown = true;
            SetInstruction("운석을 바라봐 주세요.");
            pulseView?.SetPulsing(true);
            ReminderActivated?.Invoke();
        }

        public void ResetTutorial()
        {
            CancelCompletion();
            CleanupTutorialMeteor();
            IsRunning = false;
            IsComplete = false;
            reminderShown = false;
            elapsedWithoutSuccess = 0f;
            SetInstruction(string.Empty);
        }

        private GameObject CreateTutorialMeteor()
        {
            Vector3 position = spawnPoint != null ? spawnPoint.position : new Vector3(0f, 1.6f, 6f);
            Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
            if (tutorialMeteorPrefab != null) return Instantiate(tutorialMeteorPrefab, position, rotation, transform);

            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fallback.name = "TutorialMeteor_Fallback";
            fallback.transform.SetParent(transform);
            fallback.transform.SetPositionAndRotation(position, rotation);
            return fallback;
        }

        private void HandleMeteorDestroyed(MeteorController meteor)
        {
            if (!IsRunning || IsComplete || meteor != ActiveMeteor) return;
            IsComplete = true;
            IsRunning = false;
            pulseView?.SetPulsing(false);
            SetInstruction("좋아요! 이런 방식으로 운석을 파괴하세요.");
            TutorialSucceeded?.Invoke();
            completionRoutine = StartCoroutine(CompleteAfterDelay());
        }

        private IEnumerator CompleteAfterDelay()
        {
            if (successDisplayDuration > 0f) yield return new WaitForSecondsRealtime(successDisplayDuration);
            gameFlow?.StartPractice();
            completionRoutine = null;
        }

        private void HandleStateChanged(GameState state)
        {
            if (!autoStartFromGameFlow) return;
            if (state == GameState.Tutorial) BeginTutorial();
            else if (IsRunning || IsComplete) ResetTutorial();
        }

        private void CleanupTutorialMeteor()
        {
            if (ActiveMeteor == null) return;
            ActiveMeteor.Destroyed -= HandleMeteorDestroyed;
            GameObject meteorObject = ActiveMeteor.gameObject;
            ActiveMeteor = null;
            pulseView = null;
            if (Application.isPlaying) Destroy(meteorObject);
            else DestroyImmediate(meteorObject);
        }

        private void CancelCompletion()
        {
            if (completionRoutine == null) return;
            StopCoroutine(completionRoutine);
            completionRoutine = null;
        }

        private void SetInstruction(string message)
        {
            Instruction = message ?? string.Empty;
            InstructionChanged?.Invoke(Instruction);
        }
    }
}
