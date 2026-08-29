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
        private GameObject empVisual;
        private AudioSource pickupAudio;
        private readonly AudioClip[] pickupClips = new AudioClip[8];
        private float empVisualRemaining;
        private int missesSinceDrop;
        private float blinkCooldown, sourceRefresh;
        private bool blinkArmed = true, initialized, timersPaused;

        public static PowerUpManager Instance { get; private set; }
        public IReadOnlyList<ActiveBuffView> ActiveBuffs => buffViews;
        public int MissesSinceDrop => missesSinceDrop;
        public int ActiveWorldItems { get { int count = 0; foreach (PowerUpItem item in itemPool) if (item != null && item.gameObject.activeSelf) count++; return count; } }
        public bool TimersPaused => timersPaused;
        public int ActiveBuffCount => buffs.Count;
        public bool IsBuffActive(PowerUpType type) => HasBuff(type);
        public event Action<PowerUpType> PowerUpAcquired;
        public event Action BuffsChanged;
        public event Action ShieldBlocked;
        public event Action<int> OverdriveFired;

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
        private void OnDestroy() { Unbind(); if (Instance == this) Instance = null; }

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
            initialized = true; BuildPool(); BuildShield(); BuildAudio(); BuildHud(); RefreshBlinkSources(); Bind(); ResetSystem();
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
            PowerUpType type = item.Type; Release(item); PlayPickup(type); Activate(type); PowerUpAcquired?.Invoke(type);
        }
        internal void Release(PowerUpItem item) { if (item != null) item.Despawn(); }

        public bool Activate(PowerUpType type)
        {
            PowerUpDefinition source = settings.Find(type); if (source == null) return false;
            PowerUpBalance value = source.Capture();
            if (type == PowerUpType.Heal) { playerHealth.RepairNormalized(value.Power); hud?.ShowWarning("HULL REPAIRED", 1.1f); return true; }
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
            if (type == PowerUpType.Overdrive) hud?.ShowWarning("OVERDRIVE ACTIVATED", 1.25f);
            else if (type == PowerUpType.Shield) hud?.ShowWarning("SHIELD RESTORED", 1f);
            else hud?.ShowWarning("WEAPON SYSTEM ENHANCED", 1f);
            RefreshEffects(); return true;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!initialized) return;
            float dt = Mathf.Max(0f, unscaledDeltaTime);
            sourceRefresh -= dt; if (blinkSources.Count == 0 && sourceRefresh <= 0f) { sourceRefresh = 2f; RefreshBlinkSources(); }
            if (shieldVisual != null && shieldVisual.activeSelf)
            {
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 6f) * .025f;
                shieldVisual.transform.localScale = new Vector3(4.8f, 3.1f, .12f) * pulse;
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
            foreach (MeteorController candidate in spawner.ActiveMeteors)
            {
                if (remaining <= 0) break;
                if (candidate == null || candidate == primary || !candidate.IsTargetable) continue;
                if (Vector3.Angle(ray.direction, candidate.transform.position - ray.origin) > settings.multiLaserConeDegrees) continue;
                if (weapon.TryAuxiliaryHit(candidate, weapon.EffectiveDamage)) remaining--;
            }
        }

        private void FireEmp(PowerUpBalance value)
        {
            MeteorController[] snapshot = new MeteorController[spawner.ActiveMeteors.Count];
            for (int i = 0; i < snapshot.Length; i++) snapshot[i] = spawner.ActiveMeteors[i];
            foreach (MeteorController meteor in snapshot) if (meteor != null && meteor.MeteorType != MeteorType.Boss) meteor.DestroyMeteor();
            if (boss != null && boss.ActiveBoss != null) boss.ActiveBoss.ReceiveDamage(boss.ActiveBoss.MaxHealth * Mathf.Clamp(settings.empBossHealthFraction, .1f, .5f));
            if (empVisual != null) { empVisual.transform.position = driftTarget.position + driftTarget.forward * 1.5f; empVisual.transform.localScale = Vector3.one * .1f; empVisualRemaining = .65f; empVisual.SetActive(true); }
            hud?.ShowWarning("EMP SHOCKWAVE", 1.2f);
        }

        private bool TryBlockDamage(int _, Vector3 __)
        {
            ActiveBuff shield = buffs.Find(entry => entry.Value.Type == PowerUpType.Shield);
            if (shield == null) return false;
            if (settings.shieldMode == ShieldConsumptionMode.ImpactCount)
            {
                shield.ShieldHits--;
                if (shield.ShieldHits <= 0) RemoveBuff(shield); else RebuildViews();
            }
            hud?.ShowWarning("SHIELD HIT", .75f); ShieldBlocked?.Invoke(); return true;
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
            if (shieldVisual != null) shieldVisual.SetActive(HasBuff(PowerUpType.Shield));
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
            if (shieldVisual != null) shieldVisual.SetActive(false); if (empVisual != null) empVisual.SetActive(false); empVisualRemaining = 0f; RebuildViews();
        }
        private void StateChanged(GameState state)
        {
            if (state == GameState.Boot || state == GameState.Reset) ResetSystem();
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
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            Material shared = shader != null ? new Material(shader) { name = "PowerUp_Hologram_Runtime" } : null;
            if (shared != null) { shared.enableInstancing = true; if (shared.HasProperty("_Surface")) shared.SetFloat("_Surface", 1f); }
            TextMesh fontTemplate = FindAnyObjectByType<TextMesh>(FindObjectsInactive.Include);
            for (int i = 0; i < settings.maximumWorldItems; i++)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = $"PowerUpItem_{i:D2}"; go.transform.SetParent(root, false); go.transform.localScale = Vector3.one * .65f;
                Renderer renderer = go.GetComponent<Renderer>(); if (renderer != null && shared != null) renderer.sharedMaterial = shared;
                GameObject labelObject = new GameObject("HologramLabel"); labelObject.transform.SetParent(go.transform, false); labelObject.transform.localPosition = new Vector3(0, 1.2f, 0);
                TextMesh text = labelObject.AddComponent<TextMesh>(); text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.characterSize = .12f; text.fontSize = 42;
                if (fontTemplate != null && fontTemplate.font != null) text.font = fontTemplate.font;
                PowerUpItem item = go.AddComponent<PowerUpItem>(); item.Prepare(this, new[] { renderer }, text); itemPool.Add(item);
            }
        }
        private void BuildShield()
        {
            shieldVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere); shieldVisual.name = "EnergyShield_Pooled";
            SafeDestroy(shieldVisual.GetComponent<Collider>()); shieldVisual.transform.SetParent(driftTarget, false); shieldVisual.transform.localPosition = new Vector3(0, 0, 2.7f); shieldVisual.transform.localScale = new Vector3(4.8f, 3.1f, .12f);
            Renderer renderer = shieldVisual.GetComponent<Renderer>(); Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (renderer != null && shader != null) { var material = new Material(shader) { name = "EnergyShield_Runtime" }; Color color = new Color(.05f, .65f, 1f, .18f); material.color = color; if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color); if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f); renderer.sharedMaterial = material; }
            shieldVisual.SetActive(false);
            empVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere); empVisual.name = "EMP_Shockwave_Pooled";
            SafeDestroy(empVisual.GetComponent<Collider>()); empVisual.transform.SetParent(transform, true);
            Renderer empRenderer = empVisual.GetComponent<Renderer>();
            if (empRenderer != null && shader != null) { var empMaterial = new Material(shader) { name = "EMP_Shockwave_Runtime" }; Color empColor = new Color(.85f,.95f,1f,.18f); empMaterial.color = empColor; if (empMaterial.HasProperty("_BaseColor")) empMaterial.SetColor("_BaseColor", empColor); if (empMaterial.HasProperty("_Surface")) empMaterial.SetFloat("_Surface",1f); empRenderer.sharedMaterial = empMaterial; }
            empVisual.SetActive(false);
        }
        private void BuildAudio()
        {
            pickupAudio = gameObject.GetComponent<AudioSource>();
            if (pickupAudio == null) pickupAudio = gameObject.AddComponent<AudioSource>();
            pickupAudio.playOnAwake = false; pickupAudio.spatialBlend = 0f; pickupAudio.volume = .22f;
            float[] frequencies = { 660f, 520f, 780f, 920f, 840f, 1080f, 610f, 1250f };
            for (int i = 0; i < pickupClips.Length; i++) pickupClips[i] = CreateTone("PowerUp_" + ((PowerUpType)i), frequencies[i], i == (int)PowerUpType.Overdrive ? .28f : .16f);
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
            if (clip != null) pickupAudio.PlayOneShot(clip, type == PowerUpType.Overdrive ? 1f : .72f);
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
            TextMesh template = root.parent != null ? root.parent.GetComponentInChildren<TextMesh>(true) : null;
            if (template != null && template != text && template.font != null) text.font = template.font;
            text.anchor = anchor; text.alignment = anchor == TextAnchor.MiddleLeft ? TextAlignment.Left : TextAlignment.Center; text.characterSize = size; text.fontSize = 0; text.color = new Color(.2f, .92f, 1f); return text;
        }
    }
}
