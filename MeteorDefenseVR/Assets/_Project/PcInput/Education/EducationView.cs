using System;
using System.Collections.Generic;
using System.Linq;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.PcInput;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace MeteorDefenseVR.Education
{
    // Bounded, reusable panels. Player-facing results contain no storage paths or debug values.
    public sealed class EducationView : MonoBehaviour
    {
        private static readonly Color Ink = new Color(.012f, .038f, .06f, .98f), Cyan = new Color(.3f,.94f,1), White = new Color(.86f,.96f,1);
        private EducationSessionManager owner;
        private GazeInputRouter router;
        private Camera cameraView;
        private EducationSettings settings;
        private GameObject root, entry, registration, hud, question, report, dashboard;
        private Canvas canvas;
        private RectTransform canvasRect;
        private TMP_FontAsset font, participantFont;
        private readonly List<HangarMenuButton> buttons = new List<HangarMenuButton>();
        private HangarMenuButton hovered;
        private TMP_InputField nameInput, numberInput, gradeInput, groupInput;
        private TextMeshProUGUI registrationHint, difficultyValue, skipValue, stageLabel, instruction, timer, guidance, questionTitle, questionBody, reportName, reportBody, reportMetrics, dashboardTitle, dashboardNotice;
        private readonly TextMeshProUGUI[] optionLabels = new TextMeshProUGUI[4];
        private readonly TextMeshProUGUI[,] rowCells = new TextMeshProUGUI[5,8];
        private readonly UIInteractionAnimator[] difficultyFeedback = new UIInteractionAnimator[4];
        private readonly GameObject[] options = new GameObject[4], rowButtons = new GameObject[5];
        private readonly TextMeshPro[] markers = new TextMeshPro[5];
        private GameObject teacherEntry;
        private DifficultyLevel selected = DifficultyLevel.Normal;
        private bool skipTutorial, details, todayOnly = true, confirmClear;
        private int page;
        private float nextText, selectionAt, clearUntil;
        private EducationSessionResult detailRecord;
        private List<EducationSessionResult> visibleRecords = new List<EducationSessionResult>();
        private bool wasXr;
        private ReadyPrewarmController warmup;
        private SfPresentationDirector bootPresentation;
        public GameObject Root => root;
        public void Build(EducationSessionManager session, GazeInputRouter input, Camera camera, EducationSettings configuration)
        {
            owner = session; router = input; cameraView = camera; settings = configuration; font = settings.interfaceFont; participantFont = settings.participantFont != null ? settings.participantFont : font;
            warmup=GetComponent<ReadyPrewarmController>();bootPresentation=GetComponent<SfPresentationDirector>();
            if (EventSystem.current == null)
            {
                var events = new GameObject("EducationEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)); events.transform.SetParent(transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            root = new GameObject("EducationCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasRect = (RectTransform)root.transform; canvas = root.GetComponent<Canvas>(); canvas.worldCamera = cameraView; canvas.sortingOrder = 80;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280,720); scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            canvas.planeDistance = Mathf.Max(.12f,cameraView.nearClipPlane+.01f); ConfigureCanvas();
            entry = Group("Entry");
            Button(entry.transform, "EducationStart", "우주 안전 훈련 시작 · 약 12분", new Vector2(0,-202), new Vector2(470,56), () => { ClearParticipantFont(); owner.OpenRegistration(); });
            teacherEntry = Button(entry.transform, "TeacherResults", "교사용 결과", new Vector2(-505,-314), new Vector2(180,36), () => { page = 0; detailRecord = null; ClearParticipantFont(); owner.OpenOperator(); }).gameObject;
            BuildRegistration(); BuildHud(); BuildQuestion(); BuildReport(); BuildDashboard();
            for (int i = 0; i < markers.Length; i++)
            {
                var marker = new GameObject("EducationObjectLabel" + i); marker.SetActive(false); markers[i] = marker.AddComponent<TextMeshPro>();
                markers[i].font = font; markers[i].fontSize = 6; markers[i].alignment = TextAlignmentOptions.Center; markers[i].color = Cyan;
                markers[i].enableAutoSizing = false; markers[i].textWrappingMode = TextWrappingModes.NoWrap; markers[i].rectTransform.sizeDelta = new Vector2(16,5);
            }
            owner.Changed += Refresh; Refresh();
        }
        private void ConfigureCanvas()
        {
            wasXr = cameraView.stereoEnabled;
            canvas.renderMode = wasXr ? RenderMode.WorldSpace : RenderMode.ScreenSpaceCamera;
            if (wasXr)
            {
                canvasRect.SetParent(null, false); canvasRect.sizeDelta = new Vector2(1280,720); canvasRect.localScale = Vector3.one * .0021f;
                canvasRect.SetPositionAndRotation(cameraView.transform.TransformPoint(0,.25f,2.4f), cameraView.transform.rotation);
            }
            else canvasRect.SetParent(cameraView.transform, false);
        }
        private void ClearParticipantFont()
        {
            if (participantFont != null && participantFont != font && participantFont.atlasPopulationMode == AtlasPopulationMode.Dynamic)
                participantFont.ClearFontAssetData(true);
        }
        private void BuildRegistration()
        {
            registration = Panel("ParticipantRegistration", new Vector2(1030,580));
            Label(registration.transform,"Title","우주 안전 훈련 / 참가자 등록",new Vector2(0,238),new Vector2(930,45),31);
            Label(registration.transform,"Privacy","입력한 정보와 훈련 결과만 이 PC에 저장합니다. 영상·얼굴 정보는 저장하지 않습니다.",new Vector2(0,192),new Vector2(940,38),19);
            nameInput = Input(registration.transform,"ParticipantName","이름 *",new Vector2(-220,119),32);
            numberInput = Input(registration.transform,"ParticipantNumber","번호 *",new Vector2(220,119),24);
            gradeInput = Input(registration.transform,"ParticipantGrade","학년 (선택)",new Vector2(-220,38),16);
            groupInput = Input(registration.transform,"ParticipantGroup","그룹 / 반 (선택)",new Vector2(220,38),32);
            difficultyValue = Label(registration.transform,"Difficulty","난이도 : 보통",new Vector2(0,-41),new Vector2(820,36),24);
            for (int i=0;i<4;i++) { int index=i; difficultyFeedback[i]=Button(registration.transform,"Difficulty"+i,DifficultyConfig.Label((DifficultyLevel)i),new Vector2(-315+i*210,-91),new Vector2(196,43),()=> { selected=(DifficultyLevel)index; UpdateRegistration(); }).GetComponent<UIInteractionAnimator>(); }
            skipValue = Button(registration.transform,"SkipTutorial","재방문: 튜토리얼 건너뛰기 OFF",new Vector2(0,-151),new Vector2(650,38),()=> { skipTutorial=!skipTutorial; UpdateRegistration(); }).GetComponentInChildren<TextMeshProUGUI>();
            registrationHint = Label(registration.transform,"Validation","이름과 번호는 직접 입력해 주세요. 별도 로그인은 없습니다.",new Vector2(0,-197),new Vector2(900,32),18);
            Button(registration.transform,"CancelRegistration","돌아가기",new Vector2(-245,-244),new Vector2(220,44),owner.CloseMenu);
            Button(registration.transform,"BeginTraining","훈련 시작",new Vector2(245,-244),new Vector2(300,44),()=>
            {
                var participant = new ParticipantInfo { Name=nameInput.text,Number=numberInput.text,Grade=gradeInput.text,Group=groupInput.text };
                if (!owner.StartTraining(participant,selected,skipTutorial)) registrationHint.text="이름과 번호를 입력해 주세요.";
            });
        }
        private void UpdateRegistration()
        {
            var d=GetComponent<DifficultyManager>(); if(d!=null && d.Settings.lockDifficultySelection) selected=d.Settings.Default;
            difficultyValue.text="난이도 : "+DifficultyConfig.Label(selected)+(d!=null && d.Settings.lockDifficultySelection?" (운영자 고정)":"");
            skipValue.text="재방문: 튜토리얼 건너뛰기 "+(skipTutorial?"ON":"OFF");
            for(int i=0;i<4;i++)difficultyFeedback[i].SetSelected(i==(int)selected);
        }
        private void BuildHud()
        {
            hud=Group("TrainingHud");
            var bar=Image(hud.transform,"StageRail",new Vector2(0,206),new Vector2(1120,114),new Color(.006f,.025f,.042f,.89f));
            stageLabel=Label(bar.transform,"Stage","",new Vector2(-240,33),new Vector2(610,34),22); stageLabel.alignment=TextAlignmentOptions.Left;
            timer=Label(bar.transform,"Time","",new Vector2(382,33),new Vector2(300,34),18); timer.alignment=TextAlignmentOptions.Right;
            instruction=Label(bar.transform,"Instruction","",new Vector2(0,-18),new Vector2(1050,63),23);
            guidance=Label(hud.transform,"Guidance","",new Vector2(-385,-233),new Vector2(490,130),19); guidance.alignment=TextAlignmentOptions.BottomLeft;
        }
        private void BuildQuestion()
        {
            question=Panel("EmergencyQuestion",new Vector2(1000,445));
            questionTitle=Label(question.transform,"Title","",new Vector2(0,174),new Vector2(920,43),29);
            questionBody=Label(question.transform,"Body","",new Vector2(0,100),new Vector2(900,85),23);
            for(int i=0;i<4;i++)
            {
                int option=i; var button=Button(question.transform,"Answer"+i,"",new Vector2(0,19-i*54),new Vector2(850,46),()=>owner.SubmitAnswer(option));
                options[i]=button.gameObject; optionLabels[i]=button.GetComponentInChildren<TextMeshProUGUI>();
            }
        }
        private void BuildReport()
        {
            report=Panel("LearningReport",new Vector2(1160,610));
            Label(report.transform,"Title","우주 안전 훈련 결과",new Vector2(0,254),new Vector2(1060,43),32);
            reportName=Label(report.transform,"Participant","",new Vector2(0,199),new Vector2(1080,60),20,participantFont);
            reportBody=Label(report.transform,"LearningSummary","",new Vector2(-255,-1),new Vector2(480,338),24); reportBody.alignment=TextAlignmentOptions.TopLeft;
            reportMetrics=Label(report.transform,"Metrics","",new Vector2(275,-1),new Vector2(510,338),23); reportMetrics.alignment=TextAlignmentOptions.TopLeft;
            Label(report.transform,"Meaning","충돌 회피율 = 기존 레이저 방어로 충돌을 막은 비율 · 모의 계통은 교육용입니다.",new Vector2(0,-219),new Vector2(1070,30),17);
            Button(report.transform,"ReportDetails","요약 / 상세",new Vector2(-360,-267),new Vector2(215,42),()=> {details=!details;UpdateReport();});
            Button(report.transform,"ReportBack","메인 메뉴",new Vector2(-70,-267),new Vector2(215,42),()=> {if(owner.Phase==EducationPhase.Operator){detailRecord=null;Refresh();}else owner.ReturnToReady();});
            Button(report.transform,"NextParticipant","다음 참가자",new Vector2(280,-267),new Vector2(330,42),()=> { if(owner.Phase==EducationPhase.Report)owner.NextParticipant(); });
        }
        private void BuildDashboard()
        {
            dashboard=Panel("OperatorEducationResults",new Vector2(1190,610));
            dashboardTitle=Label(dashboard.transform,"Title","교사용 교육 결과",new Vector2(0,257),new Vector2(1100,42),30);
            string[] headers={"번호","이름","교육 점수","탐지","반응","회피","비상대응","보완 영역"};
            float[] widths={70,135,100,110,110,110,110,330};float x=-540;
            for(int c=0;c<8;c++){var header=Label(dashboard.transform,"Column"+c,headers[c],new Vector2(x+widths[c]/2,150),new Vector2(widths[c]-8,34),18);header.alignment=TextAlignmentOptions.Left;x+=widths[c];}
            for(int i=0;i<5;i++)
            {
                int row=i; var button=Button(dashboard.transform,"StudentRow"+i,"",new Vector2(0,94-i*58),new Vector2(1120,49),()=>
                {int index=page*5+row;if(index<visibleRecords.Count){detailRecord=visibleRecords[index];details=false;ClearParticipantFont();Refresh();}});
                rowButtons[i]=button.gameObject;button.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
                x=-540;for(int c=0;c<8;c++)
                {var cell=Label(button.transform,"Cell"+c,"",new Vector2(x+widths[c]/2,0),new Vector2(widths[c]-8,42),18,c<2?participantFont:font);cell.alignment=TextAlignmentOptions.Left;cell.textWrappingMode=TextWrappingModes.NoWrap;cell.overflowMode=TextOverflowModes.Ellipsis;rowCells[i,c]=cell;x+=widths[c];}
            }
            Button(dashboard.transform,"DateFilter","오늘 / 전체",new Vector2(-445,199),new Vector2(220,36),()=>{todayOnly=!todayOnly;page=0;ClearParticipantFont();UpdateDashboard();});
            Button(dashboard.transform,"PreviousPage","이전",new Vector2(-95,-210),new Vector2(170,38),()=>{page=Mathf.Max(0,page-1);ClearParticipantFont();UpdateDashboard();});
            Button(dashboard.transform,"NextPage","다음",new Vector2(95,-210),new Vector2(170,38),()=>{page=Mathf.Min(Mathf.Max(0,(visibleRecords.Count-1)/5),page+1);ClearParticipantFont();UpdateDashboard();});
            dashboardNotice=Label(dashboard.transform,"Notice","",new Vector2(0,-168),new Vector2(1120,35),16);
            Button(dashboard.transform,"ExportCsv","결과 CSV 저장",new Vector2(-405,-268),new Vector2(265,42),()=>
            { confirmClear=false;bool ok=owner.ExportCsv(out var file);dashboardNotice.text=ok?"CSV 저장 완료 · Education/Exports 폴더":"CSV 저장 실패 · PC 폴더 권한을 확인하세요."; });
            Button(dashboard.transform,"ClearResults","기록 전체 삭제",new Vector2(-90,-268),new Vector2(245,42),()=>
            {
                if(!confirmClear||Time.unscaledTime>clearUntil){confirmClear=true;clearUntil=Time.unscaledTime+5;dashboardNotice.text="5초 안에 다시 누르면 교육 기록과 복구본을 지웁니다. 먼저 CSV를 저장하세요.";return;}
                bool ok=owner.ClearStoredResults();confirmClear=false;UpdateDashboard();dashboardNotice.text=ok?"기록과 복구본을 비웠습니다.":"삭제 실패 · 원본을 확인해 주세요.";
            });
            Button(dashboard.transform,"CloseOperator","돌아가기",new Vector2(390,-268),new Vector2(260,42),owner.CloseMenu);
        }
        private void Refresh()
        {
            if(root==null)return; hovered?.OnGazeExit();hovered=null;selectionAt=Time.unscaledTime+.35f;
            foreach(var group in new[]{entry,registration,hud,question,report,dashboard})group.SetActive(false);
            if(wasXr) canvasRect.SetPositionAndRotation(cameraView.transform.TransformPoint(0,.25f,2.4f),cameraView.transform.rotation);
            switch(owner.Phase)
            {
                case EducationPhase.Idle: break;
                case EducationPhase.Registration:
                    registration.SetActive(true);nameInput.text=numberInput.text=gradeInput.text=groupInput.text="";skipTutorial=false;selected=GetComponent<DifficultyManager>().Selected;UpdateRegistration();break;
                case EducationPhase.Operator:
                    if(detailRecord!=null){report.SetActive(true);UpdateReport();}else{dashboard.SetActive(true);UpdateDashboard();}break;
                case EducationPhase.Report: detailRecord=null;report.SetActive(true);details=false;UpdateReport();break;
                case EducationPhase.Preparing: break;
                default:
                    hud.SetActive(true);
                    bool asking=owner.Phase==EducationPhase.CauseQuestion||owner.Phase==EducationPhase.ResponseQuestion||owner.Phase==EducationPhase.TriageQuestion;
                    question.SetActive(asking);
                    if(asking)
                    {
                        questionTitle.text=owner.Message;questionBody.text=owner.QuestionText;
                        string[] choices=owner.AnswerOptions;
                        for(int i=0;i<4;i++){options[i].SetActive(i<choices.Length);if(i<choices.Length)optionLabels[i].text=choices[i];}
                    }
                    break;
            }
            UpdateLive();
        }
        private void UpdateReport()
        {
            var r=detailRecord??owner.Session;if(r==null)return;
            bool teacher=owner.Phase==EducationPhase.Operator;
            reportName.text=(teacher?r.Participant.Name:r.Participant.MaskedName)+" / "+r.Participant.Number+"번\n"+DifficultyConfig.Label(r.Difficulty)+" · "+DateTimeOffset.Parse(r.StartedAt).ToLocalTime().ToString("yyyy-MM-dd HH:mm");reportName.richText=false;
            reportBody.text=details?EducationReportFormatter.Details(r):EducationReportFormatter.Summary(r);reportBody.fontSize=details?21:22;
            reportMetrics.fontSize=details?21:23;
            reportMetrics.text=(details?EducationReportFormatter.Areas(r):EducationReportFormatter.Metrics(r))+(owner.Phase==EducationPhase.Report&&!owner.Store.Saved?"\n\n로컬 저장 실패 · 교사에게 알려 주세요.":"");
            var next=report.transform.Find("NextParticipant");if(next!=null)next.gameObject.SetActive(!teacher);
        }
        private void UpdateDashboard()
        {
            if(!owner.OperatorAllowed)return;
            confirmClear=false;
            var now=DateTime.Now.Date;
            visibleRecords=owner.Store.Records.Where(r=>!todayOnly||DateTimeOffset.Parse(r.StartedAt).ToLocalTime().Date==now).ToList();
            int today=owner.Store.Records.Where(r=>DateTimeOffset.Parse(r.StartedAt).ToLocalTime().Date==now).Select(r=>new{r.Participant.Name,r.Participant.Number,r.Participant.Grade,r.Participant.Group}).Distinct().Count();
            dashboardTitle.text=$"교사용 교육 결과 · 오늘 {today}명 · {(todayOnly?"오늘":"전체")} {visibleRecords.Count}건";
            page=Mathf.Clamp(page,0,Mathf.Max(0,(visibleRecords.Count-1)/5));
            for(int i=0;i<5;i++)
            {
                int index=page*5+i;rowButtons[i].SetActive(index<visibleRecords.Count);if(index>=visibleRecords.Count)continue;
                var r=visibleRecords[index];var a=EducationAssessment.Calculate(r);
                string[] values={r.Participant.Number,r.Participant.Name,$"{a.Score:F0}{(r.Completed?"":" / 중단")}",EducationReportFormatter.Percent(a.DetectionAccuracy,a.Threats+a.FalsePositive),EducationReportFormatter.Seconds(a.MeanReaction),EducationReportFormatter.Percent(a.DefenseRate,a.DefenseRequired),EducationReportFormatter.Percent(a.EmergencyAccuracy,a.EmergencyQuestions),a.Weakest};
                for(int c=0;c<8;c++)rowCells[i,c].text=values[c];
            }
            dashboardNotice.text=$"최근 {settings.retentionDays}일 / 최대 {settings.maximumStoredSessions}건 · 페이지 {page+1} · 학생 행을 선택하면 상세 결과"+(owner.Store.Recovery!=EducationResultStore.RecoveryStatus.None?" · 저장 복구 상태 확인 필요":"");
        }
        private void LateUpdate()
        {
            if(root==null)return;
            if(wasXr!=cameraView.stereoEnabled)ConfigureCanvas();
            if(wasXr&&hud.activeSelf&&!question.activeSelf)canvasRect.SetPositionAndRotation(cameraView.transform.TransformPoint(0,.25f,2.4f),cameraView.transform.rotation);
            bool idle=owner.Phase==EducationPhase.Idle && GameFlowManager.Instance!=null && GameFlowManager.Instance.CurrentState==Core.GameState.Boot
                && (warmup==null||warmup.IsComplete) && (bootPresentation==null||!bootPresentation.IsBootPresenting);
            entry.SetActive(idle);teacherEntry.SetActive(idle&&owner.OperatorAllowed);
            if(Time.unscaledTime>=nextText){nextText=Time.unscaledTime+.1f;UpdateLive();}
            UpdateGaze();
        }
        private void UpdateLive()
        {
            if(owner.IsTraining&&hud.activeSelf)
            {
                stageLabel.text=$"STAGE {(int)owner.Stage+1}/4 · {EducationLabels.Stage(owner.Stage)}";
                timer.text=$"남은 학습 {owner.StageRemaining:F0}초 / 과제 {owner.PhaseRemaining:F0}초";
                instruction.text=owner.Message;
                if(question.activeSelf)questionBody.text=owner.QuestionText;
            }
            int index=0;var lines=new List<string>(5);
            foreach(var pair in owner.Tracked)
            {
                var meteor=pair.Key;if(meteor==null||!meteor.IsTargetable||index>=markers.Length)continue;
                var risk=owner.Risk(meteor);var marker=markers[index++];marker.gameObject.SetActive(hud.activeSelf&&!question.activeSelf);
                bool detailsAvailable=owner.Session.Difficulty<=DifficultyLevel.Normal||pair.Value.DetectedAt>=0;
                marker.text=$"#{pair.Value.Id}"+(detailsAvailable?$"  {risk.Distance:F0}m\n접근 {Mathf.Max(0,risk.ClosingSpeed):F1}m/s":"");
                marker.transform.SetPositionAndRotation(meteor.transform.position+cameraView.transform.up*(meteor.Size*1.1f),cameraView.transform.rotation);
                marker.fontSize=Mathf.Clamp(risk.Distance/12,2,8);
                if(owner.Session.Difficulty<=DifficultyLevel.Normal)lines.Add($"#{pair.Value.Id} {EducationLabels.Direction(pair.Value.Direction)}  {risk.Distance:F0}m");
                else if(owner.Session.Difficulty==DifficultyLevel.Hard&&lines.Count<2)lines.Add($"#{pair.Value.Id} {EducationLabels.Direction(pair.Value.Direction)}");
            }
            for(int i=index;i<markers.Length;i++)markers[i].gameObject.SetActive(false);
            guidance.text=string.Join("\n",lines);
        }
        private void UpdateGaze()
        {
            if(Time.unscaledTime<selectionAt||Time.timeScale<=0||!router.IsMenuTrackingValid){hovered?.OnGazeExit();hovered=null;return;}
            Vector2 screen;
            if((router.ActiveMode==PcGazeMode.MouseGaze||router.ActiveMode==PcGazeMode.WindowsVRSimulation||router.ActiveMode==PcGazeMode.CameraCenterGaze)&&Mouse.current!=null&&!router.Simulation.UsesCenterAim)screen=Mouse.current.position.ReadValue();
            else {var ray=router.GetGazeRay();screen=cameraView.WorldToScreenPoint(ray.origin+ray.direction*3);}
            HangarMenuButton next=null;
            foreach(var button in buttons)
                if(button!=null&&button.isActiveAndEnabled&&button.GetComponent<UnityEngine.UI.Button>().interactable&&RectTransformUtility.RectangleContainsScreenPoint((RectTransform)button.transform,screen,cameraView)){next=button;break;}
            if(next!=hovered){hovered?.OnGazeExit();hovered=next;hovered?.OnGazeEnter(default);}
            hovered?.OnGazeStay(default,Time.deltaTime);
        }
        private GameObject Group(string name)
        {var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(canvasRect,false);return go;}
        private GameObject Panel(string name,Vector2 size)
        {var go=Group(name);Image(go.transform,"Border",Vector2.zero,size,Cyan*.65f);Image(go.transform,"Surface",Vector2.zero,size-Vector2.one*3,Ink);return go;}
        private static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
        {var go=new GameObject(name,typeof(RectTransform));var r=(RectTransform)go.transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=Vector2.one*.5f;r.anchoredPosition=position;r.sizeDelta=size;return r;}
        private static UnityEngine.UI.Image Image(Transform parent,string name,Vector2 position,Vector2 size,Color color)
        {var r=Rect(parent,name,position,size);var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;return image;}
        private TextMeshProUGUI Label(Transform parent,string name,string text,Vector2 position,Vector2 size,float fontSize,TMP_FontAsset typeface=null)
        {
            var r=Rect(parent,name,position,size);r.gameObject.SetActive(false);var label=r.gameObject.AddComponent<TextMeshProUGUI>();
            label.font=typeface!=null?typeface:font;label.fontSize=fontSize;label.fontStyle=FontStyles.Bold;label.textWrappingMode=TextWrappingModes.Normal;label.enableAutoSizing=false;label.color=White;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;label.text=text;label.richText=false;r.gameObject.SetActive(true);return label;
        }
        private UnityEngine.UI.Button Button(Transform parent,string name,string caption,Vector2 position,Vector2 size,Action action)
        {
            var surface=Image(parent,name,position,size,new Color(.1f,.4f,.5f,.8f));surface.raycastTarget=true;
            var fill=Image(surface.transform,"Fill",Vector2.zero,size-Vector2.one*3,new Color(.025f,.10f,.15f));
            var button=surface.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=fill;
            var label=Label(surface.transform,"Label",caption,new Vector2(-10,0),size-new Vector2(50,6),22);
            var ring=CalibrationProgressRing.Create(surface.transform,24);ring.rectTransform.anchoredPosition=new Vector2(size.x*.5f-19,0);
            var animator=surface.gameObject.AddComponent<UIInteractionAnimator>();animator.SetAutomaticPointerConfirm(false);animator.EnableHoverCue(MeteorDefenseVR.Audio.AudioCue.TargetFocus);animator.EnableConfirmCue(MeteorDefenseVR.Audio.AudioCue.LockOn);
            var gaze=surface.gameObject.AddComponent<HangarMenuButton>();gaze.Configure(animator,ring,surface,action);buttons.Add(gaze);return button;
        }
        private TMP_InputField Input(Transform parent,string name,string caption,Vector2 position,int length)
        {
            Label(parent,name+"Caption",caption,position+new Vector2(0,32),new Vector2(380,28),19);
            var surface=Image(parent,name,position-new Vector2(0,12),new Vector2(380,46),new Color(.16f,.55f,.65f));surface.raycastTarget=true;
            Image(surface.transform,"Fill",Vector2.zero,new Vector2(377,43),new Color(.025f,.105f,.145f));
            surface.gameObject.SetActive(false);var field=surface.gameObject.AddComponent<TMP_InputField>();
            var viewport=Rect(surface.transform,"Viewport",Vector2.zero,new Vector2(355,41));viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var text=Label(viewport,"Text","",Vector2.zero,new Vector2(350,38),24,participantFont);text.alignment=TextAlignmentOptions.Left;
            field.textViewport=viewport;field.textComponent=text;field.targetGraphic=surface;field.characterLimit=length;field.lineType=TMP_InputField.LineType.SingleLine;field.richText=false;field.caretColor=Cyan;field.customCaretColor=true;
            surface.gameObject.SetActive(true);return field;
        }
        public void Hide(){if(root!=null)root.SetActive(false);foreach(var marker in markers)if(marker!=null)marker.gameObject.SetActive(false);}
        private void OnDestroy(){if(owner!=null)owner.Changed-=Refresh;if(root!=null)Destroy(root);foreach(var marker in markers)if(marker!=null)Destroy(marker.gameObject);}
    }
}
