using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.PcInput
{
    public static class MeteorDamageProof
    {
        public static IEnumerator Run(GazeInputRouter router, string output, bool validate, Action<bool> complete)
        {
            var camera = Camera.main;
            var spawner = Object.FindAnyObjectByType<MeteorSpawner>();
            var weapon = Object.FindAnyObjectByType<LaserWeapon>();
            var gaze = Object.FindAnyObjectByType<GazeRaycaster>();
            bool input = router.InputSuspended, fire = weapon.AutoFireOnLock, looking = gaze.enabled;
            router.InputSuspended = true; weapon.AutoFireOnLock = false; gaze.enabled = false;
            var notes = new List<string> { "Explicit real weapon/spawner fixture with frozen large meteor; player camera, no webcam; not human/HMD/novice perception testing." };
            bool passed = true;
            var used = new List<MeteorController>();
            try
            {
                GameFlowManager.Instance.StartMainGame(); spawner.BeginScenario();
                yield return new WaitForSecondsRealtime(5);
                foreach (var size in new[] { new Vector2Int(1280,720), new Vector2Int(800,600) })
                {
                    using var proof = new PcStabilityProbe(camera, size.x, size.y);
                    var meteor = spawner.SpawnScenario(camera.ViewportToWorldPoint(new Vector3(.62f,.60f,18)), Vector3.zero, 3.2f, 20, 3, MeteorType.Large);
                    meteor.Configure(MeteorType.Large,3,0,20,300,3.2f); meteor.Spawn(); meteor.enabled = false;
                    used.Add(meteor);
                    var surface=meteor.GetComponent<MeteorDamageSurface>();
                    int surfaceCount=surface.SurfaceCount;
                    bool lodCorrect=surfaceCount==3 && meteor.GetComponent<LODGroup>().GetLODs().All(l=>l.renderers.Length==2);
                    bool died=false;
                    void Death(MeteorController m) => died=m.State==MeteorLifecycleState.Destroyed && m.CurrentHealth==0;
                    meteor.Destroyed+=Death;
                    var target = meteor.GetComponent<GazeLockOnTarget>();
                    void Capture(string label)
                    {
                        Canvas.ForceUpdateCanvases(); PcStabilityRenderDriver.RenderForProof(camera);
                        string folder = Path.Combine(output,$"{size.x}x{size.y}",label); Directory.CreateDirectory(folder);
                        proof.SaveGameOnlyProof(folder,router);
                        notes.Add($"{size}/{label}: health={meteor.CurrentHealth} state={meteor.State} active={meteor.gameObject.activeSelf} damage={surface.DamageFraction} surfaces={surface.SurfaceCount}");
                    }
                    bool Shoot()
                    {
                        target.Advance(3); var turrets=weapon.TurretAiming;
                        for(int i=0;i<200&&(!turrets.IsReadyFor(target)||!turrets.HasTracked(target));i++)turrets.Tick(.01f);
                        weapon.TickCooldown(1); return weapon.TryFire(target);
                    }
                    yield return new WaitForSecondsRealtime(.3f); Capture("intact");
                    bool intact=surface.DamageFraction==0;
                    bool miss = !weapon.TryFire(null) && !meteor.ReceiveDamage(0);
                    yield return new WaitForSecondsRealtime(.7f); Capture("miss-no-change");miss &= surface.DamageFraction==0;
                    bool first = Shoot(); yield return new WaitForSecondsRealtime(.7f); Capture("survived-first");
                    first &= meteor.IsTargetable && Mathf.Abs(surface.DamageFraction-1f/3)<.001f;
                    bool second = Shoot(); yield return new WaitForSecondsRealtime(.7f); Capture("survived-second");
                    second &= meteor.IsTargetable && Mathf.Abs(surface.DamageFraction-2f/3)<.001f;
                    yield return new WaitForSecondsRealtime(1.5f); Capture("persistent");
                    bool persistent=Mathf.Abs(surface.DamageFraction-2f/3)<.001f;
                    var lodGroup=meteor.GetComponent<LODGroup>();
                    for(int lod=0;lod<3;lod++)
                    {lodGroup.ForceLOD(lod);yield return null;Capture("survived-lod-"+lod);}
                    lodGroup.ForceLOD(-1);
                    var pose=camera.transform.localPosition;var rotation=camera.transform.localRotation;
                    bool uiClear=true;
                    Rect rock=TelemetryReadabilityProof.MeteorRect(camera,meteor);
                    foreach(string name in new[]{"ShipStatusHologram","ThreatStatusHologram"})
                    {
                        var rect=GameObject.Find(name).GetComponent<RectTransform>();
                        uiClear &= !rock.Overlaps(TelemetryReadabilityProof.ViewRect(camera,rect));
                    }
                    bool reticle=meteor.GetComponentsInChildren<LineRenderer>().Any(r=>r.enabled && r.name.StartsWith("TacticalBracket_"));
                    // Paired explicit render submission samples; CPU wall time, not a GPU frame-time benchmark.
                    foreach(bool enabled in new[]{false,true})
                    {
                        surface.enabled=enabled;yield return null;
                        for(int i=0;i<5;i++){PcStabilityRenderDriver.RenderForProof(camera);yield return null;}
                        var samples=new double[30];var clock=new System.Diagnostics.Stopwatch();
                        for(int i=0;i<samples.Length;i++)
                        {clock.Restart();PcStabilityRenderDriver.RenderForProof(camera);clock.Stop();samples[i]=clock.Elapsed.TotalMilliseconds;yield return null;}
                        Array.Sort(samples);notes.Add($"{size}: damageEnabled={enabled} renderSubmissionMedianMs={samples[15]:F4} p95Ms={samples[28]:F4} samples=30");
                    }
                    bool last = Shoot(); yield return new WaitForSecondsRealtime(.08f); Capture("destroyed-flash");
                    yield return new WaitForSecondsRealtime(.9f); Capture("destroyed-cleared");
                    bool destroyed=died && !meteor.gameObject.activeSelf && surface.DamageFraction==0;
                    meteor.Destroyed-=Death;
                    var reused=spawner.SpawnScenario(camera.ViewportToWorldPoint(new Vector3(.62f,.6f,18)),Vector3.zero,3.2f,20,3,MeteorType.Large);
                    reused.enabled=false;used.Add(reused);
                    bool reuse=reused==meteor && surface.DamageFraction==0 && surface.SurfaceCount==surfaceCount;
                    yield return new WaitForSecondsRealtime(.2f);Capture("pooled-intact");
                    bool cameraStable=pose==camera.transform.localPosition && rotation==camera.transform.localRotation;
                    bool valid = intact && miss && first && second && persistent && last && destroyed && reuse && lodCorrect && uiClear && reticle && cameraStable;
                    notes.Add($"{size}: shots={first}/{second}/{last} intact={intact} miss={miss} persistent={persistent} destroyed={destroyed} reuse={reuse} lodCorrect={lodCorrect} uiClear={uiClear} reticle={reticle} cameraStable={cameraStable} valid={valid}");
                    if(validate)passed &= valid;
                    spawner.ClearScenarioRound();
                }
                var restart=spawner.SpawnScenario(camera.transform.TransformPoint(new Vector3(0,5,20)),Vector3.zero,3,20,3);
                used.Add(restart);restart.ReceiveDamage(1);
                bool damaged=restart.GetComponent<MeteorDamageSurface>().DamageFraction>0;
                Object.FindAnyObjectByType<OperationsResetController>().ResetExperience();yield return null;
                bool reset=damaged && used.All(m=>m.GetComponent<MeteorDamageSurface>().DamageFraction==0);
                notes.Add("restartClearsAll="+reset);if(validate)passed &= reset;
            }
            finally
            {
                spawner.EndScenario();foreach(var meteor in used)if(meteor!=null)meteor.enabled=true;
                router.InputSuspended=input;weapon.AutoFireOnLock=fire;gaze.enabled=looking;
                Object.FindAnyObjectByType<OperationsResetController>().ResetExperience();
                Directory.CreateDirectory(output);notes.Add("passed="+passed);File.WriteAllLines(Path.Combine(output,"verification.txt"),notes);
            }
            complete(passed);
        }
    }
}
