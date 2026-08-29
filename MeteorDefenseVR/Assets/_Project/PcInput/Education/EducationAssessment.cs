using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MeteorDefenseVR.Education
{
    public sealed class LearningArea
    {
        public string Name;
        public int Samples, Successes;
        public float Reaction = -1, Quality;
    }
    public sealed class EducationAssessment
    {
        public int Threats, Detected, Missed, FalsePositive, ReactionSamples, DefenseRequired, DefenseAttempts, Defended, Collisions, DefenseMissed, PriorityQuestions, PriorityCorrect, EmergencyQuestions, EmergencyCorrect;
        public float DetectionAccuracy, Precision, Recall, MeanReaction = -1, FastestReaction = -1, SlowestReaction = -1, MeanDetectionToAction = -1, DefenseRate, PriorityAccuracy, EmergencyAccuracy, MeanEmergencyResponse = -1, Score;
        public string Strongest = "자료 없음", Weakest = "자료 없음";
        public readonly List<LearningArea> Areas = new List<LearningArea>();
        public string Grade => Score >= 85 ? "우수" : Score >= 65 ? "잘했어요" : "함께 연습해요";
        public static float Ratio(int success, int total) => total > 0 ? Mathf.Clamp01((float)success / total) : 0;
        public static EducationAssessment Calculate(EducationSessionResult session)
        {
            var a = new EducationAssessment(); if (session == null) return a;
            var eligible = session.Threats.Where(t => t.EvaluationTarget && !t.Demonstration && t.Outcome != ThreatOutcome.Cancelled).ToList();
            a.Threats = eligible.Count; a.Detected = eligible.Count(t => t.DetectedAt >= t.AvailableAt); a.Missed = a.Threats - a.Detected;
            a.FalsePositive = session.Events.Count(e => e.Kind == EducationEventKind.WrongDetection);
            // TP / (TP + FP + FN), not destroyed-count or time-played based.
            a.DetectionAccuracy = Ratio(a.Detected, a.Detected + a.FalsePositive + a.Missed);
            a.Precision = Ratio(a.Detected, a.Detected + a.FalsePositive); a.Recall = Ratio(a.Detected, a.Threats);
            var reactions = eligible.Where(t => t.ReactionSeconds >= 0).Select(t => t.ReactionSeconds).ToList();
            a.ReactionSamples = reactions.Count;
            if (reactions.Count > 0) { a.MeanReaction = reactions.Average(); a.FastestReaction = reactions.Min(); a.SlowestReaction = reactions.Max(); }
            var actionDelays = eligible.Where(t => t.DetectedAt >= 0 && t.ActionAt >= t.DetectedAt).Select(t => t.ActionAt - t.DetectedAt).ToList();
            if (actionDelays.Count > 0) a.MeanDetectionToAction = actionDelays.Average();
            // Failed/expired exercises count as missed defense, NOT as fabricated physical collisions.
            // Operator-cancelled and still-running trials are excluded; neither can grant success.
            var defense = session.Threats.Where(t => t.RequiresDefense && !t.Demonstration && (t.Outcome == ThreatOutcome.Defended || t.Outcome == ThreatOutcome.Collision || t.Outcome == ThreatOutcome.Missed)).ToList();
            a.DefenseRequired = defense.Count; a.DefenseAttempts = defense.Count(t => t.ActionAt >= 0); a.Defended = defense.Count(t => t.Outcome == ThreatOutcome.Defended); a.Collisions = defense.Count(t => t.Outcome == ThreatOutcome.Collision); a.DefenseMissed = defense.Count(t=>t.Outcome==ThreatOutcome.Missed); a.DefenseRate = Ratio(a.Defended, a.DefenseRequired);
            var priority = session.Questions.Where(q => !q.Cancelled && q.Kind == EducationQuestionKind.Priority).ToList();
            var emergency = session.Questions.Where(q => !q.Cancelled && q.Kind != EducationQuestionKind.Priority).ToList();
            a.PriorityQuestions = priority.Count; a.PriorityCorrect = priority.Count(q => q.Correct); a.PriorityAccuracy = Ratio(a.PriorityCorrect, a.PriorityQuestions);
            a.EmergencyQuestions = emergency.Count; a.EmergencyCorrect = emergency.Count(q => q.Correct); a.EmergencyAccuracy = Ratio(a.EmergencyCorrect, a.EmergencyQuestions);
            var response = emergency.Where(q => q.ResponseSeconds >= 0).Select(q => q.ResponseSeconds).ToList(); if (response.Count > 0) a.MeanEmergencyResponse = response.Average();
            var w = session.Weights ?? new EducationWeights();
            float sum = Mathf.Max(0, w.Detection) + Mathf.Max(0, w.Judgment) + Mathf.Max(0, w.Defense) + Mathf.Max(0, w.Emergency);
            a.Score = sum > 0 ? 100 * (a.DetectionAccuracy * Mathf.Max(0, w.Detection) + a.PriorityAccuracy * Mathf.Max(0, w.Judgment) + a.DefenseRate * Mathf.Max(0, w.Defense) + a.EmergencyAccuracy * Mathf.Max(0, w.Emergency)) / sum : 0;
            foreach (ThreatDirection direction in Enum.GetValues(typeof(ThreatDirection)))
            {
                var items = eligible.Where(t => t.Direction == direction).ToList(); var times = items.Where(t => t.ReactionSeconds >= 0).Select(t => t.ReactionSeconds).ToList();
                AddArea(a, EducationLabels.Direction(direction) + " 위험 탐지", items.Count, items.Count(t => t.DetectedAt >= 0), times.Count > 0 ? times.Average() : -1, w);
            }
            AddArea(a, "위험 우선순위 판단", a.PriorityQuestions, a.PriorityCorrect, -1, w);
            AddArea(a, "충돌 방지 판단", a.DefenseRequired, a.Defended, -1, w);
            AddArea(a, "비상상황 대응", a.EmergencyQuestions, a.EmergencyCorrect, a.MeanEmergencyResponse, w);
            var measured = a.Areas.Where(x => x.Samples > 0).ToList();
            if (measured.Count > 0) { a.Strongest = measured.OrderByDescending(x => x.Quality).First().Name; a.Weakest = measured.OrderBy(x => x.Quality).First().Name; }
            return a;
        }
        private static void AddArea(EducationAssessment a, string name, int total, int correct, float reaction, EducationWeights weights)
        {
            float accuracy = Ratio(correct, total), reactionWeight = reaction >= 0 ? Mathf.Clamp(weights.WeaknessReactionWeight, 0, .5f) : 0;
            a.Areas.Add(new LearningArea { Name = name, Samples = total, Successes = correct, Reaction = reaction,
                Quality = accuracy * (1 - reactionWeight) + reactionWeight * (1 - Mathf.Clamp01(reaction / Mathf.Max(.1f, weights.ReactionBenchmark))) });
        }
    }
    public static class LearningFeedbackGenerator
    {
        public static List<string> Generate(EducationSessionResult result, EducationAssessment a)
        {
            var feedback = new List<string>();
            if (!result.Completed) feedback.Add("훈련이 중단되어 일부 영역은 평가하지 못했어요. 완료한 기록만 확인해 주세요.");
            if (a.PriorityQuestions > 0 && a.PriorityAccuracy < .8f) feedback.Add("위험은 거리만으로 정해지지 않아요. 접근 속도와 방향도 함께 살펴보세요.");
            var rear = a.Areas.Find(x => x.Name == "후방 위험 탐지");
            if (rear != null && rear.Samples > 0 && (EducationAssessment.Ratio(rear.Successes, rear.Samples) < .8f || rear.Reaction > 4)) feedback.Add("뒤쪽 위험을 놓치지 않도록 주기적으로 주변을 둘러보세요.");
            if (a.EmergencyQuestions > 0 && a.EmergencyAccuracy < .8f) feedback.Add("비상상황에서는 즉시 충돌하거나 생명 유지에 영향을 주는 문제부터 대응하세요.");
            if (feedback.Count < 3 && a.Threats > 0) feedback.Add(a.DetectionAccuracy >= .8f ? "주변 위험을 잘 발견했어요. 발견 뒤에는 접근 방향과 속도까지 확인해 보세요." : "지시한 방향을 차근차근 살펴보고 목표가 맞는지 확인해 보세요.");
            if (feedback.Count == 0) feedback.Add("아직 평가 자료가 없어요. 첫 탐색 과제부터 천천히 시작해 보세요.");
            if (feedback.Count > 3) feedback.RemoveRange(3, feedback.Count - 3); return feedback;
        }
    }
}
