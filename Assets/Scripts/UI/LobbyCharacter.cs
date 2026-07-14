using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// The soldier standing in the lobby. He is the biggest thing on the screen because in a
    /// game about being a person on an island, the person is the point — a lobby made of panels
    /// is a settings menu with a PLAY button on it.
    ///
    /// He breathes and he turns his head. That is all, and it is enough: a model on a plinth
    /// reads as a mannequin, and the difference between a mannequin and a character is about
    /// four lines of drift.
    /// </summary>
    public class LobbyCharacter : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        [Header("Idle motion")]
        [Tooltip("Degrees the body sways, either side of centre. Small — this is weight shifting " +
                 "from one foot to the other, not a dance.")]
        [SerializeField] private float swayDegrees = 2.5f;

        [SerializeField] private float swaySeconds = 6f;

        [Tooltip("How far he sinks and rises with the breath, in metres.")]
        [SerializeField] private float breathHeight = 0.012f;

        [SerializeField] private float breathSeconds = 3.4f;

        [Header("Turntable")]
        [Tooltip("Drag left or right on him to spin him. He drifts back to facing you when let go.")]
        [SerializeField] private float dragSensitivity = 0.35f;

        [SerializeField] private float returnSeconds = 2.5f;

        private Vector3 _home;
        private float _baseYaw;
        private float _dragged;      // degrees away from home, from the player's finger
        private float _idlePhase;

        private void Start()
        {
            _home = transform.position;
            _baseYaw = transform.eulerAngles.y;

            if (animator != null && animator.runtimeAnimatorController != null)
                animator.CrossFade("Idle", 0.2f);
        }

        private void Update()
        {
            Drag();
            Idle();
        }

        /// <summary>
        /// Spin him with a drag. The angle decays back to home when you let go, so he always
        /// ends up facing the player again and the lobby never settles into a shot of his back.
        /// </summary>
        private void Drag()
        {
            if (Input.GetMouseButton(0))
            {
                _dragged += Input.GetAxisRaw("Mouse X") * dragSensitivity * 60f * Time.deltaTime;
            }
            else if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Moved)
            {
                _dragged += Input.GetTouch(0).deltaPosition.x * dragSensitivity * 0.15f;
            }
            else
            {
                _dragged = Mathf.Lerp(_dragged, 0f, Time.deltaTime / returnSeconds);
                if (Mathf.Abs(_dragged) < 0.05f) _dragged = 0f;
            }
        }

        private void Idle()
        {
            _idlePhase += Time.deltaTime;

            // Two periods that do not divide into each other, so the loop never lands on the
            // same pose twice and he never looks like he is on a timer.
            float sway = Mathf.Sin(_idlePhase / swaySeconds * Mathf.PI * 2f) * swayDegrees;
            float breath = Mathf.Sin(_idlePhase / breathSeconds * Mathf.PI * 2f) * breathHeight;

            transform.rotation = Quaternion.Euler(0f, _baseYaw + sway + _dragged, 0f);
            transform.position = _home + Vector3.up * breath;
        }
    }
}
