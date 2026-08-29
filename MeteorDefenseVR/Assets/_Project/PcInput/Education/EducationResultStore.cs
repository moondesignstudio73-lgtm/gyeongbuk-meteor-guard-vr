using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MeteorDefenseVR.Education
{
    public sealed class EducationResultStore
    {
        public const string FileName = "MeteorDefense-education-v1.json";
        public const int MaximumBytes = 16 * 1024 * 1024;
        [Serializable] private sealed class Document { public int Version; public List<EducationSessionResult> Sessions; }
        public enum RecoveryStatus { None, Backup, Empty, Unavailable, FutureVersion }
        private readonly string path;
        private readonly int capacity, retentionDays;
        private readonly List<EducationSessionResult> records = new List<EducationSessionResult>();
        private bool preserveBackup, blocked;
        public IReadOnlyList<EducationSessionResult> Records => records;
        public RecoveryStatus Recovery { get; private set; }
        public bool Saved { get; private set; } = true;
        public bool PendingWrite { get; private set; }
        // null is a memory-only store used by scene tests. It never touches real participant data.
        public EducationResultStore(string filePath, int maximum = 200, int days = 30)
        { path = filePath; capacity = Mathf.Clamp(maximum, 1, 500); retentionDays = Mathf.Clamp(days, 1, 365); }
        public void Load(DateTimeOffset now)
        {
            records.Clear(); blocked = false; preserveBackup = false; Saved = true; PendingWrite = false; Recovery = RecoveryStatus.None;
            if (path == null) return;
            int primary = Read(path, out var data);
            if (primary == 1) records.AddRange(data);
            else if (primary == 3 || primary == 4) { Block(primary); return; }
            else
            {
                int backup = Read(path + ".bak", out data);
                if (backup == 1) { records.AddRange(data); Recovery = RecoveryStatus.Backup; preserveBackup = true; }
                else if (backup == 3 || backup == 4) { Block(backup); return; }
                else if (primary != 0 || backup != 0) { Recovery = RecoveryStatus.Empty; preserveBackup = true; }
            }
            int count = records.Count; Trim(now); if (records.Count != count && Save()) Save(); // Expired identities must also leave the backup.
        }
        private void Block(int state) { blocked = true; Saved = false; Recovery = state == 4 ? RecoveryStatus.FutureVersion : RecoveryStatus.Unavailable; }
        public bool Add(EducationSessionResult result, DateTimeOffset now)
        {
            if (!Valid(result)) { Saved=false; return false; }
            if (records.Any(r => r.Id == result.Id)) return false;
            bool expired=records.Any(r=>DateTimeOffset.Parse(r.StartedAt,CultureInfo.InvariantCulture)<now.AddDays(-retentionDays));
            records.Add(JsonUtility.FromJson<EducationSessionResult>(JsonUtility.ToJson(result))); Trim(now); if(Save()&&expired)Save(); return true;
        }
        private void Trim(DateTimeOffset now)
        {
            DateTimeOffset oldest = now.AddDays(-retentionDays);
            records.RemoveAll(r => DateTimeOffset.Parse(r.StartedAt, CultureInfo.InvariantCulture) < oldest);
            records.Sort((a, b) => string.CompareOrdinal(b.StartedAt, a.StartedAt));
            if (records.Count > capacity) records.RemoveRange(capacity, records.Count - capacity);
        }
        public bool RetrySave() => !PendingWrite || Save();
        public bool Clear()
        {
            if (blocked) return false;
            records.Clear(); if (!Save()) return false;
            // Replace twice so backup recovery cannot resurrect personal records after an operator clear.
            return Save();
        }
        private bool Save()
        {
            PendingWrite = true;
            if (blocked) return false;
            if (path == null) { Saved = true; PendingWrite = false; return true; }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Document { Version = 1, Sessions = records }));
                while (bytes.Length > MaximumBytes && records.Count > 1)
                { records.RemoveAt(records.Count - 1); bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Document { Version = 1, Sessions = records })); }
                if (bytes.Length > MaximumBytes) { Saved = false; return false; }
                using (var stream = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(path + ".tmp", path, preserveBackup ? null : path + ".bak");
                else File.Move(path + ".tmp", path);
                preserveBackup = false; Saved = true; PendingWrite = false; return true;
            }
            catch (Exception e) when (IoFailure(e)) { Saved = false; return false; }
        }
        private static int Read(string file, out List<EducationSessionResult> records)
        {
            records = null;
            try
            {
                if (!File.Exists(file)) return 0;
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (stream.Length <= 0 || stream.Length > MaximumBytes) return 2;
                using var reader = new StreamReader(stream, Encoding.UTF8, true);
                Document document;
                try { document = JsonUtility.FromJson<Document>(reader.ReadToEnd()); }
                catch (ArgumentException) { return 2; }
                if (document != null && document.Version > 1) return 4;
                if (document == null || document.Version != 1 || document.Sessions == null || document.Sessions.Count > 500 || document.Sessions.Any(r => !Valid(r)) || document.Sessions.Select(r => r.Id).Distinct().Count() != document.Sessions.Count) return 2;
                records = document.Sessions; return 1;
            }
            catch (Exception e) when (IoFailure(e)) { return 3; }
        }
        internal static bool IoFailure(Exception e) => e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException || e is ArgumentException || e is NotSupportedException;
        private static bool Number(float n, float minimum = 0, float maximum = 100000) => !float.IsNaN(n) && !float.IsInfinity(n) && n >= minimum && n <= maximum;
        public static bool Valid(EducationSessionResult r)
        {
            if (r == null || !Guid.TryParseExact(r.Id, "N", out _) || r.Participant == null || !r.Participant.Valid || r.Completed == r.Aborted
                || !DateTimeOffset.TryParseExact(r.StartedAt, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
                || !DateTimeOffset.TryParseExact(r.EndedAt, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end) || end < start
                || !Enum.IsDefined(typeof(Difficulty.DifficultyLevel), r.Difficulty) || !Number(r.ActiveSeconds, 0, 86400) || r.GameScore < 0 || r.CompletedStages < 0 || r.CompletedStages > 4
                || r.Threats == null || r.Questions == null || r.Events == null || r.Faults == null || r.Faults.Count > EducationSessionLog.MaximumThreats || r.Threats.Count > EducationSessionLog.MaximumThreats || r.Questions.Count > EducationSessionLog.MaximumQuestions || r.Events.Count > EducationSessionLog.MaximumEvents || r.Weights == null) return false;
            if(r.Faults.Any(f=>f==null||!Enum.IsDefined(typeof(TrainingFault),f.Kind)||!Enum.IsDefined(typeof(EducationStage),f.Stage)||!Enum.IsDefined(typeof(ThreatDirection),f.Direction)||f.ThreatId<1||f.Damage<0||!Number(f.At)||!Number(f.Severity,0,1)||!Number(f.InitialCondition,0,100)||!Number(f.FinalCondition,0,100)||!Number(f.MitigatedAt,-1)))return false;
            if (!Number(r.Weights.Detection) || !Number(r.Weights.Judgment) || !Number(r.Weights.Defense) || !Number(r.Weights.Emergency) || !Number(r.Weights.ReactionBenchmark, .1f) || !Number(r.Weights.WeaknessReactionWeight, 0, .5f)) return false;
            if (r.Threats.Any(t => t == null || t.Id < 1 || !Enum.IsDefined(typeof(ThreatDirection), t.Direction) || !Enum.IsDefined(typeof(EducationStage), t.Stage) || !Enum.IsDefined(typeof(ThreatOutcome), t.Outcome)
                || !Number(t.AvailableAt) || !Number(t.DetectedAt, -1) || !Number(t.ActionAt, -1) || !Number(t.ResolvedAt, -1) || !Number(t.InitialDistance) || !Number(t.InitialSpeed) || !Number(t.InitialTtc, -1)
                || t.Outcome==ThreatOutcome.Pending || t.ResolvedAt<t.AvailableAt || t.ResolvedAt>r.ActiveSeconds+.01f
                || t.DetectedAt>=0&&t.DetectedAt<t.AvailableAt || t.ActionAt>=0&&t.ActionAt<t.AvailableAt)) return false;
            if (r.Questions.Any(q => q == null || q.Id < 1 || q.PromptId == null || q.PromptId.Length > 100 || !Enum.IsDefined(typeof(EducationStage), q.Stage) || !Enum.IsDefined(typeof(EducationQuestionKind), q.Kind)
                || !Number(q.ShownAt) || !Number(q.AnsweredAt, -1) || !Number(q.ReferenceTtc, -1) || q.Attempts < 0 || q.Selected < -1 || q.Expected < -1
                || q.Options == null || q.Options.Length > 4 || q.Options.Any(s => s == null || s.Length > 120)
                || q.Attempts>0&&(q.Correct!=(q.Selected>=0&&q.Selected==q.Expected)||q.TimedOut!=(q.Selected<0)||q.AnsweredAt<q.ShownAt||q.AnsweredAt>r.ActiveSeconds+.01f)
                || q.Attempts==0&&(!q.Cancelled||q.Correct||q.AnsweredAt!=-1))) return false;
            return r.Events.All(e => e != null && Number(e.At) && Number(e.Value, -1) && Enum.IsDefined(typeof(EducationEventKind), e.Kind))
                && r.Threats.Select(t => t.Id).Distinct().Count() == r.Threats.Count && r.Questions.Select(q => q.Id).Distinct().Count() == r.Questions.Count;
        }
    }
    public static class EducationCsvExporter
    {
        public static string Cell(string value)
        {
            value ??= "";
            // Excel formula injection prevention, including participant-supplied names/groups.
            string trimmed = value.TrimStart();
            if (trimmed.Length > 0 && (trimmed[0] == '=' || trimmed[0] == '+' || trimmed[0] == '-' || trimmed[0] == '@') || value.StartsWith("\t") || value.StartsWith("\r")) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        public static string Render(IEnumerable<EducationSessionResult> records)
        {
            var lines = new StringBuilder("플레이 날짜,이름,번호,학년,그룹,난이도,교육 종합점수,위험 탐지 정확도,평균 반응시간,충돌 회피율,위험판단 정답률,비상대응 정답률,가장 잘한 영역,가장 약한 영역,완료 상태\r\n");
            foreach (var r in records)
            {
                var a = EducationAssessment.Calculate(r);
                string N(float n) => n.ToString("0.##", CultureInfo.InvariantCulture);
                string Rate(float n, int samples) => samples > 0 ? N(n * 100) : "";
                var values = new[] { DateTimeOffset.Parse(r.StartedAt, CultureInfo.InvariantCulture).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz"), r.Participant.Name, r.Participant.Number, r.Participant.Grade, r.Participant.Group,
                    Difficulty.DifficultyConfig.Label(r.Difficulty), N(a.Score), Rate(a.DetectionAccuracy,a.Threats), a.MeanReaction < 0 ? "" : N(a.MeanReaction), Rate(a.DefenseRate,a.DefenseRequired), Rate(a.PriorityAccuracy,a.PriorityQuestions), Rate(a.EmergencyAccuracy,a.EmergencyQuestions), a.Strongest, a.Weakest, r.Completed ? "완료" : "중단" };
                lines.Append(string.Join(",", values.Select(Cell))).Append("\r\n");
            }
            return lines.ToString();
        }
        public static bool Write(string file, IEnumerable<EducationSessionResult> records)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                using var writer = new StreamWriter(new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None), new UTF8Encoding(true));
                writer.Write(Render(records)); return true;
            }
            catch (Exception e) when (EducationResultStore.IoFailure(e)) { return false; }
        }
    }
}
