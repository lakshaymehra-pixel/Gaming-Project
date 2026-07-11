using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Damageable pool shared by the player and every enemy. Regeneration is opt-in and
    /// pauses for a beat after each hit, the way most modern shooters handle it.
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [Header("Pool")]
        [SerializeField] private float maxHealth = 100f;

        [Header("Regeneration")]
        [SerializeField] private bool regenerates;
        [SerializeField] private float regenPerSecond = 20f;
        [SerializeField] private float regenDelayAfterHit = 4f;

        private float _current;
        private float _lastHitTime = -999f;

        public float Current => _current;
        public float Max => maxHealth;
        public float Normalized => maxHealth <= 0f ? 0f : _current / maxHealth;
        public bool IsDead { get; private set; }

        /// <summary>Fired on every damage event with the new normalized value.</summary>
        public event Action<float> Changed;
        public event Action<GameObject> Died;

        private void Awake()
        {
            _current = maxHealth;
        }

        private void Update()
        {
            if (IsDead || !regenerates) return;
            if (_current >= maxHealth) return;
            if (Time.time - _lastHitTime < regenDelayAfterHit) return;

            _current = Mathf.Min(maxHealth, _current + regenPerSecond * Time.deltaTime);
            Changed?.Invoke(Normalized);
        }

        public void TakeDamage(float amount, GameObject source)
        {
            if (IsDead || amount <= 0f) return;

            _current = Mathf.Max(0f, _current - amount);
            _lastHitTime = Time.time;
            Changed?.Invoke(Normalized);

            if (_current <= 0f)
            {
                IsDead = true;
                Died?.Invoke(source);
            }
        }

        public void Revive()
        {
            IsDead = false;
            _current = maxHealth;
            _lastHitTime = -999f;
            Changed?.Invoke(Normalized);
        }
    }
}
