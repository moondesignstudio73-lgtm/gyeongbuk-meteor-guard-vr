using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.PcInput
{
    public static class PausePriorityProof
    {
        public static IEnumerator Run(GazeInputRouter router, string output, Action<bool> complete)
        {
            bool passed = true;
            var notes = new List<string> { "REVIEW-20261021-06; synthetic keyboard/UI events, not human input or HMD." };
            var flow = GameFlowManager.Instance;
            var reset = Object.FindAnyObjectByType<OperationsResetController>();
            var overlay = Object.FindAnyObjectByType<PcTestOverlay>();
            var gate = overlay.GetComponent<PausePresentationGate>();
            var presentation = Object.FindAnyObjectByType<SfPresentationDirector>();
            var hud = Object.FindAnyObjectByType<MissionHudController>(FindObjectsInactive.Include);
            var look = router.Simulation;
            var camera = Camera.main;
            var raycaster = Object.FindAnyObjectByType<MeteorDefenseVR.EyeTracking.GazeRaycaster>();
            bool rayEnabled = raycaster.enabled; raycaster.enabled = false;
            var oldInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            var oldBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            Button Button(string name) => overlay.GetComponentsInChildren<Button>(true).Single(b => b.name == name);
            void Check(bool condition, string message) { passed &= condition; notes.Add(message + "=" + condition); }
            IEnumerator Escape()
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return new WaitForSecondsRealtime(.12f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSecondsRealtime(.12f);
            }
            try
            {
                foreach (var size in new[] { new Vector2Int(1920,1080), new Vector2Int(1280,720), new Vector2Int(1024,768), new Vector2Int(800,600), new Vector2Int(2560,1080) })
                {
                    using var proof = new PcStabilityProbe(camera, size.x, size.y);
                    foreach (string scenario in new[] { "stage-start", "stage-middle", "stage-end", "tutorial", "practice", "combat", "boss" })
                    {
                        string prefix = size + "/" + scenario;
                        void Capture(string phase)
                        {
                            Canvas.ForceUpdateCanvases(); gate.Apply(); PcStabilityRenderDriver.RenderForProof(camera);
                            string folder = Path.Combine(output, $"{size.x}x{size.y}", scenario, phase);
                            Directory.CreateDirectory(folder); proof.SaveGameOnlyProof(folder, router);
                        }
                        reset.ResetExperience(); yield return null;
                        if (scenario == "tutorial") flow.StartTutorial();
                        else if (scenario == "practice") flow.StartPractice();
                        else if (scenario == "boss") flow.StartBoss();
                        else flow.StartMainGame();
                        float wait = scenario == "stage-start" ? .1f : scenario == "stage-middle" ? .75f : scenario == "stage-end" ? 1.45f : scenario == "combat" ? 1.8f : .3f;
                        yield return new WaitForSeconds(wait);
                        Capture("before-pause");
                        look.Pause();
                        float remaining = presentation.StageTimeRemaining, feedback = hud.FeedbackRemaining;
                        var state = flow.CurrentState;
                        yield return new WaitForSecondsRealtime(.35f);
                        Capture("paused");
                        Check(gate.IsSuspended && look.IsPaused && state == flow.CurrentState, prefix + " pause retains game state");
                        Check(Mathf.Abs(presentation.StageTimeRemaining - remaining) < .001f && Mathf.Abs(hud.FeedbackRemaining - feedback) < .001f, prefix + " freezes remaining notice time");
                        foreach (var text in Object.FindObjectsByType<TMPro.TextMeshPro>(FindObjectsInactive.Exclude))
                            if (text.TryGetComponent<Renderer>(out var renderer)) Check(renderer.forceRenderingOff, prefix + " hidden world text " + text.name);
                        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
                            if (!canvas.transform.IsChildOf(overlay.transform)) Check(canvas.GetComponents<CanvasGroup>().Any(g => g.alpha == 0), prefix + " hidden world canvas " + canvas.name);
                        foreach(var target in Object.FindObjectsByType<MeteorDefenseVR.Combat.GazeLockOnTarget>(FindObjectsInactive.Exclude))
                            foreach(var renderer in target.GetComponentsInChildren<Renderer>()) Check(renderer.forceRenderingOff, prefix+" hidden target marker/body "+renderer.name);
                        // Whole control rectangles, including button backgrounds, must be disjoint.
                        var controls = new[] { (RectTransform)Button("ResumeSimulation").transform, overlay.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Single(t => t.name == "SimulationHelp").rectTransform, (RectTransform)Button("LookSettings").transform };
                        Rect Bounds(RectTransform rect)
                        {
                            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                            Vector2 a = camera.WorldToScreenPoint(corners[0]), b = camera.WorldToScreenPoint(corners[2]); return Rect.MinMaxRect(a.x,a.y,b.x,b.y);
                        }
                        for (int i=0;i<controls.Length;i++)
                        {
                            Rect a = Bounds(controls[i]); Check(a.xMin>=12 && a.yMin>=12 && a.xMax<=size.x-12 && a.yMax<=size.y-12, prefix + " safe " + controls[i].name);
                            for(int j=i+1;j<controls.Length;j++) Check(!a.Overlaps(Bounds(controls[j])), prefix + " disjoint " + controls[i].name + "/" + controls[j].name);
                        }
                        Button("LookSettings").onClick.Invoke(); yield return new WaitForSecondsRealtime(.15f); Capture("settings");
                        yield return Escape(); Capture("settings-closed");
                        Check(look.IsPaused && !overlay.LookSettingsOpen && gate.IsSuspended, prefix + " first Esc stays paused");
                        yield return Escape(); Capture("resumed");
                        Check(!look.IsPaused && !gate.IsSuspended && state == flow.CurrentState, prefix + " second Esc resumes same state");
                        Check(presentation.StageTimeRemaining <= remaining && presentation.StageTimeRemaining >= Mathf.Max(0,remaining-.4f), prefix + " remaining time resumes");
                        if (remaining > .4f) Check(presentation.IsStagePresenting, prefix + " stage notice restored");
                        yield return new WaitForSeconds(.2f);
                    }
                    reset.ResetExperience(); flow.ShowResult(); yield return new WaitForSecondsRealtime(.2f);
                    look.Pause(); Check(!look.IsPaused && !gate.IsSuspended, size + " result does not expose unsupported pause");
                }
            }
            finally
            {
                reset.ResetExperience(); gate.Apply(); raycaster.enabled = rayEnabled;
                InputSystem.RemoveDevice(keyboard); InputSystem.settings.editorInputBehaviorInPlayMode=oldInput; InputSystem.settings.backgroundBehavior=oldBackground;
                Directory.CreateDirectory(output); notes.Add("passed="+passed); File.WriteAllLines(Path.Combine(output,"verification.txt"),notes);
            }
            complete(passed);
        }
    }
}
