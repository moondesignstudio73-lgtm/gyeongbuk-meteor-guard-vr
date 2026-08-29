using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using UnityEngine;

namespace MeteorDefenseVR.Visual
{
    [DisallowMultipleComponent]
    public sealed class CinematicEnvironmentView : MonoBehaviour
    {
        [Header("World")]
        [SerializeField] private Transform earth;
        [SerializeField] private Transform starDust;
        [SerializeField] private Light spaceKeyLight;

        [Header("Cockpit and Hangar")]
        [SerializeField] private GameObject cockpitRoot;
        [SerializeField] private GameObject missionHudRoot;
        [SerializeField] private GameObject[] hangarObjects;
        [SerializeField] private Light[] cockpitAccentLights;

        [Header("Cinematic Alert")]
        [SerializeField] private GameObject alertRoot;
        [SerializeField] private TextMesh alertText;
        [SerializeField] private Renderer alertFrame;
        [SerializeField] private Color warningColor = new Color(1f, 0.12f, 0.045f, 1f);
        [SerializeField] private Color missionColor = new Color(0.25f, 1f, 0.42f, 1f);
        [SerializeField] private Color cyanColor = new Color(0.18f, 0.9f, 1f, 1f);
        [SerializeField, Min(0.1f)] private float introDuration = 3f;
        [SerializeField, Min(0.1f)] private float briefingDuration = 4f;

        private GameFlowManager flow;
        private MaterialPropertyBlock propertyBlock;
        private Color activeAlertColor;
        private bool alertVisible;
        private float stateElapsed;
        private Vector3 earthDestination;
        private float shieldFraction = 1f;
        private MeteorDefenseVR.Player.PlayerHealth health;
        public float IntroDuration => introDuration;
        public float BriefingDuration => briefingDuration;
        public void SetDurations(float intro, float briefing)
        { introDuration = Mathf.Max(.1f, intro); briefingDuration = Mathf.Max(.1f, briefing); }

        private void Start()
        {
            health = FindAnyObjectByType<MeteorDefenseVR.Player.PlayerHealth>();
            BindFlow();
            ApplyState(flow != null ? flow.CurrentState : GameState.Boot);
        }

        private void OnEnable() => BindFlow();

        private void OnDisable()
        {
            if (flow != null) flow.OnStateChanged -= ApplyState;
            flow = null;
        }

        private void Update()
        {
            if (Time.timeScale == 0) return;
            if (flow == null) BindFlow();
            if (flow != null)
            {
                stateElapsed += Time.unscaledDeltaTime;
                if (flow.CurrentState == GameState.Intro && stateElapsed >= introDuration)
                    flow.StartMissionBriefing();
                else if (flow.CurrentState == GameState.MissionBriefing && stateElapsed >= briefingDuration)
                    flow.StartCalibration();
            }
            if (earth != null) earth.Rotate(0f, 0.18f * Time.unscaledDeltaTime, 0f, Space.Self);
            if (earth != null) earth.position = Vector3.Lerp(earth.position, earthDestination, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 3f));
            if (starDust != null) starDust.Rotate(0f, 0.12f * Time.unscaledDeltaTime, 0f, Space.World);

            if (health != null) shieldFraction = health.MaxHealth > 0 ? health.CurrentHealth / health.MaxHealth : 1f;
            bool combat = flow != null && (flow.CurrentState == GameState.Playing || flow.CurrentState == GameState.BossMeteor);
            bool danger = combat && shieldFraction <= .3f;
            if (cockpitAccentLights != null)
                foreach (var light in cockpitAccentLights)
                    if (light != null && light.enabled)
                    {
                        light.color = danger || (flow != null && flow.CurrentState == GameState.BossMeteor) ? warningColor : cyanColor;
                        light.intensity = danger ? .7f + .2f * Mathf.Sin(Time.unscaledTime * 3f) : .65f + .035f * Mathf.Sin(Time.unscaledTime * 1.4f);
                    }

            if (!alertVisible || alertRoot == null) return;
            float pulse = (1f + Mathf.Sin(Time.unscaledTime * 5.5f) * 0.018f) *
                Mathf.SmoothStep(.96f, 1f, Mathf.Clamp01(stateElapsed / .5f));
            alertRoot.transform.localScale = Vector3.one * pulse;
            ApplyFrameColor(activeAlertColor * (0.78f + Mathf.Sin(Time.unscaledTime * 7f) * 0.18f));
        }

