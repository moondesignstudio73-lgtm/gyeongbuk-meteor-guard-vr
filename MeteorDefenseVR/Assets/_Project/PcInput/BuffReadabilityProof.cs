using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.PowerUps;
using UnityEngine;
using Object=UnityEngine.Object;
namespace MeteorDefenseVR.PcInput
{
    public static class BuffReadabilityProof
    {
        public static IEnumerator Run(GazeInputRouter router,string output,bool validate,Action<bool> complete)
        {
            var manager=PowerUpManager.Instance;var view=Object.FindAnyObjectByType<PowerUpWeaponFeedback>();var camera=Camera.main;
            var flow=GameFlowManager.Instance;var reset=Object.FindAnyObjectByType<OperationsResetController>();
            var spawner=Object.FindAnyObjectByType<MeteorSpawner>();var health=Object.FindAnyObjectByType<PlayerHealth>();
            var weapon=Object.FindAnyObjectByType<LaserWeapon>();var gaze=Object.FindAnyObjectByType<MeteorDefenseVR.EyeTracking.GazeRaycaster>();
            var hud=Object.FindAnyObjectByType<GameplayHudOverlayView>();
            bool input=router.InputSuspended,fire=weapon.AutoFireOnLock,looking=gaze.enabled;var policy=health.Policy;
            router.InputSuspended=true;weapon.AutoFireOnLock=false;gaze.enabled=false;
            var notes=new List<string>{"Actual scene and abilities; five render sizes; synthetic fixed target fixture. Human/HMD not tested."};bool passed=true;
            void Check(bool valid,string message){notes.Add(message+"="+valid);if(validate)passed &= valid;}
            try
            {
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(800,600),new Vector2Int(2560,1080)})
                {
                    reset.ResetExperience();flow.StartMainGame();spawner.BeginScenario();yield return new WaitForSecondsRealtime(3);
                    using var proof=new PcStabilityProbe(camera,size.x,size.y);
                    MeteorController meteor=null;
                    void Target(){if(meteor!=null&&meteor.IsTargetable)return;meteor=spawner.SpawnScenario(camera.ViewportToWorldPoint(new Vector3(.5f,.6f,28)),Vector3.zero,2,10,100,MeteorType.Large);meteor.enabled=false;}
                    Target();
                    IEnumerator Capture(string name)
                    {
                        view.Tick(0);yield return new WaitForSecondsRealtime(.15f);Canvas.ForceUpdateCanvases();PcStabilityRenderDriver.RenderForProof(camera);
                        string folder=Path.Combine(output,$"{size.x}x{size.y}",name);Directory.CreateDirectory(folder);proof.SaveGameOnlyProof(folder,router);
                        var legacy=Object.FindAnyObjectByType<PowerUpHudView>(FindObjectsInactive.Include);
                        bool oldVisible=false;foreach(var mesh in legacy.GetComponentsInChildren<TextMesh>(true))oldVisible |= mesh.gameObject.activeInHierarchy&&!string.IsNullOrEmpty(mesh.text);
                        Check(!oldVisible,$"{size}/{name} legacy hidden");
                        bool combat=flow.CurrentState==MeteorDefenseVR.Core.GameState.Playing||flow.CurrentState==MeteorDefenseVR.Core.GameState.BossMeteor;
                        Check(view.Visible==(combat&&manager.ActiveBuffCount>0),$"{size}/{name} visible agrees");
                        if(hud.FeedbackValue.gameObject.activeInHierarchy && (name=="Heal"||name=="EMP"))
                        {
                            var notice=hud.FeedbackValue;float h=1000;
                            for(int j=0;j<notice.textInfo.characterCount;j++){var c=notice.textInfo.characterInfo[j];if(c.isVisible)h=Mathf.Min(h,Mathf.Abs(camera.WorldToScreenPoint(notice.transform.TransformPoint(c.topLeft)).y-camera.WorldToScreenPoint(notice.transform.TransformPoint(c.bottomLeft)).y));}
                            Check(!notice.isTextTruncated&&notice.preferredWidth<=notice.rectTransform.rect.width&&notice.preferredHeight<=notice.rectTransform.rect.height&&h>=12,$"{size}/{name} immediate fits and glyphPx={h:F2}");
                        }
                        if(!view.Visible)yield break;
                        var text=view.Readout;string parsed=text.GetParsedText();
                        for(int i=0;i<manager.ActiveBuffs.Count;i++)
                        {
                            var buff=manager.ActiveBuffs[i];string expected=Korean(buff.Type);
                            Check(parsed.Contains(expected),$"{size}/{name} complete name {expected}");
                            string number=(buff.ShieldHits>=0?buff.ShieldHits:Mathf.CeilToInt(buff.Remaining)).ToString();
                            Check(parsed.Contains(number+(buff.ShieldHits>=0?"회":"초")),$"{size}/{name} actual value {number}");
                        }
                        bool fits=!text.isTextTruncated&&text.preferredWidth<=text.rectTransform.rect.width&&text.preferredHeight<=text.rectTransform.rect.height;
                        var bounds=TelemetryReadabilityProof.ViewRect(camera,(RectTransform)text.transform.parent);
                        bool clear=bounds.xMin>=0&&bounds.xMax<=1&&bounds.yMin>=0&&bounds.yMax<=.9f&&!bounds.Overlaps(new Rect(.42f,.4f,.16f,.2f));
                        foreach(var m in spawner.ActiveMeteors)if(m.IsTargetable)clear &= !bounds.Overlaps(TelemetryReadabilityProof.MeteorRect(camera,m));
                        if(hud.FeedbackValue.gameObject.activeInHierarchy&&!string.IsNullOrEmpty(hud.FeedbackValue.text))clear &= !bounds.Overlaps(TelemetryReadabilityProof.ViewRect(camera,hud.FeedbackValue.rectTransform));
                        var warnings=GameObject.Find("ThreatIndicators");if(warnings!=null)foreach(var t in warnings.GetComponentsInChildren<TMPro.TextMeshProUGUI>())if(!string.IsNullOrEmpty(t.text))clear &= !bounds.Overlaps(TelemetryReadabilityProof.ViewRect(camera,t.rectTransform));
                        float minGlyph=1000;for(int i=0;i<text.textInfo.characterCount;i++){var c=text.textInfo.characterInfo[i];if(!c.isVisible)continue;minGlyph=Mathf.Min(minGlyph,Mathf.Abs(camera.WorldToScreenPoint(text.transform.TransformPoint(c.topLeft)).y-camera.WorldToScreenPoint(text.transform.TransformPoint(c.bottomLeft)).y));}
                        Check(fits&&clear&&minGlyph>=12,$"{size}/{name} fits={fits} clear={clear} minGlyph={minGlyph:F2}");
                        if(!clear)notes.Add($"panel={bounds} warning={TelemetryReadabilityProof.ViewRect(camera,hud.FeedbackValue.rectTransform)} target={TelemetryReadabilityProof.MeteorRect(camera,meteor)}");
                        notes.Add(parsed.Replace('\n','|'));
                    }
                    yield return Capture("none");
                    foreach(PowerUpType type in Enum.GetValues(typeof(PowerUpType)))
                    {
                        manager.ResetSystem();Target();if(type==PowerUpType.Heal)health.ApplyDamage(40);
                        manager.Activate(type);yield return Capture(type.ToString());
                        if(type==PowerUpType.Heal||type==PowerUpType.EMP)Check(hud.FeedbackValue.text.Contains(type==PowerUpType.Heal?"회복":"충격파"),$"{size}/{type} immediate notice");
                    }
                    manager.ResetSystem();Target();manager.Activate(PowerUpType.Shield);manager.Activate(PowerUpType.DamageBoost);manager.Activate(PowerUpType.Overdrive);
                    yield return Capture("maximum-defense-long-name");
                    health.ApplyImpactDamage(5,health.transform.position);yield return Capture("shield-hit-count");
                    manager.ResetSystem();manager.Activate(PowerUpType.RapidFire);manager.Activate(PowerUpType.MultiLaser);manager.Activate(PowerUpType.SlowTime);
                    yield return Capture("maximum-weapons");
                    var panel=(RectTransform)view.Readout.transform.parent;var center=TelemetryReadabilityProof.ViewRect(camera,panel).center;
                    meteor.transform.position=camera.ViewportToWorldPoint(new Vector3(center.x,center.y,28));yield return Capture("target-crossing");
                    meteor.transform.position=camera.ViewportToWorldPoint(new Vector3(.5f,.6f,28));
                    manager.Tick(7);yield return Capture("ending-two-seconds");
                    manager.Activate(PowerUpType.RapidFire);manager.Activate(PowerUpType.MultiLaser);manager.Activate(PowerUpType.SlowTime);yield return Capture("refreshed");
                    Time.timeScale=0;float remaining=view.RapidRemaining;yield return Capture("paused");Check(view.RapidRemaining==remaining,$"{size} pause frozen");Time.timeScale=1;
                    manager.Tick(40);yield return Capture("expired");
                    manager.Activate(PowerUpType.Shield);manager.Activate(PowerUpType.DamageBoost);manager.Activate(PowerUpType.Overdrive);
                    // Remove the shield through real impact events before applying lethal damage.
                    health.SetPolicy(GameOverPolicy.NormalMode);for(int i=0;i<5&&health.CurrentHealth>0;i++)health.ApplyImpactDamage(100,health.transform.position);
                    yield return Capture("defeated");reset.ResetExperience();yield return Capture("restarted");
                    spawner.EndScenario();
                }
            }
            finally{Time.timeScale=1;reset.ResetExperience();health.SetPolicy(policy);router.InputSuspended=input;weapon.AutoFireOnLock=fire;gaze.enabled=looking;Directory.CreateDirectory(output);notes.Add("passed="+passed);File.WriteAllLines(Path.Combine(output,"verification.txt"),notes);}
            complete(passed);
        }
        private static string Korean(PowerUpType t)=>t switch{PowerUpType.Heal=>"선체 회복",PowerUpType.Shield=>"방어막",PowerUpType.DamageBoost=>"공격력 강화",PowerUpType.RapidFire=>"속사",PowerUpType.MultiLaser=>"다중 레이저",PowerUpType.Overdrive=>"과부하 레이저",PowerUpType.SlowTime=>"운석 감속",_=>"전자기 충격파"};
    }
}
