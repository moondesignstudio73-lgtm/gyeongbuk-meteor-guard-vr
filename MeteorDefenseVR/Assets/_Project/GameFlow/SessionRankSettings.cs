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
    }
}
