namespace MeteorDefenseVR.GameFlow
{
    public static class ResultFeedback
    {
        public static string Achievement(GameSessionSnapshot snapshot)
        {
            if (snapshot == null) return "첫 격파에 도전해요!";
            if (!snapshot.MissionFailed && snapshot.AllStagesCleared && snapshot.DamageTaken == 0 && snapshot.SuccessfulHits > 0)
                return "피해 없이 임무 완료!";
            if (snapshot.DestroyedMeteors > 0) return $"운석 {snapshot.DestroyedMeteors:N0}개 격파!";
            if (snapshot.SuccessfulHits > 0) return $"공격 {snapshot.SuccessfulHits:N0}회 명중!";
            return "첫 격파에 도전해요!";
        }
    }
}
