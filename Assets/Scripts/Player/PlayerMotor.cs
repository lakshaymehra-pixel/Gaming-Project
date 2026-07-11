using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Moves the player capsule from a normalized 2D input vector and applies gravity.
    /// Rotation is owned by PlayerLook; this script only translates.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float sprintSpeed = 7f;
        [SerializeField] private float acceleration = 14f;

        [Header("Gravity")]
        [SerializeField] private float gravity = -22f;
        [SerializeField] private float groundedStick = -2f;

        private CharacterController _controller;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;

        public bool IsSprinting { get; set; }
        public float CurrentSpeed => _horizontalVelocity.magnitude;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        /// <param name="input">x = strafe, y = forward. Expected magnitude 0..1.</param>
        public void Move(Vector2 input)
        {
            Vector3 wish = transform.right * input.x + transform.forward * input.y;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            float target = IsSprinting ? sprintSpeed : walkSpeed;
            _horizontalVelocity = Vector3.Lerp(
                _horizontalVelocity, wish * target, acceleration * Time.deltaTime);

            // Keep a small downward bias while grounded so the controller stays snapped
            // to the floor on slopes and steps instead of skipping into the air.
            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = groundedStick;
            else
                _verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = _horizontalVelocity;
            velocity.y = _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }
    }
}
