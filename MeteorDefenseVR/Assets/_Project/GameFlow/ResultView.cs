using UnityEngine;

namespace MeteorDefenseVR.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class ResultView : MonoBehaviour
    {
        [SerializeField] private ResultController controller;
        [SerializeField] private TextMesh resultText;
        [SerializeField] private GameObject visualFrame;

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        public void Configure(ResultController resultController, TextMesh text)
        {
            Unsubscribe();
            controller = resultController;
            resultText = text;
            Subscribe();
            if (controller != null && controller.IsVisible) Show(controller.Snapshot, controller.Rank);
            else Hide();
        }

        public void Configure(ResultController resultController, TextMesh text, GameObject frame)
        {
            visualFrame = frame;
            Configure(resultController, text);
        }

        public static string Format(GameSessionSnapshot snapshot, string rank)
        {
            snapshot ??= new GameSessionSnapshot();
            string heading = snapshot.MissionFailed ? "MISSION FAILED" : snapshot.AllStagesCleared ? "MISSION COMPLETE\nALL THREAT LEVELS CLEARED" : "MISSION RESULT";
            string progress = snapshot.CampaignMode
                ? $"{Difficulty.DifficultyConfig.Code(snapshot.Difficulty)} · STAGE {snapshot.MaxStage:D2}\nASTEROIDS {snapshot.DestroyedMeteors} / 210\nBOSSES    {snapshot.BossesDestroyed}\n"
                : string.Empty;
            string outcome = snapshot.MissionFailed ? "RETRY STAGE · RESTART MISSION · RETURN TO BASE" : "지구 방어 성공!";
            return $"{heading}\n\n{progress}TOTAL SCORE {snapshot.Score:N0}\nDESTROYED   {snapshot.DestroyedMeteors}\nACCURACY    {snapshot.AccuracyPercent}%\nDAMAGE      {snapshot.DamageTaken}%\nCLEAR TIME  {snapshot.PlayTime:0.0}s\nMAX STAGE   {snapshot.MaxStage}\nFINAL RANK  {rank}\n\n{outcome}";
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled || controller == null) return;
            controller.ResultShown -= Show;
            controller.ResultHidden -= Hide;
            controller.ResultShown += Show;
            controller.ResultHidden += Hide;
        }

        private void Unsubscribe()
        {
            if (controller == null) return;
            controller.ResultShown -= Show;
            controller.ResultHidden -= Hide;
        }

        private void Show(GameSessionSnapshot snapshot, string rank)
        {
            if (resultText != null) resultText.text = Format(snapshot, rank);
            if (visualFrame != null) visualFrame.SetActive(true);
        }

        private void Hide()
        {
            if (resultText != null) resultText.text = string.Empty;
            if (visualFrame != null) visualFrame.SetActive(false);
        }
    }
}
