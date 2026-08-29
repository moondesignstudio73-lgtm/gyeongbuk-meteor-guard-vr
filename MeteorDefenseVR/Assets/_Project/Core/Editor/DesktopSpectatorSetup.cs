using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MeteorDefenseVR.Editor
{
    public static class DesktopSpectatorSetup
    {
        // Targeted additive wiring; never regenerates the authored scene or changes XR/Audio imports.
        public static void ApplyAndBake()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
            var pc = Object.FindAnyObjectByType<ExperienceConfiguration>();
            if (pc == null || Camera.main == null) throw new System.InvalidOperationException("Authored PC system/camera missing");
            var output = pc.GetComponent<DesktopSpectatorController>();
            if (output == null) output = pc.gameObject.AddComponent<DesktopSpectatorController>();
            var hud = Object.FindAnyObjectByType<MissionHudController>(FindObjectsInactive.Include);
            int hudLayer = EnsurePresentationLayer();
            if (hud != null)
                foreach (Transform child in hud.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = hudLayer;
            output.Configure(Camera.main, hud,
                Object.FindAnyObjectByType<GazeRaycaster>(), pc,
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/PcInput/Fonts/PcTestKorean.asset"), 1 << hudLayer);
            int turretLayer=EnsureNamedLayer(MeteorDefenseVR.Combat.TurretAimingSystem.PlayerHiddenLayerName);
            output.IncludeLayerForSpectator(turretLayer);
            Camera.main.cullingMask&=~(1<<turretLayer);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            PcTestSetup.BakePresentationFont();
        }
        private static int EnsureNamedLayer(string name)
        {
            int existing=LayerMask.NameToLayer(name);if(existing>=0)return existing;
            var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);var layers=tags.FindProperty("layers");
            for(int index=8;index<layers.arraySize;index++)if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(index).stringValue))
            {layers.GetArrayElementAtIndex(index).stringValue=name;tags.ApplyModifiedPropertiesWithoutUndo();return index;}
            throw new System.InvalidOperationException("No unused layer available for "+name);
        }
        private static int EnsurePresentationLayer()
        {
            const string name = "Player HUD";
            int existing = LayerMask.NameToLayer(name); if (existing >= 0) return existing;
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("layers");
            var objects = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int index = 8; index < layers.arraySize; index++)
            {
                if (!string.IsNullOrEmpty(layers.GetArrayElementAtIndex(index).stringValue)) continue;
                bool used = false; foreach (var item in objects) if (item.gameObject.layer == index) { used = true; break; }
                if (used) continue;
                layers.GetArrayElementAtIndex(index).stringValue = name; tags.ApplyModifiedPropertiesWithoutUndo(); return index;
            }
            throw new System.InvalidOperationException("No unused layer available for player-only HUD");
        }
    }
}
