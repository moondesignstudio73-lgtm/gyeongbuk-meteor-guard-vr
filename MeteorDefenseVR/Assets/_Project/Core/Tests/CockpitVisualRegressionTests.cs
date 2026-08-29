using System.Collections;
using System.IO;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace MeteorDefenseVR.Tests
{
    public sealed class CockpitVisualRegressionTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup(){if(Application.isPlaying)yield return new ExitPlayMode();}

        [UnityTest,Timeout(90000)]
        public IEnumerator CaptureClosedCockpitAtFourHeadings()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity",OpenSceneMode.Single);
            yield return new EnterPlayMode();
            var router=Object.FindAnyObjectByType<GazeInputRouter>();
            var simulation=router.Simulation;router.SetMode(PcGazeMode.WindowsVRSimulation);
            var flow=GameFlowManager.Instance;flow.StartLaunch();
            float deadline=Time.realtimeSinceStartup+20f;
            while(flow.CurrentState!=GameState.Playing&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(flow.CurrentState,Is.EqualTo(GameState.Playing));
            using var probe=new PcStabilityProbe(Camera.main);
            yield return Capture(probe,router,simulation,0f,"Front");
            yield return Capture(probe,router,simulation,90f,"Right");
            yield return Capture(probe,router,simulation,180f,"Rear");
            yield return Capture(probe,router,simulation,-90f,"Left");
        }

        private static IEnumerator Capture(PcStabilityProbe probe,GazeInputRouter router,WindowsVRSimulation simulation,float yaw,string name)
        {
            simulation.Recenter();simulation.ApplyLook(new Vector2(yaw/.1f,0),1f);
            yield return new WaitForSecondsRealtime(.3f);
            string directory=Path.Combine("Docs/CockpitRestoreQA","After",name);Directory.CreateDirectory(directory);
            probe.SaveGameOnlyProof(directory,router);
        }
    }
}
