using MeteorDefenseVR.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace MeteorDefenseVR.PcInput
{
    // Screen-space PC-only controls. Gameplay and VR HUD remain world-space and unchanged.
    public sealed class PcTestOverlay : MonoBehaviour
    {
        [SerializeField] private GazeInputRouter router;
        [SerializeField] private PcCalibrationController calibration;
        [SerializeField] private PcTestControls controls;
        [SerializeField] private TMP_FontAsset font;
        private GameObject canvasObject, panel, calibrationPanel;
        private TextMeshProUGUI status, debug, instruction, pointText, gazeText, previewLabel;
        private UnityEngine.UI.RawImage preview;
        private RectTransform gazeRect, pointRect;
        private GameObject startButton;
        private float nextTextUpdate;
        private bool menuOpen;
        private GameObject resumeButton;
        private TextMeshProUGUI simulationHelp;
        private HangarLobbyController lobby;
        private bool wasBoot;
        private GameObject developerMenuButton;
        private TextMeshProUGUI developerMenuLabel;
        private TextMeshProUGUI compactStatus;
        private TextMeshProUGUI buildIdentity;
        private TextMeshProUGUI fallbackNotice;
        private GameObject inputButton, fallbackButton, resetButton, exitButton, keyLegend;
        private GameObject previewButton, cursorButton, faceButton;
        private CalibrationPointMotion pointMotion;
        private CalibrationProgressRing pointRing;
        private CanvasGroup calibrationFade;
        private bool calibrationWasVisible;
        private float calibrationAge;
        private GameObject lookSettingsPanel, lookSettingsButton;
        private readonly TextMeshProUGUI[] lookSettingsValues = new TextMeshProUGUI[4];
        private float nextSettingsText;
        private bool settingsOpen, wasPaused;
        public void Configure(GazeInputRouter input, PcCalibrationController calibrator, PcTestControls testControls, TMP_FontAsset uiFont)
        { router = input; calibration = calibrator; controls = testControls; font = uiFont; }
        private void Start()
        {
            lobby = Object.FindAnyObjectByType<HangarLobbyController>();
            if (EventSystem.current == null)
            {
                var events = new GameObject("PcEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            canvasObject = new GameObject("PcTestCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.GetComponent<Canvas>().sortingOrder = 90;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            panel = Rect("InputPanel", canvasObject.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -20), new Vector2(360, 535)).gameObject;
            Background(panel, new Color(.005f, .025f, .045f, .94f));
            Label(panel.transform, "Heading", "PC TEST / INPUT MODE", new Vector2(16, -16), new Vector2(328, 34), 23);
            status = Label(panel.transform, "Status", "AUTO", new Vector2(16, -54), new Vector2(328, 74), 18);
            AddModeButton("AUTO", PcGazeMode.Auto, 138);
            AddModeButton("웹캠 시선", PcGazeMode.WebcamGaze, 184);
            AddModeButton("마우스 시선", PcGazeMode.MouseGaze, 230);
            AddModeButton("화면 중앙 시선", PcGazeMode.CameraCenterGaze, 276);
            AddModeButton("VR 시뮬레이션", PcGazeMode.WindowsVRSimulation, 444);
            AddModeButton("VR EYE TRACKING", PcGazeMode.VREyeTracking, 490);
            Button(panel.transform, "Recalibrate", "웹캠 다시 보정", new Vector2(16, -324), new Vector2(160, 38), calibration.RetryWebcam);
            previewButton = Button(panel.transform, "Preview", "영상 보기 / 숨기기", new Vector2(184, -324), new Vector2(160, 38), () => router.Webcam.ShowWebcamPreview = !router.Webcam.ShowWebcamPreview).gameObject;
            cursorButton = Button(panel.transform, "Cursor", "시선점 표시", new Vector2(16, -369), new Vector2(160, 38), () =>
            {
                if (router.Simulation != null) controls.ShowGazeCursor = router.Simulation.ShowCenterPoint = !router.Simulation.ShowCenterPoint;
                else controls.ShowGazeCursor = !controls.ShowGazeCursor;
            }).gameObject;
            faceButton = Button(panel.transform, "Debug", "얼굴 Debug", new Vector2(184, -369), new Vector2(160, 38), () => router.Webcam.ShowFaceDebug = !router.Webcam.ShowFaceDebug).gameObject;
            Label(panel.transform, "Privacy", "영상 저장·업로드 없음 / 근사 시선 테스트", new Vector2(16, -413), new Vector2(328, 24), 15);
            debug = Label(canvasObject.transform, "DebugStatus", "", new Vector2(25, -480), new Vector2(580, 130), 17);
            compactStatus = Label(canvasObject.transform, "CompactInputStatus", "", new Vector2(24, -20), new Vector2(480, 58), 18);
            buildIdentity = Label(canvasObject.transform, "BuildIdentity", LoadBuildIdentity(), Vector2.zero, new Vector2(260, 42), 13);
            buildIdentity.alignment = TextAlignmentOptions.BottomLeft;
            buildIdentity.color = new Color(.48f, .72f, .78f, .72f);
            DockBottom(buildIdentity.gameObject, false, 20, 8);
            fallbackNotice = Label(canvasObject.transform, "InputHelp", "", Vector2.zero, new Vector2(680, 34), 22);
            var helpRect = fallbackNotice.rectTransform;
            helpRect.anchorMin = helpRect.anchorMax = new Vector2(.5f, 0f); helpRect.pivot = new Vector2(.5f, 0f);
            helpRect.anchoredPosition = new Vector2(0, 24); fallbackNotice.alignment = TextAlignmentOptions.Center;
            inputButton = Button(canvasObject.transform, "InputMenu", "INPUT MODE", Vector2.zero, new Vector2(174, 38), ToggleInputMenu).gameObject;
            DockBottom(inputButton, false, 20, 24);
            developerMenuButton = Button(canvasObject.transform, "DeveloperMenu", "개발자 테스트 UI", Vector2.zero, new Vector2(280, 44), ToggleDeveloperMenu).gameObject;
            var developerRect = developerMenuButton.GetComponent<RectTransform>();
            developerRect.anchorMin = developerRect.anchorMax = developerRect.pivot = new Vector2(.5f, 0f);
            developerRect.anchoredPosition = new Vector2(0f, 24f);
            developerMenuLabel = developerMenuButton.GetComponentInChildren<TextMeshProUGUI>();
            // Start hidden until the mode has been resolved; Public must never flash a debug entry.
            developerMenuButton.SetActive(false);
            fallbackButton = Button(canvasObject.transform, "MouseFallback", "마우스로 전환", Vector2.zero, new Vector2(174, 38), () => router.SetMode(PcGazeMode.MouseGaze)).gameObject;
            DockBottom(fallbackButton, false, 206, 24);
            var start = Button(canvasObject.transform, "StartPC", "START / 게임 시작", new Vector2(20, -650), new Vector2(360, 48), () => Object.FindAnyObjectByType<ReadyScreenController>()?.StartExperience());
            startButton = start.gameObject;
            var startRect = start.GetComponent<RectTransform>();
            startRect.anchorMin = startRect.anchorMax = new Vector2(.5f, .16f);
            startRect.pivot = Vector2.one * .5f; startRect.anchoredPosition = Vector2.zero;
            resetButton = Button(canvasObject.transform, "Reset", "RESET", Vector2.zero, new Vector2(174, 38), controls.ResetExperience).gameObject;
            DockBottom(resetButton, true, 206, 24);
            exitButton = Button(canvasObject.transform, "Exit", "EXIT", Vector2.zero, new Vector2(174, 38), controls.Exit).gameObject;
            DockBottom(exitButton, true, 20, 24);
            keyLegend = Label(canvasObject.transform, "Keys", "F1 인트로 · F2 보정 · F3 튜토리얼 · F4 연습\nF5 본게임 · F6 보스 · F7 결과 · R 초기화 · ESC 일시정지\n마우스 이동: 둘러보기 / 중앙 시선점으로 자동 조준", Vector2.zero, new Vector2(590, 78), 17).gameObject;
            DockBottom(keyLegend, false, 20, 76);
            preview = Rect("WebcamPreview", canvasObject.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -24), new Vector2(320, 240)).gameObject.AddComponent<UnityEngine.UI.RawImage>();
            preview.raycastTarget = false;
            preview.uvRect = new Rect(1, 1, -1, -1);
            previewLabel = Label(preview.transform, "Caption", "LOCAL WEBCAM PREVIEW", new Vector2(0, -244), new Vector2(320, 28), 18);
            calibrationPanel = Rect("Calibration", canvasObject.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -42), new Vector2(880, 155)).gameObject;
            Background(calibrationPanel, new Color(.005f, .035f, .055f, .94f), false);
            calibrationFade = calibrationPanel.AddComponent<CanvasGroup>();
            calibrationFade.blocksRaycasts = false;
            instruction = Label(calibrationPanel.transform, "Instructions", "", new Vector2(20, -12), new Vector2(840, 132), 28);
            instruction.alignment = TextAlignmentOptions.Center;
            pointRect = Rect("CalibrationPoint", canvasObject.transform, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, Vector2.one * 90);
            pointText = pointRect.gameObject.AddComponent<TextMeshProUGUI>(); Style(pointText, 62); pointText.alignment = TextAlignmentOptions.Center;
            pointMotion = pointRect.gameObject.AddComponent<CalibrationPointMotion>(); pointMotion.ConfigurePc();
            pointRing = CalibrationProgressRing.Create(pointRect, 90f);
            gazeRect = Rect("GazeCursor", canvasObject.transform, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, Vector2.one * 44);
            gazeText = gazeRect.gameObject.AddComponent<TextMeshProUGUI>(); Style(gazeText, 32); gazeText.text = "+"; gazeText.alignment = TextAlignmentOptions.Center;
            resumeButton = Button(canvasObject.transform, "ResumeSimulation", "게임으로 돌아가기 / ESC", Vector2.zero, new Vector2(370, 52), () => router.Simulation?.Resume()).gameObject;
            var resumeRect = resumeButton.GetComponent<RectTransform>(); resumeRect.anchorMin = resumeRect.anchorMax = resumeRect.pivot = Vector2.one * .5f;
            resumeRect.anchoredPosition = new Vector2(0, -120);
            simulationHelp = Label(canvasObject.transform, "SimulationHelp", "마우스로 주변을 둘러보세요.\nESC를 누르면 커서가 해제됩니다.", Vector2.zero, new Vector2(960, 90), 24);
            simulationHelp.rectTransform.anchorMin = simulationHelp.rectTransform.anchorMax = simulationHelp.rectTransform.pivot = new Vector2(.5f, 0);
            simulationHelp.rectTransform.anchoredPosition = new Vector2(0, 100); simulationHelp.alignment = TextAlignmentOptions.Center;
            BuildLookSettings();
        }
        private void ToggleInputMenu() { menuOpen = !menuOpen; if (menuOpen) router.Simulation?.Pause(); }
        private void BuildLookSettings()
        {
            lookSettingsButton = Button(canvasObject.transform, "LookSettings", "시점 설정", Vector2.zero, new Vector2(150, 38), () => { settingsOpen = !settingsOpen; menuOpen = false; }).gameObject;
            DockBottom(lookSettingsButton, true, 20, 76);
            lookSettingsPanel = Rect("LookSettingsPanel", canvasObject.transform, Vector2.one * .5f, Vector2.one * .5f, new Vector2(0, 80), new Vector2(620, 330)).gameObject;
            Background(lookSettingsPanel, new Color(.005f, .025f, .045f, .98f));
            Label(lookSettingsPanel.transform, "Title", "FLIGHT CONTROL / 시점 설정", new Vector2(24, -18), new Vector2(572, 40), 27);
            for (int i = 0; i < 4; i++) lookSettingsValues[i] = Label(lookSettingsPanel.transform, "Value" + i, "", new Vector2(24, -72 - i * 44), new Vector2(340, 36), 23);
            Button(lookSettingsPanel.transform, "SensitivityDown", "−", new Vector2(390, -72), new Vector2(85, 36), () => router.Simulation.Sensitivity -= .1f);
            Button(lookSettingsPanel.transform, "SensitivityUp", "+", new Vector2(490, -72), new Vector2(85, 36), () => router.Simulation.Sensitivity += .1f);
            Button(lookSettingsPanel.transform, "InvertY", "변경", new Vector2(390, -116), new Vector2(185, 36), () => router.Simulation.InvertY = !router.Simulation.InvertY);
            Button(lookSettingsPanel.transform, "CenterPoint", "변경", new Vector2(390, -160), new Vector2(185, 36), () => router.Simulation.ShowCenterPoint = !router.Simulation.ShowCenterPoint);
            Button(lookSettingsPanel.transform, "CameraShake", "변경", new Vector2(390, -204), new Vector2(185, 36), () => router.Simulation.Shake = (CameraShakeLevel)(((int)router.Simulation.Shake + 1) % 3));
            Label(lookSettingsPanel.transform, "Comfort", "VR은 흔들림 항상 끔 · 웹캠은 고개를 돌려 유지하면 회전", new Vector2(24, -256), new Vector2(570, 26), 16);
            Button(lookSettingsPanel.transform, "Close", "닫기", new Vector2(240, -289), new Vector2(140, 32), () => settingsOpen = false);
            lookSettingsPanel.SetActive(false);
        }
        private void Update()
        {
            if (canvasObject == null) return;
            bool visible = controls.ShowUi && (!router.EducationModalOpen || router.Simulation != null && router.Simulation.IsPaused);
            canvasObject.SetActive(visible);
            if (!visible) { menuOpen = settingsOpen = false; router.UiModalOpen = false; return; }
            bool calibrating = calibration.IsVisible;
            if (calibrating && !calibrationWasVisible) calibrationAge = 0f;
            calibrationWasVisible = calibrating;
            calibrationAge += Time.unscaledDeltaTime;
            calibrationFade.alpha = Mathf.Clamp01(calibrationAge / .22f);
            bool isBoot = GameFlowManager.Instance != null && GameFlowManager.Instance.CurrentState == MeteorDefenseVR.Core.GameState.Boot;
            if (isBoot && !wasBoot) menuOpen = settingsOpen = false;
            wasBoot = isBoot;
            if (controls.DebugKeysEnabled && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) ToggleInputMenu();
            bool development = controls.ShowDebugUi;
            bool operatorUi = controls.ShowOperatorUi;
            bool simulation = router.Simulation != null && router.Simulation.UsesCenterAim;
            bool paused = router.Simulation != null && router.Simulation.IsPaused;
            if (wasPaused && !paused) menuOpen = settingsOpen = false;
            wasPaused = paused;
            bool settingsAvailable = router.Simulation != null && (isBoot || paused || (GameFlowManager.Instance != null && GameFlowManager.Instance.CurrentState == MeteorDefenseVR.Core.GameState.Result));
            if (!settingsAvailable) settingsOpen = false;
            lookSettingsButton.SetActive(settingsAvailable && !calibrating);
            lookSettingsPanel.SetActive(settingsAvailable && settingsOpen && !calibrating);
            if (settingsOpen && Time.unscaledTime >= nextSettingsText)
            {
                nextSettingsText = Time.unscaledTime + .1f;
                var look = router.Simulation;
                lookSettingsValues[0].text = $"마우스 감도   {look.Sensitivity:F1}";
                lookSettingsValues[1].text = "상하 반전   " + (look.InvertY ? "ON" : "OFF");
                lookSettingsValues[2].text = "시선점 표시   " + (look.ShowCenterPoint ? "ON" : "OFF");
                lookSettingsValues[3].text = "카메라 흔들림   " + (look.Shake == CameraShakeLevel.Off ? "끔" : look.Shake == CameraShakeLevel.Weak ? "약" : "강");
            }
            resumeButton.SetActive(paused);
            simulationHelp.gameObject.SetActive(simulation && (isBoot || paused) && !calibrating);
            if (!operatorUi && !development) menuOpen = false;
            panel.SetActive(!settingsOpen && !calibrating && (operatorUi || development) && (menuOpen || paused));
            router.UiModalOpen = panel.activeSelf || lookSettingsPanel.activeSelf;
            compactStatus.gameObject.SetActive(development && !panel.activeSelf && !calibrating);
            buildIdentity.gameObject.SetActive((development || operatorUi) && !calibrating);
            developerMenuButton.SetActive(development && isBoot && !calibrating);
            developerMenuLabel.text = menuOpen ? "개발자 UI 닫기" : "개발자 테스트 UI";
            inputButton.SetActive((operatorUi || development) && !(development && isBoot));
            resetButton.SetActive(operatorUi); exitButton.SetActive(operatorUi);
            fallbackButton.SetActive(development); keyLegend.SetActive(development);
            previewButton.SetActive(development); cursorButton.SetActive(development); faceButton.SetActive(development);
            debug.gameObject.SetActive(development);
            bool mouseFallback = router.ActiveMode == PcGazeMode.MouseGaze && !string.IsNullOrEmpty(router.Notice);
            fallbackNotice.gameObject.SetActive(!development && !operatorUi && !calibrating && mouseFallback);
            fallbackNotice.text = mouseFallback ? "마우스로 둘러보고 중앙 시선점을 운석에 맞추세요." : string.Empty;
            calibrationPanel.SetActive(calibrating);
            pointRect.gameObject.SetActive(calibrating && calibration.IsWebcamCalibration);
            if (calibrating)
            {
                instruction.text = calibration.Message + (calibration.IsWebcamCalibration ? $"\n{Mathf.Min(calibration.PointIndex + 1, 5)} / 5" : "");
                if (!calibration.IsWebcamCalibration && calibration.Message == "시선 인식 완료!")
                    instruction.text = "입력 안내 완료!\n마우스로 미션을 시작합니다.";
                pointRect.anchorMin = pointRect.anchorMax = calibration.Target;
                pointText.text = calibration.Progress >= .99f ? "●" : calibration.Progress > .15f ? "◉" : "○";
                pointText.color = calibration.IsPointComplete ? CalibrationProgressRing.CompleteGreen : CalibrationProgressRing.ActiveRed;
                pointRing.Present(calibration.Progress, calibration.IsPointComplete);
                if (calibration.IsWebcamCalibration) pointMotion.Present(calibration.PointIndex, calibration.Progress);
            }
            startButton.SetActive(isBoot && development);
            bool previewVisible = development && router.Webcam.ShowWebcamPreview && router.Webcam.PreviewTexture != null;
            preview.gameObject.SetActive(previewVisible);
            if (previewVisible) preview.texture = router.Webcam.PreviewTexture;
            var state = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentState : MeteorDefenseVR.Core.GameState.Boot;
            bool aimStage = isBoot || state == MeteorDefenseVR.Core.GameState.Tutorial || state == MeteorDefenseVR.Core.GameState.Practice || state == MeteorDefenseVR.Core.GameState.Playing || state == MeteorDefenseVR.Core.GameState.BossMeteor;
            aimStage |= simulation && lobby != null && (lobby.Phase == HangarLobbyController.LobbyPhase.Menu || lobby.Phase == HangarLobbyController.LobbyPhase.Difficulty || lobby.Phase == HangarLobbyController.LobbyPhase.Records || lobby.Phase == HangarLobbyController.LobbyPhase.Standby);
            gazeRect.gameObject.SetActive(controls.ShowGazeCursor && !simulation && !calibrating && !paused && !settingsOpen && aimStage);
            Camera camera = Camera.main;
            if (camera != null)
            {
                Ray ray = router.GetGazeRay();
                Vector3 p = camera.WorldToViewportPoint(ray.GetPoint(10));
                gazeRect.anchorMin = gazeRect.anchorMax = new Vector2(Mathf.Clamp01(p.x), Mathf.Clamp01(p.y));
                gazeText.color = router.IsTrackingValid ? Color.cyan : Color.gray;
            }
            if (Time.unscaledTime < nextTextUpdate) return;
            nextTextUpdate = Time.unscaledTime + .2f;
            if (!development && !operatorUi) return; // No hidden debug-string allocations in Public.
            status.text = router.ActiveMode.ToString().ToUpperInvariant() + "\n" + (string.IsNullOrEmpty(router.Notice) ? router.Webcam.Status : router.Notice);
            compactStatus.text = "INPUT: " + router.ActiveMode.ToString().ToUpperInvariant() + (string.IsNullOrEmpty(router.Notice) ? "" : "\n" + router.Notice);
            debug.text = router.Webcam.ShowFaceDebug ? $"FACE / IRIS\nQuality: {router.Webcam.Confidence:F2}  Samples: {router.Webcam.SampleNumber}\nRaw eye: {router.Webcam.RawFeatures:F3}\nCalibration error: {router.Webcam.Calibration.Error:F3}" : "";
        }
        private void ToggleDeveloperMenu()
        {
            if (controls == null || !controls.ShowUi || !controls.ShowDebugUi || calibration.IsVisible ||
                GameFlowManager.Instance == null || GameFlowManager.Instance.CurrentState != MeteorDefenseVR.Core.GameState.Boot) return;
            menuOpen = !menuOpen;
        }

        private static string LoadBuildIdentity()
        {
            if (Application.isEditor) return $"BUILD {Application.version}\nEDITOR";
            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BUILD_IDENTITY.json"));
                if (!File.Exists(path)) return $"BUILD {Application.version}\nUNKNOWN";
                var identity = JsonUtility.FromJson<RuntimeBuildIdentity>(File.ReadAllText(path));
                string version = string.IsNullOrWhiteSpace(identity.buildVersion) ? Application.version : identity.buildVersion;
                string commit = string.IsNullOrWhiteSpace(identity.gitCommitShort) ? "UNKNOWN" : identity.gitCommitShort;
                return $"BUILD {version}\n{commit}";
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[BuildIdentity] Could not read BUILD_IDENTITY.json: " + exception.GetType().Name);
                return $"BUILD {Application.version}\nUNKNOWN";
            }
        }

        [Serializable]
        private sealed class RuntimeBuildIdentity
        {
            public string buildVersion;
            public string gitCommitShort;
        }

        private void OnDisable() { menuOpen = settingsOpen = wasBoot = wasPaused = false; if (router != null) router.UiModalOpen = false; }
        private void AddModeButton(string text, PcGazeMode mode, float y) => Button(panel.transform, mode.ToString(), text, new Vector2(16, -y), new Vector2(328, 38), () =>
        {
            if (mode == PcGazeMode.WebcamGaze && !router.Webcam.Calibration.IsCalibrated && GameFlowManager.Instance != null && GameFlowManager.Instance.CurrentState != MeteorDefenseVR.Core.GameState.Boot)
                calibration.RetryWebcam();
            else router.SetMode(mode);
            menuOpen = false;
        });
        private static void DockBottom(GameObject target, bool right, float inset, float y)
        {
            var rect = target.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(right ? 1f : 0f, 0f);
            rect.anchoredPosition = new Vector2(right ? -inset : inset, y);
        }
        private UnityEngine.UI.Button Button(Transform parent, string name, string text, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1), position, size);
            Background(rect.gameObject, new Color(.025f, .17f, .23f, 1));
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
            button.onClick.AddListener(action);
            TextMeshProUGUI label = Label(rect, "Label", text, new Vector2(6, -2), size - new Vector2(12, 4), size.x < 170 ? 18 : 20);
            label.alignment = TextAlignmentOptions.Center;
            rect.gameObject.AddComponent<UIInteractionAnimator>();
            return button;
        }
        private TextMeshProUGUI Label(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize)
        {
            var target = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1), position, size).gameObject;
            // Assign the authored static font before TMP Awake/OnEnable uses the default font.
            target.SetActive(false);
            var label = target.AddComponent<TextMeshProUGUI>();
            Style(label, fontSize); label.text = text;
            target.SetActive(true);
            return label;
        }
        private void Style(TextMeshProUGUI text, float size) { text.font = font; text.fontSize = size; text.enableAutoSizing = false; text.color = new Color(.68f, .96f, 1f); text.raycastTarget = false; }
        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static void Background(GameObject target, Color color, bool raycast = true) { var image = target.AddComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = raycast; }
    }
}
using System;
using System.IO;
