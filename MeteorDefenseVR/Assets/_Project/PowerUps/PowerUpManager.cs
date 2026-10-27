using System;
using System.Collections.Generic;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.Progression;
using MeteorDefenseVR.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MeteorDefenseVR.PowerUps
{
    [DefaultExecutionOrder(-340), DisallowMultipleComponent]
    public sealed class PowerUpManager : MonoBehaviour
    {
        internal sealed class ActiveBuff
        {
            public PowerUpBalance Value;
            public float Remaining;
            public int ShieldHits = -1;
        }

        public readonly struct ActiveBuffView
        {
            public readonly PowerUpType Type; public readonly string Code, Icon; public readonly float Remaining; public readonly int ShieldHits;
            internal ActiveBuffView(ActiveBuff buff) { Type = buff.Value.Type; Code = buff.Value.ShortCode; Icon = buff.Value.Icon; Remaining = buff.Remaining; ShieldHits = buff.ShieldHits; }
        }

        [SerializeField] private PowerUpSettings settings;
        [SerializeField] private MeteorSpawner spawner;
        [SerializeField] private BossClimaxController boss;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private LaserWeapon weapon;
        [SerializeField] private GazeRaycaster gaze;
        [SerializeField] private MissionHudController hud;
        [SerializeField] private StageProgressionController progression;

        private readonly List<PowerUpItem> itemPool = new List<PowerUpItem>();
        private readonly List<ActiveBuff> buffs = new List<ActiveBuff>();
        private readonly List<ActiveBuffView> buffViews = new List<ActiveBuffView>();
        private readonly HashSet<MeteorController> tracked = new HashSet<MeteorController>();
        private readonly List<IBlinkSource> blinkSources = new List<IBlinkSource>();
        private GameFlowManager flow;
        private Transform driftTarget;
        private GameObject shieldVisual;
        private Renderer shieldRenderer;
        private GameObject empVisual;
        private AudioSource pickupAudio;
        private ParticleSystem pickupBurst, pickupRing;
        private readonly AudioClip[] pickupClips = new AudioClip[8];
        private Material powerUpCoreMaterial, powerUpHaloMaterial, shieldMaterial, empMaterial, pickupBurstMaterial, pickupRingMaterial;
        private Mesh powerUpCoreMesh;
        private MaterialPropertyBlock shieldProperties;
        private float empVisualRemaining, shieldHitPulse, shieldFadeRemaining;
        private int missesSinceDrop;
        private float blinkCooldown, sourceRefresh;
        private bool blinkArmed = true, initialized, timersPaused;
        private static readonly Vector3 ShieldBaseScale = new Vector3(7.2f, 4.8f, 6.4f);
        private static readonly int ShieldHitPulseId = Shader.PropertyToID("_HitPulse");
        private static readonly int ShieldHitPointId = Shader.PropertyToID("_HitPoint");

        public static PowerUpManager Instance { get; private set; }
        public IReadOnlyList<ActiveBuffView> ActiveBuffs => buffViews;
        public int MissesSinceDrop => missesSinceDrop;
        public int ActiveWorldItems { get { int count = 0; foreach (PowerUpItem item in itemPool) if (item != null && item.gameObject.activeSelf) count++; return count; } }
        public bool TimersPaused => timersPaused;
        public int ActiveBuffCount => buffs.Count;
        public int ActivePickupParticles => (pickupBurst != null ? pickupBurst.particleCount : 0) + (pickupRing != null ? pickupRing.particleCount : 0);
        public bool IsBuffActive(PowerUpType type) => HasBuff(type);
        public event Action<PowerUpType> PowerUpAcquired;
        public event Action BuffsChanged;
        public event Action ShieldBlocked;
        public event Action<int> OverdriveFired;
        public event Action<int> MultiLaserFired;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install() { SceneManager.sceneLoaded -= SceneLoaded; SceneManager.sceneLoaded += SceneLoaded; }
        private static void SceneLoaded(Scene scene, LoadSceneMode _)
        {
            if (scene.name != "MeteorDefense") return;
            MeteorSpawner source = UnityEngine.Object.FindAnyObjectByType<MeteorSpawner>(FindObjectsInactive.Include);
            if (source == null) return;
            PowerUpManager manager = source.GetComponent<PowerUpManager>();
            if (manager == null) manager = source.gameObject.AddComponent<PowerUpManager>();
            manager.Initialize();
        }

        private void Awake() { if (Instance == null || Instance == this) Instance = this; }
        private void Start() => Initialize();
        private void Update() => Tick(Time.unscaledDeltaTime);
        private void OnDestroy()
        {
            Unbind();
            SafeDestroy(shieldVisual);
            SafeDestroy(powerUpCoreMaterial); SafeDestroy(powerUpHaloMaterial); SafeDestroy(powerUpCoreMesh);
            SafeDestroy(shieldMaterial); SafeDestroy(empMaterial); SafeDestroy(pickupBurstMaterial); SafeDestroy(pickupRingMaterial);
            if (Instance == this) Instance = null;
        }

        public void Initialize()
        {
            if (initialized) return;
            if (settings == null) settings = Resources.Load<PowerUpSettings>("PowerUpSettings");
            if (spawner == null) spawner = FindAnyObjectByType<MeteorSpawner>(FindObjectsInactive.Include);
            if (boss == null) boss = FindAnyObjectByType<BossClimaxController>(FindObjectsInactive.Include);
            if (playerHealth == null) playerHealth = FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if (weapon == null) weapon = FindAnyObjectByType<LaserWeapon>(FindObjectsInactive.Include);
            if (gaze == null) gaze = FindAnyObjectByType<GazeRaycaster>(FindObjectsInactive.Include);
            if (hud == null) hud = FindAnyObjectByType<MissionHudController>(FindObjectsInactive.Include);
            if (progression == null) progression = FindAnyObjectByType<StageProgressionController>(FindObjectsInactive.Include);
            flow = GameFlowManager.Instance;
            Camera camera = Camera.main; driftTarget = camera != null ? camera.transform : spawner != null ? spawner.transform : transform;
            if (settings == null || spawner == null || playerHealth == null || weapon == null)
            { enabled = false; Debug.LogWarning("[PowerUp] Settings or gameplay connections missing; power-ups disabled safely."); return; }
            initialized = true; BuildPool(); BuildShield(); BuildPickupVfx(); BuildAudio(); BuildHud(); RefreshBlinkSources(); Bind(); ResetSystem();
        }

        public void Configure(PowerUpSettings source, MeteorSpawner meteorSpawner, BossClimaxController bossController,
            PlayerHealth health, LaserWeapon laserWeapon, GazeRaycaster raycaster, MissionHudController missionHud, StageProgressionController stage)
        {
            if (initialized) Unbind(); settings = source; spawner = meteorSpawner; boss = bossController; playerHealth = health;
            weapon = laserWeapon; gaze = raycaster; hud = missionHud; progression = stage; initialized = false; Initialize();
        }

        private void Bind()
        {
            spawner.MeteorSpawned -= TrackMeteor; spawner.MeteorSpawned += TrackMeteor;
            if (boss != null) { boss.BossSpawned -= TrackBoss; boss.BossSpawned += TrackBoss; boss.StageChanged -= BossStageChanged; boss.StageChanged += BossStageChanged; }
            if (progression != null) { progression.PhaseChanged -= StagePhaseChanged; progression.PhaseChanged += StagePhaseChanged; }
            weapon.Fired -= WeaponFired; weapon.Fired += WeaponFired;
            playerHealth.DamageInterceptor = TryBlockDamage;
            playerHealth.HealthReset -= ResetSystem; playerHealth.HealthReset += ResetSystem;
            if (flow != null) { flow.OnStateChanged -= StateChanged; flow.OnStateChanged += StateChanged; }
        }
        private void Unbind()
        {
            if (spawner != null) spawner.MeteorSpawned -= TrackMeteor;
            if (boss != null) { boss.BossSpawned -= TrackBoss; boss.StageChanged -= BossStageChanged; }
            if (progression != null) progression.PhaseChanged -= StagePhaseChanged;
            if (weapon != null) weapon.Fired -= WeaponFired;
            if (playerHealth != null) { if (playerHealth.DamageInterceptor == TryBlockDamage) playerHealth.DamageInterceptor = null; playerHealth.HealthReset -= ResetSystem; }
            if (flow != null) flow.OnStateChanged -= StateChanged;
            foreach (MeteorController meteor in tracked) if (meteor != null) meteor.Destroyed -= MeteorDestroyed;
            tracked.Clear();
        }

        private void TrackMeteor(MeteorController meteor, int _, int __) { Track(meteor); }
        private void TrackBoss(MeteorController meteor)
        {
            Track(meteor);
            ActiveBuff slow = GetBuff(PowerUpType.SlowTime);
            meteor?.SetRuntimeSpeedMultiplier(slow != null ? Mathf.Clamp(slow.Value.Power, .5f, .65f) : 1f);
        }
        private void Track(MeteorController meteor)
        {
            if (meteor == null || !tracked.Add(meteor)) return;
            meteor.Destroyed -= MeteorDestroyed; meteor.Destroyed += MeteorDestroyed;
        }
        private void MeteorDestroyed(MeteorController meteor)
        {
            if (meteor == null) return;
            meteor.Destroyed -= MeteorDestroyed; tracked.Remove(meteor);
            TryDrop(meteor, meteor.MeteorType == MeteorType.Boss);
        }

        public bool TryDrop(MeteorController meteor, bool guaranteedBoss = false, float? randomValue = null)
        {
            if (!initialized || meteor == null) return false;
            // A final boss callback can arrive after MissionComplete has already cleared the pool.
            if (flow != null && (flow.CurrentState == GameState.MissionComplete || flow.CurrentState == GameState.Result)) return false;
            if (ActiveWorldItems >= settings.maximumWorldItems)
            {
                if (guaranteedBoss && settings.bossGuaranteedDrop)
                {
                    foreach (PowerUpItem activeItem in itemPool) if (activeItem != null && activeItem.gameObject.activeSelf) { Release(activeItem); break; }
                }
                else { missesSinceDrop++; return false; }
            }
            int stage = progression != null && progression.IsCampaign ? progression.CurrentStage : 1;
            float chance = settings.regularDropChance;
            if (missesSinceDrop >= settings.pityStartsAfter) chance += (missesSinceDrop - settings.pityStartsAfter + 1) * settings.pityChancePerMiss;
            bool guaranteed = guaranteedBoss && settings.bossGuaranteedDrop || missesSinceDrop + 1 >= settings.pityGuaranteedAt;
            float roll = randomValue ?? UnityEngine.Random.value;
            if (!guaranteed && roll > Mathf.Clamp01(chance)) { missesSinceDrop++; return false; }
            PowerUpDefinition selected = SelectDefinition(stage, randomValue.HasValue ? Mathf.Repeat(randomValue.Value * 7.17f, 1f) : UnityEngine.Random.value);
            if (selected == null) { missesSinceDrop++; return false; }
            PowerUpItem item = BorrowItem();
            if (item == null) return false;
            item.Spawn(selected.Capture(), meteor.transform.position, driftTarget, settings.itemLifetime, settings.gazeCollectDuration, settings.driftSpeed);
            missesSinceDrop = 0; return true;
        }

        private PowerUpDefinition SelectDefinition(int stage, float roll)
        {
            float total = 0f;
            if (settings.definitions == null) return null;
            foreach (PowerUpDefinition definition in settings.definitions)
            {
                if (definition == null || definition.spawnStage > stage) continue;
                float weight = definition.dropWeight * definition.dropChance;
                if (definition.type == PowerUpType.Heal)
                {
                    float hp = playerHealth != null ? playerHealth.HealthNormalized : 1f;
                    weight *= hp <= .4f ? 2.25f : hp <= .7f ? 1.45f : 1f;
                }
                total += Mathf.Max(0f, weight);
            }
            if (total <= 0f) return null;
            float cursor = Mathf.Clamp01(roll) * total;
            foreach (PowerUpDefinition definition in settings.definitions)
            {
                if (definition == null || definition.spawnStage > stage) continue;
                float weight = definition.dropWeight * definition.dropChance;
                if (definition.type == PowerUpType.Heal)
                { float hp = playerHealth != null ? playerHealth.HealthNormalized : 1f; weight *= hp <= .4f ? 2.25f : hp <= .7f ? 1.45f : 1f; }
                cursor -= Mathf.Max(0f, weight); if (cursor <= 0f) return definition;
            }
            return settings.definitions[^1];
        }

        internal void Collect(PowerUpItem item)
        {
            if (item == null || !item.IsAvailable) return;
            PowerUpType type = item.Type; Vector3 position = item.transform.position; Color color = GetDefinition(type).Color;
            Release(item); PlayPickupVfx(position, color); PlayPickup(type); Activate(type); PowerUpAcquired?.Invoke(type);
        }
        internal void Release(PowerUpItem item) { if (item != null) item.Despawn(); }

        public bool Activate(PowerUpType type)
        {
            PowerUpDefinition source = settings.Find(type); if (source == null) return false;
            PowerUpBalance value = source.Capture();
            if (type == PowerUpType.Heal) { playerHealth.RepairNormalized(value.Power); hud?.ShowWarning("선체 회복!", 1.1f); return true; }
            if (type == PowerUpType.EMP) { FireEmp(value); return true; }
            ActiveBuff existing = buffs.Find(entry => entry.Value.Type == type);
            if (existing != null)
            {
                if (type == PowerUpType.Shield && value.StackRule == PowerUpStackRule.AddShieldHit && existing.ShieldHits >= 0) existing.ShieldHits++;
                else if (value.StackRule == PowerUpStackRule.ExtendDuration) existing.Remaining = Mathf.Min(value.MaximumDuration, existing.Remaining + Mathf.Max(1f, value.Duration * .5f));
                else existing.Remaining = value.Duration;
            }
            else
            {
                if (buffs.Count >= settings.maximumActiveBuffs) RemoveBuff(ShortestBuff());
                existing = new ActiveBuff { Value = value, Remaining = value.Duration };
                if (type == PowerUpType.Shield && settings.shieldMode == ShieldConsumptionMode.ImpactCount)
                { existing.ShieldHits = settings.shieldImpactCount; existing.Remaining = float.PositiveInfinity; }
                buffs.Add(existing);
            }
            if (type == PowerUpType.Overdrive) hud?.ShowWarning("과부하 레이저 강화!", 1.25f);
            else if (type == PowerUpType.Shield) hud?.ShowWarning("방어막 충전!", 1f);
            else if (type == PowerUpType.RapidFire) hud?.ShowWarning("속사 강화!", 1f);
            else if (type == PowerUpType.MultiLaser) hud?.ShowWarning("다중 레이저 강화!", 1f);
            else hud?.ShowWarning(PowerUpHudView.PlayerName(type) + " 강화!", 1f);
            RefreshEffects(); return true;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!initialized) return;
            float dt = Mathf.Max(0f, unscaledDeltaTime);
            sourceRefresh -= dt; if (blinkSources.Count == 0 && sourceRefresh <= 0f) { sourceRefresh = 2f; RefreshBlinkSources(); }
            if (shieldVisual != null && shieldVisual.activeSelf)
            {
                shieldHitPulse = Mathf.MoveTowards(shieldHitPulse, 0f, dt * 1.85f);
                shieldFadeRemaining = Mathf.Max(0f, shieldFadeRemaining - dt);
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 2.4f) * .006f + shieldHitPulse * .018f;
                shieldVisual.transform.localScale = ShieldBaseScale * pulse;
                if (shieldRenderer != null)
                {
                    shieldRenderer.GetPropertyBlock(shieldProperties);
                    shieldProperties.SetFloat(ShieldHitPulseId, shieldHitPulse);
                    shieldRenderer.SetPropertyBlock(shieldProperties);
                }
                if (!HasBuff(PowerUpType.Shield) && shieldFadeRemaining <= 0f) shieldVisual.SetActive(false);
            }
            if (empVisual != null && empVisual.activeSelf)
            {
                empVisualRemaining -= dt; float t = 1f - Mathf.Clamp01(empVisualRemaining / .65f);
                empVisual.transform.localScale = Vector3.one * Mathf.Lerp(.1f, 10f, t);
                if (empVisualRemaining <= 0f) empVisual.SetActive(false);
            }
            blinkCooldown = Mathf.Max(0f, blinkCooldown - dt);
            ProcessOverdriveInput();
            if (timersPaused || Time.timeScale <= 0f) return;
            bool changed = false;
            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                buffs[i].Remaining -= dt;
                if (buffs[i].Remaining <= 0f) { buffs.RemoveAt(i); changed = true; }
            }
            if (changed) RefreshEffects(); else if (buffs.Count > 0) RebuildViews();
        }

        private void ProcessOverdriveInput()
        {
            if (!HasBuff(PowerUpType.Overdrive) || blinkCooldown > 0f || timersPaused) return;
            bool reliable = false, closed = false;
            foreach (IBlinkSource source in blinkSources) if (source != null && source.HasReliableBlink) { reliable = true; closed |= source.AreEyesClosed; }
            bool mousePressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            if (BlinkTriggerPolicy.ShouldFire(ref blinkArmed, reliable, closed, mousePressed, blinkCooldown <= 0f))
            {
                blinkCooldown = settings.blinkLaserCooldown; FireOverdrive();
            }
        }

        private void FireOverdrive()
        {
            Ray ray = gaze != null && gaze.Provider != null ? gaze.Provider.GetGazeRay() : new Ray(driftTarget.position, driftTarget.forward);
            var targets = new List<MeteorController>(settings.overdriveMaximumTargets);
            foreach (MeteorController meteor in spawner.ActiveMeteors)
                if (meteor != null && meteor.IsTargetable && Vector3.Angle(ray.direction, meteor.transform.position - ray.origin) <= settings.overdriveConeDegrees) targets.Add(meteor);
            if (boss != null && boss.ActiveBoss != null && boss.ActiveBoss.IsTargetable && Vector3.Angle(ray.direction, boss.ActiveBoss.transform.position - ray.origin) <= settings.overdriveConeDegrees) targets.Add(boss.ActiveBoss);
            targets.Sort((a, b) => Vector3.Distance(ray.origin, a.transform.position).CompareTo(Vector3.Distance(ray.origin, b.transform.position)));
            int hit = weapon.TryPowerBeam(targets, 4f, 2.5f, settings.overdriveMaximumTargets);
            if (hit > 0) { hud?.ShowWarning("OVERDRIVE BEAM", .55f); OverdriveFired?.Invoke(hit); }
        }

        private void WeaponFired(GazeLockOnTarget _, MeteorController primary)
        {
            if (!HasBuff(PowerUpType.MultiLaser) || primary == null) return;
            Ray ray = gaze != null && gaze.Provider != null ? gaze.Provider.GetGazeRay() : new Ray(weapon.transform.position, primary.transform.position - weapon.transform.position);
            int remaining = settings.multiLaserAdditionalTargets;
            int fired = 0;
            // A lethal auxiliary hit removes its meteor immediately. Walk backwards so
            // removal cannot invalidate an enumerator or skip another eligible target.
            for (int i = spawner.ActiveMeteors.Count - 1; i >= 0; i--)
            {
                MeteorController candidate = spawner.ActiveMeteors[i];
                if (remaining <= 0) break;
                if (candidate == null || candidate == primary || !candidate.IsTargetable) continue;
                if (Vector3.Angle(ray.direction, candidate.transform.position - ray.origin) > settings.multiLaserConeDegrees) continue;
                if (weapon.TryAuxiliaryHit(candidate, weapon.EffectiveDamage)) { remaining--; fired++; }
            }
            if (fired > 0) MultiLaserFired?.Invoke(fired);
        }

        private void FireEmp(PowerUpBalance value)
        {
            MeteorController[] snapshot = new MeteorController[spawner.ActiveMeteors.Count];
            for (int i = 0; i < snapshot.Length; i++) snapshot[i] = spawner.ActiveMeteors[i];
            foreach (MeteorController meteor in snapshot) if (meteor != null && meteor.MeteorType != MeteorType.Boss) meteor.DestroyMeteor();
            if (boss != null && boss.ActiveBoss != null) boss.ActiveBoss.ReceiveDamage(boss.ActiveBoss.MaxHealth * Mathf.Clamp(settings.empBossHealthFraction, .1f, .5f));
            if (empVisual != null) { empVisual.transform.position = driftTarget.position + driftTarget.forward * 1.5f; empVisual.transform.localScale = Vector3.one * .1f; empVisualRemaining = .65f; empVisual.SetActive(true); }
            hud?.ShowWarning("전자기 충격파 발동!", 1.2f);
        }

        private bool TryBlockDamage(int _, Vector3 position)
        {
            ActiveBuff shield = buffs.Find(entry => entry.Value.Type == PowerUpType.Shield);
            if (shield == null) return false;
            shieldHitPulse = 1f; shieldFadeRemaining = .32f;
            if (shieldRenderer != null)
            {
                shieldRenderer.GetPropertyBlock(shieldProperties);
                shieldProperties.SetVector(ShieldHitPointId, position);
                shieldProperties.SetFloat(ShieldHitPulseId, 1f);
                shieldRenderer.SetPropertyBlock(shieldProperties);
            }
            if (settings.shieldMode == ShieldConsumptionMode.ImpactCount)
            {
                shield.ShieldHits--;
                if (shield.ShieldHits <= 0) RemoveBuff(shield); else RebuildViews();
            }
            hud?.ShowWarning("방어막으로 막았어요!", .75f); ShieldBlocked?.Invoke(); return true;
        }

        private void RefreshEffects()
        {
            float damage = HasBuff(PowerUpType.DamageBoost) ? GetBuff(PowerUpType.DamageBoost).Value.Power : 1f;
            float cooldown = HasBuff(PowerUpType.RapidFire) ? Mathf.Clamp(GetBuff(PowerUpType.RapidFire).Value.Power, .2f, 1f) : 1f;
            weapon.SetPowerUpModifiers(damage, cooldown);
            ActiveBuff slow = GetBuff(PowerUpType.SlowTime);
            float asteroidScale = slow != null ? Mathf.Clamp(slow.Value.Power, .5f, .65f) : 1f;
            spawner.SetPowerUpTimeScale(asteroidScale, slow != null ? 1.45f : 1f);
            boss?.ActiveBoss?.SetRuntimeSpeedMultiplier(asteroidScale);
            if (shieldVisual != null)
            {
                bool active=HasBuff(PowerUpType.Shield);
                if(active && !shieldVisual.activeSelf) shieldHitPulse=.24f;
                shieldVisual.SetActive(active || shieldFadeRemaining>0f);
            }
            RebuildViews(); BuffsChanged?.Invoke();
        }

        private ActiveBuff GetBuff(PowerUpType type) => buffs.Find(entry => entry.Value.Type == type);
        private bool HasBuff(PowerUpType type) => GetBuff(type) != null;
        private ActiveBuff ShortestBuff() { ActiveBuff result = null; foreach (ActiveBuff buff in buffs) if (result == null || buff.Remaining < result.Remaining) result = buff; return result; }
        private void RemoveBuff(ActiveBuff buff) { if (buff != null && buffs.Remove(buff)) RefreshEffects(); }
        private void RebuildViews()
        {
            buffViews.Clear(); foreach (ActiveBuff buff in buffs) buffViews.Add(new ActiveBuffView(buff)); BuffsChanged?.Invoke();
        }

        public PowerUpBalance GetDefinition(PowerUpType type) { PowerUpDefinition source = settings != null ? settings.Find(type) : null; return source != null ? source.Capture() : default; }
        public void ResetSystem()
        {
            if (!initialized) return;
            foreach (PowerUpItem item in itemPool) item?.Despawn();
            buffs.Clear(); missesSinceDrop = 0; blinkCooldown = 0f; blinkArmed = true; timersPaused = false;
            weapon.SetPowerUpModifiers(1f, 1f); spawner.SetPowerUpTimeScale(1f, 1f);
            if (shieldVisual != null) shieldVisual.SetActive(false); if (empVisual != null) empVisual.SetActive(false); empVisualRemaining = shieldHitPulse = shieldFadeRemaining = 0f; RebuildViews();
        }
        private void StateChanged(GameState state)
        {
            if (state == GameState.Boot || state == GameState.Reset) ResetSystem();
            if (state == GameState.MissionComplete || state == GameState.Result)
                foreach (PowerUpItem item in itemPool) item?.Despawn();
            timersPaused = state != GameState.Playing && state != GameState.BossMeteor;
        }
        private void StagePhaseChanged(StageTransitionPhase phase) => timersPaused = phase == StageTransitionPhase.ExplosionQuiet || phase == StageTransitionPhase.StageClear;
        private void BossStageChanged(BossClimaxStage stage, string _) => timersPaused = stage == BossClimaxStage.Warning || stage == BossClimaxStage.Narration;

        private void RefreshBlinkSources()
        {
            blinkSources.Clear();
            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (behaviour is IBlinkSource source) blinkSources.Add(source);
        }

        private PowerUpItem BorrowItem() { foreach (PowerUpItem item in itemPool) if (item != null && !item.gameObject.activeSelf) return item; return null; }
        private void BuildPool()
        {
            Transform root = new GameObject("PowerUpItemPool").transform; root.SetParent(transform, false);
            Shader coreShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            Shader haloShader = Shader.Find("MeteorDefense/Soft Energy") ?? coreShader;
            powerUpCoreMaterial = coreShader != null ? new Material(coreShader) { name = "PowerUp_Core_Runtime" } : null;
            powerUpHaloMaterial = haloShader != null ? new Material(haloShader) { name = "PowerUp_Halo_Runtime" } : null;
            if (powerUpCoreMaterial != null)
            {
                powerUpCoreMaterial.enableInstancing = true;
                if (powerUpCoreMaterial.HasProperty("_Surface")) powerUpCoreMaterial.SetFloat("_Surface", 1f);
            }
            if (powerUpHaloMaterial != null)
            {
                powerUpHaloMaterial.enableInstancing = true;
                if (powerUpHaloMaterial.HasProperty("_Ring")) powerUpHaloMaterial.SetFloat("_Ring", 1f);
            }
            powerUpCoreMesh = CreatePowerUpCoreMesh();
            Font runtimeFont = FindDynamicRuntimeFont();
            for (int i = 0; i < settings.maximumWorldItems; i++)
            {
                GameObject go = new GameObject($"PowerUpItem_{i:D2}"); go.transform.SetParent(root, false); go.transform.localScale = Vector3.one * .65f;

                Transform visualRoot = new GameObject("EnergyCorePivot").transform; visualRoot.SetParent(go.transform, false);
                GameObject core = new GameObject("FacetedEnergyCore"); core.transform.SetParent(visualRoot, false);
                core.transform.localScale = new Vector3(.82f, 1f, .82f);
                core.AddComponent<MeshFilter>().sharedMesh = powerUpCoreMesh;
                MeshRenderer coreRenderer = core.AddComponent<MeshRenderer>();
                if (powerUpCoreMaterial != null) coreRenderer.sharedMaterial = powerUpCoreMaterial;

                GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Quad); halo.name = "AcquisitionRing"; halo.transform.SetParent(go.transform, false);
                halo.transform.localScale = Vector3.one * 2.05f; SafeDestroy(halo.GetComponent<Collider>());
                MeshRenderer haloRenderer = halo.GetComponent<MeshRenderer>();
                if (haloRenderer != null && powerUpHaloMaterial != null) haloRenderer.sharedMaterial = powerUpHaloMaterial;

                GameObject labelObject = new GameObject("HologramLabel"); labelObject.transform.SetParent(go.transform, false); labelObject.transform.localPosition = new Vector3(0, 1.28f, 0);
                TextMesh text = labelObject.AddComponent<TextMesh>(); text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.characterSize = .17f; text.fontSize = 48; text.lineSpacing = .8f;
                if (runtimeFont != null) text.font = runtimeFont;
                PowerUpItem item = go.AddComponent<PowerUpItem>();
                item.Prepare(this, new Renderer[] { coreRenderer, haloRenderer }, text, visualRoot, halo.transform);
                itemPool.Add(item);
            }
        }

        private static Mesh CreatePowerUpCoreMesh()
        {
            var mesh = new Mesh { name = "PowerUp_FacetedCore_Runtime" };
            mesh.vertices = new[]
            {
                new Vector3(0f, .72f, 0f), new Vector3(0f, -.72f, 0f),
                new Vector3(.52f, 0f, 0f), new Vector3(0f, 0f, .52f),
                new Vector3(-.52f, 0f, 0f), new Vector3(0f, 0f, -.52f)
            };
            mesh.triangles = new[]
            {
                0,3,2, 0,4,3, 0,5,4, 0,2,5,
                1,2,3, 1,3,4, 1,4,5, 1,5,2
            };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.UploadMeshData(true);
            return mesh;
        }
        private void BuildShield()
        {
            shieldVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere); shieldVisual.name = "EnergyShield_Pooled";
            SafeDestroy(shieldVisual.GetComponent<Collider>()); shieldVisual.transform.SetParent(driftTarget, false); shieldVisual.transform.localPosition = new Vector3(0, 0, 1.4f); shieldVisual.transform.localScale = ShieldBaseScale;
            shieldRenderer = shieldVisual.GetComponent<Renderer>();
            Shader shieldShader = Resources.Load<Shader>("Shaders/EnergyShield") ?? Shader.Find("MeteorDefense/Energy Shield") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (shieldRenderer != null && shieldShader != null)
            {
                shieldMaterial = new Material(shieldShader) { name = "EnergyShield_Runtime" };
                shieldMaterial.enableInstancing = true;
                shieldMaterial.SetColor("_BaseColor", new Color(.035f, .58f, 1f, .72f));
                shieldRenderer.sharedMaterial = shieldMaterial; shieldProperties = new MaterialPropertyBlock();
            }
            shieldVisual.SetActive(false);
            empVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere); empVisual.name = "EMP_Shockwave_Pooled";
            SafeDestroy(empVisual.GetComponent<Collider>()); empVisual.transform.SetParent(transform, true);
            Renderer empRenderer = empVisual.GetComponent<Renderer>(); Shader empShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (empRenderer != null && empShader != null)
            {
                empMaterial = new Material(empShader) { name = "EMP_Shockwave_Runtime" };
                Color empColor = new Color(.85f,.95f,1f,.18f);
                empMaterial.color = empColor;
                if (empMaterial.HasProperty("_BaseColor")) empMaterial.SetColor("_BaseColor", empColor);
                if (empMaterial.HasProperty("_Surface"))
                {
                    // Runtime materials do not receive the URP inspector's surface setup.
                    // Surface alone leaves opaque blending and hides the combat scene.
                    empMaterial.SetFloat("_Surface", 1f);
                    empMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    empMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    empMaterial.SetFloat("_ZWrite", 0f);
                    empMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    empMaterial.SetOverrideTag("RenderType", "Transparent");
                    empMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
                empRenderer.sharedMaterial = empMaterial;
            }
            empVisual.SetActive(false);
        }
        private void BuildAudio()
        {
            pickupAudio = gameObject.GetComponent<AudioSource>();
            if (pickupAudio == null) pickupAudio = gameObject.AddComponent<AudioSource>();
            MeteorDefenseVR.Audio.AudioChannelFilter.Attach(pickupAudio, MeteorDefenseVR.Audio.UserAudioChannel.Effects);
            pickupAudio.playOnAwake = false; pickupAudio.spatialBlend = 0f; pickupAudio.volume = .22f;
            float[] frequencies = { 660f, 520f, 780f, 920f, 840f, 1080f, 610f, 1250f };
            for (int i = 0; i < pickupClips.Length; i++) pickupClips[i] = CreateTone("PowerUp_" + ((PowerUpType)i), frequencies[i], i == (int)PowerUpType.Overdrive ? .28f : .16f);
        }
        private void BuildPickupVfx()
        {
            Transform root = new GameObject("PowerUpPickupVfx_Pooled").transform; root.SetParent(transform, false);
            Shader shader = Shader.Find("MeteorDefense/Soft Energy") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader == null) return;
            pickupBurstMaterial = new Material(shader) { name = "PowerUpPickup_Burst_Runtime" };
            pickupRingMaterial = new Material(shader) { name = "PowerUpPickup_Ring_Runtime" };
            if (pickupBurstMaterial.HasProperty("_BaseColor")) pickupBurstMaterial.SetColor("_BaseColor", Color.white);
            if (pickupBurstMaterial.HasProperty("_Ring")) pickupBurstMaterial.SetFloat("_Ring", 0f);
            if (pickupRingMaterial.HasProperty("_BaseColor")) pickupRingMaterial.SetColor("_BaseColor", Color.white);
            if (pickupRingMaterial.HasProperty("_Ring")) pickupRingMaterial.SetFloat("_Ring", 1f);
            pickupBurst = CreatePickupSystem(root, "EnergySparkBurst", pickupBurstMaterial, 36, .42f, 3.1f, .11f, true, false);
            pickupRing = CreatePickupSystem(root, "AcquisitionCollapseRing", pickupRingMaterial, 4, .46f, 0f, .52f, false, true);
        }
        private static ParticleSystem CreatePickupSystem(Transform root, string name, Material material, int capacity,
            float lifetime, float speed, float size, bool useShape, bool expandingRing)
        {
            GameObject go = new GameObject(name); go.transform.SetParent(root, false);
            ParticleSystem system = go.AddComponent<ParticleSystem>();
            var main = system.main; main.loop = false; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true; main.maxParticles = capacity; main.startLifetime = lifetime; main.startSpeed = speed;
            main.startSize = size; main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var emission = system.emission; emission.rateOverTime = 0f; emission.rateOverDistance = 0f; emission.enabled = false;
            var shape = system.shape; shape.enabled = useShape;
            if (useShape) { shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .16f; shape.radiusThickness = 1f; }
            var fade = system.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .08f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var scale = system.sizeOverLifetime; scale.enabled = true;
            AnimationCurve curve = expandingRing
                ? new AnimationCurve(new Keyframe(0f, .35f), new Keyframe(.22f, 1.35f), new Keyframe(1f, 2.8f))
                : new AnimationCurve(new Keyframe(0f, .25f), new Keyframe(.12f, 1f), new Keyframe(1f, 0f));
            scale.size = new ParticleSystem.MinMaxCurve(1f, curve);
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
            renderer.renderMode = expandingRing ? ParticleSystemRenderMode.Billboard : ParticleSystemRenderMode.Stretch;
            renderer.alignment = ParticleSystemRenderSpace.View; renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            if (!expandingRing) { renderer.lengthScale = 1.65f; renderer.velocityScale = .06f; }
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return system;
        }
        private void PlayPickupVfx(Vector3 position, Color color)
        {
            Color bright = new Color(color.r * 2.1f, color.g * 2.1f, color.b * 2.1f, .9f);
            if (pickupBurst != null)
            {
                pickupBurst.transform.position = position; var main = pickupBurst.main; main.startColor = bright;
                if (!pickupBurst.isPlaying) pickupBurst.Play(false);
                var flash = new ParticleSystem.EmitParams { position = position, velocity = Vector3.zero, startColor = bright, startLifetime = .16f, startSize = .72f };
                pickupBurst.Emit(flash, 1); pickupBurst.Emit(16);
            }
            if (pickupRing != null)
            {
                pickupRing.transform.position = position; var main = pickupRing.main; main.startColor = bright;
                if (!pickupRing.isPlaying) pickupRing.Play(false);
                var outer = new ParticleSystem.EmitParams { startColor = bright, startLifetime = .46f, startSize = 1.2f };
                var inner = new ParticleSystem.EmitParams { startColor = new Color(bright.r, bright.g, bright.b, .58f), startLifetime = .32f, startSize = .65f };
                pickupRing.Emit(outer, 1); pickupRing.Emit(inner, 1);
            }
        }
        private static AudioClip CreateTone(string name, float frequency, float duration)
        {
            const int rate = 24000; int count = Mathf.CeilToInt(rate * duration); float[] samples = new float[count];
            for (int i = 0; i < count; i++) { float t = (float)i / rate; float envelope = Mathf.Sin(Mathf.PI * i / Mathf.Max(1, count - 1)); samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * .24f; }
            AudioClip clip = AudioClip.Create(name, count, 1, rate, false); clip.SetData(samples, 0); return clip;
        }
        private static void SafeDestroy(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
        private void PlayPickup(PowerUpType type)
        {
            if (pickupAudio == null) return; PowerUpDefinition definition = settings.Find(type);
            AudioClip clip = definition != null && definition.pickupSfx != null ? definition.pickupSfx : pickupClips[(int)type];
            if (clip != null) FindAnyObjectByType<MeteorDefenseVR.Audio.AudioManager>()?.TryPlayEffect(pickupAudio,clip,MeteorDefenseVR.Audio.AudioCue.CalibrationComplete,type == PowerUpType.Overdrive ? 1f : .72f);
        }
        private void BuildHud()
        {
            MissionHudView missionView = FindAnyObjectByType<MissionHudView>(FindObjectsInactive.Include); if (missionView == null) return;
            Transform root = missionView.transform.Find("PowerUpHUD"); if (root == null) { root = new GameObject("PowerUpHUD").transform; root.SetParent(missionView.transform, false); }
            TextMesh buffsText = CreateText(root, "ActiveBuffs", new Vector3(-3.25f, .8f, 4f), TextAnchor.MiddleLeft, .028f);
            TextMesh acquired = CreateText(root, "Acquired", new Vector3(0, .7f, 3.98f), TextAnchor.MiddleCenter, .04f);
            PowerUpHudView view = root.GetComponent<PowerUpHudView>();
            if (view == null) view = root.gameObject.AddComponent<PowerUpHudView>();
            view.Configure(this, buffsText, acquired);
        }
        private static TextMesh CreateText(Transform root, string name, Vector3 position, TextAnchor anchor, float size)
        {
            Transform child = root.Find(name); if (child == null) { child = new GameObject(name).transform; child.SetParent(root, false); }
            child.localPosition = position; TextMesh text = child.GetComponent<TextMesh>();
            if (text == null) text = child.gameObject.AddComponent<TextMesh>();
            Font runtimeFont = FindDynamicRuntimeFont(); if (runtimeFont != null) text.font = runtimeFont;
            text.anchor = anchor; text.alignment = anchor == TextAnchor.MiddleLeft ? TextAlignment.Left : TextAlignment.Center; text.characterSize = size; text.fontSize = 0; text.color = new Color(.2f, .92f, 1f); return text;
        }
        private static Font FindDynamicRuntimeFont()
        {
            foreach (TextMesh candidate in FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (candidate != null && candidate.font != null && candidate.font.dynamic) return candidate.font;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
