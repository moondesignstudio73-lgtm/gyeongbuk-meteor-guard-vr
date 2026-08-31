using MeteorDefenseVR.Combat;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.Meteor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.Tests
{
    public sealed class TargetReleaseRegressionTests
    {
        private GameObject fixture;

        [SetUp]
        public void SetUp() => fixture = new GameObject("TargetReleaseFixture");

        [TearDown]
        public void TearDown()
        {
            foreach (GazeLockOnTarget target in Object.FindObjectsByType<GazeLockOnTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                target.ForceRelease();
            if (fixture != null) Object.DestroyImmediate(fixture);
        }

        [Test]
        public void LockedMeteorDestructionImmediatelyClearsEveryTargetPresentation()
        {
            MeteorController meteor = Meteor("LockedMeteor", Vector3.forward * 3f, 1f, false);
            GazeLockOnTarget target = LockTarget(meteor, out LineRenderer ring, out TextMesh text, out LineRenderer bracket);
            GazeRaycaster gaze = Raycaster();
            TurretAimingSystem turrets = Turrets();

            target.Advance(1f);
            Physics.SyncTransforms(); gaze.Tick(.02f); turrets.Tick(.02f);
            Assert.That(target.IsLocked, Is.True);
            Assert.That(gaze.CurrentTarget, Is.SameAs(target));
            Assert.That(turrets.CurrentCandidate, Is.SameAs(target));
            // Reproduce the reported stale presentation explicitly. EditMode does not drive
            // the renderer lifecycle exactly like a running scene, but the target event path
            // must still clear every visible element synchronously.
            ring.enabled = true;
            text.gameObject.SetActive(true);
            bracket.enabled = true;

            meteor.ReceiveDamage(1f);

            Assert.That(meteor.State, Is.EqualTo(MeteorLifecycleState.Destroyed));
            Assert.That(meteor.gameObject.activeSelf, Is.True, "Explosion hold keeps the defeated mesh object alive for this regression case");
            Assert.That(target.State, Is.EqualTo(LockOnState.Destroyed));
            Assert.That(target.Progress, Is.Zero);
            Assert.That(GazeLockOnTarget.ActiveTarget, Is.Null);
            Assert.That(gaze.CurrentTarget, Is.Null);
            Assert.That(turrets.CurrentCandidate, Is.Null);
            Assert.That(turrets.SelectedTurretIndex, Is.EqualTo(-1));
            Assert.That(ring.enabled, Is.False);
            Assert.That(ring.positionCount, Is.Zero, "Released green lock geometry must not remain resident for a later frame");
            Assert.That(text.gameObject.activeSelf, Is.False);
            Assert.That(bracket.enabled, Is.False);
        }

        [Test]
        public void ResetWhileLockedClearsPoolStateBeforeReuse()
        {
            MeteorController meteor = Meteor("PooledMeteor", Vector3.forward * 3f, 2f, false);
            GazeLockOnTarget target = LockTarget(meteor, out LineRenderer ring, out TextMesh text, out LineRenderer bracket);
            GazeRaycaster gaze = Raycaster();
            target.Advance(1f); Physics.SyncTransforms(); gaze.Tick(.02f);

            meteor.ResetMeteor(false);

            Assert.That(meteor.State, Is.EqualTo(MeteorLifecycleState.Inactive));
            Assert.That(target.State, Is.EqualTo(LockOnState.Idle));
            Assert.That(target.Progress, Is.Zero);
            Assert.That(gaze.CurrentTarget, Is.Null);
            Assert.That(ring.enabled || text.gameObject.activeSelf || bracket.enabled, Is.False);
            meteor.Spawn();
            Assert.That(target.State, Is.EqualTo(LockOnState.Idle));
            Assert.That(target.Progress, Is.Zero);
        }

        [Test]
        public void LaserKillThenImmediateCandidateChangeDoesNotRestoreDestroyedTarget()
        {
            MeteorController firstMeteor = Meteor("LaserTarget", Vector3.forward * 3f, 1f, false);
            GazeLockOnTarget first = LockTarget(firstMeteor, out _, out _, out _);
            MeteorController secondMeteor = Meteor("NextTarget", new Vector3(2, 0, 5), 2f, false);
            GazeLockOnTarget second = LockTarget(secondMeteor, out _, out _, out _);
            GazeRaycaster gaze = Raycaster();
            var weaponObject = new GameObject("LaserWeapon"); weaponObject.transform.SetParent(fixture.transform);
            LaserWeapon weapon = weaponObject.AddComponent<LaserWeapon>(); weapon.Configure(gaze, new[] { weaponObject.transform }, System.Array.Empty<LaserBeamView>());

            first.Advance(1f);
            Assert.That(weapon.TryFire(first), Is.True);
            second.OnGazeEnter(default);

            Assert.That(first.State, Is.EqualTo(LockOnState.Destroyed));
            Assert.That(first.Progress, Is.Zero);
            Assert.That(GazeLockOnTarget.ActiveTarget, Is.SameAs(second));
            Assert.That(second.State, Is.EqualTo(LockOnState.Focusing));
        }

        private MeteorController Meteor(string name, Vector3 position, float health, bool disableOnComplete)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = name; go.transform.SetParent(fixture.transform); go.transform.position = position;
            MeteorController meteor = go.AddComponent<MeteorController>(); meteor.Configure(MeteorType.Normal, health, 0, 10, 100, 1);
            var serialized = new SerializedObject(meteor); serialized.FindProperty("disableOnComplete").boolValue = disableOnComplete; serialized.ApplyModifiedPropertiesWithoutUndo();
            meteor.Spawn(); return meteor;
        }

        private static GazeLockOnTarget LockTarget(MeteorController meteor, out LineRenderer ring, out TextMesh text, out LineRenderer bracket)
        {
            GazeLockOnTarget target = meteor.gameObject.AddComponent<GazeLockOnTarget>(); target.Configure(meteor, .1f, 1f, 0f);
            var reticle = new GameObject("Reticle"); reticle.SetActive(false); reticle.transform.SetParent(meteor.transform, false);
            ring = reticle.AddComponent<LineRenderer>();
            text = new GameObject("LockText").AddComponent<TextMesh>(); text.transform.SetParent(reticle.transform, false);
            bracket = new GameObject("Bracket").AddComponent<LineRenderer>(); bracket.transform.SetParent(reticle.transform, false);
            LockOnReticleView view = reticle.AddComponent<LockOnReticleView>(); view.Configure(target, ring, text, new Renderer[] { bracket });
            reticle.SetActive(true);
            view.Configure(target, ring, text, new Renderer[] { bracket });
            return target;
        }

        private GazeRaycaster Raycaster()
        {
            GameObject go = new GameObject("GazeRaycaster"); go.transform.SetParent(fixture.transform);
            GazeRaycaster raycaster = go.AddComponent<GazeRaycaster>(); raycaster.SetProvider(new ForwardProvider()); return raycaster;
        }

        private TurretAimingSystem Turrets()
        {
            GameObject go = new GameObject("Turrets"); go.transform.SetParent(fixture.transform);
            TurretAimingSystem aiming = go.AddComponent<TurretAimingSystem>();
            aiming.Configure(null, null, go.transform, Mount(go.transform, "Top", Vector3.up), Mount(go.transform, "Bottom", Vector3.down), null, null);
            return aiming;
        }

        private static TurretAimingSystem.TurretMount Mount(Transform parent, string name, Vector3 position)
        {
            Transform root = new GameObject(name).transform; root.SetParent(parent); root.localPosition = position;
            Transform yaw = new GameObject("Yaw").transform; yaw.SetParent(root, false);
            Transform pitch = new GameObject("Pitch").transform; pitch.SetParent(yaw, false);
            Transform muzzle = new GameObject("Muzzle").transform; muzzle.SetParent(pitch, false); muzzle.localPosition = Vector3.forward;
            return new TurretAimingSystem.TurretMount { label = name, root = root, yawPivot = yaw, pitchPivot = pitch, muzzle = muzzle };
        }

        private sealed class ForwardProvider : IGazeProvider
        {
            public bool IsTrackingValid => true;
            public Ray GetGazeRay() => new Ray(Vector3.zero, Vector3.forward);
            public Vector3 GetGazeOrigin() => Vector3.zero;
            public Vector3 GetGazeDirection() => Vector3.forward;
        }
    }
}
