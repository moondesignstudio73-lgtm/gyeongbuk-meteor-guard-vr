using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.EyeTracking;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MeteorDefenseVR.PcInput
{
    public sealed class ResultActionsView : MonoBehaviour
    {
        private GameObject root;
        private Camera cameraView;
        private ResultController result;
        private HangarLobbyController lobby;
        private GazeRaycaster gaze;
        private Vector3 seat;
        private Quaternion facing;
        private Material textOverlay;
        public HangarMenuButton Replay { get; private set; }
        public HangarMenuButton Menu { get; private set; }
        public bool Visible => root != null && root.activeSelf;
        public void Initialize(Camera camera,TMP_FontAsset font,ResultController controller,HangarLobbyController hangar)
        {
            cameraView=camera;result=controller;lobby=hangar;seat=camera.transform.position;facing=camera.transform.rotation;
            gaze=FindAnyObjectByType<GazeRaycaster>();
            root=new GameObject("ResultActions",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.GraphicRaycaster));
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;canvas.sortingOrder=102;
            Replay=HangarLobbyView.Button(root.transform,"Replay","다시 출격",new Vector2(-155,0),font,()=>lobby.ChooseFromResult(true),new Vector2(285,68));
            Menu=HangarLobbyView.Button(root.transform,"Menu","기지 메뉴",new Vector2(155,0),font,()=>lobby.ChooseFromResult(false),new Vector2(285,68));
            Replay.AllowImmediateClick=Menu.AllowImmediateClick=true;
            textOverlay=new Material(font.material){name="Result action text overlay",renderQueue=4000};
            textOverlay.SetFloat("_ZTestMode",(float)UnityEngine.Rendering.CompareFunction.Always);
            foreach(var label in root.GetComponentsInChildren<TextMeshPro>()){label.fontSize=360;label.fontSharedMaterial=textOverlay;}
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())renderer.sortingOrder=103;
            result.ResultHidden+=Hide;
            result.ResultShown+=Show;
            root.SetActive(false);Present();
        }
        private void Hide(){if(root!=null)root.SetActive(false);}
        private void Show(GameSessionSnapshot snapshot,string rank)=>Present();
        public void Present()
        {
            if(root==null)return;
            bool visible=result.IsVisible&&lobby.Phase==HangarLobbyController.LobbyPhase.Hidden;
            root.SetActive(visible);
            float fit=cameraView.stereoEnabled?1:Mathf.Min(1,cameraView.aspect/1.6f);
            root.transform.SetPositionAndRotation(seat+facing*new Vector3(0,-.7f*fit,3.86f),facing);
            root.transform.localScale=Vector3.one*(.0045f*fit);
        }
        private void LateUpdate()
        {
            Present();
            if(Visible&&Time.timeScale>0&&Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame)
            {if(gaze.CurrentTarget==Replay)Replay.Click();else if(gaze.CurrentTarget==Menu)Menu.Click();}
        }
        private void OnDisable(){if(root!=null)root.SetActive(false);}
        private void OnDestroy(){if(result!=null){result.ResultHidden-=Hide;result.ResultShown-=Show;}if(root!=null)Destroy(root);if(textOverlay!=null)Destroy(textOverlay);}
    }
}
