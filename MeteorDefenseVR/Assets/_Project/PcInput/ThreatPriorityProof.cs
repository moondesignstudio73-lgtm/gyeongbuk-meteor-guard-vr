using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.PcInput
{
    // Opt-in QA: use real pooled targets and threat selection, freeze only target movement.
    public static class ThreatPriorityProof
    {
        public static IEnumerator Run(GazeInputRouter router, string output, bool validate, Action<bool> complete)
        {
            var camera = Camera.main;
            var spawner = Object.FindAnyObjectByType<MeteorSpawner>();
            var warnings = Object.FindAnyObjectByType<ThreatIndicatorView>();
            var weapon = Object.FindAnyObjectByType<LaserWeapon>();
            bool priorInput = router.InputSuspended, priorFire = weapon.AutoFireOnLock, priorSuppression=warnings.VisualsSuppressed;
            var notes = new List<string> { "Explicit position/velocity fixtures. Actual pooled prefabs; movement disabled for deterministic capture. Not natural mouse/HMD play." };
            bool passed = true;
            router.InputSuspended = true; weapon.AutoFireOnLock = false;
            var originalPosition = camera.transform.position; var originalRotation = camera.transform.rotation;
            using var probe = new PcStabilityProbe(camera);
            MeteorController near = null, far = null, recycled = null;
            bool Emphasized(MeteorController meteor) => meteor != null && meteor.GetComponentsInChildren<LineRenderer>()
                .Any(r=>r.name.StartsWith("TacticalBracket_") && r.enabled && r.startColor.r>r.startColor.g && r.startColor.a>=.8f);
            bool Detectable(MeteorController meteor) => meteor.GetComponentsInChildren<LineRenderer>()
                .Any(r=>r.name.StartsWith("TacticalBracket_") && r.enabled && r.startColor.a>=.2f);
            Rect ScreenBounds(RectTransform transform)
            {
                var corners=new Vector3[4];transform.GetWorldCorners(corners);
                Vector2 min=Vector2.one*float.PositiveInfinity,max=Vector2.one*float.NegativeInfinity;
                foreach(var corner in corners){Vector2 p=camera.WorldToViewportPoint(corner);min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
                return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
            }
            bool LabelsClear()
            {
                var root=GameObject.Find("ThreatIndicators");if(root==null)return true;
                foreach(var text in root.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
                    foreach(string name in new[]{"ShipStatusHologram","ThreatStatusHologram"})
                    {
                        var panel=GameObject.Find(name)?.GetComponent<RectTransform>();
                        if(panel!=null && ScreenBounds(text.rectTransform).Overlaps(ScreenBounds(panel)))return false;
                    }
                return true;
            }
            void Capture(string name, MeteorController expected)
            {
                string folder = Path.Combine(output,name); Directory.CreateDirectory(folder);
                Canvas.ForceUpdateCanvases(); PcStabilityRenderDriver.RenderForProof(camera);
                probe.SaveGameOnlyProof(folder,router);
                int emphasized=spawner.ActiveMeteors.Count(Emphasized);
                bool correct=warnings.MostUrgentThreat==expected && (expected==null ? emphasized==0 : emphasized==1 && Emphasized(expected));
                bool labelsClear=LabelsClear();correct &= labelsClear;
                if(validate) passed &= correct;
                notes.Add($"{name}: expected={(expected==null?"none":expected.name)}, selected={(warnings.MostUrgentThreat==null?"none":warnings.MostUrgentThreat.name)}, emphasized={emphasized}, labelsClear={labelsClear}, correct={correct}, nearTtc={(near==null?-1:ThreatProjection.TimeToImpact(spawner.ShipCenter,near,spawner.ShipCollisionRadius))}, farTtc={(far==null?-1:ThreatProjection.TimeToImpact(spawner.ShipCenter,far,spawner.ShipCollisionRadius))}");
            }
            try
            {
                GameFlowManager.Instance.StartMainGame(); spawner.BeginScenario(2);
                Vector3 a=camera.transform.TransformPoint(new Vector3(-7,3,35)), b=camera.transform.TransformPoint(new Vector3(12,5,65));
                near=spawner.SpawnScenario(a,(spawner.ShipCenter-a).normalized,3,10);
                far=spawner.SpawnScenario(b,(spawner.ShipCenter-b).normalized*12,3,10);
                near.name="QA_NearSlow"; far.name="QA_FarFast"; near.enabled=far.enabled=false;
                yield return new WaitForSecondsRealtime(1.5f);
                Capture("far-fast",far);
                if(validate) passed &= Detectable(near) && Detectable(far);
                var debris=GameObject.Find("DistantDebris");
                bool backgroundUntargeted=debris!=null && debris.GetComponentsInChildren<GazeLockOnTarget>(true).Length==0
                    && debris.GetComponentsInChildren<LockOnReticleView>(true).Length==0;
                if(validate)passed &= backgroundUntargeted;
                notes.Add("backgroundUntargeted="+backgroundUntargeted);
                using(var small=new PcStabilityProbe(camera,800,600))
                {
                    camera.GetComponent<MeteorDefenseVR.Visual.WorldUiSafeArea>().Apply();
                    yield return null;
                    string folder=Path.Combine(output,"far-fast-800x600");Directory.CreateDirectory(folder);
                    Canvas.ForceUpdateCanvases();PcStabilityRenderDriver.RenderForProof(camera);small.SaveGameOnlyProof(folder,router);
                }
                camera.GetComponent<MeteorDefenseVR.Visual.WorldUiSafeArea>().Apply();
                warnings.VisualsSuppressed=true;yield return new WaitForSecondsRealtime(.12f);
                bool suppressed=!Detectable(near) && !Detectable(far);
                if(validate)passed &= suppressed; notes.Add("suppressionClearsDiscovery="+suppressed);
                warnings.VisualsSuppressed=false;yield return new WaitForSecondsRealtime(.12f);
                var focused=far.GetComponent<GazeLockOnTarget>(); focused.OnGazeEnter(default);focused.OnGazeStay(default,.1f);
                yield return new WaitForSecondsRealtime(.12f);
                bool focusSafe=Emphasized(far) && focused.State==LockOnState.Focusing;
                if(validate)passed &= focusSafe; notes.Add("focusPreservesPriority="+focusSafe);
                focused.ForceRelease();
                near.SetRuntimeSpeedMultiplier(2); far.SetRuntimeSpeedMultiplier(.1f);
                yield return new WaitForSecondsRealtime(.12f);
                Capture("near-now-urgent",near);
                Time.timeScale=0; Vector3 held=near.transform.position;
                yield return new WaitForSecondsRealtime(.15f);
                if(validate) passed &= near.transform.position==held && warnings.MostUrgentThreat==near;
                Time.timeScale=1;
                // Move the fastest threat behind the view, preserving the same real speed calculation.
                near.transform.position=camera.transform.TransformPoint(new Vector3(-30,2,-25));
                near.SetMovementDirection(spawner.ShipCenter-near.transform.position); near.SetRuntimeSpeedMultiplier(2);
                far.SetRuntimeSpeedMultiplier(.1f);
                yield return new WaitForSecondsRealtime(.12f); Capture("urgent-behind",near);
                using(var small=new PcStabilityProbe(camera,800,600))
                {
                    camera.GetComponent<MeteorDefenseVR.Visual.WorldUiSafeArea>().Apply();
                    yield return new WaitForSecondsRealtime(.15f);
                    string folder=Path.Combine(output,"urgent-behind-800x600");Directory.CreateDirectory(folder);
                    Canvas.ForceUpdateCanvases();PcStabilityRenderDriver.RenderForProof(camera);small.SaveGameOnlyProof(folder,router);
                    bool clear=LabelsClear();if(validate)passed &= clear;notes.Add("800x600LabelsClear="+clear);
                }
                camera.GetComponent<MeteorDefenseVR.Visual.WorldUiSafeArea>().Apply();
                near.ReceiveDamage(10000); yield return new WaitForSecondsRealtime(.12f);
                Capture("destroyed-switch",far);
                spawner.ClearScenarioRound(); yield return new WaitForSecondsRealtime(.12f);
                recycled=spawner.SpawnScenario(a,(spawner.ShipCenter-a).normalized*3,3,10); recycled.enabled=false;
                yield return new WaitForSecondsRealtime(.4f); Capture("pooled-respawn",recycled);
                Object.FindAnyObjectByType<OperationsResetController>().ResetExperience();
                yield return new WaitForSecondsRealtime(.12f); Capture("reset-cleared",null);
                if(validate) passed &= camera.transform.position==originalPosition && camera.transform.rotation==originalRotation;
            }
            finally
            {
                Time.timeScale=1; spawner.EndScenario();
                if(near!=null)near.enabled=true;if(far!=null)far.enabled=true;if(recycled!=null)recycled.enabled=true;
                router.InputSuspended=priorInput;weapon.AutoFireOnLock=priorFire;warnings.VisualsSuppressed=priorSuppression;
                Directory.CreateDirectory(output); notes.Add("passed="+passed); File.WriteAllLines(Path.Combine(output,"verification.txt"),notes);
            }
            complete(passed);
        }
    }
}
