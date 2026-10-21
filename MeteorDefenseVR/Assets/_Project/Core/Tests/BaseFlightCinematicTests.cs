using System.Collections;
using System.IO;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Launch;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Player;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeteorDefenseVR.Tests
{
    public sealed class BaseFlightCinematicTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup() { Time.timeScale = 1; if (Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest, Timeout(150000)]
        public IEnumerator DepartureAndFailedLandingPreserveResultAndResetSafely()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return Verify();
        }
        private static IEnumerator Verify()
        {
            var router = Object.FindAnyObjectByType<GazeInputRouter>(); router.InputSuspended = true;
            var warm = Object.FindAnyObjectByType<ReadyPrewarmController>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (!warm.IsComplete && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(warm.IsComplete, Is.True);
            var cinema = Object.FindAnyObjectByType<BaseFlightCinematic>();
            Assert.That(Resources.Load<Shader>("FlightSmoke"), Is.Not.Null);
            Assert.That(Resources.Load<Shader>("FlightSmoke").isSupported, Is.True);
            Assert.That(Resources.Load<Shader>("FlightVelocityBlur").isSupported, Is.True);
            var launch = Object.FindAnyObjectByType<LaunchSequenceController>();
            var flow = GameFlowManager.Instance;
            var result = Object.FindAnyObjectByType<ResultController>();
            var reset = Object.FindAnyObjectByType<OperationsResetController>();
            var stats = Object.FindAnyObjectByType<GameSessionStats>();
            var health = Object.FindAnyObjectByType<PlayerHealth>();
            var camera = Camera.main; var position = camera.transform.position; var rotation = camera.transform.rotation;
            using var proof = new PcStabilityProbe(camera);
            launch.SetDurations(.3f, .7f, .7f, .15f, 2, .6f, .3f);
            flow.StartLaunch();
            yield return new WaitForSecondsRealtime(.7f); Save(proof, router, "door-opening");
            Assert.That(cinema.CurrentShot, Is.EqualTo(BaseFlightCinematic.Shot.Departure));
            var fuselage=GameObject.Find("Fuselage");
            Assert.That(fuselage.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(16));
            Assert.That(fuselage.GetComponent<Renderer>().sharedMaterial.GetTexture("_BaseMap"), Is.Not.Null);
            Assert.That(fuselage.GetComponent<Renderer>().sharedMaterial.GetTexture("_BumpMap"), Is.Not.Null);
            yield return new WaitForSecondsRealtime(1.1f); Save(proof, router, "engine-ready");
            yield return new WaitForSecondsRealtime(1.3f); Save(proof, router, "acceleration");
            Assert.That(cinema.BlurStrength, Is.GreaterThan(0));
            deadline = Time.realtimeSinceStartup + 8;
            while (flow.CurrentState != GameState.Playing && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(flow.CurrentState, Is.EqualTo(GameState.Playing));
            Assert.That(cinema.CurrentShot, Is.EqualTo(BaseFlightCinematic.Shot.None));
            stats.SetScore(138940);
            health.ApplyImpactDamage(100, health.transform.position);
            Assert.That(flow.CurrentState, Is.EqualTo(GameState.Result));
            Assert.That(result.IsVisible, Is.False);
            Assert.That(result.IsPresentationPending, Is.True);
            Assert.That(result.Snapshot.Score, Is.EqualTo(138940));
            reset.Tick(9);
            Assert.That(flow.CurrentState, Is.EqualTo(GameState.Result), "Result timeout must wait for landing");
            yield return new WaitForSecondsRealtime(.5f); Save(proof, router, "damaged-approach");
            Time.timeScale = 0; float paused = cinema.ReturnProgress;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(cinema.ReturnProgress, Is.EqualTo(paused)); Time.timeScale = 1;
            yield return new WaitForSecondsRealtime(2.8f); Save(proof, router, "landing");
            Assert.That(result.IsVisible, Is.False);
            yield return new WaitForSecondsRealtime(1.2f); Save(proof, router, "door-closing");
            Assert.That(result.IsVisible, Is.False);
            yield return new WaitForSecondsRealtime(1);
            deadline = Time.realtimeSinceStartup + 5;
            while (!cinema.HasReturned && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(cinema.HasReturned, Is.True);
            Assert.That(cinema.DoorOpening, Is.EqualTo(0).Within(.001f));
            Assert.That(result.IsVisible, Is.True);
            Assert.That(result.Snapshot.Score, Is.EqualTo(138940));
            yield return new WaitForSecondsRealtime(1.6f);
            Save(proof, router, "failed-result");
            reset.Tick(8.1f);
            var lobby = Object.FindAnyObjectByType<HangarLobbyController>();
            Assert.That(lobby.IsMenuReady, Is.True, "Already docked failure must not replay the return");
            reset.ResetExperience(); flow.StartMainGame();
            health.ApplyImpactDamage(100, health.transform.position);
            Assert.That(result.IsPresentationPending, Is.True);
            reset.ResetExperience(); yield return null;
            Assert.That(result.IsPresentationPending, Is.False);
            Assert.That(cinema.CurrentShot, Is.EqualTo(BaseFlightCinematic.Shot.None));
            Assert.That(camera.transform.position, Is.EqualTo(position)); Assert.That(camera.transform.rotation, Is.EqualTo(rotation));
        }
        private static void Save(PcStabilityProbe proof, GazeInputRouter router, string label)
        {
            var directory = "Docs/DeliveryQA/hangar-graphics-20261022/After/" + label; Directory.CreateDirectory(directory); proof.SaveGameOnlyProof(directory, router);
        }
    }
}
