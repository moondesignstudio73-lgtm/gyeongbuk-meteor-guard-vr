using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.PowerUps;
using MeteorDefenseVR.Player;
using UnityEngine;
using Object = UnityEngine.Object;
namespace MeteorDefenseVR.PcInput
{
    public static class PowerUpFeedbackProof
    {
        public static IEnumerator Run(GazeInputRouter router, string output, Action<bool> complete)
        {
            var camera=Camera.main; var flow=GameFlowManager.Instance; var reset=Object.FindAnyObjectByType<OperationsResetController>();
            var spawner=Object.FindAnyObjectByType<MeteorSpawner>(); var weapon=Object.FindAnyObjectByType<LaserWeapon>();
            var gaze=Object.FindAnyObjectByType<GazeRaycaster>(); var manager=PowerUpManager.Instance;
            var health=Object.FindAnyObjectByType<PlayerHealth>(); var policy=health.Policy;
            var feedback=Object.FindAnyObjectByType<PowerUpWeaponFeedback>();
            if(camera==null||flow==null||reset==null||spawner==null||weapon==null||gaze==null||manager==null||feedback==null)
                throw new InvalidOperationException($"Fixture references: camera={camera!=null} flow={flow!=null} reset={reset!=null} spawner={spawner!=null} weapon={weapon!=null} gaze={gaze!=null} manager={manager!=null} feedback={feedback!=null}");
            bool input=router.InputSuspended, fire=weapon.AutoFireOnLock, looking=gaze.enabled;
            router.InputSuspended=true; weapon.AutoFireOnLock=false; gaze.enabled=false;
            var pose=camera.transform.position;var rotation=camera.transform.rotation; int mask=camera.cullingMask;
            bool passed=true;var notes=new List<string>{"Actual manager/weapon/spawner fixture; fixed targets, real player camera. Not human or HMD verification."};
            void Check(bool ok,string name){notes.Add(name+"="+ok);passed &= ok;}
            try
            {
                foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(800,600)})
                {
                    reset.ResetExperience();flow.StartMainGame();spawner.BeginScenario();
                    yield return new WaitForSecondsRealtime(3);
                    using var proof=new PcStabilityProbe(camera,size.x,size.y);
                    var primary=spawner.SpawnScenario(camera.ViewportToWorldPoint(new Vector3(.5f,.6f,28)),Vector3.zero,2,10,100,MeteorType.Large);
                    primary.enabled=false;
                    var target=primary.GetComponent<GazeLockOnTarget>();
                    bool Shoot(){target.Advance(3);var turrets=weapon.TurretAiming;for(int i=0;i<200&&(!turrets.IsReadyFor(target)||!turrets.HasTracked(target));i++)turrets.Tick(.01f);weapon.TickCooldown(1);return weapon.TryFire(target);}
                    void Capture(string label)
                    {
                        feedback.Tick(0);Canvas.ForceUpdateCanvases();PcStabilityRenderDriver.RenderForProof(camera);
                        string folder=Path.Combine(output,$"{size.x}x{size.y}",label);Directory.CreateDirectory(folder);proof.SaveGameOnlyProof(folder,router);
                        notes.Add($"{size}/{label}: rapid={feedback.RapidRemaining:F3} multi={feedback.MultiRemaining:F3} charge={feedback.Charge:F3} pulse={feedback.AuxiliaryPulse:F3} shots={feedback.LastAdditionalShots} visible={feedback.Visible}");
                        if(!feedback.Visible)return;
                        var text=feedback.Readout;var bounds=TelemetryReadabilityProof.ViewRect(camera,(RectTransform)text.transform.parent);
                        bool clear=!bounds.Overlaps(new Rect(.42f,.4f,.16f,.2f))&&bounds.xMin>=0&&bounds.xMax<=1&&bounds.yMin>=0&&bounds.yMax<=1;
                        foreach(var meteor in spawner.ActiveMeteors)if(meteor.IsTargetable)clear &= !bounds.Overlaps(TelemetryReadabilityProof.MeteorRect(camera,meteor));
                        bool fits=!text.isTextTruncated&&text.preferredWidth<=text.rectTransform.rect.width&&text.preferredHeight<=text.rectTransform.rect.height;
                        float minGlyph=1000;
                        for(int i=0;i<text.textInfo.characterCount;i++){var c=text.textInfo.characterInfo[i];if(!c.isVisible)continue;float height=Mathf.Abs(camera.WorldToScreenPoint(text.transform.TransformPoint(c.topLeft)).y-camera.WorldToScreenPoint(text.transform.TransformPoint(c.bottomLeft)).y);minGlyph=Mathf.Min(minGlyph,height);}
                        notes.Add($"{size}/{label}: clear={clear} fits={fits} minGlyphPx={minGlyph:F2} panel={bounds}");Check(clear&&fits&&minGlyph>=12,label+" readability");
                    }
                    IEnumerator Measure(string name)
                    {
                        var samples=new double[20];var watch=new System.Diagnostics.Stopwatch();
                        for(int i=0;i<20;i++){watch.Restart();PcStabilityRenderDriver.RenderForProof(camera);watch.Stop();samples[i]=watch.Elapsed.TotalMilliseconds;yield return null;}
                        Array.Sort(samples);notes.Add($"{size}/{name}: renderCPUmedianMs={samples[10]:F4} p95={samples[18]:F4}; QA submission, not GPU benchmark");
                    }
                    yield return new WaitForSecondsRealtime(.3f);Capture("before");yield return Measure("before");
                    float normalCooldown=weapon.EffectiveCooldown;
                    manager.Activate(PowerUpType.RapidFire);feedback.Tick(0);
                    Check(feedback.RapidRemaining>0&&weapon.EffectiveCooldown<normalCooldown,"actual rapid ability");
                    yield return new WaitForSecondsRealtime(.2f);Capture("rapid");
                    manager.Activate(PowerUpType.MultiLaser);yield return new WaitForSecondsRealtime(.2f);Capture("simultaneous");
                    Check(Shoot(),"primary shot without auxiliary targets");feedback.Tick(0);Check(feedback.AuxiliaryPulse==0,"no invented additional shots");
                    var extra1=spawner.SpawnScenario(primary.transform.position+new Vector3(2,0,0),Vector3.zero,1,10,1,MeteorType.Normal);extra1.enabled=false;
                    var extra2=spawner.SpawnScenario(primary.transform.position+new Vector3(-2,0,0),Vector3.zero,1,10,1,MeteorType.Normal);extra2.enabled=false;
                    Check(Shoot(),"real primary and two lethal auxiliary hits");feedback.Tick(0);
                    Check(!extra1.IsTargetable&&!extra2.IsTargetable&&feedback.LastAdditionalShots==2&&feedback.AuxiliaryPulse>0,"actual two additional hits and pulse");
                    Check(feedback.Charge<.01f,"rapid charge follows cooldown");Capture("extra-shot");
                    Time.timeScale=0;float rapid=feedback.RapidRemaining,multi=feedback.MultiRemaining,pulse=feedback.AuxiliaryPulse;
                    yield return new WaitForSecondsRealtime(.25f);Check(feedback.RapidRemaining==rapid&&feedback.MultiRemaining==multi&&feedback.AuxiliaryPulse==pulse,"pause freezes timers and pulse");Capture("paused");Time.timeScale=1;
                    yield return new WaitForSecondsRealtime(.3f);Check(feedback.AuxiliaryPulse==0&&feedback.Charge>.99f,"shot pulse clears and weapon recharges");
                    manager.Tick(Mathf.Min(feedback.RapidRemaining,feedback.MultiRemaining)-1.5f);feedback.Tick(0);Capture("ending");Check(feedback.EndingSoon,"real remaining time end warning");
                    manager.Activate(PowerUpType.RapidFire);manager.Activate(PowerUpType.MultiLaser);feedback.Tick(0);Check(!feedback.EndingSoon&&feedback.RapidRemaining>2&&feedback.MultiRemaining>2,"refresh clears ending warning");Capture("refreshed");
                    yield return Measure("active");
                    feedback.Tick(0);var timer=new System.Diagnostics.Stopwatch();long bytes=GC.GetAllocatedBytesForCurrentThread();timer.Start();for(int i=0;i<1000;i++)feedback.Tick(.001f);timer.Stop();bytes=GC.GetAllocatedBytesForCurrentThread()-bytes;
                    notes.Add($"feedback1000TicksBytes={bytes} cpuMs={timer.Elapsed.TotalMilliseconds:F4} (steady feedback only)");
                    manager.Tick(30);feedback.Tick(0);Check(!feedback.Visible&&feedback.AuxiliaryPulse==0&&Mathf.Approximately(weapon.EffectiveCooldown,normalCooldown),"expiration restores normal ability and layout");Capture("expired");
                    int objects=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length,materials=Resources.FindObjectsOfTypeAll<Material>().Length;
                    for(int i=0;i<30;i++){manager.Activate(PowerUpType.RapidFire);manager.Activate(PowerUpType.MultiLaser);feedback.Tick(0);manager.ResetSystem();feedback.Tick(0);}
                    Check(objects==Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length&&materials==Resources.FindObjectsOfTypeAll<Material>().Length&&!feedback.Visible,"30 reuses no object or material growth");
                    manager.Activate(PowerUpType.RapidFire);manager.Activate(PowerUpType.MultiLaser);feedback.Tick(0);health.SetPolicy(GameOverPolicy.NormalMode);health.ApplyImpactDamage(100,health.transform.position);feedback.Tick(0);
                    Check(!feedback.Visible&&feedback.AuxiliaryPulse==0&&health.CurrentHealth==0,"actual defeat hides feedback");Capture("result-cleared");
                    reset.ResetExperience();feedback.Tick(0);Check(manager.ActiveBuffCount==0&&!feedback.Visible&&Mathf.Approximately(weapon.EffectiveCooldown,normalCooldown),"restart clears abilities and indicators");
                    Check(camera.transform.position==pose&&camera.transform.rotation==rotation&&camera.cullingMask==mask,"camera and turret hiding preserved");
                    spawner.EndScenario();
                }
            }
            finally{Time.timeScale=1;reset.ResetExperience();health.SetPolicy(policy);router.InputSuspended=input;weapon.AutoFireOnLock=fire;gaze.enabled=looking;Directory.CreateDirectory(output);notes.Add("passed="+passed);File.WriteAllLines(Path.Combine(output,"verification.txt"),notes);}
            complete(passed);
        }
    }
}
