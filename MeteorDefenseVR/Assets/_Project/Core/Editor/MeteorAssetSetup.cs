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
        private const string MeteorTexturePath = "Assets/Art/VisualIntegration/Textures/MeteorSurface.png";

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
            GameObject root = PrefabUtility.LoadPrefabContents(bossPath);
            MeteorController controller = root.GetComponent<MeteorController>();
            Renderer[] bodyRenderers = root.GetComponentsInChildren<Renderer>(true);
            Renderer[] crackRenderers = System.Array.FindAll(bodyRenderers,
                renderer => renderer != null && renderer.GetComponentInParent<LockOnReticleView>() == null);
            Material bossMaterial = GetOrCreateBossCrackMaterial();
            foreach (Renderer bodyRenderer in crackRenderers)
                bodyRenderer.sharedMaterial = bossMaterial;

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
            view.Configure(controller, crackRenderers, crackLight, audioSource);
            PrefabUtility.SaveAsPrefabAsset(root, bossPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
        }

        private static Material GetOrCreateBossCrackMaterial()
        {
            string path = Root + "/BossCrackedMaterial.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                material = new Material(shader) { name = "BossCrackedMaterial" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = new Color(0.58f, 0.42f, 0.34f, 1f);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(MeteorTexturePath);
            if (texture != null)
            {
                material.mainTexture = texture;
                material.SetTexture("_EmissionMap", texture);
            }
            material.SetFloat("_Smoothness", 0.08f);
            material.SetFloat("_Metallic", 0.05f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(1.5f, 0.12f, 0.015f));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetOrCreateMeteorSurfaceMaterial()
        {
            string path = Root + "/MeteorSurfaceMaterial.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                material = new Material(shader) { name = "MeteorSurfaceMaterial" };
                AssetDatabase.CreateAsset(material, path);
            }
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(MeteorTexturePath);
            material.color = Color.white;
            if (texture != null)
                material.mainTexture = texture;
            material.SetTexture("_EmissionMap", null);
            material.SetFloat("_Smoothness", 0.06f);
            material.SetFloat("_Metallic", 0.02f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(0.2f, 0.085f, 0.032f));
            EditorUtility.SetDirty(material);
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

            Material meteorMaterial = GetOrCreateMeteorSurfaceMaterial();
            Renderer rootRenderer = root.GetComponent<Renderer>();
            if (rootRenderer != null) rootRenderer.sharedMaterial = meteorMaterial;
            root.GetComponent<MeshFilter>().sharedMesh = GetOrCreateRockMesh();
            EnsureRockLobe(root.transform, "RockLobe_A", new Vector3(-0.32f, 0.18f, 0.08f), new Vector3(0.78f, 0.58f, 0.7f), new Vector3(18f, 26f, -12f), meteorMaterial);
            EnsureRockLobe(root.transform, "RockLobe_B", new Vector3(0.3f, -0.18f, 0.05f), new Vector3(0.62f, 0.72f, 0.55f), new Vector3(-22f, 8f, 31f), meteorMaterial);
            EnsureRockLobe(root.transform, "RockLobe_C", new Vector3(0.05f, 0.3f, -0.12f), new Vector3(0.48f, 0.42f, 0.62f), new Vector3(35f, -19f, 8f), meteorMaterial);

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

            LineRenderer outerRing = EnsureRing(reticle.transform, "OuterTacticalRing", 0.9f, 64, 0.012f);
            Renderer[] brackets = new Renderer[5];
            brackets[0] = outerRing;
            Vector3[] bracketPositions =
            {
                new Vector3(0f, 0.98f, 0f),
                new Vector3(0f, -0.98f, 0f),
                new Vector3(-0.98f, 0f, 0f),
                new Vector3(0.98f, 0f, 0f)
            };
            Vector3[] bracketScales =
            {
                new Vector3(0.18f, 0.025f, 0.015f),
                new Vector3(0.18f, 0.025f, 0.015f),
                new Vector3(0.025f, 0.18f, 0.015f),
                new Vector3(0.025f, 0.18f, 0.015f)
            };
            for (int i = 0; i < 4; i++)
                brackets[i + 1] = EnsureReticleBracket(reticle.transform, "TacticalBracket_" + (i + 1), bracketPositions[i], bracketScales[i]);

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
            view.Configure(lockTarget, ring, text, brackets);

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

        private static void EnsureRockLobe(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Vector3 rotation,
            Material material)
        {
            Transform existing = parent.Find(name);
            GameObject lobe = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lobe.name = name;
            lobe.transform.SetParent(parent, false);
            lobe.transform.localPosition = position;
            lobe.transform.localScale = scale;
            lobe.transform.localRotation = Quaternion.Euler(rotation);
            Collider collider = lobe.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            Renderer renderer = lobe.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            lobe.GetComponent<MeshFilter>().sharedMesh = GetOrCreateRockMesh();
        }

        private static Mesh GetOrCreateRockMesh()
        {
            const string path = Root + "/MeteorRockMesh.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;

            GameObject sourceObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh source = sourceObject.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] sourceVertices = source.vertices;
            Vector2[] sourceUv = source.uv;
            int[] triangles = source.triangles;
            Vector3[] vertices = new Vector3[triangles.Length];
            Vector2[] uv = new Vector2[triangles.Length];
            int[] indices = new int[triangles.Length];
            for (int i = 0; i < triangles.Length; i++)
            {
                int index = triangles[i];
                Vector3 p = sourceVertices[index].normalized;
                float relief = 0.86f + 0.10f * Mathf.Sin(p.x * 7f + p.y * 3f) * Mathf.Cos(p.z * 9f)
                    + 0.055f * Mathf.Sin(p.y * 19f - p.z * 5f) * Mathf.Cos(p.x * 13f);
                vertices[i] = sourceVertices[index] * relief;
                uv[i] = sourceUv[index];
                indices[i] = i;
            }
            mesh = new Mesh { name = "MeteorRockMesh", vertices = vertices, uv = uv, triangles = indices };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            Object.DestroyImmediate(sourceObject);
            return mesh;
        }

        private static LineRenderer EnsureRing(Transform parent, string name, float radius, int segments, float width)
        {
            Transform existing = parent.Find(name);
            GameObject ringObject = existing != null ? existing.gameObject : new GameObject(name);
            ringObject.transform.SetParent(parent, false);
            LineRenderer line = ringObject.GetComponent<LineRenderer>();
            if (line == null) line = ringObject.AddComponent<LineRenderer>();
            line.sharedMaterial = GetOrCreateReticleMaterial();
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = width;
            line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                float modulation = i % 8 < 5 ? 1f : 0.992f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius * modulation);
            }
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private static Renderer EnsureReticleBracket(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            Transform existing = parent.Find(name);
            GameObject bracket = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
            bracket.name = name;
            bracket.transform.SetParent(parent, false);
            bracket.transform.localPosition = position;
            bracket.transform.localScale = scale;
            Collider collider = bracket.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            Renderer renderer = bracket.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = GetOrCreateReticleMaterial();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return renderer;
        }

        private static Material GetOrCreateReticleMaterial()
        {
            string path = Root + "/LockOnReticleMaterial.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                material = new Material(shader) { name = "LockOnReticleMaterial" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = new Color(0.08f, 0.9f, 1f, 1f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(0.08f, 1.2f, 1.6f));
            EditorUtility.SetDirty(material);
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
