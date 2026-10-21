using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.PowerUps;
using TMPro;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    // Reuses the existing side panel so its target-avoidance placement also covers these indicators.
    [DisallowMultipleComponent]
    public sealed class PowerUpWeaponFeedback : MonoBehaviour
    {
        private PowerUpManager manager;
        private LaserWeapon weapon;
        private TMP_Text label, summary;
        private RectTransform panel, graphics;
        private UnityEngine.UI.Image charge, first, second;
        private float pulse;
        private int shotCount;
        private bool bound;
        public float RapidRemaining { get; private set; }
        public float MultiRemaining { get; private set; }
        public bool Visible { get; private set; }
        public float AuxiliaryPulse => pulse;
        public int LastAdditionalShots => shotCount;
        public TMP_Text Readout => label;
        public float Charge => charge != null ? charge.rectTransform.sizeDelta.x / 124f : 0;
        public bool EndingSoon => (RapidRemaining > 0 && RapidRemaining <= 2) || (MultiRemaining > 0 && MultiRemaining <= 2);

        public void Configure(TMP_Text existing, TMP_Text threat)
        {
            label = existing; summary = threat; panel = (RectTransform)transform;
            manager = PowerUpManager.Instance; weapon = FindAnyObjectByType<LaserWeapon>();
            label.font = threat.font; label.fontSharedMaterial = threat.fontSharedMaterial;
            label.fontSize = 30; label.enableAutoSizing = false; label.lineSpacing = 4;
            label.alignment = TextAlignmentOptions.TopLeft; label.color = Color.white;
            label.textWrappingMode = TextWrappingModes.NoWrap; label.raycastTarget = false;
            var rect = label.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(0, 0);
            rect.pivot = new Vector2(0, 0); rect.anchoredPosition = new Vector2(16, 10); rect.sizeDelta = new Vector2(210, 110);
            graphics = new GameObject("WeaponFeedbackIndicators", typeof(RectTransform)).GetComponent<RectTransform>();
            graphics.SetParent(panel, false); graphics.anchorMin = graphics.anchorMax = Vector2.zero;
            graphics.anchoredPosition = Vector2.zero; graphics.sizeDelta = Vector2.zero;
            Make("ChargeTrack", new Vector2(238, 94), new Vector2(124, 10)).color = new Color(.10f, .22f, .26f);
            charge = Make("RapidCharge", new Vector2(238, 94), new Vector2(124, 10));
            first = Make("AuxiliaryMuzzle1", new Vector2(244, 44), new Vector2(44, 12));
            second = Make("AuxiliaryMuzzle2", new Vector2(310, 44), new Vector2(44, 12));
            Bind(); Tick(0);
        }
        private UnityEngine.UI.Image Make(string name, Vector2 position, Vector2 size)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            image.transform.SetParent(graphics, false); image.raycastTarget = false;
            image.material = panel.GetComponent<UnityEngine.UI.Image>().material;
            var rect = image.rectTransform; rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = new Vector2(0, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size; return image;
        }
        private void Bind() { if (!bound && manager != null) { manager.MultiLaserFired += Fired; bound = true; } }
        private void OnEnable() => Bind();
        private void OnDisable()
        {
            if (bound && manager != null) manager.MultiLaserFired -= Fired;
            bound = false; pulse = 0; shotCount = 0; Present(false);
        }
        private void Fired(int count) { if (manager != null && !manager.TimersPaused && Time.timeScale > 0) { pulse = .22f; shotCount = count; } }
        private void Update() => Tick(Time.unscaledDeltaTime);
        public void Tick(float dt)
        {
            if (label == null || manager == null) return;
            RapidRemaining = MultiRemaining = 0;
            for (int i = 0; i < manager.ActiveBuffs.Count; i++)
            { var buff = manager.ActiveBuffs[i]; if (buff.Type == PowerUpType.RapidFire) RapidRemaining = buff.Remaining; if (buff.Type == PowerUpType.MultiLaser) MultiRemaining = buff.Remaining; }
            var state = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentState : GameState.Boot;
            bool combat = state == GameState.Playing || state == GameState.BossMeteor;
            Present(combat && (RapidRemaining > 0 || MultiRemaining > 0));
            if (!Visible || MultiRemaining <= 0) { pulse = 0; shotCount = 0; }
            if (!manager.TimersPaused && Time.timeScale > 0) pulse = Mathf.Max(0, pulse - Mathf.Max(0, dt));
            if (!Visible) return;
            label.SetText("속사 {0:0}초\n다중 {1:0}초", Mathf.Ceil(RapidRemaining), Mathf.Ceil(MultiRemaining));
            charge.rectTransform.sizeDelta = new Vector2(RapidRemaining > 0 ? 124 * weapon.ChargeNormalized : 0, 10);
            charge.color = Tint(RapidRemaining);
            first.color = Color.Lerp(Tint(MultiRemaining) * new Color(1, 1, 1, .5f), Color.white, pulse / .22f);
            second.color = Color.Lerp(Tint(MultiRemaining) * new Color(1, 1, 1, .5f), Color.white, shotCount >= 2 ? pulse / .22f : 0);
            label.color = EndingSoon ? new Color(1, .78f, .4f) : Color.white;
        }
        private static Color Tint(float time) => time <= 0 ? new Color(.15f, .28f, .3f) : time <= 2 ? new Color(1, .65f, .22f) : new Color(.18f, .92f, 1);
        private void Present(bool visible)
        {
            Visible = visible; if (label == null) return;
            label.gameObject.SetActive(visible); graphics.gameObject.SetActive(visible);
            panel.sizeDelta = new Vector2(400, visible ? 276 : 160);
            summary.rectTransform.offsetMin = new Vector2(16, visible ? 124 : 8);
        }
    }
}
