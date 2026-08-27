using System.IO;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.UI;
using MeteorDefenseVR.VFX;
using MeteorDefenseVR.Visual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MeteorDefenseVR.Editor
{
    public static class ProjectVisualIntegration
    {
        private const string ArtRoot = "Assets/Art/VisualIntegration";
        private const string MaterialRoot = ArtRoot + "/Materials";
        private const string TextureRoot = ArtRoot + "/Textures";
        private const string ScenePath = "Assets/_Project/Scenes/MeteorDefense.unity";

        private static readonly Color SpaceBlack = new Color(0.002f, 0.009f, 0.018f, 1f);
        private static readonly Color HullBlack = new Color(0.018f, 0.045f, 0.065f, 1f);
        private static readonly Color HullMid = new Color(0.055f, 0.11f, 0.14f, 1f);
        private static readonly Color Cyan = new Color(0.12f, 0.82f, 1f, 1f);
        private static readonly Color Green = new Color(0.25f, 1f, 0.3f, 1f);
        private static readonly Color Orange = new Color(1f, 0.19f, 0.025f, 1f);
        private static readonly Color Warning = new Color(1f, 0.055f, 0.02f, 1f);

        [MenuItem("Meteor Defense/Apply Final Visual Integration")]
        public static void ApplyToCurrentScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Camera camera = Camera.main;
            if (camera == null)
            {
                Debug.LogError("[VisualIntegration] Main Camera is missing.");
                return;
            }
            Ensure(camera);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[VisualIntegration] Final visual layer applied to MeteorDefense.");
        }

        public static void Ensure(Camera camera)
        {
            if (camera == null) return;
            Directory.CreateDirectory(MaterialRoot);
            ConfigureTextureImporters();
            AssetDatabase.Refresh();
            MeteorAssetSetup.CreateMeteorAssets();

            Material hull = GetOrCreateLit("CockpitHull", HullMid, 0.78f, 0.32f);
            Material hullDark = GetOrCreateLit("CockpitHullDark", HullBlack, 0.55f, 0.2f);
            Material cyan = GetOrCreateEmission("CyanEmission", Cyan, 4.2f);
            Material green = GetOrCreateEmission("GreenEmission", Green, 3.2f);
            Material orange = GetOrCreateEmission("OrangePlasma", Orange, 6f);
            Material warning = GetOrCreateEmission("WarningRed", Warning, 5f);
            Material panel = GetOrCreateTransparent("TacticalGlass", new Color(0.005f, 0.045f, 0.065f, 0.86f), 0.04f);
            Material canopy = GetOrCreateTransparent("CanopyGlass", new Color(0.02f, 0.16f, 0.22f, 0.1f), 0.7f);

            ConfigureSpace(camera);
            Transform earth = ConfigureEarth();
            Transform stars = ConfigureStarDust();
            GameObject cockpit = ConfigureCockpit(hull, hullDark, cyan, orange, canopy);
            GameObject hangar = ConfigureHangar(hull, hullDark, cyan, warning);
            ConfigureHud(camera, panel, cyan, green);
            ConfigureReadyAndResults(camera, panel, cyan, green);
            ConfigureLasers(orange);
            ConfigureCombatVfx(orange, cyan, warning);
            ConfigureCinematicDirector(camera, earth, stars, cockpit, hangar, panel, cyan, warning);
            ConfigurePostProcessing(camera);
            DisableShadows();
        }

        private static void ConfigureTextureImporters()
        {
            ConfigureTexture(TextureRoot + "/SpacePanorama.png", TextureWrapMode.Repeat, 4096, true);
            ConfigureTexture(TextureRoot + "/EarthAlbedo.png", TextureWrapMode.Repeat, 4096, true);
            ConfigureTexture(TextureRoot + "/MeteorSurface.png", TextureWrapMode.Repeat, 2048, true);
        }

        private static void ConfigureTexture(string path, TextureWrapMode wrap, int maxSize, bool mipmaps)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            bool changed = importer.wrapMode != wrap || importer.maxTextureSize != maxSize || importer.mipmapEnabled != mipmaps;
            importer.wrapMode = wrap;
            importer.maxTextureSize = maxSize;
            importer.mipmapEnabled = mipmaps;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.sRGBTexture = true;
            if (changed) importer.SaveAndReimport();
        }

        private static void ConfigureSpace(Camera camera)
        {
            Texture2D panorama = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureRoot + "/SpacePanorama.png");
            const string skyboxPath = MaterialRoot + "/DeepSpaceSkybox.mat";
            Material skybox = AssetDatabase.LoadAssetAtPath<Material>(skyboxPath);
            if (skybox == null)
            {
                skybox = new Material(Shader.Find("Skybox/Panoramic")) { name = "DeepSpaceSkybox" };
                AssetDatabase.CreateAsset(skybox, skyboxPath);
            }
            if (panorama != null) skybox.SetTexture("_MainTex", panorama);
            skybox.SetFloat("_Exposure", 0.38f);
            skybox.SetFloat("_Rotation", 8f);
            skybox.SetColor("_Tint", new Color(0.68f, 0.82f, 1f, 1f));
            EditorUtility.SetDirty(skybox);

            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.055f, 0.11f, 0.17f);
            RenderSettings.ambientEquatorColor = new Color(0.015f, 0.04f, 0.065f);
            RenderSettings.ambientGroundColor = SpaceBlack;
            RenderSettings.ambientIntensity = 0.62f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.002f, 0.015f, 0.026f);
            RenderSettings.fogDensity = 0.0012f;

            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = SpaceBlack;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 250f;
            camera.fieldOfView = 74f;
            camera.allowHDR = true;

            Light key = GameObject.Find("Directional Light")?.GetComponent<Light>();
            if (key != null)
            {
                key.type = LightType.Directional;
                key.transform.rotation = Quaternion.Euler(28f, -38f, 8f);
                key.color = new Color(0.46f, 0.68f, 1f);
                key.intensity = 0.85f;
                key.shadows = LightShadows.None;
            }

            GameObject fillObject = GameObject.Find("SpaceFillLight");
            if (fillObject == null) fillObject = new GameObject("SpaceFillLight");
            fillObject.transform.position = new Vector3(0f, 1.8f, 1.2f);
            Light fill = fillObject.GetComponent<Light>();
            if (fill == null) fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(0.36f, 0.58f, 1f);
            fill.intensity = 1.35f;
            fill.range = 28f;
            fill.shadows = LightShadows.None;
        }

        private static Transform ConfigureEarth()
        {
            GameObject completeRoot = GameObject.Find("MissionCompleteSystem");
            if (completeRoot == null) return null;
            Transform earth = completeRoot.transform.Find("EarthFocus");
            if (earth == null) return null;
            earth.position = new Vector3(5.2f, 1.1f, 18f);
            earth.localScale = Vector3.one * 6.2f;
            Renderer renderer = earth.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = GetOrCreateLit("EarthSurface", Color.white, 0f, 0.12f);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureRoot + "/EarthAlbedo.png");
                if (texture != null) material.mainTexture = texture;
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(0.025f, 0.06f, 0.12f));
                EditorUtility.SetDirty(material);
                renderer.sharedMaterial = material;
            }

            Shader rimShader = Shader.Find("MeteorDefense/AtmosphereRim");
            string atmospherePath = MaterialRoot + "/EarthAtmosphere.mat";
            Material atmosphere = AssetDatabase.LoadAssetAtPath<Material>(atmospherePath);
            if (atmosphere == null)
            {
                atmosphere = new Material(rimShader) { name = "EarthAtmosphere" };
                AssetDatabase.CreateAsset(atmosphere, atmospherePath);
            }
            atmosphere.shader = rimShader;
            atmosphere.renderQueue = (int)RenderQueue.Transparent;
            atmosphere.SetColor("_BaseColor", new Color(0.08f, 0.48f, 1f, 0.45f));
            EditorUtility.SetDirty(atmosphere);
            Transform glow = EnsurePrimitive(earth, "AtmosphereGlow", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 1.035f, Vector3.zero, atmosphere);
            glow.SetAsFirstSibling();

            Transform rimLight = earth.Find("EarthRimLight");
            GameObject lightObject = rimLight != null ? rimLight.gameObject : new GameObject("EarthRimLight");
            lightObject.transform.SetParent(earth, false);
            lightObject.transform.localPosition = new Vector3(-1.4f, 0.8f, -1.8f);
            Light light = lightObject.GetComponent<Light>();
            if (light == null) light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.1f, 0.55f, 1f);
            light.range = 5f;
            light.intensity = 3.5f;
            return earth;
        }

        private static Transform ConfigureStarDust()
        {
            GameObject root = GameObject.Find("SpaceEnvironment");
            if (root == null) root = new GameObject("SpaceEnvironment");
            Transform particleTransform = root.transform.Find("NearStarDust");
            GameObject particleObject = particleTransform != null ? particleTransform.gameObject : new GameObject("NearStarDust");
            particleObject.transform.SetParent(root.transform, false);
            ParticleSystem particles = particleObject.GetComponent<ParticleSystem>();
            if (particles == null) particles = particleObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(32f, 48f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.015f, 0.06f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.055f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.4f, 0.75f, 1f), Color.white);
            main.maxParticles = 260;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 7f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 36f;
            shape.radiusThickness = 1f;
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = GetOrCreateEmission("StarParticle", new Color(0.65f, 0.85f, 1f), 2.2f);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return root.transform;
        }

        private static GameObject ConfigureCockpit(Material hull, Material hullDark, Material cyan, Material orange, Material canopy)
        {
            GameObject launch = GameObject.Find("LaunchSystem");
            if (launch == null) return null;
            Transform cockpit = launch.transform.Find("CockpitGeometry");
            if (cockpit == null)
            {
                cockpit = new GameObject("CockpitGeometry").transform;
                cockpit.SetParent(launch.transform, false);
            }

            EnsurePrimitive(cockpit, "Console_Left", PrimitiveType.Cube, new Vector3(-1.08f, -0.62f, 1.45f), new Vector3(1.25f, 0.18f, 1.45f), new Vector3(8f, -11f, -7f), hull);
            EnsurePrimitive(cockpit, "Console_Right", PrimitiveType.Cube, new Vector3(1.08f, -0.62f, 1.45f), new Vector3(1.25f, 0.18f, 1.45f), new Vector3(8f, 11f, 7f), hull);
            EnsurePrimitive(cockpit, "CenterCommandDeck", PrimitiveType.Cube, new Vector3(0f, -0.72f, 1.28f), new Vector3(0.64f, 0.18f, 1.05f), new Vector3(9f, 0f, 0f), hullDark);
            EnsurePrimitive(cockpit, "FrontDefenseLip", PrimitiveType.Cube, new Vector3(0f, -0.66f, 1.05f), new Vector3(1.62f, 0.065f, 0.12f), new Vector3(4f, 0f, 0f), hullDark);
            EnsurePrimitive(cockpit, "Canopy_Left", PrimitiveType.Cube, new Vector3(-1.33f, 0.47f, 1.9f), new Vector3(0.09f, 2.35f, 0.11f), new Vector3(0f, -4f, -24f), hull);
            EnsurePrimitive(cockpit, "Canopy_Right", PrimitiveType.Cube, new Vector3(1.33f, 0.47f, 1.9f), new Vector3(0.09f, 2.35f, 0.11f), new Vector3(0f, 4f, 24f), hull);
            EnsurePrimitive(cockpit, "Canopy_Top", PrimitiveType.Cube, new Vector3(0f, 1.52f, 2.05f), new Vector3(2.35f, 0.085f, 0.11f), Vector3.zero, hullDark);
            Transform canopyGlass = EnsurePrimitive(cockpit, "CanopyGlass", PrimitiveType.Cube, new Vector3(0f, 0.62f, 2.32f), new Vector3(2.25f, 1.55f, 0.018f), Vector3.zero, canopy);
            Renderer canopyRenderer = canopyGlass.GetComponent<Renderer>();
            if (canopyRenderer != null) canopyRenderer.enabled = false;

            EnsurePrimitive(cockpit, "DefenseRail_Left", PrimitiveType.Cube, new Vector3(-0.78f, -0.48f, 0.92f), new Vector3(0.72f, 0.022f, 0.045f), new Vector3(0f, -13f, -5f), cyan);
            EnsurePrimitive(cockpit, "DefenseRail_Right", PrimitiveType.Cube, new Vector3(0.78f, -0.48f, 0.92f), new Vector3(0.72f, 0.022f, 0.045f), new Vector3(0f, 13f, 5f), cyan);
            EnsurePrimitive(cockpit, "CommandRail", PrimitiveType.Cube, new Vector3(0f, -0.61f, 0.77f), new Vector3(0.48f, 0.018f, 0.035f), Vector3.zero, cyan);
            EnsurePrimitive(cockpit, "WarningRail_Left", PrimitiveType.Cube, new Vector3(-1.12f, -0.52f, 1.1f), new Vector3(0.22f, 0.018f, 0.04f), new Vector3(0f, -11f, -6f), orange);
            EnsurePrimitive(cockpit, "WarningRail_Right", PrimitiveType.Cube, new Vector3(1.12f, -0.52f, 1.1f), new Vector3(0.22f, 0.018f, 0.04f), new Vector3(0f, 11f, 6f), orange);

            AddPanelDetails(cockpit, hullDark, cyan);
            AddFlightDisplays(cockpit, hullDark, cyan, orange);
            Light left = EnsurePointLight(cockpit, "CockpitAccent_Left", new Vector3(-1.05f, -0.3f, 1f), Cyan, 2.1f, 3.2f);
            Light right = EnsurePointLight(cockpit, "CockpitAccent_Right", new Vector3(1.05f, -0.3f, 1f), Cyan, 2.1f, 3.2f);
            left.shadows = LightShadows.None;
            right.shadows = LightShadows.None;
            return cockpit.gameObject;
        }

        private static void AddPanelDetails(Transform cockpit, Material hullDark, Material cyan)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Transform bank = EnsurePrimitive(cockpit, side < 0 ? "TelemetryBank_Left" : "TelemetryBank_Right", PrimitiveType.Cube,
                    new Vector3(side * 1.2f, -0.64f, 1.85f), new Vector3(0.48f, 0.18f, 0.52f), new Vector3(18f, side * 14f, side * 5f), hullDark);
                for (int i = 0; i < 3; i++)
                    EnsurePrimitive(bank, "TelemetryLine_" + (i + 1), PrimitiveType.Cube,
                        new Vector3(0f, 0.54f, -0.28f + i * 0.25f), new Vector3(0.65f - i * 0.08f, 0.018f, 0.035f), Vector3.zero, cyan);
            }
        }

        private static void AddFlightDisplays(Transform cockpit, Material hullDark, Material cyan, Material orange)
        {
            Material screen = GetOrCreateEmission("DisplayBlack", new Color(0.003f, 0.016f, 0.025f), 1f);
            for (int side = -1; side <= 1; side += 2)
            {
                Transform monitor = EnsurePrimitive(cockpit, side < 0 ? "FlightDisplay_Left" : "FlightDisplay_Right", PrimitiveType.Cube,
                    new Vector3(side * 0.78f, -0.38f, 1.52f), new Vector3(0.56f, 0.29f, 0.055f), new Vector3(-10f, side * -12f, 0f), hullDark);
                EnsurePrimitive(monitor, "Screen", PrimitiveType.Cube, new Vector3(0f, 0f, -0.54f), new Vector3(0.91f, 0.82f, 0.12f), Vector3.zero, screen);
                EnsurePrimitive(monitor, "Header", PrimitiveType.Cube, new Vector3(0f, 0.35f, -0.63f), new Vector3(0.8f, 0.014f, 0.02f), Vector3.zero, cyan);
                for (int i = 0; i < 7; i++)
                {
                    EnsurePrimitive(monitor, "Telemetry_" + i, PrimitiveType.Cube,
                        new Vector3(-0.2f, 0.23f - i * 0.075f, -0.63f), new Vector3(0.29f - (i % 3) * 0.04f, 0.018f, 0.02f), Vector3.zero, cyan);
                    EnsurePrimitive(monitor, "Signal_" + i, PrimitiveType.Cube,
                        new Vector3(0.04f + i * 0.045f, -0.19f, -0.63f), new Vector3(0.021f, 0.12f + (i % 4) * 0.065f, 0.02f), Vector3.zero, i == 6 ? orange : cyan);
                }
                for (int i = 0; i < 6; i++)
                    EnsurePrimitive(cockpit, "HullRib_" + side + "_" + i, PrimitiveType.Cube,
                        new Vector3(side * (1.15f + i * 0.045f), -0.48f - i * 0.014f, 1.05f + i * 0.12f),
                        new Vector3(0.15f, 0.018f, 0.027f), new Vector3(0f, side * 12f, 0f), hullDark);
            }
            Transform center = EnsurePrimitive(cockpit, "NavigationDisplay", PrimitiveType.Cube,
                new Vector3(0f, -0.45f, 1.25f), new Vector3(0.46f, 0.29f, 0.04f), new Vector3(-14f, 0f, 0f), hullDark);
            EnsurePrimitive(center, "Screen", PrimitiveType.Cube, new Vector3(0f, 0f, -0.55f), new Vector3(0.9f, 0.82f, 0.1f), Vector3.zero, screen);
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI * 2f / 24f;
                EnsurePrimitive(center, "RadarTick_" + i, PrimitiveType.Cube,
                    new Vector3(Mathf.Cos(angle) * 0.2f, Mathf.Sin(angle) * 0.3f, -0.65f),
                    new Vector3(0.018f, 0.035f, 0.02f), new Vector3(0f, 0f, angle * Mathf.Rad2Deg - 90f), cyan);
            }
            EnsurePrimitive(center, "RadarCross_X", PrimitiveType.Cube, new Vector3(0f, 0f, -0.65f), new Vector3(0.12f, 0.012f, 0.02f), Vector3.zero, cyan);
            EnsurePrimitive(center, "RadarCross_Y", PrimitiveType.Cube, new Vector3(0f, 0f, -0.65f), new Vector3(0.009f, 0.17f, 0.02f), Vector3.zero, cyan);
        }

        private static GameObject ConfigureHangar(Material hull, Material hullDark, Material cyan, Material warning)
        {
            GameObject launch = GameObject.Find("LaunchSystem");
            if (launch == null) return null;
            Transform root = launch.transform.Find("HangarVisuals");
            if (root == null)
            {
                root = new GameObject("HangarVisuals").transform;
                root.SetParent(launch.transform, false);
            }
            Transform leftDoor = launch.transform.Find("HangarDoor_Left");
            Transform rightDoor = launch.transform.Find("HangarDoor_Right");
            ApplyMaterial(leftDoor, hullDark);
            ApplyMaterial(rightDoor, hullDark);
            EnsurePrimitive(root, "Portal_Left", PrimitiveType.Cube, new Vector3(-2.65f, 1.25f, 5.25f), new Vector3(0.26f, 3.25f, 0.4f), Vector3.zero, hull);
            EnsurePrimitive(root, "Portal_Right", PrimitiveType.Cube, new Vector3(2.65f, 1.25f, 5.25f), new Vector3(0.26f, 3.25f, 0.4f), Vector3.zero, hull);
            EnsurePrimitive(root, "Portal_Top", PrimitiveType.Cube, new Vector3(0f, 2.85f, 5.25f), new Vector3(5.55f, 0.25f, 0.4f), Vector3.zero, hull);
            EnsurePrimitive(root, "Portal_Bottom", PrimitiveType.Cube, new Vector3(0f, -0.38f, 5.25f), new Vector3(5.55f, 0.2f, 0.4f), Vector3.zero, hullDark);
            for (int i = 0; i < 5; i++)
            {
                float z = 3.7f + i * 0.46f;
                EnsurePrimitive(root, "CeilingRib_" + (i + 1), PrimitiveType.Cube, new Vector3(0f, 2.72f, z), new Vector3(5.1f - i * 0.22f, 0.07f, 0.08f), Vector3.zero, i % 2 == 0 ? cyan : hull);
            }
            for (int i = 0; i < 4; i++)
            {
                float y = 0.15f + i * 0.62f;
                EnsurePrimitive(root, "LeftWallRib_" + (i + 1), PrimitiveType.Cube, new Vector3(-2.5f, y, 5.05f), new Vector3(0.12f, 0.42f, 0.28f), new Vector3(0f, 0f, -12f), hull);
                EnsurePrimitive(root, "RightWallRib_" + (i + 1), PrimitiveType.Cube, new Vector3(2.5f, y, 5.05f), new Vector3(0.12f, 0.42f, 0.28f), new Vector3(0f, 0f, 12f), hull);
            }
            EnsurePrimitive(root, "DoorWarning_Left", PrimitiveType.Cube, new Vector3(-0.18f, 1.6f, 5.36f), new Vector3(0.025f, 2.4f, 0.025f), Vector3.zero, warning);
            EnsurePrimitive(root, "DoorWarning_Right", PrimitiveType.Cube, new Vector3(0.18f, 1.6f, 5.36f), new Vector3(0.025f, 2.4f, 0.025f), Vector3.zero, warning);
            return root.gameObject;
        }

        private static void ConfigureHud(Camera camera, Material panel, Material cyan, Material green)
        {
            GameObject root = camera.transform.Find("MissionHUD")?.gameObject;
            if (root == null) return;
            ConfigureHudPanel(root.transform.Find("Panel_HP"), new Vector3(-1.68f, 1.22f, 4.03f), new Vector3(1.35f, 0.38f, 0.025f), panel);
            ConfigureHudPanel(root.transform.Find("Panel_Remaining"), new Vector3(0f, 1.24f, 4.03f), new Vector3(1.6f, 0.35f, 0.025f), panel);
            ConfigureHudPanel(root.transform.Find("Panel_Score"), new Vector3(1.68f, 1.22f, 4.03f), new Vector3(1.4f, 0.38f, 0.025f), panel);

            TextMesh health = root.transform.Find("HudHealth")?.GetComponent<TextMesh>();
            TextMesh remaining = root.transform.Find("HudRemaining")?.GetComponent<TextMesh>();
            TextMesh score = root.transform.Find("HudScore")?.GetComponent<TextMesh>();
            TextMesh feedback = root.transform.Find("HudFeedback")?.GetComponent<TextMesh>();
            StyleHudText(health, new Vector3(-1.68f, 1.27f, 3.98f), Green, 0.032f);
            StyleHudText(remaining, new Vector3(0f, 1.24f, 3.98f), Cyan, 0.032f);
            StyleHudText(score, new Vector3(1.68f, 1.24f, 3.98f), new Color(1f, 0.72f, 0.28f), 0.032f);
            StyleHudText(feedback, new Vector3(0f, 0.18f, 3.9f), new Color(0.45f, 1f, 0.6f), 0.038f);
            // Reserve the larger font for alerts; telemetry must fit after VR minimum-size enforcement.
            if (health != null) health.fontSize = 48;
            if (remaining != null) remaining.fontSize = 48;
            if (score != null) score.fontSize = 48;

            Transform frames = EnsureChild(root.transform, "TacticalHudFrames");
            EnsureAngularFrame(frames, "HpFrame", new Vector3(-1.68f, 1.22f, 4f), new Vector2(1.44f, 0.47f), cyan);
            EnsureAngularFrame(frames, "MeteorFrame", new Vector3(0f, 1.24f, 4f), new Vector2(1.68f, 0.44f), cyan);
            EnsureAngularFrame(frames, "ScoreFrame", new Vector3(1.68f, 1.22f, 4f), new Vector2(1.48f, 0.47f), cyan);

            Renderer[] segments = new Renderer[8];
            for (int i = 0; i < segments.Length; i++)
            {
                Transform segment = EnsurePrimitive(frames, "HealthSegment_" + (i + 1), PrimitiveType.Cube,
                    new Vector3(-2.12f + i * 0.126f, 1.1f, 3.94f), new Vector3(0.105f, 0.035f, 0.018f), Vector3.zero, green);
                segments[i] = segment.GetComponent<Renderer>();
            }
            Transform feedbackPanel = EnsurePrimitive(frames, "FeedbackPanel", PrimitiveType.Cube,
                new Vector3(0f, 0.18f, 3.96f), new Vector3(1.45f, 0.42f, 0.018f), Vector3.zero, panel);
            HudVisualEffectsView effects = root.GetComponent<HudVisualEffectsView>();
            if (effects == null) effects = root.AddComponent<HudVisualEffectsView>();
            effects.Configure(root.GetComponent<MissionHudController>(), segments, feedbackPanel.GetComponent<Renderer>(), feedback);
        }

        private static void ConfigureReadyAndResults(Camera camera, Material panel, Material cyan, Material green)
        {
            GameObject readyRoot = GameObject.Find("ReadyScreen");
            if (readyRoot != null)
            {
                TextMesh title = readyRoot.transform.Find("ReadyTitle")?.GetComponent<TextMesh>();
                StyleHudText(title, new Vector3(0f, 0.42f, 3.82f), new Color(0.7f, 0.92f, 1f), 0.06f);
                GameObject button = readyRoot.transform.Find("ReadyStartButton")?.gameObject;
                if (button != null)
                {
                    button.transform.localPosition = new Vector3(0f, -0.48f, 3.95f);
                    button.transform.localScale = new Vector3(1.3f, 0.34f, 0.035f);
                    ApplyMaterial(button.transform, panel);
                }
                Transform legacyFrame = readyRoot.transform.Find("ReadyTitleFrame");
                if (legacyFrame != null) Object.DestroyImmediate(legacyFrame.gameObject);
                Transform legacyLabel = readyRoot.transform.Find("StartLabel");
                if (legacyLabel != null) Object.DestroyImmediate(legacyLabel.gameObject);
                Transform decorations = EnsureChild(readyRoot.transform, "ReadyDecorations");
                EnsureAngularFrame(decorations, "ReadyTitleFrame", new Vector3(0f, 0.36f, 3.88f), new Vector2(2.4f, 1.1f), cyan);
                TextMesh start = EnsureText(decorations, "StartLabel", new Vector3(0f, -0.48f, 3.9f), "START", new Color(0.82f, 0.97f, 1f), 0.04f);
                start.fontStyle = FontStyle.Bold;
                ReadyScreenView readyView = readyRoot.GetComponent<ReadyScreenView>();
                if (readyView != null) readyView.SetDecorationRoot(decorations.gameObject);
            }

            GameObject resultRoot = GameObject.Find("ResultSystem");
            TextMesh resultText = camera.transform.Find("ResultText")?.GetComponent<TextMesh>();
            if (resultRoot != null && resultText != null)
            {
                StyleHudText(resultText, new Vector3(0f, 0.08f, 3.86f), new Color(0.72f, 0.96f, 1f), 0.033f);
                GameObject frame = EnsureMajorPanel(camera.transform, "ResultVisualFrame", new Vector3(0f, 0.08f, 4.03f), new Vector2(2.55f, 2.25f), panel, cyan);
                ResultView view = resultRoot.GetComponent<ResultView>();
                ResultController controller = resultRoot.GetComponent<ResultController>();
                if (view != null) view.Configure(controller, resultText, frame);
            }

            GameObject completeRoot = GameObject.Find("MissionCompleteSystem");
            TextMesh completeText = camera.transform.Find("MissionCompleteText")?.GetComponent<TextMesh>();
            if (completeRoot != null && completeText != null)
            {
                StyleHudText(completeText, new Vector3(0f, 0.18f, 3.84f), Green, 0.045f);
                GameObject frame = EnsureMajorPanel(camera.transform, "MissionCompleteVisualFrame", new Vector3(0f, 0.18f, 4.02f), new Vector2(2.75f, 0.92f), panel, green);
                MissionCompleteView view = completeRoot.GetComponent<MissionCompleteView>();
                if (view != null) view.SetVisualFrame(frame);
            }
        }

        private static void ConfigureLasers(Material orange)
        {
            GameObject root = GameObject.Find("CombatSystem");
            if (root == null) return;
            foreach (string suffix in new[] { "Left", "Right" })
            {
                Transform beamTransform = root.transform.Find("LaserBeam_" + suffix);
                if (beamTransform == null) continue;
                LineRenderer core = beamTransform.GetComponent<LineRenderer>();
                if (core == null) continue;
                core.sharedMaterial = orange;
                core.widthMultiplier = 0.026f;
                core.numCapVertices = 4;
                core.startColor = new Color(1f, 0.92f, 0.7f, 1f);
                core.endColor = Orange;
                Transform glowTransform = beamTransform.Find("PlasmaGlow");
                GameObject glowObject = glowTransform != null ? glowTransform.gameObject : new GameObject("PlasmaGlow");
                glowObject.transform.SetParent(beamTransform, false);
                LineRenderer glow = glowObject.GetComponent<LineRenderer>();
                if (glow == null) glow = glowObject.AddComponent<LineRenderer>();
                glow.sharedMaterial = orange;
                glow.widthMultiplier = 0.09f;
                glow.numCapVertices = 4;
                glow.startColor = new Color(1f, 0.18f, 0.01f, 0.45f);
                glow.endColor = new Color(1f, 0.02f, 0f, 0.05f);
                glow.shadowCastingMode = ShadowCastingMode.Off;
                glow.receiveShadows = false;
                LaserBeamView view = beamTransform.GetComponent<LaserBeamView>();
                if (view != null) view.Configure(core, glow);
            }
        }

        private static void ConfigureCombatVfx(Material orange, Material cyan, Material warning)
        {
            GameObject root = GameObject.Find("CombatVFX");
            if (root == null) return;
            Transform hit = root.transform.Find("HitSpark");
            Transform explosion = root.transform.Find("MeteorExplosion");
            Transform boss = root.transform.Find("BossExplosion");
            ConfigureEffectCore(hit, cyan, new Color(0.3f, 0.9f, 1f), 3.2f, 12, 0.16f, 2.2f, 0.035f);
            ConfigureEffectCore(explosion, orange, new Color(1f, 0.14f, 0.01f), 5f, 36, 0.5f, 3.4f, 0.09f);
            ConfigureEffectCore(boss, warning, new Color(1f, 0.03f, 0f), 8f, 86, 0.95f, 5.8f, 0.14f);
            CombatVfxController controller = root.GetComponent<CombatVfxController>();
            if (controller != null)
                controller.Configure(GameObject.Find("CombatSystem")?.GetComponent<LaserWeapon>(),
                    GameObject.Find("BossClimaxSystem")?.GetComponent<BossClimaxController>(), hit, explosion, boss);
        }

        private static void ConfigureEffectCore(
            Transform root,
            Material coreMaterial,
            Color particleColor,
            float lightIntensity,
            int burstCount,
            float lifetime,
            float speed,
            float size)
        {
            if (root == null) return;
            ApplyMaterial(root, coreMaterial);
            Transform particlesTransform = root.Find("ParticleBurst");
            GameObject particleObject = particlesTransform != null ? particlesTransform.gameObject : new GameObject("ParticleBurst");
            particleObject.transform.SetParent(root, false);
            ParticleSystem particles = particleObject.GetComponent<ParticleSystem>();
            if (particles == null) particles = particleObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.55f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.55f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.45f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, particleColor);
            main.maxParticles = Mathf.Max(24, burstCount + 8);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(particleColor, 0.25f), new GradientColorKey(new Color(0.1f, 0.01f, 0f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.75f, 0.55f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            particleRenderer.lengthScale = 2.5f;
            particleRenderer.velocityScale = 0.12f;
            particleRenderer.sharedMaterial = coreMaterial;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            EnsurePointLight(root, "ImpactLight", Vector3.zero, particleColor, lightIntensity, burstCount > 40 ? 8f : 3f);
        }

        private static void ConfigureCinematicDirector(
            Camera camera,
            Transform earth,
            Transform stars,
            GameObject cockpit,
            GameObject hangar,
            Material panel,
            Material cyan,
            Material warning)
        {
            GameObject root = GameObject.Find("VisualIntegration");
            if (root == null) root = new GameObject("VisualIntegration");
            GameObject alert = EnsureMajorPanel(camera.transform, "CinematicAlert", new Vector3(0f, 0.35f, 4.02f), new Vector2(2.45f, 0.82f), panel, warning);
            TextMesh text = EnsureText(alert.transform, "AlertText", new Vector3(0f, 0f, -0.12f), string.Empty, Warning, 0.036f);
            Renderer frame = alert.transform.Find("FrameTop")?.GetComponent<Renderer>();
            Light key = GameObject.Find("Directional Light")?.GetComponent<Light>();
            Transform cockpitTransform = cockpit != null ? cockpit.transform : null;
            Light[] accentLights = cockpitTransform != null
                ? cockpitTransform.GetComponentsInChildren<Light>(true)
                : System.Array.Empty<Light>();
            GameObject launch = GameObject.Find("LaunchSystem");
            GameObject leftDoor = launch != null ? launch.transform.Find("HangarDoor_Left")?.gameObject : null;
            GameObject rightDoor = launch != null ? launch.transform.Find("HangarDoor_Right")?.gameObject : null;
            CinematicEnvironmentView view = root.GetComponent<CinematicEnvironmentView>();
            if (view == null) view = root.AddComponent<CinematicEnvironmentView>();
            view.Configure(
                earth,
                stars,
                key,
                cockpit,
                camera.transform.Find("MissionHUD")?.gameObject,
                new[] { hangar, leftDoor, rightDoor },
                accentLights,
                alert,
                text,
                frame);
        }

        private static void ConfigurePostProcessing(Camera camera)
        {
            const string profilePath = ArtRoot + "/MeteorDefenseVisualProfile.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            // Volume components must be sub-assets to survive an editor restart.
            profile.components.RemoveAll(component => component == null);
            Bloom bloom;
            if (!profile.TryGet(out bloom)) bloom = profile.Add<Bloom>(true);
            bloom.active = true;
            bloom.intensity.Override(0.72f);
            bloom.threshold.Override(0.82f);
            bloom.scatter.Override(0.62f);
            Vignette vignette;
            if (!profile.TryGet(out vignette)) vignette = profile.Add<Vignette>(true);
            vignette.active = true;
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.58f);
            ColorAdjustments color;
            if (!profile.TryGet(out color)) color = profile.Add<ColorAdjustments>(true);
            color.active = true;
            color.postExposure.Override(0.05f);
            color.contrast.Override(12f);
            color.saturation.Override(-4f);
            color.colorFilter.Override(new Color(0.93f, 0.98f, 1f));
            foreach (VolumeComponent component in profile.components)
            {
                if (!AssetDatabase.Contains(component)) AssetDatabase.AddObjectToAsset(component, profile);
                EditorUtility.SetDirty(component);
            }
            EditorUtility.SetDirty(profile);

            GameObject volumeObject = GameObject.Find("GlobalVisualVolume");
            if (volumeObject == null) volumeObject = new GameObject("GlobalVisualVolume");
            Volume volume = volumeObject.GetComponent<Volume>();
            if (volume == null) volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
        }

        private static void DisableShadows()
        {
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (light != null) light.shadows = LightShadows.None;
            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (renderer == null) continue;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static Material GetOrCreateLit(string name, Color color, float metallic, float smoothness)
        {
            string path = MaterialRoot + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetOrCreateEmission(string name, Color color, float intensity)
        {
            string path = MaterialRoot + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            Color hdrColor = color * intensity;
            hdrColor.a = color.a;
            material.color = hdrColor;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * intensity);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetOrCreateTransparent(string name, Color color, float smoothness)
        {
            Material material = GetOrCreateLit(name, color, 0.08f, smoothness);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Transform EnsurePrimitive(
            Transform parent,
            string name,
            PrimitiveType type,
            Vector3 position,
            Vector3 scale,
            Vector3 rotation,
            Material material)
        {
            Transform existing = parent.Find(name);
            GameObject primitive = existing != null ? existing.gameObject : GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = position;
            primitive.transform.localScale = scale;
            primitive.transform.localRotation = Quaternion.Euler(rotation);
            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            ApplyMaterial(primitive.transform, material);
            return primitive.transform;
        }

        private static Transform EnsureChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) return child;
            child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static void ApplyMaterial(Transform target, Material material)
        {
            Renderer renderer = target != null ? target.GetComponent<Renderer>() : null;
            if (renderer == null) return;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void ConfigureHudPanel(Transform panel, Vector3 position, Vector3 scale, Material material)
        {
            if (panel == null) return;
            panel.localPosition = position;
            panel.localScale = scale;
            panel.localRotation = Quaternion.identity;
            ApplyMaterial(panel, material);
        }

        private static void StyleHudText(TextMesh text, Vector3 position, Color color, float characterSize)
        {
            if (text == null) return;
            text.transform.localPosition = position;
            text.transform.localRotation = Quaternion.identity;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 72;
            text.characterSize = characterSize;
            text.color = color;
            text.fontStyle = FontStyle.Bold;
        }

        private static TextMesh EnsureText(Transform parent, string name, Vector3 position, string content, Color color, float size)
        {
            Transform existing = parent.Find(name);
            GameObject textObject = existing != null ? existing.gameObject : new GameObject(name);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = position;
            textObject.transform.localRotation = Quaternion.identity;
            TextMesh text = textObject.GetComponent<TextMesh>();
            if (text == null) text = textObject.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 72;
            text.characterSize = size;
            text.color = color;
            text.text = content;
            return text;
        }

        private static void EnsureAngularFrame(Transform parent, string name, Vector3 center, Vector2 size, Material material)
        {
            Transform root = EnsureChild(parent, name);
            root.localPosition = Vector3.zero;
            float halfW = size.x * 0.5f;
            float halfH = size.y * 0.5f;
            float thickness = 0.018f;
            EnsurePrimitive(root, "FrameTop", PrimitiveType.Cube, center + Vector3.up * halfH, new Vector3(size.x, thickness, 0.014f), Vector3.zero, material);
            EnsurePrimitive(root, "FrameBottom", PrimitiveType.Cube, center - Vector3.up * halfH, new Vector3(size.x, thickness, 0.014f), Vector3.zero, material);
            EnsurePrimitive(root, "FrameLeft", PrimitiveType.Cube, center - Vector3.right * halfW, new Vector3(thickness, size.y, 0.014f), Vector3.zero, material);
            EnsurePrimitive(root, "FrameRight", PrimitiveType.Cube, center + Vector3.right * halfW, new Vector3(thickness, size.y, 0.014f), Vector3.zero, material);
            EnsurePrimitive(root, "CornerCutTL", PrimitiveType.Cube, center + new Vector3(-halfW + 0.08f, halfH, -0.005f), new Vector3(0.18f, thickness * 1.5f, 0.016f), new Vector3(0f, 0f, -35f), material);
            EnsurePrimitive(root, "CornerCutBR", PrimitiveType.Cube, center + new Vector3(halfW - 0.08f, -halfH, -0.005f), new Vector3(0.18f, thickness * 1.5f, 0.016f), new Vector3(0f, 0f, -35f), material);
        }

        private static GameObject EnsureMajorPanel(Transform parent, string name, Vector3 center, Vector2 size, Material panel, Material frame)
        {
            Transform root = EnsureChild(parent, name);
            root.localPosition = Vector3.zero;
            EnsurePrimitive(root, "Panel", PrimitiveType.Cube, center, new Vector3(size.x, size.y, 0.02f), Vector3.zero, panel);
            float halfW = size.x * 0.5f;
            float halfH = size.y * 0.5f;
            EnsurePrimitive(root, "FrameTop", PrimitiveType.Cube, center + Vector3.up * halfH, new Vector3(size.x, 0.022f, 0.02f), Vector3.zero, frame);
            EnsurePrimitive(root, "FrameBottom", PrimitiveType.Cube, center - Vector3.up * halfH, new Vector3(size.x, 0.022f, 0.02f), Vector3.zero, frame);
            EnsurePrimitive(root, "FrameLeft", PrimitiveType.Cube, center - Vector3.right * halfW, new Vector3(0.022f, size.y, 0.02f), Vector3.zero, frame);
            EnsurePrimitive(root, "FrameRight", PrimitiveType.Cube, center + Vector3.right * halfW, new Vector3(0.022f, size.y, 0.02f), Vector3.zero, frame);
            return root.gameObject;
        }

        private static Light EnsurePointLight(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
        {
            Transform existing = parent.Find(name);
            GameObject lightObject = existing != null ? existing.gameObject : new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position;
            Light light = lightObject.GetComponent<Light>();
            if (light == null) light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        [MenuItem("Meteor Defense/Capture Visual Integration Frame")]
        public static void CaptureReferenceFrame()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Camera camera = Camera.main;
            if (camera == null) return;
            Ensure(camera);
            Object.FindAnyObjectByType<VrUxComfortController>()?.ApplySettings();
            // Persist only the ready-state composition, never the staged preview below.
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            CinematicEnvironmentView view = Object.FindFirstObjectByType<CinematicEnvironmentView>();
            if (view != null) view.ApplyState(GameState.Playing);
            GameObject ready = GameObject.Find("ReadyScreen");
            if (ready != null) ready.SetActive(false);
            GameObject alert = GameObject.Find("CinematicAlert");
            if (alert != null) alert.SetActive(false);
            MissionHudController previewHud = Object.FindAnyObjectByType<MissionHudController>();
            previewHud?.ResetSession();
            previewHud?.GetComponent<MissionHudView>()?.RefreshAll();

            GameObject meteorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Meteor/Prefabs/Meteor_Normal.prefab");
            GameObject previewMeteor = meteorPrefab != null ? PrefabUtility.InstantiatePrefab(meteorPrefab) as GameObject : null;
            if (previewMeteor != null)
            {
                previewMeteor.name = "VisualPreviewMeteor";
                previewMeteor.transform.position = new Vector3(0.5f, 1.2f, 7.4f);
                previewMeteor.transform.rotation = Quaternion.Euler(18f, 32f, -9f);
                previewMeteor.transform.localScale = Vector3.one * 1.4f;
            }

            const int width = 1920;
            const int height = 1080;
            RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            camera.targetTexture = target;
            RenderTexture.active = target;
            camera.Render();
            Texture2D capture = new Texture2D(width, height, TextureFormat.RGB24, false);
            capture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            capture.Apply();
            string directory = Path.GetFullPath("Docs/VisualQA");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "meteor-defense-gameview.png");
            File.WriteAllBytes(path, capture.EncodeToPNG());
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(capture);
            Object.DestroyImmediate(target);
            if (previewMeteor != null) Object.DestroyImmediate(previewMeteor);
            Debug.Log("[VisualIntegration] Capture saved: " + path);
        }
    }
}
