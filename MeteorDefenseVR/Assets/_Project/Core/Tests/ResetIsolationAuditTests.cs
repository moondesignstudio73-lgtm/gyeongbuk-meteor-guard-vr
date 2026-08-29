using System;
using System.Collections.Generic;
using System.Reflection;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Player;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.Tests
{
    public sealed class ResetIsolationAuditTests
    {
        private GameObject root;
        private MeteorWaveData wave;
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        [SetUp] public void Setup() => root = new GameObject("ResetIsolationAudit");
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(root);
            if (wave != null) Object.DestroyImmediate(wave);
        }

        [Test] public void ResetDropsOldMeteorSubscriptionsAndNextSessionHitsOnlyOnce()
        {
            var spawner = root.AddComponent<MeteorSpawner>();
            var health = root.AddComponent<PlayerHealth>();
            var entry = new MeteorSpawnData();
            entry.Configure(MeteorType.Normal, null, 1, 0, 0, 0, 1, 25, 100, 1, new Vector2(20, 15));
            wave = ScriptableObject.CreateInstance<MeteorWaveData>(); wave.Configure("Reset audit", 0, entry);
            spawner.Configure(new[] { wave }, root.transform, root.transform);
            health.Configure(spawner, null, 100, 10, 0, GameOverPolicy.NoFailExperienceMode);
            spawner.BeginSpawning(); spawner.Tick(0);
            var oldMeteor = spawner.ActiveMeteors[0];
            spawner.StopSpawning(true); health.ResetHealth();
            var tracked = (HashSet<MeteorController>)typeof(PlayerHealth).GetField("trackedMeteors", Private).GetValue(health);
            Assert.That(tracked.Count, Is.Zero, "A reset must release references to the previous session.");
            var callbacks = (Delegate)typeof(MeteorController).GetField("ReachedPlayerEvent", Private).GetValue(oldMeteor);
            if (callbacks != null)
                foreach (var callback in callbacks.GetInvocationList()) Assert.That(callback.Target, Is.Not.SameAs(health));
            int hits = 0; health.DamageTaken += (_, __) => hits++;
            spawner.BeginSpawning(); spawner.Tick(0); spawner.ActiveMeteors[0].ReachPlayer();
            Assert.That(hits, Is.EqualTo(1)); Assert.That(health.CurrentHealth, Is.EqualTo(75));
        }

        [Test] public void HiddenResultClearsPreviousSnapshotWithoutDuplicateHiddenEvent()
        {
            var stats = root.AddComponent<GameSessionStats>(); var result = root.AddComponent<ResultController>();
            result.Configure(stats, null); stats.ResetSession(22); stats.SetScore(3300); result.ShowResult();
            Assert.That(result.Snapshot.Score, Is.EqualTo(3300));
            int hides = 0; result.ResultHidden += () => hides++;
            result.HideResult(); result.HideResult();
            Assert.That(result.Snapshot, Is.Null); Assert.That(result.Rank, Is.EqualTo("C"));
            Assert.That(hides, Is.EqualTo(1));
            stats.ResetSession(22); result.ShowResult(); Assert.That(result.Snapshot.Score, Is.Zero);
        }

        [Test] public void NonTargetLookupStaysCachedUntilProviderReset()
        {
            var collider = root.AddComponent<BoxCollider>(); var caster = root.AddComponent<GazeRaycaster>();
            var lookup = (Func<Collider, IGazeTarget>)Delegate.CreateDelegate(typeof(Func<Collider, IGazeTarget>), caster,
                typeof(GazeRaycaster).GetMethod("FindTargetCached", Private));
            Assert.That(lookup(collider), Is.Null);
            var target = root.AddComponent<GazeLockOnTarget>();
            // A rescan would discover this newly-added component. Cached null must not rescan.
            Assert.That(lookup(collider), Is.Null);
            caster.SetProvider(null);
            Assert.That(lookup(collider), Is.SameAs(target), "Provider/reset invalidation must permit a fresh lookup.");
        }
    }
}
