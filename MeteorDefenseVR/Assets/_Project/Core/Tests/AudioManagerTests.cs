using System.Collections.Generic;
using MeteorDefenseVR.Audio;
using NUnit.Framework;
using UnityEngine;

namespace MeteorDefenseVR.Tests
{
    public sealed class AudioManagerTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        [Test]
        public void TryPlay_AppliesPerCueCooldown()
        {
            AudioManager manager = CreateManager(new AudioCueEntry(AudioCue.Laser, AudioCategory.Sfx, 0.5f, 0.2f));
            int requests = 0;
            manager.CuePlaybackRequested += (_, _) => requests++;

            Assert.That(manager.TryPlay(AudioCue.Laser, 10d), Is.True);
            Assert.That(manager.TryPlay(AudioCue.Laser, 10.1d), Is.False);
            Assert.That(manager.TryPlay(AudioCue.Laser, 10.2d), Is.True);
            Assert.That(requests, Is.EqualTo(2));
        }

        [Test]
        public void DifferentCues_DoNotBlockEachOther()
        {
            AudioManager manager = CreateManager(
                new AudioCueEntry(AudioCue.Laser, AudioCategory.Sfx, 0.5f, 1f),
                new AudioCueEntry(AudioCue.MeteorHit, AudioCategory.Sfx, 0.5f, 1f));

            Assert.That(manager.TryPlay(AudioCue.Laser, 5d), Is.True);
            Assert.That(manager.TryPlay(AudioCue.MeteorHit, 5d), Is.True);
        }

        [Test]
        public void StopAll_ClearsCooldownState()
        {
            AudioManager manager = CreateManager(new AudioCueEntry(AudioCue.Warning, AudioCategory.UI, 0.5f, 3f));
            Assert.That(manager.TryPlay(AudioCue.Warning, 2d), Is.True);
            manager.StopAll();
            Assert.That(manager.TryPlay(AudioCue.Warning, 2.1d), Is.True);
        }

        [Test]
        public void MissingCue_IsRejected()
        {
            AudioManager manager = CreateManager(new AudioCueEntry(AudioCue.Result, AudioCategory.UI, 0.4f, 1f));
            Assert.That(manager.TryPlay(AudioCue.BossExplosion, 1d), Is.False);
        }

        private AudioManager CreateManager(params AudioCueEntry[] entries)
        {
            AudioCueLibrary library = ScriptableObject.CreateInstance<AudioCueLibrary>();
            library.Configure(entries);
            created.Add(library);

            GameObject root = new GameObject("AudioManagerTests");
            created.Add(root);
            AudioManager manager = root.AddComponent<AudioManager>();
            AudioSource bgm = root.AddComponent<AudioSource>();
            AudioSource ui = root.AddComponent<AudioSource>();
            AudioSource sfx = root.AddComponent<AudioSource>();
            AudioSource voice = root.AddComponent<AudioSource>();
            AudioSource ambient = root.AddComponent<AudioSource>();
            manager.Configure(library, bgm, ui, sfx, voice, ambient);
            return manager;
        }
    }
}
