using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.Launch;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.PcInput;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MeteorDefenseVR.Editor
{
    // Explicit targeted authoring only. Never regenerates the gameplay scene or changes BGM metas.
    public static class GameFeelSetup
    {
        public static void ApplyAndBake() { Apply(); PcTestSetup.BakePresentationFont(); }
        public static void Apply()
        {
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var boot = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Bootstrap.unity", OpenSceneMode.Single);
                var loader = Object.FindAnyObjectByType<BootstrapSceneLoader>();
                var view = loader.GetComponent<SystemBootView>();
                if (view == null) view = loader.gameObject.AddComponent<SystemBootView>();
                view.Configure(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/PcInput/Fonts/PcTestKorean.asset"),loader);
                EditorSceneManager.MarkSceneDirty(boot); EditorSceneManager.SaveScene(boot);
                var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity", OpenSceneMode.Single);
                Object.FindAnyObjectByType<LaunchSequenceController>().SetDurations(.15f,.2f,.2f,.45f,.15f,.1f,.5f);
                // Fit the existing Ready panel above the newly visible cockpit instruments.
                Object.FindAnyObjectByType<ReadyScreenController>().transform.localPosition = new Vector3(0,.85f,0);
                PrepareLegacyTextDataChannels();
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                Wave(1,"WAVE 01 / APPROACH",1f, Spawn(MeteorType.Normal,5,2.6f,.5f,1.8f,1.2f,22,15));
                Wave(2,"WAVE 02 / CROSS TRAFFIC",1.2f,
                    Spawn(MeteorType.Normal,2,2.2f,.5f,2.4f,1,32,22),
                    Spawn(MeteorType.Fast,1,2,2.2f,3.8f,.85f,35,24),
                    Spawn(MeteorType.Normal,2,2.2f,2,2.5f,1,32,22),
                    Spawn(MeteorType.Fast,2,2,2.2f,3.8f,.85f,35,24));
                Wave(3,"WAVE 03 / FINAL SURGE",1.3f,
                    Spawn(MeteorType.Normal,1,2,.4f,2.6f,1,35,25),
                    Spawn(MeteorType.Fast,2,1.8f,1.8f,4,.8f,35,25),
                    Spawn(MeteorType.Large,1,2,1.8f,1.6f,1.6f,28,20),
                    Spawn(MeteorType.Normal,2,2,1.8f,2.6f,1,35,25),
                    Spawn(MeteorType.Fast,2,1.8f,1.8f,4,.8f,35,25),
                    Spawn(MeteorType.Large,1,2,2,1.6f,1.6f,28,20));
                PlayerSettings.SplashScreen.showUnityLogo = false;
                PlayerSettings.SplashScreen.show = false;
                FinalAudioSetup.Ensure(); AssetDatabase.SaveAssets();
                Debug.Log("[GameFeelSetup] Boot UI, 2.65s launch and 21-meteor three-wave progression authored.");
            }
            finally
            {
                // A fresh batch editor has an empty, unsaved scene setup that cannot be restored.
                if (System.Array.Exists(previous, item => item.isLoaded && item.isActive))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }
        private static void PrepareLegacyTextDataChannels()
        {
            const string path = "Assets/_Project/PcInput/Fonts/HiddenTextData.fontsettings";
            var emptyFont = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (emptyFont == null)
            {
                emptyFont = new Font { name = "HiddenTextData", characterInfo = System.Array.Empty<CharacterInfo>() };
                AssetDatabase.CreateAsset(emptyFont, path);
            }
            if (emptyFont.dynamic) throw new System.InvalidOperationException("Data-only font must be static.");
            foreach (var bridge in Object.FindObjectsByType<WorldTextPresentation>(FindObjectsInactive.Include))
            {
                var source = bridge.GetComponent<TextMesh>();
                if (source == null) continue;
                using var serialized = new SerializedObject(bridge);
                if (source.font != null && source.font != emptyFont)
                    serialized.FindProperty("legacyFont").objectReferenceValue = source.font;
                if (source.font != emptyFont || source.fontSize != 0 || source.fontStyle != FontStyle.Normal)
                {
                    serialized.FindProperty("legacyFontSize").intValue = source.fontSize;
                    serialized.FindProperty("legacyFontStyle").intValue = (int)source.fontStyle;
                }
                serialized.FindProperty("dataOnlyFont").objectReferenceValue = emptyFont;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                source.fontSize = 0; source.fontStyle = FontStyle.Normal;
                source.font = emptyFont;
                EditorUtility.SetDirty(source);
            }
        }
        private static MeteorSpawnData Spawn(MeteorType type,int count,float interval,float delay,float speed,float size,float horizontal,float vertical)
        {
            string name = type == MeteorType.Fast ? "Fast" : type == MeteorType.Large ? "Large" : "Normal";
            var data = new MeteorSpawnData();
            data.Configure(type,AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Meteor/Prefabs/Meteor_"+name+".prefab"),count,interval,delay,speed,type==MeteorType.Large?2:1,type==MeteorType.Large?20:10,type==MeteorType.Large?300:type==MeteorType.Fast?150:100,size,new Vector2(horizontal,vertical));
            return data;
        }
        private static void Wave(int index,string label,float delay,params MeteorSpawnData[] entries)
        {
            var data = AssetDatabase.LoadAssetAtPath<MeteorWaveData>("Assets/_Project/Meteor/Waves/Wave_0"+index+".asset");
            data.Configure(label,delay,entries); EditorUtility.SetDirty(data);
        }
    }
}
