using System;
using System.Collections.Generic;
using System.IO;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Visual;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.Editor
{
    // Editor-only, game-camera renders. No desktop capture, webcam access, or network calls.
    // Fixtures verify composition, not a complete gameplay session or a device benchmark.
    [InitializeOnLoad]
    public static class AutonomousVisualAudit
    {
        private const string Key = "MeteorDefense.AutonomousVisualAudit";
        private const string Output = "Docs/AutonomousQA";
        private static readonly GameState[] States = { GameState.Boot, GameState.EyeCalibration,
            GameState.Playing, GameState.MissionComplete, GameState.Result };
        private static double captureAt;
        private static VisualReport visual;

        [Serializable] private sealed class AssetReport
        {
            public string scope = "Project asset references and Editor scene census; not runtime GPU timings.";
            public int buildScenes, prefabs, materials, missingScripts, checkedComponents;
            public long sceneMeshVertices, sceneMeshTriangles;
            public int sceneRenderers, sceneParticleSystems, sceneAudioSources, sceneLights, shadowCastingLights;
            public List<string> issues = new List<string>();
            public List<string> textures = new List<string>();
            public List<string> unusedCandidates = new List<string>();
        }
        [Serializable] private sealed class VisualReport
        {
            public string scope = "Editor offscreen 16:9 game-camera fixtures, not Windows display/VR/full sessions.";
            public string result;
            public List<string> errors = new List<string>();
            public List<FrameReport> frames = new List<FrameReport>();
        }
        [Serializable] private sealed class FrameReport
        {
            public string file;
            public int width, height, editorDrawCalls, editorBatches, editorSetPassCalls, editorTriangles, editorVertices;
            public int activeParticles, particleSystems, lights;
            public List<string> visibleTextBounds = new List<string>();
            public List<string> outsideViewport = new List<string>();
        }

        static AutonomousVisualAudit() => EditorApplication.playModeStateChanged += OnPlayMode;

        public static void Run()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("This camera-free fixture audit requires batch mode.");
            Directory.CreateDirectory(Output);
            AuditAssets();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".Stage", 0);
            EditorApplication.isPlaying = true;
        }

        private static void AuditAssets()
        {
            var report = new AssetReport();
            var dependencies = new HashSet<string>(StringComparer.Ordinal);
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled) continue;
                report.buildScenes++;
                foreach (string path in AssetDatabase.GetDependencies(scene.path, true)) dependencies.Add(path);
                var loaded = EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
                foreach (var root in loaded.GetRootGameObjects()) CheckHierarchy(root, scene.path, report);
            }
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
                if (renderer != null) report.sceneRenderers++;
            foreach (var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
            {
                if (filter.sharedMesh == null) continue;
                report.sceneMeshVertices += filter.sharedMesh.vertexCount;
                for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++)
                    if (filter.sharedMesh.GetTopology(sub) == MeshTopology.Triangles)
                        report.sceneMeshTriangles += (long)filter.sharedMesh.GetIndexCount(sub) / 3;
            }
            report.sceneParticleSystems = Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include).Length;
            report.sceneAudioSources = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Length;
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            report.sceneLights = lights.Length;
            foreach (var light in lights) if (light.shadows != LightShadows.None) report.shadowCastingLights++;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); report.prefabs++;
                CheckHierarchy(AssetDatabase.LoadAssetAtPath<GameObject>(path), path, report);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); report.materials++;
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null) report.issues.Add("Missing material/shader: " + path);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) { report.issues.Add("Texture could not load: " + path); continue; }
                report.textures.Add($"{path}: {texture.width}x{texture.height}, {texture.format}, readable={importer.isReadable}, mipmaps={importer.mipmapEnabled}, max={importer.maxTextureSize}");
            }
            foreach (string settings in new[] { "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/QualitySettings.asset" })
                foreach (string path in AssetDatabase.GetDependencies(settings, true)) dependencies.Add(path);
            foreach (string path in AssetDatabase.GetAllAssetPaths())
                if (path.StartsWith("Assets/", StringComparison.Ordinal) && path.Contains("/Resources/"))
                    foreach (string dependency in AssetDatabase.GetDependencies(path, true)) dependencies.Add(dependency);
            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (!path.StartsWith("Assets/", StringComparison.Ordinal) || dependencies.Contains(path)) continue;
                if (ext == ".prefab" || ext == ".mat" || ext == ".png" || ext == ".mp3" || ext == ".wav" || ext == ".unity")
                    report.unusedCandidates.Add(path + " (not in static dependency closure; code/build references still require review)");
            }
            File.WriteAllText(Output + "/asset-audit.json", JsonUtility.ToJson(report, true));
            if (report.issues.Count != 0 || report.missingScripts != 0)
                throw new InvalidOperationException("Asset audit found missing references; inspect asset-audit.json.");
        }

        private static void CheckHierarchy(GameObject root, string path, AssetReport report)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                report.missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                foreach (var component in transform.GetComponents<Component>())
                {
                    if (component == null) continue;
                    report.checkedComponents++;
                    using (var serialized = new SerializedObject(component))
                    {
                        var property = serialized.GetIterator();
                        while (property.Next(true))
                        {
                            if (property.propertyType == SerializedPropertyType.ObjectReference &&
                                property.objectReferenceValue == null && property.objectReferenceEntityIdValue != EntityId.None)
                                report.issues.Add(path + "/" + transform.name + ": missing " + property.propertyPath);
                            if (property.propertyType == SerializedPropertyType.Float &&
                                (double.IsNaN(property.doubleValue) || double.IsInfinity(property.doubleValue)))
                                report.issues.Add(path + "/" + transform.name + ": nonfinite " + property.propertyPath);
                        }
                    }
                }
            }
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                visual = new VisualReport(); Application.logMessageReceived += CaptureError;
                if (Mouse.current == null) InputSystem.AddDevice<Mouse>();
                captureAt = EditorApplication.timeSinceStartup + 2;
                EditorApplication.update += Capture;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false);
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key + ".Exit", 1));
            }
        }

        private static void CaptureError(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                visual.errors.Add(type + ": " + message);
        }

        private static void Capture()
        {
            if (!Application.isPlaying || EditorApplication.timeSinceStartup < captureAt) return;
            try
            {
                var router = Object.FindAnyObjectByType<GazeInputRouter>();
                router.SetMode(PcGazeMode.MouseGaze); router.Webcam.StopCapture();
                if (router.Webcam.IsStreaming || router.Webcam.IsStarting || router.Webcam.ShowWebcamPreview || router.Webcam.ShowFaceDebug)
                    throw new InvalidOperationException("Game-only capture privacy precondition failed.");
                int stage = SessionState.GetInt(Key + ".Stage", 0);
                if (GameFlowManager.Instance.CurrentState != States[stage]) throw new InvalidOperationException("Fixture changed state before capture.");
                foreach (int width in new[] { 1920, 2560, 3840 }) SaveFrame(States[stage], width, width * 9 / 16);
                if (++stage == States.Length) { Finish(); return; }
                SessionState.SetInt(Key + ".Stage", stage);
                Object.FindAnyObjectByType<OperationsResetController>().ResetExperience();
                var config = Object.FindAnyObjectByType<ExperienceConfiguration>();
                config.Settings.experienceMode = ExperienceMode.PublicExperience; config.Apply();
                if (States[stage] == GameState.Result)
                {
                    var stats = Object.FindAnyObjectByType<GameSessionStats>(); stats.ResetSession(22); stats.SetScore(3300);
                    for (int i = 0; i < 22; i++) stats.RecordMeteorDestroyed();
                    for (int i = 0; i < 26; i++) { stats.RecordShotAndLock(); stats.RecordSuccessfulHit(); }
                }
                GameFlowManager.Instance.ChangeState(States[stage]);
                captureAt = EditorApplication.timeSinceStartup + (States[stage] == GameState.MissionComplete ? 1.6 : 1.1);
            }
            catch (Exception exception) { visual.errors.Add(exception.GetType().Name + ": " + exception.Message); Finish(); }
        }

        private static void SaveFrame(GameState state, int width, int height)
        {
            Camera camera = Camera.main;
            Canvas canvas = Object.FindAnyObjectByType<PcTestOverlay>().GetComponentInChildren<Canvas>();
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
            var previousMode = canvas.renderMode; var previousCamera = canvas.worldCamera;
            float previousAspect = camera.aspect, previousPlane = canvas.planeDistance;
            var frame = new FrameReport { file = $"Visual/{state}-{width}x{height}.png", width = width, height = height };
            try
            {
                camera.targetTexture = target; camera.aspect = (float)width / height;
                camera.GetComponent<WorldUiSafeArea>()?.Apply();
                foreach (var presentation in Object.FindObjectsByType<WorldTextPresentation>()) presentation.Refresh();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .5f;
                Canvas.ForceUpdateCanvases();
                foreach (var text in Object.FindObjectsByType<TMP_Text>()) text.ForceMeshUpdate();
                camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                Directory.CreateDirectory(Output + "/Visual"); File.WriteAllBytes(Output + "/" + frame.file, image.EncodeToPNG());
                frame.editorDrawCalls = UnityStats.drawCalls;
                frame.editorBatches = -1; // No UnityStats.batches counter in the installed Editor API.
                frame.editorSetPassCalls = UnityStats.setPassCalls; frame.editorTriangles = UnityStats.triangles; frame.editorVertices = UnityStats.vertices;
                foreach (var particles in Object.FindObjectsByType<ParticleSystem>())
                { frame.particleSystems++; frame.activeParticles += particles.particleCount; }
                frame.lights = Object.FindObjectsByType<Light>().Length;
                foreach (var text in Object.FindObjectsByType<TMP_Text>())
                {
                    if (!text.isActiveAndEnabled || text.color.a <= 0) continue;
                    if (text is TextMeshPro meshText && !meshText.renderer.enabled) continue;
                    Vector2 min = Vector2.one * float.MaxValue, max = Vector2.one * float.MinValue;
                    bool any = false;
                    foreach (var glyph in text.textInfo.characterInfo)
                    {
                        if (!glyph.isVisible) continue;
                        foreach (var corner in new[] { glyph.bottomLeft, glyph.topRight, glyph.topLeft, glyph.bottomRight })
                        {
                            Vector3 point = camera.WorldToViewportPoint(text.transform.TransformPoint(corner));
                            if (point.z <= 0) continue;
                            min = Vector2.Min(min, point); max = Vector2.Max(max, point); any = true;
                        }
                    }
                    if (!any) continue;
                    string bounds = text.name + ": " + min.ToString("F3") + " to " + max.ToString("F3");
                    frame.visibleTextBounds.Add(bounds);
                    if (min.x < -.002f || min.y < -.002f || max.x > 1.002f || max.y > 1.002f) frame.outsideViewport.Add(bounds);
                }
                visual.frames.Add(frame);
            }
            finally
            {
                canvas.renderMode = previousMode; canvas.worldCamera = previousCamera; canvas.planeDistance = previousPlane;
                camera.targetTexture = previousTarget; camera.aspect = previousAspect; camera.GetComponent<WorldUiSafeArea>()?.Apply();
                RenderTexture.active = previousActive; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }

        private static void Finish()
        {
            EditorApplication.update -= Capture; Application.logMessageReceived -= CaptureError;
            visual.result = visual.errors.Count == 0 && visual.frames.Count == 15 ? "CAPTURED_REQUIRES_VISUAL_REVIEW" : "FAILED";
            File.WriteAllText(Output + "/visual-audit.json", JsonUtility.ToJson(visual, true));
            SessionState.SetInt(Key + ".Exit", visual.result == "FAILED" ? 1 : 0);
            EditorApplication.isPlaying = false;
        }
    }
}
