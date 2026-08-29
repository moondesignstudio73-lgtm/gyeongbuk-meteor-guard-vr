using MeteorDefenseVR.Combat;
using MeteorDefenseVR.EyeTracking;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
using System.Collections;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;

namespace MeteorDefenseVR.Tests
{
    public sealed class GazeLifecycleAuditTests
    {
        private GameObject root;
        [SetUp] public void Setup() => root = new GameObject("GazeLifecycleAudit");
        [TearDown] public void Cleanup() { if (root != null) Object.DestroyImmediate(root); }
        [UnityTearDown] public IEnumerator ExitPlay() { if (Application.isPlaying) yield return new ExitPlayMode(); }
        private GameObject Target()
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            target.transform.SetParent(root.transform); target.transform.position = Vector3.forward * 3;
            return target;
        }
        private GazeRaycaster Raycaster()
        {
            var raycaster = root.AddComponent<GazeRaycaster>(); raycaster.SetProvider(new Provider()); return raycaster;
        }
        [UnityTest] public IEnumerator DisabledLockTargetClearsProgressAndRejectsFocus()
        {
            Object.DestroyImmediate(root);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            root = new GameObject("GazeLifecyclePlayAudit");
            var target = Target().AddComponent<GazeLockOnTarget>(); target.Advance(.3f);
            target.enabled = false;
            Assert.That(target.Progress, Is.Zero); Assert.That(target.IsAvailable, Is.False);
            target.Advance(10); Assert.That(target.IsLocked, Is.False);
            target.enabled = true; Assert.That(target.Progress, Is.Zero);
        }
        [Test] public void RaycasterDoesNotSelectDisabledTargetComponent()
        {
            var raycaster = Raycaster(); var target = Target().AddComponent<GazeLockOnTarget>(); target.enabled = false;
            Physics.SyncTransforms(); raycaster.Tick(.1f); Assert.That(raycaster.CurrentTarget, Is.Null);
            target.enabled = true; raycaster.Tick(.1f); Assert.That(raycaster.CurrentTarget, Is.SameAs(target));
        }
        [Test] public void DestroyedCalibrationTargetIsNotCalledThroughInterface()
        {
            var raycaster = Raycaster(); var target = Target().AddComponent<CalibrationPoint>(); target.Activate();
            Physics.SyncTransforms(); raycaster.Tick(.1f); Assert.That(raycaster.CurrentTarget, Is.SameAs(target));
            Object.DestroyImmediate(target.gameObject); Physics.SyncTransforms();
            Assert.DoesNotThrow(() => raycaster.Tick(.1f)); Assert.That(raycaster.CurrentTarget, Is.Null);
        }
        [Test] public void InvalidCalibrationDwellCannotCompleteAutomatically()
        {
            var target = Target().AddComponent<CalibrationPoint>(); target.DwellDuration = float.NaN; target.Activate();
            target.Advance(float.NaN); target.Advance(float.PositiveInfinity);
            Assert.That(target.IsComplete, Is.False); Assert.That(float.IsNaN(target.Progress), Is.False);
            target.Advance(target.DwellDuration); Assert.That(target.IsComplete, Is.True);
        }
        private sealed class Provider : IGazeProvider
        {
            public bool IsTrackingValid => true;
            public Ray GetGazeRay() => new Ray(Vector3.zero, Vector3.forward);
            public Vector3 GetGazeOrigin() => Vector3.zero;
            public Vector3 GetGazeDirection() => Vector3.forward;
        }
    }
}
