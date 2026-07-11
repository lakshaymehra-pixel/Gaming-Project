using Game.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Enemies
{
    /// <summary>
    /// Chase-and-shoot soldier. Runs a small state machine on a NavMeshAgent: close the
    /// distance until it has line of sight from inside its firing range, then hold and
    /// fire in bursts.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Health))]
    public class EnemyAI : MonoBehaviour
    {
        private enum State { Idle, Chase, Attack, Dead }

        [Header("Perception")]
        [SerializeField] private float sightRange = 40f;
        [SerializeField] private float attackRange = 18f;
        [SerializeField] private LayerMask sightBlockers = ~0;
        [SerializeField] private Transform eyes;

        [Header("Combat")]
        [SerializeField] private float damage = 9f;
        [SerializeField] private float secondsBetweenShots = 0.9f;
        [SerializeField] private float aimSpreadDegrees = 3.5f;
        [SerializeField] private float turnSpeed = 8f;

        [Header("Feedback")]
        [SerializeField] private ParticleSystem muzzleFlash;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip fireClip;

        [Header("Death")]
        [SerializeField] private float despawnDelay = 4f;

        private NavMeshAgent _agent;
        private Health _health;
        private Transform _target;
        private State _state = State.Idle;
        private float _nextShotTime;

        /// <summary>Points at the player once the spawner hands it over.</summary>
        public void SetTarget(Transform target) => _target = target;

        public Health Health => _health;

        /// <summary>Raised on every shot. The animator listens so the fire pose can sync
        /// to the actual trigger pull rather than guessing from state.</summary>
        public event System.Action Fired;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            if (eyes == null) eyes = transform;
        }

        private void OnEnable()
        {
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            _health.Died -= OnDied;
        }

        private void Update()
        {
            if (_state == State.Dead || _target == null) return;

            float distance = Vector3.Distance(transform.position, _target.position);
            bool canSee = distance <= sightRange && HasLineOfSight();

            switch (_state)
            {
                case State.Idle:
                    if (canSee) _state = State.Chase;
                    break;

                case State.Chase:
                    _agent.isStopped = false;
                    _agent.SetDestination(_target.position);
                    if (canSee && distance <= attackRange) _state = State.Attack;
                    break;

                case State.Attack:
                    _agent.isStopped = true;
                    FaceTarget();

                    // Back off from Attack the moment the shot is no longer available,
                    // otherwise the enemy stands still firing at a wall.
                    if (!canSee || distance > attackRange)
                    {
                        _state = State.Chase;
                        break;
                    }

                    if (Time.time >= _nextShotTime) Fire();
                    break;
            }
        }

        private bool HasLineOfSight()
        {
            Vector3 origin = eyes.position;
            Vector3 aimPoint = _target.position + Vector3.up * 1.2f;
            Vector3 direction = aimPoint - origin;

            if (!Physics.Raycast(origin, direction.normalized, out RaycastHit hit,
                                 direction.magnitude, sightBlockers,
                                 QueryTriggerInteraction.Ignore))
                return true; // nothing in between at all

            // The ray reached the player only if what it struck belongs to the target.
            return hit.transform.IsChildOf(_target) || hit.transform == _target;
        }

        private void FaceTarget()
        {
            Vector3 flat = _target.position - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) return;

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(flat),
                turnSpeed * Time.deltaTime);
        }

        private void Fire()
        {
            _nextShotTime = Time.time + secondsBetweenShots;

            Vector3 origin = eyes.position;
            Vector3 aimPoint = _target.position + Vector3.up * 1.2f;
            Vector3 direction = (aimPoint - origin).normalized;

            // Scatter the shot so the enemy is beatable — a perfect-accuracy AI is not fun.
            direction = Quaternion.Euler(
                Random.Range(-aimSpreadDegrees, aimSpreadDegrees),
                Random.Range(-aimSpreadDegrees, aimSpreadDegrees),
                0f) * direction;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, attackRange * 1.5f,
                                sightBlockers, QueryTriggerInteraction.Ignore))
            {
                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                    damageable.TakeDamage(damage, gameObject);
            }

            if (muzzleFlash != null) muzzleFlash.Play();
            if (audioSource != null && fireClip != null) audioSource.PlayOneShot(fireClip);

            Fired?.Invoke();
        }

        private void OnDied(GameObject killer)
        {
            _state = State.Dead;

            // Stop steering and stop blocking bullets, but leave the body visible for a beat
            // so the kill reads.
            if (_agent.isOnNavMesh) _agent.isStopped = true;
            _agent.enabled = false;

            foreach (var col in GetComponentsInChildren<Collider>())
                col.enabled = false;

            Destroy(gameObject, despawnDelay);
        }
    }
}
