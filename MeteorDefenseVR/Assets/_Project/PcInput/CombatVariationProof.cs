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
    public static class CombatVariationProof
    {
        public static IEnumerator Run(string output,bool validate,Action<bool> complete)
        {
            var manager=Object.FindAnyObjectByType<AudioManager>();
            var reset=Object.FindAnyObjectByType<OperationsResetController>();
            var router=Object.FindAnyObjectByType<GazeInputRouter>();bool suspended=router.InputSuspended,passed=true;
            var notes=new List<string>{"Existing clips only. Internal listener recording, not human listening or OS/speaker/HMD validation. No multiple-clip avoidance claim."};
            var trace=new List<string>{"cue,index,clip,pitch,sourceGain,baselineGain,admitted"};
            var library=(AudioCueLibrary)typeof(AudioManager).GetField("cueLibrary",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(manager);
            AudioCueEntry Entry(AudioCue cue)=>library.Entries.First(e=>e.Cue==cue);
            var recorder=Camera.main.gameObject.AddComponent<CampaignOutputRecorder>();
            var externalRoot=new GameObject("QACombatVariationExternal");var external=externalRoot.AddComponent<AudioSource>();external.playOnAwake=false;external.volume=.25f;external.pitch=1.1f;
            void Check(bool ok,string label){notes.Add(label+"="+ok);if(validate)passed&=ok;}
            AudioSource Active(AudioClip clip)=>manager.GetComponentsInChildren<AudioSource>().FirstOrDefault(s=>s.isPlaying&&s.clip==clip);
            try
            {
                router.InputSuspended=true;reset.ResetExperience();manager.StopAll();
                AudioUserPreferences.ClearVerificationProfile();AudioUserPreferences.UseVerificationProfile(true);
                yield return new WaitForSecondsRealtime(.2f);recorder.Begin();
                foreach(var cue in new[]{AudioCue.Laser,AudioCue.MeteorHit,AudioCue.MeteorExplosion})
                {
                    var entry=Entry(cue);var pitches=new HashSet<float>();bool bounded=true;
                    for(int i=0;i<6;i++)
                    {
                        bool admitted=manager.TryPlay(cue);var source=Active(entry.Clip);
                        Check(admitted&&source!=null,cue+" actual repeat "+i);
                        if(source!=null)
                        {
                            float baseline=entry.Volume*manager.SfxOutputGain;pitches.Add(source.pitch);
                            bounded&=source.pitch>=.9699f&&source.pitch<=1.0301f&&source.volume<=baseline+.0001f&&source.volume>=baseline*.9399f;
                            trace.Add($"{cue},{i},{entry.Clip.name},{source.pitch},{source.volume},{baseline},{admitted}");
                        }
                        yield return new WaitForSecondsRealtime(Mathf.Max(entry.Cooldown+.05f,entry.Clip.length/.97f+.08f));
                    }
                    Check(bounded,cue+" bounded and never louder than baseline");Check(pitches.Count>1,cue+" actual pitch varies");
                }
                notes.Add(recorder.Save(Path.Combine(Ensure(output),"listener-output.wav")));
                manager.StopAll();
                var state=typeof(AudioManager).GetField("combatVariationState",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                uint State()=>state==null?0:(uint)state.GetValue(manager);
                manager.TryPlay(AudioCue.Laser,100);uint before=State();var laser=Active(Entry(AudioCue.Laser).Clip);float pitch=laser.pitch,gain=laser.volume;
                Check(!manager.TryPlay(AudioCue.Laser,100.001)&&State()==before&&laser.pitch==pitch&&laser.volume==gain,"cooldown rejection leaves choice and playback unchanged");
                manager.StopAll();for(int i=0;i<8;i++)manager.TryPlay(AudioCue.MeteorExplosion,200+i);
                before=State();Check(!manager.TryPlay(AudioCue.Laser,300)&&State()==before&&manager.ActiveTransientCount==8,"cap rejection leaves choice and eight voices unchanged");
                Check(manager.TryPlay(AudioCue.Warning,400),"warning preempts full low-priority pool");
                var warning=Active(Entry(AudioCue.Warning).Clip);Check(warning!=null&&warning.pitch==1,"pooled warning inherits no pitch variation");
                manager.StopAll();var original=Entry(AudioCue.MeteorHit).Clip;external.clip=original;
                Check(manager.TryPlayEffect(external,original,AudioCue.MeteorHit),"external combat path accepted");
                Check(external.pitch>=1.1f*.97f&&external.pitch<=1.1f*1.03f&&external.volume<=.25f,"external authored pitch and maximum gain preserved");
                manager.StopAll();Check(external.clip==original&&external.pitch==1.1f&&external.volume==.25f,"external state restored on stop");
                manager.TryPlayEffect(external,Entry(AudioCue.Warning).Clip,AudioCue.Warning);
                Check(external.pitch==1.1f&&external.volume==.25f,"external warning inherits no variation");manager.StopAll();
                foreach(var cue in new[]{AudioCue.Countdown,AudioCue.Launch,AudioCue.CalibrationComplete,AudioCue.BossWarning})
                {
                    manager.TryPlay(cue,500);var source=Active(Entry(cue).Clip);
                    Check(source!=null&&source.pitch==1,cue+" excluded");manager.StopAll();
                }
                before=State();AudioListener.pause=true;Check(!manager.TryPlay(AudioCue.Laser,600)&&State()==before,"paused request does not choose variation");AudioListener.pause=false;
                manager.TryPlay(AudioCue.Laser,601);manager.ResetForNewSession();Check(manager.ActiveTransientCount==0,"reset clears combat voices");
            }
            finally
            {
                AudioListener.pause=false;Time.timeScale=1;manager.StopAll();reset.ResetExperience();router.InputSuspended=suspended;
                AudioUserPreferences.ClearVerificationProfile();AudioUserPreferences.UseVerificationProfile(false);
                Object.Destroy(externalRoot);Object.Destroy(recorder);Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"playback.csv"),trace);
                notes.Add("passed="+passed);File.WriteAllLines(Path.Combine(output,"verification.txt"),notes);
            }
            complete(passed);
        }
        private static string Ensure(string output){Directory.CreateDirectory(output);return output;}
    }
}
