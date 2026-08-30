using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Tutorial;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace MeteorDefenseVR.Editor
{
    public static class TutorialSafeZoneSetup
    {
        private const string ScenePath="Assets/_Project/Scenes/MeteorDefense.unity";
        private const string FontPath="Assets/_Project/PcInput/Fonts/PcTestKorean.asset";
        private const string UiOverlayMaterialPath="Assets/_Project/UI/Materials/GameplayHudAlwaysOnTop.mat";
        private const string FontOverlayMaterialPath="Assets/_Project/UI/Materials/GameplayHudFontAlwaysOnTop.mat";

        [MenuItem("Meteor Defense/Apply Tutorial HUD Overlay")]
        public static void ApplyAndBake()
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath,UnityEditor.SceneManagement.OpenSceneMode.Single);
            Ensure(Camera.main,AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath));
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        public static void Ensure(Camera camera,TMP_FontAsset font)
        {
            if(camera==null||font==null)return;
            var controller=Object.FindAnyObjectByType<TutorialController>(FindObjectsInactive.Include);
            var legacy=Object.FindAnyObjectByType<TutorialStatusView>(FindObjectsInactive.Include);
            if(controller==null||legacy==null)return;
            int layer=LayerMask.NameToLayer("Player HUD");if(layer<0)throw new System.InvalidOperationException("Player HUD layer is required");

            var existingView=Object.FindAnyObjectByType<TutorialSafeZoneView>(FindObjectsInactive.Include);
            RectTransform root=existingView!=null?(RectTransform)existingView.transform:Rect(null,"TutorialSafeZoneCanvas");
            root.SetParent(null,false);root.gameObject.layer=layer;
            Canvas canvas=Get<Canvas>(root.gameObject);canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.45f;canvas.overrideSorting=true;canvas.sortingOrder=230;
            var scaler=Get<UnityEngine.UI.CanvasScaler>(root.gameObject);scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;scaler.matchWidthOrHeight=.5f;
            Get<UnityEngine.UI.GraphicRaycaster>(root.gameObject).enabled=false;
            CanvasGroup group=Get<CanvasGroup>(root.gameObject);group.alpha=0;group.interactable=false;group.blocksRaycasts=false;

            RectTransform panel=Rect(root,"MainTutorialPanel");panel.anchorMin=new Vector2(.19f,.57f);panel.anchorMax=new Vector2(.81f,.80f);panel.pivot=Vector2.one*.5f;
            panel.offsetMin=panel.offsetMax=Vector2.zero;panel.gameObject.layer=layer;
            Material imageMaterial=AssetDatabase.LoadAssetAtPath<Material>(UiOverlayMaterialPath);
            Material fontMaterial=AssetDatabase.LoadAssetAtPath<Material>(FontOverlayMaterialPath);
            if(imageMaterial==null||fontMaterial==null)throw new System.InvalidOperationException("Gameplay HUD always-on-top materials are required");
            var background=Get<UnityEngine.UI.Image>(panel.gameObject);background.color=new Color(.004f,.022f,.034f,.94f);background.material=imageMaterial;background.raycastTarget=false;
            var outline=Get<UnityEngine.UI.Outline>(panel.gameObject);outline.effectColor=new Color(.04f,.9f,.96f,.82f);outline.effectDistance=new Vector2(2,-2);outline.useGraphicAlpha=true;

            TextMeshProUGUI instruction=Text(panel,"TutorialTitle",font,layer);Anchor(instruction.rectTransform,new Vector2(.04f,.08f),new Vector2(.96f,.94f));
            instruction.fontSharedMaterial=fontMaterial;instruction.fontSize=48;instruction.enableAutoSizing=false;instruction.fontStyle=FontStyles.Normal;instruction.characterSpacing=0;
            instruction.color=new Color(.78f,.98f,1f,1);instruction.alignment=TextAlignmentOptions.Center;
            instruction.textWrappingMode=TextWrappingModes.Normal;instruction.overflowMode=TextOverflowModes.Truncate;instruction.richText=true;
            instruction.canvasRenderer.cullTransparentMesh=false;
            Transform obsoleteBody=panel.Find("TutorialInstruction");if(obsoleteBody!=null)Object.DestroyImmediate(obsoleteBody.gameObject);

            foreach(Transform item in root.GetComponentsInChildren<Transform>(true))item.gameObject.layer=layer;
            var legacyRenderer=legacy.GetComponent<MeshRenderer>();if(legacyRenderer!=null)legacyRenderer.enabled=false;
            var view=Get<TutorialSafeZoneView>(root.gameObject);view.Configure(controller,camera,canvas,group,panel,instruction);
            EditorUtility.SetDirty(view);EditorUtility.SetDirty(canvas);
        }

        private static T Get<T>(GameObject target) where T:Component {var value=target.GetComponent<T>();return value!=null?value:target.AddComponent<T>();}
        private static RectTransform Rect(Transform parent,string name)
        {
            Transform existing=parent!=null?parent.Find(name):null;if(existing!=null)return (RectTransform)existing;
            var root=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();root.SetParent(parent,false);return root;
        }
        private static TextMeshProUGUI Text(Transform parent,string name,TMP_FontAsset font,int layer)
        {
            RectTransform rect=Rect(parent,name);rect.gameObject.layer=layer;rect.gameObject.SetActive(false);
            var text=Get<TextMeshProUGUI>(rect.gameObject);text.font=font;text.raycastTarget=false;text.margin=new Vector4(14,8,14,8);rect.gameObject.SetActive(true);return text;
        }
        private static void Anchor(RectTransform rect,Vector2 min,Vector2 max){rect.anchorMin=min;rect.anchorMax=max;rect.pivot=Vector2.one*.5f;rect.offsetMin=rect.offsetMax=Vector2.zero;}
    }
}
