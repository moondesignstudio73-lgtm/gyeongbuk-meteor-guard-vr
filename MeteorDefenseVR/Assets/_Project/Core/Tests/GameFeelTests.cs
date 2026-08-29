using System.Collections;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Audio;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Launch;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.Progression;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeteorDefenseVR.Tests
{
    public sealed class GameFeelTests
    {
        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [Test]
        public void AuthoredWavesEscalateWithoutChangingTotalAndLaunchStaysShort()
        {
            var waves = Enumerable.Range(1,3).Select(i => AssetDatabase.LoadAssetAtPath<MeteorWaveData>("Assets/_Project/Meteor/Waves/Wave_0"+i+".asset")).ToArray();
            Assert.That(waves.Sum(w => w.TotalMeteors), Is.EqualTo(21));
            Assert.That(waves[0].Spawns.All(s => s.MeteorType == MeteorType.Normal && s.Size >= 1.2f), Is.True);
            float FastFraction(MeteorWaveData w) => (float)w.Spawns.Where(s=>s.MeteorType==MeteorType.Fast).Sum(s=>s.Count)/w.TotalMeteors;
            Assert.That(FastFraction(waves[2]), Is.GreaterThan(FastFraction(waves[1])));
            Assert.That(waves[2].Spawns.Select(s=>s.MeteorType).Distinct().Count(), Is.EqualTo(3));
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            var launch = Object.FindAnyObjectByType<LaunchSequenceController>();
            using var serialized = new SerializedObject(launch);
            float duration = new[]{"hangarDuration","doorDuration","engineDuration","launchDuration","spaceDuration","missionStartDuration"}.Sum(n=>serialized.FindProperty(n).floatValue) + 3*serialized.FindProperty("countdownInterval").floatValue;
            Assert.That(duration, Is.InRange(2f,3f));
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/PcInput/Fonts/PcTestKorean.asset");
            Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
            Assert.That(font.HasCharacters("SYSTEM INITIALIZING GAZE SENSOR DEFENSE SYSTEM ONLINE SHIELD LOW 방어막 위험 NICE COMBO GREAT LOCK"), Is.True);
            Assert.That(PlayerSettings.SplashScreen.show, Is.False);
            var title = Object.FindObjectsByType<WorldTextPresentation>(FindObjectsInactive.Include).Single(b=>b.name=="ReadyTitle");
            Assert.That(title.GetComponent<TextMesh>().font.dynamic, Is.False);
            using var bridgeData = new SerializedObject(title);
            Assert.That(bridgeData.FindProperty("legacyFont").objectReferenceValue, Is.Not.Null);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator BootShowsRealProgressAndHandsOffWithoutMinimumWait()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            // Hold the loader for a deterministic game-only rendering fixture (no webcam components).
            Object.FindAnyObjectByType<BootstrapSceneLoader>().enabled = false;
            yield return new EnterPlayMode();
            yield return VerifyBoot();
            yield return new ExitPlayMode();
        }
        private static IEnumerator VerifyBoot()
        {
            var loader = Object.FindAnyObjectByType<BootstrapSceneLoader>();
            var view = loader.GetComponent<SystemBootView>();
            Assert.That(view.IsVisible, Is.True);
            Assert.That(loader.Progress, Is.Zero);
            var canvas = view.GetComponentInChildren<Canvas>();
            var camera = view.GetComponentInChildren<Camera>();
            Assert.That(camera.cullingMask, Is.Zero);
            var target = new RenderTexture(1280,720,24);
            var image = new Texture2D(1280,720,TextureFormat.RGB24,false);
            var previous = RenderTexture.active;
            try
            {
                target.Create(); camera.targetTexture=target;
                // The runtime overlay does not need culling; an explicit camera capture does.
                camera.cullingMask=1<<canvas.gameObject.layer;
                canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1;
                yield return null;
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
                Directory.CreateDirectory("Docs/GameFeelQA/After/boot");
                File.WriteAllBytes("Docs/GameFeelQA/After/boot/stability-game-render.png",image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=previous; camera.targetTexture=null; target.Release(); Object.Destroy(target); Object.Destroy(image);
                canvas.renderMode=RenderMode.ScreenSpaceOverlay; camera.cullingMask=0;
            }
            loader.enabled=true;
            float deadline=Time.realtimeSinceStartup+25;
            float prior=0;
            while(!loader.IsDone && Time.realtimeSinceStartup<deadline)
            {
                Assert.That(loader.Progress,Is.GreaterThanOrEqualTo(prior)); prior=loader.Progress;
                yield return null;
            }
            Assert.That(loader.IsDone,Is.True);
            yield return null;
            Assert.That(view.IsVisible,Is.False);
            Assert.That(camera.enabled,Is.False);
            Assert.That(Object.FindObjectsByType<SystemBootView>(FindObjectsInactive.Include).Length,Is.EqualTo(1));
            Assert.That(Object.FindAnyObjectByType<GazeInputRouter>().Webcam.IsStreaming,Is.False);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator PresentationFeedbackIsBoundedAndResetSafe()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return VerifyPresentation();
            yield return new ExitPlayMode();
        }
        private static IEnumerator VerifyPresentation()
        {
            var router = Object.FindAnyObjectByType<GazeInputRouter>(); router.SetMode(PcGazeMode.MouseGaze); router.InputSuspended = true;
            var warm = Object.FindAnyObjectByType<ReadyPrewarmController>();
            float deadline = Time.realtimeSinceStartup + 25;
            while (!warm.IsComplete && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(warm.IsComplete, Is.True);
            Assert.That(warm.CompletedSteps, Is.EqualTo(warm.TotalSteps));
            var titleBridge = Object.FindObjectsByType<WorldTextPresentation>().Single(b=>b.name=="ReadyTitle");
            var legacyTitle = titleBridge.GetComponent<TextMesh>();
            var dataFont = legacyTitle.font;
            Assert.That(dataFont, Is.Not.Null);
            Assert.That(dataFont.dynamic, Is.False, "Hidden legacy meshes must not rasterize dynamic fonts on state changes");
            Assert.That(legacyTitle.fontSize, Is.Zero);
            Assert.That(legacyTitle.fontStyle, Is.EqualTo(FontStyle.Normal));
            string titleContent = legacyTitle.text;
            titleBridge.enabled = false;
            Assert.That(legacyTitle.font, Is.Not.Null, "Disabling the bridge restores the original fallback");
            Assert.That(legacyTitle.font, Is.Not.EqualTo(dataFont));
            titleBridge.enabled = true;
            Assert.That(legacyTitle.font, Is.EqualTo(dataFont));
            Assert.That(legacyTitle.text, Is.EqualTo(titleContent));
            var director = Object.FindAnyObjectByType<SfPresentationDirector>();
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(director.IsBootPresenting, Is.False, "No artificial loading hold after actual preparation");
            var camera = Camera.main; var position = camera.transform.localPosition; var rotation = camera.transform.localRotation;
            float fov = camera.fieldOfView;
            using var probe = new PcStabilityProbe(camera);
            yield return null; Save(probe,router,"ready");
            var flow = GameFlowManager.Instance;
            var audio = Object.FindAnyObjectByType<AudioManager>();
            flow.StartLaunch();
            var launch = Object.FindAnyObjectByType<LaunchSequenceController>();
            float launchAt = Time.realtimeSinceStartup;
            float captureTime = 0;
            bool countdownCaptured = false;
            while (flow.CurrentState != GameState.Playing && Time.realtimeSinceStartup-launchAt < 5)
            {
                if (!countdownCaptured && launch.CurrentStage == LaunchStage.Countdown2)
                { float captureAt=Time.realtimeSinceStartup; Save(probe,router,"launch-countdown"); captureTime+=Time.realtimeSinceStartup-captureAt; countdownCaptured = true; }
                yield return null;
            }
            Assert.That(flow.CurrentState, Is.EqualTo(GameState.Playing));
            StageProgressionController.Instance.ResetProgressionState(); // This presentation test intentionally exercises one legacy-sized combat beat.
            Assert.That(Time.realtimeSinceStartup-launchAt-captureTime, Is.LessThan(3.5f), "PNG encode/readback excluded; authored sequence is 2.65s");
            var spawner = Object.FindAnyObjectByType<MeteorSpawner>();
            var weapon = Object.FindAnyObjectByType<LaserWeapon>(); weapon.AutoFireOnLock = false;
            for (int i=0;i<15 && spawner.ActiveCount==0;i++) spawner.Tick(.5f);
            yield return new WaitForSecondsRealtime(.1f); Save(probe,router,"wave-1");
            for (int kill=0;kill<3;kill++)
            {
                for(int i=0;i<30 && spawner.ActiveCount==0;i++) spawner.Tick(.5f);
                var meteor = spawner.ActiveMeteors.First();
                var target = meteor.GetComponent<GazeLockOnTarget>();
                target.OnGazeEnter(default); target.OnGazeStay(default,2f);
                weapon.TickCooldown(1f); Assert.That(weapon.TryFire(target), Is.True);
                yield return null;
            }
            Assert.That(director.ConsecutiveDestructions, Is.EqualTo(3));
            Assert.That(director.PraiseCount, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(.08f); Save(probe,router,"combo");
            for (int i=0;i<100 && director.WaveNoticeCount<3;i++)
            {
                int prior = director.WaveNoticeCount;
                foreach(var meteor in spawner.ActiveMeteors.ToArray()) meteor.ReceiveDamage(10000);
                spawner.Tick(.5f);
                if(director.WaveNoticeCount>prior)
                { yield return new WaitForSecondsRealtime(.1f); Save(probe,router,"wave-"+director.WaveNoticeCount); }
            }
            Assert.That(director.WaveNoticeCount, Is.EqualTo(3));
            Object.FindAnyObjectByType<PlayerHealth>().ApplyDamage(75);
            yield return new WaitForSecondsRealtime(.12f); Save(probe,router,"shield-low");
            Assert.That(director.ConsecutiveDestructions, Is.Zero);
            var boss = Object.FindAnyObjectByType<BossClimaxController>(); boss.BeginClimax();
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(audio.PresentationGain, Is.EqualTo(.28f).Within(.01f)); Save(probe,router,"boss-warning");
            deadline=Time.realtimeSinceStartup+4;
            while(boss.ActiveBoss==null && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(boss.ActiveBoss, Is.Not.Null);
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(audio.PresentationGain, Is.EqualTo(1).Within(.01f)); Save(probe,router,"boss-active");
            boss.ActiveBoss.ReceiveDamage(10000);
            yield return new WaitForSecondsRealtime(.15f); Save(probe,router,"boss-explosion");
            yield return new WaitForSecondsRealtime(1.15f); Save(probe,router,"mission-complete");
            deadline=Time.realtimeSinceStartup+5;
            while(flow.CurrentState!=GameState.Result && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(flow.CurrentState, Is.EqualTo(GameState.Result));
            yield return new WaitForSecondsRealtime(.15f); Save(probe,router,"result-reveal");
            yield return new WaitForSecondsRealtime(1.2f); Save(probe,router,"result");
            int sourceCount = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Length;
            int canvasCount = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Length;
            var reset = Object.FindAnyObjectByType<OperationsResetController>();
            for(int i=0;i<30;i++) { flow.StartMainGame(); reset.ResetExperience(); director.enabled=false; director.enabled=true; }
            yield return new WaitForSecondsRealtime(.7f); Save(probe,router,"reset-ready");
            Assert.That(flow.CurrentState, Is.EqualTo(GameState.Boot));
            Assert.That(director.PraiseCount+director.WaveNoticeCount, Is.Zero);
            Assert.That(audio.PresentationGain, Is.EqualTo(1).Within(.01f));
            Assert.That(Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Length, Is.EqualTo(sourceCount));
            Assert.That(Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Length, Is.EqualTo(canvasCount));
            Assert.That(PcStabilityProbe.InspectSubscriptions().duplicateCallbacks, Is.Zero);
            Assert.That(camera.transform.localPosition, Is.EqualTo(position));
            Assert.That(camera.transform.localRotation, Is.EqualTo(rotation));
            Assert.That(camera.fieldOfView, Is.EqualTo(fov));
            Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(router.Webcam.IsStreaming, Is.False);
        }
        private static void Save(PcStabilityProbe probe, GazeInputRouter router, string label)
        {
            Canvas.ForceUpdateCanvases(); Camera.main.Render();
            string path="Docs/GameFeelQA/After/"+label; Directory.CreateDirectory(path);
            probe.SaveGameOnlyProof(path,router);
        }
    }
}
