using MeteorDefenseVR.Core;
using MeteorDefenseVR.Progression;
using TMPro;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    public sealed partial class SfPresentationDirector
    {
        private FlightLoadingVisual loadingVisual;
        private RectTransform loadingOverlay;
        private RectTransform stageCard, stageRule;
        private CanvasGroup stageGroup;
        private TextMeshProUGUI stageTitle, stageSubtitle;
        private float stageAge = 10f;
        public float StageTimeRemaining => Mathf.Max(0, 1.6f-stageAge);
        public bool IsStagePresenting => stageCard != null && stageCard.gameObject.activeSelf;
        public int PresentedStage { get; private set; }
        public int StagePresentationCount { get; private set; }

        private void BuildTransitions(TMP_FontAsset font)
        {
            loadingOverlay = FlightLoadingVisual.Rect("FlightPreparationCanvas", view.transform, Vector2.zero, new Vector2(1280, 720));
            var loadingCanvas = loadingOverlay.gameObject.AddComponent<Canvas>();
            loadingCanvas.renderMode = RenderMode.WorldSpace; loadingCanvas.worldCamera = view; loadingCanvas.sortingOrder = 240;
            loadingOverlay.localPosition = Vector3.forward * .4f;
            var veil = Image("PreparationVeil", loadingOverlay, Vector2.zero, new Vector2(4000, 3000));
            veil.color = FlightUiTheme.Ink;
            loadingVisual = FlightLoadingVisual.Create(loadingOverlay, font);
            loadingOverlay.gameObject.SetActive(false);
            stageCard = FlightLoadingVisual.Rect("StageArrival", overlay, Vector2.zero, new Vector2(760, 164));
            stageGroup = stageCard.gameObject.AddComponent<CanvasGroup>(); stageGroup.blocksRaycasts = false; stageGroup.interactable = false;
            var plate = Image("StageBackdrop", stageCard, Vector2.zero, new Vector2(760, 164)); plate.color = new Color(.005f, .025f, .055f, .86f);
            stageTitle = FlightLoadingVisual.Label("StageTitle", stageCard, font, "스테이지 1", 50, new Vector2(0, 14), new Vector2(700, 72), new Color(.85f, 1, 1));
            stageSubtitle = FlightLoadingVisual.Label("StageSubtitle", stageCard, font, "지구를 지켜 주세요!", 20, new Vector2(0, -44), new Vector2(700, 35), new Color(.45f, .9f, .95f));
            var rule = Image("StageRule", stageCard, new Vector2(0, 81), new Vector2(0, 2)); rule.color = Cyan; stageRule = rule.rectTransform;
            rule = Image("StageLowerRule", stageCard, new Vector2(0, -81), new Vector2(680, 1)); rule.color = new Color(.14f, .9f, .95f, .35f);
            stageCard.gameObject.SetActive(false);
        }
        private void TransitionArrival(GameState state)
        {
            if (stageCard == null) return;
            stageCard.gameObject.SetActive(false);
            stageAge = 10f;
            if (state == GameState.Boot || state == GameState.Reset)
            { StagePresentationCount = 0; PresentedStage = 0; }
            if (state != GameState.Playing) return;
            var progression = StageProgressionController.Instance;
            bool campaign = progression != null && progression.IsCampaign;
            PresentedStage = campaign ? progression.CurrentStage : 0;
            stageTitle.text = campaign ? $"스테이지 {PresentedStage}" : "출격!";
            stageSubtitle.text = PresentedStage > 1 ? "새로운 운석 무리에 대비하세요" : "지구를 지켜 주세요!";
            stageAge = 0; stageGroup.alpha = 0; stageCard.gameObject.SetActive(true); StagePresentationCount++;
        }
        private void TickTransitions(float dt)
        {
            loadingOverlay.gameObject.SetActive(booting);
            if (booting)
            {
                loadingOverlay.localScale = Vector3.one * (2f * .4f * Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad) / 720f * Mathf.Min(1f,view.aspect/(1280f/720f)));
                float progress = IsPreparing ? .4f + .6f * preparation.CompletedSteps / preparation.TotalSteps : 1;
                loadingVisual.Present(progress, IsPreparing ? preparation.Stage : resetting ? "다음 임무를 준비해요" : "출격 준비 완료!", dt);
            }
            if (!IsStagePresenting) return;
            stageAge += dt;
            stageGroup.alpha = Mathf.Clamp01(stageAge / .18f) * Mathf.Clamp01((1.6f - stageAge) / .4f);
            float appear = WorldTextMotion.Ease(stageAge / .3f);
            // Settle just above the reticle before the first incoming meteor arrives.
            stageCard.anchoredPosition = new Vector2(0, Mathf.Lerp(-20, 0, appear) + 86 * WorldTextMotion.Ease((stageAge - .55f) / .4f));
            stageCard.localScale = Vector3.one * Mathf.Lerp(1.035f, 1f, appear);
            stageRule.sizeDelta = new Vector2(680 * appear, 2);
            if (stageAge >= 1.6f) stageCard.gameObject.SetActive(false);
        }
    }
}
