using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MeteorDefenseVR.Audio;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.Tests
{
    public sealed class DifficultyTests
    {
        private static DifficultySettings Settings => AssetDatabase.LoadAssetAtPath<DifficultySettings>("Assets/_Project/Difficulty/Resources/DifficultySettings.asset");
        [Test]
        public void DefaultsSeparateSpawnDirectionFromStagePressure()
        {
            Assert.That(Settings, Is.Not.Null);
            Assert.That(Settings.Default, Is.EqualTo(DifficultyLevel.Normal));
            Assert.That(Settings.resetDifficultyAfterSession && !Settings.lockDifficultySelection, Is.True);
            var easy = Settings.easy.Capture(DifficultyLevel.Easy); var normal = Settings.normal.Capture(DifficultyLevel.Normal); var hard = Settings.hard.Capture(DifficultyLevel.Hard);
            foreach (var value in new[] { normal.Speed, normal.Health, normal.Interval, normal.Size, normal.Damage, normal.BossHealth, normal.BossSpeed, normal.Score }) Assert.That(value, Is.EqualTo(1));
            Assert.That(easy.Speed, Is.EqualTo(normal.Speed)); Assert.That(hard.Speed, Is.EqualTo(normal.Speed));
            Assert.That(easy.Interval, Is.EqualTo(normal.Interval)); Assert.That(hard.Interval, Is.EqualTo(normal.Interval));
            Assert.That(easy.SpawnDirections, Is.EqualTo(SpawnDirectionMode.FrontOnly));
            Assert.That(normal.SpawnDirections, Is.EqualTo(SpawnDirectionMode.FrontAndSides));
            Assert.That(hard.SpawnDirections, Is.EqualTo(SpawnDirectionMode.FullSphere));
            Assert.That(Settings.Experience.Capture(DifficultyLevel.Experience).SpawnDirections, Is.EqualTo(SpawnDirectionMode.ExperienceArc));
            Assert.That(easy.ScaleScore(100), Is.EqualTo(100)); Assert.That(normal.ScaleScore(100), Is.EqualTo(100)); Assert.That(hard.ScaleScore(100), Is.EqualTo(100));
            Assert.That(hard.ScaleScore(int.MaxValue), Is.EqualTo(int.MaxValue));
        }
        [Test]
        public void InvalidAssetValuesAreClampedAndSnapshotCannotChange()
        {
            var copy = Object.Instantiate(Settings.hard);
            try
            {
                var before = copy.Capture(DifficultyLevel.Hard);
                copy.meteorSpeed = float.NaN; copy.lockDuration = float.PositiveInfinity; copy.maxConcurrentMeteors = int.MaxValue;
                var after = copy.Capture(DifficultyLevel.Hard);
                Assert.That(after.Speed, Is.EqualTo(1)); Assert.That(after.LockSeconds, Is.EqualTo(.65f)); Assert.That(after.MaxConcurrent, Is.EqualTo(6));
                Assert.That(before.Speed, Is.EqualTo(1f));
            }
            finally { Object.DestroyImmediate(copy); }
        }
        [TestCase(DifficultyLevel.Easy, 3)] [TestCase(DifficultyLevel.Normal, 3)] [TestCase(DifficultyLevel.Hard, 3)]
        public void BossHealthAndLockTuningRequireExpectedShots(DifficultyLevel level, int shots)
        {
            var root = new GameObject("DifficultyBossFixture");
            try
            {
                var boss = root.AddComponent<BossClimaxController>(); boss.SetDifficulty(Settings.Get(level).Capture(level));
                boss.SetDurations(0, 0, 0); boss.BeginClimax(); boss.Tick(.1f); boss.Tick(.1f);
                var meteor = boss.ActiveBoss; var target = meteor.GetComponent<GazeLockOnTarget>();
                Assert.That(Mathf.CeilToInt(meteor.MaxHealth), Is.EqualTo(shots));
                Assert.That(target.LockOnDuration, Is.EqualTo(Settings.Get(level).lockDuration));
                var weapon = root.AddComponent<LaserWeapon>();
                for (int hit = 0; hit < shots; hit++) { target.Advance(target.LockOnDuration); weapon.TickCooldown(1); Assert.That(weapon.TryFire(target), Is.True); }
                Assert.That(boss.IsComplete, Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test]
        public void PoolReuseDoesNotCompoundStatsAssistScoreOrDamage()
        {
            var root = new GameObject("DifficultyPoolFixture"); var wave = ScriptableObject.CreateInstance<MeteorWaveData>();
            try
            {
                var data = new MeteorSpawnData(); data.Configure(MeteorType.Normal, null, 3, .1f, 0, 2, 1, 10, 100, 1, Vector2.one);
                wave.Configure("QA", 0, new[] { data });
                var spawner = root.AddComponent<MeteorSpawner>(); spawner.Configure(new[] { wave }, root.transform, root.transform);
                var health = root.AddComponent<PlayerHealth>(); health.Configure(spawner, null, 100, 10, 0, GameOverPolicy.NoFailExperienceMode);
                for (int i = 0; i < 6; i++)
                {
                    var level = i % 2 == 0 ? DifficultyLevel.Hard : DifficultyLevel.Easy; var balance = Settings.Get(level).Capture(level);
                    spawner.SetDifficulty(balance); spawner.BeginSpawning(); spawner.Tick(1);
                    var meteor = spawner.ActiveMeteors[0]; var target = meteor.GetComponent<GazeLockOnTarget>();
                    float distance = Vector3.Distance(meteor.transform.position, spawner.ShipCenter);
                    float scale = balance.AllDirections ? Mathf.Clamp(distance / 14f, 1, 2.6f) : 1;
                    float expectedSpeed = 2 * balance.Speed * scale;
                    if (balance.AllDirections) expectedSpeed = Mathf.Min(expectedSpeed, Mathf.Max(.1f, (distance - 4.2f - balance.Size * scale * 1.5f) / balance.ReactionSeconds));
                    Assert.That(meteor.Speed, Is.EqualTo(expectedSpeed).Within(.001f));
                    Assert.That(meteor.Score, Is.EqualTo(balance.ScaleScore(100)));
                    Assert.That(target.GetComponent<SphereCollider>().radius, Is.EqualTo(.5f * balance.ColliderExpansion).Within(.0001f));
                    target.Advance(balance.LockSeconds / 2); float progress = target.Progress;
                    target.TickWithoutGaze(balance.Grace * .5f); Assert.That(target.Progress, Is.EqualTo(progress));
                    target.TickWithoutGaze(balance.Grace + .1f); Assert.That(target.Progress, Is.LessThan(progress));
                    meteor.ReachPlayer(); Assert.That(health.TotalDamageTaken, Is.EqualTo(balance.ScaleDamage(10)));
                    spawner.StopSpawning(true);
                }
                spawner.SetDifficulty(Settings.easy.Capture(DifficultyLevel.Easy)); spawner.BeginSpawning();
                for (int i = 0; i < 20; i++) spawner.Tick(1);
                Assert.That(spawner.ActiveCount, Is.EqualTo(3)); Assert.That(spawner.SpawnedCount, Is.EqualTo(3));
                spawner.ActiveMeteors[0].DestroyMeteor(); spawner.Tick(1);
                Assert.That(spawner.SpawnedCount, Is.EqualTo(3)); Assert.That(spawner.AllWavesSpawned, Is.True);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(wave); }
        }
        [UnityTearDown] public IEnumerator Cleanup() { Time.timeScale = 1f; if (Application.isPlaying) yield return new ExitPlayMode(); }
        [UnityTest, Timeout(120000)]
        public IEnumerator ReadyMouseAndGazeSelectionFreezeResetOperatorAndResult()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode(); yield return VerifySelection(); yield return new ExitPlayMode();
        }
        private static IEnumerator VerifySelection()
        {
            var input = InputSystem.settings.editorInputBehaviorInPlayMode; var background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var mouse = InputSystem.AddDevice<Mouse>();
            var manager = Object.FindAnyObjectByType<DifficultyManager>(); var original = manager.Settings; var copy = Object.Instantiate(original);
            var field = typeof(DifficultyManager).GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic); field.SetValue(manager, copy);
            var router = Object.FindAnyObjectByType<GazeInputRouter>(); router.SetMode(PcGazeMode.MouseGaze);
            var flow = GameFlowManager.Instance; var reset = Object.FindAnyObjectByType<OperationsResetController>();
            var selector = Object.FindAnyObjectByType<DifficultySelectionView>(); var caster = Object.FindAnyObjectByType<GazeRaycaster>();
            try
            {
                using var render = new PcStabilityProbe(Camera.main);
                var presentation = Object.FindAnyObjectByType<SfPresentationDirector>();
                float readyDeadline = Time.realtimeSinceStartup + 25;
                while (presentation.IsBootPresenting && Time.realtimeSinceStartup < readyDeadline) yield return null;
                Assert.That(presentation.IsBootPresenting, Is.False, "Wait for real preparation, not a fixed delay");
                yield return new WaitForSecondsRealtime(.5f);
                Assert.That(manager.Selected, Is.EqualTo(DifficultyLevel.Normal));
                Assert.That(selector.Choices.Length, Is.EqualTo(4));
                foreach (var choice in selector.Choices)
                    foreach (var text in choice.GetComponentsInChildren<TextMeshPro>())
                    {
                        Assert.That(text.font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
                        Assert.That(text.font.HasCharacters(text.text), Is.True, text.text);
                        Assert.That(text.GetComponent<Renderer>().sortingOrder, Is.GreaterThan(choice.GetComponentInParent<Canvas>().sortingOrder));
                    }
                Save(render, router, "ready-normal");
                var easy = selector.Choices[1]; var hard = selector.Choices[3];
                Vector3 bounds = easy.GetComponent<Collider>().bounds.size;
                Vector2 position = Camera.main.WorldToScreenPoint(easy.transform.position);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return new WaitForSecondsRealtime(.1f);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left)); yield return new WaitForSecondsRealtime(.1f);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return new WaitForSecondsRealtime(.1f);
                Assert.That(manager.Selected, Is.EqualTo(DifficultyLevel.Easy), "Real mouse click must select");
                Assert.That(flow.CurrentState, Is.EqualTo(GameState.Boot));
                Assert.That(easy.GetComponent<Collider>().bounds.size, Is.EqualTo(bounds));
                Save(render, router, "ready-easy");
                // Same IGazeProvider contract used by webcam and eye-tracking, without recording a face.
                caster.SetProvider(new FixedGaze(Camera.main.transform.position, hard.transform.position));
                yield return new WaitForSecondsRealtime(.25f); Save(render, router, "gaze-hard-progress");
                yield return new WaitForSecondsRealtime(.7f);
                Assert.That(manager.Selected, Is.EqualTo(DifficultyLevel.Hard));
                Save(render, router, "ready-hard"); caster.SetProvider(router);
                Object.FindAnyObjectByType<ReadyScreenController>().StartExperience();
                Assert.That(manager.SessionFrozen, Is.True); Assert.That(manager.Select(DifficultyLevel.Easy), Is.False);
                float saved = manager.Session.Speed;
                var configCopy = Object.Instantiate(copy.hard); copy.hard = configCopy; configCopy.meteorSpeed = .5f;
                Assert.That(manager.Session.Speed, Is.EqualTo(saved)); Object.Destroy(configCopy); copy.hard = original.hard;
                flow.StartPractice(); yield return new WaitForSecondsRealtime(.2f);
                foreach (var target in Object.FindObjectsByType<GazeLockOnTarget>()) Assert.That(target.HasDifficultyTuning, Is.False, "Practice must retain assistance");
                reset.ResetExperience(); Assert.That(manager.Selected, Is.EqualTo(DifficultyLevel.Normal));
                copy.resetDifficultyAfterSession = false; manager.Select(DifficultyLevel.Hard); flow.StartGame(); reset.ResetExperience();
                Assert.That(manager.Selected, Is.EqualTo(DifficultyLevel.Hard));
                copy.defaultDifficulty = DifficultyLevel.Easy; copy.lockDifficultySelection = true; manager.ApplyOperatorDefaults();
                Assert.That(manager.Selected, Is.EqualTo(DifficultyLevel.Easy)); Assert.That(manager.Select(DifficultyLevel.Hard), Is.False);
                flow.StartGame(); reset.ResetExperience(); yield return new WaitForSecondsRealtime(1.3f); Save(render, router, "operator-easy-fixed");
                Assert.That(selector.Choices.All(c => !c.GetComponent<UnityEngine.UI.Button>().interactable), Is.True);
                copy.lockDifficultySelection = false; copy.defaultDifficulty = DifficultyLevel.Normal; manager.ApplyOperatorDefaults();
                for (int i = 0; i < 3; i++) { manager.enabled = false; manager.enabled = true; }
                Assert.That(PcStabilityProbe.InspectSubscriptions().duplicateCallbacks, Is.Zero);
                manager.Select(DifficultyLevel.Hard); flow.StartGame(); flow.ShowResult();
                yield return new WaitForSecondsRealtime(1.4f);
                var result = Object.FindAnyObjectByType<ResultController>(); Assert.That(result.Snapshot.Difficulty, Is.EqualTo(DifficultyLevel.Hard));
                Assert.That(WorldTextPresentation.FormatResult(result.Snapshot, result.Rank), Does.Contain("난이도 : 어려움"));
                Save(render, router, "result-hard");
            }
            finally { field.SetValue(manager, original); Object.Destroy(copy); InputSystem.RemoveDevice(mouse); InputSystem.settings.editorInputBehaviorInPlayMode = input; InputSystem.settings.backgroundBehavior = background; }
        }
        private static void Save(PcStabilityProbe render, GazeInputRouter router, string name)
        { string path = "Docs/DifficultyQA/" + name; Directory.CreateDirectory(path); render.SaveGameOnlyProof(path, router); }
        private sealed class FixedGaze : IGazeProvider
        {
            private readonly Ray ray;
            public FixedGaze(Vector3 from, Vector3 to) => ray = new Ray(from, (to - from).normalized);
            public bool IsTrackingValid => true;
            public Ray GetGazeRay() => ray;
            public Vector3 GetGazeOrigin() => ray.origin;
            public Vector3 GetGazeDirection() => ray.direction;
        }
    }
}
