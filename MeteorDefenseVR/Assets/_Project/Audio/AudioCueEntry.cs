using System;
using UnityEngine;

namespace MeteorDefenseVR.Audio
{
    [Serializable]
    public sealed class AudioCueEntry
    {
        [SerializeField] private AudioCue cue;
        [SerializeField] private AudioCategory category = AudioCategory.Sfx;
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 0.7f)] private float volume = 0.5f;
        [SerializeField, Min(0f)] private float cooldown = 0.08f;
        [SerializeField] private bool loop;

        public AudioCue Cue => cue;
        public AudioCategory Category => category;
        public AudioClip Clip => clip;
        public float Volume => Mathf.Clamp(volume, 0f, 0.7f);
        public float Cooldown => Mathf.Max(0f, cooldown);
        public bool Loop => loop;

        public AudioCueEntry(AudioCue cue, AudioCategory category, float volume, float cooldown, bool loop = false, AudioClip clip = null)
        {
            this.cue = cue;
            this.category = category;
            this.volume = volume;
            this.cooldown = cooldown;
            this.loop = loop;
            this.clip = clip;
        }
    }
}
