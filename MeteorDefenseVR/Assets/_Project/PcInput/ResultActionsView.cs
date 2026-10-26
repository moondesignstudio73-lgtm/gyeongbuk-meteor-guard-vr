using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.EyeTracking;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MeteorDefenseVR.PcInput
{
    [DefaultExecutionOrder(1200)]
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
        private WorldTextPresentation legacySummary;
        private Renderer[] legacyFrame;
        private TextMeshPro title,score,metrics,accuracy,rank,feedback,difficulty;
        public HangarMenuButton Replay { get; private set; }
        public HangarMenuButton Menu { get; private set; }
        public bool Visible => root != null && root.activeSelf;
        public void Initialize(Camera camera,TMP_FontAsset font,ResultController controller,HangarLobbyController hangar)
        {
            cameraView=camera;result=controller;lobby=hangar;seat=camera.transform.position;facing=camera.transform.rotation;
            gaze=FindAnyObjectByType<GazeRaycaster>();
            root=new GameObject("ResultActions",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.GraphicRaycaster));
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;canvas.sortingOrder=102;
            var plate=HangarLobbyView.Image("ResultSummaryPlate",root.transform,Vector2.zero,new Vector2(1050,660),FlightUiTheme.Ink);
            FlightUiTheme.Plate(plate);
            title=SummaryLabel("ResultHeading","이번 임무의 기록",new Vector2(0,270),new Vector2(950,65),560,font);
            difficulty=SummaryLabel("ResultDifficulty","",new Vector2(0,210),new Vector2(950,45),360,font);difficulty.color=FlightUiTheme.Muted;
            SummaryLabel("ScoreCaption","점수",new Vector2(0,153),new Vector2(950,45),360,font).color=FlightUiTheme.Muted;
            score=SummaryLabel("ResultScore","0",new Vector2(0,90),new Vector2(950,100),660,font);
            metrics=SummaryLabel("ResultMetrics","",new Vector2(-320,-15),new Vector2(300,110),400,font);
            accuracy=SummaryLabel("ResultAccuracy","",new Vector2(0,-15),new Vector2(300,110),400,font);
            rank=SummaryLabel("ResultRank","",new Vector2(320,-15),new Vector2(300,110),400,font);
            HangarLobbyView.Image("ResultDivider",root.transform,new Vector2(0,-80),new Vector2(950,1),FlightUiTheme.Edge);
            feedback=SummaryLabel("ResultFeedback","",new Vector2(0,-150),new Vector2(950,115),400,font);
            feedback.textWrappingMode=TextWrappingModes.Normal;
            Replay=HangarLobbyView.Button(root.transform,"Replay","다시 출격",new Vector2(-230,-265),font,()=>lobby.ChooseFromResult(true),new Vector2(420,76));
            Menu=HangarLobbyView.Button(root.transform,"Menu","기지 메뉴",new Vector2(230,-265),font,()=>lobby.ChooseFromResult(false),new Vector2(420,76));
            foreach(var label in Replay.GetComponentsInChildren<TextMeshPro>())label.fontSize=360;
            foreach(var label in Menu.GetComponentsInChildren<TextMeshPro>())label.fontSize=360;
            Replay.AllowImmediateClick=Menu.AllowImmediateClick=true;
            // Result actions also accept the UI input module pointer path, not only world gaze.
            Replay.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            Menu.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            textOverlay=new Material(font.material){name="Result action text overlay",renderQueue=4000};
            textOverlay.SetFloat("_ZTestMode",(float)UnityEngine.Rendering.CompareFunction.Always);
            foreach(var label in root.GetComponentsInChildren<TextMeshPro>()){label.fontSharedMaterial=textOverlay;}
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())renderer.sortingOrder=103;
            legacySummary=camera.transform.Find("ResultText")?.GetComponent<WorldTextPresentation>();
            legacyFrame=camera.transform.Find("ResultVisualFrame")?.GetComponentsInChildren<Renderer>(true);
            result.ResultHidden+=Hide;
            result.ResultShown+=Show;
            root.SetActive(false);Present();
        }
        private void Hide(){if(root!=null)root.SetActive(false);}
        private void Show(GameSessionSnapshot snapshot,string rank)=>Present();
        private TextMeshPro SummaryLabel(string name,string content,Vector2 position,Vector2 size,float fontSize,TMP_FontAsset font)
        {
            var label=HangarLobbyView.Label(root.transform,name,content,position,fontSize,font);
            label.rectTransform.sizeDelta=size;label.color=FlightUiTheme.Text;return label;
        }
        public void Present()
        {
            if(root==null)return;
            bool visible=result.IsVisible&&lobby.Phase==HangarLobbyController.LobbyPhase.Hidden;
            root.SetActive(visible);
            float fit=cameraView.stereoEnabled?1:Mathf.Min(1,cameraView.aspect/1.6f);
            root.transform.SetPositionAndRotation(seat+facing*new Vector3(0,.6f*fit,3.86f),facing);
            root.transform.localScale=Vector3.one*(.0045f*fit);
            if(legacySummary!=null)legacySummary.Display.GetComponent<Renderer>().forceRenderingOff=true;
            if(legacyFrame!=null)foreach(var renderer in legacyFrame)renderer.forceRenderingOff=true;
            if(!visible||result.Snapshot==null)return;
            var snapshot=result.Snapshot;
            title.text=snapshot.MissionFailed?"다시 도전해요!":"이번 임무의 기록";
            title.color=snapshot.MissionFailed?FlightUiTheme.Warning:FlightUiTheme.Text;
            difficulty.text="난이도 : "+Difficulty.DifficultyConfig.Label(snapshot.Difficulty);
            score.text=(legacySummary!=null?legacySummary.DisplayedCount:snapshot.Score).ToString("N0");
            metrics.text=$"<size=320>파괴한 운석</size>\n{snapshot.DestroyedMeteors:N0}";
            accuracy.text=$"<size=320>명중률</size>\n{snapshot.AccuracyPercent}%";
            rank.text=$"<size=320>등급</size>\n{result.Rank}";
            feedback.text=ResultFeedback.Achievement(snapshot)+"\n"+(result.RankSettings!=null?result.RankSettings.NextGoal(snapshot):"다음 임무에 도전해요!");
        }
        private void LateUpdate() => Present();
        private void OnDisable(){if(root!=null)root.SetActive(false);}
        private void OnDestroy(){if(result!=null){result.ResultHidden-=Hide;result.ResultShown-=Show;}if(root!=null)Destroy(root);if(textOverlay!=null)Destroy(textOverlay);}
    }
}
