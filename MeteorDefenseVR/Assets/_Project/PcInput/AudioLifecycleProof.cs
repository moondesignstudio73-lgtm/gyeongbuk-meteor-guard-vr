using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Audio;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.PowerUps;
using MeteorDefenseVR.Meteor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace MeteorDefenseVR.PcInput
{
    public static class AudioLifecycleProof
    {
        public static IEnumerator Run(GazeInputRouter router,string output,Action<bool> complete)
        {
            var flow=GameFlowManager.Instance;var manager=Object.FindAnyObjectByType<AudioManager>();
            var health=Object.FindAnyObjectByType<PlayerHealth>();var reset=Object.FindAnyObjectByType<OperationsResetController>();
            var sources=Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include);
            var filters=Object.FindObjectsByType<AudioChannelFilter>(FindObjectsInactive.Include);
            var bgm=sources.Single(manager.OwnsBgmSource);var notes=new List<string>();var timeline=new List<string>{"seconds,segment,state,paused,hp,source,playing,volume,inputRms,outputRms"};
            bool passed=true;string segment="setup";float start=Time.realtimeSinceStartup;int warnings=0;
            void Check(bool ok,string message){passed &= ok;notes.Add(message+"="+ok);}
            void Cue(AudioCue cue,AudioCategory category){notes.Add($"t={Time.realtimeSinceStartup-start:F3} segment={segment} state={flow.CurrentState} hp={health.CurrentHealth} cue={cue} category={category}");if(cue==AudioCue.Warning||cue==AudioCue.BossWarning)warnings++;}
            IEnumerator Observe(string name,float seconds)
            {
                segment=name;float until=Time.realtimeSinceStartup+seconds;
                while(Time.realtimeSinceStartup<until)
                {
                    foreach(var source in sources){var filter=source.GetComponent<AudioChannelFilter>();timeline.Add(FormattableString.Invariant($"{Time.realtimeSinceStartup-start:F3},{name},{flow.CurrentState},{AudioListener.pause},{health.CurrentHealth},{source.name},{source.isPlaying},{source.volume},{filter?.InputRms??0},{filter?.OutputRms??0}"));}
                    yield return new WaitForSecondsRealtime(.1f);
                }
            }
            try
            {
                notes.Add("Controlled real scene states/health events and real-time DSP source timeline; not natural human play or human listening. Source RMS cannot prove perceptual masking or final speaker loudness.");
                foreach(var filter in filters){filter.Measure=true;filter.ResetMeasurement();}manager.CuePlaybackRequested+=Cue;
                reset.ResetExperience();yield return Observe("boot",2);
                flow.StartMainGame();yield return Observe("combat",1.3f);
                // Apply the real damage interceptor, then actual damage after restoring it.
                var powerups=Object.FindAnyObjectByType<PowerUpManager>();float before=health.CurrentHealth;int count=warnings;
                Check(powerups.Activate(PowerUpType.Shield),"actual shield activated");
                Check(!health.ApplyImpactDamage(10,health.transform.position),"shield hit rejects hull damage");yield return Observe("shield-hit",.5f);
                Check(health.CurrentHealth==before&&Object.FindAnyObjectByType<CockpitDamagePresentation>().DamageLevel==0,"shield hit leaves hull healthy; spatial warnings remain valid");powerups.ResetSystem();
                health.RestoreCheckpoint(health.MaxHealth);health.ApplyImpactDamage(Mathf.RoundToInt(health.MaxHealth*.85f),health.transform.position);
                count=warnings;yield return Observe("critical-combat",7.2f);Check(warnings>count&&warnings-count<=4,"critical warnings present and bounded");
                // Observe a real BGM transition across a listener pause boundary.
                flow.StartBoss();Object.FindAnyObjectByType<BossClimaxController>().BeginClimax();yield return Observe("boss-transition",.1f);
                router.Simulation.Pause();float volume=bgm.volume;var clip=bgm.clip;int starts=manager.BgmPlaybackStarts;count=warnings;
                yield return Observe("paused-transition",1.2f);
                Check(AudioListener.pause&&bgm.clip==clip&&Mathf.Abs(bgm.volume-volume)<.00001f&&manager.BgmPlaybackStarts==starts,"paused music transition freezes clip and fade");
                Check(warnings==count,"no new warnings during pause");router.Simulation.Resume();yield return Observe("boss-resumed",1.5f);
                Check(!manager.IsBgmTransitioning,"music transition finishes after resume");
                var boss=Object.FindAnyObjectByType<BossClimaxController>();float deadline=Time.realtimeSinceStartup+8;while(boss.ActiveBoss==null&&!boss.IsComplete&&Time.realtimeSinceStartup<deadline)yield return Observe("boss-approach",.2f);
                if(boss.ActiveBoss!=null&&boss.ActiveBoss.IsTargetable)boss.ActiveBoss.ReceiveDamage(boss.ActiveBoss.MaxHealth);
                yield return Observe("victory-tail",.5f);Check(flow.CurrentState==GameState.MissionComplete||flow.CurrentState==GameState.Result,"boss destruction drives victory state");count=warnings;yield return Observe("victory",1.5f);Check(warnings==count,"victory stops recurring danger warnings");
                flow.ShowResult();yield return Observe("victory-result",1);reset.ResetExperience();yield return Observe("restart",1.2f);
                flow.StartMainGame();yield return Observe("second-combat",.5f);health.RestoreCheckpoint(health.MaxHealth);health.Tick(1);health.ApplyImpactDamage(Mathf.RoundToInt(health.MaxHealth),health.transform.position);
                yield return Observe("failure-return-tail",.5f);count=warnings;yield return Observe("failure-return-result",6);
                Check(health.IsMissionFailed&&warnings==count,"failure return stops recurring danger warnings");
                reset.ResetExperience();yield return Observe("final-reset",1.2f);
                Check(!sources.Any(s=>s.isPlaying&&s.loop&&!manager.OwnsBgmSource(s)&&!manager.OwnsAmbientSource(s)),"reset has no lingering effect loops");
            }
            finally
            {
                router.Simulation.Resume();manager.CuePlaybackRequested-=Cue;foreach(var filter in filters)if(filter!=null)filter.Measure=false;reset.ResetExperience();
                Directory.CreateDirectory(output);notes.Add("passed="+passed);File.WriteAllLines(Path.Combine(output,"verification.txt"),notes);File.WriteAllLines(Path.Combine(output,"source-timeline.csv"),timeline);
            }
            complete(passed);
        }
    }
}
