using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Audio;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using UnityEngine;
using Object=UnityEngine.Object;
namespace MeteorDefenseVR.PcInput
{
    public static class BgmCrossfadeProof
    {
        public static IEnumerator Run(string output,bool validate,Action<bool> complete)
        {
            var manager=Object.FindAnyObjectByType<AudioManager>();var flow=GameFlowManager.Instance;
            var reset=Object.FindAnyObjectByType<OperationsResetController>();var router=Object.FindAnyObjectByType<GazeInputRouter>();
            bool suspended=router.InputSuspended,passed=true;
            var notes=new List<string>{"Real state events and existing BGM clips, actual source DSP and applied envelope. Controlled QA, not human listening, loop-seam or speaker/HMD verification."};
            var trace=new List<string>{"time,segment,source,clip,playing,gain,inputRms,outputRms"};
            var recorder=Camera.main.gameObject.AddComponent<CampaignOutputRecorder>();recorder.Begin();
            AudioSource[] Music()=>Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Where(manager.OwnsBgmSource).ToArray();
            void Check(bool value,string name){notes.Add(name+"="+value);if(validate)passed&=value;}
            IEnumerator Observe(string segment,float seconds,bool requireOverlap)
            {
                var music=Music();foreach(var s in music){var f=AudioChannelFilter.Attach(s,UserAudioChannel.Music);f.Measure=true;f.ResetMeasurement();}
                float end=Time.realtimeSinceStartup+seconds;bool overlap=false,bounded=true;
                while(Time.realtimeSinceStartup<end)
                {
                    yield return null;
                    overlap|=music.Count(s=>s.isPlaying&&s.volume>.001f&&s.GetComponent<AudioChannelFilter>().OutputRms>0)==2;
                    bounded&=music.Length<=2&&manager.BgmEnvelopeGain<=1.0001f;
                    foreach(var s in music){var f=s.GetComponent<AudioChannelFilter>();trace.Add($"{Time.realtimeSinceStartup},{segment},{s.name},{s.clip?.name},{s.isPlaying},{s.volume},{f.InputRms},{f.OutputRms}");}
                }
                notes.Add(segment+" overlap="+overlap);if(requireOverlap)Check(overlap,segment+" actual two-clip overlap");Check(bounded,segment+" bounded source count and summed gain");
            }
            try
            {
                router.InputSuspended=true;reset.ResetExperience();AudioUserPreferences.ClearVerificationProfile();AudioUserPreferences.UseVerificationProfile(true);
                flow.StartMainGame();yield return Observe("battle",1.3f,false);
                flow.StartBoss();yield return Observe("battle-boss",.6f,true);
                flow.ShowResult();yield return Observe("boss-result",1.3f,true);
                flow.StartGame();yield return Observe("result-base",1.3f,true);
                reset.ResetExperience();manager.StopAll();manager.RequestBgm(AudioCue.BgmIntro);
                yield return Observe("isolated-controls-baseline",1.3f,false);
                int starts=manager.BgmPlaybackStarts;
                for(int i=0;i<30;i++)manager.RequestBgm(AudioCue.BgmIntro);
                Check(manager.BgmPlaybackStarts==starts&&!manager.IsBgmTransitioning,"same track requests do not restart");
                manager.RequestBgm(AudioCue.BgmBattle);yield return Observe("rapid-first",.15f,true);
                manager.RequestBgm(AudioCue.BgmBoss);manager.RequestBgm(AudioCue.BgmResult);manager.RequestBgm(AudioCue.BgmTutorial);
                yield return Observe("latest-request",2.2f,true);
                Check(manager.TargetBgmCue==AudioCue.BgmTutorial&&manager.CurrentBgmClip.name=="Tutorial"&&!manager.IsBgmTransitioning,"latest request converges with only two sources");
                float plain=Music().Sum(s=>s.volume);
                manager.RequestBgm(AudioCue.BgmBattle);manager.TryPlay(AudioCue.Warning);
                yield return Observe("warning-overlap",.19f,true);
                Check(manager.WarningMusicGain<.5f&&Music().Sum(s=>s.volume)<=plain*.5f,"warning attenuation applied to both music voices");
                manager.SetPresentationBgmGain(.28f);yield return Observe("boss-presentation-overlap",.2f,true);
                Check(Music().Sum(s=>s.volume)<=plain*.36f,"boss presentation and warning never stack louder");
                manager.SetPresentationBgmGain(1);yield return Observe("restore",1.4f,false);
                manager.RequestBgm(AudioCue.BgmIntro);yield return Observe("pause-before",.15f,true);
                var paused=Music().Select(s=>s.volume).ToArray();float envelope=manager.BgmEnvelopeGain;
                AudioListener.pause=true;yield return new WaitForSecondsRealtime(.3f);
                Check(Music().Select(s=>s.volume).OrderBy(v=>v).SequenceEqual(paused.OrderBy(v=>v))&&manager.BgmEnvelopeGain==envelope,"pause freezes both envelopes");
                AudioListener.pause=false;yield return Observe("pause-resume",1.1f,true);
                AudioUserPreferences.SetVolume(UserAudioChannel.Music,0);manager.RequestBgm(AudioCue.BgmBattle);
                yield return new WaitForSecondsRealtime(.05f);
                yield return Observe("music-zero",.25f,false);
                Check(Music().All(s=>s.GetComponent<AudioChannelFilter>().AppliedGain==0&&s.GetComponent<AudioChannelFilter>().PeakOutputRms==0),"both voices obey music zero");
                AudioUserPreferences.SetVolume(UserAudioChannel.Music,.6f);AudioUserPreferences.SetMuted(UserAudioChannel.Music,true);
                yield return new WaitForSecondsRealtime(.05f);
                yield return Observe("music-mute",.25f,false);
                Check(Music().All(s=>s.GetComponent<AudioChannelFilter>().AppliedGain==0&&s.GetComponent<AudioChannelFilter>().PeakOutputRms==0),"both voices obey user mute");
                AudioUserPreferences.SetMuted(UserAudioChannel.Music,false);manager.SetBgmMute(true);
                Check(Music().All(s=>s.mute),"both voices obey manager mute");manager.SetBgmMute(false);
                yield return Observe("unmute",.7f,false);
                Check(Music().All(s=>Mathf.Abs(s.GetComponent<AudioChannelFilter>().AppliedGain-.6f)<.001f),"user music gain retained");
                manager.RequestBgm(AudioCue.BgmIntro);yield return new WaitForSecondsRealtime(.2f);manager.ResetForNewSession();
                yield return Observe("reset-overlap",1.1f,false);Check(Music().All(s=>!s.isPlaying&&s.clip==null),"reset clears both old tracks");
                manager.RequestBgm(AudioCue.BgmResult);yield return Observe("result",1.1f,false);
                manager.CurrentBgmSource.time=Mathf.Max(0,manager.CurrentBgmClip.length-.1f);yield return new WaitForSecondsRealtime(.35f);
                Check(!manager.CurrentBgmSource.isPlaying,"actual result reaches end after explicit near-end seek");starts=manager.BgmPlaybackStarts;
                for(int i=0;i<30;i++)manager.RequestBgm(AudioCue.BgmResult);
                Check(manager.BgmPlaybackStarts==starts&&!manager.CurrentBgmSource.isPlaying,"finished result requests do not restart");
                manager.RequestBgm(AudioCue.BgmBattle);yield return new WaitForSecondsRealtime(.2f);manager.enabled=false;
                Check(Music().All(s=>!s.isPlaying&&s.clip==null),"disable clears both tracks");manager.enabled=true;
                for(int i=0;i<20;i++){manager.RequestBgm(AudioCue.BgmIntro);manager.RequestBgm(AudioCue.BgmBoss);manager.StopAll();}
                Check(Music().Length==2&&Music().All(s=>!s.isPlaying),"twenty request resets retain exactly two sources");
            }
            finally
            {
                AudioListener.pause=false;Time.timeScale=1;manager.enabled=true;reset.ResetExperience();router.InputSuspended=suspended;
                foreach(var s in Music())s.GetComponent<AudioChannelFilter>().Measure=false;
                AudioUserPreferences.ClearVerificationProfile();AudioUserPreferences.UseVerificationProfile(false);
                Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"envelope.csv"),trace);notes.Add(recorder.Save(Path.Combine(output,"listener-output.wav")));Object.Destroy(recorder);notes.Add("passed="+passed);File.WriteAllLines(Path.Combine(output,"verification.txt"),notes);
            }
            complete(passed);
        }
    }
}
