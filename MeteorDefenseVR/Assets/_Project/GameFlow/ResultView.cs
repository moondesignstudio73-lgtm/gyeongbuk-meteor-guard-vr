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
            return $"MISSION RESULT\n\nSCORE     {snapshot.Score:N0}\nDESTROYED {snapshot.DestroyedMeteors}\nACCURACY  {snapshot.AccuracyPercent}%\nRANK      {rank}\n\n지구 방어 성공!";
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
