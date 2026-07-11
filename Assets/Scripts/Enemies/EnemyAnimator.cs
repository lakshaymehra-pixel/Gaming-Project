using Game.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Enemies
{
    /// <summary>
    /// Drives the soldier model's Animator from what the enemy is actually doing: agent
    /// velocity picks Idle or Run, the AI's Fired event triggers the shoot pose, and death
    /// plays once and stays down. States are crossfaded by name — the controller asset has
    /// no transitions of its own, so this script is the entire brain, and there is no state
    /// graph to keep in sync with the code.
    /// </summary>
    public class EnemyAnimator : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        [Tooltip("Agent speed above which the run cycle plays.")]
        [SerializeField] private float runThreshold = 0.4f;

        [Tooltip("How long the shoot pose owns the body before locomotion resumes.")]
        [SerializeField] private float shootPoseSeconds = 0.45f;

        private NavMeshAgent _agent;
        private EnemyAI _ai;
        private Health _health;

        private string _current;
        private float _busyUntil;
        private bool _dead;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _ai = GetComponent<EnemyAI>();
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            if (_ai != null) _ai.Fired += OnFired;
            if (_health != null) _health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_ai != null) _ai.Fired -= OnFired;
            if (_health != null) _health.Died -= OnDied;
        }

        private void Update()
        {
            if (_dead || animator == null) return;
            if (Time.time < _busyUntil) return;   // shoot pose still owns the body

            float speed = _agent != null && _agent.enabled ? _agent.velocity.magnitude : 0f;
            Play(speed > runThreshold ? "Run" : "Idle", 0.15f);
        }

        private void OnFired()
        {
            if (_dead || animator == null) return;

            Play("Shoot", 0.05f);
            _busyUntil = Time.time + shootPoseSeconds;

            // Clear the cache so locomotion re-crossfades cleanly when the pose releases.
            _current = null;
        }

        private void OnDied(GameObject killer)
        {
            _dead = true;
            if (animator != null) Play("Death", 0.1f);
        }

        private void Play(string state, float fade)
        {
            if (_current == state) return;
            _current = state;
            animator.CrossFadeInFixedTime(state, fade);
        }
    }
}
