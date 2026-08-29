using System.Linq;

namespace MeteorDefenseVR.Education
{
    public static class EducationReportFormatter
    {
        public static string Percent(float rate, int samples) => samples > 0 ? $"{rate * 100:F0}%" : "자료 없음";
        public static string Seconds(float seconds) => seconds >= 0 ? $"{seconds:F1}초" : "자료 없음";
        public static string Summary(EducationSessionResult session)
        {
            var a = EducationAssessment.Calculate(session);
            string grade = !session.Completed ? "중단된 훈련 · 부분 기록" : a.Threats == 0 ? "평가 자료 없음" : a.Grade;
            return $"{grade}\n교육 평가 {a.Score:F0} / 100\n\n" + string.Join("\n\n", LearningFeedbackGenerator.Generate(session, a));
        }
        public static string Metrics(EducationSessionResult session)
        {
            var a = EducationAssessment.Calculate(session);
            return $"위험 탐지 정확도   {Percent(a.DetectionAccuracy, a.Threats + a.FalsePositive)}\n평균 반응시간   {Seconds(a.MeanReaction)}\n충돌 회피율   {Percent(a.DefenseRate, a.DefenseRequired)}\n위험판단 정답률   {Percent(a.PriorityAccuracy, a.PriorityQuestions)}\n비상대응 정답률   {Percent(a.EmergencyAccuracy, a.EmergencyQuestions)}\n\n잘한 영역   {a.Strongest}\n보완 영역   {a.Weakest}";
        }
        public static string Details(EducationSessionResult session)
        {
            var a = EducationAssessment.Calculate(session);
            return $"탐지 {a.Detected}/{a.Threats} · 미발견 {a.Missed} · 잘못 탐지 {a.FalsePositive}\n반응 표본 {a.ReactionSamples} · 최단 {Seconds(a.FastestReaction)} · 최장 {Seconds(a.SlowestReaction)}\n발견 → 행동 {Seconds(a.MeanDetectionToAction)}\n방어 시도 {a.DefenseAttempts} · 성공 {a.Defended} · 충돌 {a.Collisions}\n비상 응답시간 {Seconds(a.MeanEmergencyResponse)}\n게임 점수 {session.GameScore:N0} · 교육 점수 {a.Score:F0}/100\n활성 훈련 {session.ActiveSeconds / 60:F1}분 · 완료 단계 {session.CompletedStages}/4\n\n" +
                $"위험판단 {a.PriorityCorrect}/{a.PriorityQuestions}\n비상대응 {a.EmergencyCorrect}/{a.EmergencyQuestions}";
        }
        public static string Areas(EducationSessionResult session) => "영역별 성공 / 표본 · 평균 반응\n\n"+string.Join("\n",EducationAssessment.Calculate(session).Areas.Select(x=>$"{x.Name}   {(x.Samples>0?$"{x.Successes}/{x.Samples}":"자료 없음")}   {(x.Reaction>=0?Seconds(x.Reaction):"")}"));
    }
}
