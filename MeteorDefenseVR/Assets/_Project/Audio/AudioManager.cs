using System;
using System.Collections.Generic;
using UnityEngine;

namespace MeteorDefenseVR.Audio
{
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour
    {
        [SerializeField] private AudioCueLibrary cueLibrary;
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource uiSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource voiceSource;
        [SerializeField] private AudioSource ambientSource;
        [SerializeField, Range(0f, 0.7f)] private float masterVolume = 0.65f;

        private readonly Dictionary<AudioCue, AudioCueEntry> entries = new Dictionary<AudioCue, AudioCueEntry>();
        private readonly Dictionary<AudioCue, double> lastPlayedAt = new Dictionary<AudioCue, double>();

        public event Action<AudioCue, AudioCategory> CuePlaybackRequested;

        private void Awake() => RebuildLookup();

        public void Configure(
            AudioCueLibrary library,
            AudioSource bgm,
            AudioSource ui,
            AudioSource sfx,
            AudioSource voice,
            AudioSource ambient)
        {
            cueLibrary = library;
            bgmSource = bgm;
            uiSource = ui;
            sfxSource = sfx;
            voiceSource = voice;
            ambientSource = ambient;
            ConfigureSource(bgmSource);
            ConfigureSource(uiSource);
            ConfigureSource(sfxSource);
            ConfigureSource(voiceSource);
            ConfigureSource(ambientSource);
            RebuildLookup();
        }

        public bool TryPlay(AudioCue cue) => TryPlay(cue, AudioSettings.dspTime);

        public bool TryPlay(AudioCue cue, double timestamp)
        {
            if (!entries.TryGetValue(cue, out AudioCueEntry entry)) return false;
            const double timingTolerance = 0.000001d;
            if (lastPlayedAt.TryGetValue(cue, out double previous) && timestamp - previous + timingTolerance < entry.Cooldown)
                return false;

            lastPlayedAt[cue] = timestamp;
            CuePlaybackRequested?.Invoke(cue, entry.Category);
            AudioSource source = GetSource(entry.Category);
            if (source == null || entry.Clip == null) return true;

            float volume = Mathf.Clamp01(masterVolume) * entry.Volume;
            if (entry.Loop)
            {
                if (source.clip == entry.Clip && source.isPlaying) return true;
                source.Stop();
                source.clip = entry.Clip;
                source.loop = true;
                source.volume = volume;
                source.Play();
            }
            else
            {
                source.loop = false;
                source.PlayOneShot(entry.Clip, volume);
            }
            return true;
        }

        public void StopAll()
        {
            StopSource(bgmSource);
            StopSource(uiSource);
            StopSource(sfxSource);
            StopSource(voiceSource);
            StopSource(ambientSource);
            lastPlayedAt.Clear();
        }

        public void SetMasterVolume(float value) => masterVolume = Mathf.Clamp(value, 0f, 0.7f);

        private void RebuildLookup()
        {
            entries.Clear();
            if (cueLibrary == null) return;
            foreach (AudioCueEntry entry in cueLibrary.Entries)
                if (entry != null) entries[entry.Cue] = entry;
        }

        private AudioSource GetSource(AudioCategory category)
        {
            return category switch
            {
                AudioCategory.Bgm => bgmSource,
                AudioCategory.UI => uiSource,
                AudioCategory.Sfx => sfxSource,
                AudioCategory.Voice => voiceSource,
                AudioCategory.Ambient => ambientSource,
                _ => sfxSource
            };
        }

        private static void ConfigureSource(AudioSource source)
        {
            if (source == null) return;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = 1f;
        }

        private static void StopSource(AudioSource source)
        {
            if (source == null) return;
            source.Stop();
            source.clip = null;
            source.loop = false;
        }
    }
}
