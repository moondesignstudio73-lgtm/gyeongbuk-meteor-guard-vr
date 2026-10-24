using System.Collections.Generic;
using MeteorDefenseVR.PowerUps;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace MeteorDefenseVR.PcInput {
 // Presentation only: colliders, positions, dwell and collection remain on PowerUpItem.
 [DefaultExecutionOrder(1450),DisallowMultipleComponent]
 public sealed class PickupInstructionOverlay:MonoBehaviour {
  private sealed class Entry { public PowerUpItem item; public TextMesh source; public MeshRenderer legacy; public RectTransform panel; public TextMeshProUGUI text; public Vector2 desired; public bool visible; }
  private readonly List<Entry> entries=new List<Entry>();
  private readonly List<Rect> occupied=new List<Rect>();
  private readonly List<float> candidateX=new List<float>(),candidateY=new List<float>();
  private readonly Vector3[] corners=new Vector3[4];
  private static readonly string[] protectedNames={"HealthPanel","StagePanel","ScorePanel","SystemMessagePanel","ShipStatusHologram","ThreatStatusHologram","LowHealthNoticeBacking"};
  private readonly RectTransform[] protectedPanels=new RectTransform[protectedNames.Length];
  private Camera view;private RectTransform root;private Canvas canvas;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
  private static void Install(){SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}
  private static void Loaded(Scene scene,LoadSceneMode mode){var camera=Camera.main;if(camera!=null&&camera.GetComponent<PickupInstructionOverlay>()==null)camera.gameObject.AddComponent<PickupInstructionOverlay>();}
  private void LateUpdate(){
   if(root==null){Initialize();if(root==null)return;}
   bool stereo=view.stereoEnabled;canvas.renderMode=stereo?RenderMode.WorldSpace:RenderMode.ScreenSpaceCamera;
   if(stereo){root.sizeDelta=new Vector2(720*view.aspect,720);root.localPosition=Vector3.forward*2.6f;root.localRotation=Quaternion.identity;root.localScale=Vector3.one*(2*2.6f*Mathf.Tan(view.fieldOfView*.5f*Mathf.Deg2Rad)/720);}
   occupied.Clear();
   for(int i=0;i<protectedPanels.Length;i++){
    if(protectedPanels[i]==null){var go=GameObject.Find(protectedNames[i]);if(go!=null)protectedPanels[i]=go.GetComponent<RectTransform>();}
    var panel=protectedPanels[i];if(panel==null||!panel.gameObject.activeInHierarchy)continue;
    var plate=panel.GetComponent<UnityEngine.UI.Image>();if(plate!=null&&!plate.enabled)continue;
    panel.GetWorldCorners(corners);Vector2 min=Vector2.one*float.PositiveInfinity,max=Vector2.one*float.NegativeInfinity;
    foreach(var corner in corners){var p=view.WorldToViewportPoint(corner);var q=new Vector2((p.x-.5f)*root.rect.width,(p.y-.5f)*root.rect.height);min=Vector2.Min(min,q);max=Vector2.Max(max,q);}
    occupied.Add(Rect.MinMaxRect(min.x-8,min.y-8,max.x+8,max.y+8));
   }
   // Reserve all acquisition spheres before placing any instruction, so one
   // item's label cannot hide a different item's aiming centre.
   foreach(var e in entries){
    bool available=e.item!=null&&e.item.IsAvailable;Vector3 p=available?view.WorldToViewportPoint(e.item.transform.position):Vector3.back;
    bool visible=available&&p.z>view.nearClipPlane&&p.x>=0&&p.x<=1&&p.y>=0&&p.y<=1;
    e.panel.gameObject.SetActive(visible);e.visible=visible;if(!visible)continue;
    e.legacy.forceRenderingOff=true;e.text.text=e.source.text;
    Vector2 preferred=e.text.GetPreferredValues(e.text.text);
    float width=Mathf.Max(220,preferred.x+32),height=Mathf.Max(76,preferred.y+24);e.panel.sizeDelta=new Vector2(width,height);
    // Keep the whole instruction beside the acquisition sphere, leaving the
    // target itself clear. Opaque camera UI draws after world particle effects.
    var bounds=e.item.GetComponent<Collider>().bounds;
    float radius=Mathf.Abs(view.WorldToViewportPoint(e.item.transform.position+view.transform.up*bounds.extents.magnitude).y-p.y)*root.rect.height;
    float centerX=(p.x-.5f)*root.rect.width;
    float x=centerX+radius+16+width*.5f,y=(p.y-.5f)*root.rect.height;
    occupied.Add(new Rect(centerX-radius-8,y-radius-8,2*radius+16,2*radius+16));
    if(x+width*.5f>root.rect.width*.5f-8)x=centerX-radius-16-width*.5f;
    e.desired=new Vector2(Mathf.Clamp(x,-root.rect.width*.5f+width*.5f+8,root.rect.width*.5f-width*.5f-8),Mathf.Clamp(y,-root.rect.height*.5f+height*.5f+8,root.rect.height*.5f-height*.5f-8));
   }
   foreach(var e in entries){if(!e.visible)continue;
    var size=e.panel.sizeDelta;Vector2 position=Place(e.desired,size);
    e.panel.anchoredPosition=position;occupied.Add(new Rect(position-size*.5f-Vector2.one*8,size+Vector2.one*16));
   }
  }
  private Vector2 Place(Vector2 desired,Vector2 size){
   var bounds=root.rect;float left=bounds.xMin+size.x*.5f+8,right=bounds.xMax-size.x*.5f-8,bottom=bounds.yMin+size.y*.5f+8,top=bounds.yMax-size.y*.5f-8;
   bool Free(Vector2 p){var r=new Rect(p-size*.5f,size);foreach(var block in occupied)if(r.Overlaps(block))return false;return true;}
   if(Free(desired))return desired;
   candidateX.Clear();candidateY.Clear();candidateX.Add(desired.x);candidateY.Add(desired.y);candidateX.Add(left);candidateX.Add(right);candidateY.Add(bottom);candidateY.Add(top);
   foreach(var block in occupied){candidateX.Add(Mathf.Clamp(block.xMin-size.x*.5f-1,left,right));candidateX.Add(Mathf.Clamp(block.xMax+size.x*.5f+1,left,right));candidateY.Add(Mathf.Clamp(block.yMin-size.y*.5f-1,bottom,top));candidateY.Add(Mathf.Clamp(block.yMax+size.y*.5f+1,bottom,top));}
   Vector2 best=desired;float distance=float.PositiveInfinity;
   foreach(float x in candidateX)foreach(float y in candidateY){var p=new Vector2(x,y);float d=(p-desired).sqrMagnitude;if(d>=distance||!Free(p))continue;best=p;distance=d;}
   return best;
  }
  private void Initialize(){
   view=GetComponent<Camera>();TMP_FontAsset font=null;
   foreach(var bridge in FindObjectsByType<WorldTextPresentation>(FindObjectsInactive.Include))if(bridge.Display!=null){font=bridge.Display.font;break;}
   var items=FindObjectsByType<PowerUpItem>(FindObjectsInactive.Include);var hud=FindAnyObjectByType<GameplayHudOverlayView>();if(view==null||font==null||items.Length==0||hud==null||hud.HealthValue==null)return;
   font=hud.HealthValue.font;
   var go=new GameObject("PickupInstructions",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));root=(RectTransform)go.transform;root.SetParent(view.transform,false);go.layer=LayerMask.NameToLayer("Player HUD");
   canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=view;canvas.planeDistance=.25f;canvas.sortingOrder=230;
   var scaler=go.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.screenMatchMode=UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
   foreach(var item in items){
    var source=item.GetComponentInChildren<TextMesh>(true);if(source==null)continue;
    var panelGo=new GameObject("PickupInstructionBacking",typeof(RectTransform),typeof(UnityEngine.UI.Image));panelGo.layer=go.layer;var panel=(RectTransform)panelGo.transform;panel.SetParent(root,false);
    var background=panelGo.GetComponent<UnityEngine.UI.Image>();background.color=new Color(.008f,.025f,.035f,1);background.raycastTarget=false;background.material=hud.TelemetryRoot.GetComponentInChildren<UnityEngine.UI.Image>(true).material;
    var textGo=new GameObject("PickupInstruction",typeof(RectTransform));textGo.layer=go.layer;textGo.transform.SetParent(panel,false);var text=textGo.AddComponent<TextMeshProUGUI>();text.font=font;text.fontSharedMaterial=hud.HealthValue.fontSharedMaterial;text.fontSize=32;text.enableAutoSizing=false;text.textWrappingMode=TextWrappingModes.NoWrap;text.alignment=TextAlignmentOptions.Center;text.color=Color.white;text.raycastTarget=false;
    var rect=text.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(12,6);rect.offsetMax=new Vector2(-12,-6);
    var legacy=source.GetComponent<MeshRenderer>();legacy.forceRenderingOff=true;entries.Add(new Entry{item=item,source=source,legacy=legacy,panel=panel,text=text});panelGo.SetActive(false);
   }
  }
  private void OnDisable(){if(root!=null)root.gameObject.SetActive(false);}
  private void OnEnable(){if(root!=null)root.gameObject.SetActive(true);}
  private void OnDestroy(){foreach(var e in entries)if(e.legacy!=null)e.legacy.forceRenderingOff=false;if(root!=null)Destroy(root.gameObject);}
 }
}
