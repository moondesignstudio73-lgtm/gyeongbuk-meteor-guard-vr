using System;
using System.Collections;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using TMPro;
using MeteorDefenseVR.Visual;
using UnityEngine;
using UnityEngine.InputSystem;
using Object=UnityEngine.Object;
namespace MeteorDefenseVR.PcInput
{
    public static class PopulatedRecordsProof
    {
        static void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);}
        public static IEnumerator Run(string output)
        {
            var router=Object.FindAnyObjectByType<GazeInputRouter>();var caster=Object.FindAnyObjectByType<GazeRaycaster>();
            var lobby=Object.FindAnyObjectByType<HangarLobbyController>();var flow=GameFlowManager.Instance;
            var result=Object.FindAnyObjectByType<ResultController>();var reset=Object.FindAnyObjectByType<OperationsResetController>();
            var original=lobby.Ranking.Store;bool casterEnabled=caster.enabled,suspended=router.InputSuspended;
            var devices=InputSystem.devices.Where(d=>d.enabled&&(d is Mouse||d is Keyboard)).ToArray();
            foreach(var d in devices)InputSystem.DisableDevice(d);caster.enabled=false;router.InputSuspended=true;
            var store=new LocalRankingStore(null);var empty=new LocalRankingStore(null);
            lobby.Ranking.Initialize(flow,result,store);
            for(int i=0;i<10;i++)Check(store.Add(new LocalMissionRecord {Id=Guid.NewGuid().ToString("N"),Score=int.MaxValue-i,Difficulty=(DifficultyLevel)(i%4),Destroyed=int.MaxValue,Combo=int.MaxValue,Accuracy=i%2==0?1:.999f,RemainingHP=i%2==0?10000:9999.9f,Rank="S",PlayTime=86400,MaxStage=10,Timestamp=DateTimeOffset.Parse("2026-10-23T00:00:00Z").AddSeconds(i).ToString("O")}),"Valid model boundary record rejected");

            try
            {
                yield return new WaitForSecondsRealtime(4);flow.ShowResult();lobby.BeginReturn();yield return new WaitForSecondsRealtime(4);
                Check(lobby.IsMenuReady,"Lobby ready");lobby.Queue(HangarLobbyController.MenuAction.Ranking);yield return new WaitForSecondsRealtime(.4f);
                foreach(var size in new[]{new Vector2Int(800,600),new Vector2Int(2560,1080)})
                {
                    using var proof=new PcStabilityProbe(Camera.main,size.x,size.y);
                    IEnumerator Save(string name)
                    {
                        yield return new WaitForSecondsRealtime(.3f);Camera.main.GetComponent<WorldUiSafeArea>().Apply();Canvas.ForceUpdateCanvases();PcStabilityRenderDriver.RenderForProof(Camera.main);
                        string dir=$"{output}/{size.x}x{size.y}/{name}";Directory.CreateDirectory(dir);proof.SaveGameOnlyProof(dir,router);
                        var labels=lobby.View.Records.BackButton.transform.parent.GetComponentsInChildren<TextMeshPro>().Where(t=>t.gameObject.activeInHierarchy).ToArray();
                        File.WriteAllLines(Path.Combine(dir,"text.txt"),new[]{"Controlled memory-only valid-model boundary fixtures, not natural play/OS/HMD. Score/destroyed/combo=int.MaxValue; HP<=10000; time<=86400; accuracy<=1; stage=actual campaign 10."}.Concat(labels.Select(t=>{t.ForceMeshUpdate();return $"{t.name}: font={t.fontSize} width={t.rectTransform.rect.width} preferred={t.preferredWidth} truncated={t.isTextTruncated} text={t.text}";})));
                    }
                    lobby.View.Records.Present(store,HangarRecordView.RecordTab.Recent);yield return Save("recent-max");
                    lobby.View.Records.Present(store,HangarRecordView.RecordTab.TopTen);yield return Save("top-first");
                    lobby.Queue(HangarLobbyController.MenuAction.NextRecordPage);yield return new WaitForSecondsRealtime(.4f);yield return Save("top-last");
                    lobby.Queue(HangarLobbyController.MenuAction.NextRecordPage);yield return new WaitForSecondsRealtime(.4f);yield return Save("top-return-first");
                    lobby.Queue(HangarLobbyController.MenuAction.BestRecords);yield return new WaitForSecondsRealtime(.4f);yield return Save("best-max");
                    foreach(var tab in new[]{HangarRecordView.RecordTab.Recent,HangarRecordView.RecordTab.TopTen,HangarRecordView.RecordTab.Best})
                    {lobby.View.Records.Present(empty,tab);Check(!lobby.View.Records.NextButton.gameObject.activeSelf,"Empty next hidden");yield return Save("empty-"+tab);}
                    lobby.View.Records.Present(store,HangarRecordView.RecordTab.TopTen);Check(lobby.View.Records.NextButton.gameObject.activeSelf,"Populated next restored");
                    lobby.Queue(HangarLobbyController.MenuAction.Back);yield return new WaitForSecondsRealtime(.4f);Check(lobby.IsMenuReady,"Back returns menu");
                    lobby.Queue(HangarLobbyController.MenuAction.Ranking);yield return new WaitForSecondsRealtime(.4f);Check(lobby.View.Records.Tab==HangarRecordView.RecordTab.Recent,"Reentry recent");
                }
                File.WriteAllText(Path.Combine(output,"verification.txt"),"Controlled memory store; first/last/first page, tabs, empty/populated, back/reentry passed. Gaze covered separately.\npassed=True\n");
            }
            finally {lobby.Ranking.Initialize(flow,result,original);reset.ResetExperience();caster.enabled=casterEnabled;router.InputSuspended=suspended;foreach(var d in devices)InputSystem.EnableDevice(d);}
        }
    }
}



