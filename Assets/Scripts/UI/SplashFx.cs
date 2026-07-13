using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// The horror in the splash: the title flickers like a dying light, and the emblem
    /// creeps toward the viewer too slowly to notice consciously. Both are cheap tricks,
    /// and both work because they are irregular — a flicker on a timer is a metronome,
    /// a flicker on random gaps is a faulty tube.
    /// </summary>
    public class SplashFx : MonoBehaviour
    {
        [Header("Flicker")]
        [SerializeField] private CanvasGroup flickerTarget;
        [SerializeField] private float minGap = 1.2f;
        [SerializeField] private float maxGap = 4.5f;
        [SerializeField] private float minDrop = 0.15f;
        [SerializeField] private float maxDrop = 0.6f;

        [Header("Creep zoom")]
        [SerializeField] private RectTransform creepTarget;
        [SerializeField] private float creepScale = 1.09f;
        [SerializeField] private float creepSeconds = 14f;

        private float _elapsed;
        private float _nextFlickerAt;
        private float _flickerUntil;

        private void Start()
        {
            _nextFlickerAt = Time.time + Random.Range(0.4f, 1.2f);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;

            if (creepTarget != null)
            {
                float t = Mathf.Clamp01(_elapsed / creepSeconds);
                creepTarget.localScale = Vector3.one * Mathf.Lerp(1f, creepScale, t);
            }

            if (flickerTarget == null) return;

            if (Time.time >= _nextFlickerAt)
            {
                // A burst, not a single dip — real faulty lights stutter.
                _flickerUntil = Time.time + Random.Range(0.06f, 0.22f);
                _nextFlickerAt = Time.time + Random.Range(minGap, maxGap);
            }

            flickerTarget.alpha = Time.time < _flickerUntil
                ? Random.Range(minDrop, maxDrop)
                : 1f;
        }
    }
}