        public void Configure(
            Transform earthTransform,
            Transform stars,
            Light keyLight,
            GameObject cockpit,
            GameObject missionHud,
            GameObject[] hangar,
            Light[] cockpitLights,
            GameObject alert,
            TextMesh text,
            Renderer frame)
        {
            earth = earthTransform;
            starDust = stars;
            spaceKeyLight = keyLight;
            cockpitRoot = cockpit;
            missionHudRoot = missionHud;
            hangarObjects = hangar;
            cockpitAccentLights = cockpitLights;
            alertRoot = alert;
            alertText = text;
            alertFrame = frame;
            propertyBlock ??= new MaterialPropertyBlock();
            BindFlow();
            ApplyState(flow != null ? flow.CurrentState : GameState.Boot);
        }

        public void ApplyState(GameState state)
        {
            stateElapsed = 0f;
            bool hangarVisible = state == GameState.Launch || state == GameState.Countdown;
            bool cockpitVisible = state != GameState.Intro;
            bool hudVisible = state == GameState.Playing || state == GameState.BossMeteor;
            SetActive(cockpitRoot, cockpitVisible);
            SetActive(missionHudRoot, hudVisible);
            if (hangarObjects != null)
                foreach (GameObject item in hangarObjects) SetActive(item, hangarVisible);

            if (cockpitAccentLights != null)
            {
                foreach (Light light in cockpitAccentLights)
                {
                    if (light == null) continue;
                    light.enabled = cockpitVisible;
                    light.intensity = state == GameState.BossMeteor ? 1.2f : 0.65f;
                    light.color = state == GameState.BossMeteor ? warningColor : cyanColor;
                }
            }

            if (spaceKeyLight != null)
            {
                // Boss danger belongs to cockpit lights and fissures, not a red wash over Earth.
                spaceKeyLight.color = new Color(0.84f, 0.91f, 1f);
                spaceKeyLight.intensity = 2.2f;
            }

            if (earth != null)
            {
                earthDestination = state switch
                {
                    GameState.Boot => new Vector3(85f, -485f, 650f),
                    GameState.Intro => new Vector3(-50f, -480f, 650f),
                    GameState.MissionComplete => new Vector3(0f, -500f, 650f),
                    GameState.Result => new Vector3(0f, -500f, 650f),
                    _ => new Vector3(0f, -525f, 650f)
                };
            }

            switch (state)
            {
                case GameState.Intro:
                    ShowAlert("WARNING\n운석이 지구로 접근합니다", warningColor);
                    break;
                case GameState.MissionBriefing:
                    ShowAlert("MISSION\n운석을 파괴하고 지구를 지키세요", missionColor);
                    break;
                case GameState.Launch:
                case GameState.Countdown:
                case GameState.BossMeteor:
                case GameState.MissionComplete:
                    // The launch, boss HUD and completion views own their respective messages.
                    ShowAlert(string.Empty, cyanColor);
                    break;
                default:
                    ShowAlert(string.Empty, cyanColor);
                    break;
            }
        }

        private void BindFlow()
        {
            GameFlowManager next = GameFlowManager.Instance;
            if (next == flow) return;
            if (flow != null) flow.OnStateChanged -= ApplyState;
            flow = next;
            if (flow != null)
            {
                flow.OnStateChanged -= ApplyState;
                flow.OnStateChanged += ApplyState;
            }
        }

        private void ShowAlert(string message, Color color)
        {
            alertVisible = !string.IsNullOrEmpty(message);
            activeAlertColor = color;
            SetActive(alertRoot, alertVisible);
            if (alertText != null)
            {
                alertText.text = message ?? string.Empty;
                alertText.color = color;
            }
            ApplyFrameColor(color);
        }

        private void ApplyFrameColor(Color color)
        {
            if (alertFrame == null) return;
            propertyBlock ??= new MaterialPropertyBlock();
            alertFrame.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            propertyBlock.SetColor("_EmissionColor", color * 2.5f);
            alertFrame.SetPropertyBlock(propertyBlock);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active) target.SetActive(active);
        }
    }
}
