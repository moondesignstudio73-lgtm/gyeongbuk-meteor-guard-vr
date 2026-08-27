using MeteorDefenseVR.Meteor;
using NUnit.Framework;
using UnityEngine;

namespace MeteorDefenseVR.Tests
{
    public sealed class MeteorSpawnerTests
    {
        private GameObject root;
        private GameObject targetObject;
        private MeteorSpawner spawner;
        private MeteorWaveData wave;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("MeteorSpawnerTest");
            targetObject = new GameObject("SpawnTarget");
            targetObject.transform.position = Vector3.zero;
            spawner = root.AddComponent<MeteorSpawner>();

            var entry = new MeteorSpawnData();
            entry.Configure(MeteorType.Normal, null, 2, 1f, 1f, 2f, 1f, 10, 100, 1f, new Vector2(35f, 25f));
            wave = ScriptableObject.CreateInstance<MeteorWaveData>();
            wave.Configure("Test Wave", 1f, entry);
            spawner.Configure(new[] { wave }, root.transform, targetObject.transform);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (targetObject != null) Object.DestroyImmediate(targetObject);
            if (wave != null) Object.DestroyImmediate(wave);
        }

        [Test]
        public void WaveDelayAndEntryDelayAreAppliedBeforeFirstSpawn()
        {
            spawner.BeginSpawning();
            spawner.Tick(1.5f);
            Assert.That(spawner.SpawnedCount, Is.Zero);

            spawner.Tick(0.5f);
            Assert.That(spawner.SpawnedCount, Is.EqualTo(1));
            spawner.Tick(1f);

            Assert.That(spawner.SpawnedCount, Is.EqualTo(2));
            Assert.That(spawner.AllWavesSpawned, Is.True);
        }

        [Test]
        public void SpawnPositionClampsRequestedAreaToGazeFriendlyFov()
        {
            Vector3 position = spawner.CalculateSpawnPosition(1f, 1f, 0f, new Vector2(80f, 80f));
            Vector3 local = root.transform.InverseTransformDirection((position - root.transform.position).normalized);
            float horizontal = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float vertical = Mathf.Atan2(local.y, local.z) * Mathf.Rad2Deg;

            Assert.That(horizontal, Is.EqualTo(35f).Within(0.01f));
            Assert.That(vertical, Is.EqualTo(25f).Within(0.01f));
            Assert.That(Vector3.Distance(position, root.transform.position), Is.EqualTo(10f).Within(0.01f));
        }

        [Test]
        public void DestroyedMeteorLeavesActiveListAndCountsAsCompleted()
        {
            spawner.BeginSpawning();
            spawner.Tick(2f);
            MeteorController meteor = spawner.ActiveMeteors[0];
            meteor.DestroyMeteor();

            Assert.That(spawner.ActiveCount, Is.Zero);
            Assert.That(spawner.CompletedCount, Is.EqualTo(1));
        }

        [Test]
        public void WaveExposesTotalAndEstimatedDuration()
        {
            Assert.That(wave.TotalMeteors, Is.EqualTo(2));
            Assert.That(wave.EstimatedDuration, Is.EqualTo(3f).Within(0.001f));
        }
    }
}
