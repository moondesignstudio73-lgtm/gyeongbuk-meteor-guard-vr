using System.IO;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Visual;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MeteorDefenseVR.Editor
{
    // Offscreen UI verification: a hidden Windows player has no usable presented backbuffer.
    // Never reads a desktop or webcam image; only renders this game's camera and its PC canvas.
    [InitializeOnLoad]
    public static class PcVisualQaCapture
    {
        private const string Key = "MeteorDefense.PcVisualQa";
        private static double nextCapture;
        private static readonly GameState[] States = { GameState.Boot, GameState.Intro, GameState.MissionBriefing, GameState.EyeCalibration,
            GameState.Tutorial, GameState.Practice, GameState.Launch, GameState.Playing, GameState.BossMeteor, GameState.MissionComplete, GameState.Result,
            GameState.Boot, GameState.Boot };
        static PcVisualQaCapture()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    if (Mouse.current == null) InputSystem.AddDevice<Mouse>();
                    Object.FindAnyObjectByType<GazeInputRouter>()?.SetMode(PcGazeMode.MouseGaze);
                    nextCapture = EditorApplication.timeSinceStartup + 2;
                    EditorApplication.update += CaptureWhenReady;
                }
                else if (state == PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.SetBool(Key, false);
                    if (Application.isBatchMode) EditorApplication.Exit(0);
                }
            };
        }
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".Stage", 0);
            EditorApplication.isPlaying = true;
        }
        public static void RunGraphics()
        {
            SessionState.SetBool(Key + ".Graphics", true);
            Run();
        }
        public static void RebuildGraphics()
        {
            ProjectSceneSetup.EnsureProjectScenes();
            RunGraphics();
        }
        private static void CaptureWhenReady()
        {
            if (!Application.isPlaying || EditorApplication.timeSinceStartup < nextCapture) return;
            try
            {
                var router = Object.FindAnyObjectByType<GazeInputRouter>();
                router.SetMode(PcGazeMode.MouseGaze);
                router.Webcam.StopCapture();
                int stage = SessionState.GetInt(Key + ".Stage", 0);
                string label = stage == 11 ? "operator" : stage == 12 ? "development" : States[stage].ToString();
                SaveFrame(label + "-1280x720.png", 1280, 720);
                if (SessionState.GetBool(Key + ".Graphics", false) && States[stage] == GameState.Playing) CaptureCombatFixture();
                if (States[stage] == GameState.Boot || States[stage] == GameState.MissionComplete || States[stage] == GameState.Result)
                {
                    SaveFrame(label + "-1024x768.png", 1024, 768);
                    SaveFrame(label + "-2560x1080.png", 2560, 1080);
                    SaveFrame(label + "-720x1280.png", 720, 1280);
                }
                if (stage < States.Length - 1)
                {
                    stage++;
                    SessionState.SetInt(Key + ".Stage", stage);
                    Object.FindAnyObjectByType<OperationsResetController>().ResetExperience();
                    var config = Object.FindAnyObjectByType<ExperienceConfiguration>();
                    config.Settings.experienceMode = stage == 11 ? ExperienceMode.Operator : stage == 12 ? ExperienceMode.Development : ExperienceMode.PublicExperience;
                    if (States[stage] == GameState.Result)
                    {
                        // A labelled visual fixture covers the largest score/row widths; not a playthrough.
                        var stats = Object.FindAnyObjectByType<GameSessionStats>();
                        stats.ResetSession(22); stats.SetScore(3300);
                        for (int i = 0; i < 22; i++) stats.RecordMeteorDestroyed();
                        for (int i = 0; i < 26; i++) { stats.RecordShotAndLock(); stats.RecordSuccessfulHit(); }
                    }
                    if (States[stage] != GameState.Boot) GameFlowManager.Instance.ChangeState(States[stage]);
                    if (States[stage] == GameState.BossMeteor) Object.FindAnyObjectByType<MeteorDefenseVR.Meteor.BossClimaxController>().BeginClimax();
                    nextCapture = EditorApplication.timeSinceStartup + (States[stage] == GameState.MissionComplete ? 1.6 : States[stage] == GameState.Playing ? 4.0 : States[stage] == GameState.BossMeteor ? 3.2 : 1.1);
                }
                else
                {
                    EditorApplication.update -= CaptureWhenReady;
                    EditorApplication.isPlaying = false;
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetBool(Key, false);
                EditorApplication.update -= CaptureWhenReady;
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
        private static void SaveFrame(string filename, int width, int height)
        {
            Camera camera = Camera.main;
            Canvas canvas = Object.FindAnyObjectByType<PcTestOverlay>().GetComponentInChildren<Canvas>();
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previousTarget = camera.targetTexture, previousActive = RenderTexture.active;
            RenderMode previousMode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            float previousAspect = camera.aspect;
            try
            {
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                camera.GetComponent<WorldUiSafeArea>()?.Apply();
                foreach (var presentation in Object.FindObjectsByType<WorldTextPresentation>()) presentation.Refresh();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = .5f;
                Canvas.ForceUpdateCanvases();
                foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>()) text.ForceMeshUpdate();
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                string directory = SessionState.GetBool(Key + ".Graphics", false) ? "Docs/GraphicsUpgrade/After" : "Docs/FinalQA/Visual";
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, filename), image.EncodeToPNG());
                var bounds = new System.Text.StringBuilder();
                foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>())
                {
                    if (!text.isActiveAndEnabled || string.IsNullOrEmpty(text.text)) continue;
                    bounds.AppendLine(text.transform.parent.name + ": preferred=" + text.preferredWidth.ToString("F2") + "x" + text.preferredHeight.ToString("F2") + " rect=" + text.rectTransform.rect.size + " text=" + text.text.Replace("\n", " | "));
                }
                bounds.AppendLine("Draw calls="+UnityStats.drawCalls+" triangles="+UnityStats.triangles);
                File.WriteAllText(Path.Combine(directory, filename + ".txt"), bounds.ToString());
                Debug.Log("[PCVisualQA] Saved game-only render: " + filename);
            }
            finally
            {
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                camera.targetTexture = previousTarget;
                camera.aspect = previousAspect;
                camera.GetComponent<WorldUiSafeArea>()?.Apply();
                RenderTexture.active = previousActive;
                Object.Destroy(image);
                Object.Destroy(target);
            }
        }
        private static void CaptureCombatFixture()
        {
            // Explicitly labelled composition fixtures, separate from natural play captures.
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Meteor/Prefabs/Meteor_Normal.prefab");
            var fixture=Object.Instantiate(prefab,new Vector3(.9f,.1f,6.8f),Quaternion.identity);
            try
            {
                var meteor=fixture.GetComponent<MeteorDefenseVR.Meteor.MeteorController>();
                meteor.Configure(MeteorDefenseVR.Meteor.MeteorType.Normal,3,0,0,0,1.8f);meteor.Spawn();
                var target=fixture.GetComponent<MeteorDefenseVR.Combat.GazeLockOnTarget>();
                target.OnGazeEnter(default);target.Advance(target.LockOnDuration*.62f);
                foreach(var p in fixture.GetComponentsInChildren<WorldTextPresentation>(true))p.Refresh();
                SaveFrame("fixture-tracking-1920x1080.png",1920,1080);
                // Drive the presentation state directly, never count this as a successful gameplay shot.
                var reticle=fixture.GetComponentInChildren<MeteorDefenseVR.Combat.LockOnReticleView>();
                typeof(MeteorDefenseVR.Combat.LockOnReticleView).GetMethod("Refresh",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)
                    .Invoke(reticle,new object[]{1f,MeteorDefenseVR.Combat.LockOnState.Locked});
                var beam=Object.FindAnyObjectByType<MeteorDefenseVR.Combat.LaserBeamView>();
                beam.Play(new Vector3(.65f,-.65f,1.2f),fixture.transform.position);
                Object.FindAnyObjectByType<MeteorDefenseVR.VFX.CombatVfxController>().PlayHitFeedback(null,fixture.transform.position+new Vector3(.5f,-.15f,-.7f));
                foreach(var p in Object.FindObjectsByType<ParticleSystem>())p.Simulate(.06f,true,false);
                SaveFrame("fixture-impact-1920x1080.png",1920,1080);beam.Stop();
                var vfx=Object.FindAnyObjectByType<PremiumCombatVfx>();
                typeof(PremiumCombatVfx).GetMethod("Explode",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(vfx,new object[]{fixture.transform.position});
                for(int i=0;i<12;i++)vfx.SendMessage("Update");
                fixture.SetActive(false);
                foreach(var p in Object.FindObjectsByType<ParticleSystem>())p.Simulate(.28f,true,false);
                SaveFrame("fixture-explosion-1920x1080.png",1920,1080);
            }
            finally{Object.DestroyImmediate(fixture);}
        }
    }
}
