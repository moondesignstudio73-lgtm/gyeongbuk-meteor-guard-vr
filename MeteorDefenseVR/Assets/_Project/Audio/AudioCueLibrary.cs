using System;
using UnityEngine;

namespace MeteorDefenseVR.Audio
{
    [CreateAssetMenu(menuName = "Meteor Defense/Audio Cue Library", fileName = "AudioCueLibrary")]
    public sealed class AudioCueLibrary : ScriptableObject
    {
        [SerializeField] private AudioCueEntry[] entries = Array.Empty<AudioCueEntry>();

        public AudioCueEntry[] Entries => entries ?? Array.Empty<AudioCueEntry>();

        public void Configure(AudioCueEntry[] cueEntries)
        {
            entries = cueEntries ?? Array.Empty<AudioCueEntry>();
        }
    }
}
