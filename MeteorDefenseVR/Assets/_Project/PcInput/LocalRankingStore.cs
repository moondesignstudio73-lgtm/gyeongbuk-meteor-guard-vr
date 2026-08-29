using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MeteorDefenseVR.Difficulty;
using MeteorDefenseVR.GameFlow;
using UnityEngine;

namespace MeteorDefenseVR.PcInput
{
    // Explicit allowlist: never serialize a component, a webcam object, or a player identity.
    [Serializable]
    public sealed class LocalMissionRecord
    {
        public string Id;
        public int Score;
        public DifficultyLevel Difficulty;
        public int Destroyed;
        public float Accuracy;
        public float RemainingHP;
        public string Rank;
        public int Combo;
        public float PlayTime;
        public int MaxStage;
        public int BossesDestroyed;
        public bool CampaignComplete;
        public string Timestamp;

        public static LocalMissionRecord FromResult(GameSessionSnapshot result, string rank, string id, DateTimeOffset time)
            => new LocalMissionRecord { Id=id, Score=result.Score, Difficulty=result.Difficulty,
                Destroyed=result.DestroyedMeteors, Accuracy=result.Accuracy, RemainingHP=result.RemainingHP,
                Rank=rank, Combo=result.MaxCombo, PlayTime=result.PlayTime, MaxStage=result.MaxStage,
                BossesDestroyed=result.BossesDestroyed, CampaignComplete=result.AllStagesCleared,
                Timestamp=time.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) };
        internal LocalMissionRecord Copy() => (LocalMissionRecord)MemberwiseClone();
        internal DateTimeOffset Time => DateTimeOffset.ParseExact(Timestamp,"O",CultureInfo.InvariantCulture);
    }

    public sealed class LocalRankingStore
    {
        public const string FileName = "MeteorDefense-ranking-v1.json";
        public const int MaximumFileBytes = 262144;
        public enum Recovery { None, Backup, Empty, Unavailable, UnsupportedVersion }
        private enum ReadResult { Missing, Valid, Invalid, Unavailable, Future }
        [Serializable] private sealed class Document { public int Version; public List<LocalMissionRecord> Records; }
        private readonly List<LocalMissionRecord> records = new List<LocalMissionRecord>();
        private readonly string path;
        private bool preserveBackup, writesBlocked;
        public int Capacity { get; }
        public IReadOnlyList<LocalMissionRecord> Records => records.AsReadOnly();
        public LocalMissionRecord Latest => records.Count == 0 ? null : records[0].Copy();
        public Recovery RecoveryState { get; private set; }
        public bool PersistenceAvailable { get; private set; } = true;
        public bool HasPendingWrite { get; private set; }
        public int LastWriteErrorCode { get; private set; }

        // Null path is an explicit memory-only store for automated scene tests.
        public LocalRankingStore(string filePath, int capacity=100)
        { path=filePath; Capacity=Mathf.Clamp(capacity,50,100); }
        public void Load()
        {
            records.Clear(); preserveBackup=false; writesBlocked=false; RecoveryState=Recovery.None;
            PersistenceAvailable=true; HasPendingWrite=false; LastWriteErrorCode=0;
            if(path==null) return;
            var primary=Read(path,out var loaded);
            if(primary==ReadResult.Valid) records.AddRange(loaded);
            else if(primary==ReadResult.Future || primary==ReadResult.Unavailable)
            {
                writesBlocked=true; PersistenceAvailable=false;
                RecoveryState=primary==ReadResult.Future?Recovery.UnsupportedVersion:Recovery.Unavailable;
            }
            else
            {
                var backup=Read(path+".bak",out loaded);
                if(backup==ReadResult.Valid)
                { records.AddRange(loaded); RecoveryState=Recovery.Backup; preserveBackup=true; }
                else if(backup==ReadResult.Future || backup==ReadResult.Unavailable)
                { writesBlocked=true; PersistenceAvailable=false; RecoveryState=Recovery.Unavailable; }
                else if(primary!=ReadResult.Missing || backup!=ReadResult.Missing)
                { RecoveryState=Recovery.Empty; preserveBackup=true; }
            }
            int before=records.Count; Trim();
            if(before>Capacity) Persist();
        }
        public bool Add(LocalMissionRecord record)
        {
            if(!Valid(record) || records.Any(r=>r.Id==record.Id)) return false;
            records.Add(record.Copy()); Trim(); Persist(); return true;
        }
        public List<LocalMissionRecord> TopTen() => Ranked(records).Take(10).Select(r=>r.Copy()).ToList();
        public LocalMissionRecord Best(DifficultyLevel level) => Ranked(records.Where(r=>r.Difficulty==level)).FirstOrDefault()?.Copy();
        public string HighestRank => records.Count==0?"—":records.OrderBy(r=>RankOrder(r.Rank)).First().Rank;
        private static int RankOrder(string rank) => rank=="S"?0:rank=="A"?1:rank=="B"?2:3;
        private static IOrderedEnumerable<LocalMissionRecord> Ranked(IEnumerable<LocalMissionRecord> source)
            => source.OrderByDescending(r=>r.Score).ThenByDescending(r=>r.Accuracy)
                .ThenByDescending(r=>r.RemainingHP).ThenByDescending(r=>r.Time).ThenBy(r=>r.Id,StringComparer.Ordinal);
        private void Trim()
        {
            records.Sort((a,b)=> { int c=b.Time.CompareTo(a.Time); return c!=0?c:StringComparer.Ordinal.Compare(a.Id,b.Id); });
            if(records.Count>Capacity) records.RemoveRange(Capacity,records.Count-Capacity);
        }
        public bool Clear()
        {
            records.Clear();
            // Two atomic empty writes ensure the recovery backup is empty too: no resurrection on next launch.
            if(!Persist()) return false;
            return Persist();
        }
        public bool RetryPendingSave() => HasPendingWrite ? Persist() : PersistenceAvailable;
        private bool Persist()
        {
            if(path==null) return true;
            if(writesBlocked) return false;
            HasPendingWrite=true;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                byte[] bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Document { Version=1, Records=records }));
                using(var stream=new FileStream(path+".tmp",FileMode.Create,FileAccess.Write,FileShare.None))
                { stream.Write(bytes,0,bytes.Length); stream.Flush(true); }
                if(File.Exists(path)) File.Replace(path+".tmp",path,preserveBackup?null:path+".bak");
                else File.Move(path+".tmp",path);
                preserveBackup=false; PersistenceAvailable=true; HasPendingWrite=false; LastWriteErrorCode=0; return true;
            }
            catch(Exception ex) when(IsIoFailure(ex))
            { PersistenceAvailable=false; LastWriteErrorCode=ex.HResult; return false; } // Numeric diagnostic only; no paths/payloads in logs.
        }
        private static bool IsIoFailure(Exception ex) => ex is IOException || ex is UnauthorizedAccessException
            || ex is System.Security.SecurityException || ex is NotSupportedException || ex is ArgumentException;
        private static ReadResult Read(string file,out List<LocalMissionRecord> result)
        {
            result=null;
            try
            {
                if(!File.Exists(file)) return ReadResult.Missing;
                using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    if(stream.Length==0 || stream.Length>MaximumFileBytes) return ReadResult.Invalid;
                    using(var reader=new StreamReader(stream,Encoding.UTF8,true))
                    {
                        var data=JsonUtility.FromJson<Document>(reader.ReadToEnd());
                        if(data!=null && data.Version>1) return ReadResult.Future;
                        if(data==null || data.Version!=1 || data.Records==null || data.Records.Count>1000
                            || data.Records.Any(r=>!Valid(r)) || data.Records.Select(r=>r.Id).Distinct().Count()!=data.Records.Count)
                            return ReadResult.Invalid;
                        result=data.Records; return ReadResult.Valid;
                    }
                }
            }
            catch(ArgumentException) { return ReadResult.Invalid; }
            catch(Exception ex) when(IsIoFailure(ex)) { return ReadResult.Unavailable; }
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Valid(LocalMissionRecord r) => r!=null && Guid.TryParseExact(r.Id,"N",out _)
            && r.Score>=0 && Enum.IsDefined(typeof(DifficultyLevel),r.Difficulty) && r.Destroyed>=0
            && Finite(r.Accuracy) && r.Accuracy>=0 && r.Accuracy<=1 && Finite(r.RemainingHP) && r.RemainingHP>=0 && r.RemainingHP<=10000
            && (r.Rank=="S" || r.Rank=="A" || r.Rank=="B" || r.Rank=="C") && r.Combo>=0 && r.Combo<=r.Destroyed
            && Finite(r.PlayTime) && r.PlayTime>=0 && r.PlayTime<=86400
            && DateTimeOffset.TryParseExact(r.Timestamp,"O",CultureInfo.InvariantCulture,DateTimeStyles.None,out _);
    }
}
