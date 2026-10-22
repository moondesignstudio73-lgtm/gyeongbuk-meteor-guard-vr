using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MeteorDefenseVR.Audio;
using MeteorDefenseVR.GameFlow;
using UnityEngine;
using Object=UnityEngine.Object;
namespace MeteorDefenseVR.PcInput
{
    public static class AudioPriorityProof
    {
        public static IEnumerator Run(string output,Action<bool> complete)
        {
            var manager=Object.FindAnyObjectByType<AudioManager>();var reset=Object.FindAnyObjectByType<OperationsResetController>();
            var router=Object.FindAnyObjectByType<GazeInputRouter>();bool suspended=router.InputSuspended;
            var library=(AudioCueLibrary)typeof(AudioManager).GetField("cueLibrary",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(manager);
            AudioClip Clip(AudioCue cue)=>library.Entries.Single(e=>e.Cue==cue).Clip;
            var notes=new List<string>{"Actual existing scene clips and DSP playback; deterministic synthetic timestamps stress cooldown/admission. Not natural human play, speaker recording or human listening. No generated audio. Listener WAV is engine mixed output."};
            bool passed=true;void Check(bool ok,string name){passed&=ok;notes.Add(name+"="+ok);}
            var externalRoot=new GameObject("QAExternalEffect");var external=externalRoot.AddComponent<AudioSource>();external.playOnAwake=false;external.spatialBlend=1;external.volume=.25f;external.clip=Clip(AudioCue.MeteorExplosion);AudioChannelFilter.Attach(external,UserAudioChannel.Effects);
            var recorder=Camera.main.gameObject.AddComponent<CampaignOutputRecorder>();recorder.Begin();
            int baseline=Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Length;
            var preferences=Enumerable.Range(0,4).Select(i=>(AudioUserPreferences.Volume((UserAudioChannel)i),AudioUserPreferences.Muted((UserAudioChannel)i))).ToArray();
            void Fill(){for(int i=0;i<AudioManager.TransientVoiceLimit;i++)Check(manager.TryPlay(AudioCue.MeteorExplosion,100+i),"fill low priority "+i);}
            try
            {
                router.InputSuspended=true;reset.ResetExperience();manager.StopAll();
                AudioUserPreferences.ClearVerificationProfile();AudioUserPreferences.UseVerificationProfile(true);
                Fill();Check(manager.ActiveTransientCount==8,"eight-voice global transient cap");
                int rejects=manager.RejectedVoices;Check(!manager.TryPlay(AudioCue.Laser,200)&&manager.RejectedVoices==rejects+1,"equal low priority rejected at cap");
                int preempts=manager.PreemptedVoices;Check(manager.TryPlay(AudioCue.Warning,200)&&manager.PreemptedVoices==preempts+1&&manager.ActiveTransientCount==8&&manager.IsCueActive(AudioCue.Warning),"critical warning preempts lowest at cap");
                Check(!manager.TryPlay(AudioCue.Warning,200.001),"critical cue cooldown retained");
                Check(!manager.TryPlayEffect(external,external.clip,AudioCue.Laser),"external effect cannot bypass cap");
                Check(manager.TryPlayEffect(external,external.clip,AudioCue.PlayerDamage)&&manager.ActiveTransientCount==8,"external damage shares cap and priority");
                var filters=manager.GetComponentsInChildren<AudioChannelFilter>();foreach(var f in filters){f.Measure=true;f.ResetMeasurement();}
                yield return new WaitForSecondsRealtime(.12f);
                Check(filters.Any(f=>f.GetComponent<AudioSource>().clip==Clip(AudioCue.Warning)&&f.PeakOutputRms>0),"actual warning DSP output under saturation");
                Check(manager.GetComponentsInChildren<AudioSource>().Where(s=>s.isPlaying&&s.clip==Clip(AudioCue.MeteorExplosion)).All(s=>s.volume<=manager.SfxOutputGain*.35f+.001f),"lower effects duck while critical warning active");
                yield return new WaitForSecondsRealtime(1.2f);Check(manager.ActiveTransientCount==0,"completed voices reclaimed");
                Check(manager.TryPlay(AudioCue.Laser,200),"admission rejection did not consume cue cooldown");
                manager.StopAll();Check(manager.TryPlay(AudioCue.EngineStart,300),"pause test existing engine cue");
                int active=manager.ActiveTransientCount;AudioListener.pause=true;
                Check(!manager.TryPlay(AudioCue.Warning,301)&&!manager.TryPlayEffect(external,external.clip,AudioCue.PlayerDamage),"no new playback queued while paused");
                yield return new WaitForSecondsRealtime(.3f);Check(manager.ActiveTransientCount==active,"listener pause does not reap frozen voices");
                AudioListener.pause=false;yield return new WaitForSecondsRealtime(1);Check(manager.ActiveTransientCount==0,"resume completes existing voice");
                manager.StopAll();Fill();Check(manager.TryPlaySpatial(AudioCue.Warning,external,Camera.main.transform.position+Vector3.right),"spatial warning preempts saturation");Check(manager.ActiveTransientCount==8,"spatial shares global cap");
                manager.StopAll();Check(!external.isPlaying&&external.clip==Clip(AudioCue.MeteorExplosion)&&Mathf.Abs(external.volume-.25f)<.001f,"external authored clip and volume restored");
                for(int i=0;i<8;i++)Check(manager.TryPlay(AudioCue.Warning,400+i),"critical saturation "+i);
                Check(manager.TryPlay(AudioCue.BossWarning,500)&&manager.ActiveTransientCount==8,"critical admitted even when cap all critical");
                manager.StopAll();manager.TryPlay(AudioCue.EngineStart,600);
                AudioUserPreferences.SetMuted(UserAudioChannel.Effects,true);yield return new WaitForSecondsRealtime(.05f);
                var effectFilters=filters.Where(f=>f.Channel==UserAudioChannel.Effects).ToArray();foreach(var f in effectFilters)f.ResetMeasurement();
                yield return new WaitForSecondsRealtime(.12f);
                Check(effectFilters.All(f=>f.AppliedGain==0&&f.PeakOutputRms<.00001f)&&effectFilters.Any(f=>f.MeasuredBlocks>0&&f.PeakInputRms>0),"pooled effects obey existing mute DSP channel");
                AudioUserPreferences.SetMuted(UserAudioChannel.Effects,false);yield return new WaitForSecondsRealtime(.1f);Check(effectFilters.All(f=>f.AppliedGain==1),"channel gain restored");
                manager.StopAll();manager.TryPlay(AudioCue.EngineStart,650);manager.TryPlay(AudioCue.Warning,650);
                yield return new WaitForSecondsRealtime(.4f);
                var engine=manager.GetComponentsInChildren<AudioSource>().FirstOrDefault(s=>s.isPlaying&&s.clip==Clip(AudioCue.EngineStart));
                Check(!manager.HasCriticalVoice&&engine!=null&&engine.volume>.1f,"remaining effect volume restored after warning ends");
                for(int i=0;i<25;i++){manager.StopAll();Fill();reset.ResetExperience();Check(manager.ActiveTransientCount==0,"reset clears voices "+i);}
                yield return null;Check(Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Length==baseline,"25 resets no source accumulation");
                manager.TryPlay(AudioCue.EngineStart,700);manager.enabled=false;Check(manager.ActiveTransientCount==0,"disable clears all managed voices");manager.enabled=true;
                Check(manager.TryPlay(AudioCue.Warning,700),"next session warning restored");manager.StopAll();
                // Actual existing music and warning clips; sample the applied source envelope
                // alongside fresh DSP output. These measurements are not a listening test.
                var music=manager.CurrentBgmSource;
                var musicFilter=AudioChannelFilter.Attach(music,UserAudioChannel.Music);musicFilter.Measure=true;
                manager.RequestBgm(AudioCue.BgmIntro);yield return new WaitForSecondsRealtime(1.5f);
                float normal=music.volume;Check(normal>0,"music baseline active");
                manager.TryPlay(AudioCue.Warning,800);
                var trace=new List<string>{"time,warningGain,sourceVolume,musicFilterGain,inputRms,outputRms"};
                float previous=1;float end=Time.realtimeSinceStartup+.18f;
                while(Time.realtimeSinceStartup<end)
                {
                    yield return null;
                    Check(manager.WarningMusicGain<=previous+.001f,"attack monotonic");previous=manager.WarningMusicGain;
                    trace.Add($"{Time.realtimeSinceStartup},{manager.WarningMusicGain},{music.volume},{musicFilter.AppliedGain},{musicFilter.InputRms},{musicFilter.OutputRms}");
                }
                Check(Mathf.Abs(manager.WarningMusicGain-.35f)<.01f&&Mathf.Abs(music.volume/normal-.35f)<.01f,"single music attenuation reaches35percent");
                manager.TryPlay(AudioCue.BossWarning,801);yield return new WaitForSecondsRealtime(.2f);
                Check(Mathf.Abs(manager.WarningMusicGain-.35f)<.01f,"overlapping critical cues do not compound");
                float frozen=manager.WarningMusicGain;AudioListener.pause=true;yield return new WaitForSecondsRealtime(.25f);
                Check(manager.WarningMusicGain==frozen,"pause freezes envelope");AudioListener.pause=false;
                while(manager.HasCriticalVoice)yield return null;
                yield return new WaitForSecondsRealtime(.15f);Check(Mathf.Abs(manager.WarningMusicGain-.35f)<.01f,"short silence held without pumping");
                manager.TryPlay(AudioCue.Warning,802);yield return new WaitForSecondsRealtime(.1f);
                Check(Mathf.Abs(manager.WarningMusicGain-.35f)<.01f,"warning during hold does not pump");
                while(manager.HasCriticalVoice)yield return null;
                previous=manager.WarningMusicGain;end=Time.realtimeSinceStartup+1.3f;
                musicFilter.ResetMeasurement();
                while(Time.realtimeSinceStartup<end)
                {
                    yield return null;Check(manager.WarningMusicGain>=previous-.001f,"release monotonic");previous=manager.WarningMusicGain;
                    trace.Add($"{Time.realtimeSinceStartup},{manager.WarningMusicGain},{music.volume},{musicFilter.AppliedGain},{musicFilter.InputRms},{musicFilter.OutputRms}");
                }
                Check(manager.WarningMusicGain==1&&Mathf.Abs(music.volume-normal)<.001f&&musicFilter.PeakOutputRms>0,"normal music gain restored with actual DSP output");
                manager.TryPlay(AudioCue.Warning,803);manager.RequestBgm(AudioCue.BgmBoss);yield return new WaitForSecondsRealtime(1.6f);
                music=manager.CurrentBgmSource;musicFilter=AudioChannelFilter.Attach(music,UserAudioChannel.Music);musicFilter.Measure=true;
                Check(!manager.IsBgmTransitioning&&manager.WarningMusicGain==1&&music.clip==Clip(AudioCue.BgmBoss),"song transition restores without changing selection");
                AudioUserPreferences.SetVolume(UserAudioChannel.Music,0);manager.TryPlay(AudioCue.Warning,804);yield return new WaitForSecondsRealtime(.05f);musicFilter.ResetMeasurement();yield return new WaitForSecondsRealtime(.15f);
                Check(musicFilter.AppliedGain==0&&musicFilter.PeakOutputRms==0,"music zero stays silent during warning");
                AudioUserPreferences.SetVolume(UserAudioChannel.Music,.6f);AudioUserPreferences.SetMuted(UserAudioChannel.Music,true);
                yield return new WaitForSecondsRealtime(1.3f);Check(musicFilter.AppliedGain==0&&manager.WarningMusicGain==1,"music mute survives restoration");
                AudioUserPreferences.SetMuted(UserAudioChannel.Music,false);Check(Mathf.Abs(musicFilter.AppliedGain-.6f)<.001f,"user music volume retained");
                manager.TryPlay(AudioCue.Warning,805);yield return new WaitForSecondsRealtime(.18f);manager.ResetForNewSession();
                Check(manager.WarningMusicGain==1,"reset clears warning envelope");yield return new WaitForSecondsRealtime(1.2f);
                Check(!music.isPlaying,"reset fade completes");manager.RequestBgm(AudioCue.BgmIntro);yield return new WaitForSecondsRealtime(1.2f);
                Check(manager.WarningMusicGain==1&&music.isPlaying,"next session music restored");manager.StopAll();Check(manager.WarningMusicGain==1&&!music.isPlaying,"stop clears warning envelope");
                Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"music-envelope.csv"),trace);
            }
            finally
            {
                AudioListener.pause=false;manager.enabled=true;reset.ResetExperience();router.InputSuspended=suspended;
                foreach(var f in manager.GetComponentsInChildren<AudioChannelFilter>())f.Measure=false;
                AudioUserPreferences.ClearVerificationProfile();AudioUserPreferences.UseVerificationProfile(false);
                for(int i=0;i<4;i++)Check(AudioUserPreferences.Volume((UserAudioChannel)i)==preferences[i].Item1&&AudioUserPreferences.Muted((UserAudioChannel)i)==preferences[i].Item2,"user preference retained "+i);
                Directory.CreateDirectory(output);notes.Add(recorder.Save(Path.Combine(output,"listener-output.wav")));Object.Destroy(recorder);Object.Destroy(externalRoot);
                notes.Add("passed="+passed);File.WriteAllLines(Path.Combine(output,"verification.txt"),notes);
            }
            complete(passed);
        }
    }
}
