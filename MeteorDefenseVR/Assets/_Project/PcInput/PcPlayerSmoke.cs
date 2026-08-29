using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.PcInput
{
    // Opt-in release-player QA only. Never runs on an ordinary double-click launch.
    public sealed class PcPlayerSmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartIfRequested()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (!args.Contains("--pc-smoke") && !args.Contains("--pc-camera-check") && !args.Contains("--pc-soak") && !args.Contains("--pc-webcam-stability")) return;
            if (Object.FindAnyObjectByType<PcPlayerSmoke>() != null) return;
            var root = new GameObject("OptInPcPlayerQA");
            DontDestroyOnLoad(root);
            root.AddComponent<PcPlayerSmoke>();
        }
        private IEnumerator Start()
        {
            GazeInputRouter router = null;
            float began = Time.realtimeSinceStartup;
            while (router == null && Time.realtimeSinceStartup - began < 25f)
            { router = Object.FindAnyObjectByType<GazeInputRouter>(); yield return null; }
            if (router == null) { Debug.LogError("[PCPlayerQA] Input router missing"); Application.Quit(2); yield break; }
            if (Environment.GetCommandLineArgs().Contains("--pc-webcam-stability"))
            {
                yield return PcWebcamStabilityRunner.Run(router);
                yield break;
            }
            if (Environment.GetCommandLineArgs().Contains("--pc-soak"))
            {
                int rounds=20;
                string option=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("--pc-soak-rounds="));
                if(option!=null&&int.TryParse(option.Substring("--pc-soak-rounds=".Length),out int requested))rounds=Mathf.Clamp(requested,1,100);
                yield return PcSoakRunner.Run(router, rounds);
                yield break;
            }
            if (Environment.GetCommandLineArgs().Contains("--pc-camera-check"))
            {
                router.Webcam.ShowWebcamPreview = false;
                yield return new WaitForSecondsRealtime(12f);
                Debug.Log($"[PCCameraQA] Mode={router.ActiveMode}; Streaming={router.Webcam.IsStreaming}; Tracking={router.Webcam.HasFreshTracking}; Status={router.Webcam.Status}. No images saved.");
                router.Webcam.StopCapture();
                Application.Quit();
                yield break;
            }
            router.SetMode(PcGazeMode.MouseGaze);
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            router.Webcam.ShowWebcamPreview = false;
            yield return null;
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "QA"));
            Directory.CreateDirectory(output);
            var flow = GameFlowManager.Instance;
            var visited = new HashSet<GameState> { flow.CurrentState };
            var captured = new HashSet<GameState>();
            flow.OnStateChanged += state => { visited.Add(state); Debug.Log("[PCPlayerQA] State=" + state); };
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "pc-ready.png"));
            yield return new WaitForSecondsRealtime(.5f);
            Mouse simulated = InputSystem.AddDevice<Mouse>();
            Object.FindAnyObjectByType<ReadyScreenController>().StartExperience();
            began = Time.realtimeSinceStartup;
            GazeLockOnTarget target = null;
            while (Time.realtimeSinceStartup - began < 160f)
            {
                if (target == null || !target.IsAvailable)
                    target = Object.FindObjectsByType<GazeLockOnTarget>().Where(t => t.IsAvailable)
                        .OrderBy(t => Vector3.Distance(Camera.main.transform.position, t.transform.position)).FirstOrDefault();
                Vector2 position = target != null ? (Vector2)Camera.main.WorldToScreenPoint(target.transform.position) : new Vector2(Screen.width * .5f, Screen.height * .45f);
                InputSystem.QueueStateEvent(simulated, new MouseState { position = position });
                GameState state = flow.CurrentState;
                if ((state == GameState.Tutorial || state == GameState.Playing || state == GameState.Result) && !captured.Contains(state))
                {
                    captured.Add(state);
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "pc-" + state + ".png"));
                }
                if (visited.Contains(GameState.Result) && state == GameState.Boot) break;
                yield return null;
            }
            InputSystem.RemoveDevice(simulated);
            var required = new[] { GameState.Intro, GameState.MissionBriefing, GameState.EyeCalibration, GameState.Tutorial, GameState.Practice, GameState.Launch, GameState.Countdown, GameState.Playing, GameState.BossMeteor, GameState.MissionComplete, GameState.Result, GameState.Reset, GameState.Boot };
            bool passed = required.All(visited.Contains) && !router.Webcam.IsStreaming;
            string report = $"PC release mouse full loop: {(passed ? "PASS" : "FAIL")}\nStates: {string.Join(", ", visited)}\nCamera recording: disabled\n";
            File.WriteAllText(Path.Combine(output, "pc-player-smoke.txt"), report);
            Debug.Log("[PCPlayerQA] " + report);
            Application.Quit(passed ? 0 : 2);
        }
    }
}
