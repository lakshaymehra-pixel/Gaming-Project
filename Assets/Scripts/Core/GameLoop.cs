using System;
using System.Collections;
using Game.Enemies;
using Game.Player;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Owns the match: keeps score, hands the spawner its target, and decides what
    /// happens when the player dies.
    /// </summary>
    public class GameLoop : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerController player;
        [SerializeField] private WaveSpawner spawner;

        [Header("Respawn")]
        [SerializeField] private bool respawnOnDeath = true;
        [SerializeField] private float respawnDelay = 3f;
        [SerializeField] private Transform playerSpawnPoint;

        [Header("Scoring")]
        [SerializeField] private int pointsPerKill = 100;

        public int Kills { get; private set; }
        public int Score { get; private set; }
        public bool IsGameOver { get; private set; }

        public PlayerController Player => player;
        public WaveSpawner Spawner => spawner;

        public event Action ScoreChanged;
        public event Action PlayerDied;
        public event Action PlayerRespawned;
        public event Action GameOver;

        private void Start()
        {
            if (player == null || spawner == null)
            {
                Debug.LogError($"{name}: player or spawner not assigned.", this);
                enabled = false;
                return;
            }

            spawner.SetTarget(player.transform);
            spawner.EnemyKilled += OnEnemyKilled;
            player.Health.Died += OnPlayerDied;

            Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
            if (spawner != null) spawner.EnemyKilled -= OnEnemyKilled;
            if (player != null) player.Health.Died -= OnPlayerDied;
        }

        private void OnEnemyKilled(EnemyAI enemy)
        {
            Kills++;
            Score += pointsPerKill;
            ScoreChanged?.Invoke();
        }

        private void OnPlayerDied(GameObject killer)
        {
            PlayerDied?.Invoke();

            if (respawnOnDeath)
                StartCoroutine(RespawnAfterDelay());
            else
                EndGame();
        }

        private IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(respawnDelay);

            Vector3 position = playerSpawnPoint != null
                ? playerSpawnPoint.position
                : Vector3.up;
            float yaw = playerSpawnPoint != null
                ? playerSpawnPoint.eulerAngles.y
                : 0f;

            player.Respawn(position, yaw);
            PlayerRespawned?.Invoke();
        }

        private void EndGame()
        {
            IsGameOver = true;
            spawner.StopSpawning();
            GameOver?.Invoke();
        }

        public void Restart()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }
}
