using System;
using System.Collections.Generic;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using UnityEngine;

namespace MeteorDefenseVR.Meteor
{
    [DisallowMultipleComponent]
    public sealed class MeteorSpawner : MonoBehaviour
    {
        [Header("Waves")]
        [SerializeField] private MeteorWaveData[] waves = Array.Empty<MeteorWaveData>();
        [SerializeField] private bool autoStartFromGameFlow = true;

        [Header("Spawn Space")]
        [SerializeField] private Transform spawnOrigin;
        [SerializeField] private Transform movementTarget;
        [SerializeField, Range(1f, 60f)] private float maxHorizontalAngle = 35f;
        [SerializeField, Range(1f, 45f)] private float maxVerticalAngle = 25f;
        [SerializeField, Min(1f)] private float minSpawnDistance = 10f;
        [SerializeField, Min(1f)] private float maxSpawnDistance = 14f;

        private readonly List<MeteorController> activeMeteors = new List<MeteorController>();
        private GameFlowManager gameFlow;
        private int waveIndex;
        private int entryIndex;
        private int spawnedInEntry;
        private float delayRemaining;

        public int TotalScheduled { get; private set; }
        public int SpawnedCount { get; private set; }
        public int CompletedCount { get; private set; }
        public int ActiveCount => activeMeteors.Count;
        public bool IsSpawning { get; private set; }
        public bool AllWavesSpawned { get; private set; }
        public float EstimatedGameDuration { get; private set; }
        public float MaxHorizontalAngle => maxHorizontalAngle;
        public float MaxVerticalAngle => maxVerticalAngle;
        public IReadOnlyList<MeteorController> ActiveMeteors => activeMeteors;

        public event Action<int, MeteorWaveData> WaveStarted;
        public event Action<int> SpawningStarted;
        public event Action<MeteorController, int, int> MeteorSpawned;
        public event Action<MeteorController> MeteorCompleted;
        public event Action AllSpawnsCompleted;

        private void Awake() => gameFlow = GameFlowManager.Instance;

        private void OnEnable() => BindGameFlow(GameFlowManager.Instance);

        private void Start()
        {
            if (autoStartFromGameFlow && gameFlow != null && gameFlow.CurrentState == GameState.Playing)
                BeginSpawning();
        }

        private void Update()
        {
            if (IsSpawning) Tick(Time.deltaTime);
        }

        private void OnDisable()
        {
            if (gameFlow != null) gameFlow.OnStateChanged -= HandleStateChanged;
            IsSpawning = false;
        }

        public void Configure(MeteorWaveData[] waveData, Transform origin, Transform target)
        {
            waves = waveData ?? Array.Empty<MeteorWaveData>();
            spawnOrigin = origin;
            movementTarget = target;
        }

        public void SetSpawnLimits(float horizontalDegrees, float verticalDegrees)
        {
            maxHorizontalAngle = Mathf.Clamp(horizontalDegrees, 1f, 60f);
            maxVerticalAngle = Mathf.Clamp(verticalDegrees, 1f, 45f);
        }

        public void BindGameFlow(GameFlowManager flow)
        {
            if (gameFlow != null) gameFlow.OnStateChanged -= HandleStateChanged;
            gameFlow = flow;
            if (isActiveAndEnabled && gameFlow != null) gameFlow.OnStateChanged += HandleStateChanged;
        }

        public void BeginSpawning()
        {
            ClearTrackedMeteors(false);
            waveIndex = 0;
            entryIndex = 0;
            spawnedInEntry = 0;
            SpawnedCount = 0;
            CompletedCount = 0;
            TotalScheduled = 0;
            EstimatedGameDuration = 0f;
            foreach (MeteorWaveData wave in waves)
            {
                if (wave == null) continue;
                TotalScheduled += wave.TotalMeteors;
                EstimatedGameDuration += wave.EstimatedDuration;
            }

            AllWavesSpawned = TotalScheduled == 0;
            IsSpawning = !AllWavesSpawned;
            SpawningStarted?.Invoke(TotalScheduled);
            if (AllWavesSpawned)
            {
                AllSpawnsCompleted?.Invoke();
                return;
            }
            MoveToNextValidEntry(true);
        }

        public void Tick(float deltaTime)
        {
            if (!IsSpawning) return;
            delayRemaining -= Mathf.Max(0f, deltaTime);
            if (delayRemaining > 0f) return;

            MeteorSpawnData entry = CurrentEntry();
            if (entry == null)
            {
                MoveToNextValidEntry(false);
                return;
            }

            SpawnMeteor(entry);
            spawnedInEntry++;
            if (spawnedInEntry < entry.Count)
            {
                delayRemaining = entry.SpawnInterval;
                return;
            }

            entryIndex++;
            spawnedInEntry = 0;
            MoveToNextValidEntry(false);
        }

