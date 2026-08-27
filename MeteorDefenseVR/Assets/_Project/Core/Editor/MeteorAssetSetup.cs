using System.IO;
using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Meteor;
using UnityEditor;
using UnityEngine;

namespace MeteorDefenseVR.Editor
{
    public static class MeteorAssetSetup
    {
        private const string Root = "Assets/_Project/Meteor";
        private const string DefinitionDirectory = Root + "/Definitions";
        private const string PrefabDirectory = Root + "/Prefabs";
        private const string WaveDirectory = Root + "/Waves";

        [MenuItem("Meteor Defense/Create Meteor Placeholder Assets")]
        public static void CreateMeteorAssets()
        {
            Directory.CreateDirectory(DefinitionDirectory);
            Directory.CreateDirectory(PrefabDirectory);

            MeteorDefinition normal = CreateDefinition("Normal", MeteorType.Normal, 1f, 2.2f, 10, 100, 1f);
            MeteorDefinition fast = CreateDefinition("Fast", MeteorType.Fast, 1f, 3.8f, 10, 150, 0.75f);
            MeteorDefinition large = CreateDefinition("Large", MeteorType.Large, 2f, 1.5f, 20, 300, 1.6f);
            MeteorDefinition boss = CreateDefinition("Boss", MeteorType.Boss, 3f, 0.8f, 30, 500, 3f);
            MeteorDefinition tutorial = CreateDefinition("Tutorial", MeteorType.Tutorial, 1f, 0f, 0, 0, 1.2f);

            string basePath = PrefabDirectory + "/MeteorBase.prefab";
            GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
            if (basePrefab == null)
            {
                GameObject source = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                source.name = "MeteorBase";
                source.AddComponent<MeteorController>();
                Rigidbody body = source.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                basePrefab = PrefabUtility.SaveAsPrefabAsset(source, basePath);
                Object.DestroyImmediate(source);
            }

            UpdateBasePrefab(basePath);
            basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);

