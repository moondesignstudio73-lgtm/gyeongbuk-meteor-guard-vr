using MeteorDefenseVR.Tutorial;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MeteorDefenseVR.PcInput
{
    // Presentation-only subscriber. Tutorial sequence, wording and completion remain owned by TutorialController.
    [DefaultExecutionOrder(980), DisallowMultipleComponent]
    public sealed class TutorialSafeZoneView : MonoBehaviour
    {
        [SerializeField] private TutorialController controller;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform safePanel;
        [SerializeField] private TextMeshProUGUI instructionText;
        [SerializeField, Range(.8f,2f)] private float vrDepth=1.25f;
        [SerializeField, Range(.0005f,.002f)] private float vrScale=.0011f;
        private bool? worldMode;

        public Canvas HudCanvas=>canvas;
        public CanvasGroup HudGroup=>group;
        public RectTransform SafePanel=>safePanel;
        public TextMeshProUGUI InstructionText=>instructionText;
        public string CurrentInstruction { get; private set; } = string.Empty;
        public bool IsVisible=>canvas!=null&&canvas.enabled&&group!=null&&group.alpha>.99f;
        public float VrDepth=>vrDepth;

        public void Configure(TutorialController owner,Camera camera,Canvas targetCanvas,CanvasGroup canvasGroup,RectTransform panel,TextMeshProUGUI text)
        {
            controller=owner;playerCamera=camera;canvas=targetCanvas;group=canvasGroup;safePanel=panel;instructionText=text;
            worldMode=null;ApplyCanvasMode(playerCamera!=null&&playerCamera.stereoEnabled);Present(controller!=null?controller.Instruction:string.Empty);
        }

        private void OnEnable()
        {
            if(controller==null)controller=FindAnyObjectByType<TutorialController>(FindObjectsInactive.Include);
            if(playerCamera==null)playerCamera=Camera.main;
            if(controller!=null){controller.InstructionChanged-=Present;controller.InstructionChanged+=Present;Present(controller.Instruction);}
        }

        private void OnDisable(){if(controller!=null)controller.InstructionChanged-=Present;}

        private void LateUpdate()
        {
            if(playerCamera!=null)ApplyCanvasMode(playerCamera.stereoEnabled);
            bool visible=controller!=null&&(controller.IsRunning||controller.IsComplete)&&!string.IsNullOrWhiteSpace(controller.Instruction);
            if(group!=null){group.alpha=visible?1f:0f;group.interactable=false;group.blocksRaycasts=false;}
            if(canvas!=null)canvas.enabled=visible;
        }

        public void ApplyCanvasMode(bool stereo)
        {
            if(canvas==null||playerCamera==null||worldMode==stereo)return;worldMode=stereo;
            RectTransform root=(RectTransform)canvas.transform;
            canvas.worldCamera=playerCamera;canvas.overrideSorting=true;canvas.sortingOrder=230;
            if(stereo)
            {
                root.SetParent(playerCamera.transform,false);
                canvas.renderMode=RenderMode.WorldSpace;root.anchorMin=Vector2.zero;root.anchorMax=Vector2.zero;root.pivot=Vector2.one*.5f;
                root.sizeDelta=new Vector2(1920,1080);root.localPosition=Vector3.forward*vrDepth;root.localRotation=Quaternion.identity;root.localScale=Vector3.one*vrScale;
            }
            else
            {
                // A camera-parented Screen Space canvas can receive two transform invalidations
                // during a large simulated head turn. TMP then renders stale/cull-corrupted
                // geometry for one frame. Keep desktop HUD roots transform-independent.
                root.SetParent(null,false);root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;
                root.offsetMin=root.offsetMax=Vector2.zero;
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.planeDistance=.45f;
                root.localPosition=Vector3.zero;root.localRotation=Quaternion.identity;root.localScale=Vector3.one;
            }
            RefreshTextGeometry();
        }

        private void Present(string message)
        {
            if(instructionText==null)return;
            CurrentInstruction=message??string.Empty;
            instructionText.text="<size=27><b><cspace=3>TUTORIAL // GAZE TRAINING</cspace></b></size>\n<size=48><b>"+CurrentInstruction+"</b></size>";
            RefreshTextGeometry();
        }

        private void RefreshTextGeometry()
        {
            if(instructionText==null)return;
            instructionText.canvasRenderer.cullTransparentMesh=false;
            instructionText.SetVerticesDirty();
            if(safePanel!=null)LayoutRebuilder.ForceRebuildLayoutImmediate(safePanel);
            instructionText.ForceMeshUpdate(true,true);
        }
    }
}
