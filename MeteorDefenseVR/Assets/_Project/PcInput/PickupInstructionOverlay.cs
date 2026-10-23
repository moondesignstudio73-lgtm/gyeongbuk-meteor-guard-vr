using System.Collections.Generic;
using MeteorDefenseVR.PowerUps;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace MeteorDefenseVR.PcInput {
 // Presentation only: colliders, positions, dwell and collection remain on PowerUpItem.
 [DefaultExecutionOrder(1450),DisallowMultipleComponent]
 public sealed class PickupInstructionOverlay:MonoBehaviour {
  private sealed class Entry { public PowerUpItem item; public TextMesh source; public MeshRenderer legacy; public RectTransform panel; public TextMeshProUGUI text; }
  private readonly List<Entry> entries=new List<Entry>();
  private Camera view;private RectTransform root;private Canvas canvas;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
  private static void Install(){SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}
  private static void Loaded(Scene scene,LoadSceneMode mode){var camera=Camera.main;if(camera!=null&&camera.GetComponent<PickupInstructionOverlay>()==null)camera.gameObject.AddComponent<PickupInstructionOverlay>();}
  private void LateUpdate(){
   if(root==null){Initialize();if(root==null)return;}
   bool stereo=view.stereoEnabled;canvas.renderMode=stereo?RenderMode.WorldSpace:RenderMode.ScreenSpaceCamera;
   if(stereo){root.sizeDelta=new Vector2(720*view.aspect,720);root.localPosition=Vector3.forward*2.6f;root.localRotation=Quaternion.identity;root.localScale=Vector3.one*(2*2.6f*Mathf.Tan(view.fieldOfView*.5f*Mathf.Deg2Rad)/720);}
   foreach(var e in entries){
    bool available=e.item!=null&&e.item.IsAvailable;Vector3 p=available?view.WorldToViewportPoint(e.item.transform.position):Vector3.back;
    bool visible=available&&p.z>view.nearClipPlane&&p.x>=0&&p.x<=1&&p.y>=0&&p.y<=1;
    e.panel.gameObject.SetActive(visible);if(!visible)continue;
    e.legacy.forceRenderingOff=true;e.text.text=e.source.text;
    Vector2 preferred=e.text.GetPreferredValues(e.text.text);
    float width=Mathf.Max(220,preferred.x+32),height=Mathf.Max(76,preferred.y+24);e.panel.sizeDelta=new Vector2(width,height);
    // Keep the whole instruction beside the acquisition sphere, leaving the
    // target itself clear. Opaque camera UI draws after world particle effects.
    var bounds=e.item.GetComponent<Collider>().bounds;
    float radius=Mathf.Abs(view.WorldToViewportPoint(e.item.transform.position+view.transform.up*bounds.extents.magnitude).y-p.y)*root.rect.height;
    float centerX=(p.x-.5f)*root.rect.width;
    float x=centerX+radius+16+width*.5f,y=(p.y-.5f)*root.rect.height;
    if(x+width*.5f>root.rect.width*.5f-8)x=centerX-radius-16-width*.5f;
    e.panel.anchoredPosition=new Vector2(Mathf.Clamp(x,-root.rect.width*.5f+width*.5f+8,root.rect.width*.5f-width*.5f-8),Mathf.Clamp(y,-root.rect.height*.5f+height*.5f+8,root.rect.height*.5f-height*.5f-8));
   }
  }
  private void Initialize(){
   view=GetComponent<Camera>();TMP_FontAsset font=null;
   foreach(var bridge in FindObjectsByType<WorldTextPresentation>(FindObjectsInactive.Include))if(bridge.Display!=null){font=bridge.Display.font;break;}
   var items=FindObjectsByType<PowerUpItem>(FindObjectsInactive.Include);if(view==null||font==null||items.Length==0)return;
   var go=new GameObject("PickupInstructions",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));root=(RectTransform)go.transform;root.SetParent(view.transform,false);go.layer=LayerMask.NameToLayer("Player HUD");
   canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=view;canvas.planeDistance=2.6f;canvas.sortingOrder=23;
   var scaler=go.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.screenMatchMode=UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
   foreach(var item in items){
    var source=item.GetComponentInChildren<TextMesh>(true);if(source==null)continue;
    var panelGo=new GameObject("PickupInstructionBacking",typeof(RectTransform),typeof(UnityEngine.UI.Image));panelGo.layer=go.layer;var panel=(RectTransform)panelGo.transform;panel.SetParent(root,false);
    var background=panelGo.GetComponent<UnityEngine.UI.Image>();background.color=new Color(.008f,.025f,.035f,1);background.raycastTarget=false;
    var textGo=new GameObject("PickupInstruction",typeof(RectTransform));textGo.layer=go.layer;textGo.transform.SetParent(panel,false);var text=textGo.AddComponent<TextMeshProUGUI>();text.font=font;text.fontSize=32;text.enableAutoSizing=false;text.textWrappingMode=TextWrappingModes.NoWrap;text.alignment=TextAlignmentOptions.Center;text.color=Color.white;text.raycastTarget=false;
    var rect=text.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(12,6);rect.offsetMax=new Vector2(-12,-6);
    var legacy=source.GetComponent<MeshRenderer>();legacy.forceRenderingOff=true;entries.Add(new Entry{item=item,source=source,legacy=legacy,panel=panel,text=text});panelGo.SetActive(false);
   }
  }
  private void OnDisable(){if(root!=null)root.gameObject.SetActive(false);}
  private void OnEnable(){if(root!=null)root.gameObject.SetActive(true);}
  private void OnDestroy(){foreach(var e in entries)if(e.legacy!=null)e.legacy.forceRenderingOff=false;if(root!=null)Destroy(root.gameObject);}
 }
}
