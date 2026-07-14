using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Walks the splash camera into the jungle. Nobody is holding it — the bob and the drift
    /// are what make it read as a person rather than a dolly, and that is the whole job.
    ///
    /// SplashController owns the beats: it calls Halt() when the creature roars, because the
    /// most frightening thing the camera can do at that moment is stop.
    /// </summary>
    public class SplashCamera : MonoBehaviour
    {
        [Header("Walk")]
        [Tooltip("How far the camera gets if nothing stops it.")]
        [SerializeField] private float walkDistance = 7f;

        [Tooltip("Seconds to cover that distance at a normal pace. The roar halts the camera " +
                 "about a second and a half in, long before it gets there — walkDistance is " +
                 "the road, not the journey, and it has to be longer than the walk or the " +
                 "halt lands on a camera that already stopped.")]
        [SerializeField] private float walkSeconds = 9f;

        [Header("Handheld")]
        [Tooltip("Vertical bob, metres. A footfall, not a bounce — much over 2cm and it reads " +
                 "as a camera on a spring.")]
        [SerializeField] private float bobHeight = 0.018f;

        [Tooltip("Footfalls per second.")]
        [SerializeField] private float bobRate = 1.55f;

        [Tooltip("Slow wander of the aim, degrees. Nobody holds a rifle perfectly still.")]
        [SerializeField] private float swayDegrees = 1.4f;

        private Vector3 _startPosition;
        private Quaternion _startRotation;

        /// <summary>The path, fixed at Awake. Walking along the *current* forward would let the
        /// sway steer the camera: a 1.4° wander seven metres out is a 17cm slew, ten times the
        /// bob it is supposed to sit under, and it grows the further the camera gets.</summary>
        private Vector3 _forward;
        private Vector3 _right;

        private float _travelled;
        private float _speed;
        private float _bobPhase;

        /// <summary>Multiplies the walk speed. The controller rides this to 0 on the roar.</summary>
        private float _throttle = 1f;

        /// <summary>Nothing moves until the intro says so. The BGMI cards hold a near-black
        /// curtain over the whole screen for their first ten seconds, and a camera that walks
        /// its route behind that curtain arrives with the walk already spent.</summary>
        private bool _walking;

        private void Awake()
        {
            _startPosition = transform.position;
            _startRotation = transform.rotation;

            _forward = transform.forward;
            _right = transform.right;

            _speed = walkDistance / Mathf.Max(0.01f, walkSeconds);
        }

        /// <summary>Starts the walk. Called when the curtain lifts, not on Awake.</summary>
        public void Begin() => _walking = true;

        private void Update()
        {
            if (_walking)
            {
                float step = _speed * _throttle * Time.deltaTime;
                _travelled = Mathf.Min(_travelled + step, walkDistance);

                // Bob only accumulates while moving. A camera that keeps bobbing after it has
                // stopped is a camera that is breathing, and this one stopped for a worse reason.
                _bobPhase += bobRate * _throttle * Time.deltaTime * Mathf.PI * 2f;
            }

            // The double bounce of a footfall: the body dips on each step, so the vertical runs
            // at twice the stride.
            float bob = Mathf.Sin(_bobPhase * 2f) * bobHeight;
            float lateral = Mathf.Sin(_bobPhase) * bobHeight * 0.6f;

            transform.position = _startPosition
                                 + _forward * _travelled
                                 + Vector3.up * bob
                                 + _right * lateral;

            // Two out-of-phase sines: a wander that never repeats on any beat you can hear.
            // Rotation only — the position above is on the fixed path.
            float t = Time.time;
            transform.rotation = _startRotation * Quaternion.Euler(
                Mathf.Sin(t * 0.41f) * swayDegrees * 0.5f,
                Mathf.Sin(t * 0.27f) * swayDegrees,
                Mathf.Sin(t * 0.33f) * swayDegrees * 0.25f);
        }

        /// <summary>
        /// Stops the walk over <paramref name="seconds"/>. Not a hard cut: a man does not
        /// freeze instantly, he slows and then he does not move at all, and the difference
        /// between those two is most of the fear.
        /// </summary>
        public void Halt(float seconds)
        {
            StopAllCoroutines();
            StartCoroutine(Throttle(0f, seconds));
        }

        /// <summary>Backs the camera off, hard — used on the emblem slam.</summary>
        public void Recoil(float metres)
        {
            _travelled = Mathf.Max(0f, _travelled - metres);
        }

        private System.Collections.IEnumerator Throttle(float target, float seconds)
        {
            float from = _throttle;
            float t = 0f;

            while (t < seconds)
            {
                t += Time.deltaTime;
                _throttle = Mathf.Lerp(from, target, t / seconds);
                yield return null;
            }

            _throttle = target;
        }
    }
}
