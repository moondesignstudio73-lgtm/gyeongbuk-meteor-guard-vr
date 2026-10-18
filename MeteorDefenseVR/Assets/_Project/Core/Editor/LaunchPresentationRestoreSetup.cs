using System;
using MeteorDefenseVR.Launch;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MeteorDefenseVR.Editor
{
    public static class LaunchPresentationRestoreSetup
    {
        [MenuItem("Meteor Defense/Restore Launch Presentation")]
        public static void Apply()
        {
            const string path = "Assets/_Project/Scenes/MeteorDefense.unity";
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var launch = UnityEngine.Object.FindAnyObjectByType<LaunchSequenceController>(FindObjectsInactive.Include);
                if (launch == null) throw new InvalidOperationException("LaunchSequenceController is missing from MeteorDefense scene.");
                launch.SetDurations(.35f, .7f, .75f, .45f, 2f, .65f, .7f);
                EditorUtility.SetDirty(launch);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("[LaunchPresentation] Restored comfort acceleration without moving the player camera.");
            }
            finally
            {
                if (Array.Exists(previous, item => item.isLoaded && item.isActive))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }
    }
}
