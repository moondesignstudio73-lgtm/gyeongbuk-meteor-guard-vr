using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.UI;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.PcInput
{
    // Opt-in display regression fixture. State transitions are injected, not gameplay completion.
    public static class DifficultyHudProof
    {
        public static IEnumerator Run(string output, int width, int height, bool validate)
        {
            Directory.CreateDirectory(output);
            var manager = Object.FindAnyObjectByType<DifficultyManager>();
            float deadline = Time.realtimeSinceStartup + 30;
            while ((manager == null || !manager.CanSelect) && Time.realtimeSinceStartup < deadline)
            { yield return null; manager = Object.FindAnyObjectByType<DifficultyManager>(); }
            if (manager == null || !manager.CanSelect) throw new InvalidOperationException("Difficulty unavailable");
            var warm = Object.FindAnyObjectByType<ReadyPrewarmController>();
            while (!warm.IsComplete && Time.realtimeSinceStartup < deadline) yield return null;
            var flow = GameFlowManager.Instance;
            var hud = Object.FindAnyObjectByType<MissionHudController>(FindObjectsInactive.Include);
            var view = Object.FindAnyObjectByType<GameplayHudOverlayView>(FindObjectsInactive.Include);
            var result = Object.FindAnyObjectByType<ResultController>();
            var router = Object.FindAnyObjectByType<GazeInputRouter>();
            bool oldInput = router.InputSuspended;
            router.InputSuspended = true;
            var errors = new List<string>();
            using var probe = new PcStabilityProbe(Camera.main, width, height);
            IEnumerator Capture(string label, DifficultyLevel expected, int stage = 1)
            {
                yield return new WaitForSecondsRealtime(.3f);
                var driver = Object.FindAnyObjectByType<PcStabilityRenderDriver>();
                bool done = false; Exception failure = null;
                Action capture = () =>
                {
                    try
                    {
                        string folder = Path.Combine(output, label); Directory.CreateDirectory(folder);
                        Canvas.ForceUpdateCanvases(); PcStabilityRenderDriver.RenderForProof(Camera.main);
                        probe.SaveGameOnlyProof(folder, router);
                        string expectedText = GameplayHudOverlayView.FormatDifficultyStage(expected, stage);
                        bool resultState = flow.CurrentState == GameState.Result;
                        string resultText = string.Join("\n", Object.FindObjectsByType<TextMeshPro>(FindObjectsSortMode.None)
                            .Where(t => t.enabled && t.gameObject.activeInHierarchy && t.GetParsedText().Contains("난이도 :"))
                            .Select(t => t.GetParsedText()));
                        bool correct = manager.Session.Level == expected && (resultState
                            ? result.Snapshot.Difficulty == expected && resultText.Contains("난이도 : " + DifficultyConfig.Label(expected))
                            : view.DifficultyStageValue.text == expectedText);
                        File.WriteAllText(Path.Combine(folder, "state.txt"),
                            $"Forced state/display fixture; not natural input or combat completion. {width}x{height}\n" +
                            $"state={flow.CurrentState}; selected={manager.Selected}; session={manager.Session.Level}; frozen={manager.SessionFrozen}; campaignStage={hud.CampaignStage}; campaignDifficulty={hud.CampaignDifficulty}\n" +
                            $"display={view.DifficultyStageValue.text}; expected={expectedText}; resultDifficulty={result.Snapshot?.Difficulty}; correct={correct}\nresultText={resultText}\n");
                        if (!correct) errors.Add(label);
                        view.DifficultyStageValue.ForceMeshUpdate();
                        if (!resultState && view.DifficultyStageValue.isTextTruncated) errors.Add(label + " truncated");
                    }
                    catch (Exception e) { failure = e; }
                    done = true;
                };
                driver.RenderCompleted += capture;
                try { while (!done) yield return null; }
                finally { driver.RenderCompleted -= capture; }
                if (failure != null) throw failure;
            }
            try
            {
                int round = 0;
                foreach (var level in new[] { DifficultyLevel.Experience, DifficultyLevel.Easy, DifficultyLevel.Hard, DifficultyLevel.Experience })
                {
                    flow.ResetGame(); yield return null;
                    manager.Select(level);
                    string prefix = (++round) + "-" + level;
                    flow.StartMainGame();
                    yield return Capture(prefix + "-playing", level);
                    if (level != DifficultyLevel.Experience)
                    {
                        // Exercise the existing campaign metadata display independently of stage completion.
                        hud.SetCampaignStage(level, 10, 20);
                        yield return Capture(prefix + "-stage10", level, 10);
                    }
                    flow.StartBoss();
                    yield return Capture(prefix + "-boss", level, level == DifficultyLevel.Experience ? 1 : 10);
                    flow.ShowResult();
                    yield return Capture(prefix + "-result", level);
                }
                flow.ResetGame(); yield return null;
                yield return Capture("reset-default", manager.Settings.Default);
                File.WriteAllText(Path.Combine(output, "summary.txt"), $"validate={validate}; mismatches={string.Join(",", errors)}\n");
                if (validate && errors.Count > 0) throw new InvalidOperationException("Difficulty display mismatch: " + string.Join(",", errors));
            }
            finally { router.InputSuspended = oldInput; }
        }
    }
}
