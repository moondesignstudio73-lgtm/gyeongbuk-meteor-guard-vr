using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Player;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MeteorDefenseVR.PcInput
{
    public static class DockRepairProof
    {
        public static IEnumerator Run(GazeInputRouter router,string output,Action<bool> complete)
        {
            var camera=Camera.main;var flow=GameFlowManager.Instance;var reset=Object.FindAnyObjectByType<OperationsResetController>();
            var lobby=Object.FindAnyObjectByType<HangarLobbyController>();var repair=lobby.Repair;
            var health=Object.FindAnyObjectByType<PlayerHealth>();var damage=Object.FindAnyObjectByType<CockpitDamagePresentation>();
            var stats=Object.FindAnyObjectByType<GameSessionStats>();var result=Object.FindAnyObjectByType<ResultController>();
            var spawner=Object.FindAnyObjectByType<MeteorSpawner>();var weapon=Object.FindAnyObjectByType<LaserWeapon>();var gaze=Object.FindAnyObjectByType<GazeRaycaster>();
            bool input=router.InputSuspended,fire=weapon.AutoFireOnLock,looking=gaze.enabled;var policy=health.Policy;
            var store=lobby.Ranking.Store;lobby.Ranking.Initialize(flow,result,new LocalRankingStore(null));
            router.InputSuspended=true;weapon.AutoFireOnLock=false;gaze.enabled=false;
            var pose=camera.transform.position;var rotation=camera.transform.rotation;
            var notes=new List<string>{"Explicit real health/impact/return/menu command fixtures; not natural human/HMD play. Ranking uses an in-memory store."};
            bool passed=true;
            bool Clean()=>repair.VisibleDrones==0 && !repair.ScanVisible && health.HealthNormalized==1 && damage.VisibleCrackCount==0 && !damage.HasWarningLight && !damage.AirLeakPlaying;
            IEnumerator Start(int amount,bool failed=false)
            {
                reset.ResetExperience();router.InputSuspended=true;flow.StartMainGame();spawner.StopSpawning(true);
                yield return new WaitForSecondsRealtime(.3f);health.SetPolicy(failed?GameOverPolicy.NormalMode:GameOverPolicy.NoFailExperienceMode);
                stats.SetScore(43210);
                if(amount>0){health.Tick(1);health.ApplyImpactDamage(amount,damage.RepairAnchor(0).position);}
                if(!failed)flow.ShowResult();
            }
            IEnumerator Menu()
            {
                float deadline=Time.realtimeSinceStartup+7;
                while(!lobby.IsMenuReady && Time.realtimeSinceStartup<deadline)yield return null;
                passed &= lobby.IsMenuReady;
            }
            try
            {
                foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(800,600)})
                {
                    using var proof=new PcStabilityProbe(camera,size.x,size.y);
                    void Capture(string name)
                    {
                        Canvas.ForceUpdateCanvases();PcStabilityRenderDriver.RenderForProof(camera);
                        string folder=Path.Combine(output,$"{size.x}x{size.y}",name);Directory.CreateDirectory(folder);proof.SaveGameOnlyProof(folder,router);
                        notes.Add($"{size}/{name}: hp={health.CurrentHealth:F3} cracks={damage.VisibleCrackCount} phase={repair.Phase} progress={repair.Progress:F3} drones={repair.VisibleDrones} scan={repair.ScanVisible} menu={lobby.Phase}");
                    }
                    yield return Start(85);Capture("damaged-result");
                    bool preserved=result.Snapshot.Score==43210;
                    reset.Tick(result.DisplayDuration+.1f);yield return Menu();Capture("approach");
                    bool menu=repair.VisibleDrones==2 && lobby.IsMenuReady && lobby.View.Buttons[0].gameObject.activeInHierarchy;
                    var fracture=damage.RepairAnchor(0).GetComponentsInChildren<Renderer>().First(r=>r.enabled && r.name.StartsWith("Crack"));
                    var block=new MaterialPropertyBlock();fracture.GetPropertyBlock(block);float beforeAlpha=block.GetColor("_BaseColor").a;
                    yield return new WaitForSecondsRealtime(1.15f);Capture("scan-repair");
                    bool partial=health.CurrentHealth>15 && health.CurrentHealth<100 && damage.VisibleCrackCount>0 && repair.ScanVisible;
                    fracture.GetPropertyBlock(block);float afterAlpha=block.GetColor("_BaseColor").a;
                    bool fading=afterAlpha>0 && afterAlpha<beforeAlpha;
                    notes.Add($"{size}: crackAlphaBefore={beforeAlpha:F3} during={afterAlpha:F3} fadesWithHealth={fading}");partial &= fading;
                    var watch=new System.Diagnostics.Stopwatch();repair.Tick(0);
                    long allocated=GC.GetAllocatedBytesForCurrentThread();watch.Start();for(int i=0;i<1000;i++)repair.Tick(0);watch.Stop();
                    allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
                    notes.Add($"{size}: warm1000TicksBytes={allocated} totalCpuMs={watch.Elapsed.TotalMilliseconds:F4}; zero-delta active scan fixture, not full GPU benchmark");
                    watch.Restart();long healingBytes=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<200;i++)repair.Tick(.00001f);
                    healingBytes=GC.GetAllocatedBytesForCurrentThread()-healingBytes;watch.Stop();
                    notes.Add($"{size}: 200HealthAndScanTicksBytes={healingBytes} totalCpuMs={watch.Elapsed.TotalMilliseconds:F4}; includes existing HP/UI event listeners");
                    var text=repair.Status;text.ForceMeshUpdate(true,true);
                    var chars=text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(c=>c.isVisible&&char.IsLetterOrDigit(c.character)).ToArray();
                    float min=chars.Min(c=>Mathf.Abs(camera.WorldToScreenPoint(text.transform.TransformPoint(c.topLeft)).y-camera.WorldToScreenPoint(text.transform.TransformPoint(c.bottomLeft)).y));
                    bool readable=min>=18 && !text.isTextTruncated && text.preferredWidth<=text.rectTransform.rect.width
                        && text.text.Where(char.IsLetterOrDigit).All(c=>text.font.HasCharacter(c,true));
                    notes.Add($"{size}: minGlyphPixels={min:F3} readable={readable} text={text.text}");
                    yield return new WaitForSecondsRealtime(2.3f);Capture("ready");
                    preserved &= lobby.LastResult.Score==43210;
                    bool ready=Clean() && repair.Phase==DockRepairController.RepairPhase.Ready && lobby.IsMenuReady;
                    passed &= menu && partial && preserved && ready && readable && allocated==0;
                    notes.Add($"{size}: menuAvailable={menu} actualHealthDuringScan={partial} resultPreserved={preserved} ready={ready}");
                    yield return Start(0);reset.Tick(result.DisplayDuration+.1f);yield return Menu();Capture("undamaged-check");
                    yield return new WaitForSecondsRealtime(.85f);Capture("undamaged-ready");
                    bool quick=Clean() && repair.Phase==DockRepairController.RepairPhase.Ready && repair.IsInspecting;
                    notes.Add($"{size}: shortInspection={quick}");passed &= quick;
                    yield return Start(85);reset.Tick(result.DisplayDuration+.1f);yield return Menu();
                    yield return new WaitForSecondsRealtime(1);Capture("early-exit-scan");
                    lobby.Queue(size.x==1280?HangarLobbyController.MenuAction.Replay:HangarLobbyController.MenuAction.Difficulty);
                    yield return new WaitForSecondsRealtime(.32f);Capture("early-exit-clean");
                    bool cancelled=Clean() && repair.Phase==DockRepairController.RepairPhase.Hidden && !repair.Status.gameObject.activeInHierarchy;
                    notes.Add($"{size}: early{(size.x==1280?"Replay":"MenuChange")}Clean={cancelled}");passed &= cancelled;
                    if(size.x==1280)
                    {
                        float deadline=Time.realtimeSinceStartup+12;
                        while(flow.CurrentState!=GameState.Playing && Time.realtimeSinceStartup<deadline)yield return null;
                        spawner.StopSpawning(true);yield return new WaitForSecondsRealtime(.3f);Capture("early-exit-next-combat");
                        bool combatClean=flow.CurrentState==GameState.Playing && Clean();notes.Add("earlyReplayNextCombatClean="+combatClean);passed &= combatClean;
                    }
                    else
                    {
                        lobby.Queue(HangarLobbyController.MenuAction.Back);yield return new WaitForSecondsRealtime(.6f);
                        bool menuClean=lobby.IsMenuReady && repair.Phase==DockRepairController.RepairPhase.Hidden && Clean();
                        notes.Add("menuBackDoesNotRestartRepair="+menuClean);passed &= menuClean;
                    }
                }
                int objects=0,materials=0,totalObjects=0,totalMaterials=0;
                for(int cycle=0;cycle<4;cycle++)
                {
                    yield return Start(65);reset.Tick(result.DisplayDuration+.1f);yield return Menu();yield return new WaitForSecondsRealtime(3.5f);
                    int count=repair.PresentationRoot.GetComponentsInChildren<Transform>(true).Length;
                    int mats=repair.PresentationRoot.GetComponentsInChildren<Renderer>(true).Select(r=>r.sharedMaterial).Distinct().Count();
                    int allObjects=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Length,allMats=Resources.FindObjectsOfTypeAll<Material>().Length;
                    if(cycle==0){objects=count;materials=mats;totalObjects=allObjects;totalMaterials=allMats;}
                    bool stable=Clean() && count==objects && mats==materials && allObjects<=totalObjects && allMats<=totalMaterials;
                    notes.Add($"repeat={cycle+1}: pooledObjects={count} sharedMaterials={mats} totalObjects={allObjects} totalMaterials={allMats} cleanStable={stable}");passed &= stable;
                }
                using(var proof=new PcStabilityProbe(camera,800,600))
                {
                    yield return Start(100,true);
                    bool pending=result.IsPresentationPending && !result.IsVisible && repair.Phase==DockRepairController.RepairPhase.Hidden;
                    float deadline=Time.realtimeSinceStartup+8;
                    while(!result.IsVisible && Time.realtimeSinceStartup<deadline)yield return null;
                    bool score=result.IsVisible && result.Snapshot.Score==43210;
                    yield return new WaitForSecondsRealtime(4.7f);
                    bool duration=result.IsVisible && repair.Phase==DockRepairController.RepairPhase.Hidden;
                    deadline=Time.realtimeSinceStartup+12;
                    while(!lobby.IsMenuReady && Time.realtimeSinceStartup<deadline)yield return null;
                    yield return new WaitForSecondsRealtime(1.1f);
                    string folder=Path.Combine(output,"failure-scan");Directory.CreateDirectory(folder);PcStabilityRenderDriver.RenderForProof(camera);proof.SaveGameOnlyProof(folder,router);
                    bool failedRepair=health.CurrentHealth>0 && health.CurrentHealth<100 && repair.ScanVisible && lobby.LastResult.MissionFailed;
                    reset.ResetExperience();yield return null;
                    bool resetClean=Clean() && repair.Phase==DockRepairController.RepairPhase.Hidden;
                    passed &= pending && score && duration && failedRepair && resetClean;
                    notes.Add($"failure: pending={pending} scorePreserved={score} resultVisibleAfter4.7={duration} failedShipRepair={failedRepair} resetClean={resetClean}");
                }
                bool cameraStable=pose==camera.transform.position && rotation==camera.transform.rotation;
                notes.Add("cameraStable="+cameraStable);passed &= cameraStable;
            }
            finally
            {
                reset.ResetExperience();router.InputSuspended=input;weapon.AutoFireOnLock=fire;gaze.enabled=looking;health.SetPolicy(policy);
                lobby.Ranking.Initialize(flow,result,store);
                Directory.CreateDirectory(output);notes.Add("passed="+passed);File.WriteAllLines(Path.Combine(output,"verification.txt"),notes);
            }
            complete(passed);
        }
    }
}
