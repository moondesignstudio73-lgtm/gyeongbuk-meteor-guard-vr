using UnityEngine;

namespace MeteorDefenseVR.Meteor
{
    // Scene unload destroys the cached meteor with its owner; Operator Reset retains it.
    [DisallowMultipleComponent]
    public sealed class PreparedMeteorMember : MonoBehaviour
    {
        public Component Owner { get; internal set; }
    }
}
