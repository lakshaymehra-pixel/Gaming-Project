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

        [Header("Aim down sights")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private float hipFov = 70f;
        [SerializeField] private float aimFov = 50f;
        [SerializeField] private float fovSpeed = 12f;

        private PlayerInputHub _input;
        private PlayerMotor _motor;
        private PlayerLook _look;
        private Health _health;

        public Health Health => _health;
        public Weapon Weapon => weapon;
        public bool IsAiming { get; private set; }

        private void Awake()
        {
            _input = GetComponent<PlayerInputHub>();
            _motor = GetComponent<PlayerMotor>();
            _look = GetComponent<PlayerLook>();
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            if (weapon != null) weapon.RecoilKick += OnRecoil;
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (weapon != null) weapon.RecoilKick -= OnRecoil;
            _health.Died -= OnDied;
        }

        private void Update()
        {
            if (_health.IsDead) return;

            IsAiming = _input.AimHeld;
            _look.IsAiming = IsAiming;

            _look.Look(_input.Look);
            _motor.Move(_input.Move);

            if (weapon != null)
            {
                weapon.UpdateTrigger(_input.FireHeld, IsAiming);
                if (_input.ReloadPressed) weapon.TryReload();
            }

            UpdateFov();
        }

        private void UpdateFov()
        {
            if (viewCamera == null) return;

            float target = IsAiming ? aimFov : hipFov;
            viewCamera.fieldOfView = Mathf.Lerp(
                viewCamera.fieldOfView, target, fovSpeed * Time.deltaTime);
        }

        private void OnRecoil(Vector2 kick)
        {
            if (recoil != null) recoil.AddRecoil(kick);
        }

        private void OnDied(GameObject killer)
        {
            // Movement and firing are gated on IsDead in Update; the game loop owns
            // what happens next (respawn, game over screen).
        }

        public void Respawn(Vector3 position, float yaw)
        {
            // The CharacterController overrides transform writes while enabled, so it has
            // to be switched off for the teleport to land.
            var cc = GetComponent<CharacterController>();
            cc.enabled = false;
            transform.position = position;
            cc.enabled = true;

            _look.SnapTo(yaw);
            _health.Revive();
        }
    }
}
