using System;
using System.Collections;
using System.IO;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.Tests
{
    public sealed class RearPanoramaCockpitTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/MeteorDefense.unity";

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void RearShellUsesPanoramaGlassAndSlimStructuralFrames()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform geometry = GameObject.Find("LaunchSystem").transform.Find("CockpitGeometry");
            Transform cockpit = geometry.Find("PremiumCockpit");
            Transform rear = cockpit.Find("RestoredRearCockpit");
            Assert.That(rear, Is.Not.Null);

            Assert.That(WorldSize(rear.Find("RearBulkhead_Port")).x, Is.LessThanOrEqualTo(.55f));
            Assert.That(WorldSize(rear.Find("RearBulkhead_Starboard")).x, Is.LessThanOrEqualTo(.55f));
            Assert.That(WorldSize(rear.Find("RearObservationGlass")).y, Is.GreaterThanOrEqualTo(1.95f));
            Assert.That(WorldSize(rear.Find("PortObservationGlass")).y, Is.GreaterThanOrEqualTo(1.95f));
            Assert.That(WorldSize(rear.Find("StarboardObservationGlass")).y, Is.GreaterThanOrEqualTo(1.95f));
            Assert.That(WorldSize(rear.Find("CeilingSpine")).x, Is.LessThanOrEqualTo(.25f));
            Assert.That(rear.Find("CeilingObservationGlass_Port"), Is.Not.Null);
            Assert.That(rear.Find("CeilingObservationGlass_Starboard"), Is.Not.Null);

            Material glass = rear.Find("RearObservationGlass").GetComponent<Renderer>().sharedMaterial;
            Assert.That(glass.shader.name, Is.EqualTo("MeteorDefense/Cockpit Canopy"));
            foreach (string monitor in new[] { "PortSecondary", "PortPrimary", "Navigation", "StarboardPrimary", "StarboardSecondary" })
                Assert.That(cockpit.Find(monitor), Is.Not.Null, monitor + " front layout regression");

            CockpitDamagePresentation damage = Object.FindAnyObjectByType<CockpitDamagePresentation>(FindObjectsInactive.Include);
            Assert.That(damage.PointCount, Is.EqualTo(8));
            using var serialized = new SerializedObject(damage);
            var points = serialized.FindProperty("points");
            foreach (int index in new[] { 5, 6, 7 })
            {
                Transform anchor = (Transform)points.GetArrayElementAtIndex(index).FindPropertyRelative("anchor").objectReferenceValue;
                Assert.That(damage.FindNearestPoint(anchor.position), Is.EqualTo(index));
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator CaptureEightDirectionMeteorVisibility()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            var router = Object.FindAnyObjectByType<GazeInputRouter>();
            var simulation = router.Simulation;
            router.SetMode(PcGazeMode.WindowsVRSimulation);
            Object.FindAnyObjectByType<DifficultyManager>().Select(DifficultyLevel.Extreme);
            var flow = GameFlowManager.Instance;
            flow.StartMainGame();
            float deadline = Time.realtimeSinceStartup + 8f;
            while (flow.CurrentState != GameState.Playing && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(flow.CurrentState, Is.EqualTo(GameState.Playing));

            var spawner = Object.FindAnyObjectByType<MeteorSpawner>();
            spawner.BeginScenario(8);
            using var probe = new PcStabilityProbe(Camera.main);
            yield return Capture(probe, router, simulation, spawner, 0, 0, "Front");
            yield return Capture(probe, router, simulation, spawner, -90, 0, "Left");
            yield return Capture(probe, router, simulation, spawner, -135, 0, "RearLeft");
            yield return Capture(probe, router, simulation, spawner, 180, 0, "Rear");
            yield return Capture(probe, router, simulation, spawner, 135, 0, "RearRight");
            yield return Capture(probe, router, simulation, spawner, 90, 0, "Right");
            yield return Capture(probe, router, simulation, spawner, 0, 65, "Above");
            yield return Capture(probe, router, simulation, spawner, 0, -65, "Below");
            spawner.EndScenario();
        }

        private static IEnumerator Capture(PcStabilityProbe probe, GazeInputRouter router,
            WindowsVRSimulation simulation, MeteorSpawner spawner, float yaw, float pitch, string name)
        {
            spawner.ClearScenarioRound();
            simulation.Recenter();
            yield return null;
            simulation.ApplyLook(new Vector2(yaw / .1f, -pitch / .09f), 1f);
            yield return new WaitForSecondsRealtime(.2f);
            Camera camera = Camera.main;
            MeteorController meteor = spawner.SpawnScenario(camera.transform.position + camera.transform.forward * 18f,
                Vector3.zero, 2.3f, 0, 10f);
            Assert.That(meteor, Is.Not.Null);
            yield return new WaitForSecondsRealtime(.15f);
            Vector3 viewport = camera.WorldToViewportPoint(meteor.transform.position);
            Assert.That(viewport.z, Is.GreaterThan(0), name);
            Assert.That(viewport.x, Is.InRange(.42f, .58f), name);
            Assert.That(viewport.y, Is.InRange(.40f, .60f), name);

            string phase = Environment.GetEnvironmentVariable("MD_REAR_CAPTURE_PHASE");
            if (string.IsNullOrWhiteSpace(phase)) phase = "After";
            string directory = Path.Combine("Docs/RearPanoramaQA", phase, name);
            Directory.CreateDirectory(directory);
            Canvas.ForceUpdateCanvases();
            camera.Render();
            probe.SaveGameOnlyProof(directory, router);
        }

        private static Vector3 WorldSize(Transform transform)
        {
            Assert.That(transform, Is.Not.Null);
            Renderer renderer = transform.GetComponent<Renderer>();
            Assert.That(renderer, Is.Not.Null, transform.name);
            return renderer.bounds.size;
        }
    }
}
