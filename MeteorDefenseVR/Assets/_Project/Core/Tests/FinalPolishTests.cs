using MeteorDefenseVR.Combat;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using UnityEngine;

namespace MeteorDefenseVR.Tests
{
    public sealed class FinalPolishTests
    {
        [Test]
        public void PublicIsDefaultAndKeepsEmergencyControls()
        {
            var settings = new ExperienceSettings();
            Assert.That(settings.experienceMode, Is.EqualTo(ExperienceMode.PublicExperience));
            Assert.That(settings.operatorControls, Is.True);
            Assert.That(settings.gameDuration, Is.InRange(40f, 60f));
        }
        [Test]
        public void ConfigClampsUnsafeAndNonFiniteValues()
        {
            var settings = new ExperienceSettings { lockDuration = float.NaN, gameDuration = float.PositiveInfinity, audioVolume = 99,
                experienceMode = (ExperienceMode)99, inputMode = (PcGazeMode)99 };
            settings.Validate();
            Assert.That(settings.lockDuration, Is.EqualTo(.65f));
            Assert.That(settings.gameDuration, Is.EqualTo(46f));
            Assert.That(settings.audioVolume, Is.EqualTo(.7f));
            Assert.That(settings.experienceMode, Is.EqualTo(ExperienceMode.PublicExperience));
            Assert.That(settings.inputMode, Is.EqualTo(PcGazeMode.Auto));
        }
        [TestCase(ExperienceMode.PublicExperience, false, false)]
        [TestCase(ExperienceMode.Operator, false, true)]
        [TestCase(ExperienceMode.Development, true, true)]
        public void ModesHaveSeparateVisibilityAndDebugInput(ExperienceMode mode, bool debug, bool operatorUi)
        {
            var root = new GameObject("ModeTest");
            try
            {
                var config = root.AddComponent<ExperienceConfiguration>();
                var controls = root.AddComponent<PcTestControls>();
                config.Settings.experienceMode = mode;
                controls.SetExperienceConfiguration(config);
                Assert.That(controls.ShowDebugUi, Is.EqualTo(debug));
                Assert.That(controls.DebugKeysEnabled, Is.EqualTo(debug));
                Assert.That(controls.ShowOperatorUi, Is.EqualTo(operatorUi));
                Assert.That(controls.OperatorControlsEnabled, Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test]
        public void VictoryHasSingleConciseCopyAndDelayedPresentation()
        {
            var root = new GameObject("VictoryTest");
            try
            {
                var complete = root.AddComponent<MissionCompleteController>();
                int presentations = 0;
                complete.PresentationStarted += () => presentations++;
                complete.BeginMissionComplete();
                Assert.That(complete.StatusMessage, Is.EqualTo("MISSION COMPLETE\n지구 방어 성공!"));
                Assert.That(complete.PresentationProgress, Is.Zero);
                complete.Tick(.5f);
                Assert.That(presentations, Is.Zero);
                complete.Tick(.6f); complete.Tick(.3f);
                Assert.That(presentations, Is.EqualTo(1));
                Assert.That(complete.PresentationProgress, Is.EqualTo(1f));
                complete.ResetSequence();
                Assert.That(complete.PresentationProgress, Is.Zero);
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test]
        public void ResultPresentationUsesActualSnapshotWithoutSampleNumbers()
        {
            var snapshot = new GameSessionSnapshot { Score = 1234, DestroyedMeteors = 11, Shots = 20, SuccessfulHits = 17 };
            string text = WorldTextPresentation.FormatResult(snapshot, "B");
            Assert.That(text, Does.Contain("1,234"));
            Assert.That(text, Does.Contain("11"));
            Assert.That(text, Does.Contain("85%"));
            Assert.That(text, Does.Contain(">B<"));
            Assert.That(text, Does.Not.Contain("8,500"));
        }
        [Test]
        public void HalfLockProgressDrawsHalfCircleNotQuarterCircle()
        {
            var root = new GameObject("ReticleTest");
            try
            {
                var target = root.AddComponent<GazeLockOnTarget>();
                var ring = root.AddComponent<LineRenderer>();
                var view = root.AddComponent<LockOnReticleView>();
                view.Configure(target, ring, null);
                typeof(LockOnReticleView).GetMethod("Refresh", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(view, new object[] { .5f, LockOnState.Focusing });
                Assert.That(ring.GetPosition(ring.positionCount - 1).x, Is.LessThan(-.7f));
                Assert.That(Mathf.Abs(ring.GetPosition(ring.positionCount - 1).y), Is.LessThan(.01f));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
