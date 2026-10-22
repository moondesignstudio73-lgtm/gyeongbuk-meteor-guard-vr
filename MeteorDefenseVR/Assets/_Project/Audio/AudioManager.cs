using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace MeteorDefenseVR.Audio
{
    [DisallowMultipleComponent]
    public sealed partial class AudioManager : MonoBehaviour
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
        [Tooltip("Overlap transition duration. Also used for the full Reset fade-out.")]
        [SerializeField, Min(0.05f)] private float fadeDuration = 1f;
        [SerializeField, Min(0.05f)] private float bossFadeDuration = 0.4f;

        private enum FadePhase { Idle, Out, In }
        private FadePhase fadePhase;
        private AudioCueEntry requestedBgm;
        private AudioCueEntry playingBgm;
        private float fadeGain, fadeElapsed, phaseDuration, fadeStartGain;
        private AudioSource outgoingBgmSource, ownedBgmSource;
        private AudioCueEntry outgoingBgm;
        private float outgoingGain, outgoingStartGain;
        public AudioSource CurrentBgmSource => bgmSource;
        public float BgmEnvelopeGain => fadeGain + outgoingGain;
        private float presentationGain = 1f, presentationTarget = 1f;
        private float presentationSfxPitch = 1f;
        private float warningMusicGain = 1f, warningMusicHold;
        public float WarningMusicGain => warningMusicGain;
        private void TickWarningMusic(float delta)
        {
            if (HasCriticalVoice) warningMusicHold = .35f;
            else warningMusicHold = Mathf.Max(0f, warningMusicHold - delta);
            float target = HasCriticalVoice || warningMusicHold > 0f ? .35f : 1f;
            warningMusicGain = Mathf.MoveTowards(warningMusicGain, target,
                delta * .65f / (target < warningMusicGain ? .15f : .8f));
        }
        public float PresentationGain => presentationGain;
        public void SetPresentationBgmGain(float gain) => presentationTarget = SafeVolume(gain);
        public void SetPresentationSfxPitch(float pitch) => presentationSfxPitch = Mathf.Clamp(pitch, .85f, 1.25f);
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
            InitializeVoices();
            RebuildLookup();
        }

        private void Update()
        {
            // Listener pause freezes audible playback and its fade at the same point.
            // A timeScale-only reset still fades out on unscaled time as before.
            if (AudioListener.pause) return;
            ReapVoices();
            TickWarningMusic(Time.unscaledDeltaTime);
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
            outgoingBgmSource = ownedBgmSource;
            uiSource = ui;
            sfxSource = sfx;
            voiceSource = voice;
            ambientSource = ambient;
            ConfigureSources();
            RebuildLookup();
        }

        // Legacy optional boss clip shares the existing effects source; no temporary source leaks.
        public void PlaySupplementalEffect(AudioClip clip) { if (clip != null && sfxSource != null) PlayTransient(clip,AudioCategory.Sfx,AudioCue.BossExplosion,1f); }

        public bool TryPlay(AudioCue cue) => TryPlay(cue, AudioSettings.dspTime);

        // Caller owns a single prewarmed 3D source. Shares library, mix and cue cooldowns.
        public bool TryPlaySpatial(AudioCue cue, AudioSource source, Vector3 position, float gain = 1f)
        {
            if (source == null || source.isPlaying || !entries.TryGetValue(cue, out var entry) || entry.Clip == null || entry.Category == AudioCategory.Bgm) return false;
            double now = AudioSettings.dspTime;
            if (lastPlayedAt.TryGetValue(cue, out double previous) && now - previous < entry.Cooldown) return false;
            if(!AdmitVoice(source,entry.Clip,entry.Category,cue,SafeVolume(masterVolume)*SafeVolume(sfxVolume)*entry.Volume*SafeVolume(gain),false))return false;
            source.transform.position = position;
            lastPlayedAt[cue] = now;CuePlaybackRequested?.Invoke(cue, entry.Category);return true;
        }

        public bool TryPlay(AudioCue cue, double timestamp)
        {
            if (!entries.TryGetValue(cue, out AudioCueEntry entry)) return false;
            if (entry.Category == AudioCategory.Bgm) return RequestBgm(cue);
            if(AudioListener.pause||!isActiveAndEnabled)return false;
            const double timingTolerance = 0.000001d;
            if (lastPlayedAt.TryGetValue(cue, out double previous) && timestamp - previous + timingTolerance < entry.Cooldown)
                return false;

            AudioSource source = GetSource(entry.Category);
            if(source!=null&&entry.Clip!=null&& !entry.Loop)
            {
                if(!PlayTransient(entry.Clip,entry.Category,cue,entry.Volume))return false;
            }
            else if (source!=null&&entry.Clip!=null&&entry.Loop)
            {
                if (source.clip != entry.Clip || !source.isPlaying)
                {
                    source.Stop();
                    source.clip = entry.Clip;
                    source.loop = true;
                    channelTrim[(int)entry.Category] = entry.Volume;
                    ApplyMix();
                    source.Play();
                }
            }
            lastPlayedAt[cue] = timestamp;
            CuePlaybackRequested?.Invoke(cue, entry.Category);
            return true;
        }

        public void StopAll()
        {
            presentationGain = presentationTarget = 1f;
            presentationSfxPitch = 1f;
            requestedBgm = playingBgm = outgoingBgm = null;
            fadePhase = FadePhase.Idle;
            fadeGain = fadeElapsed = outgoingGain = outgoingStartGain = 0f;
            StopSource(bgmSource); StopSource(outgoingBgmSource);
            StopEffects();
            ApplyMix();
        }

        private void StopEffects()
        {
            StopVoices();
            warningMusicHold = 0f;
            warningMusicGain = 1f;
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
        public bool OwnsBgmSource(AudioSource source) => source != null && (source == bgmSource || source == outgoingBgmSource);
        public bool OwnsAmbientSource(AudioSource source) => source != null && source == ambientSource;

        public bool RequestBgm(AudioCue cue)
        {
            if (!entries.TryGetValue(cue,out var entry) || entry.Category!=AudioCategory.Bgm ||
                entry.Clip==null || bgmSource==null || !isActiveAndEnabled) return false;
            if (SameTrack(requestedBgm,entry)) return true; // includes finished Result one-shot
            bool restarting = requestedBgm==null && fadePhase==FadePhase.Out;
            requestedBgm=entry;
            if(restarting)
            {
                StopSource(bgmSource);StopSource(outgoingBgmSource);
                playingBgm=outgoingBgm=null;fadeGain=outgoingGain=0;fadePhase=FadePhase.Idle;
            }
            // Only one latest request is retained during an overlap. Do not cut an
            // audible source to make room for a third song or allocate another source.
            if(fadePhase==FadePhase.Idle)StartRequestedBgm();
            return true;
        }
        private static bool SameTrack(AudioCueEntry a,AudioCueEntry b)
            =>a!=null&&b!=null&&a.Clip==b.Clip&&a.Loop==b.Loop;
        public void ResetForNewSession()
        {
            StopEffects();
            if(requestedBgm==null&&fadePhase==FadePhase.Out)return;
            requestedBgm=null;
            if(!isActiveAndEnabled||playingBgm==null){StopAll();return;}
            fadeStartGain=fadeGain;outgoingStartGain=outgoingGain;
            fadeElapsed=0;phaseDuration=FadeDuration;fadePhase=FadePhase.Out;
        }
        private void StartRequestedBgm()
        {
            if(requestedBgm==null||SameTrack(playingBgm,requestedBgm))return;
            if(playingBgm!=null&&bgmSource.clip!=null)
            {
                StopSource(outgoingBgmSource);
                var previous=bgmSource;bgmSource=outgoingBgmSource;outgoingBgmSource=previous;
                outgoingBgm=playingBgm;outgoingGain=fadeGain;
            }
            else {outgoingBgm=null;outgoingGain=0;}
            outgoingStartGain=outgoingGain;
            playingBgm=requestedBgm;fadeGain=fadeElapsed=0;
            bgmSource.clip=playingBgm.Clip;bgmSource.loop=playingBgm.Loop;bgmSource.volume=0;
            bgmSource.Play();BgmPlaybackStarts++;
            phaseDuration=playingBgm.Cue==AudioCue.BgmBoss?BossFadeDuration:FadeDuration;
            fadePhase=FadePhase.In;
            CuePlaybackRequested?.Invoke(playingBgm.Cue,AudioCategory.Bgm);
        }
        private void TickBgm(float delta)
        {
            if(float.IsNaN(delta)||float.IsInfinity(delta)||delta<=0)return;
            for(int i=0;i<2&&fadePhase!=FadePhase.Idle;i++)
            {
                float step=Mathf.Min(delta,Mathf.Max(0,phaseDuration-fadeElapsed));
                fadeElapsed+=step;delta-=step;float t=Mathf.Clamp01(fadeElapsed/phaseDuration);
                outgoingGain=outgoingStartGain*(1-t);
                fadeGain=fadePhase==FadePhase.Out?fadeStartGain*(1-t):t;
                if(t<1)break;
                StopSource(outgoingBgmSource);outgoingBgm=null;outgoingGain=0;
                if(fadePhase==FadePhase.Out){StopSource(bgmSource);playingBgm=null;fadeGain=0;}
                fadePhase=FadePhase.Idle;
                if(requestedBgm!=null&&!SameTrack(playingBgm,requestedBgm))StartRequestedBgm();
            }
        }
        private void InitializeMusicSource()
        {
            if(bgmSource==null)return;
            if(ownedBgmSource==null)
            {
                var root=new GameObject("ManagedBgmCrossfade");root.transform.SetParent(transform,false);
                ownedBgmSource=root.AddComponent<AudioSource>();ConfigureSource(ownedBgmSource);
                ownedBgmSource.outputAudioMixerGroup=bgmSource.outputAudioMixerGroup;
                outgoingBgmSource=ownedBgmSource;
            }
            AudioChannelFilter.Attach(bgmSource,UserAudioChannel.Music);
            AudioChannelFilter.Attach(outgoingBgmSource,UserAudioChannel.Music);
        }
        private void ApplyMix()
        {
            float master = SafeVolume(masterVolume, 0.7f);
            if (bgmSource != null)
            {
                // One attenuation: boss presentation and critical warnings never stack.
                bgmSource.volume = master * SafeVolume(bgmVolume) * (playingBgm?.Volume ?? 0f) * fadeGain * Mathf.Min(presentationGain, warningMusicGain);
                bgmSource.mute = bgmMute;
            }
            if(outgoingBgmSource!=null)
            {
                outgoingBgmSource.volume=master*SafeVolume(bgmVolume)*(outgoingBgm?.Volume??0f)*outgoingGain*Mathf.Min(presentationGain,warningMusicGain);
                outgoingBgmSource.mute=bgmMute;
            }
            if (uiSource != null) uiSource.volume = master * channelTrim[(int)AudioCategory.UI];
            if (sfxSource != null) sfxSource.volume = master * SafeVolume(sfxVolume) * channelTrim[(int)AudioCategory.Sfx];
            if (sfxSource != null) sfxSource.pitch = presentationSfxPitch;
            if (voiceSource != null) voiceSource.volume = master * SafeVolume(voiceVolume) * channelTrim[(int)AudioCategory.Voice];
            if (ambientSource != null) ambientSource.volume = master * channelTrim[(int)AudioCategory.Ambient];
            ApplyVoiceMix();
        }

        private static float SafeVolume(float value, float maximum = 1f) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp(value, 0f, maximum);
        private static float SafeDuration(float value, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, 0.05f, 10f);

        private void ConfigureSources()
        {
            ConfigureSource(bgmSource);
            InitializeMusicSource();
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
