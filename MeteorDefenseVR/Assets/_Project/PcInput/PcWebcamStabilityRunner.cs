using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MeteorDefenseVR.GameFlow;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.PcInput
{
    // Explicit command-line QA only; never saves camera pixels, facial features or calibration values.
    public static class PcWebcamStabilityRunner
    {
        [Serializable] public sealed class Cycle
        {
            public int number;
            public bool opened, receivedFrame, released, fallbackAfterInjectedFailure;
            public float startupSeconds;
            public long allocatedBytes;
            public string outcome;
        }
        [Serializable] public sealed class Report
        {
            public string startedUtc, completedUtc;
            public bool softwareRestartPassed, physicalCameraAvailable, cancellationPassed;
            public string scope = "Actual capture start/stop/reopen, plus injected provider failure. No OS permission changes or physical USB unplug. No images/landmarks stored.";
            public List<Cycle> cycles = new List<Cycle>();
        }
        public static IEnumerator Run(GazeInputRouter router)
        {
            var report = new Report { startedUtc = DateTime.UtcNow.ToString("O") };
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "QA"));
            Directory.CreateDirectory(directory);
            var webcam = router.Webcam;
            router.SetMode(PcGazeMode.MouseGaze);
            webcam.ShowWebcamPreview = webcam.ShowFaceDebug = false;
            Application.runInBackground = true;
            var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            try
            {
                for (int i = 1; i <= 3; i++)
                {
                    var cycle = new Cycle { number = i };
                    router.SetMode(PcGazeMode.WebcamGaze);
                    float began = Time.realtimeSinceStartup;
                    while (webcam.IsStarting && Time.realtimeSinceStartup - began < 14f) yield return null;
                    cycle.startupSeconds = Time.realtimeSinceStartup - began;
                    cycle.opened = webcam.IsStreaming;
                    if (cycle.opened)
                    {
                        report.physicalCameraAvailable = true;
                        yield return new WaitForSecondsRealtime(3f);
                        cycle.receivedFrame = webcam.PreviewTexture != null;
                        cycle.outcome = "Actual camera initialized; frame presence only (no contents recorded)";
                    }
                    else cycle.outcome = webcam.Failed ? webcam.Status : "Startup did not complete";
                    webcam.Fail("Injected permission/startup/device failure for QA");
                    yield return null;
                    cycle.fallbackAfterInjectedFailure = router.ActiveMode == PcGazeMode.MouseGaze;
                    router.SetMode(PcGazeMode.MouseGaze);
                    yield return new WaitForSecondsRealtime(.3f);
                    cycle.released = !webcam.IsStarting && !webcam.IsStreaming && webcam.PreviewTexture == null;
                    cycle.allocatedBytes = Profiler.GetTotalAllocatedMemoryLong();
                    report.cycles.Add(cycle);
                    Debug.Log($"[WebcamStability] Cycle={i} opened={cycle.opened} frames={cycle.receivedFrame} released={cycle.released} fallback={cycle.fallbackAfterInjectedFailure}");
                }
                for (int i = 0; i < 10; i++) { router.SetMode(PcGazeMode.WebcamGaze); router.SetMode(PcGazeMode.MouseGaze); }
                yield return new WaitForSecondsRealtime(.5f);
                report.cancellationPassed = !webcam.IsStarting && !webcam.IsStreaming && webcam.PreviewTexture == null;
                Object.FindAnyObjectByType<PcTestControls>()?.ResetExperience();
            }
            finally
            {
                router.SetMode(PcGazeMode.MouseGaze); webcam.StopCapture();
                UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
            }
            report.completedUtc = DateTime.UtcNow.ToString("O");
            report.softwareRestartPassed = report.cycles.Count == 3 && report.cycles.TrueForAll(c => c.opened && c.receivedFrame && c.released && c.fallbackAfterInjectedFailure) && report.cancellationPassed;
            File.WriteAllText(Path.Combine(directory, "webcam-stability.json"), JsonUtility.ToJson(report, true));
            Debug.Log("[WebcamStability] " + (report.softwareRestartPassed ? "PASS" : "UNVERIFIED/FAIL") + "; no camera images or face data saved");
            Application.Quit(report.softwareRestartPassed ? 0 : 2);
        }
    }
}
