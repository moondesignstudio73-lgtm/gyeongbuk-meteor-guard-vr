using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Profiling;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.Tests
{
    // Opt-in CPU Profiler capture. No webcam, screenshots, user input values or face data are saved.
    public sealed class StartupHitchProfilerTests
    {
        internal static string RunName => Environment.GetCommandLineArgs()
            .FirstOrDefault(a => a.StartsWith("--hitch-profile=") || a.StartsWith("--hitch-reexport="))?.Split('=')[1];
        internal static string DirectoryPath => Path.GetFullPath("Docs/StartupHitchQA/" + RunName);

        [UnityTest, Timeout(180000)]
        public IEnumerator CaptureStartupAndTransitions()
        {
            if (RunName == null) Assert.Ignore("Opt-in: --hitch-profile=before-1 or after-1");
            Assert.That(RunName.IndexOfAny(Path.GetInvalidFileNameChars()), Is.EqualTo(-1));
            Directory.CreateDirectory(DirectoryPath);
            if (Environment.GetCommandLineArgs().Any(a => a.StartsWith("--hitch-reexport=")))
            {
                foreach (string segment in new[] { "startup", "transitions" })
                {
                    ProfilerDriver.enabled = false;
                    Assert.That(ProfilerDriver.LoadProfile(Path.Combine(DirectoryPath, segment + ".data"), false), Is.True);
                    Export(segment);
                }
                yield break;
            }
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            ConfigureProfiler();
            yield return new EnterPlayMode();
            yield return Capture();
            yield return new ExitPlayMode();
        }

        private static void ConfigureProfiler()
        {
            SessionState.SetBool("MD.Hitch.WasProfiling", ProfilerDriver.enabled);
            SessionState.SetBool("MD.Hitch.ProfileEditor", ProfilerDriver.profileEditor);
            SessionState.SetBool("MD.Hitch.Cpu", ProfilerDriver.IsAreaEnabled(ProfilerArea.CPU));
            SessionState.SetBool("MD.Hitch.Memory", ProfilerDriver.IsAreaEnabled(ProfilerArea.Memory));
            Assert.That(ProfilerDriver.deepProfiling, Is.False, "Disable Deep Profile for comparable captures");
            // Reflection keeps this QA helper compatible with Unity's internal settings namespace.
            var settings = typeof(ProfilerDriver).Assembly.GetTypes().FirstOrDefault(t => t.Name == "ProfilerUserSettings");
            var history = settings?.GetProperty("frameCount", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (history != null)
            {
                SessionState.SetInt("MD.Hitch.History", (int)history.GetValue(null));
                history.SetValue(null, 2000);
            }
            ProfilerDriver.profileEditor = false;
            ProfilerDriver.SetAreaEnabled(ProfilerArea.CPU, true);
            ProfilerDriver.SetAreaEnabled(ProfilerArea.Memory, true);
            ProfilerDriver.ClearAllFrames();
            ProfilerDriver.enabled = true;
        }

        private static IEnumerator Capture()
        {
            yield return GameplaySceneBootstrapTests.WaitForGameplayScene();
            var driver = Object.FindAnyObjectByType<StartupHitchPilot>();
            Assert.That(driver, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + 130f;
            while (!driver.Completed && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(driver.Completed, Is.True, "Natural startup/transition pilot timed out");
            // Flush the final player frame into the Profiler before stopping.
            yield return new WaitForSecondsRealtime(.1f);
            ProfilerDriver.enabled = false;
            Assert.That(ProfilerDriver.SaveProfile(Path.Combine(DirectoryPath, "transitions.data")), Is.True);
            Export("transitions");
            Assert.That(ProfilerDriver.LoadProfile(Path.Combine(DirectoryPath, "startup.data"), false), Is.True);
            Export("startup");
            Assert.That(driver.WebcamOff, Is.True);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (RunName == null) yield break;
            ProfilerDriver.enabled = false;
            if (Application.isPlaying) yield return new ExitPlayMode();
            ProfilerDriver.profileEditor = SessionState.GetBool("MD.Hitch.ProfileEditor", false);
            ProfilerDriver.SetAreaEnabled(ProfilerArea.CPU, SessionState.GetBool("MD.Hitch.Cpu", true));
            ProfilerDriver.SetAreaEnabled(ProfilerArea.Memory, SessionState.GetBool("MD.Hitch.Memory", true));
            var settings = typeof(ProfilerDriver).Assembly.GetTypes().FirstOrDefault(t => t.Name == "ProfilerUserSettings");
            settings?.GetProperty("frameCount", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?.SetValue(null, SessionState.GetInt("MD.Hitch.History", 300));
            ProfilerDriver.enabled = SessionState.GetBool("MD.Hitch.WasProfiling", false);
        }

        [Serializable] private sealed class Frame
        {
            public int index;
            public string state;
            public float mainThreadFrameMs, playerLoopMs, gcCollectMs, instantiateMs, destroyMs,
                shaderMs, audioMs, textureMs, particleMs, canvasMs, tmpMs, assetMs, lifecycleMs;
            public long gcAllocBytes;
        }
        [Serializable] private sealed class CaptureData
        {
            public string environment = "Unity Editor CPU Profiler, no Deep Profile; 1280x720 game-only render; 60 FPS cap; mouse; webcam OFF";
            public string graphics, unity, run, segment;
            public List<Frame> frames = new List<Frame>();
        }

        private static void Export(string segment)
        {
            var data = new CaptureData { graphics = SystemInfo.graphicsDeviceType.ToString(), unity = Application.unityVersion, run = RunName, segment = segment };
            var names = new Dictionary<int, string>();
            using var detail = new StreamWriter(Path.Combine(DirectoryPath, segment + "-markers.csv"));
            detail.WriteLine("frame,thread,state,marker,inclusiveMs,calls");
            for (int frameIndex = ProfilerDriver.firstFrameIndex; frameIndex <= ProfilerDriver.lastFrameIndex; frameIndex++)
            {
                var frame = new Frame { index = frameIndex, state = "Unlabelled/scene-load" };
                for (int thread = 0; ; thread++)
                {
                    using var raw = ProfilerDriver.GetRawFrameDataView(frameIndex, thread);
                    if (!raw.valid) break;
                    if (raw.threadName != "Main Thread" && raw.threadName != "Render Thread") continue;
                    bool main = raw.threadName == "Main Thread";
                    if (main) frame.mainThreadFrameMs = raw.frameTimeMs;
                    for (int i = 0; i < raw.sampleCount; i++)
                    {
                        int id = raw.GetSampleMarkerId(i);
                        if (!names.TryGetValue(id, out string name)) names[id] = name = raw.GetSampleName(i);
                        if (main && name == "PlayerLoop") frame.playerLoopMs += raw.GetSampleTimeMs(i);
                        if (main && name.StartsWith("MD.Phase.")) frame.state = name.Substring(9);
                    }
                    var sums = new Dictionary<string, (float ms, int calls)>();
                    var categoryEnds = new int[11]; Array.Fill(categoryEnds, -1);
                    for (int i = 0; i < raw.sampleCount; i++)
                    {
                        string name = names[raw.GetSampleMarkerId(i)];
                        float ms = raw.GetSampleTimeMs(i);
                        if (main && name == "GC.Alloc" && raw.GetSampleMetadataCount(i) > 0)
                        {
                            long bytes = raw.GetSampleMetadataAsLong(i, 0);
                            frame.gcAllocBytes += bytes;
                        }
                        int category = Category(name);
                        if (category >= 0 && (main || category == 3))
                        {
                            if (i > categoryEnds[category])
                            {
                                Add(frame, category, ms);
                                categoryEnds[category] = i + raw.GetSampleChildrenCountRecursive(i);
                            }
                        }
                        if ((category >= 0 || ms >= .3f || name == "PlayerLoop" || name.Contains("WaitFor") || name.StartsWith("MD.")) && name != "GC.Alloc")
                        {
                            sums.TryGetValue(name, out var sum); sums[name] = (sum.ms + ms, sum.calls + 1);
                        }
                    }
                    foreach (var item in sums)
                        detail.WriteLine($"{frameIndex},\"{raw.threadName}\",\"{frame.state}\",\"{item.Key.Replace("\"", "\"\"")}\",{item.Value.ms.ToString("F6", CultureInfo.InvariantCulture)},{item.Value.calls}");
                }
                data.frames.Add(frame);
            }
            File.WriteAllText(Path.Combine(DirectoryPath, segment + ".json"), JsonUtility.ToJson(data, true));
            Assert.That(data.frames.Count, Is.GreaterThan(5), "CPU Profiler capture must contain actual frames");
            if (segment == "transitions")
                foreach (string state in new[] { "Intro", "EyeCalibration", "Practice", "Launch", "Playing" })
                    Assert.That(data.frames.Any(f => f.state == state), Is.True, "Missing Profiler phase: " + state);
        }

        private static int Category(string name)
        {
            if (name.Contains("GC.Collect") || name.Contains("GarbageCollect")) return 0;
            if (name.Contains("Instantiate") || name.Contains("CloneObject")) return 1;
            if (name.Contains("Destroy")) return 2;
            if (name.Contains("Shader") || name.Contains("GPUProgram") || name.Contains("GraphicsPipeline")) return 3;
            if (name.Contains("AudioClip") || name.Contains("AudioSource") || name.Contains("FMOD") || name.Contains("AudioManager") || name.Contains("Audio.Load")) return 4;
            if (name.Contains("Texture") || name.Contains("RenderBuffer")) return 5;
            if (name.Contains("Particle") || name.Contains("CombatVfx")) return 6;
            if (name.Contains("Canvas") || name.Contains("UI.Layout") || name.Contains("UI.Rebuild")) return 7;
            if (name.Contains("TMP") || name.Contains("TextMeshPro") || name.Contains("Font") || name.Contains("TextGenerator")) return 8;
            if (name.Contains("LoadAsset") || name.Contains("Resources.Load") || name.Contains("ReadObject") || name.Contains("LoadScene") || name.Contains("Loading.")) return 9;
            if (name.Contains("Awake") || name.Contains("OnEnable") || name.Contains(".Start()")) return 10;
            return -1;
        }
        private static void Add(Frame f, int category, float ms)
        {
            switch (category)
            {
                case 0: f.gcCollectMs += ms; break; case 1: f.instantiateMs += ms; break;
                case 2: f.destroyMs += ms; break; case 3: f.shaderMs += ms; break;
                case 4: f.audioMs += ms; break; case 5: f.textureMs += ms; break;
                case 6: f.particleMs += ms; break; case 7: f.canvasMs += ms; break;
                case 8: f.tmpMs += ms; break; case 9: f.assetMs += ms; break; case 10: f.lifecycleMs += ms; break;
            }
        }
    }

    [DefaultExecutionOrder(-15000)]
    public sealed class StartupHitchPilot : MonoBehaviour
    {
        private static readonly string[] phaseNames = Enum.GetNames(typeof(GameState)).Select(s => "MD.Phase." + s).ToArray();
        private Mouse mouse;
        private GazeInputRouter router;
        private ReadyScreenController ready;
        private PcStabilityProbe render;
        private GazeLockOnTarget target;
        private float readyAt, startAt, playingAt, nextTargetScan;
        private bool recordingTransitions, started;
        private InputSettings.EditorInputBehaviorInPlayMode previousInput;
        private InputSettings.BackgroundBehavior previousBackground;
        public bool Completed { get; private set; }
        public bool WebcamOff => router != null && !router.Webcam.IsStreaming;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (!Environment.GetCommandLineArgs().Any(a => a.StartsWith("--hitch-profile="))) return;
            var root = new GameObject("QA_StartupHitchPilot");
            DontDestroyOnLoad(root);
            root.AddComponent<StartupHitchPilot>();
        }
        private void Awake()
        {
            previousInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            mouse = InputSystem.AddDevice<Mouse>();
            UnityEngine.Random.InitState(240828);
        }
        private void Update()
        {
            if (Completed) return;
            Application.targetFrameRate = 60;
            if (router == null)
            {
                Profiler.BeginSample("MD.QA.SetupRenderer");
                router = Object.FindAnyObjectByType<GazeInputRouter>();
                if (router != null && Camera.main != null)
                {
                    router.SetMode(PcGazeMode.MouseGaze); router.InputSuspended = true;
                    ready = Object.FindAnyObjectByType<ReadyScreenController>();
                    render = new PcStabilityProbe(Camera.main);
                    readyAt = Time.realtimeSinceStartup;
                }
                Profiler.EndSample();
                return;
            }
            if (!recordingTransitions && Time.realtimeSinceStartup - readyAt >= 5f)
            {
                ProfilerDriver.enabled = false;
                Assert.That(ProfilerDriver.SaveProfile(Path.Combine(StartupHitchProfilerTests.DirectoryPath, "startup.data")), Is.True);
                ProfilerDriver.ClearAllFrames(); ProfilerDriver.enabled = true;
                recordingTransitions = true; startAt = Time.realtimeSinceStartup + .75f;
            }
            if (recordingTransitions && !started && Time.realtimeSinceStartup >= startAt)
            { router.InputSuspended = false; started = true; ready.StartExperience(); }
            var state = GameFlowManager.Instance.CurrentState;
            Profiler.BeginSample("MD.QA.MousePilot");
            if (started && (state == GameState.Tutorial || state == GameState.Practice || state == GameState.Playing))
            {
                if ((target == null || !target.IsAvailable) && Time.realtimeSinceStartup >= nextTargetScan)
                {
                    nextTargetScan = Time.realtimeSinceStartup + .1f;
                    target = null; float distance = float.MaxValue;
                    foreach (var candidate in Object.FindObjectsByType<GazeLockOnTarget>())
                    {
                        float d = Vector3.SqrMagnitude(candidate.transform.position - Camera.main.transform.position);
                        if (candidate.IsAvailable && d < distance) { target = candidate; distance = d; }
                    }
                }
            }
            else target = null;
            Vector2 position = target != null ? (Vector2)Camera.main.WorldToScreenPoint(target.transform.position) : new Vector2(Screen.width * .5f, Screen.height * .45f);
            UnifiedLookTests.QueueAim(mouse, Object.FindAnyObjectByType<WindowsVRSimulation>(), target != null ? target.transform : null, position);
            Profiler.EndSample();
            if (state == GameState.Playing)
            {
                if (playingAt == 0f) playingAt = Time.realtimeSinceStartup;
                if (Time.realtimeSinceStartup - playingAt >= 2f) Completed = true;
            }
        }
        private void LateUpdate()
        {
            var flow = GameFlowManager.Instance;
            if (flow != null) { Profiler.BeginSample(phaseNames[(int)flow.CurrentState]); Profiler.EndSample(); }
        }
        private void OnDestroy()
        {
            render?.Dispose();
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            InputSystem.settings.editorInputBehaviorInPlayMode = previousInput;
            InputSystem.settings.backgroundBehavior = previousBackground;
        }
    }
}
