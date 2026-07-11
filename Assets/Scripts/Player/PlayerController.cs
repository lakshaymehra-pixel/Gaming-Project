using Game.Core;
using Game.Weapons;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Wires input to motion, aim, and the gun. Everything it drives is a separate
    /// component, so this stays a thin router rather than a god object.
    /// </summary>
    [RequireComponent(typeof(PlayerInputHub))]
    [RequireComponent(typeof(PlayerMotor))]
    [RequireComponent(typeof(PlayerLook))]
    [RequireComponent(typeof(Health))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Weapon")]
        [SerializeField] private Weapon weapon;
        [SerializeField] private RecoilController recoil;
        [SerializeField] private WeaponAnimator weaponAnimator;

        [Header("Camera")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private float hipFov = 70f;
        [SerializeField] private float aimFov = 50f;
        [SerializeField] private float sprintFov = 78f;
        [SerializeField] private float fovSpeed = 10f;

        [Header("Slide")]
        [SerializeField] private float cameraDropSpeed = 12f;

        private PlayerInputHub _input;
        private PlayerMotor _motor;
        private PlayerLook _look;
        private Health _health;

        private float _pivotRestHeight;
        private float _currentDrop;

        public Health Health => _health;
        public Weapon Weapon => weapon;
        public PlayerMotor Motor => _motor;
        public bool IsAiming { get; private set; }

        private void Awake()
        {
            _input = GetComponent<PlayerInputHub>();
            _motor = GetComponent<PlayerMotor>();
            _look = GetComponent<PlayerLook>();
            _health = GetComponent<Health>();

            if (cameraPivot != null)
                _pivotRestHeight = cameraPivot.localPosition.y;
        }

        private void OnEnable()
        {
            if (weapon != null) weapon.RecoilKick += OnRecoil;
        }

        private void OnDisable()
        {
            if (weapon != null) weapon.RecoilKick -= OnRecoil;
        }

        private void Update()
        {
            if (_health.IsDead) return;

            // Aiming down sights while sprinting or sliding makes no sense, and letting both
            // run at once fights over the FOV.
            IsAiming = _input.AimHeld && !_motor.IsSliding && !_motor.IsSprinting;

            _look.IsAiming = IsAiming;
            _motor.SprintHeld = _input.SprintHeld;

            _look.Look(_input.Look);

            if (_input.JumpPressed) _motor.TryJump();
            if (_input.SlidePressed) _motor.TrySlide();

            _motor.Move(_input.Move);

            if (weapon != null)
            {
                // A sliding player cannot shoot straight, and neither can a sprinting one.
                bool canFire = !_motor.IsSliding;
                weapon.UpdateTrigger(canFire && _input.FireHeld, IsAiming);

                if (_input.ReloadPressed) weapon.TryReload();
            }

            if (weaponAnimator != null) weaponAnimator.IsAiming = IsAiming;

            UpdateFov();
            UpdateCameraDrop();
        }

        private void UpdateFov()
        {
            if (viewCamera == null) return;

            float target = hipFov;
            if (IsAiming) target = aimFov;
            else if (_motor.IsSprinting || _motor.IsSliding) target = sprintFov;

            viewCamera.fieldOfView = Mathf.Lerp(
                viewCamera.fieldOfView, target, fovSpeed * Time.deltaTime);
        }

        private void UpdateCameraDrop()
        {
            if (cameraPivot == null) return;

            // The motor decides how low the slide sits; the controller just moves the eye
            // there. Doing it here keeps the motor from reaching into the camera rig.
            _currentDrop = Mathf.Lerp(
                _currentDrop, _motor.CameraDrop, cameraDropSpeed * Time.deltaTime);

            Vector3 p = cameraPivot.localPosition;
            p.y = _pivotRestHeight - _currentDrop;
            cameraPivot.localPosition = p;
        }

        private void OnRecoil(Vector2 kick)
        {
            if (recoil != null) recoil.AddRecoil(kick);
        }

        public void Respawn(Vector3 position, float yaw)
        {
            // The CharacterController overrides transform writes while enabled, so it has to
            // be switched off for the teleport to land.
            var cc = GetComponent<CharacterController>();
            cc.enabled = false;
            transform.position = position;
            cc.enabled = true;

            _look.SnapTo(yaw);
            _health.Revive();
        }
    }
}
