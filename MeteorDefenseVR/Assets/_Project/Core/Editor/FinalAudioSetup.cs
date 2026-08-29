using System;
using System.IO;
using System.Collections.Generic;
using MeteorDefenseVR.Audio;
using UnityEditor;
using UnityEngine;

namespace MeteorDefenseVR.Editor
{
    public static class FinalAudioSetup
    {
        public static void Ensure()
        {
            const string directory = "Assets/_Project/Audio/Generated";
            Directory.CreateDirectory(directory);
            var library = AssetDatabase.LoadAssetAtPath<AudioCueLibrary>("Assets/_Project/Audio/AudioCueLibrary.asset");
            if (library == null) return;
            var entries = new List<AudioCueEntry>();
            var present = new HashSet<AudioCue>();
            foreach (AudioCueEntry entry in library.Entries)
            {
                if (entry == null || !present.Add(entry.Cue)) continue;
                string music = BgmFile(entry.Cue);
                AudioClip clip = entry.Clip;
                if (music != null)
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/BGM/" + music + ".mp3");
                else if (clip == null)
                {
                    string path = directory + "/" + entry.Cue + ".wav";
                    if (!File.Exists(path)) WriteCue(path, entry.Cue);
                    AssetDatabase.ImportAsset(path);
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                }
                // The legacy victory sting must not replace the selected Result music.
                AudioCategory category = music != null ? AudioCategory.Bgm :
                    entry.Cue == AudioCue.MissionComplete ? AudioCategory.Sfx : entry.Category;
                entries.Add(new AudioCueEntry(entry.Cue, category, entry.Volume, entry.Cooldown, entry.Loop, clip));
            }
            if (present.Add(AudioCue.EngineStart))
            {
                string path = directory + "/EngineStart.wav";
                if (!File.Exists(path)) WriteCue(path, AudioCue.EngineStart);
                AssetDatabase.ImportAsset(path);
                entries.Add(new AudioCueEntry(AudioCue.EngineStart, AudioCategory.Sfx, .45f, .5f, false, AssetDatabase.LoadAssetAtPath<AudioClip>(path)));
            }
            foreach (AudioCue cue in new[] { AudioCue.BgmIntro, AudioCue.BgmTutorial, AudioCue.BgmBattle, AudioCue.BgmBoss, AudioCue.BgmResult })
                if (present.Add(cue))
                    entries.Add(new AudioCueEntry(cue, AudioCategory.Bgm, .7f, 0f, cue != AudioCue.BgmResult,
                        AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/BGM/" + BgmFile(cue) + ".mp3")));
            library.Configure(entries.ToArray());
            EditorUtility.SetDirty(library);
        }

        private static string BgmFile(AudioCue cue) => cue switch
        {
            AudioCue.BgmIntro => "Intro", AudioCue.BgmTutorial => "Tutorial", AudioCue.BgmBattle => "Battle",
            AudioCue.BgmBoss => "Boss", AudioCue.BgmResult => "Result", _ => null
        };

        // Original, deterministic lightweight synth assets; no external audio/license dependency.
        private static void WriteCue(string path, AudioCue cue)
        {
            const int rate = 22050;
            bool ambient = cue == AudioCue.IntroSpaceAmbient;
            bool victory = cue == AudioCue.MissionComplete;
            bool explosion = cue == AudioCue.MeteorExplosion || cue == AudioCue.BossExplosion;
            bool laser = cue == AudioCue.Laser;
            bool engine = cue == AudioCue.EngineStart;
            float seconds = ambient ? 8f : victory ? 4f : engine ? .7f : explosion ? .85f : laser ? .22f : .28f;
            int count = (int)(seconds * rate);
            var random = new System.Random(73 + (int)cue);
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
            writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
            for (int i = 0; i < count; i++)
            {
                double t = (double)i / rate, normalized = t / seconds;
                double envelope = ambient ? .32 : Math.Min(1, t / .015) * Math.Pow(1 - normalized, victory ? 1 : 2);
                double sound;
                if (ambient) sound = .18 * Math.Sin(2 * Math.PI * 55 * t) + .1 * Math.Sin(2 * Math.PI * 82.5 * t);
                else if (engine) sound = .32 * Math.Sin(2 * Math.PI * (55 * t + 180 * t * t)) + .1 * (random.NextDouble() * 2 - 1);
                else if (victory) sound = .18 * (Math.Sin(2 * Math.PI * 261.63 * t) + Math.Sin(2 * Math.PI * 329.63 * t) + Math.Sin(2 * Math.PI * 392 * t));
                else if (explosion) sound = .45 * (random.NextDouble() * 2 - 1) + .2 * Math.Sin(2 * Math.PI * 65 * t);
                else if (laser) sound = .4 * Math.Sin(2 * Math.PI * (1500 * t - 2400 * t * t));
                else sound = .3 * Math.Sin(2 * Math.PI * (440 + (int)cue * 28) * t);
                writer.Write((short)(Math.Clamp(sound * envelope, -.8, .8) * short.MaxValue));
            }
        }
    }
}
