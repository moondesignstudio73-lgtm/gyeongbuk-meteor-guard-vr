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
        private void Spawned(MeteorController source)
        {
            Clear(); if (trail == null) return;
            bool fast = source.MeteorType == MeteorType.Fast;
            bool large = source.MeteorType == MeteorType.Large || source.MeteorType == MeteorType.Boss;
            trail.time = fast ? .5f : large ? .3f : .14f;
            trail.widthMultiplier = fast ? .18f : large ? .32f : .12f;
            trail.startColor = fast ? new Color(.4f,.8f,1,.5f) : large ? new Color(1,.45f,.12f,.35f) : new Color(.75f,.85f,1,.22f);
            trail.endColor = Color.clear; trail.emitting = true;
        }
        private void Reset(MeteorController _)=>Clear();
        private void Destroyed(MeteorController _)=>Clear();
        private void Clear(){if(trail==null)return;trail.emitting=false;trail.Clear();}
    }
}
