using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.VFX;
using MeteorDefenseVR.Visual;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MeteorDefenseVR.PcInput
{
    public static class BossShellProof
    {
        public static IEnumerator Run(GazeInputRouter router,string output,Action<bool> complete)
        {
            var camera=Camera.main;var flow=GameFlowManager.Instance;var reset=Object.FindAnyObjectByType<OperationsResetController>();
            var climax=Object.FindAnyObjectByType<BossClimaxController>();var spawner=Object.FindAnyObjectByType<MeteorSpawner>();
            var weapon=Object.FindAnyObjectByType<LaserWeapon>();var gaze=Object.FindAnyObjectByType<GazeRaycaster>();
            var effects=Object.FindAnyObjectByType<PremiumCombatVfx>();var events=Object.FindAnyObjectByType<CombatVfxController>();
            bool input=router.InputSuspended,fire=weapon.AutoFireOnLock,looking=gaze.enabled;
            router.InputSuspended=true;weapon.AutoFireOnLock=false;gaze.enabled=false;
            Vector3 pose=camera.transform.position;Quaternion rotation=camera.transform.rotation;
            var notes=new List<string>{"Actual prepared boss/weapon/event fixture at fixed forward position; 3HP, movement frozen for comparison. No camera pose change; not human/HMD play."};
            bool passed=true;MeteorController boss=null;int deathEvents=0;
            void Death(Vector3 p)=>deathEvents++;
            events.BossExplosionVfxPlayed+=Death;
            try
            {
                foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(800,600)})
                {
                    reset.ResetExperience();flow.StartMainGame();spawner.StopSpawning(true);climax.BeginClimax();
                    float deadline=Time.realtimeSinceStartup+18;while(climax.ActiveBoss==null&&Time.realtimeSinceStartup<deadline)yield return null;
                    boss=climax.ActiveBoss;if(boss==null)throw new InvalidOperationException("Boss did not spawn");
                    boss.Configure(MeteorType.Boss,3,0,30,3000,3);boss.Spawn();boss.enabled=false;
                    var motion=boss.GetComponent<BossStageMotion>();if(motion!=null)motion.enabled=false;
                    boss.transform.SetPositionAndRotation(camera.ViewportToWorldPoint(new Vector3(.5f,.60f,12)),Quaternion.identity);
                    var view=boss.GetComponent<BossMeteorView>();var shell=view.Shell;if(shell==null)throw new InvalidOperationException("Boss shell not built");
                    var target=boss.GetComponent<GazeLockOnTarget>();
                    using var proof=new PcStabilityProbe(camera,size.x,size.y);
                    void Capture(string label)
                    {
                        Canvas.ForceUpdateCanvases();PcStabilityRenderDriver.RenderForProof(camera);
                        string folder=Path.Combine(output,$"{size.x}x{size.y}",label);Directory.CreateDirectory(folder);proof.SaveGameOnlyProof(folder,router);
                        notes.Add($"{size}/{label}: hp={boss.CurrentHealth}/{boss.MaxHealth} stage={shell.Stage} shed={shell.ShedCount} fragments={shell.ActiveFragments} core={shell.CoreVisible} state={boss.State}");
                    }
                    bool Shoot(){target.Advance(3);var turrets=weapon.TurretAiming;for(int i=0;i<200&&(!turrets.IsReadyFor(target)||!turrets.HasTracked(target));i++)turrets.Tick(.01f);weapon.TickCooldown(1);return weapon.TryFire(target);}
                    IEnumerator Measure(string label)
                    {
                        var samples=new double[20];var timer=new System.Diagnostics.Stopwatch();
                        for(int i=0;i<samples.Length;i++){timer.Restart();PcStabilityRenderDriver.RenderForProof(camera);timer.Stop();samples[i]=timer.Elapsed.TotalMilliseconds;yield return null;}
                        Array.Sort(samples);notes.Add($"{size}/{label}: renderSubmissionMedianMs={samples[10]:F4} p95Ms={samples[18]:F4} objects={Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length} materials={Resources.FindObjectsOfTypeAll<Material>().Length} effects={effects.ActiveParticleCount}; CPU submission with QA, not GPU benchmark");
                    }
                    yield return new WaitForSecondsRealtime(.3f);Capture("intact");
                    yield return Measure("intact-cost");
                    bool invalid=true;foreach(float hit in new[]{0,-1,float.NaN,float.PositiveInfinity})invalid &= !boss.ReceiveDamage(hit);
                    invalid &= !weapon.TryFire(null)&&shell.Stage==0;
                    var clock=new System.Diagnostics.Stopwatch();long alloc=GC.GetAllocatedBytesForCurrentThread();clock.Start();for(int i=0;i<1000;i++)shell.Tick(.001f);clock.Stop();alloc=GC.GetAllocatedBytesForCurrentThread()-alloc;
                    notes.Add($"{size}: intact1000TicksBytes={alloc} cpuMs={clock.Elapsed.TotalMilliseconds:F4}");
                    bool first=Shoot();yield return new WaitForSecondsRealtime(.12f);Capture("first-hit-fragments");
                    yield return new WaitForSecondsRealtime(.65f);Capture("breached");
                    first &= boss.CurrentHealth==2&&shell.Stage==1&&shell.ShedCount==2&&shell.CoreVisible&&shell.ActiveFragments==0;
                    bool second=Shoot();yield return new WaitForSecondsRealtime(.75f);Capture("core-exposed");
                    second &= boss.CurrentHealth==1&&shell.Stage==2&&shell.ShedCount==4&&shell.CoreVisible&&shell.ActiveFragments==0;
                    yield return new WaitForSecondsRealtime(.6f);yield return Measure("exposed-cost");
                    clock.Restart();alloc=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++)shell.Tick(.001f);clock.Stop();alloc=GC.GetAllocatedBytesForCurrentThread()-alloc;
                    notes.Add($"{size}: damaged1000TicksBytes={alloc} cpuMs={clock.Elapsed.TotalMilliseconds:F4} ownedTransforms={shell.Root.GetComponentsInChildren<Transform>(true).Length} meshes={shell.Root.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh).Distinct().Count()} materials={shell.Root.GetComponentsInChildren<Renderer>(true).Select(r=>r.sharedMaterial).Distinct().Count()}");
                    bool noTimerChange=shell.Stage==2;
                    int objects=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
                    int materials=Resources.FindObjectsOfTypeAll<Material>().Length;
                    for(int i=0;i<20;i++){boss.ResetMeteor(false);boss.Configure(MeteorType.Boss,9,0,30,3000,3);boss.Spawn();boss.ReceiveDamage(6);shell.Tick(1);boss.ReceiveDamage(.1f);passed &= shell.Stage==2&&shell.ActiveFragments==0;}
                    boss.ResetMeteor(false);boss.Configure(MeteorType.Boss,3,0,30,3000,3);boss.Spawn();Capture("pooled-intact");
                    bool pooled=shell.Stage==0&&!shell.CoreVisible&&shell.ActiveFragments==0&&Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==objects&&Resources.FindObjectsOfTypeAll<Material>().Length==materials;
                    boss.ReceiveDamage(2);yield return new WaitForSecondsRealtime(.7f);Capture("large-hit-skip");
                    bool skipped=shell.Stage==2&&shell.ShedCount==4&&boss.CurrentHealth==1;
                    int before=deathEvents;bool final=Shoot();yield return new WaitForSecondsRealtime(.15f);Capture("destroyed-burst");
                    int fragments=Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Where(p=>p.name.StartsWith("RockFragments")).Sum(p=>p.particleCount);
                    bool once=deathEvents==before+1&&view.ExplosionCount==1&&!boss.ReceiveDamage(1)&&!boss.DestroyMeteor();
                    yield return new WaitForSecondsRealtime(1.4f);Capture("destroyed-clear");
                    int remainingFragments=Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Where(p=>p.name.StartsWith("RockFragments")).Sum(p=>p.particleCount);
                    bool cleared=!boss.gameObject.activeSelf&&!shell.CoreVisible&&shell.ActiveFragments==0&&effects.LastBossFragmentCount==8&&fragments==8&&remainingFragments==0;
                    bool stable=pose==camera.transform.position&&rotation==camera.transform.rotation;
                    bool valid=invalid&&first&&second&&noTimerChange&&pooled&&skipped&&final&&once&&cleared&&stable&&alloc==0;
                    notes.Add($"{size}: invalid={invalid} hits={first}/{second}/{final} noTimer={noTimerChange} pooled={pooled} skipped={skipped} once={once} cleared={cleared} finalFragments={fragments} remainingFragments={remainingFragments} cameraStable={stable} valid={valid}");passed &= valid;
                    boss.enabled=true;if(motion!=null)motion.enabled=true;
                }
                reset.ResetExperience();flow.StartMainGame();spawner.StopSpawning(true);climax.BeginClimax();
                float resetDeadline=Time.realtimeSinceStartup+18;while(climax.ActiveBoss==null&&Time.realtimeSinceStartup<resetDeadline)yield return null;
                boss=climax.ActiveBoss;if(boss==null)throw new InvalidOperationException("Reset fixture boss did not spawn");
                boss.ReceiveDamage(boss.MaxHealth*.7f);bool exposed=boss.GetComponent<BossMeteorView>().Shell.Stage==2;
                reset.ResetExperience();yield return null;
                bool restart=exposed&&!boss.gameObject.activeSelf&&!boss.GetComponent<BossMeteorView>().Shell.CoreVisible&&boss.GetComponent<BossMeteorView>().Shell.Stage==0;
                notes.Add("activeCoreRestartCleared="+restart);passed &= restart;
            }
            finally
            {
                if(boss!=null)boss.enabled=true;events.BossExplosionVfxPlayed-=Death;
                reset.ResetExperience();router.InputSuspended=input;weapon.AutoFireOnLock=fire;gaze.enabled=looking;
                Directory.CreateDirectory(output);notes.Add("passed="+passed);File.WriteAllLines(Path.Combine(output,"verification.txt"),notes);
            }
            complete(passed);
        }
    }
}
