using System;

namespace MeteorDefenseVR.GameFlow
{
    [Serializable]
    public sealed class GameSessionSnapshot
    {
        public int Score;
        public int DestroyedMeteors;
        public int TotalMeteors;
        public int Shots;
        public int Locks;
        public int SuccessfulHits;
        public int DamageTaken;
        public int MaxCombo;
        public float PlayTime;
        public float Accuracy => Shots > 0 ? (float)SuccessfulHits / Shots : 0f;
        public int AccuracyPercent => UnityEngine.Mathf.RoundToInt(Accuracy * 100f);
    }
}
