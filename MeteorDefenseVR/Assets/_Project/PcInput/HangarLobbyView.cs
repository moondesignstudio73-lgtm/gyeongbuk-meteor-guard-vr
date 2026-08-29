using System;
using MeteorDefenseVR.Audio;
using MeteorDefenseVR.GameFlow;
using TMPro;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    // Pooled world-space hologram + existing authored hangar. No camera transforms are written.
    public sealed class HangarLobbyView : MonoBehaviour
    {
        public static readonly Color Cyan=new Color(.17f,.9f,.94f);
        private static readonly Color Ink=new Color(.027f,.075f,.105f,.94f);
        private static readonly Color TextColor=new Color(.8f,.97f,1f);
        private Transform room,left,right;
        private Vector3 roomRest,leftRest,rightRest;
        private Vector3 leftScale,rightScale;
        private RectTransform panel;
        private GameObject menu,difficulty,back,standby,lightsRoot;
        private TextMeshPro title,subtitle,nextMission;
        private Light[] lights;
        private Camera cameraView;
        private Vector3 panelPosition, panelScale;
        public Transform DifficultyAnchor { get; private set; }
        public HangarMenuButton[] Buttons { get; private set; }
        public HangarRecordView Records { get; private set; }
        public bool RoomVisible => room!=null && room.gameObject.activeSelf;
        public void Build(Camera camera, Transform hangar, Transform leftDoor, Transform rightDoor, TMP_FontAsset font,
            Action<HangarLobbyController.MenuAction> action)
        {
            cameraView=camera; room=hangar; left=leftDoor; right=rightDoor;
            roomRest=room.localPosition; leftRest=left.localPosition; rightRest=right.localPosition;
            leftScale=left.localScale; rightScale=right.localScale;
            var root=new GameObject("HangarHologram",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.GraphicRaycaster));
            panel=(RectTransform)root.transform;
            // World-locked at the initial pilot seat pose, not attached to tracked head movement.
            panel.SetParent(null,false); panel.SetPositionAndRotation(camera.transform.TransformPoint(0,.65f,3.2f),camera.transform.rotation);
            panel.localScale=Vector3.one*.0045f; panel.sizeDelta=new Vector2(650,380);
            panelPosition=panel.localPosition; panelScale=panel.localScale;
            var canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.WorldSpace; canvas.worldCamera=camera; canvas.sortingOrder=14;
            Image("Frame",panel,Vector2.zero,new Vector2(650,380),new Color(.12f,.48f,.57f,.8f));
            Image("Glass",panel,Vector2.zero,new Vector2(646,376),Ink);
            Image("DockStatusRail",panel,new Vector2(-310,0),new Vector2(3,330),new Color(1,.78f,.47f));
            title=Label(panel,"Title","격납고 도착",new Vector2(0,145),310,font);
            subtitle=Label(panel,"Subtitle","EARTH DEFENSE / BAY 01",new Vector2(0,103),155,font);
            menu=Group("Menu",panel); difficulty=Group("DifficultyHud",panel); standby=Group("NextPlayer",panel);
            Buttons=new HangarMenuButton[9];
            string[] names={"Replay","Difficulty","Recalibrate","Standby"};
            string[] labels={"다시 하기","난이도 변경","시선 재설정","종료 / 대기"};
            for(int i=0;i<4;i++)
            {
                int index=i;
                var position=i==3?new Vector2(147,-108):new Vector2(i%2==0?-147:147,i<2?46:-31);
                Buttons[i]=Button(menu.transform,names[i],labels[i],position,font,()=>action((HangarLobbyController.MenuAction)index),new Vector2(280,62));
            }
            Buttons[6]=Button(menu.transform,"Ranking","랭킹",new Vector2(147,-31),font,()=>action(HangarLobbyController.MenuAction.Ranking),new Vector2(280,62));
            Buttons[8]=Button(menu.transform,"RetryStage","스테이지 재도전",new Vector2(-147,-108),font,()=>action(HangarLobbyController.MenuAction.RetryStage),new Vector2(280,62));
            nextMission=Label(menu.transform,"Help","메뉴를 바라보면 선택됩니다",new Vector2(0,-162),150,font);
            var slot=Group("DifficultyAnchor",panel); slot.transform.localPosition=new Vector3(0,0,-1);
            DifficultyAnchor=slot.transform;
            back=Group("BackToHangar",panel);
            Buttons[4]=Button(back.transform,"Back","격납고로 돌아가기",new Vector2(0,-117),font,()=>action(HangarLobbyController.MenuAction.Back));
            Buttons[5]=Button(standby.transform,"StartNewPlayer","새 체험 시작",new Vector2(0,32),font,()=>action(HangarLobbyController.MenuAction.StartNewPlayer),new Vector2(280,62));
            Buttons[7]=Button(standby.transform,"Ranking","랭킹",new Vector2(0,-48),font,()=>action(HangarLobbyController.MenuAction.Ranking),new Vector2(280,62));
            Label(standby.transform,"Help","다음 대원이 준비되면 시작하세요",new Vector2(0,-114),165,font);
            Records=new HangarRecordView(); Records.Build(panel,font,action);
            lightsRoot=Group("DockingLights",room.parent); lights=new Light[2];
            for(int i=0;i<2;i++)
            {
                var lamp=Group("BayWorkLight"+i,lightsRoot.transform);
                lamp.transform.localPosition=new Vector3(i==0?-2:2,1.9f,4);
                var light=lamp.AddComponent<Light>(); light.type=LightType.Point; light.range=10; light.intensity=3;
                light.shadows=LightShadows.None; light.color=i==0?new Color(.44f,.8f,1):new Color(1,.78f,.47f); lights[i]=light;
            }
            // Warm the new text and Canvas geometry once, while no gameplay state is activated.
            foreach(var text in panel.GetComponentsInChildren<TextMeshPro>(true)) text.ForceMeshUpdate(true);
            Canvas.ForceUpdateCanvases(); panel.gameObject.SetActive(false); lightsRoot.SetActive(false);
        }
        public void Show(HangarLobbyController.LobbyPhase phase, GameSessionSnapshot snapshot, bool locked, MeteorDefenseVR.Difficulty.DifficultyLevel nextDifficulty)
        {
            bool calibrating=phase==HangarLobbyController.LobbyPhase.Calibrating;
            panel.gameObject.SetActive(!calibrating);
            menu.SetActive(phase==HangarLobbyController.LobbyPhase.Menu);
            difficulty.SetActive(phase==HangarLobbyController.LobbyPhase.Difficulty);
            back.SetActive(phase==HangarLobbyController.LobbyPhase.Difficulty);
            standby.SetActive(phase==HangarLobbyController.LobbyPhase.Standby);
            Records.SetVisible(phase==HangarLobbyController.LobbyPhase.Records);
            subtitle.gameObject.SetActive(phase!=HangarLobbyController.LobbyPhase.Records);
            Buttons[1].SetAvailable(!locked);
            Buttons[8].gameObject.SetActive(snapshot != null && snapshot.MissionFailed);
            nextMission.text="NEXT: "+MeteorDefenseVR.Difficulty.DifficultyConfig.Label(nextDifficulty)+" / 메뉴를 바라보면 선택됩니다";
            title.text=phase switch
            {
                HangarLobbyController.LobbyPhase.Returning=>"기지로 귀환 중",
                HangarLobbyController.LobbyPhase.Difficulty=>"난이도를 선택하세요",
                HangarLobbyController.LobbyPhase.ReplayPreparing=>"출격 준비",
                HangarLobbyController.LobbyPhase.Departing=>"출격 준비",
                HangarLobbyController.LobbyPhase.Standby=>"다음 대원을 기다립니다",
                HangarLobbyController.LobbyPhase.Records=>"MISSION RECORD / 임무 기록",
                _=>"격납고 도착"
            };
            subtitle.text=phase==HangarLobbyController.LobbyPhase.Menu && snapshot!=null
                ? $"{(snapshot.MissionFailed ? "MISSION FAILED" : "MISSION SUCCESS")}   /   SCORE {snapshot.Score:N0}   /   {MeteorDefenseVR.Difficulty.DifficultyConfig.Label(snapshot.Difficulty)}"
                : phase==HangarLobbyController.LobbyPhase.Returning?"DOCKING APPROACH / BAY 01":"EARTH DEFENSE / BAY 01";
            if(phase==HangarLobbyController.LobbyPhase.Departing) panel.gameObject.SetActive(false);
        }
        public void SetRetryAvailable(bool value) { if (Buttons != null && Buttons.Length > 8 && Buttons[8] != null) Buttons[8].SetAvailable(value); }
        public void Animate(HangarLobbyController.LobbyPhase phase,float progress,float doorOpen,float speed)
        {
            bool returning=phase==HangarLobbyController.LobbyPhase.Returning;
            bool departing=phase==HangarLobbyController.LobbyPhase.Departing;
            float t=Mathf.Clamp01(progress);
            float travel=returning?7*(1-Mathf.SmoothStep(0,1,t/.8f)):departing?-9*speed:0;
            float opening=returning?1-Mathf.SmoothStep(0,1,(t-.4f)/.55f):departing?doorOpen:0;
            room.gameObject.SetActive(true); left.gameObject.SetActive(true); right.gameObject.SetActive(true);
            room.localPosition=roomRest+Vector3.forward*travel;
            left.localPosition=leftRest+Vector3.forward*travel+Vector3.left*(2.8f*opening);
            right.localPosition=rightRest+Vector3.forward*travel+Vector3.right*(2.8f*opening);
            // Extend the existing armor panels to the bay floor; restore original launch proportions when opening.
            float seal=departing?1-opening:1;
            left.localPosition+=Vector3.down*(.45f*seal); right.localPosition+=Vector3.down*(.45f*seal);
            left.localScale=Vector3.Scale(leftScale,new Vector3(1,1+.6f*seal,1));
            right.localScale=Vector3.Scale(rightScale,new Vector3(1,1+.6f*seal,1));
            lightsRoot.SetActive(true); lightsRoot.transform.localPosition=Vector3.forward*travel;
            for(int i=0;i<lights.Length;i++) lights[i].intensity=(returning?Mathf.Lerp(.8f,9f,t):9f)+.08f*Mathf.Sin(Time.unscaledTime*1.4f+i);
            if(!cameraView.stereoEnabled)
            {
                float fit=Mathf.Min(1,cameraView.aspect/1.6f);
                panel.localScale=panelScale*fit;
            }
            // No cockpit vibration is added: doors, work lights and the hull convey docking.
            panel.localPosition=panelPosition;
        }
        public void Hide()
        {
            if(panel!=null) panel.gameObject.SetActive(false);
            if(lightsRoot!=null) lightsRoot.SetActive(false);
            if(room!=null) room.localPosition=roomRest;
            if(left!=null) left.localPosition=leftRest;
            if(right!=null) right.localPosition=rightRest;
            if(left!=null) left.localScale=leftScale;
            if(right!=null) right.localScale=rightScale;
        }
        internal static GameObject Group(string name,Transform parent)
        { var go=new GameObject(name); go.transform.SetParent(parent,false); return go; }
        internal static UnityEngine.UI.Image Image(string name,Transform parent,Vector2 position,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image)); var rect=(RectTransform)go.transform;
            rect.SetParent(parent,false); rect.anchoredPosition=position; rect.sizeDelta=size;
            var image=go.GetComponent<UnityEngine.UI.Image>(); image.color=color; image.raycastTarget=false; return image;
        }
        internal static TextMeshPro Label(Transform parent,string name,string content,Vector2 position,float size,TMP_FontAsset font)
        {
            var go=new GameObject(name); go.SetActive(false); go.transform.SetParent(parent,false);
            var text=go.AddComponent<TextMeshPro>(); text.font=font; text.fontSharedMaterial=font.material; text.fontSize=size;
            text.text=content; text.color=TextColor; text.alignment=TextAlignmentOptions.Center; text.enableAutoSizing=false;
            text.textWrappingMode=TextWrappingModes.NoWrap; text.raycastTarget=false;
            text.rectTransform.sizeDelta=new Vector2(620,45); text.transform.localPosition=new Vector3(position.x,position.y,-3);
            go.GetComponent<MeshRenderer>().sortingOrder=15; go.SetActive(true); return text;
        }
        internal static HangarMenuButton Button(Transform parent,string name,string text,Vector2 position,TMP_FontAsset font,Action action,Vector2? dimensions=null)
        {
            Vector2 size=dimensions??new Vector2(280,80);
            var outline=Image(name,parent,position,size,new Color(.1f,.4f,.5f,.8f));
            var surface=Image("Surface",outline.transform,Vector2.zero,size-Vector2.one*4,new Color(.035f,.14f,.19f));
            var button=outline.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic=surface;
            var label=Label(outline.transform,"Label",text,new Vector2(-13,0),Mathf.Min(235,size.y*3.2f),font); label.rectTransform.sizeDelta=new Vector2(size.x-52,size.y);
            var ring=CalibrationProgressRing.Create(outline.transform,Mathf.Min(31,size.y*.42f)); ring.name="GazeProgressRing"; ring.rectTransform.anchoredPosition=new Vector2(size.x*.5f-23,0);
            ring.PresentTinted(0,Cyan,Cyan);
            var collider=outline.gameObject.AddComponent<BoxCollider>(); collider.size=new Vector3(size.x,size.y,2); collider.isTrigger=true;
            var feedback=outline.gameObject.AddComponent<UIInteractionAnimator>(); feedback.SetVisual(label.transform);
            feedback.EnableHoverCue(AudioCue.TargetFocus); feedback.EnableConfirmCue(AudioCue.LockOn); feedback.SetAutomaticPointerConfirm(false);
            var target=outline.gameObject.AddComponent<HangarMenuButton>(); target.Configure(feedback,ring,outline,action); return target;
        }
        private void OnDestroy()
        { if(panel!=null) Destroy(panel.gameObject); if(lightsRoot!=null) Destroy(lightsRoot); }
    }
}
