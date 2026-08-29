using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Reflection;
using MeteorDefenseVR.Audio;
using MeteorDefenseVR.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.Tests
{
    public sealed class BgmIntegrationTests
    {
        private GameObject root;
        private AudioCueLibrary library;
        private AudioManager manager;
        private AudioSource[] sources;
        private readonly List<AudioClip> clips = new List<AudioClip>();

        [SetUp]
        public void Setup()
        {
            root = new GameObject("BgmUnitFixture");
            manager = root.AddComponent<AudioManager>();
            sources = Enumerable.Range(0, 5).Select(_ => root.AddComponent<AudioSource>()).ToArray();
            library = ScriptableObject.CreateInstance<AudioCueLibrary>();
            var entries = new List<AudioCueEntry>();
            foreach (AudioCue cue in new[] { AudioCue.BgmIntro, AudioCue.BgmTutorial, AudioCue.BgmBattle, AudioCue.BgmBoss, AudioCue.BgmResult })
            {
                var clip = AudioClip.Create(cue.ToString(), 44100, 1, 44100, false);
                clips.Add(clip);
                entries.Add(new AudioCueEntry(cue, AudioCategory.Bgm, .7f, 0, cue != AudioCue.BgmResult, clip));
            }
            library.Configure(entries.ToArray());
            manager.Configure(library, sources[0], sources[1], sources[2], sources[3], sources[4]);
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(library);
            foreach (var clip in clips) Object.DestroyImmediate(clip);
            clips.Clear();
        }

        [TestCase(GameState.Boot, null)]
        [TestCase(GameState.Reset, null)]
        [TestCase(GameState.Intro, AudioCue.BgmIntro)]
        [TestCase(GameState.MissionBriefing, AudioCue.BgmIntro)]
        [TestCase(GameState.EyeCalibration, AudioCue.BgmTutorial)]
        [TestCase(GameState.Tutorial, AudioCue.BgmTutorial)]
        [TestCase(GameState.Practice, AudioCue.BgmTutorial)]
        [TestCase(GameState.Launch, AudioCue.BgmBattle)]
        [TestCase(GameState.Countdown, AudioCue.BgmBattle)]
        [TestCase(GameState.Playing, AudioCue.BgmBattle)]
        [TestCase(GameState.BossMeteor, AudioCue.BgmBoss)]
        [TestCase(GameState.MissionComplete, AudioCue.BgmResult)]
        [TestCase(GameState.Result, AudioCue.BgmResult)]
        public void MapsExistingStatesWithoutChangingFlow(GameState state, AudioCue? expected) =>
            Assert.That(GameAudioEventRouter.BgmForState(state), Is.EqualTo(expected));

        [Test]
        public void RepeatedSameTrackRequests_DoNotRestartOrExtendFade()
        {
            manager.RequestBgm(AudioCue.BgmBattle);
            Step(.5f);
            float volume = sources[0].volume;
            for (int i = 0; i < 30; i++) Assert.That(manager.RequestBgm(AudioCue.BgmBattle), Is.True);
            Assert.That(sources[0].volume, Is.EqualTo(volume));
            Step(.5f);
            Assert.That(manager.IsBgmTransitioning, Is.False);
            Assert.That(manager.BgmPlaybackStarts, Is.EqualTo(1));
            Assert.That(sources[0].loop, Is.True);
        }

        [Test]
        public void TransitionUsesOneSource_AndLatestRequestWins()
        {
            manager.RequestBgm(AudioCue.BgmIntro); Step(1);
            manager.RequestBgm(AudioCue.BgmTutorial); Step(.2f);
            manager.RequestBgm(AudioCue.BgmBattle); Step(1);
            Assert.That(manager.CurrentBgmClip, Is.SameAs(clips[2]));
            Assert.That(manager.BgmPlaybackStarts, Is.EqualTo(2), "Superseded Tutorial must never start");
            Assert.That(root.GetComponents<AudioSource>().Length, Is.EqualTo(5));
            Assert.That(sources.Skip(1).All(s => s.clip == null), Is.True);
            manager.RequestBgm(AudioCue.BgmBoss); Step(.4f);
            Assert.That(manager.CurrentBgmClip, Is.SameAs(clips[3]));
            Assert.That(manager.IsBgmTransitioning, Is.False);
        }

        [Test]
        public void ThirtyResets_ClearSourceAndState_AndRestartFresh()
        {
            for (int i = 0; i < 30; i++)
            {
                manager.RequestBgm(AudioCue.BgmIntro); Step(1);
                manager.ResetForNewSession(); Step(.4f);
                Assert.That(sources[0].volume, Is.GreaterThan(0), "Reset should fade, not abruptly stop");
                manager.ResetForNewSession(); Step(.6f);
                Assert.That(manager.CurrentBgmClip, Is.Null);
                Assert.That(manager.TargetBgmCue, Is.Null);
                Assert.That(manager.IsBgmTransitioning, Is.False);
                Assert.That(sources[0].isPlaying || sources[0].loop, Is.False);
            }
            Assert.That(manager.BgmPlaybackStarts, Is.EqualTo(30));
            Assert.That(root.GetComponents<AudioSource>().Length, Is.EqualTo(5));
        }

        [Test]
        public void RestartDuringResetFade_DoesNotResumePreviousSession()
        {
            manager.RequestBgm(AudioCue.BgmIntro); Step(1);
            manager.ResetForNewSession(); Step(.2f);
            manager.RequestBgm(AudioCue.BgmIntro); Step(1);
            Assert.That(manager.BgmPlaybackStarts, Is.EqualTo(2));
            Assert.That(manager.TargetBgmCue, Is.EqualTo(AudioCue.BgmIntro));
        }

        [Test]
        public void FinishedResultOneShot_DoesNotRestartOnResultState()
        {
            manager.RequestBgm(AudioCue.BgmResult); Step(1);
            sources[0].Stop(); // Simulate a one-shot reaching its end before the state changes.
            manager.RequestBgm(AudioCue.BgmResult); Step(1);
            Assert.That(manager.BgmPlaybackStarts, Is.EqualTo(1));
            Assert.That(sources[0].loop, Is.False);
        }

        [Test]
        public void LiveMixAndMute_AreIndependent_AndRejectNonFiniteInput()
        {
            manager.RequestBgm(AudioCue.BgmBattle); Step(1);
            manager.SetMasterVolume(.6f); manager.SetBgmVolume(.5f);
            manager.SetSfxVolume(.9f); manager.SetVoiceVolume(1f);
            Assert.That(sources[0].volume, Is.EqualTo(.21f).Within(.00001f));
            Assert.That(sources[2].volume, Is.EqualTo(.54f).Within(.00001f));
            Assert.That(sources[3].volume, Is.EqualTo(.6f).Within(.00001f));
            manager.SetBgmMute(true);
            Assert.That(sources[0].mute, Is.True);
            Assert.That(sources[2].mute, Is.False);
            manager.SetBgmMute(false);
            Assert.That(manager.BgmPlaybackStarts, Is.EqualTo(1));
            manager.SetBgmVolume(float.NaN); manager.SetVoiceVolume(float.PositiveInfinity);
            Assert.That(sources[0].volume + sources[3].volume, Is.Zero);
        }

        [Test]
        public void DisableAndReconfigure_CancelPendingPlayback()
        {
            manager.RequestBgm(AudioCue.BgmBattle); Step(1);
            manager.RequestBgm(AudioCue.BgmBoss);
            typeof(AudioManager).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null); Step(2);
            Assert.That(manager.CurrentBgmClip, Is.Null);
            Assert.That(manager.TargetBgmCue, Is.Null);
            manager.Configure(library, sources[0], sources[1], sources[2], sources[3], sources[4]);
            Assert.That(manager.RequestBgm(AudioCue.BgmIntro), Is.True);
            Assert.That(manager.BgmPlaybackStarts, Is.EqualTo(2));
            Assert.That(manager.RequestBgm(AudioCue.Laser), Is.False);
        }

        [Test]
        public void RealLibrary_HasAllFiveMp3s_AndVictoryStingIsNotBgm()
        {
            var actual = AssetDatabase.LoadAssetAtPath<AudioCueLibrary>("Assets/_Project/Audio/AudioCueLibrary.asset");
            foreach (string name in new[] { "Intro", "Tutorial", "Battle", "Boss", "Result" })
            {
                var entry = actual.Entries.Single(e => e.Cue == Enum.Parse<AudioCue>("Bgm" + name));
                Assert.That(entry.Category, Is.EqualTo(AudioCategory.Bgm));
                Assert.That(AssetDatabase.GetAssetPath(entry.Clip), Is.EqualTo("Assets/_Project/Audio/BGM/" + name + ".mp3"));
                Assert.That(entry.Loop, Is.EqualTo(name != "Result"));
                Assert.That(entry.Clip.LoadAudioData(), Is.True);
                var pcm = new float[4096];
                Assert.That(entry.Clip.GetData(pcm, Mathf.Min(entry.Clip.samples / 2, 44100 * 10)), Is.True);
                double rms = Math.Sqrt(pcm.Average(v => (double)v * v));
                Assert.That(rms, Is.GreaterThan(0), "Supplied track contains audio");
                TestContext.WriteLine($"BGM_ASSET {name}: length={entry.Clip.length:F3}s channels={entry.Clip.channels} rate={entry.Clip.frequency} load={entry.Clip.loadType} sampleRms={rms:F5} samplePeak={pcm.Max(v => Mathf.Abs(v)):F5}");
            }
            Assert.That(actual.Entries.Single(e => e.Cue == AudioCue.MissionComplete).Category, Is.EqualTo(AudioCategory.Sfx));
        }

        private void Step(float seconds)
        {
            typeof(AudioManager).GetMethod("TickBgm", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, new object[] { seconds });
            manager.SetBgmVolume(manager.BgmVolume);
        }
    }

    public sealed class BgmSceneLifecycleTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest, Timeout(90000)]
        public IEnumerator SceneReloadAndPausedReset_DoNotLeaveOldMusic()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return CheckReloads();
            yield return new ExitPlayMode();
        }

        private static IEnumerator CheckReloads()
        {
            for (int round = 0; round < 3; round++)
            {
                yield return null;
                var audio = Object.FindAnyObjectByType<AudioManager>();
                Assert.That(Object.FindObjectsByType<AudioManager>().Length, Is.EqualTo(1));
                Assert.That(audio.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(5));
                audio.RequestBgm(AudioCue.BgmBattle);
                yield return new WaitForSecondsRealtime(1.1f);
                var oldSource = audio.GetComponentsInChildren<AudioSource>().Single(audio.OwnsBgmSource);
                Assert.That(oldSource.isPlaying, Is.True);
                Time.timeScale = 0f;
                audio.ResetForNewSession();
                yield return new WaitForSecondsRealtime(1.1f);
                Assert.That(oldSource.isPlaying, Is.False);
                Assert.That(oldSource.clip, Is.Null);
                Time.timeScale = 1f;
                // Unload while actively playing, rather than only checking an already silent scene.
                audio.RequestBgm(AudioCue.BgmBoss);
                yield return new WaitForSecondsRealtime(.5f);
                var oldSceneHandle = oldSource.gameObject.scene.handle;
                // Exercise the same runtime scene loader used by the build, not the editor asset-bundle emulation API.
                AsyncOperation reload = SceneManager.LoadSceneAsync("MeteorDefense", LoadSceneMode.Single);
                Assert.That(reload, Is.Not.Null);
                float deadline = Time.realtimeSinceStartup + 20f;
                while (!reload.isDone && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(reload.isDone, Is.True, "Scene reload must complete before testing teardown");
                yield return null;
                TestContext.WriteLine($"BGM_RELOAD round={round + 1} oldSourceDestroyed={oldSource == null} oldScene={oldSceneHandle} managers={Object.FindObjectsByType<AudioManager>().Length} activeScene={SceneManager.GetActiveScene().handle}");
                Assert.That(oldSource == null, Is.True, "Scene-owned source must be destroyed, not persisted; old scene " + oldSceneHandle);
                var replacement = Object.FindAnyObjectByType<AudioManager>();
                Assert.That(Object.FindObjectsByType<AudioManager>().Length, Is.EqualTo(1));
                Assert.That(replacement.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(5));
                Assert.That(replacement.CurrentBgmClip, Is.Null);
            }
        }
    }

    // Attached only to the existing natural mouse-gaze regression, never to player/UI objects.
    internal sealed class BgmNaturalFlowProbe : IDisposable
    {
        private readonly AudioManager manager = Object.FindAnyObjectByType<AudioManager>();
        private readonly List<AudioCue> started = new List<AudioCue>();
        private readonly HashSet<AudioCue> heard = new HashSet<AudioCue>();
        private readonly float[] output = new float[256];
        private readonly int sourceCount;
        private float mixerPeak;
        private double dspStart;

        public BgmNaturalFlowProbe()
        {
            Assert.That(manager, Is.Not.Null);
            sourceCount = manager.GetComponentsInChildren<AudioSource>(true).Length;
            Assert.That(sourceCount, Is.EqualTo(5));
            Assert.That(manager.CurrentBgmClip, Is.Null, "Ready starts without BGM");
            manager.CuePlaybackRequested += OnCue;
            dspStart = AudioSettings.dspTime;
        }

        private void OnCue(AudioCue cue, AudioCategory category)
        {
            if (category != AudioCategory.Bgm) return;
            started.Add(cue);
            TestContext.WriteLine($"BGM_PLAY {cue} realTime={Time.realtimeSinceStartup:F3}s");
        }

        public void Sample(GameState state)
        {
            Assert.That(manager.TargetBgmCue, Is.EqualTo(GameAudioEventRouter.BgmForState(state)), "Audio/state mapping: " + state);
            Assert.That(manager.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(sourceCount));
            var source = manager.GetComponentsInChildren<AudioSource>(true).Single(manager.OwnsBgmSource);
            if (manager.TargetBgmCue.HasValue && !manager.IsBgmTransitioning)
            {
                Assert.That(source.isPlaying, Is.True, "Unexpected silence in " + state);
                Assert.That(source.volume, Is.GreaterThan(0));
                Assert.That(source.clip.name, Is.EqualTo(manager.TargetBgmCue.Value.ToString().Substring(3)));
                if (source.timeSamples > 0) heard.Add(manager.TargetBgmCue.Value);
            }
            AudioListener.GetOutputData(output, 0);
            foreach (float sample in output) mixerPeak = Mathf.Max(mixerPeak, Mathf.Abs(sample));
        }

        public void Complete()
        {
            Assert.That(started, Is.EqualTo(new[] { AudioCue.BgmIntro, AudioCue.BgmTutorial, AudioCue.BgmBattle, AudioCue.BgmBoss, AudioCue.BgmResult }));
            Assert.That(heard.Count, Is.EqualTo(5), "Every selected source must advance its PCM playback position");
            Assert.That(manager.CurrentBgmClip, Is.Null, "Reset fade must clear the clip");
            Assert.That(manager.TargetBgmCue, Is.Null);
            var ambient = (AudioSource)typeof(AudioManager).GetField("ambientSource", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            var remaining = manager.GetComponentsInChildren<AudioSource>(true).Where(s => s != ambient && s.isPlaying).ToArray();
            Assert.That(remaining.Length, Is.Zero, "No previous-session audio may remain: " + string.Join(", ",remaining.Select(s=>s.name+"/"+(s.clip!=null?s.clip.name:"one-shot"))));
            if (ambient != null && ambient.isPlaying)
            {
                Assert.That(ambient.clip.name, Is.EqualTo("IntroSpaceAmbient"), "Only the requested Ready ambience is allowed");
                Assert.That(ambient.loop, Is.True);
            }
            TestContext.WriteLine($"BGM_NATURAL_FLOW starts={started.Count} advancedTracks={heard.Count} audioSources={sourceCount} mixerPeak={mixerPeak:F6} dspElapsed={AudioSettings.dspTime - dspStart:F3}s resetBgmSfxVoiceSilent=true readyAmbientAllowed=true");
        }

        public void Dispose() { if (manager != null) manager.CuePlaybackRequested -= OnCue; }
    }
}
