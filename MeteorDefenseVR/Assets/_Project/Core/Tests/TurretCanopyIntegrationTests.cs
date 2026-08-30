using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.UI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MeteorDefenseVR.Tests
{
    public sealed class TurretCanopyIntegrationTests
    {
        private GameObject root;
        private LaserWeapon weapon;
        private TurretAimingSystem aiming;
        private Transform topMuzzle;
        private Transform bottomMuzzle;
        private LineRenderer topLine;
        private LineRenderer bottomLine;

        [SetUp]
        public void SetUp()
        {
            root=new GameObject("TurretIntegrationFixture");
            weapon=root.AddComponent<LaserWeapon>();
            var top=Mount("Top",new Vector3(0,1,0),out topMuzzle);
            var bottom=Mount("Bottom",new Vector3(0,-1,0),out bottomMuzzle);
            LaserBeamView topBeam=Beam("TopBeam",out topLine),bottomBeam=Beam("BottomBeam",out bottomLine);
            weapon.Configure(null,new[]{topMuzzle,bottomMuzzle},new[]{topBeam,bottomBeam});
            aiming=root.AddComponent<TurretAimingSystem>();
            aiming.Configure(null,weapon,root.transform,top,bottom,null,null);
        }

        [TearDown]
        public void TearDown()
        {
            foreach(var target in Object.FindObjectsByType<GazeLockOnTarget>(FindObjectsInactive.Include,FindObjectsSortMode.None))target.ForceRelease();
            if(root!=null)Object.DestroyImmediate(root);
        }

        [Test]
        public void CandidateDirectionSelectsTopBottomAndReturnsToNeutral()
        {
            GazeLockOnTarget upper=Target("Upper",new Vector3(3,7,12),MeteorType.Normal,2);
            upper.Advance(.05f);AimUntilReady(upper);
            Assert.That(aiming.SelectedTurretIndex,Is.EqualTo(0));
            Assert.That(aiming.TryGetFiringSolution(upper,out var upperSolution),Is.True);
            Assert.That(Vector3.Distance(upperSolution.PrimaryOrigin,topMuzzle.position),Is.LessThan(.001f));

            GazeLockOnTarget lower=Target("Lower",new Vector3(-3,-7,12),MeteorType.Normal,2);
            lower.Advance(.05f);AimUntilReady(lower);
            Assert.That(upper.State,Is.EqualTo(LockOnState.Idle),"Only the exclusive gaze candidate may drive the turrets");
            Assert.That(aiming.SelectedTurretIndex,Is.EqualTo(1));
            Assert.That(aiming.TryGetFiringSolution(lower,out var lowerSolution),Is.True);
            Assert.That(Vector3.Distance(lowerSolution.PrimaryOrigin,bottomMuzzle.position),Is.LessThan(.001f));

            lower.ForceRelease();aiming.Tick(.02f);
            Assert.That(aiming.CurrentCandidate,Is.Null);Assert.That(aiming.SelectedTurretIndex,Is.EqualTo(-1));
        }

        [Test]
        public void RearTargetAimsWithoutCameraDependencyAndLaserStartsAtSelectedMuzzle()
        {
            GazeLockOnTarget rear=Target("Rear",new Vector3(2,5,-14),MeteorType.Normal,2);
            rear.Advance(1);AimUntilReady(rear);
            Assert.That(aiming.SelectedTurretIndex,Is.EqualTo(0));
            Assert.That(weapon.TryFire(rear),Is.True);
            Assert.That(topLine.enabled,Is.True);
            Assert.That(Vector3.Distance(topLine.GetPosition(0),topMuzzle.position),Is.LessThan(.001f));
            Assert.That(rear.Meteor.CurrentHealth,Is.EqualTo(1));
        }

        [Test]
        public void BossUsesBothMuzzlesButAppliesOneDamageEvent()
        {
            GazeLockOnTarget boss=Target("Boss",new Vector3(0,0,15),MeteorType.Boss,4);
            boss.Advance(1);AimUntilReady(boss);
            int hits=0;weapon.Hit+=(_,__)=>hits++;
            Assert.That(weapon.TryFire(boss),Is.True);
            Assert.That(topLine.enabled&&bottomLine.enabled,Is.True);
            Assert.That(Vector3.Distance(topLine.GetPosition(0),topMuzzle.position),Is.LessThan(.001f));
            Assert.That(Vector3.Distance(bottomLine.GetPosition(0),bottomMuzzle.position),Is.LessThan(.001f));
            Assert.That(boss.Meteor.CurrentHealth,Is.EqualTo(3));Assert.That(hits,Is.EqualTo(1));
        }

        [Test]
        public void AuthoredSceneHasTwoExternalTurretsOneCanopyRendererAndBottomDamageAnchor()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity",OpenSceneMode.Single);
            TurretAimingSystem sceneAiming=Object.FindAnyObjectByType<TurretAimingSystem>();
            Assert.That(sceneAiming,Is.Not.Null);Assert.That(sceneAiming.TopMuzzle,Is.Not.Null);Assert.That(sceneAiming.BottomMuzzle,Is.Not.Null);
            Assert.That(sceneAiming.TopMuzzle.IsChildOf(sceneAiming.transform),Is.True);Assert.That(sceneAiming.BottomMuzzle.IsChildOf(sceneAiming.transform),Is.True);
            Assert.That(sceneAiming.GetComponent<TurretPlayerVisibilityGuard>(),Is.Not.Null);
            var glass=GameObject.Find("LaunchSystem").transform.Find("CockpitGeometry/PanoramicCanopy/CanopyGlass360");
            Assert.That(glass,Is.Not.Null);Assert.That(glass.GetComponent<MeshRenderer>().sharedMaterial.shader.name,Is.EqualTo("MeteorDefense/Cockpit Canopy"));
            Assert.That(glass.GetComponentsInChildren<Renderer>(true).Length,Is.EqualTo(1),"Panoramic glass must remain one batched transparent renderer");
            var damage=Object.FindAnyObjectByType<CockpitDamagePresentation>();
            Assert.That(damage.PointCount,Is.EqualTo(8));
            var bottom=GameObject.Find("LaunchSystem").transform.Find("CockpitGeometry/CockpitDamage/GlassImpact_Bottom");
            Assert.That(bottom,Is.Not.Null);Assert.That(damage.FindNearestPoint(bottom.position),Is.EqualTo(4));
        }

        [Test]
        public void ExteriorTurretsAreOffAxisShortAndHiddenOnlyFromPlayerView()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity",OpenSceneMode.Single);
            var sceneAiming=Object.FindAnyObjectByType<TurretAimingSystem>();var camera=Camera.main;
            int layer=LayerMask.NameToLayer(TurretAimingSystem.PlayerHiddenLayerName);
            Assert.That(layer,Is.GreaterThanOrEqualTo(8));Assert.That(camera.cullingMask&(1<<layer),Is.Zero);
            Assert.That(Mathf.Abs(sceneAiming.TopRoot.localPosition.x),Is.GreaterThanOrEqualTo(3f));
            Assert.That(Mathf.Abs(sceneAiming.BottomRoot.localPosition.x),Is.GreaterThanOrEqualTo(3f));
            Assert.That(sceneAiming.TopRoot.localPosition.z,Is.LessThanOrEqualTo(-2f));
            Assert.That(sceneAiming.BottomRoot.localPosition.z,Is.LessThanOrEqualTo(-2f));
            Assert.That(sceneAiming.TopMuzzle.localPosition.z,Is.LessThanOrEqualTo(.5f));
            Assert.That(sceneAiming.BottomMuzzle.localPosition.z,Is.LessThanOrEqualTo(.5f));
            foreach(Renderer renderer in sceneAiming.GetComponentsInChildren<Renderer>(true))
                Assert.That(renderer.gameObject.layer,Is.EqualTo(layer),renderer.name+" must be player-hidden exterior geometry");
            var spectator=Object.FindAnyObjectByType<DesktopSpectatorController>(FindObjectsInactive.Include);
            Assert.That(spectator.SpectatorIncludedMask&(1<<layer),Is.Not.Zero);
        }

        [Test]
        public void RuntimeVisibilityGuardRepairsOverwrittenPlayerMaskAndRendererLayer()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity",OpenSceneMode.Single);
            var guard=Object.FindAnyObjectByType<TurretPlayerVisibilityGuard>(FindObjectsInactive.Include);
            var camera=Camera.main;int layer=LayerMask.NameToLayer(TurretAimingSystem.PlayerHiddenLayerName);int mask=1<<layer;
            Assert.That(guard,Is.Not.Null);camera.cullingMask|=mask;
            Renderer renderer=guard.GetComponentInChildren<Renderer>(true);renderer.gameObject.layer=0;
            guard.ApplyNow();
            Assert.That(camera.cullingMask&mask,Is.Zero,"Runtime guard must recover from a camera mask overwrite");
            Assert.That(renderer.gameObject.layer,Is.EqualTo(layer),"Runtime guard must recover a misplaced turret renderer");
        }

        [Test]
        public void SixDirectionPlayerViewsKeepTurretGeometryCulledAndCombatUiVisible()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity",OpenSceneMode.Single);
            var camera=Camera.main;int turretLayer=LayerMask.NameToLayer(TurretAimingSystem.PlayerHiddenLayerName);
            var rotations=new[]{Quaternion.identity,Quaternion.Euler(0,-90,0),Quaternion.Euler(0,90,0),Quaternion.Euler(0,180,0),Quaternion.Euler(-80,0,0),Quaternion.Euler(80,0,0)};
            foreach(Quaternion rotation in rotations)
            {
                camera.transform.rotation=rotation;
                Assert.That(camera.cullingMask&(1<<turretLayer),Is.Zero,"Turret mesh leaked into a protected view direction");
            }
            var indicator=Object.FindAnyObjectByType<ThreatIndicatorView>(FindObjectsInactive.Include);
            var hud=Object.FindAnyObjectByType<MissionHudController>(FindObjectsInactive.Include);
            Assert.That(indicator,Is.Not.Null);Assert.That(hud,Is.Not.Null);
            Assert.That(indicator.gameObject.layer,Is.Not.EqualTo(turretLayer));Assert.That(hud.gameObject.layer,Is.Not.EqualTo(turretLayer));
            Assert.That(camera.cullingMask&(1<<hud.gameObject.layer),Is.Not.Zero,"Combat HUD must remain visible while turret geometry is culled");
        }

        private void AimUntilReady(GazeLockOnTarget target)
        {
            for(int i=0;i<500&&!aiming.IsReadyFor(target);i++)aiming.Tick(.02f);
            Assert.That(aiming.IsReadyFor(target),Is.True,$"Turret did not converge; error={aiming.AimError:F2}");
        }

        private GazeLockOnTarget Target(string name,Vector3 position,MeteorType type,float health)
        {
            GameObject go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name=name;go.transform.SetParent(root.transform);go.transform.position=position;
            MeteorController meteor=go.AddComponent<MeteorController>();meteor.Configure(type,health,0,10,100,1);meteor.Spawn();
            GazeLockOnTarget target=go.AddComponent<GazeLockOnTarget>();target.Configure(meteor,.1f,1,.2f);return target;
        }

        private TurretAimingSystem.TurretMount Mount(string name,Vector3 position,out Transform muzzle)
        {
            Transform mount=new GameObject(name).transform;mount.SetParent(root.transform);mount.localPosition=position;
            Transform yaw=new GameObject("YawPivot").transform;yaw.SetParent(mount,false);
            Transform pitch=new GameObject("PitchPivot").transform;pitch.SetParent(yaw,false);
            muzzle=new GameObject("Muzzle").transform;muzzle.SetParent(pitch,false);muzzle.localPosition=Vector3.forward;
            return new TurretAimingSystem.TurretMount{label=name,root=mount,yawPivot=yaw,pitchPivot=pitch,muzzle=muzzle};
        }

        private LaserBeamView Beam(string name,out LineRenderer line)
        {
            GameObject go=new GameObject(name);go.transform.SetParent(root.transform);line=go.AddComponent<LineRenderer>();
            LaserBeamView beam=go.AddComponent<LaserBeamView>();beam.Configure(line,.2f);return beam;
        }
    }
}
