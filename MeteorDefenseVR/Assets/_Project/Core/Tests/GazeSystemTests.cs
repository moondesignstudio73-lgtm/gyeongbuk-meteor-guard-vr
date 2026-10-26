using System.Collections.Generic;
using System.Collections;
using MeteorDefenseVR.EyeTracking;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeteorDefenseVR.Tests
{
    public sealed class GazeSystemTests
    {
        private GameObject root;

        public sealed class DirectMenuTarget : MonoBehaviour, IDirectGazeTarget
        {
            public float Duration;
            public void OnGazeEnter(RaycastHit hit) { }
            public void OnGazeStay(RaycastHit hit,float deltaTime) => Duration+=deltaTime;
            public void OnGazeExit() { }
        }

        [Test]
        public void CombatAssistanceCannotSelectNearbyMenuWithoutDirectAim()
        {
            root=new GameObject("MenuAimBoundary");
            var provider=new TestGazeProvider{Origin=Vector3.one*10000};
            var caster=root.AddComponent<GazeRaycaster>();caster.SetProvider(provider);caster.GazeMagnetismRadius=.3f;
            var item=GameObject.CreatePrimitive(PrimitiveType.Cube);item.transform.SetParent(root.transform);
            item.transform.position=provider.Origin+new Vector3(.25f,0,1);item.transform.localScale=Vector3.one*.1f;
            var combat=item.AddComponent<GazeTarget>();Physics.SyncTransforms();caster.Tick(.2f);
            Assert.That(caster.CurrentTarget,Is.SameAs(combat),"Combat targets retain aim assistance");
            Object.DestroyImmediate(combat);var menu=item.AddComponent<DirectMenuTarget>();caster.SetProvider(provider);caster.Tick(.8f);
            Assert.That(caster.CurrentTarget,Is.Null);Assert.That(menu.Duration,Is.Zero,"Nearby menus must not accumulate dwell");
            provider.Direction=item.transform.position-provider.Origin;caster.Tick(.8f);
            Assert.That(caster.CurrentTarget,Is.SameAs(menu));Assert.That(menu.Duration,Is.EqualTo(.8f).Within(.001));
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void EditorProvider_CameraCenter_ReturnsForwardRay()
        {
            root = new GameObject("GazeTestRoot");
            Camera camera = root.AddComponent<Camera>();
            root.transform.SetPositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 30f, 0f));
            EditorGazeProvider provider = root.AddComponent<EditorGazeProvider>();
            provider.SetCamera(camera);
            provider.Mode = EditorGazeMode.CameraCenter;

            Ray ray = provider.GetGazeRay();
            Ray expectedRay = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            Assert.That(provider.IsTrackingValid, Is.True);
            Assert.That(ray.origin, Is.EqualTo(expectedRay.origin).Using(Vector3Comparer.Instance));
            Assert.That(ray.direction, Is.EqualTo(expectedRay.direction).Using(Vector3Comparer.Instance));
        }

        [Test]
        public void Raycaster_TargetTransition_InvokesEnterStayAndExit()
        {
            root = new GameObject("GazeTestRoot");
            TestGazeProvider provider = new TestGazeProvider();
            GazeRaycaster raycaster = root.AddComponent<GazeRaycaster>();
            raycaster.SetProvider(provider);

            GameObject targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetObject.name = "Target";
            targetObject.transform.SetParent(root.transform);
            targetObject.transform.position = Vector3.forward * 3f;
            GazeTarget target = targetObject.AddComponent<GazeTarget>();

            Physics.SyncTransforms();
            raycaster.Tick(0.25f);

            Assert.That(raycaster.CurrentTarget, Is.SameAs(target));
            Assert.That(target.IsGazedAt, Is.True);
            Assert.That(target.GazeDuration, Is.EqualTo(0.25f).Within(0.001f));

            provider.Direction = Vector3.up;
            raycaster.Tick(0.1f);

            Assert.That(raycaster.CurrentTarget, Is.Null);
            Assert.That(target.IsGazedAt, Is.False);
            Assert.That(target.GazeDuration, Is.Zero);
        }

        [Test]
        public void Raycaster_InvalidTracking_ClearsCurrentTarget()
        {
            root = new GameObject("GazeTestRoot");
            TestGazeProvider provider = new TestGazeProvider();
            GazeRaycaster raycaster = root.AddComponent<GazeRaycaster>();
            raycaster.SetProvider(provider);

            GameObject targetObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            targetObject.transform.SetParent(root.transform);
            targetObject.transform.position = Vector3.forward * 2f;
            GazeTarget target = targetObject.AddComponent<GazeTarget>();
            Physics.SyncTransforms();

            raycaster.Tick(0.1f);
            provider.Valid = false;
            raycaster.Tick(0.1f);

            Assert.That(raycaster.HasHit, Is.False);
            Assert.That(raycaster.CurrentTarget, Is.Null);
            Assert.That(target.IsGazedAt, Is.False);
        }

        [Test]
        public void CalibrationPoint_RequiresConfiguredDwellAndCompletesOnce()
        {
            root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            CalibrationPoint point = root.AddComponent<CalibrationPoint>();
            point.DwellDuration = 0.6f;
            int completedCount = 0;
            point.Completed += _ => completedCount++;
            point.Activate();

            point.Advance(0.3f);
            Assert.That(point.Progress, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(point.IsComplete, Is.False);

            point.Advance(0.3f);
            point.Advance(1f);
            Assert.That(point.IsComplete, Is.True);
            Assert.That(completedCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CalibrationSequence_ActivatesPointsInOrderAndCompletes()
        {
            root = new GameObject("CalibrationTestRoot");
            CalibrationSequenceController sequence = root.AddComponent<CalibrationSequenceController>();
            sequence.PointSuccessDelay = 0f;
            sequence.CompletionDisplayDuration = 0f;

            CalibrationPoint first = CreateCalibrationPoint("First");
            CalibrationPoint second = CreateCalibrationPoint("Second");
            sequence.Configure(new[] { first, second }, new TestGazeProvider());
            sequence.BeginCalibration();

            Assert.That(sequence.CurrentPointIndex, Is.EqualTo(0));
            first.Advance(1f);
            yield return null;
            Assert.That(sequence.CurrentPointIndex, Is.EqualTo(1));

            second.Advance(1f);
            yield return null;
            Assert.That(sequence.IsComplete, Is.True);
            Assert.That(sequence.StatusMessage, Is.EqualTo("준비됐어요! 이제 시작해 볼까요?"));
        }

        private CalibrationPoint CreateCalibrationPoint(string name)
        {
            GameObject pointObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pointObject.name = name;
            pointObject.transform.SetParent(root.transform);
            return pointObject.AddComponent<CalibrationPoint>();
        }

        private sealed class TestGazeProvider : IGazeProvider
        {
            public bool Valid = true;
            public Vector3 Origin;
            public Vector3 Direction = Vector3.forward;
            public bool IsTrackingValid => Valid;
            public Ray GetGazeRay() => new Ray(Origin, Direction.normalized);
            public Vector3 GetGazeOrigin() => Origin;
            public Vector3 GetGazeDirection() => Direction.normalized;
        }

        private sealed class Vector3Comparer : IEqualityComparer<Vector3>
        {
            public static readonly Vector3Comparer Instance = new Vector3Comparer();
            public bool Equals(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < 0.0001f;
            public int GetHashCode(Vector3 value) => value.GetHashCode();
        }
    }
}
