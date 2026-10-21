using System.Text;
using UnityEngine;

namespace MeteorDefenseVR.PowerUps
{
    [DisallowMultipleComponent]
    public sealed class PowerUpHudView : MonoBehaviour
    {
        [SerializeField] private PowerUpManager manager;
        [SerializeField] private TextMesh activeBuffsText;
        [SerializeField] private TextMesh acquiredText;
        private float acquiredRemaining;
        private bool integrated;
        public static string PlayerName(PowerUpType type) => type switch
        {
            PowerUpType.Heal => "선체 회복", PowerUpType.Shield => "방어막",
            PowerUpType.DamageBoost => "공격력 강화", PowerUpType.RapidFire => "속사",
            PowerUpType.MultiLaser => "다중 레이저", PowerUpType.Overdrive => "과부하 레이저",
            PowerUpType.SlowTime => "운석 감속", PowerUpType.EMP => "전자기 충격파", _ => "능력"
        };
        public void SetIntegratedPresentation(bool value)
        {
            integrated = value;
            if (activeBuffsText != null) activeBuffsText.gameObject.SetActive(!value);
            if (acquiredText != null) acquiredText.gameObject.SetActive(!value);
        }

        public void Configure(PowerUpManager source, TextMesh buffs, TextMesh acquired)
        {
            Unbind(); manager = source; activeBuffsText = buffs; acquiredText = acquired; Bind(); Refresh();
        }
        private void OnEnable() { Bind(); Refresh(); }
        private void OnDisable() => Unbind();
        private void Update()
        {
            if (acquiredRemaining <= 0f || acquiredText == null) return;
            acquiredRemaining -= Time.unscaledDeltaTime;
            Color color = acquiredText.color; color.a = Mathf.Clamp01(acquiredRemaining * 2f); acquiredText.color = color;
            if (acquiredRemaining <= 0f) acquiredText.text = string.Empty;
        }
        private void Bind()
        {
            if (manager == null) return;
            manager.BuffsChanged -= Refresh; manager.BuffsChanged += Refresh;
            manager.PowerUpAcquired -= Acquired; manager.PowerUpAcquired += Acquired;
        }
        private void Unbind()
        {
            if (manager == null) return;
            manager.BuffsChanged -= Refresh; manager.PowerUpAcquired -= Acquired;
        }
        private void Acquired(PowerUpType type)
        {
            if (acquiredText != null)
            {
                PowerUpBalance value = manager.GetDefinition(type);
                acquiredText.text = value.DisplayName + " ACQUIRED";
                acquiredText.color = value.Color; acquiredRemaining = 1.15f;
            }
            Refresh();
        }
        public void Refresh()
        {
            if (integrated || activeBuffsText == null || manager == null) return;
            var builder = new StringBuilder(96);
            foreach (PowerUpManager.ActiveBuffView buff in manager.ActiveBuffs)
            {
                if (builder.Length > 0) builder.Append('\n');
                builder.Append('[').Append(buff.Icon).Append(' ').Append(buff.Code).Append(' ');
                if (buff.Type == PowerUpType.Shield && buff.ShieldHits >= 0) builder.Append(buff.ShieldHits);
                else builder.Append(buff.Remaining.ToString("00.0"));
                builder.Append(']');
            }
            activeBuffsText.text = builder.ToString();
        }
    }
}
