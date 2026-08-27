using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.Meteor;
using NUnit.Framework;
using UnityEngine;

namespace MeteorDefenseVR.Tests
{
    public sealed class VrUxComfortControllerTests
    {
        private GameObject root;
        private VrUxSettings settings;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("VrUxComfortTests");
            settings = ScriptableObject.CreateInstance<VrUxSettings>();
            settings.Configure(0.15f, 0.62f, 0.78f, 32f, 20f, 0.032f);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (settings != null) Object.DestroyImmediate(settings);
        }

        [Test]
        public void ApplySettings_IncreasesGazeToleranceAndKeepsMeteorsInComfortView()
        {
            Camera camera = root.AddComponent<Camera>();
            GazeRaycaster raycaster = root.AddComponent<GazeRaycaster>();
            MeteorSpawner spawner = root.AddComponent<MeteorSpawner>();
            VrUxComfortController comfort = root.AddComponent<VrUxComfortController>();

            comfort.Configure(settings, camera, raycaster, spawner, null);

            Assert.That(comfort.IsApplied, Is.True);
            Assert.That(raycaster.GazeMagnetismRadius, Is.EqualTo(0.15f).Within(0.001f));
            Assert.That(spawner.MaxHorizontalAngle, Is.EqualTo(32f).Within(0.001f));
            Assert.That(spawner.MaxVerticalAngle, Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void ApplySettings_DoesNotMoveVrCamera()
        {
            Camera camera = root.AddComponent<Camera>();
            camera.transform.position = new Vector3(1f, 2f, 3f);
            VrUxComfortController comfort = root.AddComponent<VrUxComfortController>();

            comfort.Configure(settings, camera, null, null, null);

            Assert.That(camera.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(camera.nearClipPlane, Is.LessThanOrEqualTo(0.05f));
        }

        [Test]
        public void ReadableText_EnforcesMinimumWorldSpaceSize()
        {
            TextMesh small = root.AddComponent<TextMesh>();
            small.characterSize = 0.01f;

            VrUxComfortController.EnforceReadableTextSize(new[] { small }, settings.MinimumTextSize);

            Assert.That(small.characterSize, Is.EqualTo(0.032f).Within(0.001f));
        }

        [Test]
        public void Settings_KeepChildFriendlyLockTimes()
        {
            Assert.That(settings.StandardLockDuration, Is.InRange(0.45f, 0.7f));
            Assert.That(settings.BossLockDuration, Is.LessThanOrEqualTo(0.8f));
        }
    }
}
