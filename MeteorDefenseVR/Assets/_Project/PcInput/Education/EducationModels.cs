using System;
using System.Collections.Generic;
using System.Globalization;
using MeteorDefenseVR.Difficulty;
using UnityEngine;

namespace MeteorDefenseVR.Education
{
    public enum EducationStage { Orientation, Prediction, Diagnosis, Integrated }
    public enum ThreatDirection { Front, Left, Right, Above, Below, Rear }
    public enum ThreatOutcome { Pending, Observed, Defended, Collision, Missed, Cancelled }
    public enum EducationQuestionKind { Priority, Cause, Emergency }
    public enum EducationEventKind { StageStarted, ThreatAvailable, Detected, WrongDetection, ActionStarted, Defended, Collision, QuestionShown, Answered, Timeout, StageFinished, SessionEnded }

    [Serializable] public sealed class ParticipantInfo
    {
        public string Name = "", Number = "", Grade = "", Group = "";
        public bool Valid => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Number) && Name.Length <= 32 && Number.Length <= 24 && Grade != null && Grade.Length <= 16 && Group != null && Group.Length <= 32;
        public static string Clean(string value, int maximum)
        {
            value = (value ?? "").Trim(); var buffer = new System.Text.StringBuilder();
            foreach (char c in value) if (!char.IsControl(c) && c != '<' && c != '>') { buffer.Append(c); if (buffer.Length >= maximum) break; }
            return buffer.ToString();
        }
        public ParticipantInfo Sanitized() => new ParticipantInfo { Name = Clean(Name, 32), Number = Clean(Number, 24), Grade = Clean(Grade, 16), Group = Clean(Group, 32) };
        public string MaskedName => string.IsNullOrEmpty(Name) ? "—" : Name.Length == 1 ? "*" : Name.Substring(0, 1) + new string('*', Math.Min(3, Name.Length - 1));
    }
    [Serializable] public sealed class ThreatLearningRecord
    {
        public int Id, Round;
        public EducationStage Stage;
        public ThreatDirection Direction;
        public bool EvaluationTarget, RequiresDefense, Demonstration;
        public float AvailableAt, DetectedAt = -1, ActionAt = -1, ResolvedAt = -1;
        public float InitialDistance, InitialSpeed, InitialTtc = -1;
        public ThreatOutcome Outcome;
        public float ReactionSeconds => DetectedAt >= AvailableAt ? DetectedAt - AvailableAt : -1;
    }
    [Serializable] public sealed class EducationQuestionRecord
    {
        public int Id, Round;
        public EducationStage Stage;
        public EducationQuestionKind Kind;
        public string PromptId = "";
        public float ShownAt, AnsweredAt = -1, ReferenceTtc = -1;
        public int Selected = -1, Expected = -1, Attempts;
        public bool Correct, TimedOut, Cancelled;
        public string[] Options = Array.Empty<string>();
        public float ResponseSeconds => AnsweredAt >= ShownAt ? AnsweredAt - ShownAt : -1;
    }
    [Serializable] public sealed class EducationEvent
    {
        public float At;
        public EducationStage Stage;
        public EducationEventKind Kind;
        public int Target, Choice;
        public bool Correct;
        public float Value;
    }
    // Explicit schema only: no Unity components, head poses, landmarks, webcam frames, or device identifiers.
    [Serializable] public sealed class EducationSessionResult
    {
        public string Id = "", StartedAt = "", EndedAt = "";
        public ParticipantInfo Participant = new ParticipantInfo();
        public DifficultyLevel Difficulty;
        public bool Completed, Aborted;
        public float ActiveSeconds;
        public int GameScore, CompletedStages;
        public List<ThreatLearningRecord> Threats = new List<ThreatLearningRecord>();
        public List<EducationQuestionRecord> Questions = new List<EducationQuestionRecord>();
        public List<FaultLearningRecord> Faults = new List<FaultLearningRecord>();
        public List<EducationEvent> Events = new List<EducationEvent>();
        public EducationWeights Weights = new EducationWeights();
        public static EducationSessionResult Create(ParticipantInfo participant, DifficultyLevel difficulty, DateTimeOffset now)
            => new EducationSessionResult { Id = Guid.NewGuid().ToString("N"), Participant = participant.Sanitized(), Difficulty = difficulty, StartedAt = now.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) };
    }
    [Serializable] public sealed class EducationWeights
    {
        [Min(0)] public float Detection = 30, Judgment = 25, Defense = 20, Emergency = 25;
        [Range(0, .5f)] public float WeaknessReactionWeight = .2f;
        [Min(.1f)] public float ReactionBenchmark = 6;
        public EducationWeights Copy() => (EducationWeights)MemberwiseClone();
    }
    public static class EducationLabels
    {
        public static string Direction(ThreatDirection d) => d switch { ThreatDirection.Front => "전방", ThreatDirection.Left => "좌측", ThreatDirection.Right => "우측", ThreatDirection.Above => "상단", ThreatDirection.Below => "하단", _ => "후방" };
        public static string Stage(EducationStage s) => s switch { EducationStage.Orientation => "방향·거리 인식", EducationStage.Prediction => "위험 예측·충돌 방지", EducationStage.Diagnosis => "손상 원인·비상 대응", _ => "비상상황 종합훈련" };
    }
}
