using MeteorDefenseVR.Meteor;
using UnityEngine;

namespace MeteorDefenseVR.Visual
{
    [DisallowMultipleComponent]
    public sealed class MeteorMotionTrail : MonoBehaviour
    {
        [SerializeField] private MeteorController meteor;
        [SerializeField] private TrailRenderer trail;
        public TrailRenderer Trail => trail;
        public void Configure(MeteorController controller,TrailRenderer renderer){Unbind();meteor=controller;trail=renderer;Bind();Clear();}
        private void OnEnable(){Bind();Clear();}
        private void OnDisable(){Unbind();Clear();}
        private void Bind(){if(meteor==null)return;meteor.Spawned-=Spawned;meteor.ResetPerformed-=Reset;meteor.Destroyed-=Destroyed;meteor.Spawned+=Spawned;meteor.ResetPerformed+=Reset;meteor.Destroyed+=Destroyed;}
        private void Unbind(){if(meteor==null)return;meteor.Spawned-=Spawned;meteor.ResetPerformed-=Reset;meteor.Destroyed-=Destroyed;}
        private void Spawned(MeteorController _){Clear();if(trail!=null)trail.emitting=true;}
        private void Reset(MeteorController _)=>Clear();
        private void Destroyed(MeteorController _)=>Clear();
        private void Clear(){if(trail==null)return;trail.emitting=false;trail.Clear();}
    }
}
