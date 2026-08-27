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

        private GameFlowManager flow;
        private MaterialPropertyBlock propertyBlock;
        private Color activeAlertColor;
        private bool alertVisible;

        private void Start()
        {
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
            if (flow == null) BindFlow();
            if (earth != null) earth.Rotate(0f, 1.45f * Time.unscaledDeltaTime, 0f, Space.Self);
            if (starDust != null) starDust.Rotate(0f, 0.12f * Time.unscaledDeltaTime, 0f, Space.World);

            if (!alertVisible || alertRoot == null) return;
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 5.5f) * 0.018f;
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
            bool hangarVisible = state == GameState.Launch || state == GameState.Countdown;
            bool cockpitVisible = state != GameState.Boot && state != GameState.Intro;
            bool hudVisible = state == GameState.Practice || state == GameState.Playing || state == GameState.BossMeteor;
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
                    light.intensity = state == GameState.BossMeteor ? 4.2f : 2.1f;
                    light.color = state == GameState.BossMeteor ? warningColor : cyanColor;
                }
            }

            if (spaceKeyLight != null)
            {
                spaceKeyLight.color = state == GameState.BossMeteor
                    ? new Color(1f, 0.2f, 0.08f)
                    : new Color(0.46f, 0.68f, 1f);
                spaceKeyLight.intensity = state == GameState.BossMeteor ? 1.35f : 0.85f;
            }

            if (earth != null)
            {
                earth.position = state switch
                {
                    GameState.Boot => new Vector3(5.2f, 1.1f, 18f),
                    GameState.Intro => new Vector3(-5.6f, -0.4f, 18f),
                    GameState.MissionComplete => new Vector3(0f, 0.8f, 18f),
                    GameState.Result => new Vector3(-4.8f, 0.5f, 18f),
                    _ => new Vector3(-6.5f, 0.3f, 20f)
                };
            }

            switch (state)
            {
                case GameState.Intro:
                    ShowAlert("WARNING\nMETEORS APPROACHING EARTH", warningColor);
                    break;
                case GameState.MissionBriefing:
                    ShowAlert("MISSION\nDESTROY THE METEORS", missionColor);
                    break;
                case GameState.Launch:
                case GameState.Countdown:
                    ShowAlert("MISSION START", cyanColor);
                    break;
                case GameState.BossMeteor:
                    ShowAlert("WARNING\nLARGE METEOR DETECTED", warningColor);
                    break;
                case GameState.MissionComplete:
                    ShowAlert("MISSION COMPLETE\nEARTH SECURED", missionColor);
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
