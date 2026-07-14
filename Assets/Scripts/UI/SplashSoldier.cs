using System.Collections;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// The man in the trees ahead of you. He walks away, the creature roars, he stops and
    /// turns and looks at it — and then the shooting in the sequence has somewhere to come
    /// from. Without him the gunfire is a sound effect. With him it is happening to someone.
    ///
    /// Driven on a fixed path with animator state names, not by EnemyAI: he is scenery, and
    /// wiring a NavMesh and a combat state machine into a scene that exists for ten seconds is
    /// how a splash screen ends up being the thing that crashes on first launch.
    /// </summary>
    public class SplashSoldier : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        [Header("Walk")]
        [SerializeField] private float walkSpeed = 1.1f;

        [Tooltip("Degrees per second he turns to face the thing behind him.")]
        [SerializeField] private float turnSpeed = 220f;

        /// <summary>
        /// State names on EnemySoldier.controller, which SoldierFactory builds. NOT the clip
        /// names inside the FBX — those are Idle_Gun / Gun_Shoot, and CrossFade to a state that
        /// does not exist is a silent no-op, so asking for the clip name leaves the soldier
        /// sliding along in his default pose with no error to explain it.
        /// </summary>
        private const string IdleState = "Idle";
        private const string RunState = "Run";
        private const string FireState = "Shoot";

        private bool _walking;
        private bool _turned;

        /// <summary>The state the animator is actually in. CrossFade has no idea, and re-issuing
        /// the same fade every frame restarts the clip from zero — a soldier who fires five
        /// shots 0.09s apart would never get past the first frame of the shoot pose.</summary>
        private string _current;

        /// <summary>Starts him walking. Held until the intro's opening cards are off the
        /// screen: he covers a metre a second, and ten seconds of walking behind a black
        /// curtain would put him out of the shot before anyone sees him.</summary>
        public void Begin()
        {
            _walking = true;
            Play(RunState);
        }

        private void Update()
        {
            if (_walking) transform.position += transform.forward * (walkSpeed * Time.deltaTime);
        }

        /// <summary>
        /// He heard it. Stops, turns to face the camera's side of the jungle, and raises the
        /// weapon. Called by SplashController on the roar that lands closest.
        /// </summary>
        public void Alert(Transform lookToward)
        {
            if (_turned) return;
            _turned = true;
            _walking = false;

            Play(IdleState);
            StartCoroutine(TurnTo(lookToward));
        }

        /// <summary>He fires. The controller calls this in step with the actual gunshots, so the
        /// pose and the report are the same event.</summary>
        public void Fire()
        {
            Play(FireState);
        }

        private IEnumerator TurnTo(Transform target)
        {
            // Face roughly where the thing is, not exactly — he is looking for it, not locked
            // onto it, and a perfect snap to target reads as a turret.
            Vector3 flat = target != null
                ? target.position - transform.position
                : -transform.forward;

            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) yield break;

            Quaternion to = Quaternion.LookRotation(flat)
                            * Quaternion.Euler(0f, Random.Range(-14f, 14f), 0f);

            while (Quaternion.Angle(transform.rotation, to) > 1f)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, to, turnSpeed * Time.deltaTime);
                yield return null;
            }
        }

        /// <summary>
        /// Crossfades to a state, once. Re-issuing the same fade restarts the clip from frame
        /// zero, and the burst calls Fire() five times inside half a second — each call landing
        /// well within the 0.15s fade of the one before it. Without this guard the shoot pose
        /// would be perpetually restarted and never advance: a man frozen mid-raise while five
        /// rounds go off.
        /// </summary>
        private void Play(string state)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            if (_current == state) return;

            _current = state;
            animator.CrossFade(state, 0.15f);
        }
    }
}
