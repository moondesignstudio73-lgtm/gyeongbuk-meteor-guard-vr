using UnityEngine;

namespace MeteorDefenseVR.Meteor
{
    [CreateAssetMenu(menuName = "Meteor Defense/Meteor Definition", fileName = "MeteorDefinition")]
    public sealed class MeteorDefinition : ScriptableObject
    {
        [SerializeField] private MeteorType meteorType = MeteorType.Normal;
        [SerializeField, Min(1f)] private float health = 1f;
        [SerializeField, Min(0f)] private float speed = 2f;
        [SerializeField, Min(0)] private int damage = 10;
        [SerializeField, Min(0)] private int score = 100;
        [SerializeField, Min(0.1f)] private float size = 1f;
        [SerializeField] private bool isTargetable = true;

        public MeteorType MeteorType => meteorType;
        public float Health => health;
        public float Speed => speed;
        public int Damage => damage;
        public int Score => score;
        public float Size => size;
        public bool IsTargetable => isTargetable;

        public void Configure(MeteorType type, float maxHealth, float movementSpeed, int playerDamage, int scoreValue, float scale, bool targetable = true)
        {
            meteorType = type;
            health = Mathf.Max(1f, maxHealth);
            speed = Mathf.Max(0f, movementSpeed);
            damage = Mathf.Max(0, playerDamage);
            score = Mathf.Max(0, scoreValue);
            size = Mathf.Max(0.1f, scale);
            isTargetable = targetable;
        }
    }
}
