using UnityEngine;

namespace MeteorDefenseVR.GameFlow
{
    [CreateAssetMenu(menuName = "Meteor Defense/Rank Settings", fileName = "SessionRankSettings")]
    public sealed class SessionRankSettings : ScriptableObject
    {
        [SerializeField, Min(0)] private int rankSScore = 3000;
        [SerializeField, Min(0)] private int rankAScore = 2400;
        [SerializeField, Min(0)] private int rankBScore = 1500;

        public int RankSScore => rankSScore;
        public int RankAScore => rankAScore;
        public int RankBScore => rankBScore;

        public void Configure(int s, int a, int b)
        {
            rankSScore = Mathf.Max(0, s);
            rankAScore = Mathf.Clamp(a, 0, rankSScore);
            rankBScore = Mathf.Clamp(b, 0, rankAScore);
        }

        public string Evaluate(GameSessionSnapshot snapshot)
        {
            int score = snapshot != null ? snapshot.Score : 0;
            if (score >= rankSScore) return "S";
            if (score >= rankAScore) return "A";
            if (score >= rankBScore) return "B";
            return "C";
        }

        public string NextGoal(GameSessionSnapshot snapshot)
        {
            string rank = Evaluate(snapshot);
            if (rank == "S") return "최고 S등급!";
            string next = rank == "A" ? "S" : rank == "B" ? "A" : "B";
            int threshold = rank == "A" ? rankSScore : rank == "B" ? rankAScore : rankBScore;
            long remaining = (long)threshold - (snapshot?.Score ?? 0);
            return $"다음 {next}등급까지 {remaining:N0}점";
        }
    }
}
