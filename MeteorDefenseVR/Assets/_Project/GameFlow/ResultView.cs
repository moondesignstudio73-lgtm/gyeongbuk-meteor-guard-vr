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
            string heading = snapshot.MissionFailed ? "다시 도전해요!" : snapshot.AllStagesCleared ? "모든 임무 완료!\n지구를 지켜냈어요!" : "이번 임무의 기록";
            string progress = snapshot.CampaignMode
                ? $"{Difficulty.DifficultyConfig.Label(snapshot.Difficulty)} · 구역 {snapshot.MaxStage:D2}\n파괴한 운석 {snapshot.DestroyedMeteors} / 210\n파괴한 보스 {snapshot.BossesDestroyed}\n"
                : string.Empty;
            string outcome = snapshot.MissionFailed ? "기지로 돌아가 다시 도전해요." : "지구 방어 성공!";
            return $"{heading}\n\n{progress}총점 {snapshot.Score:N0}\n파괴한 운석 {snapshot.DestroyedMeteors}\n명중률 {snapshot.AccuracyPercent}%\n받은 피해 {snapshot.DamageTaken}%\n플레이 시간 {snapshot.PlayTime:0.0}초\n최고 구역 {snapshot.MaxStage}\n최종 등급 {rank}\n\n{outcome}";
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
            // Transparent cockpit fractures are closer to the camera than this world-space
            // panel. Draw the result backing, then its legacy/TMP text, over that damage.
            if (visualFrame != null)
                foreach (Renderer renderer in visualFrame.GetComponentsInChildren<Renderer>(true))
                    renderer.sortingOrder = 100;
            if (resultText != null)
                foreach (Renderer renderer in resultText.GetComponentsInChildren<Renderer>(true))
                    renderer.sortingOrder = 101;
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
