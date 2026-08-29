using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Profiling;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.Progression;
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
        [SerializeField] private MeteorDefenseVR.Player.ShipCollisionBoundary impactBoundary;
        public void SetImpactBoundary(MeteorDefenseVR.Player.ShipCollisionBoundary boundary) => impactBoundary = boundary;
        [SerializeField] private Transform shipFrame;
        [SerializeField] private MeteorDefenseVR.Player.ShipCollisionBoundary sphericalBoundary;
        public void SetShipSpace(Transform frame, MeteorDefenseVR.Player.ShipCollisionBoundary sphere) { shipFrame = frame; sphericalBoundary = sphere; }
        public Vector3 ShipCenter => shipFrame != null ? shipFrame.position : movementTarget != null ? movementTarget.position : transform.position;
        public Vector3 ShipForward => shipFrame != null ? shipFrame.forward : spawnOrigin != null ? spawnOrigin.forward : transform.forward;
        public Quaternion ShipRotation => shipFrame != null ? shipFrame.rotation : Quaternion.LookRotation(ShipForward, Vector3.up);
        public Transform ShipFrame => shipFrame != null ? shipFrame : transform;
        public float ShipCollisionRadius => sphericalBoundary != null ? sphericalBoundary.ShipRadius : 4.2f;
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
        private float scheduleScale = 1f, speedScale = 1f, sizeScale = 1f;
        private float powerUpSpeedScale = 1f, powerUpIntervalScale = 1f;
        private bool poolsPrepared;
        private DifficultyBalance? difficulty;
        private StageBalance? stage;
        private bool stageSequenceActive;
        private bool stageSessionStarted;
        private bool resolvedRaised;
        private int experienceBudget;
        public bool ScenarioControlled { get; private set; }
        public void SetDifficulty(DifficultyBalance balance) => difficulty = balance;
        public void ClearDifficulty() => difficulty = null;
        public void SetStage(StageBalance balance) { stage = balance; stageSequenceActive = true; }
        public void ClearStage() { stage = null; stageSequenceActive = false; stageSessionStarted = false; }
        public void SetExperienceSession(int regularMeteorCount, float targetSeconds)
        {
            experienceBudget = Mathf.Clamp(regularMeteorCount, 1, 50);
            float authored = 0f; foreach (MeteorWaveData wave in waves) if (wave != null) authored += wave.EstimatedDuration;
            scheduleScale = authored > 0f ? Mathf.Clamp(targetSeconds, 30f, 300f) / authored : 1f;
        }
        public void ClearExperienceSession() { experienceBudget = 0; scheduleScale = 1f; }
        public void SetPrewarmCapacity(int count) => prewarmPerMeteorKind = Mathf.Clamp(count, prewarmPerMeteorKind, 5);
        private static readonly ProfilerMarker PrewarmMarker = new ProfilerMarker("MD.Prewarm.Meteor");

        public void SetOperationTuning(float mainSeconds, float speedMultiplier, float sizeMultiplier)
        {
            float duration = 0f;
            foreach (MeteorWaveData wave in waves) if (wave != null) duration += wave.EstimatedDuration;
            scheduleScale = duration > 0f ? Mathf.Clamp(mainSeconds, 40f, 60f) / duration : 1f;
            speedScale = Mathf.Clamp(speedMultiplier, .7f, 1.2f);
            sizeScale = Mathf.Clamp(sizeMultiplier, 1f, 1.2f);
        }

        public void SetPowerUpTimeScale(float asteroidSpeedMultiplier, float spawnIntervalMultiplier)
        {
            powerUpSpeedScale = RuntimeValueGuard.Clamp(asteroidSpeedMultiplier, .1f, 1f, 1f);
            powerUpIntervalScale = RuntimeValueGuard.Clamp(spawnIntervalMultiplier, 1f, 3f, 1f);
            foreach (MeteorController meteor in activeMeteors) meteor?.SetRuntimeSpeedMultiplier(powerUpSpeedScale);
        }

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
        public bool HasCampaignStage => stage.HasValue;
        public int CampaignStageNumber => stage?.Number ?? 0;

        public event Action<int, MeteorWaveData> WaveStarted;
        public event Action<int> SpawningStarted;
        public event Action<int, int> StageSpawningStarted;
        public event Action<MeteorController, int, int> MeteorSpawned;
        public event Action<MeteorController> MeteorCompleted;
        public event Action AllSpawnsCompleted;
        public event Action AllMeteorsResolved;

        private void Awake() => gameFlow = GameFlowManager.Instance;

        private void OnEnable() => BindGameFlow(GameFlowManager.Instance);

        private void Start()
        {
            if (!ScenarioControlled && autoStartFromGameFlow && gameFlow != null && gameFlow.CurrentState == GameState.Playing)
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
            poolsPrepared = false;
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

        public void BeginSpawning() => BeginSpawningInternal(true);
        public void BeginNextStage() => BeginSpawningInternal(false);

        private void BeginSpawningInternal(bool newSession)
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
                EstimatedGameDuration += wave.EstimatedDuration * scheduleScale * (difficulty?.Interval ?? 1f);
            }
            if (stage.HasValue) TotalScheduled = stage.Value.RegularMeteorCount;
            else if (experienceBudget > 0) TotalScheduled = experienceBudget;

            AllWavesSpawned = TotalScheduled == 0;
            IsSpawning = !AllWavesSpawned;
            resolvedRaised = false;
            if (newSession) SpawningStarted?.Invoke(TotalScheduled);
            else StageSpawningStarted?.Invoke(stage?.Number ?? 1, TotalScheduled);
            stageSessionStarted = true;
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
            if (ScenarioControlled) return;
            if (!IsSpawning) return;
            // Do not accumulate a burst of overdue spawns while the concurrent limit is full.
            int concurrentLimit = stage?.MaxConcurrent ?? difficulty?.MaxConcurrent ?? 0;
            if (concurrentLimit > 0 && activeMeteors.Count >= concurrentLimit) return;
            delayRemaining -= Mathf.Max(0f, deltaTime) / powerUpIntervalScale;
            if (delayRemaining > 0f) return;

            MeteorSpawnData entry = CurrentEntry();
            if (entry == null)
            {
                MoveToNextValidEntry(false);
                return;
            }

            if (!SpawnMeteor(entry)) { delayRemaining = .25f; return; }
            spawnedInEntry++;
            if (SpawnedCount >= TotalScheduled)
            {
                FinishScheduling();
                return;
            }
            if (spawnedInEntry < entry.Count)
            {
                delayRemaining = entry.SpawnInterval * scheduleScale * (difficulty?.Interval ?? 1f) / (stage?.SpawnRate ?? 1f);
                return;
            }

            entryIndex++;
            spawnedInEntry = 0;
            MoveToNextValidEntry(false);
        }

        public Vector3 CalculateSpawnPosition(float horizontalInput, float verticalInput, float distanceInput, Vector2 requestedArea)
        {
            Transform origin = shipFrame != null ? shipFrame : spawnOrigin != null ? spawnOrigin : transform;
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
                if (meteor != null) { controllerCache.Remove(meteor.gameObject); lockTargetCache.Remove(meteor.gameObject); }
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
                delayRemaining = (entry.Delay + (includeWaveDelay ? wave.WaveDelay : 0f)) * scheduleScale * (difficulty?.Interval ?? 1f) / (stage?.SpawnRate ?? 1f);
                return;
            }
            if ((stage.HasValue || experienceBudget > 0) && SpawnedCount < TotalScheduled && HasAnySpawnEntry())
            {
                waveIndex = 0; entryIndex = 0; spawnedInEntry = 0;
                MoveToNextValidEntry(true);
                return;
            }
            FinishScheduling();
        }

        private bool HasAnySpawnEntry()
        {
            if (waves == null) return false;
            foreach (MeteorWaveData wave in waves)
                if (wave != null && wave.Spawns != null)
                    foreach (MeteorSpawnData entry in wave.Spawns) if (entry != null && entry.Count > 0) return true;
            return false;
        }

        private void FinishScheduling()
        {
            if (AllWavesSpawned) return;
            IsSpawning = false;
            AllWavesSpawned = true;
            AllSpawnsCompleted?.Invoke();
            TryRaiseResolved();
        }

        private MeteorSpawnData CurrentEntry()
        {
            if (waveIndex < 0 || waveIndex >= waves.Length || waves[waveIndex] == null) return null;
            MeteorSpawnData[] entries = waves[waveIndex].Spawns;
            return entryIndex >= 0 && entryIndex < entries.Length ? entries[entryIndex] : null;
        }

        private bool SpawnMeteor(MeteorSpawnData data)
        {
            Vector2 area = data.SpawnArea;
            Vector3 position = CalculateSpawnPosition(
                SpawnCoordinate(UnityEngine.Random.Range(-1f, 1f)),
                SpawnCoordinate(UnityEngine.Random.Range(-1f, 1f)),
                UnityEngine.Random.value,
                area);

            SpawnDirectionMode directionMode = difficulty?.SpawnDirections ?? SpawnDirectionMode.ExperienceArc;
            bool surround = directionMode == SpawnDirectionMode.FullSphere;
            bool sideImpact = directionMode == SpawnDirectionMode.FrontAndSides;
            if (surround && !TrySurroundPosition(out position)) return false;
            if (sideImpact && !TryFrontSidesPosition(out position)) return false;
            if (directionMode == SpawnDirectionMode.FrontOnly)
                position = CalculateSpawnPosition(UnityEngine.Random.Range(-.3f, .3f), UnityEngine.Random.Range(-.35f, .35f), UnityEngine.Random.value, area);
            float distance = Vector3.Distance(position, ShipCenter);
            bool radial = surround || sideImpact;
            float visualScale = radial ? Mathf.Clamp(distance / 14f, 1f, 2.6f) : 1;
            float variation = stage.HasValue ? UnityEngine.Random.Range(1f - stage.Value.SizeVariation, 1f + stage.Value.SizeVariation) : 1f;
            float size = data.Size * sizeScale * (difficulty?.Size ?? 1f) * visualScale * variation;
            float speed = data.Speed * speedScale * (difficulty?.Speed ?? 1f) * (stage?.Speed ?? 1f) * visualScale;
            speed = Mathf.Min(speed, 16f);
            if (surround) speed = Mathf.Min(speed, Mathf.Max(.1f, (distance - (sphericalBoundary != null ? sphericalBoundary.ShipRadius : 4.2f) - size * 1.5f) / difficulty.Value.ReactionSeconds));

            FlushPendingReleases();
            GameObject meteorObject = AcquireMeteor(data);
            meteorObject.transform.SetParent(transform, true);
            meteorObject.transform.SetPositionAndRotation(position, Quaternion.identity);
            meteorObject.SetActive(true);

            MeteorController meteor = GetMeteorController(meteorObject);
            meteor.Configure(data.MeteorType, data.Health * (difficulty?.Health ?? 1f), speed,
                difficulty?.ScaleDamage(data.Damage) ?? data.Damage, stage?.ScaleScore(difficulty?.ScaleScore(data.Score) ?? data.Score) ?? (difficulty?.ScaleScore(data.Score) ?? data.Score),
                size);
            meteor.SetMovementTarget(radial && shipFrame != null ? shipFrame : movementTarget);
            meteor.SetImpactBoundary(radial ? sphericalBoundary : impactBoundary);
            meteor.SetRuntimeSpeedMultiplier(powerUpSpeedScale);
            GazeLockOnTarget lockTarget = GetLockTarget(meteorObject);
            lockTarget.Configure(meteor);
            if (difficulty.HasValue) lockTarget.ApplyDifficulty(difficulty.Value);
            meteor.Destroyed += HandleMeteorDestroyed;
            meteor.ReachedPlayerEvent += HandleMeteorReachedPlayer;
            activeMeteors.Add(meteor);
            meteor.Spawn();
            SpawnedCount++;
            MeteorSpawned?.Invoke(meteor, SpawnedCount, TotalScheduled);
            return true;
        }

        // Scenario scheduling reuses the same pool, collision, VFX, health and score events.
        // It never alters the authored Wave assets or the normal BeginSpawning path.
        public void BeginScenario(int expectedThreats = 0)
        {
            StopSpawning(true); PrewarmPools(); ScenarioControlled = true;
            SpawnedCount = CompletedCount = 0; TotalScheduled = Mathf.Max(0, expectedThreats); AllWavesSpawned = false;
            SpawningStarted?.Invoke(TotalScheduled);
        }
        public void EndScenario() { StopSpawning(true); ScenarioControlled = false; AllWavesSpawned = false; }
        public void ClearScenarioRound() { if (ScenarioControlled) { ClearTrackedMeteors(true); FlushPendingReleases(); } }
        public MeteorController SpawnScenario(Vector3 position, Vector3 velocity, float scale, int damage, float health = 1)
        {
            if (!ScenarioControlled || activeMeteors.Count >= 5) return null;
            MeteorSpawnData data = null;
            foreach (var wave in waves)
            {
                if (wave == null) continue;
                foreach (var entry in wave.Spawns) if (entry != null) { data = entry; break; }
                if (data != null) break;
            }
            if (data == null) return null;
            FlushPendingReleases(); var instance = AcquireMeteor(data);
            instance.transform.SetParent(transform, true); instance.transform.SetPositionAndRotation(position, Quaternion.identity); instance.SetActive(true);
            var meteor = GetMeteorController(instance);
            meteor.Configure(data.MeteorType, health, velocity.magnitude, damage, difficulty?.ScaleScore(data.Score) ?? data.Score, scale);
            meteor.SetMovementTarget(null); meteor.SetMovementDirection(velocity); meteor.SetImpactBoundary(sphericalBoundary != null ? sphericalBoundary : impactBoundary);
            meteor.SetRuntimeSpeedMultiplier(powerUpSpeedScale);
            var target = GetLockTarget(instance); target.Configure(meteor);
            if (difficulty.HasValue) target.ApplyDifficulty(difficulty.Value);
            target.ConfigureComfortTolerance(target.ColliderExpansion, 150);
            meteor.Destroyed += HandleMeteorDestroyed; meteor.ReachedPlayerEvent += HandleMeteorReachedPlayer;
            activeMeteors.Add(meteor); meteor.Spawn(); SpawnedCount++;
            TotalScheduled = Mathf.Max(TotalScheduled, SpawnedCount); MeteorSpawned?.Invoke(meteor, SpawnedCount, TotalScheduled); return meteor;
        }

        private bool TrySurroundPosition(out Vector3 position)
        {
            var balance = difficulty.Value;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                // Stratified directions ensure every session explores the whole canopy.
                Vector3 axis = (SpawnedCount % 6) switch { 0 => Vector3.forward, 1 => Vector3.left, 2 => Vector3.back, 3 => Vector3.right, 4 => Vector3.up, _ => Vector3.down };
                Vector3 direction = (axis + UnityEngine.Random.insideUnitSphere * .55f).normalized;
                // Leave the two pitch-limit caps clear; above/below remain targetable at <=70 degrees.
                direction.y = Mathf.Clamp(direction.y, -.90f, .90f);
                if (direction.x * direction.x + direction.z * direction.z < .19f)
                { var horizontal = new Vector2(direction.x, direction.z).normalized; if (horizontal == Vector2.zero) horizontal = Vector2.up; direction.x = horizontal.x * .44f; direction.z = horizontal.y * .44f; }
                direction.Normalize();
                position = ShipCenter + direction * UnityEngine.Random.Range(balance.SpawnMin, balance.SpawnMax);
                if (Physics.CheckSphere(position, 5f, ~0, QueryTriggerInteraction.Ignore)) continue;
                bool occupied = false;
                foreach (var other in activeMeteors)
                    if (other != null && (Vector3.Distance(position, other.transform.position) < 12 || Vector3.Angle(direction, other.transform.position - ShipCenter) < 22)) { occupied = true; break; }
                if (!occupied) return true;
            }
            position = default; return false;
        }

        private bool TryFrontSidesPosition(out Vector3 position)
        {
            Transform frame = shipFrame != null ? shipFrame : spawnOrigin != null ? spawnOrigin : transform;
            int sector = SpawnedCount % 3;
            float yaw = sector == 0 ? UnityEngine.Random.Range(-24f, 24f) : sector == 1 ? UnityEngine.Random.Range(-78f, -48f) : UnityEngine.Random.Range(48f, 78f);
            float pitch = UnityEngine.Random.Range(-28f, 34f);
            Vector3 direction = frame.TransformDirection(Quaternion.Euler(-pitch, yaw, 0) * Vector3.forward).normalized;
            float distance = UnityEngine.Random.Range(difficulty?.SpawnMin ?? 28f, difficulty?.SpawnMax ?? 42f);
            position = ShipCenter + direction * distance;
            return !Physics.CheckSphere(position, 3f, ~0, QueryTriggerInteraction.Ignore);
        }

        private float SpawnCoordinate(float value) => difficulty?.SpawnCoordinate(value) ?? value;

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
            TryRaiseResolved();
        }

        private void TryRaiseResolved()
        {
            if (resolvedRaised || !AllWavesSpawned || activeMeteors.Count > 0) return;
            resolvedRaised = true;
            AllMeteorsResolved?.Invoke();
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
            if (poolsPrepared) return;
            // Preserve the synchronous fallback for direct developer jumps and standalone tests.
            IEnumerator preparation = PrewarmIncrementally();
            while (preparation.MoveNext()) { }
        }

        public IEnumerator PrewarmIncrementally()
        {
            if (poolsPrepared || prewarmPerMeteorKind <= 0 || waves == null) yield break;
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
                        // A synchronous BeginSpawning may have completed preparation while we yielded.
                        if (poolsPrepared) yield break;
                        using (PrewarmMarker.Auto())
                        {
                            GameObject instance = CreateMeteorObject(data);
                            instance.SetActive(false);
                            pool.Push(instance);
                        }
                        yield return null;
                    }
                }
            }
            poolsPrepared = true;
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
            if (ScenarioControlled) return;
            if (!autoStartFromGameFlow) return;
            if (state == GameState.Playing)
            {
                if (stageSequenceActive && stageSessionStarted) BeginNextStage();
                else BeginSpawning();
            }
            else if (IsSpawning) StopSpawning(state == GameState.Reset || state == GameState.Result);
        }
    }
}
