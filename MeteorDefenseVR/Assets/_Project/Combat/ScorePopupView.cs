using UnityEngine;

namespace MeteorDefenseVR.Combat
{
    [DisallowMultipleComponent]
    public sealed class ScorePopupView : MonoBehaviour
    {
        [SerializeField] private LaserWeapon weapon;
        [SerializeField] private TextMesh popupText;
        [SerializeField, Min(0.1f)] private float displayDuration = 0.8f;
        [SerializeField, Min(0f)] private float riseSpeed = 0.4f;

        private float remainingTime;
        private Vector3 baseScale;
        private Color baseColor;

        private void OnEnable()
        {
            if (popupText == null) popupText = GetComponent<TextMesh>();
            CaptureStyle();
            if (weapon != null) weapon.ScorePopupRequested += Show;
            SetVisible(false);
        }

        private void OnDisable()
        {
            if (weapon != null) weapon.ScorePopupRequested -= Show;
        }

        private void Update()
        {
            if (remainingTime <= 0f) return;
            remainingTime -= Time.deltaTime;
            transform.position += Vector3.up * (riseSpeed * Time.deltaTime);
            float normalized = Mathf.Clamp01(remainingTime / displayDuration);
            transform.localScale = baseScale * Mathf.Lerp(1.25f, 1f, normalized);
            if (popupText != null)
            {
                Color color = baseColor;
                color.a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalized * 2f));
                popupText.color = color;
            }
            if (remainingTime <= 0f) SetVisible(false);
        }

        public void Configure(LaserWeapon laserWeapon, TextMesh text)
        {
            if (weapon != null) weapon.ScorePopupRequested -= Show;
            weapon = laserWeapon;
            popupText = text;
            CaptureStyle();
            if (isActiveAndEnabled && weapon != null) weapon.ScorePopupRequested += Show;
        }

        public void Show(Vector3 worldPosition, int score)
        {
            if (popupText == null) return;
            transform.position = worldPosition;
            popupText.text = "+" + Mathf.Max(0, score).ToString("N0");
            transform.localScale = baseScale * 0.85f;
            popupText.color = baseColor;
            SetVisible(true);
            remainingTime = displayDuration;
        }

        private void CaptureStyle()
        {
            baseScale = transform.localScale.sqrMagnitude > 0f ? transform.localScale : Vector3.one;
            if (popupText != null) baseColor = popupText.color;
        }

        private void SetVisible(bool visible)
        {
            if (popupText == null) return;
            Renderer textRenderer = popupText.GetComponent<Renderer>();
            if (textRenderer != null) textRenderer.enabled = visible;
        }
    }
}
