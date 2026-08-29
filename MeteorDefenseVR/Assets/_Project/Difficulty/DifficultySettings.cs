using UnityEngine;

namespace MeteorDefenseVR.Difficulty
{
    [CreateAssetMenu(menuName = "Meteor Defense/Difficulty Settings")]
    public sealed class DifficultySettings : ScriptableObject
    {
        public DifficultyConfig easy, normal, hard, extreme;
        public DifficultyConfig Experience => extreme;
        public DifficultyLevel defaultDifficulty = DifficultyLevel.Normal;
        public bool resetDifficultyAfterSession = true;
        public bool lockDifficultySelection;
        public DifficultyLevel Default => Valid(defaultDifficulty) ? defaultDifficulty : DifficultyLevel.Normal;
        public static bool Valid(DifficultyLevel level) => (int)level >= 0 && (int)level <= 3;
        public DifficultyConfig Get(DifficultyLevel level) => level == DifficultyLevel.Experience ? (extreme != null ? extreme : easy) : level == DifficultyLevel.Easy ? easy : level == DifficultyLevel.Hard ? hard : normal;
    }
}
