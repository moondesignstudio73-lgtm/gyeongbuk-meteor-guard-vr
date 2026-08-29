using System;
using System.Collections.Generic;
using System.IO;
using MeteorDefenseVR.Audio;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MeteorDefenseVR.Education
{
    public enum EducationPhase { Idle, Registration, Preparing, Search, Defend, Demonstration, CauseQuestion, ResponseQuestion, TriageQuestion, Feedback, Report, Operator }
    [DefaultExecutionOrder(600), DisallowMultipleComponent]
    public sealed partial class EducationSessionManager : MonoBehaviour
    {
        [SerializeField] private EducationSettings settings;
        private GameFlowManager flow;
        private ReadyScreenController ready;
        private OperationsResetController reset;
        private ResultController resultController;
        private HangarLobbyController lobby;
        private DifficultyManager difficulty;
        private GazeInputRouter router;
        private GazeRaycaster gaze;
        private LaserWeapon weapon;
        private PlayerHealth health;
        private GameSessionStats stats;
        private MeteorSpawner spawner;
        private ThreatIndicatorView warning;
        private AudioManager audio;
        private ExperienceConfiguration experience;
        private EducationView view;
        private EducationSessionLog log;
        private readonly Dictionary<MeteorController, ThreatLearningRecord> tracked = new Dictionary<MeteorController, ThreatLearningRecord>();
        private readonly HashSet<int> wrongThisRound = new HashSet<int>();
        private bool leased, previousAutofire, previousResultEnabled, previousLobbyEnabled, previousWarningSuppressed, finishing, initialized;
        private GameOverPolicy previousHealthPolicy;
        private float previousRayRange, stageStarted, phaseStarted, deadline, dwell;
        private MeteorController focused;
        private ThreatLearningRecord objective;
        private EducationQuestionRecord priorityQuestion;
        private string message = "", explanation = "";
        private int round;
        private bool stageRunning;
        public EducationSettings Settings => settings;
        public EducationPhase Phase { get; private set; }
        public EducationStage Stage { get; private set; }
        public float Elapsed { get; private set; }
        public int Round => round;
        public float PhaseRemaining => Mathf.Max(0, deadline - Elapsed);
        public float StageRemaining => Mathf.Max(0, settings.Duration(Stage) - (Elapsed - stageStarted));
        public EducationSessionResult Session => log?.Result;
        public EducationResultStore Store { get; private set; }
        public IReadOnlyDictionary<MeteorController, ThreatLearningRecord> Tracked => tracked;
        public bool IsTraining => leased;
        public bool OperatorAllowed => experience != null && experience.ShowOperator;
        public string Message => message;
        public string Explanation => explanation;
        public string StorageDirectory => Path.Combine(Application.persistentDataPath, "Education");
        public event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install() { SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded; }
        private static void Loaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var start = root.GetComponentInChildren<ReadyScreenController>(true);
                if (start == null) continue;
                if (start.GetComponent<EducationSessionManager>() == null) start.gameObject.AddComponent<EducationSessionManager>();
                return;
            }
        }
        private void Start()
        {
            settings = settings != null ? settings : Resources.Load<EducationSettings>("EducationSettings");
            if (settings == null || !settings.enabledForExperience) { enabled = false; return; }
            flow = GameFlowManager.Instance; ready = GetComponent<ReadyScreenController>(); reset = FindAnyObjectByType<OperationsResetController>();
            resultController = FindAnyObjectByType<ResultController>(); lobby = GetComponent<HangarLobbyController>(); difficulty = GetComponent<DifficultyManager>();
            router = FindAnyObjectByType<GazeInputRouter>(); gaze = FindAnyObjectByType<GazeRaycaster>(); weapon = FindAnyObjectByType<LaserWeapon>();
            health = FindAnyObjectByType<PlayerHealth>(); spawner = FindAnyObjectByType<MeteorSpawner>(); stats = FindAnyObjectByType<GameSessionStats>();
            warning = FindAnyObjectByType<ThreatIndicatorView>(); audio = FindAnyObjectByType<AudioManager>(); experience = FindAnyObjectByType<ExperienceConfiguration>();
            if (flow == null || ready == null || reset == null || router == null || weapon == null || spawner == null || Camera.main == null) { enabled = false; return; }
            Store = new EducationResultStore(Application.isBatchMode ? null : Path.Combine(StorageDirectory, EducationResultStore.FileName), settings.maximumStoredSessions, settings.retentionDays);
            Store.Load(DateTimeOffset.UtcNow);
            view = gameObject.AddComponent<EducationView>(); view.Build(this, router, Camera.main, settings);
            initialized = true; Bind(); Changed?.Invoke();
        }
        public void Configure(EducationSettings configuration) { if (!leased && configuration != null) settings = configuration; }
        private void Bind()
        {
            flow.OnStateChanged -= FlowChanged; flow.OnStateChanged += FlowChanged;
            weapon.Fired -= Fired; weapon.Fired += Fired;
            reset.BeforeReset -= SaveInterruptedSession; reset.BeforeReset += SaveInterruptedSession;
        }
        private void OnEnable() { if (initialized) Bind(); }
        private void OnDisable()
        {
            if (!initialized) return;
            if (flow != null) flow.OnStateChanged -= FlowChanged;
            if (weapon != null) weapon.Fired -= Fired;
            if (reset != null) reset.BeforeReset -= SaveInterruptedSession;
            if (leased && log != null && !log.Closed) { log.Close(false, Elapsed, stats != null ? stats.Score : 0, DateTimeOffset.UtcNow); Store.Add(log.Result, DateTimeOffset.UtcNow); }
            Release(); SetModal(false); if (view != null) view.Hide();
            if(Store!=null&&Store.PendingWrite)Store.RetrySave();
        }
        private void OnDestroy() { if (view != null) Destroy(view); }
        public void OpenRegistration()
        {
            if (leased || flow.CurrentState != GameState.Boot) return;
            Phase = EducationPhase.Registration; SetModal(true); Changed?.Invoke();
        }
        public void OpenOperator()
        {
            if (!OperatorAllowed || leased || flow.CurrentState != GameState.Boot) return;
            Phase = EducationPhase.Operator; SetModal(true); Changed?.Invoke();
        }
        public void CloseMenu()
        {
            if (Phase != EducationPhase.Registration && Phase != EducationPhase.Operator) return;
            Phase = EducationPhase.Idle; SetModal(false); Changed?.Invoke();
        }
        public bool StartTraining(ParticipantInfo participant, DifficultyLevel selected, bool skipTutorial)
        {
            participant = participant?.Sanitized();
            if (leased || flow.CurrentState != GameState.Boot || participant == null || !participant.Valid) return false;
            difficulty.Select(selected);
            log = new EducationSessionLog(EducationSessionResult.Create(participant, difficulty.Selected, DateTimeOffset.UtcNow));
            log.Result.Weights = settings.weights.Copy(); Elapsed = 0; round = 0; stageRunning = false; Stage = EducationStage.Orientation;
            Lease(); Phase = EducationPhase.Preparing; SetModal(false); Changed?.Invoke();
            // Skip only the interaction tutorial; an uncalibrated webcam still uses the established calibration flow.
            if (skipTutorial && (router.ActiveMode != PcGazeMode.WebcamGaze || router.Webcam.Calibration.IsCalibrated)) flow.StartLaunch();
            else ready.StartExperience();
            return true;
        }
        private void Lease()
        {
            leased = true; previousAutofire = weapon.AutoFireOnLock; previousRayRange = gaze.MaximumDistance; gaze.MaximumDistance = 150;
            previousResultEnabled = resultController != null && resultController.enabled;
            previousLobbyEnabled = lobby != null && lobby.enabled;
            if (resultController != null) { resultController.HideResult(); resultController.enabled = false; }
            if (lobby != null) lobby.enabled = false;
            previousHealthPolicy = health.Policy; health.SetPolicy(GameOverPolicy.NoFailExperienceMode);
            if (warning != null) { previousWarningSuppressed = warning.VisualsSuppressed; warning.VisualsSuppressed = true; }
            spawner.BeginScenario();
        }
        private void Release()
        {
            if (!leased) return;
            ClearRound(false); ResetSpecial(); spawner.EndScenario(); weapon.AutoFireOnLock = previousAutofire; weapon.ResetWeapon(); gaze.MaximumDistance = previousRayRange;
            health.SetPolicy(previousHealthPolicy);
            if (warning != null) warning.VisualsSuppressed = previousWarningSuppressed;
            // Restore the old result/lobby only after Reset returned the flow to Boot.
            if (resultController != null) resultController.enabled = previousResultEnabled;
            if (lobby != null) lobby.enabled = previousLobbyEnabled;
            leased = false; stageRunning = false;
        }
        private void FlowChanged(GameState state)
        {
            if (state == GameState.Reset || state == GameState.Boot)
            {
                if (leased && log != null && !log.Closed) { log.Close(false, Elapsed, stats.Score, DateTimeOffset.UtcNow); Store.Add(log.Result, DateTimeOffset.UtcNow); }
                Release(); Phase = EducationPhase.Idle; SetModal(false); Changed?.Invoke(); return;
            }
            if (!leased) { if (Phase == EducationPhase.Registration || Phase == EducationPhase.Operator) CloseMenu(); return; }
            if (state == GameState.Playing && !stageRunning) BeginStage(EducationStage.Orientation);
            if (state == GameState.Result && !finishing && Phase != EducationPhase.Report) Finish(false);
        }
        private void SaveInterruptedSession()
        {
            if (!leased || log == null || log.Closed) return;
            log.Close(false, Elapsed, stats.Score, DateTimeOffset.UtcNow); Store.Add(log.Result, DateTimeOffset.UtcNow);
        }
        private void Update()
        {
            if (!initialized || !leased) return;
            if (Phase == EducationPhase.Report) { reset.CancelResultTimer(); return; }
            if (Time.timeScale <= 0 || !stageRunning) return;
            Tick(Time.deltaTime);
        }
        public void Tick(float dt)
        {
            if (!leased || !stageRunning || Phase == EducationPhase.Report || !RuntimeValueGuard.IsFinite(dt) || dt <= 0) return;
            Elapsed += dt;
            if (Phase == EducationPhase.Search || Phase == EducationPhase.Defend) TrackDetection(dt);
            TickSpecial(dt);
            if (Phase == EducationPhase.Feedback && Elapsed >= deadline)
            {
                if (Elapsed - stageStarted >= settings.Duration(Stage))
                {
                    log.Result.CompletedStages = Mathf.Max(log.Result.CompletedStages, (int)Stage + 1); log.Event(Elapsed, Stage, EducationEventKind.StageFinished);
                    if (Stage == EducationStage.Integrated) { Finish(true); return; }
                    BeginStage(Stage + 1);
                }
                else BeginRound();
            }
            else if ((Phase == EducationPhase.Search || Phase == EducationPhase.Defend) && Elapsed >= deadline)
            {
                if (priorityQuestion != null && priorityQuestion.Attempts == 0) log.Answer(priorityQuestion, -1, MostUrgentId(out var ttc), Elapsed, ttc);
                Feedback("시간이 끝났어요. 방향과 접근 속도를 다시 살펴보세요.");
            }
            else if (Phase == EducationPhase.Defend && spawner.ActiveCount == 0) Feedback("방어 결과를 확인하세요. 거리뿐 아니라 속도와 방향도 중요합니다.");
        }
        private void BeginStage(EducationStage stage)
        {
            Stage = stage; stageStarted = Elapsed; stageRunning = true; log.Event(Elapsed, Stage, EducationEventKind.StageStarted); audio?.TryPlay(AudioCue.CalibrationPoint); BeginRound();
        }
        private void BeginRound()
        {
            ClearRound(true); ResetSpecial(); round++; focused = null; dwell = 0; objective = null; priorityQuestion = null; explanation = "";
            // Leave headroom for a complete round rather than silently dropping telemetry at its bounded capacity.
            if (log.Result.Threats.Count > EducationSessionLog.MaximumThreats - 5 || log.Result.Questions.Count > EducationSessionLog.MaximumQuestions - 4) { Finish(false); return; }
            weapon.AutoFireOnLock = Stage == EducationStage.Prediction;
            SetModal(false); phaseStarted = Elapsed;
            if (Stage == EducationStage.Orientation)
            {
                Phase = EducationPhase.Search; bool nearest = round % 7 == 0;
                var direction = (ThreatDirection)((round - 1) % 6);
                message = nearest ? "가장 가까운 운석을 찾아보세요." : "기체 기준 " + EducationLabels.Direction(direction) + " 물체를 찾아보세요.";
                var target = Spawn(direction, nearest ? 24 : 35, 0, true, false, false);
                objective = target != null ? tracked[target] : null;
                Spawn((ThreatDirection)(((int)direction + 2) % 6), nearest ? 45 : 32, 0, false, false, false);
                deadline = Elapsed + settings.observationDeadline * AssistanceTime();
            }
            else if (Stage == EducationStage.Prediction)
            {
                Phase = EducationPhase.Defend; message = "거리·속도·방향을 보고 우선 방어할 운석을 선택하세요.";
                float multiplier = SpeedMultiplier();
                var dir = (ThreatDirection)(round % 6); Spawn(dir, 50, .8f * multiplier, true, true, false);
                Spawn((ThreatDirection)(((int)dir + 1) % 6), 110, 6 * multiplier, true, true, false);
                if (difficulty.Selected >= DifficultyLevel.Hard) Spawn((ThreatDirection)(((int)dir + 3) % 6), 75, 2.4f * multiplier, true, true, false);
                priorityQuestion = log.Question(Stage, round, EducationQuestionKind.Priority, "distance-speed-priority", Elapsed);
                deadline = Elapsed + 75 * AssistanceTime();
            }
            else BeginSpecialRound();
            Changed?.Invoke();
        }
        private float AssistanceTime() => difficulty.Selected == DifficultyLevel.Easy ? 1.35f : difficulty.Selected == DifficultyLevel.Normal ? 1 : difficulty.Selected == DifficultyLevel.Hard ? .85f : .75f;
        private float SpeedMultiplier() => difficulty.Selected == DifficultyLevel.Easy ? .8f : difficulty.Selected == DifficultyLevel.Normal ? 1 : difficulty.Selected == DifficultyLevel.Hard ? 1.2f : 1.35f;
        private static Vector3 DirectionVector(ThreatDirection direction) => direction switch { ThreatDirection.Front => Vector3.forward, ThreatDirection.Left => Vector3.left,
            ThreatDirection.Right => Vector3.right, ThreatDirection.Above => new Vector3(0,.85f,.52f).normalized, ThreatDirection.Below => new Vector3(0,-.85f,.52f).normalized, _ => Vector3.back };
        private MeteorController Spawn(ThreatDirection direction, float distance, float speed, bool evaluated, bool defense, bool demo, int damage = 14)
        {
            Vector3 axis = Quaternion.LookRotation(spawner.ShipForward) * DirectionVector(direction);
            Vector3 position = spawner.ShipCenter + axis * distance;
            var meteor = spawner.SpawnScenario(position, -axis * speed, Mathf.Clamp(distance / 15, 1.6f, 4.5f), damage);
            if (meteor == null) return null;
            var record = log.AddThreat(Stage, round, direction, evaluated, defense, demo, Elapsed, Risk(meteor));
            tracked.Add(meteor, record); meteor.Destroyed += Destroyed; meteor.ReachedPlayerEvent += Collided; return meteor;
        }
        public ThreatRisk Risk(MeteorController meteor) => ThreatRiskCalculator.Evaluate(meteor.transform.position - spawner.ShipCenter, meteor.Velocity, spawner.ShipCollisionRadius + meteor.CollisionRadius, meteor.Damage, settings.risk);
        private void TrackDetection(float dt)
        {
            if (!router.IsTrackingValid) { focused = null; dwell = 0; return; }
            Ray ray = router.GetGazeRay(); MeteorController nearest = null; float angle = settings.detectionConeDegrees;
            foreach (var pair in tracked)
            {
                var meteor = pair.Key; if (meteor == null || !meteor.IsTargetable) continue;
                float a = Vector3.Angle(ray.direction, meteor.transform.position - ray.origin);
                if (a > angle) continue;
                Vector3 to = meteor.transform.position - ray.origin;
                if (Physics.Raycast(ray.origin, to.normalized, out var hit, to.magnitude + .1f, ~0, QueryTriggerInteraction.Collide) && hit.collider.GetComponentInParent<MeteorController>() != meteor) continue;
                nearest = meteor; angle = a;
            }
            if (nearest != focused) { focused = nearest; dwell = 0; }
            if (focused == null) return;
            dwell += dt; if (dwell < settings.detectionDwell) return;
            var record = tracked[focused];
            if (Phase == EducationPhase.Search && record != objective)
            { if (wrongThisRound.Add(record.Id)) { log.WrongDetection(record, Elapsed); message = "다른 물체예요. "+(round%7==0?"가장 가까운 운석":EducationLabels.Direction(objective.Direction)+"의 물체")+"을 다시 찾아보세요."; Changed?.Invoke(); } return; }
            if (!log.Detect(record, Elapsed)) return;
            if (Phase == EducationPhase.Search) { log.Resolve(record, ThreatOutcome.Observed, Elapsed); audio?.TryPlay(AudioCue.CalibrationComplete); Feedback("좋아요. " + EducationLabels.Direction(record.Direction) + "의 물체를 발견했어요."); }
        }
        private int MostUrgentId(out float ttc)
        {
            int id = -1; float risk = -1; ttc = -1;
            foreach (var pair in tracked)
            {
                if (pair.Key == null || !pair.Key.IsTargetable || !pair.Value.RequiresDefense || pair.Value.Demonstration) continue;
                var state = Risk(pair.Key); if (!state.CanCollide || state.Danger <= risk) continue;
                risk = state.Danger; id = pair.Value.Id; ttc = state.Ttc;
            }
            return id;
        }
        private void Fired(GazeLockOnTarget target, MeteorController meteor)
        {
            if (!leased || log == null || log.Closed || !tracked.TryGetValue(meteor, out var record)) return;
            // Lock/fire implies an intentional observation even if the detection cone was briefly interrupted.
            log.Detect(record, Elapsed); log.Action(record, Elapsed);
            if (priorityQuestion != null && priorityQuestion.Attempts == 0)
            {
                int expected = MostUrgentId(out var ttc); log.Answer(priorityQuestion, record.Id, expected, Elapsed, ttc);
                explanation = record.Id == expected ? "충돌 예상시간이 짧은 위협부터 대응했어요." : "가까운 물체보다 빠르게 접근하는 물체가 먼저 충돌할 수 있어요.";
            }
        }
        private void Destroyed(MeteorController meteor)
        { if (tracked.TryGetValue(meteor, out var r)) log.Resolve(r, ThreatOutcome.Defended, Elapsed); }
        private void Collided(MeteorController meteor, int damage)
        {
            if (!tracked.TryGetValue(meteor, out var r)) return;
            log.Resolve(r, ThreatOutcome.Collision, Elapsed); ImpactSpecial(r, damage);
        }
        private void ClearRound(bool missed)
        {
            foreach (var pair in tracked)
            {
                if (pair.Key != null) { pair.Key.Destroyed -= Destroyed; pair.Key.ReachedPlayerEvent -= Collided; }
                log?.Resolve(pair.Value, missed ? ThreatOutcome.Missed : ThreatOutcome.Cancelled, Elapsed);
            }
            tracked.Clear(); wrongThisRound.Clear(); focused = null; spawner?.ClearScenarioRound();
        }
        private void Feedback(string text)
        {
            if(priorityQuestion!=null&&priorityQuestion.Attempts==0)log.Answer(priorityQuestion,-1,MostUrgentId(out var ttc),Elapsed,ttc);
            message = text; Phase = EducationPhase.Feedback; phaseStarted = Elapsed; deadline = Elapsed + settings.feedbackSeconds;
            weapon.AutoFireOnLock = false; SetModal(false); ClearRound(true); Changed?.Invoke();
        }
        private void SetModal(bool value)
        {
            if (router != null) { router.EducationModalOpen = value; router.TextEntryOpen = value && Phase == EducationPhase.Registration; if (router.Simulation != null) router.Simulation.MenuLookSuppressed = value; }
            if (ready != null) ready.InputBlocked = value;
        }
        private void Finish(bool complete)
        {
            if (log == null || log.Closed) return;
            ClearRound(false); log.Close(complete, Elapsed, stats.Score, DateTimeOffset.UtcNow); stats.StopSession(); Store.Add(log.Result, DateTimeOffset.UtcNow);
            stageRunning = false; weapon.AutoFireOnLock = false; Phase = EducationPhase.Report; SetModal(true);
            finishing = true; flow.ShowResult(); finishing = false; reset.CancelResultTimer(); Changed?.Invoke();
        }
        public void NextParticipant()
        {
            if (Phase != EducationPhase.Report) return;
            reset.ResetExperience(); Phase = EducationPhase.Registration; SetModal(true); Changed?.Invoke();
        }
        public void ReturnToReady()
        {
            if (leased) reset.ResetExperience(); Phase = EducationPhase.Idle; SetModal(false); Changed?.Invoke();
        }
        public bool ExportCsv(out string file)
        {
            file = ""; if (!OperatorAllowed || Phase != EducationPhase.Operator) return false;
            file = Path.Combine(StorageDirectory, "Exports", "Education-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".csv");
            return EducationCsvExporter.Write(file, Store.Records);
        }
        public bool ClearStoredResults() => OperatorAllowed && Phase == EducationPhase.Operator && Store.Clear();
        partial void BeginSpecialRound();
        partial void TickSpecial(float dt);
        partial void ImpactSpecial(ThreatLearningRecord record, int damage);
        partial void ResetSpecial();
    }
}
