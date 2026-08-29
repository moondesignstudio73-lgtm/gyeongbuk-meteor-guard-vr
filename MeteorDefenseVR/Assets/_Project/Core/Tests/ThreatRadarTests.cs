using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Visual;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MeteorDefenseVR.Tests
{
    public sealed class ThreatRadarTests
    {
        [TestCase(0f, 0f, 100f, 0f, .098995f)]
        [TestCase(100f, 0f, 0f, .183848f, 0f)]
        [TestCase(0f, 0f, -100f, 0f, -.098995f)]
        public void WorldDirectionProjectsToMatchingRadarQuadrant(float x,float y,float z,float expectedX,float expectedY)
        {
            Vector2 point=ThreatRadarMath.Project(new Vector3(x,y,z),Vector3.zero,Quaternion.identity,.26f,.14f,200f);
            Assert.That(point.x,Is.EqualTo(expectedX).Within(.001f));Assert.That(point.y,Is.EqualTo(expectedY).Within(.001f));
        }

        [Test]
        public void DistanceRingsAreMonotonicAndReachOuterRange()
        {
            float r25=ThreatRadarMath.DistanceRadius(25,200),r50=ThreatRadarMath.DistanceRadius(50,200),r100=ThreatRadarMath.DistanceRadius(100,200),r200=ThreatRadarMath.DistanceRadius(200,200);
            Assert.That(r25,Is.LessThan(r50));Assert.That(r50,Is.LessThan(r100));Assert.That(r100,Is.LessThan(r200));Assert.That(r200,Is.EqualTo(1));
        }

        [Test]
        public void TimeToCollisionUsesClosingVelocityNotDistanceAlone()
        {
            var go=new GameObject("RadarTtcMeteor");
            try
            {
                var meteor=go.AddComponent<MeteorController>();go.transform.position=Vector3.forward*50;
                meteor.Configure(MeteorType.Fast,1,10,10,100,1);meteor.SetMovementTarget(null);meteor.SetMovementDirection(Vector3.back);meteor.Spawn();
                Assert.That(ThreatRadarMath.TimeToCollision(Vector3.zero,meteor,4.2f),Is.InRange(4.4f,4.7f));
                meteor.SetMovementDirection(Vector3.forward);Assert.That(ThreatRadarMath.TimeToCollision(Vector3.zero,meteor,4.2f),Is.EqualTo(float.PositiveInfinity));
            }
            finally{Object.DestroyImmediate(go);}
        }

        [TestCase(2f,100f,ThreatRadarRisk.Imminent)]
        [TestCase(6f,100f,ThreatRadarRisk.Danger)]
        [TestCase(12f,100f,ThreatRadarRisk.Approaching)]
        [TestCase(30f,100f,ThreatRadarRisk.Safe)]
        public void RiskColorBandUsesTimeToCollision(float ttc,float distance,ThreatRadarRisk expected)
            =>Assert.That(ThreatRadarMath.Risk(ttc,distance),Is.EqualTo(expected));

        [TestCase(DifficultyLevel.Experience,3)]
        [TestCase(DifficultyLevel.Easy,6)]
        [TestCase(DifficultyLevel.Normal,10)]
        [TestCase(DifficultyLevel.Hard,16)]
        public void DifficultyControlsInformationDensity(DifficultyLevel level,int expected)
            =>Assert.That(ThreatRadarMath.DisplayLimit(level),Is.EqualTo(expected));

        [Test]
        public void AuthoredCockpitBuildsBoundedReusableRadarPool()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity",OpenSceneMode.Single);
            var radar=Object.FindAnyObjectByType<CockpitTelemetryPresentation>(FindObjectsInactive.Include);
            Assert.That(radar,Is.Not.Null);radar.Refresh();
            Assert.That(radar.RadarPoolCapacity,Is.EqualTo(16));
            Transform[] children=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None);bool ring=false,cone=false;
            foreach(Transform child in children){ring|=child.name=="DistanceRing_25";cone|=child.name=="HeadViewCone";}
            Assert.That(ring,Is.True);Assert.That(cone,Is.True);
        }
    }
}
