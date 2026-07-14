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

        [Header("Death")]
        [Tooltip("Off. One life — dying ends the match, which is the only reason a match has " +
                 "ever ended, which is the only reason the profile has anything to record.")]
        [SerializeField] private bool respawnOnDeath;

        [SerializeField] private float respawnDelay = 3f;
        [SerializeField] private Transform playerSpawnPoint;

        [Tooltip("The camera that watches the island after you are dead.")]
        [SerializeField] private SpectatorCamera spectator;

        [Header("Scoring")]
        [SerializeField] private int pointsPerKill = 100;

        [Header("Lobby")]
        [SerializeField] private string lobbyScene = "Lobby";

        public int Kills { get; private set; }
        public int Score { get; private set; }
        public bool IsGameOver { get; private set; }

        /// <summary>A match is banked once. Quitting from the pause menu and then dying to the
        /// last enemy on the way out must not pay twice.</summary>
        private bool _recorded;

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

            RecordMatch();

            // Death is not the end of the round — you stay on the island and watch it go on
            // without you. The enemies keep hunting, the waves keep coming, and you can move
            // through it all as nothing at all.
            if (spectator != null && player != null)
                spectator.Begin(player.transform.position + Vector3.up * 2f);

            GameOver?.Invoke();
        }

        /// <summary>
        /// Banks the match into the profile — the kills, the score, the wave reached. Guarded,
        /// because both death and quitting land here and a player who quits during their own
        /// death animation should not be paid twice for it.
        /// </summary>
        private void RecordMatch()
        {
            if (_recorded) return;
            _recorded = true;

            int wave = spawner != null ? spawner.WaveNumber : 0;
            PlayerProfile.RecordMatch(Kills, Score, wave);
        }

        /// <summary>Leaves the match for the lobby, banking it on the way out. The pause menu
        /// and the game-over screen both come through here.</summary>
        public void QuitToLobby()
        {
            RecordMatch();

            Time.timeScale = 1f;   // the pause menu may have left it at zero
            UnityEngine.SceneManagement.SceneManager.LoadScene(lobbyScene);
        }

        /// <summary>
        /// Straight back into a fresh match. Reached from the game-over screen, so the match
        /// just played is already banked — the guard in RecordMatch is what makes that safe, and
        /// what stops a mid-match restart from paying out a run the player abandoned.
        /// </summary>
        public void Restart()
        {
            Time.timeScale = 1f;

            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }
}
