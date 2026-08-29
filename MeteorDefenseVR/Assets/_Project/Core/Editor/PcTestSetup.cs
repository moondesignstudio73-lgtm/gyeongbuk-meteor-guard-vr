using System.IO;
using System.Linq;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.PcInput;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace MeteorDefenseVR.Editor
{
    public static class PcTestSetup
    {
        [MenuItem("Meteor Defense/Setup PC Test Mode")]
        public static void Setup() => WithResources(ProjectSceneSetup.EnsureProjectScenes);
        private static void WithResources(System.Action work)
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
            {
                // ImportPackage completes asynchronously; do not create a font before its settings exist.
                AssetDatabase.ImportPackageCallback completed = null;
                completed = _ =>
                {
                    AssetDatabase.importPackageCompleted -= completed;
                    EditorApplication.delayCall += () => WithResources(work);
                };
                AssetDatabase.importPackageCompleted += completed;
                TMP_PackageResourceImporter.ImportResources(true, false, false);
                return;
            }
            try
            {
                work();
                Debug.Log("[PCSetup] PC input, calibration and UI ready.");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }
        public static void Ensure(Camera camera)
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
                throw new System.InvalidOperationException("Run Meteor Defense/Setup PC Test Mode to import TMP essentials first.");
            TMP_FontAsset font = EnsureFont();
            GameObject gaze = GameObject.Find("GazeSystem");
            if (gaze == null || camera == null) return;
            EditorGazeProvider mouse = gaze.GetComponent<EditorGazeProvider>();
            mouse.Mode = EditorGazeMode.MousePosition;
            Transform centerRoot = gaze.transform.Find("CameraCenterInput");
            if (centerRoot == null) { centerRoot = new GameObject("CameraCenterInput").transform; centerRoot.SetParent(gaze.transform, false); }
            EditorGazeProvider center = GetOrAdd<EditorGazeProvider>(centerRoot.gameObject);
            center.SetCamera(camera); center.Mode = EditorGazeMode.CameraCenter;
            VREyeGazeProvider vr = GetOrAdd<VREyeGazeProvider>(gaze);
            WebcamGazeProvider webcam = GetOrAdd<WebcamGazeProvider>(gaze);
            TextAsset model = AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/com.github.homuler.mediapipe/PackageResources/MediaPipe/face_landmarker_v2_with_blendshapes.bytes");
            webcam.Configure(camera, model);
            GazeInputRouter router = GetOrAdd<GazeInputRouter>(gaze);
            router.Configure(camera, vr, webcam, mouse, center);
            gaze.GetComponent<GazeRaycaster>().SetProvider(router);
            CalibrationSequenceController calibration = Object.FindAnyObjectByType<CalibrationSequenceController>();
            if (calibration != null) calibration.Configure(calibration.GetComponentsInChildren<CalibrationPoint>(true), router);
            GameObject pc = GameObject.Find("PcTestSystem");
            if (pc == null) pc = new GameObject("PcTestSystem");
            PcCalibrationController pcCalibration = GetOrAdd<PcCalibrationController>(pc);
            pcCalibration.Configure(router, calibration);
            PcTestControls controls = GetOrAdd<PcTestControls>(pc);
            controls.Configure(router, camera);
            GetOrAdd<ExperienceConfiguration>(pc).Configure(router, controls);
            GetOrAdd<PcTestOverlay>(pc).Configure(router, pcCalibration, controls, font);
            FinalPresentationSetup.Ensure(camera, font);
            FinalAudioSetup.Ensure();
            PremiumVisualSetup.Ensure(camera, font);
            // Do not package unused mobile webcam native libraries into a future VR build.
            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                if (!path.StartsWith("Packages/com.github.homuler.mediapipe/Runtime/Plugins/Android/")) continue;
                if (AssetImporter.GetAtPath(path) is PluginImporter plugin && plugin.GetCompatibleWithPlatform(BuildTarget.Android))
                { plugin.SetCompatibleWithPlatform(BuildTarget.Android, false); plugin.SaveAndReimport(); }
            }
        }
        private static T GetOrAdd<T>(GameObject root) where T : Component => root.GetComponent<T>() ?? root.AddComponent<T>();
        public static void BakePresentationFont() => WithResources(() => { EnsureFont(); AssetDatabase.SaveAssets(); });
        private static TMP_FontAsset EnsureFont()
        {
            const string assetPath = "Assets/_Project/PcInput/Fonts/PcTestKorean.asset";
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            Font source = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/PcInput/Fonts/NotoSansKR.ttf");
            if (source == null) return TMP_Settings.defaultFontAsset;
            TMP_FontAsset font = existing != null ? existing : TMP_FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            // Static assets drop the runtime source reference. Reattach it explicitly when
            // baking additional characters after an Editor/domain restart.
            using (var serializedFont = new SerializedObject(font))
            {
                serializedFont.FindProperty("m_SourceFontFile").objectReferenceValue = source;
                serializedFont.FindProperty("m_SourceFontFilePath").stringValue = Path.GetFullPath(AssetDatabase.GetAssetPath(source));
                serializedFont.ApplyModifiedPropertiesWithoutUndo();
            }
            font.name = "PcTestKorean";
            string characters = string.Concat(Enumerable.Range(32, 95).Select(c => (char)c)) + "○◉●";
            foreach (string path in Directory.GetFiles("Assets/_Project", "*.cs", SearchOption.AllDirectories)) characters += File.ReadAllText(path);
            characters = new string(characters.Distinct().Where(c => !char.IsControl(c)).ToArray());
            string toBake = new string(characters.Where(c => !font.HasCharacter(c)).ToArray());
            string missing = string.Empty;
            if (toBake.Length > 0) font.TryAddCharacters(toBake, out missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("[PCFont] Unsupported source characters: " + missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            if (existing == null) AssetDatabase.CreateAsset(font, assetPath);
            if (!AssetDatabase.Contains(font.material)) AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (Texture2D texture in font.atlasTextures) if (texture != null && !AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture, font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return font;
        }
        [MenuItem("Meteor Defense/Build Windows PC Test")]
        public static void BuildWindows()
            => WithResources(BuildWindowsReady);
        // Targeted UI/code releases must not regenerate or overwrite the authored scene.
        public static void BuildWindowsCurrentScene()
            => WithResources(() => { EnsureFont(); FinalAudioSetup.Ensure(); AssetDatabase.SaveAssets(); BuildWindowsReady(false); });
        public static void BuildWindowsAndCapture()
        {
            BuildWindowsReady();
            PcVisualQaCapture.RunGraphics();
        }
        private static void BuildWindowsReady() => BuildWindowsReady(true);
        private static void BuildWindowsReady(bool regenerateScene)
        {
            if (regenerateScene) ProjectSceneSetup.EnsureProjectScenes();
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            Directory.CreateDirectory("Builds/Windows");
            string configPath = "Builds/Windows/" + ExperienceConfiguration.FileName;
            if (!File.Exists(configPath)) File.WriteAllText(configPath, JsonUtility.ToJson(new ExperienceSettings(), true));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Project/Scenes/Bootstrap.unity", "Assets/_Project/Scenes/MeteorDefense.unity" },
                locationPathName = "Builds/Windows/MeteorDefenseVR.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded) throw new System.Exception("Windows build failed: " + report.summary.totalErrors);
            File.Copy("THIRD_PARTY_PC_INPUT.md", "Builds/Windows/THIRD_PARTY_PC_INPUT.md", true);
            File.Copy("Assets/_Project/PcInput/Fonts/OFL.txt", "Builds/Windows/NotoSansKR-OFL.txt", true);
            Directory.CreateDirectory("Builds/Windows/ThirdPartyNotices");
            foreach (string notice in Directory.GetFiles("ThirdPartyNotices"))
                File.Copy(notice, Path.Combine("Builds/Windows/ThirdPartyNotices", Path.GetFileName(notice)), true);
            foreach (string launcher in Directory.GetFiles("Tools/Launchers", "*.cmd"))
                File.Copy(launcher, Path.Combine("Builds/Windows", Path.GetFileName(launcher)), true);
            File.Copy("Docs/FinalQA/OPERATOR_GUIDE.md", "Builds/Windows/OPERATOR_GUIDE.md", true);
            Debug.Log("[PCBuild] Windows executable ready: " + Path.GetFullPath("Builds/Windows/MeteorDefenseVR.exe"));
        }
    }
}
