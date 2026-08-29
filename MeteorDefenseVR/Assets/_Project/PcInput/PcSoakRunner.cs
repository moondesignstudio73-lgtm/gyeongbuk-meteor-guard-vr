using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Audio;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.UI;
using MeteorDefenseVR.VFX;
using MeteorDefenseVR.Visual;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.PcInput
{
    public static class PcSoakRunner
    {
        [Serializable] public sealed class Round
        {
            public int number, score, destroyed, shots, hits, spawned, objects, materials, audioSources, playingAudio, canvases, pool, errors, warnings;
            public float startToResult, startToReset, mainSeconds, averageFps, p95FrameMs;
            public float bossAverageFps, bossP95FrameMs;
            public int peakEffectParticles, resetEffectParticles;
            public int renderedFrames, gen0Collections, gen1Collections, gen2Collections, framesOver33Ms, mainFrames;
            public int weaponHitEvents, hitVfxEvents, destroyedHitEvents, explosionEvents, bossExplosionEvents, laserAudioEvents, firedEvents;
            public int duplicateSpawnEvents, instantiatedMeteors, reusedMeteors;
            public float p99FrameMs, maxFrameMs, allStagesMaxFrameMs, averageFrameAllocationBytes;
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
            Application.runInBackground = true;
            Time.timeScale = 1f;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            router.SetMode(PcGazeMode.MouseGaze);
            router.Webcam.ShowWebcamPreview = false;
            Mouse simulated = InputSystem.AddDevice<Mouse>();
            var flow = GameFlowManager.Instance;
            var ready = Object.FindAnyObjectByType<ReadyScreenController>();
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
                    counts.Clear(); stageTimes.Clear(); frames.Clear(); bossFrames.Clear(); peakParticles=0; snapshot = null;
                    errors = warnings = resultEvents = completeAudio = resultAudio = 0; statsAgree = true;
                    allFrames.Clear();aliveSpawnEvents.Clear();allocationTotal=allocationMax=0;
                    hitEvents=hitVisuals=destroyedHits=explosions=bossExplosions=fired=laserCues=duplicateSpawns=waveSpawns=0;
                    int renderedBefore=probe.RenderedFrames,gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2);
                    roundStart = stateStart = Time.realtimeSinceStartup; reachedResult = 0; lastState = GameState.Boot;
                    ready.StartExperience();
                    GazeLockOnTarget target = null;
                    float nextTargetSearch=0;
                    while (Time.realtimeSinceStartup - roundStart < 160f)
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
                        InputSystem.QueueStateEvent(simulated, new MouseState { position = position });
                        if (flow.CurrentState == GameState.Playing) frames.Add(Time.unscaledDeltaTime);
                        if (flow.CurrentState == GameState.BossMeteor) bossFrames.Add(Time.unscaledDeltaTime);
                        allFrames.Add(Time.unscaledDeltaTime);
                        long allocated=probe.LastFrameAllocation;if(allocated>=0){allocationTotal+=allocated;allocationMax=Math.Max(allocationMax,allocated);}
                        if (premiumVfx != null) peakParticles=Mathf.Max(peakParticles,premiumVfx.ActiveParticleCount);
                        if (reachedResult > 0 && flow.CurrentState == GameState.Boot) break;
                        yield return null;
                    }
                    float resetTime = Time.realtimeSinceStartup;
                    yield return new WaitForSecondsRealtime(Mathf.Max(.5f, audio != null ? audio.FadeDuration + .1f : .5f)); // Include the intentional Reset music fade.
                    // Do not force GC: production-like heap growth and natural collections must remain observable.
                    frames.Sort();
                    bossFrames.Sort();
                    var sources = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include);
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
                        audioSources = sources.Length, playingAudio = sources.Count(s => s.isPlaying),
                        allocatedBytes = Profiler.GetTotalAllocatedMemoryLong(), managedBytes = GC.GetTotalMemory(false),
                        errors = errors, warnings = warnings,
                        renderedFrames=probe.RenderedFrames-renderedBefore,
                        gen0Collections=GC.CollectionCount(0)-gc0,gen1Collections=GC.CollectionCount(1)-gc1,gen2Collections=GC.CollectionCount(2)-gc2,
                        mainFrames=frames.Count,framesOver33Ms=frames.Count(f=>f>.033333f),
                        p99FrameMs=frames.Count>0?frames[Mathf.Min(frames.Count-1,(int)(frames.Count*.99f))]*1000:0,
                        maxFrameMs=frames.Count>0?frames[frames.Count-1]*1000:0,allStagesMaxFrameMs=allFrames.Count>0?allFrames.Max()*1000:0,
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
                        && !vfx.HitActive && !vfx.ExplosionActive && !vfx.BossExplosionActive && item.playingAudio == 0
                        && !router.Webcam.IsStreaming && item.errors == 0 && item.startToResult > 0 && item.resetEffectParticles == 0;
                    item.passed &= item.renderedFrames>item.mainFrames*.9f && hitEvents==hitVisuals && destroyedHits==explosions && bossExplosions==1
                        && laserCues==fired && duplicateSpawns==0 && item.subscriptions.duplicateCallbacks==0 && item.subscriptions.coroutineHandles==0;
                    if (round > 3)
                    {
                        Round baseline = report.rounds[2];
                        item.passed &= item.objects == baseline.objects && item.canvases == baseline.canvases && item.audioSources == baseline.audioSources
                            && item.materials <= baseline.materials + 2 && item.managedBytes < baseline.managedBytes + 8 * 1024 * 1024
                            && item.subscriptions.callbacks==baseline.subscriptions.callbacks && item.instantiatedMeteors==baseline.instantiatedMeteors
                            && item.allocatedBytes < baseline.allocatedBytes + 64 * 1024 * 1024;
                    }
                    if (!item.passed) report.issues.Add($"Round {round}: state/stat/reset/leak assertions failed; resultEvents={resultEvents}, statsAgree={statsAgree}, victoryAudio={completeAudio}, resultAudio={resultAudio}, counts={string.Join(",", counts.Select(kv => kv.Key + "=" + kv.Value))}");
                    report.rounds.Add(item);
                    File.WriteAllText(path, JsonUtility.ToJson(report, true));
                    Debug.Log($"[PCSoak] Round {round}/{repetitions} {(item.passed ? "PASS" : "FAIL")} result={item.startToResult:F2}s reset={item.startToReset:F2}s fps={item.averageFps:F1} objects={item.objects} materials={item.materials} memory={item.allocatedBytes / 1048576f:F1}MB");
                    if (!item.passed) break;
                }
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
            report.passed = report.preflight.passed && report.rounds.Count == repetitions && report.rounds.All(r => r.passed);
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Debug.Log("[PCSoak] " + (report.passed ? "PASS" : "FAIL") + "; QA/pc-soak.json");
            Application.Quit(report.passed ? 0 : 2);
        }
    }
}
