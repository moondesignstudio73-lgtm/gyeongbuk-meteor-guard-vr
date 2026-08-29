using MeteorDefenseVR.Education;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace MeteorDefenseVR.Editor
{
    public static class EducationSetup
    {
        public static void Ensure()
        {
            const string folder="Assets/_Project/PcInput/Education/Resources";
            if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/_Project/PcInput/Education","Resources");
            string path=folder+"/EducationSettings.asset";
            var settings=AssetDatabase.LoadAssetAtPath<EducationSettings>(path);
            if(settings==null){settings=ScriptableObject.CreateInstance<EducationSettings>();AssetDatabase.CreateAsset(settings,path);}
            settings.interfaceFont=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/PcInput/Fonts/PcTestKorean.asset");
            const string fontPath="Assets/_Project/PcInput/Education/Resources/ParticipantFont.asset";
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            if(font==null)
            {
                var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/PcInput/Fonts/NotoSansKR.ttf");
                font=TMP_FontAsset.CreateFontAsset(source,36,4,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,false);
                font.name="EducationParticipantFont";AssetDatabase.CreateAsset(font,fontPath);
                if(!AssetDatabase.Contains(font.material))AssetDatabase.AddObjectToAsset(font.material,font);
                foreach(var texture in font.atlasTextures)if(texture!=null&&!AssetDatabase.Contains(texture))AssetDatabase.AddObjectToAsset(texture,font);
            }
            font.isMultiAtlasTexturesEnabled=false;
            using(var serialized=new SerializedObject(font)){serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue=true;serialized.ApplyModifiedPropertiesWithoutUndo();}
            settings.participantFont=font;EditorUtility.SetDirty(settings);EditorUtility.SetDirty(font);AssetDatabase.SaveAssets();
            PcTestSetup.BakePresentationFont();
        }
    }
}
