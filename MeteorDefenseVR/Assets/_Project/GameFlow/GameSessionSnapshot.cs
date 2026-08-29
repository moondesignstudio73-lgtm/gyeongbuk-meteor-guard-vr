using System;

namespace MeteorDefenseVR.GameFlow
{
    [Serializable]
    public sealed class GameSessionSnapshot
    {
        public int Score;
        public bool MissionFailed;
        public MeteorDefenseVR.Difficulty.DifficultyLevel Difficulty = MeteorDefenseVR.Difficulty.DifficultyLevel.Normal;
        public int DestroyedMeteors;
        public int TotalMeteors;
        public int Shots;
        public int Locks;
        public int SuccessfulHits;
        public int DamageTaken;
        public float RemainingHP;
        public int MaxCombo;
        public float PlayTime;
        public int CurrentStage = 1;
        public int MaxStage = 1;
        public int BossesDestroyed;
        public int ClearedStages;
        public bool CampaignMode;
        public bool AllStagesCleared;
        public float Accuracy => Shots > 0 ? UnityEngine.Mathf.Clamp01((float)SuccessfulHits / Shots) : 0f;
        public int AccuracyPercent => UnityEngine.Mathf.RoundToInt(Accuracy * 100f);
    }
}
