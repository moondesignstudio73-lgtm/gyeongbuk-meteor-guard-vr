using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Audio;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.UI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.Tests
{
    public sealed class DifficultySoakTests
    {
        [UnityTearDown] public IEnumerator Cleanup() { Time.timeScale = 1; if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest, Timeout(7200000)]
        public IEnumerator EachDifficultyCompletesThreeNaturalRenderedSessions()
        {
            // Opt-in long run; the complete regression suite still includes all short difficulty tests.
            if (!Environment.GetCommandLineArgs().Contains("--difficulty-soak")) Assert.Ignore("Run with --difficulty-soak for the 12 complete, timeScale=1 sessions.");
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(); yield return Pilot(); yield return new ExitPlayMode();
        }
        private static IEnumerator Pilot()
        {
            var previousInput = InputSystem.settings.editorInputBehaviorInPlayMode; var previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var mouse = InputSystem.AddDevice<Mouse>();
            var router = Object.FindAnyObjectByType<GazeInputRouter>(); router.SetMode(PcGazeMode.MouseGaze);
            var manager = Object.FindAnyObjectByType<DifficultyManager>(); var ready = Object.FindAnyObjectByType<ReadyScreenController>();
            var spawner = Object.FindAnyObjectByType<MeteorSpawner>(); var boss = Object.FindAnyObjectByType<BossClimaxController>();
            var progression = Object.FindAnyObjectByType<MeteorDefenseVR.Progression.StageProgressionController>();
            var flow = GameFlowManager.Instance; var result = Object.FindAnyObjectByType<ResultController>();
            var lobby = Object.FindAnyObjectByType<HangarLobbyController>();
            var hud = Object.FindAnyObjectByType<MissionHudController>(FindObjectsInactive.Include); var audio = Object.FindAnyObjectByType<AudioManager>();
            var rows = new List<string> { "difficulty,round,missionFailed,secondsToResult,score,destroyed,shots,damageTaken,bossShots,peakConcurrent,renderedFrames" };
            var required = new[] { GameState.Intro, GameState.MissionBriefing, GameState.EyeCalibration, GameState.Tutorial, GameState.Practice,
                GameState.Launch, GameState.Countdown, GameState.Playing, GameState.BossMeteor, GameState.Result, GameState.Reset, GameState.Boot };
            try
            {
                using var render = new PcStabilityProbe(Camera.main);
                Time.timeScale = 1;
                yield return new WaitForSecondsRealtime(5);
                // Runtime UI installers finish in Start; take the lifetime baseline after Ready warms.
                int sourceCount = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Length;
                int canvasCount = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Length;
                Assert.That(Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Count(c => c.name == "DifficultySelection"), Is.EqualTo(1));
                var levels = new[] { DifficultyLevel.Easy, DifficultyLevel.Normal, DifficultyLevel.Hard, DifficultyLevel.Experience };
                string requestedLevel = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--difficulty-soak-level=", StringComparison.OrdinalIgnoreCase));
                if (requestedLevel != null && Enum.TryParse(requestedLevel.Split('=')[1], true, out DifficultyLevel parsedLevel)) levels = new[] { parsedLevel };
                int rounds = 3;
                string requestedRounds = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--difficulty-soak-rounds=", StringComparison.OrdinalIgnoreCase));
                if (requestedRounds != null && int.TryParse(requestedRounds.Split('=')[1], out int parsedRounds)) rounds = Mathf.Clamp(parsedRounds, 1, 3);
                foreach (DifficultyLevel level in levels)
                for (int round = 1; round <= rounds; round++)
                {
                    Assert.That(flow.CurrentState, Is.EqualTo(GameState.Boot));
                    if(lobby!=null && lobby.Phase==HangarLobbyController.LobbyPhase.Standby)
                        Object.FindAnyObjectByType<OperationsResetController>().ResetExperience();
                    manager.Select(level); Assert.That(manager.Selected, Is.EqualTo(level));
                    UnityEngine.Random.InitState(8400 + round); // Matching seeds across difficulty levels.
                    var visited = new HashSet<GameState>(); GameSessionSnapshot snapshot = null;
                    int resultCount = 0, bossShots = 0, expectedBossShots = 0, bossSpawns = 0, bossDestroys = 0, peakConcurrent = 0, expectedScore = 0;
                    bool savedProof = false;
                    float resultTime = 0, start = Time.realtimeSinceStartup;
                    Action<GameState> stateChanged = s => visited.Add(s);
                    Action<GameSessionSnapshot, string> resultShown = (s, _) => { snapshot = s; resultCount++; resultTime = Time.realtimeSinceStartup - start; };
                    Action<MeteorController, float> bossHit = (_, __) => bossShots++;
                    Action<MeteorController> meteorCompleted = m => { if (m.State == MeteorLifecycleState.Destroyed) expectedScore += m.Score; };
                    int bossAward = 0;
                    Action<MeteorController> bossSpawned = b => { bossAward = b.Score; bossSpawns++; expectedBossShots += Mathf.CeilToInt(b.MaxHealth); };
                    Action bossDestroyed = () => { expectedScore += bossAward; bossDestroys++; };
                    flow.OnStateChanged += stateChanged; result.ResultShown += resultShown; boss.BossDamaged += bossHit;
                    spawner.MeteorCompleted += meteorCompleted; boss.BossSpawned += bossSpawned; boss.BossDestroyed += bossDestroyed;
                    int framesBefore = render.RenderedFrames;
                    using var bgm = new BgmNaturalFlowProbe();
                    try
                    {
                        ready.StartExperience(); GazeLockOnTarget target = null;
                        // Experience authors a 240-second spawn schedule. Concurrent caps and the
                        // final meteor/boss resolution legitimately extend the wall-clock runtime.
                        float sessionDeadline = level == DifficultyLevel.Experience ? 600f : 900f;
                        while (Time.realtimeSinceStartup - start < sessionDeadline)
                        {
                            bgm.Sample(flow.CurrentState);
                            if (target == null || !target.IsAvailable)
                                target = Object.FindObjectsByType<GazeLockOnTarget>().Where(t => t.IsAvailable)
                                    .OrderBy(t => (t.transform.position - Camera.main.transform.position).sqrMagnitude).FirstOrDefault();
                            Vector2 position = target != null ? (Vector2)Camera.main.WorldToScreenPoint(target.transform.position)
                                : new Vector2(Screen.width * .5f, Screen.height * .45f);
                            UnifiedLookTests.QueueAim(mouse, router.Simulation, target != null ? target.transform : null, position);
                            if (target != null && target.Meteor != null && target.Meteor.MeteorType != MeteorType.Tutorial && manager.CombatActive)
                            {
                                Assert.That(target.HasDifficultyTuning, Is.True);
                                Assert.That(target.LockOnDuration, Is.EqualTo(manager.Session.LockSeconds));
                            }
                            peakConcurrent = Mathf.Max(peakConcurrent, spawner.ActiveCount);
                            if (round == 1 && !savedProof && result.IsVisible && Time.realtimeSinceStartup - start > resultTime + 1.5f)
                            {
                                string path = "Docs/DifficultyQA/natural-result-" + level;
                                Directory.CreateDirectory(path); render.SaveGameOnlyProof(path, router); savedProof = true;
                            }
                            if (lobby!=null && lobby.IsMenuReady) lobby.Queue(HangarLobbyController.MenuAction.Standby);
                            if (snapshot != null && flow.CurrentState == GameState.Boot) break;
                            yield return null;
                        }
                        yield return new WaitForSecondsRealtime(audio.FadeDuration + .15f);
                        foreach (var state in required) Assert.That(visited.Contains(state), Is.True, $"{level}/{round}: missing {state}; last={flow.CurrentState}");
                        Assert.That(visited.Contains(GameState.MissionComplete) || snapshot.MissionFailed, Is.True, $"{level}/{round}: neither success nor mission-failed result");
                        if (level == DifficultyLevel.Easy) Assert.That(snapshot.MissionFailed, Is.False, $"Easy/{round}: beginner pilot must be able to complete the campaign");
                        Assert.That(resultCount, Is.EqualTo(1)); Assert.That(snapshot.Difficulty, Is.EqualTo(level));
                        // Campaign score also includes stage-clear accuracy/no-hit bonuses.
                        Assert.That(snapshot.Score, Is.GreaterThanOrEqualTo(expectedScore));
                        Assert.That(snapshot.Score, Is.GreaterThan(0));
                        Assert.That(bossShots, Is.InRange(bossDestroys, expectedBossShots));
                        Assert.That(bossSpawns, Is.GreaterThan(0));
                        // A failed campaign can retry the same stage boss several times after impacts.
                        // Every destroyed boss must still have originated from a tracked spawn, but
                        // the number of unresolved retry spawns is intentionally not capped at one.
                        Assert.That(bossSpawns, Is.GreaterThanOrEqualTo(bossDestroys));
                        if (level != DifficultyLevel.Experience && !snapshot.MissionFailed) Assert.That(bossDestroys, Is.EqualTo(progression.TotalStages));
                        Assert.That(manager.Selected, Is.EqualTo(DifficultyLevel.Normal)); Assert.That(spawner.ActiveCount, Is.Zero); Assert.That(hud.Score, Is.Zero);
                        Assert.That(Object.FindObjectsByType<DifficultyManager>().Length, Is.EqualTo(1));
                        Assert.That(Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Length, Is.EqualTo(sourceCount));
                        Assert.That(Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Length, Is.EqualTo(canvasCount));
                        Assert.That(PcStabilityProbe.InspectSubscriptions().duplicateCallbacks, Is.Zero);
                        Assert.That(router.Webcam.IsStreaming, Is.False);
                        Assert.That(render.RenderedFrames - framesBefore, Is.GreaterThan(100)); bgm.Complete();
                        rows.Add(string.Join(",", level, round, snapshot.MissionFailed, resultTime.ToString("F2", CultureInfo.InvariantCulture), snapshot.Score, snapshot.DestroyedMeteors,
                            snapshot.Shots, snapshot.DamageTaken, bossShots, peakConcurrent, render.RenderedFrames - framesBefore));
                        File.WriteAllLines("Docs/DifficultyQA/natural-sessions.csv", rows);
                        Debug.Log($"[DifficultyQA] {level} {round}/{rounds} PASS score={snapshot.Score} destroyed={snapshot.DestroyedMeteors} bossHits={bossShots} seconds={resultTime:F2}");
                    }
                    finally
                    {
                        flow.OnStateChanged -= stateChanged; result.ResultShown -= resultShown; boss.BossDamaged -= bossHit;
                        spawner.MeteorCompleted -= meteorCompleted; boss.BossSpawned -= bossSpawned; boss.BossDestroyed -= bossDestroyed;
                    }
                }
            }
            finally { InputSystem.RemoveDevice(mouse); InputSystem.settings.editorInputBehaviorInPlayMode = previousInput; InputSystem.settings.backgroundBehavior = previousBackground; }
        }
    }
}
