using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Launch;
using MeteorDefenseVR.PcInput;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MeteorDefenseVR.Editor
{
    public static class HangarLobbySetup
    {
        public static void ApplyAndBake()
        {
            var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity",OpenSceneMode.Single);
            var ready=Object.FindAnyObjectByType<ReadyScreenController>();
            var launch=Object.FindAnyObjectByType<LaunchSequenceController>().transform;
            var lobby=ready.GetComponent<HangarLobbyController>();
            if(lobby==null) lobby=ready.gameObject.AddComponent<HangarLobbyController>();
            if(ready.GetComponent<LocalRankingController>()==null) ready.gameObject.AddComponent<LocalRankingController>();
            lobby.Configure(launch.Find("HangarVisuals"),launch.Find("HangarDoor_Left"),launch.Find("HangarDoor_Right"),
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/PcInput/Fonts/PcTestKorean.asset"));
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            PcTestSetup.BakePresentationFont();
        }
    }
}
