using UnityEngine;

namespace Game.Weapons
{
    /// <summary>
    /// Moves the viewmodel. No animation clips and no rig — the gun is a block, so its
    /// motion is driven procedurally from the weapon's state. That keeps the whole thing in
    /// code (nothing to import, nothing to keep in sync with a .fbx) and means the reload
    /// dip is always exactly as long as WeaponData says the reload is.
    ///
    /// Sits on the gun model, which must be a child of the camera.
    /// </summary>
    public class WeaponAnimator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Weapon weapon;
        [SerializeField] private Player.PlayerMotor motor;

        [Header("Aim down sights")]
        [SerializeField] private Vector3 hipPosition = new(0.28f, -0.22f, 0.55f);
        [SerializeField] private Vector3 aimPosition = new(0f, -0.14f, 0.42f);
        [SerializeField] private float aimSpeed = 14f;

        [Header("Reload")]
        [Tooltip("How far the gun drops out of view during a reload.")]
        [SerializeField] private Vector3 reloadOffset = new(0.05f, -0.28f, -0.15f);
        [SerializeField] private Vector3 reloadTilt = new(-38f, 12f, 8f);

        [Header("Fire kick")]
        [SerializeField] private float kickBack = 0.06f;
        [SerializeField] private float kickUp = 2.5f;
        [SerializeField] private float kickRecovery = 12f;

        [Header("Sway and bob")]
        [SerializeField] private float bobFrequency = 9f;
        [SerializeField] private float bobAmount = 0.022f;
        [SerializeField] private float swayAmount = 0.015f;
        [SerializeField] private float swaySmoothing = 8f;

        private Vector3 _kickPosition;
        private Vector3 _kickRotation;
        private Vector3 _sway;
        private float _bobPhase;
        private float _reloadProgress;   // 0 = ready, 1 = fully dipped

        private bool _isAiming;

        /// <summary>Set by PlayerController each frame; the animator does not read input.</summary>
        public bool IsAiming { set => _isAiming = value; }

        private void OnEnable()
        {
            if (weapon != null) weapon.RecoilKick += OnFired;
        }

        private void OnDisable()
        {
            if (weapon != null) weapon.RecoilKick -= OnFired;
        }

        private void OnFired(Vector2 _)
        {
            _kickPosition += new Vector3(0f, 0f, -kickBack);
            _kickRotation += new Vector3(
                -kickUp, Random.Range(-1.2f, 1.2f), Random.Range(-0.8f, 0.8f));
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // Reload dip. Driving it off the weapon's IsReloading flag rather than a timer
            // means it cannot desync from the actual reload.
            float reloadTarget = weapon != null && weapon.IsReloading ? 1f : 0f;
            _reloadProgress = Mathf.MoveTowards(
                _reloadProgress, reloadTarget, dt / GetDipTime());

            // A reload has to break the aim, or the sights stay glued to the eye while the
            // magazine changes.
            float aim = _isAiming && _reloadProgress < 0.05f ? 1f : 0f;

            Vector3 basePosition = Vector3.Lerp(hipPosition, aimPosition, GetAimBlend(aim, dt));

            // Springs decay toward zero; each shot re-arms them.
            _kickPosition = Vector3.Lerp(_kickPosition, Vector3.zero, kickRecovery * dt);
            _kickRotation = Vector3.Lerp(_kickRotation, Vector3.zero, kickRecovery * dt);

            Vector3 bob = ComputeBob(dt);
            Vector3 sway = ComputeSway(dt);

            // Ease the reload with a smoothstep so the gun does not snap at either end.
            float r = Mathf.SmoothStep(0f, 1f, _reloadProgress);

            transform.localPosition =
                basePosition + _kickPosition + bob + sway + reloadOffset * r;

            transform.localRotation = Quaternion.Euler(_kickRotation + reloadTilt * r);
        }

        private float _aimBlend;

        private float GetAimBlend(float target, float dt)
        {
            _aimBlend = Mathf.MoveTowards(_aimBlend, target, aimSpeed * dt);
            return _aimBlend;
        }

        /// <summary>Time for the gun to drop or come back up, derived from the reload length
        /// so a fast weapon does not spend its whole reload mid-animation.</summary>
        private float GetDipTime()
        {
            float reload = weapon != null && weapon.Data != null
                ? weapon.Data.reloadSeconds
                : 2f;
            return Mathf.Max(0.12f, reload * 0.18f);
        }

        private Vector3 ComputeBob(float dt)
        {
            if (motor == null) return Vector3.zero;

            // Bob only while actually walking on the ground. Bobbing midair reads as a bug.
            bool moving = motor.IsGrounded && motor.CurrentSpeed > 0.5f && !motor.IsSliding;
            if (!moving)
            {
                _bobPhase = Mathf.Lerp(_bobPhase, 0f, 6f * dt);
                return Vector3.zero;
            }

            float speedScale = motor.IsSprinting ? 1.6f : 1f;
            _bobPhase += dt * bobFrequency * speedScale;

            // The classic figure-of-eight: the horizontal component runs at half the
            // vertical's rate, so the gun traces a lissajous rather than a straight bounce.
            float amount = bobAmount * speedScale * (_isAiming ? 0.35f : 1f);
            return new Vector3(
                Mathf.Sin(_bobPhase * 0.5f) * amount,
                -Mathf.Abs(Mathf.Sin(_bobPhase)) * amount,
                0f);
        }

        private Vector3 ComputeSway(float dt)
        {
            // Lag the gun behind the view: turn right and the gun trails left for a beat.
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            var target = new Vector3(
                Mathf.Clamp(-mouseX * swayAmount, -0.05f, 0.05f),
                Mathf.Clamp(-mouseY * swayAmount, -0.05f, 0.05f),
                0f);

            if (_isAiming) target *= 0.3f;

            _sway = Vector3.Lerp(_sway, target, swaySmoothing * dt);
            return _sway;
        }
    }
}
