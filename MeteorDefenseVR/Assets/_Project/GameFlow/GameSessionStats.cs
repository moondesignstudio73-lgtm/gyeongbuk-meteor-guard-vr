using MeteorDefenseVR.Combat;
using MeteorDefenseVR.Meteor;
using MeteorDefenseVR.Player;
using MeteorDefenseVR.UI;
using UnityEngine;

namespace MeteorDefenseVR.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class GameSessionStats : MonoBehaviour
    {
        [SerializeField] private MeteorSpawner spawner;
        [SerializeField] private BossClimaxController bossClimax;
        [SerializeField] private LaserWeapon laserWeapon;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private MissionHudController missionHud;

        private int currentCombo;
        private bool sessionRunning;
        private bool bossCounted;

        public int Score { get; private set; }
        public int DestroyedMeteors { get; private set; }
        public int TotalMeteors { get; private set; }
        public int Shots { get; private set; }
        public int Locks { get; private set; }
        public int SuccessfulHits { get; private set; }
        public int DamageTaken { get; private set; }
        public int MaxCombo { get; private set; }
        public float PlayTime { get; private set; }
        public float Accuracy => Shots > 0 ? (float)SuccessfulHits / Shots : 0f;

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void Update() => Tick(Time.unscaledDeltaTime);

        public void Configure(
            MeteorSpawner meteorSpawner,
            BossClimaxController boss,
            LaserWeapon weapon,
            PlayerHealth health,
            MissionHudController hud)
        {
            Unsubscribe();
            spawner = meteorSpawner;
            bossClimax = boss;
            laserWeapon = weapon;
            playerHealth = health;
            missionHud = hud;
            Subscribe();
        }

        public void ResetSession(int totalMeteors = 0)
        {
            Score = 0;
            DestroyedMeteors = 0;
            TotalMeteors = Mathf.Max(0, totalMeteors);
            Shots = 0;
            Locks = 0;
            SuccessfulHits = 0;
            DamageTaken = 0;
            currentCombo = 0;
            MaxCombo = 0;
            PlayTime = 0f;
            bossCounted = false;
            sessionRunning = true;
        }

        public void StopSession() => sessionRunning = false;
        public void SetScore(int score) => Score = Mathf.Max(0, score);
        public void RecordShotAndLock() { Shots++; Locks++; }
        public void RecordSuccessfulHit() => SuccessfulHits++;
        public void RecordDamage(int damage) => DamageTaken += Mathf.Max(0, damage);

        public void RecordMeteorDestroyed()
        {
            DestroyedMeteors++;
            currentCombo++;
            MaxCombo = Mathf.Max(MaxCombo, currentCombo);
        }

        public void RecordMeteorMissed() => currentCombo = 0;

        public void Tick(float deltaTime)
        {
            if (sessionRunning) PlayTime += Mathf.Max(0f, deltaTime);
        }

        public GameSessionSnapshot CreateSnapshot()
        {
            return new GameSessionSnapshot
            {
                Score = Score,
                DestroyedMeteors = DestroyedMeteors,
                TotalMeteors = TotalMeteors,
                Shots = Shots,
                Locks = Locks,
                SuccessfulHits = SuccessfulHits,
                DamageTaken = DamageTaken,
                MaxCombo = MaxCombo,
                PlayTime = PlayTime
            };
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled) return;
            if (spawner != null)
            {
                spawner.SpawningStarted -= HandleSpawningStarted;
                spawner.MeteorCompleted -= HandleMeteorCompleted;
                spawner.SpawningStarted += HandleSpawningStarted;
                spawner.MeteorCompleted += HandleMeteorCompleted;
            }
            if (bossClimax != null)
            {
                bossClimax.BossSpawned -= HandleBossSpawned;
                bossClimax.BossDestroyed -= HandleBossDestroyed;
                bossClimax.BossSpawned += HandleBossSpawned;
                bossClimax.BossDestroyed += HandleBossDestroyed;
            }
            if (laserWeapon != null)
            {
                laserWeapon.Fired -= HandleFired;
                laserWeapon.Hit -= HandleHit;
                laserWeapon.Fired += HandleFired;
                laserWeapon.Hit += HandleHit;
            }
            if (playerHealth != null)
            {
                playerHealth.DamageTaken -= HandleDamage;
                playerHealth.DamageTaken += HandleDamage;
            }
            if (missionHud != null)
            {
                missionHud.ScoreChanged -= SetScore;
                missionHud.ScoreChanged += SetScore;
            }
        }

        private void Unsubscribe()
        {
            if (spawner != null)
            {
                spawner.SpawningStarted -= HandleSpawningStarted;
                spawner.MeteorCompleted -= HandleMeteorCompleted;
            }
            if (bossClimax != null)
            {
                bossClimax.BossSpawned -= HandleBossSpawned;
                bossClimax.BossDestroyed -= HandleBossDestroyed;
            }
            if (laserWeapon != null)
            {
                laserWeapon.Fired -= HandleFired;
                laserWeapon.Hit -= HandleHit;
            }
            if (playerHealth != null) playerHealth.DamageTaken -= HandleDamage;
            if (missionHud != null) missionHud.ScoreChanged -= SetScore;
        }

        private void HandleSpawningStarted(int total) => ResetSession(total);

        private void HandleMeteorCompleted(MeteorController meteor)
        {
            if (meteor != null && meteor.State == MeteorLifecycleState.Destroyed) RecordMeteorDestroyed();
            else RecordMeteorMissed();
        }

        private void HandleBossSpawned(MeteorController _)
        {
            if (bossCounted) return;
            bossCounted = true;
            TotalMeteors++;
        }

        private void HandleBossDestroyed() => RecordMeteorDestroyed();
        private void HandleFired(GazeLockOnTarget _, MeteorController __) => RecordShotAndLock();
        private void HandleHit(MeteorController _, Vector3 __) => RecordSuccessfulHit();
        private void HandleDamage(int amount, float _) => RecordDamage(amount);
    }
}
