using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.PowerUps;
using MeteorDefenseVR.UI;
using TMPro;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.Tests
{
    public sealed class BossAbilityReadabilityTests
    {
        sealed class HeldRay : IGazeProvider
        {
            public Ray Ray; public bool Valid;
            public bool IsTrackingValid => Valid;
            public Ray GetGazeRay() => Ray;
            public Vector3 GetGazeOrigin() => Ray.origin;
            public Vector3 GetGazeDirection() => Ray.direction;
        }
        [UnityTearDown] public IEnumerator Cleanup() { Time.timeScale=1; if(Application.isPlaying)yield return new ExitPlayMode(); }
        [UnityTest, Timeout(240000)] public IEnumerator ActualBossStagesWithAbilities()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");
            yield return new EnterPlayMode(); yield return Review();
        }
        static IEnumerator Review()
        {
            const string root="Docs/DeliveryQA/boss-ability-20261028/Editor";
            Directory.CreateDirectory(root);
            float deadline=Time.realtimeSinceStartup+45;
            while(Object.FindAnyObjectByType<ReadyScreenController>()==null&&Time.realtimeSinceStartup<deadline)yield return null;
            var ready=Object.FindAnyObjectByType<ReadyScreenController>();Assert.IsNotNull(ready);
            while(!ready.CanStart&&Time.realtimeSinceStartup<deadline)yield return null;Assert.IsTrue(ready.CanStart);
            var director=Object.FindAnyObjectByType<SfPresentationDirector>();
            while(director.IsBootPresenting&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsFalse(director.IsBootPresenting);
            var camera=Camera.main;var router=Object.FindAnyObjectByType<GazeInputRouter>();
            var gaze=Object.FindAnyObjectByType<GazeRaycaster>();var weapon=Object.FindAnyObjectByType<LaserWeapon>();
            var manager=PowerUpManager.Instance;var spawner=Object.FindAnyObjectByType<MeteorSpawner>();
            var climax=Object.FindAnyObjectByType<BossClimaxController>(FindObjectsInactive.Include);
            var health=Object.FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            var hud=Object.FindAnyObjectByType<MissionHudController>(FindObjectsInactive.Include);
            var overlay=Object.FindAnyObjectByType<GameplayHudOverlayView>();var feedback=Object.FindAnyObjectByType<PowerUpWeaponFeedback>(FindObjectsInactive.Include);
            var reset=Object.FindAnyObjectByType<OperationsResetController>();
            var settings=Resources.Load<PowerUpSettings>("PowerUpSettings");
            bool input=router.InputSuspended,auto=weapon.AutoFireOnLock;var provider=gaze.Provider;var ray=new HeldRay();
            string state="setup",stageMessage="";int acquired=0;var rows=new List<string>();var issues=new List<string>();MeteorController boss=null;
            void Log(string message)=>rows.Add($"frame={Time.frameCount};game={Time.time:F6};wall={Time.realtimeSinceStartup:F6};state={state};phase={climax.CurrentStage};bossHp={(boss!=null?boss.CurrentHealth:0)}/{(boss!=null?boss.MaxHealth:0)};buffs={string.Join("|",manager.ActiveBuffs.Select(b=>b.Type+":"+b.Remaining.ToString("F3")+":"+b.ShieldHits))};{message}");
            void Stage(BossClimaxStage stage,string message){stageMessage=message;Log("stage-event="+stage+";copy="+message.Replace('\n','|'));}
            void Acquired(PowerUpType type){acquired++;Log("acquired="+type);}
            void Score(int score)=>Log("score="+score);
            climax.StageChanged+=Stage;manager.PowerUpAcquired+=Acquired;hud.ScoreChanged+=Score;
            try
            {
                router.InputSuspended=true;gaze.SetProvider(ray);weapon.AutoFireOnLock=false;
                foreach(var size in new[]{new Vector2Int(800,600),new Vector2Int(1280,720)})
                {
                    reset.ResetExperience();GameFlowManager.Instance.StartMainGame();spawner.BeginScenario();
                    yield return new WaitForSecondsRealtime(2);
                    using var proof=new PcStabilityProbe(camera,size.x,size.y);
                    string folder=Path.Combine(root,size.x+"x"+size.y);
                    PowerUpItem SpawnItem(PowerUpType type)
                    {
                        var item=Object.FindObjectsByType<PowerUpItem>(FindObjectsInactive.Include).First(i=>!i.gameObject.activeSelf);
                        item.Spawn(manager.GetDefinition(type),camera.ViewportToWorldPoint(new Vector3(.66f,.53f,9)),camera.transform,settings.itemLifetime,settings.gazeCollectDuration,settings.driftSpeed);
                        return item;
                    }
                    IEnumerator Collect(PowerUpItem item)
                    {
                        int before=acquired;ray.Valid=true;
                        float end=Time.realtimeSinceStartup+3;
                        while(item.IsAvailable&&Time.realtimeSinceStartup<end){ray.Ray=new Ray(camera.transform.position,(item.transform.position-camera.transform.position).normalized);yield return null;}
                        ray.Valid=false;Assert.AreEqual(before+1,acquired);Assert.LessOrEqual(manager.ActiveBuffCount,3);
                    }
                    IEnumerator Capture(string label,bool warning=false)
                    {
                        state=size.x+"x"+size.y+"/"+label;
                        yield return null;yield return null;
                        bool done=false;Exception failure=null;
                        void Save(){if(done)return;try{
                        Canvas.ForceUpdateCanvases();PcStabilityRenderDriver.RenderForProof(camera);
                        string path=Path.Combine(folder,label);Directory.CreateDirectory(path);proof.SaveGameOnlyProof(path,router);
                        var texts=Object.FindObjectsByType<TMP_Text>().Where(t=>t.isActiveAndEnabled&&!string.IsNullOrEmpty(t.text)).ToArray();
                        File.WriteAllLines(Path.Combine(path,"state.txt"),new[]{$"frame={Time.frameCount};game={Time.time:F6};wall={Time.realtimeSinceStartup:F6};phase={climax.CurrentStage};hp={(boss!=null?boss.CurrentHealth:0)}/{(boss!=null?boss.MaxHealth:0)};shell={(boss!=null?boss.GetComponent<BossMeteorView>().Shell.Stage:0)};core={(boss!=null&&boss.GetComponent<BossMeteorView>().Shell.CoreVisible)};buffs={manager.ActiveBuffCount};message={stageMessage.Replace('\n','|')}"}.Concat(texts.Select(t=>{t.ForceMeshUpdate();return $"{t.name}:font={t.fontSize};truncated={t.isTextTruncated};textBounds={t.textBounds.size};rect={t.rectTransform.rect.size};text={t.GetParsedText().Replace('\n','|')}";})));
                        if(warning&&overlay.FeedbackValue.GetParsedText()!=WorldTextPresentation.PlayerMessage(stageMessage))issues.Add(state+": warning replaced");
                        foreach(var text in new TMP_Text[]{overlay.FeedbackValue,feedback.Readout}.Where(t=>t.isActiveAndEnabled))
                            if(text.isTextTruncated||text.textBounds.size.x>text.rectTransform.rect.width+1||text.textBounds.size.y>text.rectTransform.rect.height+1)issues.Add(state+": clipped "+text.name);
                        var panels=new List<Rect>();
                        if(overlay.FeedbackPanel.activeInHierarchy)panels.Add(TelemetryReadabilityProof.ViewRect(camera,(RectTransform)overlay.FeedbackPanel.transform));
                        if(feedback.Visible)
                        {
                            var b=TelemetryReadabilityProof.ViewRect(camera,(RectTransform)feedback.Readout.transform.parent);
                            if(panels.Any(p=>p.Overlaps(b)))issues.Add(state+": feedback overlaps ability list");panels.Add(b);
                            foreach(var buff in manager.ActiveBuffs)
                            {
                                string expected=PowerUpHudView.PlayerName(buff.Type)+" "+(buff.ShieldHits>=0?buff.ShieldHits:Mathf.CeilToInt(buff.Remaining))+(buff.ShieldHits>=0?"회":"초");
                                Assert.IsTrue(feedback.Readout.GetParsedText().Contains(expected),expected);
                            }
                        }
                        if(boss!=null&&boss.IsTargetable)
                        {
                            // Visible mesh bounds, not the much larger gameplay collision sphere.
                            foreach(var renderer in boss.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled&&!r.forceRenderingOff&&r.GetComponent<TMP_Text>()==null))
                            {
                                var bound=renderer.bounds;Vector2 min=Vector2.one*float.PositiveInfinity,max=Vector2.one*float.NegativeInfinity;
                                for(int i=0;i<8;i++){var p=camera.WorldToViewportPoint(bound.center+Vector3.Scale(bound.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
                                var rect=Rect.MinMaxRect(min.x,min.y,max.x,max.y);
                                if(panels.Any(p=>p.Overlaps(rect)))issues.Add(state+": panel covers "+renderer.name);
                            }
                        }
                        Log("capture="+label+";visibleFeedback="+overlay.FeedbackValue.GetParsedText().Replace('\n','|'));
                        }catch(Exception e){failure=e;}finally{done=true;}}
                        var driver=(PcStabilityRenderDriver)typeof(PcStabilityProbe).GetField("driver",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(proof);
                        driver.RenderCompleted+=Save;
                        try{float end=Time.realtimeSinceStartup+5;while(!done&&Time.realtimeSinceStartup<end)yield return null;Assert.IsTrue(done,"Render timeout");if(failure!=null)Assert.Fail(failure.ToString());}
                        finally{driver.RenderCompleted-=Save;}
                    }
                    foreach(var type in new[]{PowerUpType.Shield,PowerUpType.RapidFire,PowerUpType.MultiLaser})yield return Collect(SpawnItem(type));
                    Assert.AreEqual(3,manager.ActiveBuffCount);
                    health.ApplyImpactDamage(40,camera.transform.position);
                    // Item exists before the warning; no boss damage or invented score during arrival.
                    var heal=SpawnItem(PowerUpType.Heal);
                    climax.ResetClimax();climax.BeginClimax();Assert.AreEqual(BossClimaxStage.Warning,climax.CurrentStage);
                    yield return Capture("warning-three",true);
                    yield return Collect(heal);yield return Capture("warning-acquired-heal",true);
                    float endStage=Time.realtimeSinceStartup+5;
                    while(climax.CurrentStage==BossClimaxStage.Warning&&Time.realtimeSinceStartup<endStage)yield return null;
                    Assert.AreEqual(BossClimaxStage.Narration,climax.CurrentStage);yield return Capture("narration-three",true);
                    while(climax.ActiveBoss==null&&Time.realtimeSinceStartup<endStage)yield return null;
                    boss=climax.ActiveBoss;Assert.IsNotNull(boss);
                    boss.enabled=false;var motion=boss.GetComponent<BossStageMotion>();motion.enabled=false;
                    var shell=boss.GetComponent<BossMeteorView>().Shell;
                    Log("actual-spawn;position="+boss.transform.position+";scale="+boss.transform.localScale+";damage="+weapon.EffectiveDamage);
                    Assert.AreEqual(0,shell.Stage);yield return Capture("active-intact-three");
                    void Damage(MeteorController m,float amount){weapon.AutoFireOnLock=false;Log("actual-weapon-damage="+amount);}
                    boss.Damaged+=Damage;
                    try
                    {
                        IEnumerator Shot()
                        {
                            float before=boss.CurrentHealth;ray.Valid=true;ray.Ray=new Ray(camera.transform.position,(boss.transform.position-camera.transform.position).normalized);weapon.AutoFireOnLock=true;
                            float end=Time.realtimeSinceStartup+4;while(boss.CurrentHealth==before&&Time.realtimeSinceStartup<end)yield return null;
                            weapon.AutoFireOnLock=false;ray.Valid=false;Assert.Less(boss.CurrentHealth,before);
                        }
                        while(boss.IsTargetable&&shell.Stage<1)yield return Shot();
                        Assert.IsTrue(boss.IsTargetable&&shell.CoreVisible);yield return Capture("breach-hit");
                        yield return new WaitForSecondsRealtime(.7f);yield return Capture("breach-settled");
                        yield return Collect(SpawnItem(PowerUpType.Heal));yield return Capture("breach-acquired-heal");
                        while(boss.IsTargetable&&shell.Stage<2)yield return Shot();
                        Assert.IsTrue(boss.IsTargetable&&shell.CoreVisible);yield return Capture("core-hit");
                        yield return new WaitForSecondsRealtime(.7f);yield return Capture("core-settled");
                        // Refresh real timed items via collection if needed, retaining the legal same three types.
                        yield return Collect(SpawnItem(PowerUpType.RapidFire));yield return Capture("core-acquired-rapid");
                        int score=hud.Score;
                        while(boss.IsTargetable)yield return Shot();
                        Assert.Greater(hud.Score,score);yield return Capture("actual-boss-destroyed");
                    }
                    finally{boss.Damaged-=Damage;boss.enabled=true;motion.enabled=true;}
                    boss=null;reset.ResetExperience();yield return null;
                }
                File.WriteAllLines(Path.Combine(root,"issues.txt"),issues);Assert.IsEmpty(issues,string.Join("\n",issues));
            }
            finally
            {
                File.WriteAllLines(Path.Combine(root,"events.txt"),rows);
                climax.StageChanged-=Stage;manager.PowerUpAcquired-=Acquired;hud.ScoreChanged-=Score;
                weapon.AutoFireOnLock=auto;gaze.SetProvider(provider);router.InputSuspended=input;spawner.EndScenario();reset.ResetExperience();
            }
        }
    }
}
