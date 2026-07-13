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

        [Header("TPP / FPP (BGMI style)")]
        [SerializeField] private Vector3 tppOffset = new(0.5f, 0.3f, -2.5f);
        [SerializeField] private float tppFov = 60f;
        [SerializeField] private float viewSwitchSpeed = 12f;
        [SerializeField] private float cameraCollisionRadius = 0.2f;
        [SerializeField] private LayerMask cameraCollisionMask = ~0;
        [SerializeField] private GameObject playerBody;

        [Header("Slide")]
        [SerializeField] private float cameraDropSpeed = 12f;

        private PlayerInputHub _input;
        private PlayerMotor _motor;
        private PlayerLook _look;
        private Health _health;

        private float _pivotRestHeight;
        private float _currentDrop;
        private Vector3 _currentCamLocal;   // smoothed camera local position
        private bool _isTPP = true;         // start in TPP like BGMI

        public Health Health => _health;
        public Weapon Weapon => weapon;
        public PlayerMotor Motor => _motor;
        public bool IsAiming { get; private set; }
        public bool IsTPP => _isTPP;

        private void Awake()
        {
            _input = GetComponent<PlayerInputHub>();
            _motor = GetComponent<PlayerMotor>();
            _look = GetComponent<PlayerLook>();
            _health = GetComponent<Health>();

            if (cameraPivot != null)
                _pivotRestHeight = cameraPivot.localPosition.y;

            // Start in TPP
            _isTPP = true;
            if (viewCamera != null)
                _currentCamLocal = tppOffset;
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

            // BGMI style: AIM switches to FPP, release goes back to TPP
            _isTPP = !IsAiming;

            UpdateFov();
            UpdateCameraMode();
            UpdateCameraDrop();
        }

        private void UpdateFov()
        {
            if (viewCamera == null) return;

            float target;
            if (IsAiming) target = aimFov;               // FPP aim
            else if (_motor.IsSprinting || _motor.IsSliding) target = sprintFov;
            else target = _isTPP ? tppFov : hipFov;      // TPP uses wider FOV

            viewCamera.fieldOfView = Mathf.Lerp(
                viewCamera.fieldOfView, target, fovSpeed * Time.deltaTime);
        }

        private void UpdateCameraMode()
        {
            if (viewCamera == null) return;

            // Target position: FPP = origin (at pivot), TPP = offset behind player
            Vector3 targetLocal = _isTPP ? tppOffset : Vector3.zero;

            // Smooth transition between TPP and FPP
            _currentCamLocal = Vector3.Lerp(
                _currentCamLocal, targetLocal, viewSwitchSpeed * Time.deltaTime);

            // Camera collision: don't go through walls
            if (_isTPP && cameraPivot != null)
            {
                Vector3 worldTarget = cameraPivot.TransformPoint(_currentCamLocal);
                Vector3 pivotWorld = cameraPivot.position;
                Vector3 dir = worldTarget - pivotWorld;
                float dist = dir.magnitude;

                if (dist > 0.01f &&
                    Physics.SphereCast(pivotWorld, cameraCollisionRadius,
                        dir.normalized, out RaycastHit hit, dist, cameraCollisionMask))
                {
                    // Pull camera forward so it doesn't clip through wall
                    float safeDist = Mathf.Max(0.1f, hit.distance - cameraCollisionRadius);
                    Vector3 safeWorld = pivotWorld + dir.normalized * safeDist;
                    viewCamera.transform.position = safeWorld;
                }
                else
                {
                    viewCamera.transform.localPosition = _currentCamLocal;
                }
            }
            else
            {
                viewCamera.transform.localPosition = _currentCamLocal;
            }

            // Show/hide player body: visible in TPP, hidden in FPP
            if (playerBody != null)
                playerBody.SetActive(_isTPP);

            // Show/hide gun viewmodel: visible in FPP, hidden in TPP
            if (weaponAnimator != null)
                weaponAnimator.gameObject.SetActive(!_isTPP);
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
