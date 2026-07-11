using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Moves the player capsule from a normalized 2D input vector and applies gravity.
    /// Rotation is owned by PlayerLook; this script only translates.
    ///
    /// The slide is the one piece of state here worth explaining. It is entered from a
    /// sprint, launches the player at a fixed speed along the direction they were already
    /// running, and decays — so it is a commitment, not a free dodge. Steering during it is
    /// deliberately weak.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float sprintSpeed = 7.5f;
        [SerializeField] private float acceleration = 14f;
        [SerializeField] private float airControl = 0.35f;

        [Header("Jump")]
        [SerializeField] private float jumpHeight = 1.15f;

        [Header("Slide")]
        [SerializeField] private float slideSpeed = 11f;
        [SerializeField] private float slideDuration = 0.85f;
        [SerializeField] private float slideFriction = 6f;
        [SerializeField] private float slideSteering = 2.5f;
        [SerializeField] private float slideCameraDrop = 0.55f;
        [SerializeField] private float slideCooldown = 0.5f;

        [Header("Gravity")]
        [SerializeField] private float gravity = -22f;
        [SerializeField] private float groundedStick = -2f;

        private CharacterController _controller;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;

        private float _slideEndsAt = -999f;
        private float _slideReadyAt;
        private Vector3 _slideDirection;
        private float _slideCurrentSpeed;

        public bool SprintHeld { get; set; }

        public bool IsGrounded => _controller.isGrounded;
        public bool IsSliding => Time.time < _slideEndsAt;
        public float CurrentSpeed => _horizontalVelocity.magnitude;

        /// <summary>How far the view should dip while sliding, in metres. The camera rig
        /// reads this rather than the motor reaching into the camera.</summary>
        public float CameraDrop => IsSliding ? slideCameraDrop : 0f;

        /// <summary>True while sprinting on the ground and actually moving.</summary>
        public bool IsSprinting =>
            SprintHeld && IsGrounded && !IsSliding && _horizontalVelocity.sqrMagnitude > 1f;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        /// <param name="input">x = strafe, y = forward. Expected magnitude 0..1.</param>
        public void Move(Vector2 input)
        {
            if (IsSliding)
                UpdateSlide(input);
            else
                UpdateWalk(input);

            ApplyGravity();

            Vector3 velocity = _horizontalVelocity;
            velocity.y = _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }

        private void UpdateWalk(Vector2 input)
        {
            Vector3 wish = transform.right * input.x + transform.forward * input.y;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            float target = SprintHeld ? sprintSpeed : walkSpeed;

            // Air control is throttled so a jump cannot be used to change direction freely.
            float accel = IsGrounded ? acceleration : acceleration * airControl;

            _horizontalVelocity = Vector3.Lerp(
                _horizontalVelocity, wish * target, accel * Time.deltaTime);
        }

        private void UpdateSlide(Vector2 input)
        {
            _slideCurrentSpeed = Mathf.Max(
                0f, _slideCurrentSpeed - slideFriction * Time.deltaTime);

            // Steering is weak on purpose: a slide is a committed move, not a free dodge.
            Vector3 steer = transform.right * input.x;
            _slideDirection = Vector3.Slerp(
                _slideDirection, (_slideDirection + steer * 0.5f).normalized,
                slideSteering * Time.deltaTime);

            _horizontalVelocity = _slideDirection * _slideCurrentSpeed;

            // Coming to a stop mid-slide should end it, or the player is stuck crouched.
            if (_slideCurrentSpeed < walkSpeed * 0.4f) EndSlide();
        }

        private void ApplyGravity()
        {
            // Keep a small downward bias while grounded so the controller stays snapped to
            // the floor on slopes and steps instead of skipping into the air.
            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = groundedStick;
            else
                _verticalVelocity += gravity * Time.deltaTime;
        }

        public void TryJump()
        {
            if (!IsGrounded) return;

            // A jump out of a slide is a cancel, and should carry the slide's speed with it.
            if (IsSliding) EndSlide(keepMomentum: true);

            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        /// <summary>
        /// Slides only out of a grounded sprint. Trying it from a standstill would be a free
        /// crouch, and trying it midair would be a dive.
        /// </summary>
        public void TrySlide()
        {
            if (IsSliding) return;
            if (Time.time < _slideReadyAt) return;
            if (!IsGrounded) return;
            if (!SprintHeld) return;
            if (_horizontalVelocity.sqrMagnitude < walkSpeed * walkSpeed * 0.5f) return;

            _slideDirection = _horizontalVelocity.normalized;
            _slideCurrentSpeed = slideSpeed;
            _slideEndsAt = Time.time + slideDuration;

            // Shrink the capsule so the player actually fits under things while sliding.
            _controller.height = 1f;
            _controller.center = new Vector3(0f, 0.5f, 0f);
        }

        private void EndSlide(bool keepMomentum = false)
        {
            _slideEndsAt = -999f;
            _slideReadyAt = Time.time + slideCooldown;

            _controller.height = 1.8f;
            _controller.center = new Vector3(0f, 0.9f, 0f);

            if (!keepMomentum)
                _horizontalVelocity = _slideDirection * Mathf.Min(_slideCurrentSpeed, walkSpeed);
        }
    }
}
