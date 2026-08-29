using System;
using UnityEngine;
using MeteorDefenseVR.Core;

namespace MeteorDefenseVR.Meteor
{
    [CreateAssetMenu(menuName = "Meteor Defense/Meteor Wave", fileName = "MeteorWave")]
    public sealed class MeteorWaveData : ScriptableObject
    {
        [SerializeField] private string displayName = "Wave";
        [SerializeField, Min(0f)] private float waveDelay = 1f;
        [SerializeField] private MeteorSpawnData[] spawns = Array.Empty<MeteorSpawnData>();

        public string DisplayName => displayName;
        public float WaveDelay => RuntimeValueGuard.Clamp(waveDelay, 0, 300, 1);
        public MeteorSpawnData[] Spawns => spawns ?? Array.Empty<MeteorSpawnData>();
        public int TotalMeteors
        {
            get
            {
                int total = 0;
                foreach (MeteorSpawnData spawn in Spawns)
                    if (spawn != null) total = RuntimeValueGuard.Add(total, spawn.Count);
                return total;
            }
        }

        public float EstimatedDuration
        {
            get
            {
                float duration = WaveDelay;
                foreach (MeteorSpawnData spawn in Spawns)
                    if (spawn != null) duration += spawn.EstimatedDuration;
                return duration;
            }
        }

        public void Configure(string waveName, float delay, params MeteorSpawnData[] spawnEntries)
        {
            displayName = string.IsNullOrWhiteSpace(waveName) ? name : waveName;
            waveDelay = Mathf.Max(0f, delay);
            spawns = spawnEntries ?? Array.Empty<MeteorSpawnData>();
        }
    }
}
