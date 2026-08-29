using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Profiling;
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
        [Header("Music mix (no player-facing debug UI)")]
        [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float voiceVolume = 1f;
        [SerializeField] private bool bgmMute;
        [Tooltip("Total fade-out + fade-in time. Also used for the full Reset fade-out.")]
        [SerializeField, Min(0.05f)] private float fadeDuration = 1f;
        [SerializeField, Min(0.05f)] private float bossFadeDuration = 0.4f;

        private enum FadePhase { Idle, Out, In }
        private FadePhase fadePhase;
        private AudioCueEntry requestedBgm;
        private AudioCueEntry playingBgm;
        private float fadeGain, fadeElapsed, phaseDuration, fadeStartGain, incomingDuration;
        private float presentationGain = 1f, presentationTarget = 1f;
        public float PresentationGain => presentationGain;
        public void SetPresentationBgmGain(float gain) => presentationTarget = SafeVolume(gain);
        public void StopAmbient() => StopSource(ambientSource);
        private readonly float[] channelTrim = { 1f, 1f, 1f, 1f, 1f };

        public AudioClip CurrentBgmClip => bgmSource != null ? bgmSource.clip : null;
        public AudioCue? TargetBgmCue => requestedBgm?.Cue;
        public bool IsBgmTransitioning => fadePhase != FadePhase.Idle;
        public int BgmPlaybackStarts { get; private set; }
        public float FadeDuration => SafeDuration(fadeDuration, 1f);
        public float BossFadeDuration => SafeDuration(bossFadeDuration, 0.4f);
        public float BgmVolume => bgmVolume;
        public float SfxVolume => sfxVolume;
        public float SfxOutputGain => SafeVolume(masterVolume) * SafeVolume(sfxVolume);
        public float VoiceVolume => voiceVolume;
        public bool BgmMute => bgmMute;
        public int PreloadedClipCount { get; private set; }
        private static readonly ProfilerMarker PreloadMarker = new ProfilerMarker("MD.Prewarm.AudioClip");

        // No playback, source creation or cue events. Import settings decide whether decoding is async.
        public IEnumerator PreloadClips()
        {
            PreloadedClipCount = 0;
            var unique = new HashSet<AudioClip>();
            if (cueLibrary == null) yield break;
            foreach (AudioCueEntry entry in cueLibrary.Entries)
            {
                AudioClip clip = entry?.Clip;
                if (clip == null || !unique.Add(clip)) continue;
                if (clip.loadState == AudioDataLoadState.Unloaded)
                {
                    using (PreloadMarker.Auto()) clip.LoadAudioData();
                    yield return null;
                }
                float deadline = Time.realtimeSinceStartup + 10f;
                while (clip != null && clip.loadState == AudioDataLoadState.Loading && Time.realtimeSinceStartup < deadline)
                    yield return null;
                if (clip != null && clip.loadState == AudioDataLoadState.Loaded) PreloadedClipCount++;
                yield return null;
            }
        }

        private readonly Dictionary<AudioCue, AudioCueEntry> entries = new Dictionary<AudioCue, AudioCueEntry>();
        private readonly Dictionary<AudioCue, double> lastPlayedAt = new Dictionary<AudioCue, double>();

        public event Action<AudioCue, AudioCategory> CuePlaybackRequested;

        private void Awake()
        {
            ConfigureSources();
            RebuildLookup();
        }

        private void Update()
        {
            presentationGain = Mathf.MoveTowards(presentationGain, presentationTarget, Time.unscaledDeltaTime * 3f);
            TickBgm(Time.unscaledDeltaTime);
            ApplyMix();
        }

        private void OnDisable() => StopAll();

        public void Configure(
            AudioCueLibrary library,
            AudioSource bgm,
            AudioSource ui,
            AudioSource sfx,
            AudioSource voice,
            AudioSource ambient)
        {
            StopAll();
            cueLibrary = library;
            bgmSource = bgm;
            uiSource = ui;
            sfxSource = sfx;
            voiceSource = voice;
            ambientSource = ambient;
            ConfigureSources();
            RebuildLookup();
        }

        public bool TryPlay(AudioCue cue) => TryPlay(cue, AudioSettings.dspTime);

        // Caller owns a single prewarmed 3D source. Shares library, mix and cue cooldowns.
        public bool TryPlaySpatial(AudioCue cue, AudioSource source, Vector3 position, float gain = 1f)
        {
            if (source == null || source.isPlaying || !entries.TryGetValue(cue, out var entry) || entry.Clip == null || entry.Category == AudioCategory.Bgm) return false;
            double now = AudioSettings.dspTime;
            if (lastPlayedAt.TryGetValue(cue, out double previous) && now - previous < entry.Cooldown) return false;
            lastPlayedAt[cue] = now;
            source.transform.position = position; source.volume = SafeVolume(masterVolume) * SafeVolume(sfxVolume) * entry.Volume * SafeVolume(gain);
            source.PlayOneShot(entry.Clip); CuePlaybackRequested?.Invoke(cue, entry.Category); return true;
        }

        public bool TryPlay(AudioCue cue, double timestamp)
        {
            if (!entries.TryGetValue(cue, out AudioCueEntry entry)) return false;
            if (entry.Category == AudioCategory.Bgm) return RequestBgm(cue);
            const double timingTolerance = 0.000001d;
            if (lastPlayedAt.TryGetValue(cue, out double previous) && timestamp - previous + timingTolerance < entry.Cooldown)
                return false;

            lastPlayedAt[cue] = timestamp;
            CuePlaybackRequested?.Invoke(cue, entry.Category);
            AudioSource source = GetSource(entry.Category);
            if (source == null || entry.Clip == null) return true;

            if (entry.Loop)
            {
                if (source.clip == entry.Clip && source.isPlaying) return true;
                source.Stop();
                source.clip = entry.Clip;
                source.loop = true;
                channelTrim[(int)entry.Category] = entry.Volume;
                ApplyMix();
                source.Play();
            }
            else
            {
                source.loop = false;
                channelTrim[(int)entry.Category] = 1f;
                ApplyMix();
                source.PlayOneShot(entry.Clip, entry.Volume);
            }
            return true;
        }

        public void StopAll()
        {
            presentationGain = presentationTarget = 1f;
            requestedBgm = playingBgm = null;
            fadePhase = FadePhase.Idle;
            fadeGain = fadeElapsed = 0f;
            StopSource(bgmSource);
            StopEffects();
            ApplyMix();
        }

        private void StopEffects()
        {
            StopSource(uiSource);
            StopSource(sfxSource);
            StopSource(voiceSource);
            StopSource(ambientSource);
            lastPlayedAt.Clear();
            for (int i = 0; i < channelTrim.Length; i++) channelTrim[i] = 1f;
        }

        public void SetMasterVolume(float value) { masterVolume = SafeVolume(value, 0.7f); ApplyMix(); }
        public void SetBgmVolume(float value) { bgmVolume = SafeVolume(value); ApplyMix(); }
        public void SetSfxVolume(float value) { sfxVolume = SafeVolume(value); ApplyMix(); }
        public void SetVoiceVolume(float value) { voiceVolume = SafeVolume(value); ApplyMix(); }
        public void SetBgmMute(bool value) { bgmMute = value; ApplyMix(); }
        public bool OwnsBgmSource(AudioSource source) => source != null && source == bgmSource;

        public bool RequestBgm(AudioCue cue)
        {
            if (!entries.TryGetValue(cue, out AudioCueEntry entry) || entry.Category != AudioCategory.Bgm ||
                entry.Clip == null || bgmSource == null || !isActiveAndEnabled) return false;
            // A completed one-shot is still the selected song: Result must not restart it.
            if (requestedBgm != null && requestedBgm.Clip == entry.Clip && requestedBgm.Loop == entry.Loop) return true;
            requestedBgm = entry;
            float duration = cue == AudioCue.BgmBoss ? BossFadeDuration : FadeDuration;
            if (playingBgm == null || bgmSource.clip == null)
            {
                incomingDuration = duration;
                StartRequestedBgm();
            }
            else
            {
                incomingDuration = duration * 0.5f;
                BeginFadeOut(duration * 0.5f);
            }
            return true;
        }

        public void ResetForNewSession()
        {
            StopEffects();
            // Reset -> Boot, repeated operator Reset: do not prolong the same fade.
            if (requestedBgm == null && fadePhase == FadePhase.Out) return;
            requestedBgm = null;
            if (!isActiveAndEnabled || bgmSource == null || playingBgm == null) { StopAll(); return; }
            BeginFadeOut(FadeDuration);
        }

        private void BeginFadeOut(float seconds)
        {
            fadeStartGain = fadeGain;
            fadeElapsed = 0f;
            phaseDuration = seconds;
            fadePhase = FadePhase.Out;
        }

        private void StartRequestedBgm()
        {
            StopSource(bgmSource);
            playingBgm = requestedBgm;
            fadeGain = fadeElapsed = 0f;
            if (playingBgm == null || bgmSource == null)
            {
                fadePhase = FadePhase.Idle;
                return;
            }
            bgmSource.clip = playingBgm.Clip;
            bgmSource.loop = playingBgm.Loop;
            bgmSource.volume = 0f;
            bgmSource.Play();
            BgmPlaybackStarts++;
            phaseDuration = incomingDuration;
            fadePhase = FadePhase.In;
            CuePlaybackRequested?.Invoke(playingBgm.Cue, AudioCategory.Bgm);
        }

        private void TickBgm(float delta)
        {
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta <= 0f) return;
            // At most two phases per tick; no allocations, coroutines, scene searches or extra sources.
            for (int i = 0; i < 2 && fadePhase != FadePhase.Idle; i++)
            {
                float step = Mathf.Min(delta, Mathf.Max(0f, phaseDuration - fadeElapsed));
                fadeElapsed += step;
                delta -= step;
                float t = Mathf.Clamp01(fadeElapsed / phaseDuration);
                fadeGain = fadePhase == FadePhase.Out ? Mathf.Lerp(fadeStartGain, 0f, t) : t;
                if (t < 1f) break;
                if (fadePhase == FadePhase.Out) StartRequestedBgm();
                else fadePhase = FadePhase.Idle;
            }
        }

        private void ApplyMix()
        {
            float master = SafeVolume(masterVolume, 0.7f);
            if (bgmSource != null)
            {
                bgmSource.volume = master * SafeVolume(bgmVolume) * (playingBgm?.Volume ?? 0f) * fadeGain * presentationGain;
                bgmSource.mute = bgmMute;
            }
            if (uiSource != null) uiSource.volume = master * channelTrim[(int)AudioCategory.UI];
            if (sfxSource != null) sfxSource.volume = master * SafeVolume(sfxVolume) * channelTrim[(int)AudioCategory.Sfx];
            if (voiceSource != null) voiceSource.volume = master * SafeVolume(voiceVolume) * channelTrim[(int)AudioCategory.Voice];
            if (ambientSource != null) ambientSource.volume = master * channelTrim[(int)AudioCategory.Ambient];
        }

        private static float SafeVolume(float value, float maximum = 1f) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp(value, 0f, maximum);
        private static float SafeDuration(float value, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, 0.05f, 10f);

        private void ConfigureSources()
        {
            ConfigureSource(bgmSource);
            ConfigureSource(uiSource);
            ConfigureSource(sfxSource);
            ConfigureSource(voiceSource);
            ConfigureSource(ambientSource);
            ApplyMix();
        }

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
