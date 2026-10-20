using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Meteor;
using NUnit.Framework;
using UnityEngine;

namespace MeteorDefenseVR.Tests
{
    public sealed class TurretRecoilTests
    {
        private GameObject root;
        private LaserWeapon weapon;
        private TurretAimingSystem aiming;
        private Transform barrel, muzzle;
        private GazeLockOnTarget target;
        private Vector3 rest;

        [SetUp] public void Setup()
        {
            root=new GameObject("RecoilFixture"); weapon=root.AddComponent<LaserWeapon>();
            var yaw=new GameObject("Yaw").transform; yaw.SetParent(root.transform,false);
            var pitch=new GameObject("Pitch").transform; pitch.SetParent(yaw,false);
            barrel=new GameObject("Barrel").transform;barrel.SetParent(pitch,false);barrel.localPosition=Vector3.forward*.23f;rest=barrel.localPosition;
            muzzle=new GameObject("Muzzle").transform;muzzle.SetParent(pitch,false);muzzle.localPosition=Vector3.forward*.48f;
            weapon.Configure(null,new[]{muzzle},System.Array.Empty<LaserBeamView>());
            aiming=root.AddComponent<TurretAimingSystem>();
            aiming.Configure(null,weapon,root.transform,new TurretAimingSystem.TurretMount{root=root.transform,yawPivot=yaw,pitchPivot=pitch,muzzle=muzzle},null,null,null);
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name="Target";go.transform.SetParent(root.transform,false);go.transform.localPosition=Vector3.forward*20;
            var meteor=go.AddComponent<MeteorController>();meteor.Configure(MeteorType.Normal,100,0,1,1,1);meteor.Spawn();
            target=go.AddComponent<GazeLockOnTarget>();target.Configure(meteor,.1f);
        }
        [TearDown] public void Cleanup(){Object.DestroyImmediate(root);}
        private bool Shoot()
        {
            target.Advance(1);for(int i=0;i<100&&(!aiming.IsReadyFor(target)||!aiming.HasTracked(target));i++)aiming.Tick(.01f);
            weapon.TickCooldown(1);return weapon.TryFire(target);
        }
        private float Offset=>rest.z-barrel.localPosition.z;

        [Test] public void IdleAndRejectedFireNeverMoveBarrel()
        {
            Assert.That(weapon.TryFire(target),Is.False);Assert.That(weapon.TryFire(null),Is.False);
            target.Meteor.ReceiveDamage(1);aiming.Tick(.1f);
            Assert.That(barrel.localPosition,Is.EqualTo(rest));
        }
        [Test] public void RapidActualShotsStayBoundedAndReturnExactly()
        {
            for(int i=0;i<30;i++)
            {
                Assert.That(Shoot(),Is.True);aiming.Tick(.035f);
                Assert.That(Offset,Is.GreaterThan(0).And.LessThanOrEqualTo(TurretAimingSystem.RecoilDistance+.00001f));
                Assert.That(barrel.localPosition.x,Is.EqualTo(rest.x));Assert.That(barrel.localPosition.y,Is.EqualTo(rest.y));
            }
            aiming.Tick(.3f);Assert.That(barrel.localPosition,Is.EqualTo(rest));
            Assert.That(muzzle.localPosition,Is.EqualTo(Vector3.forward*.48f));
        }
        [Test] public void LethalHitReleasesAimWithoutErasingTheFiredStroke()
        {
            weapon.DamagePerShot=100;Assert.That(Shoot(),Is.True);
            Assert.That(aiming.CurrentCandidate,Is.Null);
            aiming.Tick(TurretAimingSystem.RecoilKickSeconds);Assert.That(Offset,Is.EqualTo(.09f).Within(.00001f));
            weapon.ResetWeapon();Assert.That(barrel.localPosition,Is.EqualTo(rest));
        }
        [Test] public void ExplicitResetAndPooledTargetReuseCannotRetainDisplacement()
        {
            Assert.That(Shoot(),Is.True);aiming.Tick(.04f);Assert.That(Offset,Is.GreaterThan(0));
            // EditMode does not dispatch normal MonoBehaviour disable callbacks.
            // The real disable/re-enable path is exercised by TurretRecoilProof in play mode.
            aiming.ResetTurrets();Assert.That(barrel.localPosition,Is.EqualTo(rest));
            target.Meteor.ResetMeteor(false);target.Meteor.Spawn();
            aiming.Tick(.2f);Assert.That(barrel.localPosition,Is.EqualTo(rest));
            Assert.That(Shoot(),Is.True);aiming.Tick(.3f);Assert.That(barrel.localPosition,Is.EqualTo(rest));
        }
        [Test] public void AuxiliaryAndPowerBeamUseTheSameActualDischargePath()
        {
            Assert.That(weapon.TryAuxiliaryHit(null,1),Is.False);aiming.Tick(.045f);Assert.That(Offset,Is.Zero);
            Assert.That(weapon.TryAuxiliaryHit(target.Meteor,1),Is.True);aiming.Tick(.045f);Assert.That(Offset,Is.GreaterThan(0));
            weapon.ResetWeapon();
            Assert.That(weapon.TryPowerBeam(new[]{target.Meteor},1,1,1),Is.EqualTo(1));
            aiming.Tick(.045f);Assert.That(Offset,Is.GreaterThan(0));
            aiming.ResetTurrets();Assert.That(barrel.localPosition,Is.EqualTo(rest));
        }
    }
}
