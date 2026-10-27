using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.PcInput;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace MeteorDefenseVR.Tests
{
    public sealed class ResultContrastTests
    {
        [UnityTearDown] public IEnumerator Cleanup()
        { Time.timeScale=1; if(Application.isPlaying) yield return new ExitPlayMode(); }

        [UnityTest,Timeout(180000)] public IEnumerator CompareResultColorsAcrossSizesBackgroundsAndStates()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity");
            yield return new EnterPlayMode();
            yield return Verify();
        }
        private static IEnumerator Verify()
        {
            var router=Object.FindAnyObjectByType<GazeInputRouter>();router.InputSuspended=true;
            var warm=Object.FindAnyObjectByType<ReadyPrewarmController>();float end=Time.realtimeSinceStartup+30;
            while(!warm.IsComplete&&Time.realtimeSinceStartup<end)yield return null;
            Assert.That(warm.IsComplete,Is.True);
            var flow=GameFlowManager.Instance;var result=Object.FindAnyObjectByType<ResultController>();
            Object.FindAnyObjectByType<HangarLobbyController>().Ranking.Initialize(flow,result,new LocalRankingStore(null));
            ResultActionsView actions=null;end=Time.realtimeSinceStartup+10;
            while((actions==null||actions.Replay==null)&&Time.realtimeSinceStartup<end)
            {actions=Object.FindAnyObjectByType<ResultActionsView>(FindObjectsInactive.Include);yield return null;}
            Assert.That(actions,Is.Not.Null,"Interactive result view must be initialized");
            Assert.That(actions.Replay,Is.Not.Null);
            flow.ShowResult();yield return new WaitForSecondsRealtime(1.7f);
            var camera=Camera.main;
            Assert.That(camera,Is.Not.Null,"Main camera after result");
            Assert.That(actions.Replay,Is.Not.Null,"Replay after result");
            Assert.That(actions.Replay.transform.parent,Is.Not.Null,"Result root after result");
            var labels=actions.Replay.transform.parent.GetComponentsInChildren<TextMeshPro>();
            var secondary=labels.Where(t=>t.name=="ResultDifficulty"||t.name=="ScoreCaption").ToArray();
            var backdrop=GameObject.CreatePrimitive(PrimitiveType.Quad);Object.Destroy(backdrop.GetComponent<Collider>());
            backdrop.transform.SetPositionAndRotation(camera.transform.position+camera.transform.forward*5,camera.transform.rotation);
            backdrop.transform.localScale=Vector3.one*30;
            var material=new Material(Shader.Find("Unlit/Color"));material.color=Color.white;
            backdrop.GetComponent<Renderer>().material=material;
            const string root="Docs/DeliveryQA/result-contrast-20261027";
            var notes=new List<string>{"Explicit non-failed result fixture; white unlit backdrop is a stress fixture; no OS/HMD gameplay. Before reconstructs old colors only in the same renderer."};
            foreach(var size in new[]{new Vector2Int(800,600),new Vector2Int(1280,720)})
            using(var proof=new PcStabilityProbe(camera,size.x,size.y))
            foreach(bool bright in new[]{false,true})
            foreach(bool after in new[]{false,true})
            foreach(string state in new[]{"normal","gaze","disabled"})
            {
                backdrop.SetActive(bright);actions.Present();
                foreach(var label in secondary)label.color=after?new Color32(190,207,222,255):FlightUiTheme.Muted;
                foreach(var target in new[]{actions.Replay,actions.Menu})
                {
                    target.SetAvailable(true);target.OnGazeExit();
                    target.GetComponent<UIInteractionAnimator>().CancelFeedback();
                    target.SetOutlineColors(after?new Color32(117,163,183,255):FlightUiTheme.Edge,
                        after?FlightUiTheme.Accent:HangarLobbyView.Cyan);
                }
                if(state=="gaze")actions.Replay.OnGazeStay(default,.25f);
                if(state=="disabled")actions.Replay.SetAvailable(false);
                yield return new WaitForSecondsRealtime(.2f);
                Canvas.ForceUpdateCanvases();foreach(var label in labels)label.ForceMeshUpdate();
                camera.Render();string folder=$"{root}/{(after?"After":"Before")}/{size.x}x{size.y}/{(bright?"bright":"scene")}-{state}";
                Directory.CreateDirectory(folder);proof.SaveGameOnlyProof(folder,router);
                Assert.That(result.IsVisible&&actions.Visible,Is.True,"Color states must not dismiss result");
                foreach(var label in secondary.Concat(new[]{actions.Replay.GetComponentInChildren<TextMeshPro>(),actions.Menu.GetComponentInChildren<TextMeshPro>()}))
                {
                    Assert.That(label.isTextTruncated,Is.False);
                    var visible=label.textInfo.characterInfo.Take(label.textInfo.characterCount).Where(c=>c.isVisible).ToArray();
                    Assert.That(visible.Length,Is.GreaterThan(0));
                    notes.Add($"{folder} {label.name}: size={label.fontSize}; glyphHeightMin={visible.Min(c=>Mathf.Abs(camera.WorldToScreenPoint(label.transform.TransformPoint(c.topLeft)).y-camera.WorldToScreenPoint(label.transform.TransformPoint(c.bottomLeft)).y))}");
                }
            }
            actions.Replay.SetAvailable(true);Object.Destroy(backdrop);Object.Destroy(material);
            Directory.CreateDirectory(root);File.WriteAllLines(root+"/render-notes.txt",notes);
        }
    }
}
