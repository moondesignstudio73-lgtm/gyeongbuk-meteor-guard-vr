using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Audio;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.UI;
using MeteorDefenseVR.VFX;
using MeteorDefenseVR.Visual;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.PcInput
{
    public static class PcSoakRunner
    {
        [Serializable] public sealed class Round
        {
            public int number, score, destroyed, shots, hits, spawned, objects, materials, audioSources, playingAudio, audibleAudio, canvases, pool, errors, warnings;
            public string[] playingAudioDetails;
            public float startToResult, startToReset, mainSeconds, averageFps, p95FrameMs;
            public float bossAverageFps, bossP95FrameMs;
            public int peakEffectParticles, resetEffectParticles;
            public int renderedFrames, gen0Collections, gen1Collections, gen2Collections, framesOver33Ms, mainFrames;
            public int weaponHitEvents, hitVfxEvents, destroyedHitEvents, explosionEvents, bossExplosionEvents, laserAudioEvents, firedEvents;
            public int duplicateSpawnEvents, instantiatedMeteors, reusedMeteors;
            public float p99FrameMs, maxFrameMs, allStagesMaxFrameMs, averageFrameAllocationBytes;
            public string maxFrameObservedState;
            public float maxFrameObservedAtSeconds;
            public long maxFrameAllocationBytes;
            public PcStabilityProbe.SubscriptionSnapshot subscriptions;
            public long allocatedBytes, managedBytes, privateBytes;
            public bool passed;
            public string[] stages;
        }
        [Serializable] public sealed class Report
        {
            public string startedUtc, completedUtc, input = "Synthetic mouse through real InputSystem; no direct combat/state completion; timeScale=1";
            public bool passed;
            public bool allocationCounterAvailable;
            public bool profilerAllocationCounterAvailable;
            public string allocationMeasurement;
            public string rendering = "1280x720 HDR game-camera RenderTexture; explicit QA Camera.Render every LateUpdate; verified endCameraRendering callbacks; no HMD; no forced GC";
            public string gpu;
            public PcStabilityPreflight.Report preflight = new PcStabilityPreflight.Report();
            public string webcam = "No camera used or images recorded. Physical webcam endurance requires separate operator QA.";
            public List<Round> rounds = new List<Round>();
            public List<string> issues = new List<string>();
        }
        public static IEnumerator Run(GazeInputRouter router, int repetitions)
        {
            var report = new Report { startedUtc = DateTime.UtcNow.ToString("O") };
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "QA"));
            Directory.CreateDirectory(output);
            string path = Path.Combine(output, "pc-soak.json");
            bool audioControlsQa = Environment.GetCommandLineArgs().Contains("--pc-audio-controls-qa");
            bool deliveryQa = Environment.GetCommandLineArgs().Contains("--pc-delivery-qa");
            bool threatQa = Environment.GetCommandLineArgs().Contains("--pc-threat-qa");
            bool telemetryQa = Environment.GetCommandLineArgs().Contains("--pc-telemetry-qa");
            bool recoilQa = Environment.GetCommandLineArgs().Contains("--pc-recoil-qa");
            bool damageQa = Environment.GetCommandLineArgs().Contains("--pc-damage-qa");
            bool dockRepairQa = Environment.GetCommandLineArgs().Contains("--pc-dock-repair-qa");
            bool buffReadabilityQa = Environment.GetCommandLineArgs().Contains("--pc-buff-readability-qa");
            bool resultFeedbackQa = Environment.GetCommandLineArgs().Contains("--pc-result-feedback-qa");
            bool controlDroneQa = Environment.GetCommandLineArgs().Contains("--pc-control-drone-qa");
            bool victoryEarthQa = Environment.GetCommandLineArgs().Contains("--pc-victory-earth-response-qa");
            bool introTransitionQa = Environment.GetCommandLineArgs().Contains("--pc-intro-transition-qa");
            bool pausePriorityQa = Environment.GetCommandLineArgs().Contains("--pc-pause-priority-qa");
            bool viewSettingsQa = Environment.GetCommandLineArgs().Contains("--pc-view-settings-qa");
            bool powerUpQa = Environment.GetCommandLineArgs().Contains("--pc-powerup-feedback-qa");
            bool bossShellQa = Environment.GetCommandLineArgs().Contains("--pc-boss-shell-qa");
            var capturedStates = new HashSet<GameState>();
            Application.runInBackground = true;
            Time.timeScale = 1f;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            router.SetMode(PcGazeMode.MouseGaze);
            router.Webcam.ShowWebcamPreview = false;
            Mouse simulated = InputSystem.AddDevice<Mouse>();
            var flow = GameFlowManager.Instance;
            var ready = Object.FindAnyObjectByType<ReadyScreenController>();
            var lobby = Object.FindAnyObjectByType<HangarLobbyController>();
            var stats = Object.FindAnyObjectByType<GameSessionStats>();
            var results = Object.FindAnyObjectByType<ResultController>();
            var hud = Object.FindAnyObjectByType<MissionHudController>(FindObjectsInactive.Include);
            var spawner = Object.FindAnyObjectByType<MeteorSpawner>();
            var vfx = Object.FindAnyObjectByType<CombatVfxController>();
            var premiumVfx = Object.FindAnyObjectByType<PremiumCombatVfx>();
            var audio = Object.FindAnyObjectByType<AudioManager>();
            var weapon = Object.FindAnyObjectByType<LaserWeapon>();
            Camera camera = Camera.main;
            using var probe = new PcStabilityProbe(camera);
            void Capture(string label)
            {
                string folder = Path.Combine(output, "Delivery", label);
                Directory.CreateDirectory(folder);
                Canvas.ForceUpdateCanvases();
                PcStabilityRenderDriver.RenderForProof(camera);
                probe.SaveGameOnlyProof(folder, router);
            }
            report.gpu = SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceType;
            report.allocationCounterAvailable = probe.AllocationCounterAvailable;
            report.profilerAllocationCounterAvailable = probe.ProfilerAllocationCounterAvailable;
            report.allocationMeasurement = probe.AllocationMeasurement;
            var counts = new Dictionary<GameState, int>();
            var stageTimes = new Dictionary<GameState, float>();
            var frames = new List<float>(10000);
            var bossFrames = new List<float>(3000);
            var allFrames = new List<float>(15000);
            var aliveSpawnEvents = new HashSet<MeteorController>();
            int hitEvents=0, hitVisuals=0, destroyedHits=0, explosions=0, bossExplosions=0, fired=0, laserCues=0, duplicateSpawns=0, waveSpawns=0;
            long allocationTotal=0, allocationMax=0;
            int peakParticles=0;
            int errors = 0, warnings = 0, resultEvents = 0, completeAudio = 0, resultAudio = 0;
            float roundStart = 0, stateStart = 0, reachedResult = 0;
            float peakFrame = 0, peakObservedAt = 0;
            GameState peakObservedState = GameState.Boot;
            GameState lastState = GameState.Boot;
            GameSessionSnapshot snapshot = null;
            bool statsAgree = true;
            Application.LogCallback log = (message, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { errors++; if (report.issues.Count < 30) report.issues.Add(message); }
                if (type == LogType.Warning) warnings++;
            };
            Action<GameState> stateChanged = state =>
            {
                float now = Time.realtimeSinceStartup;
                if (!stageTimes.ContainsKey(lastState)) stageTimes[lastState] = 0;
                stageTimes[lastState] += now - stateStart;
                lastState = state; stateStart = now;
                counts[state] = counts.TryGetValue(state, out int n) ? n + 1 : 1;
                if (state == GameState.Result) reachedResult = now;
            };
            Action<GameSessionSnapshot, string> resultShown = (data, rank) =>
            {
                resultEvents++; snapshot = data;
                statsAgree &= data.Score == stats.Score && data.Score == hud.Score && data.DestroyedMeteors == stats.DestroyedMeteors
                    && data.Shots == stats.Shots && data.SuccessfulHits == stats.SuccessfulHits;
            };
            Action<AudioCue, AudioCategory> cue = (value, category) =>
            { if (value == AudioCue.MissionComplete) completeAudio++; if (value == AudioCue.Result) resultAudio++; if(value==AudioCue.Laser)laserCues++; };
            Action<MeteorController,Vector3> weaponHit=(meteor,_)=>{hitEvents++;if(meteor!=null&&meteor.State==MeteorLifecycleState.Destroyed)destroyedHits++;};
            Action<GazeLockOnTarget,MeteorController> weaponFired=(_,__)=>fired++;
            Action<Vector3> hitVisual=_=>hitVisuals++, explosion=_=>explosions++, bossExplosion=_=>bossExplosions++;
            Action<MeteorController,int,int> spawned=(meteor,_,__)=>{waveSpawns++;if(!aliveSpawnEvents.Add(meteor))duplicateSpawns++;};
            Action<MeteorController> completed=meteor=>aliveSpawnEvents.Remove(meteor);
            Application.logMessageReceived += log;
            flow.OnStateChanged += stateChanged;
            results.ResultShown += resultShown;
            audio.CuePlaybackRequested += cue;
            weapon.Hit+=weaponHit;weapon.Fired+=weaponFired;
            vfx.HitVfxPlayed+=hitVisual;vfx.ExplosionVfxPlayed+=explosion;vfx.BossExplosionVfxPlayed+=bossExplosion;
            spawner.MeteorSpawned+=spawned;spawner.MeteorCompleted+=completed;
            try
            {
                yield return PcStabilityPreflight.Run(router, report.preflight);
                if (!report.preflight.passed) report.issues.Add("Windows abnormal-state/Public preflight failed; see preflight flags");
                // Prove a real render has occurred even when Windows hides the player window.
                for(int warm=0;warm<10;warm++)yield return null;
                if(probe.RenderedFrames>0)probe.SaveGameOnlyProof(output,router);
                else report.issues.Add("No game-camera rendering observed; FPS is not a rendering benchmark");
                yield return new WaitForSecondsRealtime(.5f); // Keep proof readback outside timed rounds.
                for (int round = 1; round <= repetitions; round++)
                {
                    UnityEngine.Random.InitState(7300 + round);
                    var difficulty = Object.FindAnyObjectByType<DifficultyManager>();
                    if (difficulty != null && difficulty.Selected != DifficultyLevel.Experience)
                        difficulty.Select(DifficultyLevel.Experience);
                    counts.Clear(); stageTimes.Clear(); frames.Clear(); bossFrames.Clear(); peakParticles=0; snapshot = null;
                    errors = warnings = resultEvents = completeAudio = resultAudio = 0; statsAgree = true;
                    allFrames.Clear();aliveSpawnEvents.Clear();allocationTotal=allocationMax=0;
                    peakFrame=0;peakObservedAt=0;peakObservedState=GameState.Boot;
                    hitEvents=hitVisuals=destroyedHits=explosions=bossExplosions=fired=laserCues=duplicateSpawns=waveSpawns=0;
                    int renderedBefore=probe.RenderedFrames,gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2);
                    roundStart = stateStart = Time.realtimeSinceStartup; reachedResult = 0; lastState = GameState.Boot;
                    if (lobby != null && lobby.Phase == HangarLobbyController.LobbyPhase.Standby)
                        lobby.Queue(HangarLobbyController.MenuAction.StartNewPlayer);
                    else ready.StartExperience();
                    float startDeadline = Time.realtimeSinceStartup + 5f;
                    while (flow.CurrentState == GameState.Boot && Time.realtimeSinceStartup < startDeadline) yield return null;
                    GazeLockOnTarget target = null;
                    float nextTargetSearch=0;
                    while (Time.realtimeSinceStartup - roundStart < 220f)
                    {
                        if ((target == null || !target.IsAvailable) && Time.realtimeSinceStartup>=nextTargetSearch)
                        {
                            nextTargetSearch=Time.realtimeSinceStartup+.1f;target=null;float nearest=float.MaxValue;
                            foreach(var candidate in Object.FindObjectsByType<GazeLockOnTarget>())
                            {
                                float distance=(candidate.transform.position-camera.transform.position).sqrMagnitude;
                                if(candidate.IsAvailable&&distance<nearest){target=candidate;nearest=distance;}
                            }
                        }
                        Vector2 position = target != null ? (Vector2)camera.WorldToScreenPoint(target.transform.position)
                            : new Vector2(Screen.width * .5f, Screen.height * .45f);
                        PcPlayerSmoke.QueueAim(simulated, router.Simulation, target != null ? target.transform : null, position);
                        if (flow.CurrentState == GameState.Playing) frames.Add(Time.unscaledDeltaTime);
                        if (flow.CurrentState == GameState.BossMeteor) bossFrames.Add(Time.unscaledDeltaTime);
                        allFrames.Add(Time.unscaledDeltaTime);
                        // The state at observation localizes the sample; it does not attribute the preceding frame's cost.
                        if(Time.unscaledDeltaTime>peakFrame)
                        {
                            peakFrame=Time.unscaledDeltaTime;
                            peakObservedState=flow.CurrentState;
                            peakObservedAt=Time.realtimeSinceStartup-roundStart;
                        }
                        long allocated=probe.LastFrameAllocation;if(allocated>=0){allocationTotal+=allocated;allocationMax=Math.Max(allocationMax,allocated);}
                        if (premiumVfx != null) peakParticles=Mathf.Max(peakParticles,premiumVfx.ActiveParticleCount);
                        if (deliveryQa && Time.realtimeSinceStartup - stateStart > 1.7f
                            && capturedStates.Add(flow.CurrentState))
                            Capture("victory-" + flow.CurrentState);
                        if (lobby != null && lobby.IsMenuReady) lobby.Queue(HangarLobbyController.MenuAction.Standby);
                        if (reachedResult > 0 && flow.CurrentState == GameState.Boot) break;
                        yield return null;
                    }
                    float resetTime = Time.realtimeSinceStartup;
                    yield return new WaitForSecondsRealtime(Mathf.Max(.5f, audio != null ? audio.FadeDuration + .1f : .5f)); // Include the intentional Reset music fade.
                    // Do not force GC: production-like heap growth and natural collections must remain observable.
                    frames.Sort();
                    bossFrames.Sort();
                    var sources = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include);
                    var playingSources = sources.Where(source => source != null && source.isPlaying).ToArray();
                    bool AudioIsAudible(AudioSource source) => source.gameObject.activeInHierarchy && source.enabled && !source.mute && source.volume > .001f;
                    bool audioResetClean = playingSources.Length <= 1 && playingSources.All(source => audio != null &&
                        ((audio.OwnsBgmSource(source) && !AudioIsAudible(source)) ||
                         (audio.OwnsAmbientSource(source) && source.clip != null && source.clip.name == "IntroSpaceAmbient" && source.loop)));
                    var item = new Round
                    {
                        number = round, startToResult = reachedResult > 0 ? reachedResult - roundStart : -1,
                        startToReset = resetTime - roundStart, mainSeconds = stageTimes.TryGetValue(GameState.Playing, out float main) ? main : 0,
                        averageFps = frames.Count > 0 ? frames.Count / frames.Sum() : 0,
                        p95FrameMs = frames.Count > 0 ? frames[Mathf.Min(frames.Count - 1, (int)(frames.Count * .95f))] * 1000 : 0,
                        bossAverageFps=bossFrames.Count>0?bossFrames.Count/bossFrames.Sum():0,
                        bossP95FrameMs=bossFrames.Count>0?bossFrames[Mathf.Min(bossFrames.Count-1,(int)(bossFrames.Count*.95f))]*1000:0,
                        peakEffectParticles=peakParticles,resetEffectParticles=premiumVfx!=null?premiumVfx.ActiveParticleCount:0,
                        score = snapshot?.Score ?? 0, destroyed = snapshot?.DestroyedMeteors ?? 0, shots = snapshot?.Shots ?? 0, hits = snapshot?.SuccessfulHits ?? 0,
                        spawned = spawner.SpawnedCount, pool = spawner.PoolCount,
                        objects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include).Length,
                        materials = Resources.FindObjectsOfTypeAll<Material>().Length,
                        canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Length,
                        audioSources = sources.Length, playingAudio = playingSources.Length, audibleAudio = playingSources.Count(AudioIsAudible),
                        playingAudioDetails = playingSources.Select(source => $"{source.name}: clip={(source.clip != null ? source.clip.name : "none")}, loop={source.loop}, volume={source.volume:F4}, mute={source.mute}").ToArray(),
                        allocatedBytes = Profiler.GetTotalAllocatedMemoryLong(), managedBytes = GC.GetTotalMemory(false),
                        errors = errors, warnings = warnings,
                        renderedFrames=probe.RenderedFrames-renderedBefore,
                        gen0Collections=GC.CollectionCount(0)-gc0,gen1Collections=GC.CollectionCount(1)-gc1,gen2Collections=GC.CollectionCount(2)-gc2,
                        mainFrames=frames.Count,framesOver33Ms=frames.Count(f=>f>.033333f),
                        p99FrameMs=frames.Count>0?frames[Mathf.Min(frames.Count-1,(int)(frames.Count*.99f))]*1000:0,
                        maxFrameMs=frames.Count>0?frames[frames.Count-1]*1000:0,allStagesMaxFrameMs=allFrames.Count>0?allFrames.Max()*1000:0,
                        maxFrameObservedState=peakObservedState.ToString(),maxFrameObservedAtSeconds=peakObservedAt,
                        maxFrameAllocationBytes=probe.AllocationCounterAvailable?allocationMax:-1,
                        averageFrameAllocationBytes=probe.AllocationCounterAvailable&&allFrames.Count>0?(float)allocationTotal/allFrames.Count:-1,
                        weaponHitEvents=hitEvents,hitVfxEvents=hitVisuals,destroyedHitEvents=destroyedHits,explosionEvents=explosions,bossExplosionEvents=bossExplosions,
                        firedEvents=fired,laserAudioEvents=laserCues,duplicateSpawnEvents=duplicateSpawns,
                        instantiatedMeteors=spawner.InstantiatedCount,reusedMeteors=spawner.ReusedCount,
                        subscriptions=PcStabilityProbe.InspectSubscriptions(),
                        stages = stageTimes.Select(kv => kv.Key + ": " + kv.Value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).ToArray()
                    };
                    using (var process = System.Diagnostics.Process.GetCurrentProcess()) item.privateBytes = process.PrivateMemorySize64;
                    item.spawned=waveSpawns;
                    var required = new[] { GameState.Intro, GameState.MissionBriefing, GameState.EyeCalibration, GameState.Tutorial, GameState.Practice,
                        GameState.Launch, GameState.Countdown, GameState.Playing, GameState.BossMeteor, GameState.MissionComplete, GameState.Result, GameState.Reset, GameState.Boot };
                    item.passed = required.All(s => counts.TryGetValue(s, out int n) && n == 1) && resultEvents == 1 && statsAgree
                        && completeAudio == 1 && resultAudio == 1 && stats.Score == 0 && hud.Score == 0
                        && stats.Shots == 0 && stats.DestroyedMeteors == 0 && spawner.ActiveCount == 0
                        && !vfx.HitActive && !vfx.ExplosionActive && !vfx.BossExplosionActive && audioResetClean
                        && !router.Webcam.IsStreaming && item.errors == 0 && item.startToResult > 0 && item.resetEffectParticles == 0;
                    item.passed &= item.renderedFrames>item.mainFrames*.9f && hitEvents==hitVisuals && destroyedHits==explosions+bossExplosions && bossExplosions==1
                        && laserCues==fired && duplicateSpawns==0 && item.subscriptions.duplicateCallbacks==0 && item.subscriptions.coroutineHandles==0;
                    if (round > 3)
                    {
                        Round baseline = report.rounds[2];
                        item.passed &= item.objects == baseline.objects && item.canvases == baseline.canvases && item.audioSources == baseline.audioSources
                            && item.materials <= baseline.materials + 2 && item.managedBytes < baseline.managedBytes + 8 * 1024 * 1024
                            && item.subscriptions.callbacks==baseline.subscriptions.callbacks && item.instantiatedMeteors==baseline.instantiatedMeteors
                            && item.allocatedBytes < baseline.allocatedBytes + 64 * 1024 * 1024;
                    }
                    if (!item.passed) report.issues.Add($"Round {round}: state/stat/reset/leak assertions failed; resultEvents={resultEvents}, statsAgree={statsAgree}, victoryAudio={completeAudio}, resultAudio={resultAudio}, playing={string.Join(" | ", item.playingAudioDetails)}, counts={string.Join(",", counts.Select(kv => kv.Key + "=" + kv.Value))}");
                    report.rounds.Add(item);
                    File.WriteAllText(path, JsonUtility.ToJson(report, true));
                    Debug.Log($"[PCSoak] Round {round}/{repetitions} {(item.passed ? "PASS" : "FAIL")} result={item.startToResult:F2}s reset={item.startToReset:F2}s fps={item.averageFps:F1} objects={item.objects} materials={item.materials} memory={item.allocatedBytes / 1048576f:F1}MB");
                    if (!item.passed) break;
                }
                if (deliveryQa && report.rounds.All(r => r.passed))
                {
                    Capture("restart-standby");
                    // Explicit failure fixture, separate from the synthetic-input victory run.
                    // Exercises the shipped return/result path without waiting for random misses.
                    Object.FindAnyObjectByType<DifficultyManager>().Select(DifficultyLevel.Normal);
                    flow.StartMainGame();
                    yield return new WaitForSecondsRealtime(.3f);
                    stats.SetScore(138940);
                    Object.FindAnyObjectByType<MeteorDefenseVR.Player.PlayerHealth>()
                        .ApplyImpactDamage(100, camera.transform.position);
                    bool pending = results.IsPresentationPending && !results.IsVisible;
                    yield return new WaitForSecondsRealtime(.6f); Capture("failure-approach");
                    yield return new WaitForSecondsRealtime(2.9f); Capture("failure-landing");
                    yield return new WaitForSecondsRealtime(1.2f); Capture("failure-door-closing");
                    bool retained = results.Snapshot.Score == 138940 && results.IsPresentationPending;
                    float deadline = Time.realtimeSinceStartup + 8;
                    while (!results.IsVisible && Time.realtimeSinceStartup < deadline) yield return null;
                    bool revealed = results.IsVisible && results.Snapshot.Score == 138940;
                    yield return new WaitForSecondsRealtime(1.7f); Capture("failure-result");
                    yield return new WaitForSecondsRealtime(3f);
                    bool readableDuration = results.IsVisible;
                    if (!threatQa && !telemetryQa && !recoilQa && !damageQa && !dockRepairQa && !bossShellQa && !powerUpQa && !buffReadabilityQa && !resultFeedbackQa && !viewSettingsQa && !pausePriorityQa && !ResultBoundaryProof.Capture(camera, router, Path.Combine(output, "Delivery", "result-boundaries")))
                        report.issues.Add("Delivery result boundary fixture failed");
                    deadline = Time.realtimeSinceStartup + 15;
                    while (!lobby.IsMenuReady && Time.realtimeSinceStartup < deadline) yield return null;
                    Capture("failure-after-result");
                    bool returned = Object.FindAnyObjectByType<BaseFlightCinematic>().HasReturned;
                    bool passed = pending && retained && revealed && readableDuration
                        && lobby.IsMenuReady;
                    File.WriteAllText(Path.Combine(output, "Delivery", "failure-fixture.txt"),
                        $"Input: explicit state/score/damage fixture; not human or synthetic-mouse failure gameplay\n" +
                        $"pending={pending}; retainedBeforeDoorClose={retained}; revealedWith138940={revealed}; " +
                        $"visibleAfter4.7seconds={readableDuration}; hangarMenuReady={lobby.IsMenuReady}; nextState={flow.CurrentState}; hasReturned={returned}; passed={passed}\n");
                    if (!passed) report.issues.Add("Delivery failure/return fixture failed");
                }
                if(threatQa)
                    yield return ThreatPriorityProof.Run(router,Path.Combine(output,"ThreatPriority"),true,
                        passed=>{if(!passed)report.issues.Add("Threat priority fixture failed");});
                if(telemetryQa)
                    yield return TelemetryReadabilityProof.Run(router,Path.Combine(output,"TelemetryReadable"),true,
                        passed=>{if(!passed)report.issues.Add("Telemetry readability fixture failed");});
                if(recoilQa)
                    yield return TurretRecoilProof.Run(router,Path.Combine(output,"TurretRecoil"),true,
                        passed=>{if(!passed)report.issues.Add("Turret recoil fixture failed");});
                if(damageQa)
                    yield return MeteorDamageProof.Run(router,Path.Combine(output,"MeteorDamage"),true,
                        passed=>{if(!passed)report.issues.Add("Meteor damage fixture failed");});
                if(dockRepairQa)
                    yield return DockRepairProof.Run(router,Path.Combine(output,"DockRepair"),
                        passed=>{if(!passed)report.issues.Add("Dock repair fixture failed");});
                if(buffReadabilityQa)
                    yield return BuffReadabilityProof.Run(router,Path.Combine(output,"BuffReadability"),true,
                        passed=>{if(!passed)report.issues.Add("Buff readability fixture failed");});
                if(resultFeedbackQa)
                    yield return ResultFeedbackProof.Run(router,Path.Combine(output,"ResultFeedback"),true,
                        passed=>{if(!passed)report.issues.Add("Result feedback fixture failed");});
                if(controlDroneQa)
                {
                    yield return ControlDroneProof.Run(router,Path.Combine(output,"ControlDrone"),passed=>{if(!passed)report.issues.Add("Control drone fixture failed");});
                    yield return ControlReturnScanProof.Run(router,Path.Combine(output,"ControlReturnScan"),passed=>{if(!passed)report.issues.Add("Control return scan fixture failed");});
                }
                if(victoryEarthQa)
                    yield return VictoryEarthProof.Run(router,Path.Combine(output,"VictoryEarth"),true,passed=>{if(!passed)report.issues.Add("Victory Earth fixture failed");});
                if(introTransitionQa)
                    yield return IntroTransitionProof.Run(router,Path.Combine(output,"IntroTransition"),true,passed=>{if(!passed)report.issues.Add("Intro transition fixture failed");});
                if(pausePriorityQa)
                    yield return PausePriorityProof.Run(router,Path.Combine(output,"PausePriority"),passed=>{if(!passed)report.issues.Add("Pause priority fixture failed");});
                if(viewSettingsQa)
                    yield return ViewSettingsProof.Run(router,Path.Combine(output,"ViewSettings"),true,
                        passed=>{if(!passed)report.issues.Add("View settings fixture failed");});
                if(Environment.GetCommandLineArgs().Contains("--pc-audio-lifecycle-qa"))
                {
                    yield return AudioLifecycleProof.Run(router,Path.Combine(output,"AudioLifecycle"),passed=>{if(!passed)report.issues.Add("Audio lifecycle fixture failed");});
                    yield return AudioPriorityProof.Run(Path.Combine(output,"AudioPriority"),passed=>{if(!passed)report.issues.Add("Audio priority fixture failed");});
                    yield return SpatialCadenceProof.Run(Path.Combine(output,"SpatialCadence"),true,passed=>{if(!passed)report.issues.Add("Spatial cadence fixture failed");});
                    yield return BgmCrossfadeProof.Run(Path.Combine(output,"BgmCrossfade"),true,passed=>{if(!passed)report.issues.Add("BGM crossfade fixture failed");});
                    yield return CombatVariationProof.Run(Path.Combine(output,"CombatVariation"),true,passed=>{if(!passed)report.issues.Add("Combat variation fixture failed");});
                }
                if(Environment.GetCommandLineArgs().Contains("--pc-stage-notice-qa"))
                    yield return StageBannerOverlapProof.Run(Path.Combine(output,"StageNotice"));
                if(Environment.GetCommandLineArgs().Contains("--pc-selection-state-qa"))
                    yield return SelectionStateProof.Run(Path.Combine(output,"SelectionState"));
                if(Environment.GetCommandLineArgs().Contains("--pc-menu-transition-qa"))
                    yield return MenuTransitionGazeProof.Run(Path.Combine(output,"MenuTransitionGaze"),true,passed=>{if(!passed)report.issues.Add("Menu transition gaze fixture failed");});
                if(Environment.GetCommandLineArgs().Contains("--pc-campaign-audio-qa"))
                    yield return CampaignAudioContractProof.Run(Path.Combine(output,"CampaignAudio"),passed=>{if(!passed)report.issues.Add("Campaign audio fixture failed");});
                if(Environment.GetCommandLineArgs().Contains("--pc-result-countup-qa"))
                    yield return ResultCountupProof.Run(router,Path.Combine(output,"ResultCountup"),passed=>{if(!passed)report.issues.Add("Result countup fixture failed");});
                if(audioControlsQa)
                    yield return AudioControlsProof.Run(router,Path.Combine(output,"AudioControls"),passed=>{if(!passed)report.issues.Add("Audio controls fixture failed");});
                if(powerUpQa)
                    yield return PowerUpFeedbackProof.Run(router,Path.Combine(output,"PowerUpFeedback"),
                        passed=>{if(!passed)report.issues.Add("Power-up feedback fixture failed");});
                if(bossShellQa)
                    yield return BossShellProof.Run(router,Path.Combine(output,"BossShell"),
                        passed=>{if(!passed)report.issues.Add("Boss shell fixture failed");});
            }
            finally
            {
                flow.OnStateChanged -= stateChanged; results.ResultShown -= resultShown; audio.CuePlaybackRequested -= cue;
                Application.logMessageReceived -= log; InputSystem.RemoveDevice(simulated);
                weapon.Hit-=weaponHit;weapon.Fired-=weaponFired;
                vfx.HitVfxPlayed-=hitVisual;vfx.ExplosionVfxPlayed-=explosion;vfx.BossExplosionVfxPlayed-=bossExplosion;
                spawner.MeteorSpawned-=spawned;spawner.MeteorCompleted-=completed;
            }
            report.completedUtc = DateTime.UtcNow.ToString("O");
            report.passed = report.preflight.passed && report.rounds.Count == repetitions && report.rounds.All(r => r.passed)
                && !report.issues.Contains("Delivery failure/return fixture failed")
                && !report.issues.Contains("Delivery result boundary fixture failed")
                && !report.issues.Contains("Threat priority fixture failed")
                && !report.issues.Contains("Telemetry readability fixture failed")
                && !report.issues.Contains("Turret recoil fixture failed")
                && !report.issues.Contains("Meteor damage fixture failed")
                && !report.issues.Contains("Dock repair fixture failed")
                && !report.issues.Contains("Boss shell fixture failed")
                && !report.issues.Contains("Power-up feedback fixture failed")
                && !report.issues.Contains("Buff readability fixture failed")
                && !report.issues.Contains("Result feedback fixture failed")
                && !report.issues.Contains("View settings fixture failed")
                && !report.issues.Contains("Audio controls fixture failed")
                && !report.issues.Contains("Audio lifecycle fixture failed")
                && !report.issues.Contains("Campaign audio fixture failed")
                && !report.issues.Contains("Result countup fixture failed")
                && !report.issues.Contains("Pause priority fixture failed")
                && !report.issues.Contains("Intro transition fixture failed")
                && !report.issues.Contains("Victory Earth fixture failed")
                && !report.issues.Contains("Control drone fixture failed");
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Debug.Log("[PCSoak] " + (report.passed ? "PASS" : "FAIL") + "; QA/pc-soak.json");
            Application.Quit(report.passed ? 0 : 2);
        }
    }
}

