using System.IO;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Tutorial;
using MeteorDefenseVR.Launch;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.UI;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.Audio;
using MeteorDefenseVR.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MeteorDefenseVR.Editor
{
    [InitializeOnLoad]
    internal static class ProjectSceneSetup
    {
        private const string SceneDirectory = "Assets/_Project/Scenes";
        private const string BootstrapScenePath = SceneDirectory + "/Bootstrap.unity";
        private const string GameScenePath = SceneDirectory + "/MeteorDefense.unity";
        private const string SetupSessionKey = "MeteorDefenseVR.ProjectSceneSetup.Completed";

        static ProjectSceneSetup()
        {
            if (!SessionState.GetBool(SetupSessionKey, false))
                EditorApplication.delayCall += EnsureProjectScenes;
        }

        [MenuItem("Meteor Defense/Setup Project Scenes")]
        public static void EnsureProjectScenes()
        {
            SessionState.SetBool(SetupSessionKey, true);
            Directory.CreateDirectory(SceneDirectory);

            if (!File.Exists(BootstrapScenePath))
            {
                Scene bootstrap = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Bootstrap").AddComponent<GameFlowManager>();
                EditorSceneManager.SaveScene(bootstrap, BootstrapScenePath);
            }

            EnsureBootstrapScene();

            if (!File.Exists(GameScenePath))
            {
                Scene game = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                GameObject root = new GameObject("MeteorDefense");

                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(root.transform);
                cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();

                GameObject lightObject = new GameObject("Directional Light");
                lightObject.transform.SetParent(root.transform);
                lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;

                EditorSceneManager.SaveScene(game, GameScenePath);
            }

            EnsureGazeSystem();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true)
            };

            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] Bootstrap and MeteorDefense scenes are ready.");
        }

        private static void EnsureGazeSystem()
        {
            Scene game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            Camera camera = Camera.main;
            if (camera == null)
            {
                Debug.LogWarning("[ProjectSetup] Main Camera was not found; gaze setup was skipped.");
                return;
            }

            GameObject gazeSystem = GameObject.Find("GazeSystem");
            if (gazeSystem == null) gazeSystem = new GameObject("GazeSystem");

            EditorGazeProvider provider = gazeSystem.GetComponent<EditorGazeProvider>();
            if (provider == null) provider = gazeSystem.AddComponent<EditorGazeProvider>();
            provider.SetCamera(camera);

            GazeRaycaster raycaster = gazeSystem.GetComponent<GazeRaycaster>();
            if (raycaster == null) raycaster = gazeSystem.AddComponent<GazeRaycaster>();
            raycaster.SetProvider(provider);

            EnsureCalibrationSystem(provider);
            EnsureCombatSystem(raycaster);
            EnsureTutorialSystem();
            EnsurePracticeSystem();
            EnsureLaunchSystem();
            EnsureMainGameSpawner(camera);
            EnsureMissionHud(camera);
            EnsurePlayerSystem(camera);
            EnsureBossClimaxSystem(camera);
            EnsureCombatVfxSystem();
            EnsureMissionCompleteSystem(camera);
            EnsureResultSystem(camera);
            EnsureAudioSystem();
            EnsureOperationsSystem(camera);

            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);
        }

        private static void EnsureBootstrapScene()
        {
            Scene bootstrap = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            GameObject root = GameObject.Find("Bootstrap");
            if (root == null) root = new GameObject("Bootstrap");
            if (root.GetComponent<GameFlowManager>() == null) root.AddComponent<GameFlowManager>();
            if (root.GetComponent<BootstrapSceneLoader>() == null) root.AddComponent<BootstrapSceneLoader>();
            EditorSceneManager.MarkSceneDirty(bootstrap);
            EditorSceneManager.SaveScene(bootstrap);
        }

        private static void EnsureAudioSystem()
        {
            GameObject root = GameObject.Find("AudioSystem");
            if (root == null) root = new GameObject("AudioSystem");
            AudioManager manager = root.GetComponent<AudioManager>();
            if (manager == null) manager = root.AddComponent<AudioManager>();

            AudioCueLibrary library = GetOrCreateAudioCueLibrary();
            AudioSource bgm = EnsureAudioSource(root.transform, "BGM");
            AudioSource ui = EnsureAudioSource(root.transform, "UI");
            AudioSource sfx = EnsureAudioSource(root.transform, "SFX");
            AudioSource voice = EnsureAudioSource(root.transform, "Voice");
            AudioSource ambient = EnsureAudioSource(root.transform, "Ambient");
            manager.Configure(library, bgm, ui, sfx, voice, ambient);

            GameAudioEventRouter router = root.GetComponent<GameAudioEventRouter>();
            if (router == null) router = root.AddComponent<GameAudioEventRouter>();
            router.Configure(
                manager,
                GameObject.Find("CalibrationSystem")?.GetComponent<CalibrationSequenceController>(),
                GameObject.Find("CombatSystem")?.GetComponent<LaserWeapon>(),
                GameObject.Find("PlayerSystem")?.GetComponent<PlayerHealth>(),
                GameObject.Find("BossClimaxSystem")?.GetComponent<BossClimaxController>(),
                GameObject.Find("LaunchSystem")?.GetComponent<LaunchSequenceController>(),
                GameObject.Find("MissionCompleteSystem")?.GetComponent<MissionCompleteController>(),
                GameObject.Find("ResultSystem")?.GetComponent<ResultController>());
        }

        private static void EnsureCombatVfxSystem()
        {
            GameObject root = GameObject.Find("CombatVFX");
            if (root == null) root = new GameObject("CombatVFX");
            CombatVfxController controller = root.GetComponent<CombatVfxController>();
            if (controller == null) controller = root.AddComponent<CombatVfxController>();

            Transform hit = EnsurePrimitive(root.transform, "HitSpark", PrimitiveType.Sphere,
                Vector3.zero, Vector3.one * 0.04f);
            Transform explosion = EnsurePrimitive(root.transform, "MeteorExplosion", PrimitiveType.Sphere,
                Vector3.zero, Vector3.one * 0.08f);
            Transform bossExplosion = EnsurePrimitive(root.transform, "BossExplosion", PrimitiveType.Sphere,
                Vector3.zero, Vector3.one * 0.12f);

            ApplyVfxMaterial(hit, GetOrCreateVfxMaterial("HitSpark", new Color(0.35f, 0.95f, 1f, 1f)));
            ApplyVfxMaterial(explosion, GetOrCreateVfxMaterial("MeteorExplosion", new Color(1f, 0.24f, 0.025f, 1f)));
            ApplyVfxMaterial(bossExplosion, GetOrCreateVfxMaterial("BossExplosion", new Color(1f, 0.08f, 0.015f, 1f)));

            for (int i = 0; i < 6; i++)
            {
                float angle = Mathf.PI * 2f * i / 6f;
                Transform debris = EnsurePrimitive(explosion, "Debris_" + (i + 1), PrimitiveType.Cube,
                    new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.65f,
                    new Vector3(0.18f, 0.08f, 0.08f));
                ApplyVfxMaterial(debris, GetOrCreateVfxMaterial("MeteorExplosion", new Color(1f, 0.24f, 0.025f, 1f)));
            }

            controller.Configure(
                GameObject.Find("CombatSystem")?.GetComponent<LaserWeapon>(),
                GameObject.Find("BossClimaxSystem")?.GetComponent<BossClimaxController>(),
                hit, explosion, bossExplosion);
        }

        private static Material GetOrCreateVfxMaterial(string name, Color color)
        {
            string path = "Assets/_Project/VFX/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            material = new Material(shader) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void ApplyVfxMaterial(Transform target, Material material)
        {
            Renderer renderer = target != null ? target.GetComponent<Renderer>() : null;
            if (renderer != null) renderer.sharedMaterial = material;
        }

        private static AudioSource EnsureAudioSource(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            GameObject child = existing != null ? existing.gameObject : new GameObject(name);
            child.transform.SetParent(parent, false);
            AudioSource source = child.GetComponent<AudioSource>();
            if (source == null) source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            return source;
        }

        private static AudioCueLibrary GetOrCreateAudioCueLibrary()
        {
            const string path = "Assets/_Project/Audio/AudioCueLibrary.asset";
            AudioCueLibrary library = AssetDatabase.LoadAssetAtPath<AudioCueLibrary>(path);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<AudioCueLibrary>();
                AssetDatabase.CreateAsset(library, path);
            }

            library.Configure(new[]
            {
                new AudioCueEntry(AudioCue.IntroSpaceAmbient, AudioCategory.Ambient, 0.28f, 1f, true),
                new AudioCueEntry(AudioCue.Warning, AudioCategory.UI, 0.45f, 0.7f),
                new AudioCueEntry(AudioCue.CalibrationPoint, AudioCategory.UI, 0.38f, 0.12f),
                new AudioCueEntry(AudioCue.CalibrationComplete, AudioCategory.UI, 0.5f, 0.5f),
                new AudioCueEntry(AudioCue.TargetFocus, AudioCategory.UI, 0.24f, 0.18f),
                new AudioCueEntry(AudioCue.LockOn, AudioCategory.UI, 0.48f, 0.2f),
                new AudioCueEntry(AudioCue.Laser, AudioCategory.Sfx, 0.42f, 0.08f),
                new AudioCueEntry(AudioCue.MeteorHit, AudioCategory.Sfx, 0.4f, 0.08f),
                new AudioCueEntry(AudioCue.MeteorExplosion, AudioCategory.Sfx, 0.52f, 0.14f),
                new AudioCueEntry(AudioCue.Score, AudioCategory.UI, 0.34f, 0.1f),
                new AudioCueEntry(AudioCue.Countdown, AudioCategory.Voice, 0.5f, 0.35f),
                new AudioCueEntry(AudioCue.Launch, AudioCategory.Sfx, 0.55f, 0.8f),
                new AudioCueEntry(AudioCue.PlayerDamage, AudioCategory.Sfx, 0.48f, 0.35f),
                new AudioCueEntry(AudioCue.BossWarning, AudioCategory.Voice, 0.58f, 1f),
                new AudioCueEntry(AudioCue.BossExplosion, AudioCategory.Sfx, 0.6f, 1f),
                new AudioCueEntry(AudioCue.MissionComplete, AudioCategory.Bgm, 0.48f, 1f),
                new AudioCueEntry(AudioCue.Result, AudioCategory.UI, 0.4f, 1f)
            });
            EditorUtility.SetDirty(library);
            return library;
        }

        private static void EnsureOperationsSystem(Camera camera)
        {
            GameObject root = GameObject.Find("OperationsSystem");
            if (root == null) root = new GameObject("OperationsSystem");
            OperationsResetController operations = root.GetComponent<OperationsResetController>();
            if (operations == null) operations = root.AddComponent<OperationsResetController>();

            MeteorSpawner spawner = GameObject.Find("MainGameSpawner")?.GetComponent<MeteorSpawner>();
            BossClimaxController boss = GameObject.Find("BossClimaxSystem")?.GetComponent<BossClimaxController>();
            LaserWeapon weapon = GameObject.Find("CombatSystem")?.GetComponent<LaserWeapon>();
            PlayerHealth health = GameObject.Find("PlayerSystem")?.GetComponent<PlayerHealth>();
            GameObject resultRoot = GameObject.Find("ResultSystem");
            GameSessionStats stats = resultRoot?.GetComponent<GameSessionStats>();
            ResultController result = resultRoot?.GetComponent<ResultController>();
            MissionHudController hud = GameObject.Find("MissionHUD")?.GetComponent<MissionHudController>();
            TutorialController tutorial = GameObject.Find("TutorialSystem")?.GetComponent<TutorialController>();
            PracticeController practice = GameObject.Find("PracticeSystem")?.GetComponent<PracticeController>();
            CalibrationSequenceController calibration = GameObject.Find("CalibrationSystem")?.GetComponent<CalibrationSequenceController>();
            LaunchSequenceController launch = GameObject.Find("LaunchSystem")?.GetComponent<LaunchSequenceController>();
            MissionCompleteController complete = GameObject.Find("MissionCompleteSystem")?.GetComponent<MissionCompleteController>();
            operations.Configure(spawner, boss, weapon, health, stats, hud, result, tutorial, practice, calibration, launch, complete);
            if (result != null) operations.SetResultDuration(result.DisplayDuration);

            GameObject readyRoot = GameObject.Find("ReadyScreen");
            if (readyRoot == null) readyRoot = new GameObject("ReadyScreen");
            readyRoot.transform.SetParent(camera.transform, false);
            readyRoot.transform.localPosition = Vector3.zero;
            ReadyScreenController ready = readyRoot.GetComponent<ReadyScreenController>();
            if (ready == null) ready = readyRoot.AddComponent<ReadyScreenController>();

            TextMesh title = EnsureHudText(readyRoot.transform, "ReadyTitle", new Vector3(0f, 0.25f, 3.8f),
                new Color(0.5f, 0.95f, 1f), 0.045f);
            Transform buttonTransform = readyRoot.transform.Find("ReadyStartButton");
            GameObject button = buttonTransform != null ? buttonTransform.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = "ReadyStartButton";
            button.transform.SetParent(readyRoot.transform, false);
            button.transform.localPosition = new Vector3(0f, -0.48f, 3.95f);
            button.transform.localRotation = Quaternion.identity;
            button.transform.localScale = new Vector3(1.15f, 0.36f, 0.035f);
            Renderer renderer = button.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = GetOrCreateHudPanelMaterial();
            if (button.GetComponent<Collider>() == null) button.AddComponent<BoxCollider>();
            ReadyStartGazeTarget gazeTarget = button.GetComponent<ReadyStartGazeTarget>();
            if (gazeTarget == null) gazeTarget = button.AddComponent<ReadyStartGazeTarget>();
            gazeTarget.Configure(ready);

            ReadyScreenView view = readyRoot.GetComponent<ReadyScreenView>();
            if (view == null) view = readyRoot.AddComponent<ReadyScreenView>();
            view.Configure(ready, title, button);
        }

        private static void EnsureResultSystem(Camera camera)
        {
            GameObject root = GameObject.Find("ResultSystem");
            if (root == null) root = new GameObject("ResultSystem");

            MeteorSpawner spawner = GameObject.Find("MainGameSpawner")?.GetComponent<MeteorSpawner>();
            BossClimaxController boss = GameObject.Find("BossClimaxSystem")?.GetComponent<BossClimaxController>();
            LaserWeapon weapon = GameObject.Find("CombatSystem")?.GetComponent<LaserWeapon>();
            PlayerHealth health = GameObject.Find("PlayerSystem")?.GetComponent<PlayerHealth>();
            MissionHudController hud = GameObject.Find("MissionHUD")?.GetComponent<MissionHudController>();

            GameSessionStats stats = root.GetComponent<GameSessionStats>();
            if (stats == null) stats = root.AddComponent<GameSessionStats>();
            stats.Configure(spawner, boss, weapon, health, hud);

            SessionRankSettings rankSettings = GetOrCreateRankSettings();
            ResultController controller = root.GetComponent<ResultController>();
            if (controller == null) controller = root.AddComponent<ResultController>();
            controller.Configure(stats, rankSettings);

            Transform textTransform = camera.transform.Find("ResultText");
            GameObject textObject = textTransform != null ? textTransform.gameObject : new GameObject("ResultText");
            textObject.transform.SetParent(camera.transform, false);
            textObject.transform.localPosition = new Vector3(0f, 0.05f, 3.75f);
            textObject.transform.localRotation = Quaternion.identity;
            TextMesh text = textObject.GetComponent<TextMesh>();
            if (text == null) text = textObject.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = 0.038f;
            text.color = new Color(0.45f, 0.95f, 1f);

            ResultView view = root.GetComponent<ResultView>();
            if (view == null) view = root.AddComponent<ResultView>();
            view.Configure(controller, text);
        }

        private static SessionRankSettings GetOrCreateRankSettings()
        {
            const string path = "Assets/_Project/GameFlow/SessionRankSettings.asset";
            SessionRankSettings settings = AssetDatabase.LoadAssetAtPath<SessionRankSettings>(path);
            if (settings != null) return settings;
            settings = ScriptableObject.CreateInstance<SessionRankSettings>();
            settings.Configure(3000, 2400, 1500);
            AssetDatabase.CreateAsset(settings, path);
            return settings;
        }

        private static void EnsureMissionCompleteSystem(Camera camera)
        {
            GameObject root = GameObject.Find("MissionCompleteSystem");
            if (root == null) root = new GameObject("MissionCompleteSystem");
            MissionCompleteController controller = root.GetComponent<MissionCompleteController>();
            if (controller == null) controller = root.AddComponent<MissionCompleteController>();
            MeteorSpawner spawner = GameObject.Find("MainGameSpawner")?.GetComponent<MeteorSpawner>();
            MissionHudController hud = GameObject.Find("MissionHUD")?.GetComponent<MissionHudController>();
            controller.Configure(spawner, hud);

            Transform earth = EnsurePrimitive(root.transform, "EarthFocus", PrimitiveType.Sphere,
                new Vector3(0f, 1.3f, 11f), Vector3.one * 3.4f);
            Renderer earthRenderer = earth.GetComponent<Renderer>();
            if (earthRenderer != null) earthRenderer.sharedMaterial = GetOrCreateEarthMaterial();

            Transform lightTransform = earth.Find("EarthFocusLight");
            GameObject lightObject = lightTransform != null ? lightTransform.gameObject : new GameObject("EarthFocusLight");
            lightObject.transform.SetParent(earth, false);
            lightObject.transform.localPosition = new Vector3(-0.4f, 0.5f, -1f);
            Light focusLight = lightObject.GetComponent<Light>();
            if (focusLight == null) focusLight = lightObject.AddComponent<Light>();
            focusLight.type = LightType.Point;
            focusLight.color = new Color(0.15f, 0.65f, 1f);
            focusLight.range = 8f;
            focusLight.intensity = 3f;
            focusLight.enabled = false;

            Transform textTransform = camera.transform.Find("MissionCompleteText");
            GameObject textObject = textTransform != null ? textTransform.gameObject : new GameObject("MissionCompleteText");
            textObject.transform.SetParent(camera.transform, false);
            textObject.transform.localPosition = new Vector3(0f, 0.15f, 3.8f);
            textObject.transform.localRotation = Quaternion.identity;
            TextMesh text = textObject.GetComponent<TextMesh>();
            if (text == null) text = textObject.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = 0.04f;
            text.color = new Color(0.4f, 1f, 0.48f);

            AudioSource victoryAudio = root.GetComponent<AudioSource>();
            if (victoryAudio == null) victoryAudio = root.AddComponent<AudioSource>();
            victoryAudio.playOnAwake = false;
            MissionCompleteView view = root.GetComponent<MissionCompleteView>();
            if (view == null) view = root.AddComponent<MissionCompleteView>();
            view.Configure(controller, text, earth, focusLight, victoryAudio);
        }

        private static Material GetOrCreateEarthMaterial()
        {
            const string path = "Assets/_Project/UI/Materials/EarthPlaceholder.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            material = new Material(shader) { name = "EarthPlaceholder" };
            material.color = new Color(0.025f, 0.2f, 0.5f, 1f);
            material.SetFloat("_Smoothness", 0.65f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureBossClimaxSystem(Camera camera)
        {
            MeteorAssetSetup.UpdateBossAssets();
            GameObject root = GameObject.Find("BossClimaxSystem");
            if (root == null) root = new GameObject("BossClimaxSystem");
            BossClimaxController controller = root.GetComponent<BossClimaxController>();
            if (controller == null) controller = root.AddComponent<BossClimaxController>();

            MeteorSpawner spawner = GameObject.Find("MainGameSpawner")?.GetComponent<MeteorSpawner>();
            Transform target = GameObject.Find("MainGameSpawner")?.transform.Find("PlayerDefenseTarget");
            MissionHudController hud = GameObject.Find("MissionHUD")?.GetComponent<MissionHudController>();
            PlayerHealth health = GameObject.Find("PlayerSystem")?.GetComponent<PlayerHealth>();
            GameObject bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Meteor/Prefabs/Meteor_Boss.prefab");
            controller.Configure(spawner, bossPrefab, camera.transform, target, hud, health);
        }

        private static void EnsurePlayerSystem(Camera camera)
        {
            GameObject root = GameObject.Find("PlayerSystem");
            if (root == null) root = new GameObject("PlayerSystem");
            MeteorSpawner spawner = GameObject.Find("MainGameSpawner")?.GetComponent<MeteorSpawner>();
            MissionHudController hud = GameObject.Find("MissionHUD")?.GetComponent<MissionHudController>();

            PlayerHealth health = root.GetComponent<PlayerHealth>();
            bool isNew = health == null;
            if (health == null) health = root.AddComponent<PlayerHealth>();
            if (isNew)
                health.Configure(spawner, hud, 100f, 10, 0.35f, GameOverPolicy.NoFailExperienceMode);
            else
                health.Connect(spawner, hud);

            Transform lightTransform = camera.transform.Find("CockpitDamageLight");
            GameObject lightObject = lightTransform != null ? lightTransform.gameObject : new GameObject("CockpitDamageLight");
            lightObject.transform.SetParent(camera.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, -0.2f, 0.6f);
            Light damageLight = lightObject.GetComponent<Light>();
            if (damageLight == null) damageLight = lightObject.AddComponent<Light>();
            damageLight.type = LightType.Point;
            damageLight.color = new Color(1f, 0.08f, 0.03f);
            damageLight.range = 4f;
            damageLight.intensity = 0f;
            damageLight.enabled = false;

            AudioSource impactAudio = root.GetComponent<AudioSource>();
            if (impactAudio == null) impactAudio = root.AddComponent<AudioSource>();
            impactAudio.playOnAwake = false;
            PlayerDamageFeedbackView view = root.GetComponent<PlayerDamageFeedbackView>();
            if (view == null) view = root.AddComponent<PlayerDamageFeedbackView>();
            view.Configure(health, damageLight, impactAudio);
        }

        private static void EnsureMissionHud(Camera camera)
        {
            GameObject root = GameObject.Find("MissionHUD");
            if (root == null) root = new GameObject("MissionHUD");
            root.transform.SetParent(camera.transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;

            MeteorSpawner spawner = GameObject.Find("MainGameSpawner")?.GetComponent<MeteorSpawner>();
            LaserWeapon weapon = GameObject.Find("CombatSystem")?.GetComponent<LaserWeapon>();
            MissionHudController controller = root.GetComponent<MissionHudController>();
            if (controller == null) controller = root.AddComponent<MissionHudController>();
            controller.Configure(spawner, weapon, 100f);

            Material panelMaterial = GetOrCreateHudPanelMaterial();
            CreateHudPanel(root.transform, "Panel_HP", new Vector3(-1.25f, 0.75f, 4f), panelMaterial);
            CreateHudPanel(root.transform, "Panel_Remaining", new Vector3(0f, 0.82f, 4f), panelMaterial);
            CreateHudPanel(root.transform, "Panel_Score", new Vector3(1.25f, 0.75f, 4f), panelMaterial);

            TextMesh health = EnsureHudText(root.transform, "HudHealth", new Vector3(-1.25f, 0.75f, 3.94f),
                new Color(0.35f, 1f, 0.45f), 0.026f);
            TextMesh remaining = EnsureHudText(root.transform, "HudRemaining", new Vector3(0f, 0.82f, 3.94f),
                new Color(0.35f, 0.95f, 1f), 0.027f);
            TextMesh score = EnsureHudText(root.transform, "HudScore", new Vector3(1.25f, 0.75f, 3.94f),
                new Color(0.45f, 0.95f, 1f), 0.028f);
            TextMesh feedback = EnsureHudText(root.transform, "HudFeedback", new Vector3(0f, 0.2f, 3.85f),
                new Color(0.45f, 1f, 0.5f), 0.04f);

            MissionHudView view = root.GetComponent<MissionHudView>();
            if (view == null) view = root.AddComponent<MissionHudView>();
            view.Configure(controller, health, remaining, score, feedback);
        }

        private static void CreateHudPanel(Transform parent, string name, Vector3 localPosition, Material material)
        {
            Transform panel = EnsurePrimitive(parent, name, PrimitiveType.Cube, localPosition, new Vector3(1.05f, 0.48f, 0.025f));
            Renderer renderer = panel.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
        }

        private static TextMesh EnsureHudText(
            Transform parent,
            string name,
            Vector3 localPosition,
            Color color,
            float characterSize)
        {
            Transform existing = parent.Find(name);
            GameObject textObject = existing != null ? existing.gameObject : new GameObject(name);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = localPosition;
            textObject.transform.localRotation = Quaternion.identity;
            TextMesh text = textObject.GetComponent<TextMesh>();
            if (text == null) text = textObject.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = characterSize;
            text.color = color;
            return text;
        }

        private static Material GetOrCreateHudPanelMaterial()
        {
            const string directory = "Assets/_Project/UI/Materials";
            const string path = directory + "/HudPanelDark.mat";
            Directory.CreateDirectory(directory);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            material = new Material(shader) { name = "HudPanelDark" };
            material.color = new Color(0.008f, 0.035f, 0.055f, 1f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureMainGameSpawner(Camera camera)
        {
            MeteorAssetSetup.CreateWaveAssets();
            GameObject root = GameObject.Find("MainGameSpawner");
            if (root == null) root = new GameObject("MainGameSpawner");
            MeteorSpawner spawner = root.GetComponent<MeteorSpawner>();
            if (spawner == null) spawner = root.AddComponent<MeteorSpawner>();

            Transform target = root.transform.Find("PlayerDefenseTarget");
            if (target == null)
            {
                target = new GameObject("PlayerDefenseTarget").transform;
                target.SetParent(root.transform);
            }
            target.position = camera.transform.position;

            MeteorWaveData[] waves =
            {
                AssetDatabase.LoadAssetAtPath<MeteorWaveData>("Assets/_Project/Meteor/Waves/Wave_01.asset"),
                AssetDatabase.LoadAssetAtPath<MeteorWaveData>("Assets/_Project/Meteor/Waves/Wave_02.asset"),
                AssetDatabase.LoadAssetAtPath<MeteorWaveData>("Assets/_Project/Meteor/Waves/Wave_03.asset")
            };
            spawner.Configure(waves, camera.transform, target);
        }

        private static void EnsureLaunchSystem()
        {
            GameObject root = GameObject.Find("LaunchSystem");
            if (root == null) root = new GameObject("LaunchSystem");
            LaunchSequenceController controller = root.GetComponent<LaunchSequenceController>();
            if (controller == null) controller = root.AddComponent<LaunchSequenceController>();

            Transform leftDoor = EnsurePrimitive(root.transform, "HangarDoor_Left", PrimitiveType.Cube,
                new Vector3(-1.25f, 1.6f, 5.5f), new Vector3(2.5f, 3.2f, 0.2f));
            Transform rightDoor = EnsurePrimitive(root.transform, "HangarDoor_Right", PrimitiveType.Cube,
                new Vector3(1.25f, 1.6f, 5.5f), new Vector3(2.5f, 3.2f, 0.2f));

            Transform cockpit = root.transform.Find("CockpitGeometry");
            if (cockpit == null)
            {
                cockpit = new GameObject("CockpitGeometry").transform;
                cockpit.SetParent(root.transform);
            }
            EnsurePrimitive(cockpit, "Console_Left", PrimitiveType.Cube,
                new Vector3(-1.15f, -0.35f, 1.3f), new Vector3(1.2f, 0.45f, 2f));
            EnsurePrimitive(cockpit, "Console_Right", PrimitiveType.Cube,
                new Vector3(1.15f, -0.35f, 1.3f), new Vector3(1.2f, 0.45f, 2f));

            Transform stars = root.transform.Find("Starfield");
            if (stars == null)
            {
                stars = new GameObject("Starfield").transform;
                stars.SetParent(root.transform);
            }
            for (int i = 0; i < 12; i++)
            {
                float x = ((i * 37) % 9 - 4) * 0.75f;
                float y = ((i * 19) % 7 - 3) * 0.45f + 1.6f;
                float z = 3f + i;
                EnsurePrimitive(stars, "Star_" + (i + 1), PrimitiveType.Sphere,
                    new Vector3(x, y, z), Vector3.one * 0.035f);
            }

            Light[] engineLights = new Light[2];
            for (int i = 0; i < engineLights.Length; i++)
            {
                string name = i == 0 ? "EngineGlow_Left" : "EngineGlow_Right";
                Transform existing = root.transform.Find(name);
                GameObject lightObject = existing != null ? existing.gameObject : new GameObject(name);
                lightObject.transform.SetParent(root.transform);
                lightObject.transform.localPosition = new Vector3(i == 0 ? -0.55f : 0.55f, -0.3f, 0.4f);
                Light light = lightObject.GetComponent<Light>();
                if (light == null) light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.15f, 0.75f, 1f);
                light.range = 3f;
                light.enabled = false;
                engineLights[i] = light;
            }

            TextMesh status = EnsureTextMesh(root.transform, "LaunchStatus", new Vector3(0f, 2.55f, 4f), 0.055f);
            AudioSource audioSource = root.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = root.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            LaunchEnvironmentView view = root.GetComponent<LaunchEnvironmentView>();
            if (view == null) view = root.AddComponent<LaunchEnvironmentView>();
            view.Configure(controller, status, leftDoor, rightDoor, cockpit, stars, engineLights, audioSource);
            controller.ResetLaunch();
        }

        private static Transform EnsurePrimitive(
            Transform parent,
            string name,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Vector3 localScale)
        {
            Transform existing = parent.Find(name);
            GameObject primitive = existing != null ? existing.gameObject : GameObject.CreatePrimitive(primitiveType);
            primitive.name = name;
            primitive.transform.SetParent(parent);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localRotation = Quaternion.identity;
            primitive.transform.localScale = localScale;
            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            return primitive.transform;
        }

        private static void EnsurePracticeSystem()
        {
            GameObject root = GameObject.Find("PracticeSystem");
            if (root == null) root = new GameObject("PracticeSystem");
            PracticeController controller = root.GetComponent<PracticeController>();
            if (controller == null) controller = root.AddComponent<PracticeController>();

            Vector3[] positions =
            {
                new Vector3(0f, 1.6f, 6f),
                new Vector3(-1.1f, 1.8f, 6.5f),
                new Vector3(1.1f, 1.35f, 6.2f)
            };
            Transform[] points = new Transform[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                Transform point = root.transform.Find("PracticeSpawn_" + (i + 1));
                if (point == null)
                {
                    GameObject pointObject = new GameObject("PracticeSpawn_" + (i + 1));
                    point = pointObject.transform;
                    point.SetParent(root.transform);
                }
                point.position = positions[i];
                points[i] = point;
            }

            GameObject normalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Meteor/Prefabs/Meteor_Normal.prefab");
            controller.Configure(normalPrefab, points);
            controller.ResetPractice();

            TextMesh hud = EnsureTextMesh(root.transform, "PracticeHud", new Vector3(0f, 2.8f, 4f), 0.03f);
            TextMesh status = EnsureTextMesh(root.transform, "PracticeStatus", new Vector3(0f, 2.3f, 4f), 0.035f);
            PracticeHudView view = hud.GetComponent<PracticeHudView>();
            if (view == null) view = hud.gameObject.AddComponent<PracticeHudView>();
            view.Configure(controller, hud, status);
        }

        private static TextMesh EnsureTextMesh(Transform parent, string name, Vector3 position, float characterSize)
        {
            Transform existing = parent.Find(name);
            GameObject textObject = existing != null ? existing.gameObject : new GameObject(name);
            textObject.transform.SetParent(parent);
            textObject.transform.position = position;
            TextMesh text = textObject.GetComponent<TextMesh>();
            if (text == null) text = textObject.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = characterSize;
            text.color = new Color(0.65f, 0.95f, 1f);
            return text;
        }

        private static void EnsureTutorialSystem()
        {
            GameObject root = GameObject.Find("TutorialSystem");
            if (root == null) root = new GameObject("TutorialSystem");
            TutorialController controller = root.GetComponent<TutorialController>();
            if (controller == null) controller = root.AddComponent<TutorialController>();

            Transform spawnPoint = root.transform.Find("TutorialMeteorSpawn");
            if (spawnPoint == null)
            {
                GameObject spawnObject = new GameObject("TutorialMeteorSpawn");
                spawnPoint = spawnObject.transform;
                spawnPoint.SetParent(root.transform);
            }
            spawnPoint.position = new Vector3(0f, 1.6f, 6f);

            GameObject tutorialPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Meteor/Prefabs/Meteor_Tutorial.prefab");
            controller.Configure(tutorialPrefab, spawnPoint);
            controller.ResetTutorial();

            Transform textTransform = root.transform.Find("InstructionText");
            GameObject textObject = textTransform != null ? textTransform.gameObject : new GameObject("InstructionText");
            textObject.transform.SetParent(root.transform);
            textObject.transform.position = new Vector3(0f, 2.7f, 4f);
            TextMesh text = textObject.GetComponent<TextMesh>();
            if (text == null) text = textObject.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = 0.035f;
            text.color = new Color(0.7f, 1f, 0.75f);
            TutorialStatusView view = textObject.GetComponent<TutorialStatusView>();
            if (view == null) view = textObject.AddComponent<TutorialStatusView>();
            view.Configure(controller, text);
        }

        private static void EnsureCombatSystem(GazeRaycaster raycaster)
        {
            GameObject root = GameObject.Find("CombatSystem");
            if (root == null) root = new GameObject("CombatSystem");
            LaserWeapon weapon = root.GetComponent<LaserWeapon>();
            if (weapon == null) weapon = root.AddComponent<LaserWeapon>();

            Transform[] muzzles = new Transform[2];
            LaserBeamView[] beams = new LaserBeamView[2];
            Material laserMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Meteor/LockOnReticleMaterial.mat");
            for (int i = 0; i < 2; i++)
            {
                string suffix = i == 0 ? "Left" : "Right";
                Transform muzzle = root.transform.Find("Cannon_" + suffix);
                if (muzzle == null)
                {
                    GameObject muzzleObject = new GameObject("Cannon_" + suffix);
                    muzzle = muzzleObject.transform;
                    muzzle.SetParent(root.transform);
                }
                muzzle.localPosition = new Vector3(i == 0 ? -0.35f : 0.35f, -0.3f, 0.5f);
                muzzles[i] = muzzle;

                Transform beamTransform = root.transform.Find("LaserBeam_" + suffix);
                GameObject beamObject = beamTransform != null ? beamTransform.gameObject : new GameObject("LaserBeam_" + suffix);
                beamObject.transform.SetParent(root.transform);
                LineRenderer line = beamObject.GetComponent<LineRenderer>();
                if (line == null) line = beamObject.AddComponent<LineRenderer>();
                line.sharedMaterial = laserMaterial;
                line.widthMultiplier = 0.04f;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                LaserBeamView beam = beamObject.GetComponent<LaserBeamView>();
                if (beam == null) beam = beamObject.AddComponent<LaserBeamView>();
                beam.Configure(line);
                beams[i] = beam;
            }
            weapon.Configure(raycaster, muzzles, beams);

            Transform popupTransform = root.transform.Find("ScorePopup");
            GameObject popupObject = popupTransform != null ? popupTransform.gameObject : new GameObject("ScorePopup");
            popupObject.transform.SetParent(root.transform);
            TextMesh popupText = popupObject.GetComponent<TextMesh>();
            if (popupText == null) popupText = popupObject.AddComponent<TextMesh>();
            popupText.anchor = TextAnchor.MiddleCenter;
            popupText.fontSize = 48;
            popupText.characterSize = 0.04f;
            popupText.color = new Color(0.6f, 0.95f, 1f);
            ScorePopupView popup = popupObject.GetComponent<ScorePopupView>();
            if (popup == null) popup = popupObject.AddComponent<ScorePopupView>();
            popup.Configure(weapon, popupText);
        }

        private static void EnsureCalibrationSystem(EditorGazeProvider gazeProvider)
        {
            GameObject root = GameObject.Find("CalibrationSystem");
            if (root == null) root = new GameObject("CalibrationSystem");
            CalibrationSequenceController controller = root.GetComponent<CalibrationSequenceController>();
            if (controller == null) controller = root.AddComponent<CalibrationSequenceController>();

            Vector3[] positions =
            {
                new Vector3(0f, 1.6f, 4f),
                new Vector3(-1.35f, 1.6f, 4f),
                new Vector3(1.35f, 1.6f, 4f),
                new Vector3(0f, 2.45f, 4f),
                new Vector3(0f, 0.75f, 4f)
            };
            string[] names = { "Center", "Left", "Right", "Top", "Bottom" };
            CalibrationPoint[] points = new CalibrationPoint[positions.Length];

            for (int i = 0; i < positions.Length; i++)
            {
                Transform existing = root.transform.Find("Point_" + names[i]);
                GameObject pointObject = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pointObject.name = "Point_" + names[i];
                pointObject.transform.SetParent(root.transform);
                pointObject.transform.position = positions[i];
                pointObject.transform.localScale = Vector3.one * 0.18f;
                points[i] = pointObject.GetComponent<CalibrationPoint>();
                if (points[i] == null) points[i] = pointObject.AddComponent<CalibrationPoint>();
            }

            Transform textTransform = root.transform.Find("StatusText");
            GameObject textObject = textTransform != null ? textTransform.gameObject : new GameObject("StatusText");
            textObject.transform.SetParent(root.transform);
            textObject.transform.position = new Vector3(0f, 2.9f, 4f);
            textObject.transform.rotation = Quaternion.identity;
            TextMesh text = textObject.GetComponent<TextMesh>();
            if (text == null) text = textObject.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = 0.035f;
            text.color = new Color(0.75f, 0.95f, 1f);

            CalibrationStatusView view = textObject.GetComponent<CalibrationStatusView>();
            if (view == null) view = textObject.AddComponent<CalibrationStatusView>();
            view.Configure(controller, text);
            controller.Configure(points, gazeProvider);
            controller.ResetSequence();
        }
    }
}
