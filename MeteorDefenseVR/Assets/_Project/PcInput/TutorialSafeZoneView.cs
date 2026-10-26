using System.Collections.Generic;
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
        [SerializeField] private Renderer[] suppressedWorldRenderers;
        [SerializeField, Range(.8f,2f)] private float vrDepth=1.25f;
        [SerializeField, Range(.0005f,.002f)] private float vrScale=.0011f;
        private bool? worldMode;
        private TextMeshProUGUI heading;

        public Canvas HudCanvas=>canvas;
        public CanvasGroup HudGroup=>group;
        public RectTransform SafePanel=>safePanel;
        public TextMeshProUGUI InstructionText=>instructionText;
        public string CurrentInstruction { get; private set; } = string.Empty;
        public bool IsVisible=>canvas!=null&&canvas.enabled&&group!=null&&group.alpha>.99f;
        public float VrDepth=>vrDepth;

        public IReadOnlyList<Renderer> SuppressedWorldRenderers=>suppressedWorldRenderers;

        public void Configure(TutorialController owner,Camera camera,Canvas targetCanvas,CanvasGroup canvasGroup,RectTransform panel,TextMeshProUGUI text,Renderer[] worldRenderers)
        {
            controller=owner;playerCamera=camera;canvas=targetCanvas;group=canvasGroup;safePanel=panel;instructionText=text;
            suppressedWorldRenderers=worldRenderers;worldMode=null;ApplyCanvasMode(playerCamera!=null&&playerCamera.stereoEnabled);
            SuppressWorldPresentation();Present(controller!=null?controller.Instruction:string.Empty);
        }

        private void OnEnable()
        {
            if(controller==null)controller=FindAnyObjectByType<TutorialController>(FindObjectsInactive.Include);
            if(playerCamera==null)playerCamera=Camera.main;
            if(suppressedWorldRenderers==null||suppressedWorldRenderers.Length==0)
            {
                TutorialStatusView legacy=FindAnyObjectByType<TutorialStatusView>(FindObjectsInactive.Include);
                if(legacy!=null)suppressedWorldRenderers=legacy.GetComponentsInChildren<Renderer>(true);
            }
            if(controller!=null){controller.InstructionChanged-=Present;controller.InstructionChanged+=Present;Present(controller.Instruction);}
            SuppressWorldPresentation();
        }

        private void OnDisable(){if(controller!=null)controller.InstructionChanged-=Present;}

        private void LateUpdate()
        {
            // TutorialStatusView is retained as the controller's data channel, but both its
            // TextMesh and the old TMP world-space bridge must stay invisible. State changes and
            // prewarm passes can re-enable either renderer after scene load, so enforce this in
            // the last presentation phase instead of relying only on authored enabled flags.
            SuppressWorldPresentation();
            if(playerCamera!=null)ApplyCanvasMode(playerCamera.stereoEnabled);
            bool visible=controller!=null&&(controller.IsRunning||controller.IsComplete)&&!string.IsNullOrWhiteSpace(controller.Instruction);
            if(group!=null){group.alpha=visible?1f:0f;group.interactable=false;group.blocksRaycasts=false;}
            if(canvas!=null)canvas.enabled=visible;
        }

        public void SuppressWorldPresentation()
        {
            if(suppressedWorldRenderers==null)return;
            foreach(Renderer renderer in suppressedWorldRenderers)
            {
                if(renderer==null)continue;
                renderer.enabled=false;
                renderer.forceRenderingOff=true;
            }
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
            Arrange();
            CurrentInstruction=message??string.Empty;
            instructionText.text=CurrentInstruction;
            RefreshTextGeometry();
        }

        private void Arrange()
        {
            if(safePanel==null||heading!=null)return;
            // Keep the aiming area clear; the controller still owns every instruction and its timing.
            safePanel.anchorMin=new Vector2(.08f,.73f);safePanel.anchorMax=new Vector2(.92f,.97f);
            safePanel.offsetMin=safePanel.offsetMax=Vector2.zero;
            FlightUiTheme.Plate(safePanel.GetComponent<Image>());
            var outline=safePanel.GetComponent<Outline>();if(outline!=null)outline.enabled=false;
            var go=new GameObject("TutorialHeading",typeof(RectTransform));go.transform.SetParent(safePanel,false);
            heading=go.AddComponent<TextMeshProUGUI>();heading.font=instructionText.font;heading.fontSharedMaterial=instructionText.fontSharedMaterial;
            heading.fontSize=30;heading.color=FlightUiTheme.Accent;heading.text="조준 연습";heading.raycastTarget=false;
            heading.alignment=TextAlignmentOptions.MidlineLeft;
            heading.rectTransform.anchorMin=new Vector2(0,1);heading.rectTransform.anchorMax=Vector2.one;
            heading.rectTransform.pivot=new Vector2(.5f,1);heading.rectTransform.offsetMin=new Vector2(30,-51);heading.rectTransform.offsetMax=new Vector2(-30,-10);
            var rect=instructionText.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
            rect.offsetMin=new Vector2(30,12);rect.offsetMax=new Vector2(-30,-52);
            instructionText.fontSize=48;instructionText.color=FlightUiTheme.Text;instructionText.fontStyle=FontStyles.Bold;
            instructionText.alignment=TextAlignmentOptions.MidlineLeft;instructionText.margin=Vector4.zero;
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
