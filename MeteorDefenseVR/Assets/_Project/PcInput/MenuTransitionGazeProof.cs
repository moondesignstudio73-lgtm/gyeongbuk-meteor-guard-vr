using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using Object=UnityEngine.Object;
namespace MeteorDefenseVR.PcInput
{
    public static class MenuTransitionGazeProof
    {
        private sealed class HeldRay:IGazeProvider
        {
            public Ray Ray;public GazeInputRouter Router;public bool Valid;
            public bool IsTrackingValid=>Valid&&!Router.UiModalOpen;
            public Ray GetGazeRay()=>Ray;public Vector3 GetGazeOrigin()=>Ray.origin;public Vector3 GetGazeDirection()=>Ray.direction;
        }
        public static IEnumerator Run(string output,bool validate,Action<bool> complete)
        {
            Directory.CreateDirectory(output);var notes=new List<string>{"Controlled real scene/collider gaze. State setup and settings Button.onClick invoked explicitly; not OS/HMD or physical pointer proof. Baseline observations and validated after checks distinguished by caller."};
            var reset=Object.FindAnyObjectByType<OperationsResetController>();var flow=GameFlowManager.Instance;
            var health=Object.FindAnyObjectByType<PlayerHealth>();var result=Object.FindAnyObjectByType<ResultController>();
            var router=Object.FindAnyObjectByType<GazeInputRouter>();var caster=Object.FindAnyObjectByType<GazeRaycaster>();
            var lobby=Object.FindAnyObjectByType<HangarLobbyController>();var actions=Object.FindAnyObjectByType<ResultActionsView>();var overlay=Object.FindAnyObjectByType<PcTestOverlay>();
            lobby.Ranking.Initialize(flow,result,new LocalRankingStore(null));
            var original=caster.Provider;bool wasEnabled=caster.enabled,suspended=router.InputSuspended,passed=true;
            var ray=new HeldRay{Router=router};caster.enabled=false;caster.SetProvider(ray);router.InputSuspended=true;
            var physical=InputSystem.devices.Where(d=>d.enabled&&(d is Mouse||d is Keyboard)).ToArray();
            foreach(var device in physical)InputSystem.DisableDevice(device);
            float tickAt=Time.unscaledTime;
            void Log(string s){notes.Add(s);File.WriteAllLines(Path.Combine(output,"verification.txt"),notes);}
            void Check(bool ok,string s){Log(s+"="+ok);if(validate)passed&=ok;}
            IEnumerator Observe(float seconds){float end=Time.unscaledTime+seconds;while(Time.unscaledTime<end){float now=Time.unscaledTime;caster.Tick(Mathf.Max(0,now-tickAt));tickAt=now;yield return null;}}
            void Aim(Vector3 point){ray.Ray=new Ray(Camera.main.transform.position,(point-Camera.main.transform.position).normalized);ray.Valid=true;Physics.SyncTransforms();}
            try
            {
                foreach(float closeAge in new[]{-2f,-1f,1.85f,2.15f})
                {
                    ray.Valid=false;caster.Tick(0);reset.ResetExperience();router.InputSuspended=true;flow.StartMainGame();yield return Observe(.3f);
                    health.RestoreCheckpoint(health.MaxHealth);health.Tick(1);health.ApplyImpactDamage(100,health.transform.position);
                    float end=Time.unscaledTime+10;while(!result.IsVisible&&Time.unscaledTime<end)yield return Observe(.1f);
                    reset.CancelResultTimer();Check(result.IsVisible,"failure result available "+closeAge);
                    actions.Menu.Click();Check(lobby.IsMenuReady,"result menu fixture "+closeAge);
                    lobby.Queue(HangarLobbyController.MenuAction.Ranking);yield return Observe(.35f);
                    Check(lobby.Phase==HangarLobbyController.LobbyPhase.Records,"records path "+closeAge);
                    var back=lobby.View.Records.BackButton;var retry=lobby.View.Buttons[8];
                    Aim(back.transform.TransformPoint(new Vector3(0,17,0)));yield return Observe(.01f);
                    Check(ReferenceEquals(caster.CurrentTarget,back),"actual back collider "+closeAge);
                    end=Time.unscaledTime+2;while(lobby.Phase==HangarLobbyController.LobbyPhase.Records&&Time.unscaledTime<end)yield return Observe(.02f);
                    float returned=Time.unscaledTime;Check(lobby.IsMenuReady,"gaze back returns menu "+closeAge);yield return Observe(.05f);
                    Log("held ray next target="+(caster.CurrentTarget as Component)?.name+" retry active="+retry.gameObject.activeInHierarchy);
                    Check(ReferenceEquals(caster.CurrentTarget,retry),"same ray overlaps retry "+closeAge);
                    // Queue commits .24s after Confirm; measure the actual stored confirmation timestamp.
                    float confirmed=(float)typeof(GazeMenuSelectionGate).GetField("lastConfirmedAt",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).GetValue(null);
                    if(closeAge>0)
                    {
                        var settings=overlay.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.name=="LookSettings");settings.onClick.Invoke();yield return Observe(.05f);
                        Check(router.UiModalOpen&&caster.CurrentTarget==null,"modal blocks world target "+closeAge);
                        while(Time.unscaledTime-confirmed<closeAge)yield return Observe(.01f);
                        overlay.TryCloseLookSettings();Log("modal closed at confirmation age="+(Time.unscaledTime-confirmed));
                    }
                    yield return Observe(closeAge==-2f?.05f:closeAge<0?3f:1.2f);
                    Check(lobby.IsMenuReady&&flow.CurrentState==GameState.Result&&retry.Progress==0,"held gaze does not select retry "+closeAge);
                    if(lobby.IsMenuReady)
                    {
                        Aim(Camera.main.transform.position+Camera.main.transform.up*4+Camera.main.transform.forward*3);yield return Observe(.1f);
                        Aim(retry.transform.position);
                        // A fresh menu can still be inside OpenScreen(.4), then dwell(.7)
                        // and the lobby command delay(.24). Bound by their actual contract.
                        float reentry=Time.unscaledTime;end=reentry+.4f+.7f+.24f+.1f;
                        while(lobby.IsMenuReady&&Time.unscaledTime<end)yield return Observe(.01f);
                        Log("reentry selection elapsed="+(Time.unscaledTime-reentry));
                        Check(Time.unscaledTime-reentry>=.7f,"reentry never bypasses gaze dwell "+closeAge);
                        Check(flow.CurrentState==GameState.Playing,"exit then reenter retries normally "+closeAge);
                    }
                }
            }
            finally
            {
                ray.Valid=false;caster.Tick(0);overlay.TryCloseLookSettings();reset.ResetExperience();router.InputSuspended=suspended;caster.SetProvider(original);caster.enabled=wasEnabled;
                foreach(var device in physical)InputSystem.EnableDevice(device);
                Log("passed="+passed);
            }
            complete(passed);
        }
    }
}