        public Vector3 CalculateSpawnPosition(float horizontalInput, float verticalInput, float distanceInput, Vector2 requestedArea)
        {
            Transform origin = spawnOrigin != null ? spawnOrigin : transform;
            float horizontal = Mathf.Clamp(horizontalInput, -1f, 1f) * Mathf.Min(Mathf.Abs(requestedArea.x), maxHorizontalAngle);
            float vertical = Mathf.Clamp(verticalInput, -1f, 1f) * Mathf.Min(Mathf.Abs(requestedArea.y), maxVerticalAngle);
            float distance = Mathf.Lerp(minSpawnDistance, Mathf.Max(minSpawnDistance, maxSpawnDistance), Mathf.Clamp01(distanceInput));
            float x = Mathf.Tan(horizontal * Mathf.Deg2Rad);
            float y = Mathf.Tan(vertical * Mathf.Deg2Rad);
            Vector3 localDirection = new Vector3(x, y, 1f).normalized;
            return origin.position + origin.TransformDirection(localDirection) * distance;
        }

        public void StopSpawning(bool cleanupActiveMeteors)
        {
            IsSpawning = false;
            if (cleanupActiveMeteors) ClearTrackedMeteors(true);
        }

        public MeteorController[] DetachActiveMeteorsForCleanup()
        {
            MeteorController[] detached = activeMeteors.ToArray();
            foreach (MeteorController meteor in detached)
            {
                if (meteor == null) continue;
                meteor.Destroyed -= HandleMeteorDestroyed;
                meteor.ReachedPlayerEvent -= HandleMeteorReachedPlayer;
            }
            activeMeteors.Clear();
            return detached;
        }

        private void MoveToNextValidEntry(bool includeWaveDelay)
        {
            while (waveIndex < waves.Length)
            {
                MeteorWaveData wave = waves[waveIndex];
                if (wave == null || entryIndex >= wave.Spawns.Length)
                {
                    waveIndex++;
                    entryIndex = 0;
                    includeWaveDelay = true;
                    continue;
                }

                MeteorSpawnData entry = wave.Spawns[entryIndex];
                if (entry == null)
                {
                    entryIndex++;
                    continue;
                }

                if (entryIndex == 0) WaveStarted?.Invoke(waveIndex + 1, wave);
                delayRemaining = entry.Delay + (includeWaveDelay ? wave.WaveDelay : 0f);
                return;
            }

            IsSpawning = false;
            AllWavesSpawned = true;
            AllSpawnsCompleted?.Invoke();
        }

        private MeteorSpawnData CurrentEntry()
        {
            if (waveIndex < 0 || waveIndex >= waves.Length || waves[waveIndex] == null) return null;
            MeteorSpawnData[] entries = waves[waveIndex].Spawns;
            return entryIndex >= 0 && entryIndex < entries.Length ? entries[entryIndex] : null;
        }

        private void SpawnMeteor(MeteorSpawnData data)
        {
            Vector2 area = data.SpawnArea;
            Vector3 position = CalculateSpawnPosition(
                UnityEngine.Random.Range(-1f, 1f),
                UnityEngine.Random.Range(-1f, 1f),
                UnityEngine.Random.value,
                area);

            GameObject meteorObject;
            if (data.Prefab != null) meteorObject = Instantiate(data.Prefab, position, Quaternion.identity, transform);
            else
            {
                meteorObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                meteorObject.name = "Meteor_Fallback";
                meteorObject.transform.SetParent(transform);
                meteorObject.transform.position = position;
            }

            MeteorController meteor = meteorObject.GetComponent<MeteorController>();
            if (meteor == null) meteor = meteorObject.AddComponent<MeteorController>();
            meteor.Configure(data.MeteorType, data.Health, data.Speed, data.Damage, data.Score, data.Size);
            meteor.SetMovementTarget(movementTarget);
            if (meteorObject.GetComponent<GazeLockOnTarget>() == null)
            {
                GazeLockOnTarget lockTarget = meteorObject.AddComponent<GazeLockOnTarget>();
                lockTarget.Configure(meteor);
            }
            meteor.Destroyed += HandleMeteorDestroyed;
            meteor.ReachedPlayerEvent += HandleMeteorReachedPlayer;
            activeMeteors.Add(meteor);
            meteor.Spawn();
            SpawnedCount++;
            MeteorSpawned?.Invoke(meteor, SpawnedCount, TotalScheduled);
        }

        private void HandleMeteorDestroyed(MeteorController meteor) => CompleteMeteor(meteor);
        private void HandleMeteorReachedPlayer(MeteorController meteor, int _) => CompleteMeteor(meteor);

        private void CompleteMeteor(MeteorController meteor)
        {
            if (meteor == null || !activeMeteors.Remove(meteor)) return;
            meteor.Destroyed -= HandleMeteorDestroyed;
            meteor.ReachedPlayerEvent -= HandleMeteorReachedPlayer;
            CompletedCount++;
            MeteorCompleted?.Invoke(meteor);
        }

        private void ClearTrackedMeteors(bool destroyObjects)
        {
            foreach (MeteorController meteor in activeMeteors)
            {
                if (meteor == null) continue;
                meteor.Destroyed -= HandleMeteorDestroyed;
                meteor.ReachedPlayerEvent -= HandleMeteorReachedPlayer;
                if (!destroyObjects) continue;
                if (Application.isPlaying) Destroy(meteor.gameObject);
                else DestroyImmediate(meteor.gameObject);
            }
            activeMeteors.Clear();
        }

        private void HandleStateChanged(GameState state)
        {
            if (!autoStartFromGameFlow) return;
            if (state == GameState.Playing) BeginSpawning();
            else if (IsSpawning) StopSpawning(state == GameState.Reset || state == GameState.Result);
        }
    }
}
