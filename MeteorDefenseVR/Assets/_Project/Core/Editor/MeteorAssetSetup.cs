using System.IO;
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

            CreateVariant(basePrefab, normal, "Meteor_Normal");
            CreateVariant(basePrefab, fast, "Meteor_Fast");
            CreateVariant(basePrefab, large, "Meteor_Large");
            CreateVariant(basePrefab, boss, "Meteor_Boss");
            CreateVariant(basePrefab, tutorial, "Meteor_Tutorial");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MeteorAssetSetup] Placeholder definitions and prefab variants are ready.");
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
