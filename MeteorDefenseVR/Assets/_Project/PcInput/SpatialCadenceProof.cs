using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MeteorDefenseVR.Audio;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace MeteorDefenseVR.PcInput
{
    public static class SpatialCadenceProof
    {
        public static IEnumerator Run(string output,bool validate,Action<bool> complete)
        {
            var spawner=Object.FindAnyObjectByType<MeteorSpawner>();var warnings=Object.FindAnyObjectByType<ThreatIndicatorView>();
            var router=Object.FindAnyObjectByType<GazeInputRouter>();var reset=Object.FindAnyObjectByType<OperationsResetController>();
            var weapon=Object.FindAnyObjectByType<LaserWeapon>();var audio=Object.FindAnyObjectByType<AudioManager>();
            var source=(AudioSource)typeof(ThreatIndicatorView).GetField("rearWarning",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(warnings);
            var filter=AudioChannelFilter.Attach(source,UserAudioChannel.Effects);filter.Measure=true;
            bool priorInput=router.InputSuspended,priorFire=weapon.AutoFireOnLock,passed=true;
            var notes=new List<string>{"Actual pooled meteor/real velocity, manually stepped deterministic position. Real spatial Warning source and DSP. Not natural play/listening/HMD."};
            var trace=new List<string>{"elapsed,ttc,acceptedCount,sourcePlaying,sourceGain,filterGain,outputRms"};
            var times=new List<float>();MeteorController meteor=null;
            void Check(bool ok,string name){notes.Add(name+"="+ok);if(validate)passed&=ok;}
            try
            {
                router.InputSuspended=true;weapon.AutoFireOnLock=false;reset.ResetExperience();
                AudioUserPreferences.ClearVerificationProfile();AudioUserPreferences.UseVerificationProfile(true);
                GameFlowManager.Instance.StartMainGame();spawner.BeginScenario(2);audio.StopAll();
                Vector3 direction=new Vector3(1,0,1).normalized;
                meteor=spawner.SpawnScenario(spawner.ShipCenter+direction*60,-direction*3,3,10);meteor.enabled=false;
                float radius=spawner.ShipCollisionRadius+meteor.CollisionRadius;
                float start=Time.realtimeSinceStartup;int count=warnings.RearWarnings;
                filter.ResetMeasurement();
                while(Time.realtimeSinceStartup-start<18)
                {
                    float elapsed=Time.realtimeSinceStartup-start;float ttc=Mathf.Lerp(17,2,elapsed/18);
                    meteor.transform.position=spawner.ShipCenter+direction*(radius+3*ttc);
                    yield return null;
                    if(warnings.RearWarnings!=count){times.Add(Time.realtimeSinceStartup-start);count=warnings.RearWarnings;}
                    trace.Add($"{Time.realtimeSinceStartup-start},{ttc},{count},{source.isPlaying},{source.volume},{filter.AppliedGain},{filter.OutputRms}");
                }
                var gaps=times.Skip(1).Select((t,i)=>t-times[i]).ToArray();
                Check(times.Count>=5&&filter.PeakOutputRms>0,"real approach warnings and actual DSP output");
                Check(gaps.Length>=3&&gaps.Last()<gaps.First()-.2f,"approach reduces actual accepted gaps");
                Check(gaps.All(g=>g>=1.45f),"bounded minimum gap");notes.Add("acceptedTimes="+string.Join(",",times));notes.Add("gaps="+string.Join(",",gaps));
                for(int i=1;i<gaps.Length;i++)Check(gaps[i]<=gaps[i-1]+.08f,"accepted approach gaps monotonic "+i);
                count=warnings.RearWarnings;meteor.SetMovementDirection(direction);
                yield return new WaitForSecondsRealtime(1.8f);Check(warnings.RearWarnings==count&&!source.isPlaying,"receding stops warning");
                meteor.transform.position=spawner.ShipCenter+Vector3.right*30;meteor.SetMovementDirection(new Vector3(-.5f,0,1));
                yield return new WaitForSecondsRealtime(1.8f);Check(warnings.RearWarnings==count&&!source.isPlaying,"closing but miss trajectory silent");
                meteor.SetMovementDirection(-direction);meteor.transform.position=spawner.ShipCenter+direction*(radius+3*19);
                yield return new WaitForSecondsRealtime(1.8f);Check(warnings.RearWarnings==count&&!source.isPlaying,"safe horizon silent");
                float jitterStart=Time.realtimeSinceStartup;int frame=0;
                while(Time.realtimeSinceStartup-jitterStart<2)
                {meteor.transform.position=spawner.ShipCenter+direction*(radius+3*(frame++%2==0?17.99f:18.01f));yield return null;}
                Check(warnings.RearWarnings==count,"boundary jitter does not trigger burst");
                meteor.transform.position=spawner.ShipCenter+direction*(radius+3*2);
                var second=spawner.SpawnScenario(spawner.ShipCenter-direction*(radius+3*4),direction*3,3,10);second.enabled=false;
                yield return new WaitForSecondsRealtime(.35f);
                var selected=(MeteorController)typeof(ThreatIndicatorView).GetField("playingThreat",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(warnings);
                Check(warnings.RearWarnings==count+1&&selected==meteor,"multiple threats select single nearest impact");count=warnings.RearWarnings;
                meteor.SetMovementDirection(direction);yield return new WaitForSecondsRealtime(.25f);
                Check(warnings.RearWarnings==count,"target switch cannot immediately replay");
                second.ReceiveDamage(999);yield return new WaitForSecondsRealtime(1.8f);
                Check(warnings.RearWarnings==count&&!source.isPlaying,"destroyed target stops without pending cue");
                meteor.SetMovementDirection(-direction);
                AudioUserPreferences.SetVolume(UserAudioChannel.Effects,0);filter.ResetMeasurement();yield return new WaitForSecondsRealtime(2);
                Check(filter.PeakInputRms>0&&filter.PeakOutputRms==0,"actual spatial output respects effects zero");
                AudioUserPreferences.SetVolume(UserAudioChannel.Effects,.6f);AudioUserPreferences.SetMuted(UserAudioChannel.Effects,true);
                filter.ResetMeasurement();yield return new WaitForSecondsRealtime(2);
                Check(filter.PeakInputRms>0&&filter.PeakOutputRms==0,"actual spatial output respects effects mute");
                AudioUserPreferences.SetMuted(UserAudioChannel.Effects,false);Check(Mathf.Abs(filter.AppliedGain-.6f)<.001f,"effects preference restored");
                count=warnings.RearWarnings;Time.timeScale=0;AudioListener.pause=true;yield return new WaitForSecondsRealtime(2);
                Check(warnings.RearWarnings==count,"pause produces no scheduled warnings");Time.timeScale=1;AudioListener.pause=false;
                yield return new WaitForSecondsRealtime(.3f);Check(warnings.RearWarnings==count,"resume has no overdue warning burst");
                yield return new WaitForSecondsRealtime(1.4f);Check(warnings.RearWarnings>count,"resume recomputes live warning");
                GameFlowManager.Instance.ShowResult();yield return null;count=warnings.RearWarnings;
                yield return new WaitForSecondsRealtime(1.8f);Check(warnings.RearWarnings==count&&!source.isPlaying,"result clears spatial warning");
                reset.ResetExperience();yield return new WaitForSecondsRealtime(.2f);Check(!source.isPlaying,"restart clears spatial source");
            }
            finally
            {
                AudioListener.pause=false;Time.timeScale=1;spawner.EndScenario();reset.ResetExperience();router.InputSuspended=priorInput;weapon.AutoFireOnLock=priorFire;
                filter.Measure=false;AudioUserPreferences.ClearVerificationProfile();AudioUserPreferences.UseVerificationProfile(false);
                Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"timeline.csv"),trace);notes.Add("passed="+passed);File.WriteAllLines(Path.Combine(output,"verification.txt"),notes);
            }
            complete(passed);
        }
    }
}
