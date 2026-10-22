using UnityEngine;

namespace MeteorDefenseVR.Audio
{
    public sealed partial class AudioManager
    {
        // Bounds transient playback across cue, spatial, supplemental and authored effect paths.
        // Music/ambient/servo/air-leak beds remain individually owned loops, not queued voices.
        public const int TransientVoiceLimit=8;
        private sealed class Voice
        {
            public AudioSource Pool,Source;
            public AudioClip OriginalClip;
            public bool OriginalLoop,Owned;
            public float OriginalVolume,OriginalPitch,Gain;
            public int OriginalPriority,Priority;
            public long Order;
            public AudioCategory Category;
            public AudioCue Cue;
        }
        private readonly Voice[] voices=new Voice[TransientVoiceLimit];
        private long voiceOrder;
        public int RejectedVoices { get; private set; }
        public int PreemptedVoices { get; private set; }
        public int ActiveTransientCount { get { int count=0;foreach(var v in voices)if(v?.Source!=null)count++;return count; } }
        public bool HasCriticalVoice { get { foreach(var v in voices)if(v?.Source!=null&&v.Priority==3)return true;return false; } }
        public bool IsCueActive(AudioCue cue){foreach(var v in voices)if(v?.Source!=null&&v.Cue==cue)return true;return false;}
        public static int PriorityFor(AudioCue cue)=>cue switch
        {
            AudioCue.Warning or AudioCue.BossWarning=>3,
            AudioCue.PlayerDamage or AudioCue.GlassHit or AudioCue.Countdown or AudioCue.Launch or AudioCue.EngineStart or AudioCue.MissionComplete or AudioCue.Result=>2,
            AudioCue.CalibrationPoint or AudioCue.CalibrationComplete or AudioCue.LockOn or AudioCue.BossExplosion=>1,
            _=>0
        };
        private void InitializeVoices()
        {
            for(int i=0;i<voices.Length;i++)
            {
                if(voices[i]!=null)continue;
                var root=new GameObject("ManagedEffectVoice"+i);root.transform.SetParent(transform,false);
                var source=root.AddComponent<AudioSource>();ConfigureSource(source);
                AudioChannelFilter.Attach(source,UserAudioChannel.Effects);
                voices[i]=new Voice{Pool=source};
            }
        }
        private bool PlayTransient(AudioClip clip,AudioCategory category,AudioCue cue,float gain)
            =>AdmitVoice(null,clip,category,cue,gain,true);
        // Existing authored source/clip, no new cue or new cooldown. At most one voice per external source.
        public bool TryPlayEffect(AudioSource source,AudioClip clip,AudioCue cue,float gain=1f)
        {
            if(source==null)return false;
            float volume=source.volume;
            foreach(var v in voices)if(v?.Source==source){volume=v.OriginalVolume;break;}
            return AdmitVoice(source,clip,AudioCategory.Sfx,cue,volume*SafeVolume(gain),false);
        }
        private void OnDestroy()
        {
            StopVoices();foreach(var v in voices)if(v?.Pool!=null)
            {if(Application.isPlaying)Destroy(v.Pool.gameObject);else DestroyImmediate(v.Pool.gameObject);}
        }
        private bool AdmitVoice(AudioSource external,AudioClip clip,AudioCategory category,AudioCue cue,float gain,bool owned)
        {
            if(clip==null||!isActiveAndEnabled||AudioListener.pause)return false;
            InitializeVoices();ReapVoices();int priority=PriorityFor(cue),chosen=-1;
            if(external!=null)
                for(int i=0;i<voices.Length;i++)if(voices[i].Source==external)
                {if(voices[i].Priority>priority){RejectedVoices++;return false;}chosen=i;break;}
            if(chosen<0)for(int i=0;i<voices.Length;i++)if(voices[i].Source==null){chosen=i;break;}
            if(chosen<0)
            {
                for(int i=0;i<voices.Length;i++)
                    if(chosen<0||voices[i].Priority<voices[chosen].Priority||(voices[i].Priority==voices[chosen].Priority&&voices[i].Order<voices[chosen].Order))chosen=i;
                // Critical warnings can replace the oldest critical if all eight are critical.
                if(voices[chosen].Priority>=priority&&priority<3){RejectedVoices++;return false;}
                PreemptedVoices++;
            }
            var v=voices[chosen];ReleaseVoice(v);
            v.Source=owned?v.Pool:external;v.Owned=owned;v.Category=category;v.Cue=cue;v.Priority=priority;v.Order=++voiceOrder;v.Gain=gain;
            var s=v.Source;v.OriginalClip=s.clip;v.OriginalLoop=s.loop;v.OriginalVolume=s.volume;v.OriginalPitch=s.pitch;v.OriginalPriority=s.priority;
            if(owned)
            {
                var template=GetSource(category);s.outputAudioMixerGroup=template!=null?template.outputAudioMixerGroup:null;
                var route=template!=null?template.GetComponent<AudioChannelFilter>():null;
                AudioChannelFilter.Attach(s,route!=null?route.Channel:UserAudioChannel.Effects);
                s.spatialBlend=0;s.mute=template!=null&&template.mute;
            }
            s.Stop();s.clip=clip;s.loop=false;s.priority=priority==3?32:128;
            ApplyVoiceMix();s.Play();return true;
        }
        private void ReapVoices()
        {
            if(AudioListener.pause)return;
            foreach(var v in voices)if(v!=null&&v.Source!=null&&!v.Source.isPlaying)ReleaseVoice(v);
        }
        private static void ReleaseVoice(Voice v)
        {
            if(v?.Source==null)return;
            var s=v.Source;s.Stop();s.clip=v.Owned?null:v.OriginalClip;s.loop=v.Owned?false:v.OriginalLoop;
            s.volume=v.OriginalVolume;s.pitch=v.OriginalPitch;s.priority=v.OriginalPriority;v.Source=null;
        }
        private void StopVoices(){foreach(var v in voices)ReleaseVoice(v);voiceOrder=0;}
        private void ApplyVoiceMix()
        {
            bool critical=HasCriticalVoice;
            foreach(var v in voices)
            {
                if(v?.Source==null)continue;
                float volume=v.Gain;
                if(v.Owned)
                {
                    volume*=SafeVolume(masterVolume,.7f)*(v.Category==AudioCategory.Sfx?SafeVolume(sfxVolume):v.Category==AudioCategory.Voice?SafeVolume(voiceVolume):1f);
                    v.Source.pitch=v.Category==AudioCategory.Sfx?presentationSfxPitch:1;
                }
                v.Source.volume=volume*(critical&&v.Priority<3?.35f:1f);
            }
        }
    }
}
