using System;
using System.Collections;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using UnityEngine;

namespace MeteorDefenseVR.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class PracticeController : MonoBehaviour
    {
        [Header("Meteor")]
        [SerializeField] private GameObject practiceMeteorPrefab;
        [SerializeField] private Transform[] spawnPoints = Array.Empty<Transform>();
        [SerializeField, Range(1, 3)] private int meteorCount = 3;
        [SerializeField, Min(0f)] private float interMeteorDelay = 0.25f;

        [Header("Flow")]
        [SerializeField] private bool includePracticeScoreInSession;
        [SerializeField, Min(0f)] private float readyDisplayDuration = 1.2f;
        [SerializeField] private bool autoStartFromGameFlow = true;

        private GameFlowManager gameFlow;
        private Coroutine sequenceRoutine;
        // Two slots preserve zero-delay respawn: the dying meteor finishes its callbacks
        // after the next meteor is spawned, so it must not deactivate that next meteor.
        private readonly ReusableMeteorSlot[] meteorSlots = { new ReusableMeteorSlot(), new ReusableMeteorSlot() };
        private ReusableMeteorSlot activeSlot;
        private int nextSlot;
        public int PreparedMeteorCount => meteorSlots[0].CreatedCount + meteorSlots[1].CreatedCount;
        public void PrewarmMeteor()
        {
            foreach (var slot in meteorSlots) slot.Prewarm(practiceMeteorPrefab, this, "PracticeMeteor_Fallback");
        }

        public MeteorController ActiveMeteor { get; private set; }
        public int TotalMeteors { get; private set; }
        public int DestroyedMeteors { get; private set; }
        public int RemainingMeteors => Mathf.Max(0, TotalMeteors - DestroyedMeteors);
        public int PracticeScore { get; private set; }
        public int ScoreForSession => includePracticeScoreInSession ? PracticeScore : 0;
        public bool IsRunning { get; private set; }
        public bool IsComplete { get; private set; }
        public string StatusMessage { get; private set; } = string.Empty;
        public int MeteorCount { get => meteorCount; set => meteorCount = Mathf.Clamp(value, 1, 3); }
        public float InterMeteorDelay { get => interMeteorDelay; set => interMeteorDelay = Mathf.Max(0f, value); }
        public float ReadyDisplayDuration { get => readyDisplayDuration; set => readyDisplayDuration = Mathf.Max(0f, value); }
        public bool IncludePracticeScoreInSession { get => includePracticeScoreInSession; set => includePracticeScoreInSession = value; }

        public event Action<int, int, int> ProgressChanged;
        public event Action<string> StatusChanged;
        public event Action<MeteorController> PracticeMeteorSpawned;
        public event Action PracticeCompleted;

        private void Awake() => gameFlow = GameFlowManager.Instance;

        private void OnEnable()
        {
            gameFlow = GameFlowManager.Instance;
            if (gameFlow != null) gameFlow.OnStateChanged += HandleStateChanged;
        }

        private void Start()
        {
            if (autoStartFromGameFlow && gameFlow != null && gameFlow.CurrentState == GameState.Practice)
                BeginPractice();
        }

        private void OnDisable()
        {
            if (gameFlow != null) gameFlow.OnStateChanged -= HandleStateChanged;
            ResetPractice();
        }

        public void Configure(GameObject meteorPrefab, Transform[] points)
        {
            practiceMeteorPrefab = meteorPrefab;
            spawnPoints = points ?? Array.Empty<Transform>();
        }

        public void BeginPractice()
        {
            ResetPractice();
            TotalMeteors = Mathf.Clamp(meteorCount, 1, 3);
            DestroyedMeteors = 0;
            PracticeScore = 0;
            IsRunning = true;
            IsComplete = false;
            SetStatus("연습 운석을 파괴하세요.");
            NotifyProgress();
            SpawnNextMeteor();
        }

        public void ResetPractice()
        {
            CancelRoutine();
            CleanupActiveMeteor();
            nextSlot = 0;
            TotalMeteors = 0;
            DestroyedMeteors = 0;
            PracticeScore = 0;
            IsRunning = false;
            IsComplete = false;
            SetStatus(string.Empty);
        }

        private void SpawnNextMeteor()
        {
            if (!IsRunning || DestroyedMeteors >= TotalMeteors) return;
            int index = Mathf.Clamp(DestroyedMeteors, 0, Mathf.Max(0, spawnPoints.Length - 1));
            Transform point = spawnPoints.Length > 0 ? spawnPoints[index] : null;
            Vector3 position = point != null ? point.position : new Vector3(0f, 1.6f, 6f);
            Quaternion rotation = point != null ? point.rotation : Quaternion.identity;

            activeSlot = meteorSlots[nextSlot++ % meteorSlots.Length];
            GameObject meteorObject = activeSlot.Borrow(practiceMeteorPrefab, this, position, rotation, "PracticeMeteor_Fallback");

            ActiveMeteor = meteorObject.GetComponent<MeteorController>();
            if (ActiveMeteor == null) ActiveMeteor = meteorObject.AddComponent<MeteorController>();
            if (practiceMeteorPrefab == null)
                ActiveMeteor.Configure(MeteorType.Normal, 1f, 0f, 0, 100, 1f);
            ActiveMeteor.SetMovementDirection(Vector3.zero);
            ActiveMeteor.Destroyed += HandleMeteorDestroyed;
            ActiveMeteor.ReachedPlayerEvent += HandleMeteorReachedPlayer;
            ActiveMeteor.Spawn();
            PracticeMeteorSpawned?.Invoke(ActiveMeteor);
        }

        private void HandleMeteorDestroyed(MeteorController meteor)
        {
            if (!IsRunning || meteor != ActiveMeteor) return;
            meteor.Destroyed -= HandleMeteorDestroyed;
            meteor.ReachedPlayerEvent -= HandleMeteorReachedPlayer;
            PracticeScore += meteor.Score;
            DestroyedMeteors++;
            ActiveMeteor = null;
            NotifyProgress();
            sequenceRoutine = StartCoroutine(ContinueAfterDestroy());
        }

        private void HandleMeteorReachedPlayer(MeteorController meteor, int _)
        {
            if (!IsRunning || meteor != ActiveMeteor) return;
            meteor.Destroyed -= HandleMeteorDestroyed;
            meteor.ReachedPlayerEvent -= HandleMeteorReachedPlayer;
            ActiveMeteor = null;
            sequenceRoutine = StartCoroutine(RespawnMissedMeteor());
        }

        private IEnumerator ContinueAfterDestroy()
        {
            if (DestroyedMeteors < TotalMeteors)
            {
                if (interMeteorDelay > 0f) yield return new MeteorDefenseVR.Core.PauseAwareDelay(interMeteorDelay);
                SpawnNextMeteor();
                sequenceRoutine = null;
                yield break;
            }

            IsRunning = false;
            IsComplete = true;
            SetStatus("준비 완료!");
            PracticeCompleted?.Invoke();
            if (readyDisplayDuration > 0f) yield return new MeteorDefenseVR.Core.PauseAwareDelay(readyDisplayDuration);
            gameFlow?.StartLaunch();
            sequenceRoutine = null;
        }

        private IEnumerator RespawnMissedMeteor()
        {
            if (interMeteorDelay > 0f) yield return new MeteorDefenseVR.Core.PauseAwareDelay(interMeteorDelay);
            SpawnNextMeteor();
            sequenceRoutine = null;
        }

        private void HandleStateChanged(GameState state)
        {
            if (!autoStartFromGameFlow) return;
            if (state == GameState.Practice) BeginPractice();
            else if (IsRunning || IsComplete) ResetPractice();
        }

        private void NotifyProgress() => ProgressChanged?.Invoke(RemainingMeteors, TotalMeteors, PracticeScore);

        private void SetStatus(string status)
        {
            StatusMessage = status ?? string.Empty;
            StatusChanged?.Invoke(StatusMessage);
        }

        private void CleanupActiveMeteor()
        {
            if (ActiveMeteor == null) return;
            ActiveMeteor.Destroyed -= HandleMeteorDestroyed;
            ActiveMeteor.ReachedPlayerEvent -= HandleMeteorReachedPlayer;
            ActiveMeteor = null;
            activeSlot?.Release();
        }

        private void CancelRoutine()
        {
            if (sequenceRoutine == null) return;
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }
    }
}