            CreateVariant(basePrefab, normal, "Meteor_Normal");
            CreateVariant(basePrefab, fast, "Meteor_Fast");
            CreateVariant(basePrefab, large, "Meteor_Large");
            CreateVariant(basePrefab, boss, "Meteor_Boss");
            CreateVariant(basePrefab, tutorial, "Meteor_Tutorial");
            UpdateBossAssets(true);
            CreateWaveAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MeteorAssetSetup] Placeholder definitions and prefab variants are ready.");
        }

        public static void UpdateBossAssets(bool force = false)
        {
            string bossPath = PrefabDirectory + "/Meteor_Boss.prefab";
            GameObject bossAsset = AssetDatabase.LoadAssetAtPath<GameObject>(bossPath);
            if (bossAsset == null) return;
            if (!force && bossAsset.GetComponent<BossMeteorView>() != null &&
                AssetDatabase.LoadAssetAtPath<Material>(Root + "/BossCrackedMaterial.mat") != null) return;
            GameObject root = PrefabUtility.LoadPrefabContents(bossPath);
            MeteorController controller = root.GetComponent<MeteorController>();
            Renderer bodyRenderer = root.GetComponent<Renderer>();
            if (bodyRenderer != null) bodyRenderer.sharedMaterial = GetOrCreateBossCrackMaterial();

            Transform lightTransform = root.transform.Find("CrackEmissionLight");
            GameObject lightObject = lightTransform != null ? lightTransform.gameObject : new GameObject("CrackEmissionLight");
            lightObject.transform.SetParent(root.transform, false);
            Light crackLight = lightObject.GetComponent<Light>();
            if (crackLight == null) crackLight = lightObject.AddComponent<Light>();
            crackLight.type = LightType.Point;
            crackLight.color = new Color(1f, 0.04f, 0.01f);
            crackLight.range = 5f;
            crackLight.intensity = 0.35f;

            AudioSource audioSource = root.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = root.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            BossMeteorView view = root.GetComponent<BossMeteorView>();
            if (view == null) view = root.AddComponent<BossMeteorView>();
            view.Configure(controller, bodyRenderer != null ? new[] { bodyRenderer } : System.Array.Empty<Renderer>(), crackLight, audioSource);
            PrefabUtility.SaveAsPrefabAsset(root, bossPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
        }

        private static Material GetOrCreateBossCrackMaterial()
        {
            string path = Root + "/BossCrackedMaterial.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            material = new Material(shader) { name = "BossCrackedMaterial" };
            material.color = new Color(0.09f, 0.035f, 0.025f, 1f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(0.8f, 0.015f, 0.002f));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static void CreateWaveAssets()
        {
            Directory.CreateDirectory(WaveDirectory);
            GameObject normal = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDirectory + "/Meteor_Normal.prefab");
            GameObject fast = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDirectory + "/Meteor_Fast.prefab");
            GameObject large = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDirectory + "/Meteor_Large.prefab");
            if (normal == null || fast == null || large == null) return;

            CreateWave("Wave_01", "WAVE 1", 1f,
                CreateSpawn(MeteorType.Normal, normal, 5, 2.5f, 0.5f, 2.2f, 1f, 10, 100, 1f));
            CreateWave("Wave_02", "WAVE 2", 1.5f,
                CreateSpawn(MeteorType.Normal, normal, 4, 2.2f, 0.5f, 2.4f, 1f, 10, 100, 1f),
                CreateSpawn(MeteorType.Fast, fast, 3, 2f, 0.8f, 3.8f, 1f, 10, 150, 0.75f));
            CreateWave("Wave_03", "WAVE 3", 1.5f,
                CreateSpawn(MeteorType.Normal, normal, 4, 2f, 0.4f, 2.6f, 1f, 10, 100, 1f),
                CreateSpawn(MeteorType.Fast, fast, 3, 1.8f, 0.6f, 4f, 1f, 10, 150, 0.75f),
                CreateSpawn(MeteorType.Large, large, 2, 3f, 1f, 1.6f, 2f, 20, 300, 1.6f));
            AssetDatabase.SaveAssets();
        }

        private static MeteorSpawnData CreateSpawn(
            MeteorType type,
            GameObject prefab,
            int count,
            float interval,
            float delay,
            float speed,
            float health,
            int damage,
            int score,
            float size)
        {
            var data = new MeteorSpawnData();
            data.Configure(type, prefab, count, interval, delay, speed, health, damage, score, size, new Vector2(35f, 25f));
            return data;
        }

        private static void CreateWave(string assetName, string displayName, float delay, params MeteorSpawnData[] entries)
        {
            string path = WaveDirectory + "/" + assetName + ".asset";
            MeteorWaveData wave = AssetDatabase.LoadAssetAtPath<MeteorWaveData>(path);
            if (wave == null)
            {
                wave = ScriptableObject.CreateInstance<MeteorWaveData>();
                AssetDatabase.CreateAsset(wave, path);
            }
            wave.Configure(displayName, delay, entries);
            EditorUtility.SetDirty(wave);
        }

        private static void UpdateBasePrefab(string basePath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(basePath);
            MeteorController meteor = root.GetComponent<MeteorController>();
            if (meteor == null) meteor = root.AddComponent<MeteorController>();
            GazeLockOnTarget lockTarget = root.GetComponent<GazeLockOnTarget>();
            if (lockTarget == null) lockTarget = root.AddComponent<GazeLockOnTarget>();
            lockTarget.Configure(meteor, 0.62f, 1.5f, 0.18f);
            lockTarget.ConfigureComfortTolerance(1.25f);

            Transform existing = root.transform.Find("LockOnReticle");
            GameObject reticle = existing != null ? existing.gameObject : new GameObject("LockOnReticle");
            reticle.transform.SetParent(root.transform);
            reticle.transform.localPosition = new Vector3(0f, 0f, -0.65f);
            reticle.transform.localRotation = Quaternion.identity;
            reticle.transform.localScale = Vector3.one;

            LineRenderer ring = reticle.GetComponent<LineRenderer>();
            if (ring == null) ring = reticle.AddComponent<LineRenderer>();
            ring.sharedMaterial = GetOrCreateReticleMaterial();
            ring.widthMultiplier = 0.035f;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;

            Transform textTransform = reticle.transform.Find("StatusText");
            GameObject textObject = textTransform != null ? textTransform.gameObject : new GameObject("StatusText");
            textObject.transform.SetParent(reticle.transform);
            textObject.transform.localPosition = new Vector3(0.85f, 0f, 0f);
            textObject.transform.localRotation = Quaternion.identity;
            TextMesh text = textObject.GetComponent<TextMesh>();
            if (text == null) text = textObject.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleLeft;
            text.fontSize = 40;
            text.characterSize = 0.035f;

            LockOnReticleView view = reticle.GetComponent<LockOnReticleView>();
            if (view == null) view = reticle.AddComponent<LockOnReticleView>();
            view.Configure(lockTarget, ring, text);

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer targetRenderer in renderers)
            {
                if (targetRenderer == null) continue;
                targetRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                targetRenderer.receiveShadows = false;
            }

            PrefabUtility.SaveAsPrefabAsset(root, basePath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static Material GetOrCreateReticleMaterial()
        {
            string path = Root + "/LockOnReticleMaterial.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            material = new Material(shader) { name = "LockOnReticleMaterial" };
            material.color = new Color(0.1f, 0.95f, 1f, 1f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static MeteorDefinition CreateDefinition(string name, MeteorType type, float health, float speed, int damage, int score, float size)
        {
            string path = DefinitionDirectory + "/Meteor_" + name + ".asset";
            MeteorDefinition definition = AssetDatabase.LoadAssetAtPath<MeteorDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<MeteorDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }
            definition.Configure(type, health, speed, damage, score, size);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void CreateVariant(GameObject basePrefab, MeteorDefinition definition, string name)
        {
            string path = PrefabDirectory + "/" + name + ".prefab";
            GameObject instance = PrefabUtility.InstantiatePrefab(basePrefab) as GameObject;
            if (instance == null) return;
            instance.name = name;
            MeteorController controller = instance.GetComponent<MeteorController>();
            controller.SetDefinition(definition);
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
        }
    }
}
