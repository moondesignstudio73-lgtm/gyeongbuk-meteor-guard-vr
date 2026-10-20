using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.Visual;
using TMPro;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MeteorDefenseVR.PcInput
{
    public static class TelemetryReadabilityProof
    {
        public static Rect ViewRect(Camera camera,RectTransform rect)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            Vector2 min=Vector2.one*float.PositiveInfinity,max=Vector2.one*float.NegativeInfinity;
            foreach(var corner in corners){Vector2 p=camera.WorldToViewportPoint(corner);min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        public static Rect MeteorRect(Camera camera,MeteorController meteor)
        {
            Vector3 p=camera.WorldToViewportPoint(meteor.transform.position);
            if(p.z<=0)return new Rect(-10,-10,0,0);
            float radius=Mathf.Max(1,meteor.CollisionRadius*1.4f);
            Vector3 up=camera.WorldToViewportPoint(meteor.transform.position+camera.transform.up*radius);
            Vector3 right=camera.WorldToViewportPoint(meteor.transform.position+camera.transform.right*radius);
            float dx=Mathf.Abs(right.x-p.x),dy=Mathf.Abs(up.y-p.y);
            return new Rect(p.x-dx,p.y-dy,dx*2,dy*2);
        }
        public static IEnumerator Run(GazeInputRouter router,string output,bool validate,Action<bool> complete)
        {
            var camera=Camera.main;var flow=GameFlowManager.Instance;
            var spawner=Object.FindAnyObjectByType<MeteorSpawner>();var health=Object.FindAnyObjectByType<PlayerHealth>();
            var telemetry=Object.FindAnyObjectByType<CockpitTelemetryPresentation>();
            var weapon=Object.FindAnyObjectByType<MeteorDefenseVR.Combat.LaserWeapon>();
            bool priorInput=router.InputSuspended,priorFire=weapon.AutoFireOnLock;
            router.InputSuspended=true;weapon.AutoFireOnLock=false;
            var notes=new List<string>{"Explicit live-scene health/position/velocity fixtures; frozen meteor movement; not natural input/HMD play."};
            var spawned=new List<MeteorController>();bool passed=true;
            MeteorController Spawn(Vector3 point,float speed)
            {
                var m=spawner.SpawnScenario(point,(spawner.ShipCenter-point).normalized*speed,2.4f,10);
                m.enabled=false;spawned.Add(m);return m;
            }
            try
            {
                flow.StartMainGame();
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(800,600),new Vector2Int(2560,1080)})
                {
                    using var proof=new PcStabilityProbe(camera,size.x,size.y);
                    camera.GetComponent<WorldUiSafeArea>().Apply();
                    spawner.BeginScenario();health.ResetHealth();yield return new WaitForSecondsRealtime(.5f);
                    MeteorController front=null,rear=null;
                    foreach(string state in new[]{"normal","low-health","multiple","rear-to-front","target-crossing","right-target-crossing"})
                    {
                        if(state=="low-health")
                        {health.Tick(1);health.ApplyDamage(85);front=Spawn(camera.ViewportToWorldPoint(new Vector3(.55f,.6f,45)),3);}
                        if(state=="multiple")
                        {
                            Spawn(camera.ViewportToWorldPoint(new Vector3(.15f,.55f,60)),2);
                            Spawn(camera.ViewportToWorldPoint(new Vector3(.82f,.68f,55)),2);
                            rear=Spawn(camera.transform.TransformPoint(new Vector3(-25,2,-35)),10);
                        }
                        if(state=="rear-to-front")
                        {rear.transform.position=camera.transform.TransformPoint(new Vector3(25,2,35));rear.SetMovementDirection(spawner.ShipCenter-rear.transform.position);}
                        Vector2? crossedPanelCenter=null;
                        if(state=="target-crossing" || state=="right-target-crossing")
                        {
                            var current=GameObject.Find(state=="target-crossing"?"ShipStatusHologram":"ThreatStatusHologram").GetComponent<RectTransform>();
                            Vector2 center=ViewRect(camera,current).center;
                            crossedPanelCenter=center;
                            front.transform.position=camera.ViewportToWorldPoint(new Vector3(center.x,center.y,35));
                            front.SetMovementDirection(spawner.ShipCenter-front.transform.position);
                        }
                        yield return new WaitForSecondsRealtime(.65f);
                        telemetry.Refresh();Canvas.ForceUpdateCanvases();PcStabilityRenderDriver.RenderForProof(camera);
                        string folder=Path.Combine(output,$"{size.x}x{size.y}",state);Directory.CreateDirectory(folder);
                        proof.SaveGameOnlyProof(folder,router);
                        bool valid=true;
                        foreach(string name in new[]{"LiveShipStatus","LiveThreatAnalysis"})
                        {
                            var text=GameObject.Find(name).GetComponent<TMP_Text>();text.ForceMeshUpdate(true,true);
                            var glyphs=text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(c=>c.isVisible&&char.IsLetterOrDigit(c.character)).ToArray();
                            float Height(TMP_CharacterInfo c)=>Mathf.Abs(camera.WorldToScreenPoint(text.transform.TransformPoint(c.topLeft)).y-camera.WorldToScreenPoint(text.transform.TransformPoint(c.bottomLeft)).y);
                            float minGlyph=glyphs.Min(Height),minDigit=glyphs.Where(c=>char.IsDigit(c.character)).Select(Height).DefaultIfEmpty(1000).Min();
                            var panel=(RectTransform)text.transform.parent;Rect bounds=ViewRect(camera,panel);
                            bool clear=bounds.xMin>=.01f&&bounds.xMax<=.99f&&bounds.yMin>=.01f&&bounds.yMax<=.99f
                                && !bounds.Overlaps(new Rect(.42f,.4f,.16f,.2f));
                            foreach(var meteor in spawner.ActiveMeteors)if(meteor.IsTargetable)clear &= !bounds.Overlaps(MeteorRect(camera,meteor));
                            var warnings=GameObject.Find("ThreatIndicators");
                            if(warnings!=null)foreach(var label in warnings.GetComponentsInChildren<TextMeshProUGUI>())clear &= !bounds.Overlaps(ViewRect(camera,label.rectTransform));
                            bool fits=!text.isTextTruncated && text.preferredWidth<=text.rectTransform.rect.width+.5f && text.preferredHeight<=text.rectTransform.rect.height+.5f;
                            string parsed=text.GetParsedText();
                            bool glyphsPresent=parsed.Where(char.IsLetterOrDigit).All(c=>text.font.HasCharacter(c,true));
                            bool values=name=="LiveShipStatus"
                                ? parsed.Contains($"방어막 {Mathf.RoundToInt(health.HealthNormalized*100)}%") && parsed.Contains($"피해 {health.TotalDamageTaken:0}")
                                : parsed.Contains($"위협 {spawner.ActiveMeteors.Count(m=>m.IsTargetable)}개");
                            if(crossedPanelCenter.HasValue && ((state=="target-crossing" && name=="LiveShipStatus") || (state=="right-target-crossing" && name=="LiveThreatAnalysis")))
                                clear &= Vector2.Distance(bounds.center,crossedPanelCenter.Value)>.05f;
                            if(name=="LiveThreatAnalysis" && rear!=null)
                                values &= parsed.Contains(state=="multiple"?"뒤쪽":"오른쪽")
                                    && parsed.Contains($"{Vector3.Distance(spawner.ShipCenter,rear.transform.position):0}m");
                            valid &= minGlyph>=14 && minDigit>=18 && fits && clear && glyphsPresent && values && !text.enableAutoSizing;
                            notes.Add($"{size}/{state}/{name}: minGlyphPixels={minGlyph} minDigitPixels={minDigit} fits={fits} clear={clear} glyphsPresent={glyphsPresent} values={values} panel={bounds} text={parsed.Replace('\n','|')}");
                        }
                        if(validate)passed &= valid;
                    }
                    spawner.EndScenario();
                }
            }
            finally
            {
                spawner.EndScenario();foreach(var m in spawned)if(m!=null)m.enabled=true;
                router.InputSuspended=priorInput;weapon.AutoFireOnLock=priorFire;
                Object.FindAnyObjectByType<OperationsResetController>().ResetExperience();
                Directory.CreateDirectory(output);notes.Add("passed="+passed);File.WriteAllLines(Path.Combine(output,"measurements.txt"),notes);
            }
            complete(passed);
        }
    }
}
