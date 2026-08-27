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

        [Header("Pooling")]
        [SerializeField, Range(0, 5)] private int prewarmPerMeteorKind = 2;

        private readonly List<MeteorController> activeMeteors = new List<MeteorController>();
        private readonly List<MeteorController> pendingRelease = new List<MeteorController>();
        private readonly Dictionary<GameObject, Stack<GameObject>> prefabPools = new Dictionary<GameObject, Stack<GameObject>>();
        private readonly Dictionary<MeteorType, Stack<GameObject>> fallbackPools = new Dictionary<MeteorType, Stack<GameObject>>();
        private readonly Dictionary<MeteorController, GameObject> sourcePrefabs = new Dictionary<MeteorController, GameObject>();
        private readonly Dictionary<MeteorController, MeteorType> sourceFallbackTypes = new Dictionary<MeteorController, MeteorType>();
        private readonly Dictionary<GameObject, MeteorController> controllerCache = new Dictionary<GameObject, MeteorController>();
        private readonly Dictionary<GameObject, GazeLockOnTarget> lockTargetCache = new Dictionary<GameObject, GazeLockOnTarget>();
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
        public int InstantiatedCount { get; private set; }
        public int ReusedCount { get; private set; }
        public int PoolCount
        {
            get
            {
                int count = 0;
                foreach (Stack<GameObject> pool in prefabPools.Values) count += pool.Count;
                foreach (Stack<GameObject> pool in fallbackPools.Values) count += pool.Count;
                return count;
            }
        }
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
            FlushPendingReleases();
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
            ClearTrackedMeteors(true);
            FlushPendingReleases();
            PrewarmPools();
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
            FlushPendingReleases();
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
            FlushPendingReleases();
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
            foreach (MeteorController meteor in detached)
            {
                sourcePrefabs.Remove(meteor);
                sourceFallbackTypes.Remove(meteor);
                pendingRelease.Remove(meteor);
            }
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

            FlushPendingReleases();
            GameObject meteorObject = AcquireMeteor(data);
            meteorObject.transform.SetParent(transform, true);
            meteorObject.transform.SetPositionAndRotation(position, Quaternion.identity);
            meteorObject.SetActive(true);

            MeteorController meteor = GetMeteorController(meteorObject);
            meteor.Configure(data.MeteorType, data.Health, data.Speed, data.Damage, data.Score, data.Size);
            meteor.SetMovementTarget(movementTarget);
            GazeLockOnTarget lockTarget = GetLockTarget(meteorObject);
            lockTarget.Configure(meteor);
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
            if (!pendingRelease.Contains(meteor)) pendingRelease.Add(meteor);
        }

        private void ClearTrackedMeteors(bool destroyObjects)
        {
            foreach (MeteorController meteor in activeMeteors)
            {
                if (meteor == null) continue;
                meteor.Destroyed -= HandleMeteorDestroyed;
                meteor.ReachedPlayerEvent -= HandleMeteorReachedPlayer;
                if (destroyObjects) ReleaseMeteor(meteor);
            }
            activeMeteors.Clear();
        }

        private void PrewarmPools()
        {
            if (prewarmPerMeteorKind <= 0 || waves == null) return;
            var visitedPrefabs = new HashSet<GameObject>();
            var visitedFallbacks = new HashSet<MeteorType>();
            foreach (MeteorWaveData wave in waves)
            {
                if (wave == null) continue;
                foreach (MeteorSpawnData data in wave.Spawns)
                {
                    if (data == null) continue;
                    bool first = data.Prefab != null ? visitedPrefabs.Add(data.Prefab) : visitedFallbacks.Add(data.MeteorType);
                    if (!first) continue;
                    Stack<GameObject> pool = GetPool(data);
                    while (pool.Count < prewarmPerMeteorKind)
                    {
                        GameObject instance = CreateMeteorObject(data);
                        instance.SetActive(false);
                        pool.Push(instance);
                    }
                }
            }
        }

        private GameObject AcquireMeteor(MeteorSpawnData data)
        {
            Stack<GameObject> pool = GetPool(data);
            while (pool.Count > 0)
            {
                GameObject instance = pool.Pop();
                if (instance == null) continue;
                ReusedCount++;
                return instance;
            }
            return CreateMeteorObject(data);
        }

        private GameObject CreateMeteorObject(MeteorSpawnData data)
        {
            GameObject instance;
            if (data.Prefab != null) instance = Instantiate(data.Prefab, transform);
            else
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                instance.name = "Meteor_Fallback_" + data.MeteorType;
                instance.transform.SetParent(transform);
            }
            MeteorController meteor = instance.GetComponent<MeteorController>();
            if (meteor == null) meteor = instance.AddComponent<MeteorController>();
            controllerCache[instance] = meteor;
            GazeLockOnTarget lockTarget = instance.GetComponent<GazeLockOnTarget>();
            if (lockTarget == null) lockTarget = instance.AddComponent<GazeLockOnTarget>();
            lockTargetCache[instance] = lockTarget;
            if (data.Prefab != null) sourcePrefabs[meteor] = data.Prefab;
            else sourceFallbackTypes[meteor] = data.MeteorType;
            InstantiatedCount++;
            return instance;
        }

        private MeteorController GetMeteorController(GameObject instance)
        {
            if (controllerCache.TryGetValue(instance, out MeteorController cached) && cached != null) return cached;
            MeteorController meteor = instance.GetComponent<MeteorController>();
            if (meteor == null) meteor = instance.AddComponent<MeteorController>();
            controllerCache[instance] = meteor;
            return meteor;
        }

        private GazeLockOnTarget GetLockTarget(GameObject instance)
        {
            if (lockTargetCache.TryGetValue(instance, out GazeLockOnTarget cached) && cached != null) return cached;
            GazeLockOnTarget target = instance.GetComponent<GazeLockOnTarget>();
            if (target == null) target = instance.AddComponent<GazeLockOnTarget>();
            lockTargetCache[instance] = target;
            return target;
        }

        private Stack<GameObject> GetPool(MeteorSpawnData data)
        {
            if (data.Prefab != null)
            {
                if (!prefabPools.TryGetValue(data.Prefab, out Stack<GameObject> pool))
                {
                    pool = new Stack<GameObject>();
                    prefabPools.Add(data.Prefab, pool);
                }
                return pool;
            }
            if (!fallbackPools.TryGetValue(data.MeteorType, out Stack<GameObject> fallbackPool))
            {
                fallbackPool = new Stack<GameObject>();
                fallbackPools.Add(data.MeteorType, fallbackPool);
            }
            return fallbackPool;
        }

        private void FlushPendingReleases()
        {
            for (int i = pendingRelease.Count - 1; i >= 0; i--)
            {
                MeteorController meteor = pendingRelease[i];
                pendingRelease.RemoveAt(i);
                ReleaseMeteor(meteor);
            }
        }

        private void ReleaseMeteor(MeteorController meteor)
        {
            if (meteor == null) return;
            meteor.Destroyed -= HandleMeteorDestroyed;
            meteor.ReachedPlayerEvent -= HandleMeteorReachedPlayer;
            meteor.ResetMeteor(true);
            meteor.transform.SetParent(transform, true);
            if (sourcePrefabs.TryGetValue(meteor, out GameObject prefab))
            {
                if (!prefabPools.TryGetValue(prefab, out Stack<GameObject> pool))
                {
                    pool = new Stack<GameObject>();
                    prefabPools.Add(prefab, pool);
                }
                if (!pool.Contains(meteor.gameObject)) pool.Push(meteor.gameObject);
                return;
            }
            if (sourceFallbackTypes.TryGetValue(meteor, out MeteorType type))
            {
                if (!fallbackPools.TryGetValue(type, out Stack<GameObject> pool))
                {
                    pool = new Stack<GameObject>();
                    fallbackPools.Add(type, pool);
                }
                if (!pool.Contains(meteor.gameObject)) pool.Push(meteor.gameObject);
            }
        }

        private void HandleStateChanged(GameState state)
        {
            if (!autoStartFromGameFlow) return;
            if (state == GameState.Playing) BeginSpawning();
            else if (IsSpawning) StopSpawning(state == GameState.Reset || state == GameState.Result);
        }
    }
}
