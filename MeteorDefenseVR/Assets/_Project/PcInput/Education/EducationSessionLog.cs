using System;
using UnityEngine;

namespace MeteorDefenseVR.Education
{
    // Event sink with stable per-spawn IDs. Pool reuse cannot merge separate observations.
    public sealed class EducationSessionLog
    {
        public const int MaximumThreats = 512, MaximumQuestions = 256, MaximumEvents = 4096;
        public EducationSessionResult Result { get; }
        public bool Closed { get; private set; }
        public EducationSessionLog(EducationSessionResult result) => Result = result;
        public ThreatLearningRecord AddThreat(EducationStage stage, int round, ThreatDirection direction, bool evaluated, bool requiresDefense, bool demonstration, float now, ThreatRisk initial)
        {
            if (Closed || Result.Threats.Count >= MaximumThreats) return null;
            var r = new ThreatLearningRecord { Id = Result.Threats.Count + 1, Round = round, Stage = stage, Direction = direction,
                EvaluationTarget = evaluated, RequiresDefense = requiresDefense, Demonstration = demonstration, AvailableAt = now,
                InitialDistance = initial.Distance, InitialSpeed = initial.RelativeSpeed, InitialTtc = initial.Ttc };
            Result.Threats.Add(r); Event(now, stage, EducationEventKind.ThreatAvailable, r.Id); return r;
        }
        public bool Detect(ThreatLearningRecord r, float now)
        {
            if (Closed || r == null || r.DetectedAt >= 0 || now < r.AvailableAt) return false;
            r.DetectedAt = now; Event(now, r.Stage, EducationEventKind.Detected, r.Id); return true;
        }
        public void WrongDetection(ThreatLearningRecord r, float now)
        { if (!Closed && r != null) Event(now, r.Stage, EducationEventKind.WrongDetection, r.Id); }
        public void Action(ThreatLearningRecord r, float now)
        { if (Closed || r == null || r.ActionAt >= 0) return; r.ActionAt = Mathf.Max(now, r.AvailableAt); Event(now, r.Stage, EducationEventKind.ActionStarted, r.Id); }
        public bool Resolve(ThreatLearningRecord r, ThreatOutcome outcome, float now)
        {
            if (Closed || r == null || r.Outcome != ThreatOutcome.Pending) return false;
            r.Outcome = outcome; r.ResolvedAt = now;
            if (outcome == ThreatOutcome.Defended || outcome == ThreatOutcome.Collision)
                Event(now, r.Stage, outcome == ThreatOutcome.Defended ? EducationEventKind.Defended : EducationEventKind.Collision, r.Id);
            return true;
        }
        public EducationQuestionRecord Question(EducationStage stage, int round, EducationQuestionKind kind, string prompt, float now)
        {
            if (Closed || Result.Questions.Count >= MaximumQuestions) return null;
            var q = new EducationQuestionRecord { Id = Result.Questions.Count + 1, Round = round, Stage = stage, Kind = kind, PromptId = prompt, ShownAt = now };
            Result.Questions.Add(q); Event(now, stage, EducationEventKind.QuestionShown, q.Id); return q;
        }
        public void Answer(EducationQuestionRecord q, int selected, int expected, float now, float ttc = -1)
        {
            if (Closed || q == null) return;
            q.Attempts++;
            // Retrying teaches the correct action, but only the first answer determines assessment.
            if (q.Attempts == 1) { q.Selected = selected; q.Expected = expected; q.AnsweredAt = Mathf.Max(now, q.ShownAt); q.Correct = selected >= 0 && selected == expected; q.TimedOut = selected < 0; q.ReferenceTtc = ttc; }
            Event(now, q.Stage, selected < 0 ? EducationEventKind.Timeout : EducationEventKind.Answered, q.Id, selected, selected >= 0 && selected == expected, ttc);
        }
        public void Event(float at, EducationStage stage, EducationEventKind kind, int target = 0, int choice = -1, bool correct = false, float value = 0)
        { if (!Closed && Result.Events.Count < MaximumEvents) Result.Events.Add(new EducationEvent { At = at, Stage = stage, Kind = kind, Target = target, Choice = choice, Correct = correct, Value = value }); }
        public void Close(bool completed, float now, int gameScore, DateTimeOffset ended)
        {
            if (Closed) return;
            foreach (var r in Result.Threats) if (r.Outcome == ThreatOutcome.Pending) Resolve(r, completed ? ThreatOutcome.Missed : ThreatOutcome.Cancelled, now);
            foreach (var q in Result.Questions) if (q.Attempts == 0) { if (completed) Answer(q, -1, -1, now); else q.Cancelled = true; }
            Result.Completed = completed; Result.Aborted = !completed; Result.ActiveSeconds = Mathf.Max(0, now); Result.GameScore = Mathf.Max(0, gameScore);
            Result.EndedAt = ended.ToUniversalTime().ToString("O"); Event(now, EducationStage.Integrated, EducationEventKind.SessionEnded); Closed = true;
        }
    }
}
