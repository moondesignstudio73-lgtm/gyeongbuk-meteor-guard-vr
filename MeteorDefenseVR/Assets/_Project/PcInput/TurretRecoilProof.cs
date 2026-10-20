using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.PcInput
{
    public static class TurretRecoilProof
    {
        public static IEnumerator Run(GazeInputRouter router, string output, bool validate, Action<bool> complete)
        {
            var player = Camera.main;
            var spawner = Object.FindAnyObjectByType<MeteorSpawner>();
            var weapon = Object.FindAnyObjectByType<LaserWeapon>();
            var turrets = weapon.TurretAiming;
            var gaze = Object.FindAnyObjectByType<GazeRaycaster>();
            bool input = router.InputSuspended, firing = weapon.AutoFireOnLock, gazeEnabled = gaze.enabled;
            router.InputSuspended = true; weapon.AutoFireOnLock = false; gaze.enabled = false;
            var notes = new List<string> { "Explicit real-spawner/weapon fixture, frozen target; external close-up camera is QA only. Player camera and its turret exclusion remain intact. Not human/HMD play." };
            var observerObject = new GameObject("QA_TurretCloseup");
            var observer = observerObject.AddComponent<Camera>(); observer.CopyFrom(player);
            observer.enabled = false; observer.clearFlags = CameraClearFlags.SolidColor;
            observer.backgroundColor = new Color(.015f, .025f, .045f); observer.fieldOfView = 40;
            observer.cullingMask = 1 << LayerMask.NameToLayer(TurretAimingSystem.PlayerHiddenLayerName);
            observer.stereoTargetEye = StereoTargetEyeMask.None;
            MeteorController meteor = null;
            bool passed = true;
            try
            {
                GameFlowManager.Instance.StartMainGame(); spawner.BeginScenario();
                meteor = spawner.SpawnScenario(spawner.ShipCenter + spawner.ShipRotation * new Vector3(0, 8, 24), Vector3.zero, 2.4f, 10, 1000);
                meteor.enabled = false;
                var target = meteor.GetComponent<GazeLockOnTarget>();
                target.Advance(3);
                float deadline = Time.realtimeSinceStartup + 5;
                while (!turrets.IsReadyFor(target) && Time.realtimeSinceStartup < deadline) yield return null;
                bool ready = turrets.IsReadyFor(target);
                int selected = turrets.SelectedTurretIndex;
                var muzzle = selected == 0 ? turrets.TopMuzzle : turrets.BottomMuzzle;
                var pivot = muzzle.parent; var barrel = pivot.Find("Barrel");
                Vector3 rest = barrel.localPosition, muzzleRest = muzzle.localPosition;
                Vector3 playerPosition = player.transform.localPosition; Quaternion playerRotation = player.transform.localRotation;
                int mask = player.cullingMask, firedCount = 0, hitCount = 0;
                void Fired(GazeLockOnTarget _, MeteorController m)
                { firedCount++; notes.Add($"event=Fired frame={Time.frameCount} health={m.CurrentHealth} barrelOffset={Vector3.Distance(rest,barrel.localPosition)}"); }
                void Hit(MeteorController m, Vector3 _)
                { hitCount++; notes.Add($"event=Hit frame={Time.frameCount} health={m.CurrentHealth} barrelOffset={Vector3.Distance(rest,barrel.localPosition)}"); }
                using var closeup = new PcStabilityProbe(observer, 960, 540);
                using var playerProof = new PcStabilityProbe(player, 1280, 720);
                void Capture(string label)
                {
                    observer.transform.position = pivot.TransformPoint(new Vector3(.85f, .35f, 1));
                    observer.transform.LookAt(pivot.TransformPoint(new Vector3(0, 0, .18f)), pivot.up);
                    string folder = Path.Combine(output, label); Directory.CreateDirectory(folder);
                    PcStabilityRenderDriver.RenderForProof(observer); closeup.SaveGameOnlyProof(folder, router);
                    notes.Add($"{label}: barrel={barrel.localPosition:F6} muzzle={muzzle.localPosition:F6} offset={Vector3.Distance(rest,barrel.localPosition):F6}");
                }
                Capture("before-shot");
                yield return new WaitForSecondsRealtime(.25f);
                bool idle = Vector3.Distance(rest, barrel.localPosition) < .00001f;
                weapon.Fired += Fired; weapon.Hit += Hit;
                bool shot;
                try { shot = weapon.TryFire(target); }
                finally { weapon.Fired -= Fired; weapon.Hit -= Hit; }
                Capture("shot-and-hit");
                yield return new WaitForSecondsRealtime(.045f);
                float peak = Vector3.Distance(rest, barrel.localPosition); Capture("recoil-peak");
                yield return new WaitForSecondsRealtime(.08f); Capture("returning");
                yield return new WaitForSecondsRealtime(.2f); Capture("returned");
                bool restored = Vector3.Distance(rest, barrel.localPosition) < .00001f && Vector3.Distance(muzzleRest, muzzle.localPosition) < .00001f;
                bool cameraStable = player.transform.localPosition == playerPosition && player.transform.localRotation == playerRotation && player.cullingMask == mask;
                string playerFolder = Path.Combine(output, "player-view"); Directory.CreateDirectory(playerFolder);
                PcStabilityRenderDriver.RenderForProof(player); playerProof.SaveGameOnlyProof(playerFolder, router);
                bool correct = ready && shot && idle && restored && cameraStable && firedCount == 1 && hitCount == 1 && peak > .04f && peak <= .091f;
                notes.Add($"ready={ready} selected={selected} shot={shot} idle={idle} peak={peak} restored={restored} cameraStable={cameraStable} fired={firedCount} hit={hitCount} correct={correct}");
                if(validate)
                {
                    bool Shoot()
                    {
                        target.Advance(3);
                        for(int i=0;i<200&&(!turrets.IsReadyFor(target)||!turrets.HasTracked(target));i++)turrets.Tick(.01f);
                        weapon.TickCooldown(1);return weapon.TryFire(target);
                    }
                    bool AtRest()=>Vector3.Distance(rest,barrel.localPosition)<.00001f&&Vector3.Distance(muzzleRest,muzzle.localPosition)<.00001f;
                    bool burst=true;float largest=0;
                    for(int i=0;i<16;i++)
                    {
                        burst &= Shoot();yield return new WaitForSecondsRealtime(.035f);
                        float offset=Vector3.Distance(rest,barrel.localPosition);largest=Mathf.Max(largest,offset);
                        burst &= offset<=.091f;
                    }
                    yield return new WaitForSecondsRealtime(.3f);burst &= AtRest();Capture("burst-returned");
                    bool noFire=!weapon.TryFire(null);meteor.ReceiveDamage(1);yield return new WaitForSecondsRealtime(.1f);noFire &= AtRest();
                    bool pause=Shoot();yield return new WaitForSecondsRealtime(.045f);
                    var simulation=Object.FindAnyObjectByType<WindowsVRSimulation>();simulation.Pause();
                    yield return new WaitForSecondsRealtime(.08f);
                    pause &= simulation.IsPaused && Time.timeScale==0 && AtRest();Capture("paused-rest");
                    notes.Add($"pauseObserved={simulation.IsPaused} timeScale={Time.timeScale}");
                    simulation.Resume();router.InputSuspended=true;
                    yield return new WaitForSecondsRealtime(.1f);pause &= AtRest();
                    bool reuse=Shoot();yield return new WaitForSecondsRealtime(.045f);
                    turrets.enabled=false;reuse &= AtRest();meteor.ResetMeteor(false);meteor.Spawn();meteor.enabled=false;
                    turrets.enabled=true;yield return null;reuse &= AtRest();reuse &= Shoot();
                    yield return new WaitForSecondsRealtime(.3f);reuse &= AtRest();Capture("reused-returned");
                    bool resultReset=Shoot();yield return new WaitForSecondsRealtime(.045f);
                    GameFlowManager.Instance.ShowResult();resultReset &= AtRest();Capture("result-rest");
                    Object.FindAnyObjectByType<OperationsResetController>().ResetExperience();yield return null;
                    bool restart=AtRest();
                    notes.Add($"burst={burst} largestOffset={largest} noFire={noFire} pause={pause} reuse={reuse} resultReset={resultReset} restart={restart}");
                    correct &= burst&&noFire&&pause&&reuse&&resultReset&&restart;
                }
                if (validate) passed &= correct;
            }
            finally
            {
                Time.timeScale=1;
                spawner.EndScenario(); if (meteor != null) meteor.enabled = true;
                router.InputSuspended = input; weapon.AutoFireOnLock = firing; gaze.enabled = gazeEnabled;
                Object.Destroy(observerObject);
                Object.FindAnyObjectByType<OperationsResetController>().ResetExperience();
                Directory.CreateDirectory(output); notes.Add("passed=" + passed); File.WriteAllLines(Path.Combine(output, "verification.txt"), notes);
            }
            complete(passed);
        }
    }
}
