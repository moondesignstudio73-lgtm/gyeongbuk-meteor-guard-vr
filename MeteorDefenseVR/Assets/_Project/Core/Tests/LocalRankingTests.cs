using System;
using System.Collections;
using System.IO;
using System.Linq;
using MeteorDefenseVR.Core;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.EyeTracking;
using MeteorDefenseVR.GameFlow;
using MeteorDefenseVR.PcInput;
using MeteorDefenseVR.Player;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace MeteorDefenseVR.Tests
{
    public sealed class LocalRankingTests
    {
        private static LocalMissionRecord Record(int score=12560,int offset=0,DifficultyLevel difficulty=DifficultyLevel.Normal,float accuracy=.9f,float hp=90)
            =>new LocalMissionRecord { Id=Guid.NewGuid().ToString("N"),Score=score,Difficulty=difficulty,Destroyed=34,Accuracy=accuracy,
                RemainingHP=hp,Rank="S",Combo=8,PlayTime=52.5f,Timestamp=DateTimeOffset.Parse("2026-08-28T12:00:00Z").AddSeconds(offset).ToString("O") };
        private static string FixturePath()
        {
            string folder=Path.GetFullPath("Docs/LocalRankingQA/TestData/"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder); return Path.Combine(folder,LocalRankingStore.FileName);
        }
        [UnityTearDown] public IEnumerator Cleanup(){if(Application.isPlaying)yield return new ExitPlayMode();}

        [Test] public void RestartRoundTripPreservesAllowlistedMetricsAndCopiesInput()
        {
            string path=FixturePath(); var store=new LocalRankingStore(path);store.Load();var record=Record();
            Assert.That(store.Add(record),Is.True); Assert.That(store.Add(record),Is.False);
            SaveEventually(store);record.Score=1; var reopened=new LocalRankingStore(path);reopened.Load();var saved=reopened.Latest;
            Assert.That(saved.Score,Is.EqualTo(12560)); Assert.That(saved.Destroyed,Is.EqualTo(34));
            Assert.That(saved.Accuracy,Is.EqualTo(.9f));Assert.That(saved.RemainingHP,Is.EqualTo(90));
            Assert.That(saved.Rank,Is.EqualTo("S"));Assert.That(saved.Combo,Is.EqualTo(8));
            Assert.That(saved.PlayTime,Is.EqualTo(52.5f));Assert.That(saved.Difficulty,Is.EqualTo(DifficultyLevel.Normal));
            Assert.That(saved.Timestamp,Is.EqualTo(record.Timestamp));Assert.That(reopened.Records.Count,Is.EqualTo(1));
            string json=File.ReadAllText(path).ToLowerInvariant();
            foreach(var forbidden in new[]{"username","face","webcam","image","landmark","device","machine","calibration"}) Assert.That(json,Does.Not.Contain(forbidden));
            Assert.That(typeof(LocalMissionRecord).GetFields().Select(f=>f.Name),Is.EquivalentTo(new[]{"Id","Score","Difficulty","Destroyed","Accuracy","RemainingHP","Rank","Combo","PlayTime","MaxStage","BossesDestroyed","CampaignComplete","Timestamp"}));
        }
        [Test] public void TopTenUsesScoreAccuracyHealthThenTimeAndBestReusesStoredRanks()
        {
            var store=new LocalRankingStore(null);var a=Record(100,0,DifficultyLevel.Easy,.7f,99);a.Rank="B";
            var b=Record(100,1,DifficultyLevel.Normal,.9f,50);b.Rank="A";
            var c=Record(100,2,DifficultyLevel.Hard,.9f,90);var d=Record(101,3,DifficultyLevel.Easy,.1f,1);d.Rank="C";
            foreach(var r in new[]{a,b,c,d})store.Add(r);
            Assert.That(store.TopTen().Select(r=>r.Id),Is.EqualTo(new[]{d.Id,c.Id,b.Id,a.Id}));
            Assert.That(store.HighestRank,Is.EqualTo("S"));Assert.That(store.Best(DifficultyLevel.Easy).Id,Is.EqualTo(d.Id));
            Assert.That(store.Best(DifficultyLevel.Normal).Id,Is.EqualTo(b.Id));Assert.That(store.Best(DifficultyLevel.Hard).Id,Is.EqualTo(c.Id));
            var e=Record(100,4,DifficultyLevel.Hard,.9f,90);store.Add(e);Assert.That(store.TopTen()[1].Id,Is.EqualTo(e.Id));
        }
        [TestCase(50)] [TestCase(100)] public void CapacityPrunesOldestAndSurvivesReload(int capacity)
        {
            string path=FixturePath();var store=new LocalRankingStore(null,capacity);
            for(int i=0;i<capacity+5;i++)store.Add(Record(i,i));
            Assert.That(store.Records.Count,Is.EqualTo(capacity));Assert.That(store.Records.Min(r=>r.Score),Is.EqualTo(5));
            var disk=new LocalRankingStore(path,capacity);foreach(var r in store.Records)disk.Add(r);
            SaveEventually(disk);var reload=new LocalRankingStore(path,capacity);reload.Load();
            Assert.That(reload.Records.Count,Is.EqualTo(capacity));Assert.That(reload.TopTen().Count,Is.EqualTo(10));
        }
        [Test] public void InterruptedTemporaryWriteDoesNotReplaceLastGoodFile()
        {
            string path=FixturePath();var store=new LocalRankingStore(path);store.Add(Record());
            File.WriteAllText(path+".tmp","{partial");var reload=new LocalRankingStore(path);reload.Load();
            Assert.That(reload.Latest.Score,Is.EqualTo(12560));Assert.That(reload.RecoveryState,Is.EqualTo(LocalRankingStore.Recovery.None));
        }
        [Test] public void CorruptPrimaryRecoversBackupAndDoesNotOverwriteGoodBackupWithCorruption()
        {
            string path=FixturePath();var store=new LocalRankingStore(path);store.Add(Record(10));store.Add(Record(20,1));
            File.WriteAllText(path,"{broken");var reload=new LocalRankingStore(path);reload.Load();
            Assert.That(reload.RecoveryState,Is.EqualTo(LocalRankingStore.Recovery.Backup));Assert.That(reload.Latest.Score,Is.EqualTo(10));
            reload.Add(Record(30,2));File.WriteAllText(path,"{broken again");
            var second=new LocalRankingStore(path);second.Load();Assert.That(second.Latest.Score,Is.EqualTo(10));
        }
        [TestCase("{broken")] [TestCase("{}")] [TestCase("{\"Version\":1,\"Records\":[null]}")]
        public void InvalidFilesFallBackToEmptyAndAcceptNextResult(string corrupt)
        {
            string path=FixturePath();File.WriteAllText(path,corrupt);File.WriteAllText(path+".bak",corrupt);
            var store=new LocalRankingStore(path);Assert.DoesNotThrow(store.Load);Assert.That(store.Records,Is.Empty);
            Assert.That(store.RecoveryState,Is.EqualTo(LocalRankingStore.Recovery.Empty));Assert.That(store.Add(Record()),Is.True);
            SaveEventually(store);var reload=new LocalRankingStore(path);reload.Load();Assert.That(reload.Records.Count,Is.EqualTo(1));
        }
        [Test] public void OversizedFileIsBoundedAndFutureSchemaIsNeverOverwritten()
        {
            string path=FixturePath();File.WriteAllText(path,new string('x',LocalRankingStore.MaximumFileBytes+1));
            var store=new LocalRankingStore(path);store.Load();Assert.That(store.Records,Is.Empty);
            const string future="{\"Version\":2,\"Records\":[]}";File.WriteAllText(path,future);
            store.Load();store.Add(Record());Assert.That(store.PersistenceAvailable,Is.False);
            Assert.That(store.RecoveryState,Is.EqualTo(LocalRankingStore.Recovery.UnsupportedVersion));Assert.That(File.ReadAllText(path),Is.EqualTo(future));
        }
        [Test] public void WriteFailureKeepsSessionUsableAndDoesNotExposePaths()
        {
            string blocker=FixturePath();File.WriteAllText(blocker,"fixture file instead of directory");
            var store=new LocalRankingStore(Path.Combine(blocker,"blocked.json"));store.Load();
            Assert.DoesNotThrow(()=>store.Add(Record()));Assert.That(store.Records.Count,Is.EqualTo(1));
            Assert.That(store.PersistenceAvailable,Is.False);Assert.That(store.TopTen()[0].Score,Is.EqualTo(12560));
        }
        [Test] public void ClearEmptiesBothPrimaryAndRecoveryBackup()
        {
            string path=FixturePath();var store=new LocalRankingStore(path);store.Add(Record());store.Add(Record(1,1));
            Assert.That(store.Clear(),Is.True);var reload=new LocalRankingStore(path);reload.Load();Assert.That(reload.Records,Is.Empty);
            File.WriteAllText(path,"{broken");reload.Load();Assert.That(reload.Records,Is.Empty);
            Assert.That(reload.RecoveryState,Is.EqualTo(LocalRankingStore.Recovery.Backup));
        }
        [Test] public void InvalidMetricsAreRejectedWithoutChangingRecords()
        {
            var store=new LocalRankingStore(null);var record=Record();record.Accuracy=float.NaN;Assert.That(store.Add(record),Is.False);
            record=Record();record.Rank="<sprite=0>";Assert.That(store.Add(record),Is.False);
            record=Record();record.Timestamp="not a date";Assert.That(store.Add(record),Is.False);
            record=Record();record.Difficulty=(DifficultyLevel)900;Assert.That(store.Add(record),Is.False);
            record=Record();record.RemainingHP=float.PositiveInfinity;Assert.That(store.Add(record),Is.False);Assert.That(store.Records,Is.Empty);
        }
        [Test] public void LockedDestinationKeepsOldFileAndRetriesWithoutDuplicateRecords()
        {
            string path=FixturePath();var store=new LocalRankingStore(path);store.Add(Record(10));SaveEventually(store);
            var second=Record(20,1);
            using(var held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None))
            {
                Assert.That(store.Add(second),Is.True);Assert.That(store.PersistenceAvailable,Is.False);
                Assert.That(store.HasPendingWrite,Is.True);Assert.That(store.LastWriteErrorCode,Is.Not.Zero);
                Assert.That(store.Add(second),Is.False);Assert.That(store.Records.Count,Is.EqualTo(2));
            }
            SaveEventually(store);Assert.That(store.HasPendingWrite,Is.False);
            var reload=new LocalRankingStore(path);reload.Load();Assert.That(reload.Records.Count,Is.EqualTo(2));Assert.That(reload.Latest.Score,Is.EqualTo(20));
        }
        [Test] public void LongPlayTimeDoesNotWrapAtAnHourOrADay()
        {
            Assert.That(HangarRecordView.FormatPlayTime(52.5f),Is.EqualTo("00:52"));
            Assert.That(HangarRecordView.FormatPlayTime(3601),Is.EqualTo("01:00:01"));
            Assert.That(HangarRecordView.FormatPlayTime(86400),Is.EqualTo("24:00:00"));
        }
        [Test] public void ResultEventRecordsOnceOnlyAfterMainGameAndMissionComplete()
        {
            var flowRoot=new GameObject("RankFlow");var flow=flowRoot.AddComponent<GameFlowManager>();
            var root=new GameObject("RankFixture");var health=root.AddComponent<PlayerHealth>();var stats=root.AddComponent<GameSessionStats>();
            var result=root.AddComponent<ResultController>();var ranks=ScriptableObject.CreateInstance<SessionRankSettings>();
            var ranking=root.AddComponent<LocalRankingController>();var store=new LocalRankingStore(null);
            try
            {
                stats.Configure(null,null,null,health,null);result.Configure(stats,ranks);result.BindGameFlow(flow);ranking.Initialize(flow,result,store);
                flow.ShowResult();Assert.That(store.Records,Is.Empty,"Debug Result is not a completed play");
                flow.ResetGame();stats.ResetSession();flow.StartMainGame();health.ApplyDamage(10);stats.SetScore(2400);
                stats.RecordShotAndLock();stats.RecordSuccessfulHit();stats.RecordMeteorDestroyed();
                flow.ChangeState(GameState.MissionComplete);flow.ShowResult();
                Assert.That(store.Records.Count,Is.EqualTo(1));Assert.That(store.Latest.Rank,Is.EqualTo(result.Rank));
                Assert.That(store.Latest.RemainingHP,Is.EqualTo(health.CurrentHealth));Assert.That(store.Latest.Accuracy,Is.EqualTo(result.Snapshot.Accuracy));
                result.ShowResult();flow.StartCalibration(GameState.Result);flow.CompleteCalibration();Assert.That(store.Records.Count,Is.EqualTo(1));
                flow.ResetGame();Assert.That(store.Records.Count,Is.EqualTo(1));flow.StartMainGame();flow.ChangeState(GameState.MissionComplete);flow.ShowResult();
                Assert.That(store.Records.Count,Is.EqualTo(2));
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(flowRoot);Object.DestroyImmediate(ranks);}
        }
        private sealed class TestRay:IGazeProvider
        {public Ray Ray;public bool Valid;public bool IsTrackingValid=>Valid;public Ray GetGazeRay()=>Ray;public Vector3 GetGazeOrigin()=>Ray.origin;public Vector3 GetGazeDirection()=>Ray.direction;}
        [UnityTest,Timeout(120000)] public IEnumerator HangarRankingTabsGazeBackResetAndReloadPreserveRecords()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MeteorDefense.unity",OpenSceneMode.Single);
            yield return new EnterPlayMode();yield return VerifyUi();yield return new ExitPlayMode();
        }
        private static IEnumerator VerifyUi()
        {
            var router=Object.FindAnyObjectByType<GazeInputRouter>();router.SetMode(PcGazeMode.MouseGaze);
            var warm=Object.FindAnyObjectByType<ReadyPrewarmController>();float deadline=Time.realtimeSinceStartup+25;
            while(!warm.IsComplete && Time.realtimeSinceStartup<deadline)yield return null;Assert.That(warm.IsComplete,Is.True);
            var lobby=Object.FindAnyObjectByType<HangarLobbyController>();var flow=GameFlowManager.Instance;
            var reset=Object.FindAnyObjectByType<OperationsResetController>();var result=Object.FindAnyObjectByType<ResultController>();
            string path=FixturePath();var store=new LocalRankingStore(path);lobby.Ranking.Initialize(flow,result,store);
            for(int i=0;i<12;i++)store.Add(Record(12560+i*10,i,(DifficultyLevel)(i%3)));
            flow.ShowResult();lobby.BeginReturn();yield return new WaitForSecondsRealtime(3.2f);
            Assert.That(lobby.IsMenuReady,Is.True);
            using var render=new PcStabilityProbe(Camera.main);Save(render,router,"menu");
            var gaze=Object.FindAnyObjectByType<GazeRaycaster>();var provider=new TestRay();gaze.SetProvider(provider);
            var choice=lobby.View.Buttons[6];Physics.SyncTransforms();provider.Ray=new Ray(Camera.main.transform.position,choice.transform.position-Camera.main.transform.position);provider.Valid=true;
            gaze.Tick(.75f);provider.Valid=false;gaze.Tick(0);yield return new WaitForSecondsRealtime(.3f);
            Assert.That(lobby.Phase,Is.EqualTo(HangarLobbyController.LobbyPhase.Records));Assert.That(lobby.View.Records.IsVisible,Is.True);
            Assert.That(lobby.View.Records.DisplayedScore,Does.Contain("12,670"));Save(render,router,"recent");
            LookAt(gaze,provider,lobby.View.Records.TabButtons[1]);yield return new WaitForSecondsRealtime(.3f);
            Assert.That(lobby.View.Records.Tab,Is.EqualTo(HangarRecordView.RecordTab.TopTen));Save(render,router,"top-1");
            LookAt(gaze,provider,lobby.View.Records.NextButton);yield return new WaitForSecondsRealtime(.3f);Save(render,router,"top-2");
            LookAt(gaze,provider,lobby.View.Records.TabButtons[2]);yield return new WaitForSecondsRealtime(.3f);Save(render,router,"best");
            lobby.View.Records.Present(new LocalRankingStore(null),HangarRecordView.RecordTab.Recent);Save(render,router,"empty");
            string blocker=FixturePath();File.WriteAllText(blocker,"isolated fixture");
            var unavailable=new LocalRankingStore(Path.Combine(blocker,"blocked.json"));unavailable.Add(Record());
            lobby.View.Records.Present(unavailable,HangarRecordView.RecordTab.Recent);Save(render,router,"save-unavailable");
            int canvases=Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Length;
            for(int i=0;i<30;i++){lobby.View.Records.Present(store,HangarRecordView.RecordTab.Recent);lobby.View.Records.Present(store,HangarRecordView.RecordTab.TopTen);}
            Assert.That(Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).Length,Is.EqualTo(canvases));
            LookAt(gaze,provider,lobby.View.Records.BackButton);yield return new WaitForSecondsRealtime(.3f);Assert.That(lobby.IsMenuReady,Is.True);
            lobby.Queue(HangarLobbyController.MenuAction.Standby);yield return new WaitForSecondsRealtime(1.3f);
            lobby.Queue(HangarLobbyController.MenuAction.Ranking);yield return new WaitForSecondsRealtime(.3f);Assert.That(lobby.View.Records.IsVisible,Is.True);
            lobby.View.Records.BackButton.Choose();yield return new WaitForSecondsRealtime(.3f);Assert.That(lobby.Phase,Is.EqualTo(HangarLobbyController.LobbyPhase.Standby));
            reset.ResetExperience();Assert.That(store.Records.Count,Is.EqualTo(12));Assert.That(lobby.Phase,Is.EqualTo(HangarLobbyController.LobbyPhase.Hidden));
            var reopened=new LocalRankingStore(path);reopened.Load();Assert.That(reopened.Records.Count,Is.EqualTo(12));
            Assert.That(router.Webcam.IsStreaming,Is.False);gaze.SetProvider(router);
        }
        private static void Save(PcStabilityProbe render,GazeInputRouter router,string stage)
        {Canvas.ForceUpdateCanvases();Camera.main.Render();string folder="Docs/LocalRankingQA/"+stage;Directory.CreateDirectory(folder);render.SaveGameOnlyProof(folder,router);}
        private static void LookAt(GazeRaycaster gaze,TestRay provider,HangarMenuButton button)
        {
            Physics.SyncTransforms();provider.Ray=new Ray(Camera.main.transform.position,button.transform.position-Camera.main.transform.position);provider.Valid=true;
            gaze.Tick(.75f);Assert.That(gaze.CurrentTarget,Is.SameAs(button));provider.Valid=false;gaze.Tick(0);
        }
        private static void SaveEventually(LocalRankingStore store)
        {
            // Exercise the same retry API as the runtime, allowing transient locks in the OneDrive test workspace.
            for(int i=0;i<10 && !store.PersistenceAvailable;i++) { System.Threading.Thread.Sleep(50);store.RetryPendingSave(); }
            Assert.That(store.PersistenceAvailable,Is.True,"Numeric I/O error: "+store.LastWriteErrorCode);
        }
    }
}
