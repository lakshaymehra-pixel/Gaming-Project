using System.Collections.Generic;
using UnityEngine;

namespace Game.Weapons
{
    /// <summary>
    /// Reuses a fixed set of LineRenderers for bullet tracers. Instantiating one per shot
    /// would allocate several hundred objects a minute on a full-auto gun and stutter the
    /// frame on a mid-range phone.
    /// </summary>
    public class TracerPool : MonoBehaviour
    {
        [SerializeField] private LineRenderer tracerPrefab;
        [SerializeField] private int poolSize = 24;
        [SerializeField] private float lifetime = 0.05f;

        private readonly List<LineRenderer> _pool = new();
        private readonly List<float> _expiry = new();
        private int _next;

        private void Awake()
        {
            if (tracerPrefab == null)
            {
                Debug.LogWarning($"{name}: no tracer prefab assigned; tracers disabled.", this);
                enabled = false;
                return;
            }

            for (int i = 0; i < poolSize; i++)
            {
                LineRenderer lr = Instantiate(tracerPrefab, transform);
                lr.gameObject.SetActive(false);
                lr.positionCount = 2;
                _pool.Add(lr);
                _expiry.Add(0f);
            }
        }

        public void Spawn(Vector3 from, Vector3 to)
        {
            if (!enabled || _pool.Count == 0) return;

            LineRenderer lr = _pool[_next];
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.gameObject.SetActive(true);
            _expiry[_next] = Time.time + lifetime;

            _next = (_next + 1) % _pool.Count;
        }

        private void Update()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                if (_pool[i].gameObject.activeSelf && Time.time >= _expiry[i])
                    _pool[i].gameObject.SetActive(false);
            }
        }
    }
}
