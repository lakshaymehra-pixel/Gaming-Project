using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Round-based spawner. Each wave is bigger than the last and only ends once every
    /// enemy it produced is dead, so the player always gets a breather between rounds.
    /// </summary>
    public class WaveSpawner : MonoBehaviour
    {
        [Header("Spawning")]
        [SerializeField] private EnemyAI enemyPrefab;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private Transform target;

        [Header("Wave shape")]
        [SerializeField] private int enemiesInFirstWave = 4;
        [SerializeField] private int extraEnemiesPerWave = 2;
        [SerializeField] private int maxAliveAtOnce = 8;
        [SerializeField] private float secondsBetweenSpawns = 0.8f;
        [SerializeField] private float secondsBetweenWaves = 5f;

        private readonly List<EnemyAI> _alive = new();
        private int _waveNumber;
        private bool _running;

        public int WaveNumber => _waveNumber;
        public int AliveCount => _alive.Count;

        public event Action<int> WaveStarted;
        public event Action<int> WaveCleared;
        public event Action<EnemyAI> EnemyKilled;

        private void Start()
        {
            if (enemyPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogError($"{name}: enemy prefab or spawn points not set.", this);
                enabled = false;
                return;
            }

            StartCoroutine(RunWaves());
        }

        public void SetTarget(Transform t) => target = t;

        /// <summary>Stops producing new waves. Enemies already alive are left alone.</summary>
        public void StopSpawning() => _running = false;

        private IEnumerator RunWaves()
        {
            _running = true;

            while (_running)
            {
                _waveNumber++;
                int count = enemiesInFirstWave + (_waveNumber - 1) * extraEnemiesPerWave;
                WaveStarted?.Invoke(_waveNumber);

                yield return SpawnWave(count);

                // Hold the wave open until the arena is actually clear.
                while (_running && _alive.Count > 0)
                    yield return null;

                if (!_running) yield break;

                WaveCleared?.Invoke(_waveNumber);
                yield return new WaitForSeconds(secondsBetweenWaves);
            }
        }

        private IEnumerator SpawnWave(int count)
        {
            for (int i = 0; i < count && _running; i++)
            {
                // Respect the concurrency cap so a late wave cannot drop 20 agents at once
                // and tank the frame rate on a phone.
                while (_running && _alive.Count >= maxAliveAtOnce)
                    yield return null;

                if (!_running) yield break;

                SpawnOne();
                yield return new WaitForSeconds(secondsBetweenSpawns);
            }
        }

        private void SpawnOne()
        {
            Transform point = PickSpawnPoint();
            EnemyAI enemy = Instantiate(enemyPrefab, point.position, point.rotation);
            enemy.SetTarget(target);

            _alive.Add(enemy);

            // Capture the reference so the handler still knows who died after the
            // component is torn down.
            EnemyAI captured = enemy;
            captured.Health.Died += _ => OnEnemyDied(captured);
        }

        /// <summary>Prefers the spawn point furthest from the player so enemies do not
        /// materialise in the player's face.</summary>
        private Transform PickSpawnPoint()
        {
            if (target == null || spawnPoints.Length == 1)
                return spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];

            // Sample a few candidates and take the best, rather than always using the single
            // furthest point — that would make every enemy arrive from one corner.
            Transform best = spawnPoints[0];
            float bestScore = float.MinValue;

            for (int i = 0; i < 3; i++)
            {
                Transform candidate = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
                float score = Vector3.SqrMagnitude(candidate.position - target.position);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private void OnEnemyDied(EnemyAI enemy)
        {
            _alive.Remove(enemy);
            EnemyKilled?.Invoke(enemy);
        }
    }
}
