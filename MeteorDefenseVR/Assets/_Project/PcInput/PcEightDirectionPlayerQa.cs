using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.PcInput
{
    public static class PcEightDirectionPlayerQa
    {
        private static readonly (float yaw, float pitch, string name)[] Views =
        {
            (0,0,"Front"),(-90,0,"Left"),(-135,0,"RearLeft"),(180,0,"Rear"),
            (135,0,"RearRight"),(90,0,"Right"),(0,65,"Above"),(0,-65,"Below")
        };

        [Serializable]
        private sealed class Report
        {
            public bool passed;
            public string scene = "Assets/_Project/Scenes/MeteorDefense.unity";
            public string camera = "Main Camera / Player Camera";
            public string difficulty = "Extreme";
            public string gameState = "Playing";
            public string hudMode = "Development";
            public List<string> captured = new List<string>();
            public List<string> errors = new List<string>();
        }

        public static IEnumerator Run(GazeInputRouter router)
        {
            var report = new Report();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "QA", "EightDirection"));
            Directory.CreateDirectory(output);
            PcStabilityProbe probe = null;
            try
            {
                Camera camera = Camera.main;
                if (camera == null) throw new InvalidOperationException("Player Camera missing");
                var experience = Object.FindAnyObjectByType<ExperienceConfiguration>();
                experience.Settings.experienceMode = ExperienceMode.Development; experience.Apply();
                router.Webcam.StopCapture(); router.Webcam.ShowWebcamPreview = false;
                router.SetMode(PcGazeMode.WindowsVRSimulation);
                Object.FindAnyObjectByType<DifficultyManager>().Select(DifficultyLevel.Extreme);
                GameFlowManager.Instance.StartMainGame();
                float deadline = Time.realtimeSinceStartup + 10f;
                while (GameFlowManager.Instance.CurrentState != GameState.Playing && Time.realtimeSinceStartup < deadline) yield return null;
                if (GameFlowManager.Instance.CurrentState != GameState.Playing) throw new TimeoutException("Playing state timeout");
                var simulation = router.Simulation;
                var spawner = Object.FindAnyObjectByType<MeteorSpawner>();
                spawner.BeginScenario(Views.Length);
                probe = new PcStabilityProbe(camera);
                foreach (var view in Views)
                {
                    spawner.ClearScenarioRound(); simulation.Recenter();
                    simulation.ApplyLook(new Vector2(view.yaw / .1f, -view.pitch / .09f), 1f);
                    yield return new WaitForSecondsRealtime(.25f);
                    MeteorController meteor = spawner.SpawnScenario(camera.transform.position + camera.transform.forward * 18f, Vector3.zero, 2.3f, 0, 10f);
                    if (meteor == null) throw new InvalidOperationException(view.name + " scenario meteor missing");
                    yield return new WaitForSecondsRealtime(.18f);
                    Vector3 viewport = camera.WorldToViewportPoint(meteor.transform.position);
                    if (viewport.z <= 0 || viewport.x < .40f || viewport.x > .60f || viewport.y < .38f || viewport.y > .62f)
                        throw new InvalidOperationException(view.name + " target viewport invalid: " + viewport);
                    Canvas.ForceUpdateCanvases();
                    foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>())
                    {
                        if (!Visible(text)) continue;
                        text.ForceMeshUpdate();
                        Vector2 preferred = text.GetPreferredValues(text.text, text.rectTransform.rect.width, 0);
                        if (preferred.y > text.rectTransform.rect.height + 1f) report.errors.Add(view.name + " text overflow: " + text.name);
                    }
                    string directory = Path.Combine(output, view.name); Directory.CreateDirectory(directory);
                    probe.SaveGameOnlyProof(directory, router); report.captured.Add(view.name);
                }
                spawner.EndScenario();
                report.passed = report.captured.Count == Views.Length && report.errors.Count == 0;
            }
            finally
            {
                probe?.Dispose();
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
                Debug.Log("[PCEightDirectionQA] " + (report.passed ? "PASS" : "FAIL") + "; " + string.Join(", ", report.captured));
                Application.Quit(report.passed ? 0 : 2);
            }
        }

        private static bool Visible(TMP_Text text)
        {
            if (!text.isActiveAndEnabled || text.color.a <= .01f) return false;
            for (Transform current=text.transform;current!=null;current=current.parent)
            {
                CanvasGroup group=current.GetComponent<CanvasGroup>();
                if(group!=null&&group.alpha<=.01f)return false;
            }
            return true;
        }
    }
}
