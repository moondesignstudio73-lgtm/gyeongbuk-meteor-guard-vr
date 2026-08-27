using UnityEngine;

namespace MeteorDefenseVR.Meteor
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class MeteorDefenseLine : MonoBehaviour
    {
        private void Reset()
        {
            Collider trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            MeteorController meteor = other.GetComponentInParent<MeteorController>();
            meteor?.ReachPlayer();
        }
    }
}
