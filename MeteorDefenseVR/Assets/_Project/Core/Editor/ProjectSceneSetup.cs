using System.IO;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Tutorial;
using MeteorDefenseVR.Launch;
using MeteorDefenseVR.Meteor;
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

            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);
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
