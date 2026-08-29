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
        [UnityTest, Timeout(2400000)]
        public IEnumerator EachDifficultyCompletesFiveNaturalRenderedSessions()
        {
            // Opt-in long run; the complete regression suite still includes all short difficulty tests.
            if (!Environment.GetCommandLineArgs().Contains("--difficulty-soak")) Assert.Ignore("Run with --difficulty-soak for the 15 complete, timeScale=1 sessions.");
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
            var flow = GameFlowManager.Instance; var result = Object.FindAnyObjectByType<ResultController>();
            var lobby = Object.FindAnyObjectByType<HangarLobbyController>();
            var hud = Object.FindAnyObjectByType<MissionHudController>(FindObjectsInactive.Include); var audio = Object.FindAnyObjectByType<AudioManager>();
            var rows = new List<string> { "difficulty,round,secondsToResult,score,destroyed,shots,damageTaken,bossShots,peakConcurrent,renderedFrames" };
            var required = new[] { GameState.Intro, GameState.MissionBriefing, GameState.EyeCalibration, GameState.Tutorial, GameState.Practice,
                GameState.Launch, GameState.Countdown, GameState.Playing, GameState.BossMeteor, GameState.MissionComplete, GameState.Result, GameState.Reset, GameState.Boot };
            try
            {
                using var render = new PcStabilityProbe(Camera.main);
                Time.timeScale = 1;
                yield return new WaitForSecondsRealtime(5);
                // Runtime UI installers finish in Start; take the lifetime baseline after Ready warms.
                int sourceCount = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Length;
                int canvasCount = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Length;
                Assert.That(Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Count(c => c.name == "DifficultySelection"), Is.EqualTo(1));
                for (int levelIndex = 0; levelIndex < 3; levelIndex++)
                for (int round = 1; round <= 5; round++)
                {
                    var level = (DifficultyLevel)levelIndex;
                    Assert.That(flow.CurrentState, Is.EqualTo(GameState.Boot));
                    if(lobby!=null && lobby.Phase==HangarLobbyController.LobbyPhase.Standby)
                        Object.FindAnyObjectByType<OperationsResetController>().ResetExperience();
                    manager.Select(level); Assert.That(manager.Selected, Is.EqualTo(level));
                    UnityEngine.Random.InitState(8400 + round); // Matching seeds across difficulty levels.
                    var visited = new HashSet<GameState>(); GameSessionSnapshot snapshot = null;
                    int resultCount = 0, bossShots = 0, peakConcurrent = 0, expectedScore = 0;
                    bool savedProof = false;
                    float resultTime = 0, start = Time.realtimeSinceStartup;
                    Action<GameState> stateChanged = s => visited.Add(s);
                    Action<GameSessionSnapshot, string> resultShown = (s, _) => { snapshot = s; resultCount++; resultTime = Time.realtimeSinceStartup - start; };
                    Action<MeteorController, float> bossHit = (_, __) => bossShots++;
                    Action<MeteorController> meteorCompleted = m => { if (m.State == MeteorLifecycleState.Destroyed) expectedScore += m.Score; };
                    int bossAward = 0;
                    Action<MeteorController> bossSpawned = b => bossAward = b.Score;
                    Action bossDestroyed = () => expectedScore += bossAward;
                    flow.OnStateChanged += stateChanged; result.ResultShown += resultShown; boss.BossDamaged += bossHit;
                    spawner.MeteorCompleted += meteorCompleted; boss.BossSpawned += bossSpawned; boss.BossDestroyed += bossDestroyed;
                    int framesBefore = render.RenderedFrames;
                    using var bgm = new BgmNaturalFlowProbe();
                    try
                    {
                        ready.StartExperience(); GazeLockOnTarget target = null;
                        while (Time.realtimeSinceStartup - start < 175f)
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
                        Assert.That(resultCount, Is.EqualTo(1)); Assert.That(snapshot.Difficulty, Is.EqualTo(level)); Assert.That(snapshot.Score, Is.EqualTo(expectedScore));
                        Assert.That(snapshot.Score, Is.GreaterThan(0)); Assert.That(bossShots, Is.EqualTo(level == DifficultyLevel.Easy ? 2 : level == DifficultyLevel.Normal ? 3 : 5));
                        Assert.That(manager.Selected, Is.EqualTo(DifficultyLevel.Normal)); Assert.That(spawner.ActiveCount, Is.Zero); Assert.That(hud.Score, Is.Zero);
                        Assert.That(Object.FindObjectsByType<DifficultyManager>().Length, Is.EqualTo(1));
                        Assert.That(Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Length, Is.EqualTo(sourceCount));
                        Assert.That(Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Length, Is.EqualTo(canvasCount));
                        Assert.That(PcStabilityProbe.InspectSubscriptions().duplicateCallbacks, Is.Zero);
                        Assert.That(router.Webcam.IsStreaming, Is.False);
                        Assert.That(render.RenderedFrames - framesBefore, Is.GreaterThan(100)); bgm.Complete();
                        rows.Add(string.Join(",", level, round, resultTime.ToString("F2", CultureInfo.InvariantCulture), snapshot.Score, snapshot.DestroyedMeteors,
                            snapshot.Shots, snapshot.DamageTaken, bossShots, peakConcurrent, render.RenderedFrames - framesBefore));
                        File.WriteAllLines("Docs/DifficultyQA/natural-sessions.csv", rows);
                        Debug.Log($"[DifficultyQA] {level} {round}/5 PASS score={snapshot.Score} destroyed={snapshot.DestroyedMeteors} bossHits={bossShots} seconds={resultTime:F2}");
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
