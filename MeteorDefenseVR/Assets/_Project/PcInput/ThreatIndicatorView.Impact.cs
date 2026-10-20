using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.PowerUps;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    public sealed partial class ThreatIndicatorView
    {
        private ThreatPulseGraphic collisionWarning, impactRipple;
        private UnityEngine.UI.Image directionLamp;
        private PlayerHealth playerHealth;
        private PowerUpManager powerUps;
        private float impactAge = 10, arrivalAge = 10;
        private Vector3 impactWorld, arrivalWorld;
        private bool blockedImpact;
        private int blockedFrame = -1;
        public MeteorController MostUrgentThreat => audioThreat;
        public bool HasCollisionWarning => collisionWarning != null && collisionWarning.gameObject.activeSelf;

        private void BuildImpactPresentation()
        {
            collisionWarning = BuildPulse("CollisionCountdown"); impactRipple = BuildPulse("ImpactHexRipple");
            var lamp = FlightLoadingVisual.Rect("DirectionalThreatLight", root, Vector2.zero, new Vector2(68, 4));
            directionLamp = lamp.gameObject.AddComponent<UnityEngine.UI.Image>(); directionLamp.raycastTarget = false;
            playerHealth = FindAnyObjectByType<PlayerHealth>(); powerUps = PowerUpManager.Instance;
            BindImpactPresentation();
        }
        private ThreatPulseGraphic BuildPulse(string name)
        {
            var rect = FlightLoadingVisual.Rect(name, root, Vector2.zero, new Vector2(240, 240));
            var graphic = rect.gameObject.AddComponent<ThreatPulseGraphic>(); graphic.raycastTarget = false; return graphic;
        }
        private void BindImpactPresentation()
        {
            UnbindImpactPresentation();
            if (spawner != null) spawner.MeteorSpawned += Incoming;
            if (playerHealth != null) playerHealth.ShipImpacted += Impact;
            if (powerUps != null) powerUps.ShieldBlocked += Blocked;
        }
        private void UnbindImpactPresentation()
        {
            if (spawner != null) spawner.MeteorSpawned -= Incoming;
            if (playerHealth != null) playerHealth.ShipImpacted -= Impact;
            if (powerUps != null) powerUps.ShieldBlocked -= Blocked;
        }
        private void Incoming(MeteorController meteor, int _, int __) { arrivalAge = 0; arrivalWorld = meteor.transform.position; }
        private void Blocked() { blockedFrame = Time.frameCount; }
        private void Impact(Vector3 position, bool applied)
        {
            if (!applied && blockedFrame != Time.frameCount) return;
            impactWorld = position; impactAge = 0; blockedImpact = !applied;
        }
        private Vector2 SignalPosition(Vector3 position, bool edgeOnly)
        {
            Vector3 local = Quaternion.Inverse(view.Rotation) * (position - view.Position);
            Vector3 point = view.Camera.WorldToViewportPoint(position);
            if (!edgeOnly && point.z > 0 && point.x > .15f && point.x < .85f && point.y > .2f && point.y < .8f)
                return new Vector2((point.x - .5f) * root.rect.width, (point.y - .5f) * root.rect.height);
            Vector2 edge = ThreatProjection.Edge(local, view.Camera.aspect);
            return new Vector2(edge.x * (root.rect.width * .5f - 145), edge.y * (root.rect.height * .5f - 145));
        }
        private void TickImpactPresentation()
        {
            if (collisionWarning == null) return;
            impactAge += Time.deltaTime; arrivalAge += Time.deltaTime;
            bool imminent = !VisualsSuppressed && audioThreat != null && audioArrival <= .8f;
            collisionWarning.gameObject.SetActive(imminent);
            if (imminent)
            {
                collisionWarning.rectTransform.anchoredPosition = SignalPosition(audioThreat.transform.position, false);
                collisionWarning.Present(Mathf.Lerp(45, 100, Mathf.Clamp01(audioArrival / .8f)), .8f, new Color(1,.45f,.1f));
            }
            impactRipple.gameObject.SetActive(!VisualsSuppressed && impactAge < .45f);
            if (impactAge < .45f)
            {
                impactRipple.rectTransform.anchoredPosition = SignalPosition(impactWorld, false);
                impactRipple.Present(Mathf.Lerp(24, 130, impactAge / .45f), .7f * (1 - impactAge / .45f), blockedImpact ? new Color(.15f,1,.85f) : new Color(1,.4f,.12f));
            }
            directionLamp.gameObject.SetActive(!VisualsSuppressed && (imminent || arrivalAge < .35f));
            if (directionLamp.gameObject.activeSelf)
            {
                directionLamp.rectTransform.anchoredPosition = SignalPosition(imminent ? audioThreat.transform.position : arrivalWorld, true);
                directionLamp.color = imminent ? new Color(1,.45f,.1f,.8f) : new Color(.2f,.9f,1,.55f * (1 - arrivalAge / .35f));
            }
        }
    }
}
