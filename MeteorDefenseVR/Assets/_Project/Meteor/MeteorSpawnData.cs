using System;
using UnityEngine;

namespace MeteorDefenseVR.Meteor
{
    [Serializable]
    public sealed class MeteorSpawnData
    {
        [SerializeField] private MeteorType meteorType = MeteorType.Normal;
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(1)] private int count = 1;
        [SerializeField, Min(0f)] private float spawnInterval = 2f;
        [SerializeField, Min(0f)] private float delay;
        [SerializeField, Min(0f)] private float speed = 2.2f;
        [SerializeField, Min(1f)] private float health = 1f;
        [SerializeField, Min(0)] private int damage = 10;
        [SerializeField, Min(0)] private int score = 100;
        [SerializeField, Min(0.1f)] private float size = 1f;
        [SerializeField] private Vector2 spawnArea = new Vector2(35f, 25f);

        public MeteorType MeteorType => meteorType;
        public GameObject Prefab => prefab;
        public int Count => Mathf.Max(1, count);
        public float SpawnInterval => Mathf.Max(0f, spawnInterval);
        public float Delay => Mathf.Max(0f, delay);
        public float Speed => Mathf.Max(0f, speed);
        public float Health => Mathf.Max(1f, health);
        public int Damage => Mathf.Max(0, damage);
        public int Score => Mathf.Max(0, score);
        public float Size => Mathf.Max(0.1f, size);
        public Vector2 SpawnArea => new Vector2(Mathf.Abs(spawnArea.x), Mathf.Abs(spawnArea.y));
        public float EstimatedDuration => Delay + Mathf.Max(0, Count - 1) * SpawnInterval;

        public void Configure(
            MeteorType type,
            GameObject meteorPrefab,
            int meteorCount,
            float interval,
            float initialDelay,
            float movementSpeed,
            float maxHealth,
            int playerDamage,
            int scoreValue,
            float scale,
            Vector2 area)
        {
            meteorType = type;
            prefab = meteorPrefab;
            count = Mathf.Max(1, meteorCount);
            spawnInterval = Mathf.Max(0f, interval);
            delay = Mathf.Max(0f, initialDelay);
            speed = Mathf.Max(0f, movementSpeed);
            health = Mathf.Max(1f, maxHealth);
            damage = Mathf.Max(0, playerDamage);
            score = Mathf.Max(0, scoreValue);
            size = Mathf.Max(0.1f, scale);
            spawnArea = new Vector2(Mathf.Abs(area.x), Mathf.Abs(area.y));
        }
    }
}
