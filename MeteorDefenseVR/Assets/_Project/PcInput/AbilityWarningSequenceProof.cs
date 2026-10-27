using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.PowerUps;
using MeteorDefenseVR.UI;

using TMPro;

using UnityEngine;

using Object = UnityEngine.Object;

namespace MeteorDefenseVR.PcInput { public static class AbilityWarningSequenceProof {
        static class Assert {
            public static void IsTrue(bool value,string message="Expected true"){if(!value)throw new InvalidOperationException(message);}
            public static void IsFalse(bool value,string message="Expected false")=>IsTrue(!value,message);
            public static void IsNotNull(object value)=>IsTrue(value!=null,"Expected non-null");
            public static void AreEqual<T>(T expected,T actual,string message="Values differ")=>IsTrue(EqualityComparer<T>.Default.Equals(expected,actual),message+": "+expected+" / "+actual);
            public static void LessOrEqual(int actual,int maximum)=>IsTrue(actual<=maximum,"Above maximum");
            public static void Greater(int actual,int minimum)=>IsTrue(actual>minimum,"Not greater");
            public static void IsEmpty(ICollection values,string message)=>IsTrue(values.Count==0,message);
        }
        sealed class HeldRay : IGazeProvider
        {
            public Ray Ray; public bool Valid;
            public bool IsTrackingValid => Valid;
            public Ray GetGazeRay() => Ray;
            public Vector3 GetGazeOrigin() => Ray.origin;
            public Vector3 GetGazeDirection() => Ray.direction;
        }
        public static IEnumerator Run(string root)
        {

            Directory.CreateDirectory(root);
            yield return null; yield return null;
            var warm = Object.FindAnyObjectByType<ReadyPrewarmController>();
            float warmDeadline = Time.realtimeSinceStartup + 45;
            while (!warm.IsComplete && Time.realtimeSinceStartup < warmDeadline) yield return null;
            Assert.IsTrue(warm.IsComplete);
            var director = Object.FindAnyObjectByType<SfPresentationDirector>();
            float ready = Time.realtimeSinceStartup + 45;
            while (director.IsBootPresenting && Time.realtimeSinceStartup < ready) yield return null;
            Assert.IsFalse(director.IsBootPresenting);
            var router = Object.FindAnyObjectByType<GazeInputRouter>();
            var caster = Object.FindAnyObjectByType<GazeRaycaster>();
            var weapon = Object.FindAnyObjectByType<LaserWeapon>();
            var manager = PowerUpManager.Instance;
            var health = Object.FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            var spawner = Object.FindAnyObjectByType<MeteorSpawner>();
            var hud = Object.FindAnyObjectByType<MissionHudController>(FindObjectsInactive.Include);
            var overlay = Object.FindAnyObjectByType<GameplayHudOverlayView>(FindObjectsInactive.Include);
            var view = Object.FindAnyObjectByType<PowerUpWeaponFeedback>(FindObjectsInactive.Include);
            var camera = Camera.main;
            var settings = Resources.Load<PowerUpSettings>("PowerUpSettings");
            var provider = caster.Provider; bool input = router.InputSuspended, fire = weapon.AutoFireOnLock;
            float step = Time.captureDeltaTime;
            var ray = new HeldRay();
            var rows = new List<string>(); var issues = new List<string>();
            string state = "setup";
            void Log(string message) => rows.Add($"frame={Time.frameCount};game={Time.time:F6};wall={Time.realtimeSinceStartup:F6};state={state};hp={health.CurrentHealth};score={hud.Score};buffs={string.Join("|", manager.ActiveBuffs.Select(b => b.Type+":"+b.Remaining.ToString("F3")+":"+b.ShieldHits))};{message}");
            int collected = 0, blocked = 0;
            void Acquired(PowerUpType type) { collected++; Log("acquired=" + type); }
            void Blocked() { blocked++; Log("shield-blocked"); }
            void Health(float current, float max) => Log("health-changed=" + current);
            void Score(int score) => Log("score-changed=" + score);
            manager.PowerUpAcquired += Acquired; manager.ShieldBlocked += Blocked;
            health.HealthChanged += Health; hud.ScoreChanged += Score;
            try
            {
                router.InputSuspended = true; caster.SetProvider(ray); weapon.AutoFireOnLock = false;
                Assert.AreEqual(3, settings.maximumActiveBuffs);
                GameFlowManager.Instance.StartMainGame(); spawner.BeginScenario();
                yield return new WaitForSecondsRealtime(2);
                Time.captureDeltaTime = 1f / 60f;
                foreach (var size in new[] { new Vector2Int(800, 600), new Vector2Int(1280, 720) })
                {
                    manager.ResetSystem(); health.ResetHealth(); spawner.ClearScenarioRound();
                    using var proof = new PcStabilityProbe(camera, size.x, size.y);
                    string folder = Path.Combine(root, $"{size.x}x{size.y}");
                    MeteorController target = spawner.SpawnScenario(camera.ViewportToWorldPoint(new Vector3(.5f, .57f, 15)), Vector3.zero, 1.6f, 10, 2, MeteorType.Large);
                    target.enabled = false;
                    IEnumerator Capture(string label)
                    {
                        state = $"{size.x}x{size.y}/{label}";
                        yield return null; yield return null;
                        Canvas.ForceUpdateCanvases(); PcStabilityRenderDriver.RenderForProof(camera);
                        string path = Path.Combine(folder, label); Directory.CreateDirectory(path);
                        proof.SaveGameOnlyProof(path, router);
                        var text = Object.FindObjectsByType<TMP_Text>().Where(t => t.isActiveAndEnabled && !string.IsNullOrEmpty(t.text)).ToArray();
                        File.WriteAllLines(Path.Combine(path, "state.txt"), new[] { $"frame={Time.frameCount};game={Time.time:F6};hp={health.CurrentHealth};score={hud.Score};activeBuffs={manager.ActiveBuffCount};critical={director.CriticalNoticeBounds};feedback={overlay.FeedbackValue.text}" }
                            .Concat(text.Select(t => { t.ForceMeshUpdate(); return $"{t.name}: font={t.fontSize};truncated={t.isTextTruncated};bounds={TelemetryReadabilityProof.ViewRect(camera,t.rectTransform)};preferred={t.preferredWidth}x{t.preferredHeight};available={t.rectTransform.rect.size};text={t.GetParsedText()}"; })));
                        if (view.Visible)
                        {
                            var labelText = view.Readout; string parsed = labelText.GetParsedText();
                            foreach (var buff in manager.ActiveBuffs)
                            {
                                string expected = PowerUpHudView.PlayerName(buff.Type) + " " + (buff.ShieldHits >= 0 ? buff.ShieldHits : Mathf.CeilToInt(buff.Remaining)) + (buff.ShieldHits >= 0 ? "회" : "초");
                                Assert.IsTrue(parsed.Contains(expected), expected + " versus " + parsed);
                            }
                            if (labelText.isTextTruncated || labelText.preferredHeight > labelText.rectTransform.rect.height + 1 || labelText.preferredWidth > labelText.rectTransform.rect.width + 1) issues.Add(state + ": buff clipped");
                            Rect bounds = TelemetryReadabilityProof.ViewRect(camera, (RectTransform)labelText.transform.parent);
                            if (bounds.xMin < 0 || bounds.xMax > 1 || bounds.yMin < 0 || bounds.yMax > 1) issues.Add(state + ": buff offscreen");
                            if (target.IsTargetable && bounds.Overlaps(TelemetryReadabilityProof.MeteorRect(camera, target))) issues.Add(state + ": buff covers target");
                            if (overlay.FeedbackValue.gameObject.activeInHierarchy && bounds.Overlaps(TelemetryReadabilityProof.ViewRect(camera, overlay.FeedbackValue.rectTransform))) issues.Add(state + ": buff overlaps feedback");
                            if (director.CriticalNoticeBounds != null && bounds.Overlaps(TelemetryReadabilityProof.ViewRect(camera, director.CriticalNoticeBounds))) issues.Add(state + ": critical warning overlaps status/buffs");
                        }
                        Log("capture=" + label + ";readout=" + view.Readout.GetParsedText().Replace('\n', '|'));
                    }
                    IEnumerator Acquire(PowerUpType type)
                    {
                        state = $"{size.x}x{size.y}/collect-{type}";
                        var item = Object.FindObjectsByType<PowerUpItem>(FindObjectsInactive.Include).First(i => !i.gameObject.activeSelf);
                        item.Spawn(manager.GetDefinition(type), camera.ViewportToWorldPoint(new Vector3(.63f,.55f,10)), camera.transform, settings.itemLifetime, settings.gazeCollectDuration, settings.driftSpeed);
                        int initial = collected;
                        ray.Ray = new Ray(camera.transform.position, (item.transform.position-camera.transform.position).normalized); ray.Valid = true;
                        int deadline = Time.frameCount + 100;
                        while (item.IsAvailable && Time.frameCount < deadline) yield return null;
                        ray.Valid = false;
                        Assert.AreEqual(initial + 1, collected, "Production raycast/dwell must collect once");
                        Assert.LessOrEqual(manager.ActiveBuffCount, 3);
                    }
                    Assert.IsTrue(health.ApplyImpactDamage(90, camera.transform.position + camera.transform.forward));
                    yield return Acquire(PowerUpType.Shield);
                    yield return Acquire(PowerUpType.RapidFire);
                    yield return Acquire(PowerUpType.MultiLaser);
                    Assert.AreEqual(3, manager.ActiveBuffCount);
                    yield return Capture("maximum-three");
                    for (int i=0; i<3; i++)
                    {
                        int before = blocked; float hp = health.CurrentHealth;
                        health.ApplyImpactDamage(10, camera.transform.position + camera.transform.forward);
                        Assert.AreEqual(before+1, blocked); Assert.AreEqual(hp, health.CurrentHealth);
                        yield return Capture("shield-consumed-"+(i+1));
                    }
                    Assert.IsFalse(manager.IsBuffActive(PowerUpType.Shield));
                    yield return Acquire(PowerUpType.Shield);
                    // Wait for the real periodic critical warning; do not force its age or message.
                    while (director.CriticalNoticeBounds != null) yield return null;
                    float deadlineWarning = Time.time + 7;
                    while (director.CriticalNoticeBounds == null && Time.time < deadlineWarning) yield return null;
                    Assert.IsNotNull(director.CriticalNoticeBounds);
                    Assert.AreEqual(3, manager.ActiveBuffCount);
                    yield return Capture("warning-three-before-emp");
                    int destroyed=0; target.Destroyed += _ => {destroyed++;Log("emp-target-destroyed");};
                    int scoreBefore=hud.Score;
                    yield return Acquire(PowerUpType.EMP);
                    Assert.AreEqual(1,destroyed); Assert.IsFalse(target.IsTargetable);
                    Assert.Greater(hud.Score,scoreBefore); Assert.AreEqual(3,manager.ActiveBuffCount);
                    yield return Capture("emp-score-warning-three");
                    for(int i=0;i<12;i++)yield return null;
                    yield return Capture("emp-expansion");
                    // A real subsequent kill replaces the EMP notice with score while the same warning is alive.
                    var scoreTarget=spawner.SpawnScenario(camera.ViewportToWorldPoint(new Vector3(.5f,.57f,15)),Vector3.zero,1.2f,10,1,MeteorType.Normal);
                    scoreTarget.enabled=false;
                    Assert.IsTrue(weapon.TryAuxiliaryHit(scoreTarget,1));
                    yield return Capture("score-replaces-emp-warning");
                    float beforeHeal=health.CurrentHealth;
                    yield return Acquire(PowerUpType.Heal);
                    Assert.IsTrue(Mathf.Abs(health.CurrentHealth-beforeHeal-health.MaxHealth*manager.GetDefinition(PowerUpType.Heal).Power)<.001f);
                    yield return Capture("heal-applied");
                    for(int i=0;i<12;i++)yield return null;
                    yield return Capture("heal-settled");
                    float expiryDeadline=Time.time+12;
                    while(manager.IsBuffActive(PowerUpType.RapidFire)&&manager.ActiveBuffs.First(b=>b.Type==PowerUpType.RapidFire).Remaining>1.8f&&Time.time<expiryDeadline)yield return null;
                    yield return Capture("timed-ending");
                    while((manager.IsBuffActive(PowerUpType.RapidFire)||manager.IsBuffActive(PowerUpType.MultiLaser))&&Time.time<expiryDeadline)yield return null;
                    Assert.IsFalse(manager.IsBuffActive(PowerUpType.RapidFire));Assert.IsFalse(manager.IsBuffActive(PowerUpType.MultiLaser));
                    Assert.AreEqual(1,manager.ActiveBuffCount);
                    yield return Capture("timed-expired-shield-remains");
                    for(int i=0;i<3;i++)health.ApplyImpactDamage(10,camera.transform.position);
                    Assert.AreEqual(0,manager.ActiveBuffCount);
                    yield return Capture("all-consumed-or-expired");
                }
                File.WriteAllLines(Path.Combine(root,"geometry-issues.txt"),issues);
                Assert.IsEmpty(issues,string.Join("\n",issues));
            }
            finally
            {
                Log("end"); File.WriteAllLines(Path.Combine(root,"events.txt"),rows);
                manager.PowerUpAcquired-=Acquired;manager.ShieldBlocked-=Blocked;health.HealthChanged-=Health;hud.ScoreChanged-=Score;
                Time.captureDeltaTime=step;ray.Valid=false;caster.SetProvider(provider);router.InputSuspended=input;weapon.AutoFireOnLock=fire;manager.ResetSystem();spawner.EndScenario();
            }
        }
    }
}
