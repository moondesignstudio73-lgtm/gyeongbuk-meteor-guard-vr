using MeteorDefenseVR.Combat;
using UnityEngine;

namespace MeteorDefenseVR.Visual
{
    // Presentation only: billboard the existing reticle without changing gaze dwell or hit testing.
    [DefaultExecutionOrder(500)]
    public sealed class PremiumReticlePresentation : MonoBehaviour
    {
        [SerializeField] private GazeLockOnTarget target;
        [SerializeField] private TextMesh label;
        private Camera view;
        private int previousPercent = -1;
        private string progressLabel;
        public void Configure(GazeLockOnTarget t,TextMesh text){target=t;label=text;}
        private void LateUpdate()
        {
            if(view==null)view=Camera.main;
            if(view!=null)
            {
                transform.rotation=view.transform.rotation;
                // Follow the front of a rotating asteroid, not its rotating local -Z face.
                transform.position=transform.parent.position-view.transform.forward*(transform.parent.lossyScale.x*.67f);
            }
            if(target!=null&&label!=null&&target.State==LockOnState.Focusing)
            {
                int percent = Mathf.RoundToInt(target.Progress * 100);
                if (percent != previousPercent)
                {
                    previousPercent = percent;
                    progressLabel = percent < 8 ? "TRACKING" : "LOCKING  " + percent + "%";
                }
                if (label.text != progressLabel) label.text = progressLabel;
            }
        }
        private void OnDisable() { previousPercent = -1; progressLabel = null; }
    }
}
