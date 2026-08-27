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

        private void OnEnable()
        {
            if (popupText == null) popupText = GetComponent<TextMesh>();
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
            if (remainingTime <= 0f) SetVisible(false);
        }

        public void Configure(LaserWeapon laserWeapon, TextMesh text)
        {
            if (weapon != null) weapon.ScorePopupRequested -= Show;
            weapon = laserWeapon;
            popupText = text;
            if (isActiveAndEnabled && weapon != null) weapon.ScorePopupRequested += Show;
        }

        public void Show(Vector3 worldPosition, int score)
        {
            if (popupText == null) return;
            transform.position = worldPosition;
            popupText.text = "+" + Mathf.Max(0, score).ToString("N0");
            SetVisible(true);
            remainingTime = displayDuration;
        }

        private void SetVisible(bool visible)
        {
            if (popupText == null) return;
            Renderer textRenderer = popupText.GetComponent<Renderer>();
            if (textRenderer != null) textRenderer.enabled = visible;
        }
    }
}
