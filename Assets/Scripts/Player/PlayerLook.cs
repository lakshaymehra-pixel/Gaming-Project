using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Yaw turns the body, pitch tilts the camera. Splitting them this way keeps the
    /// capsule upright while the view can look up and down.
    /// </summary>
    public class PlayerLook : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform cameraPivot;

        [Header("Sensitivity")]
        [SerializeField] private float sensitivity = 0.15f;
        [SerializeField] private float aimSensitivityMultiplier = 0.6f;

        [Header("Limits")]
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;

        [Header("Smoothing")]
        [SerializeField] private float smoothing = 22f;

        private float _yaw;
        private float _pitch;
        private float _targetYaw;
        private float _targetPitch;

        public bool IsAiming { get; set; }
        public Transform CameraPivot => cameraPivot;

        private void Start()
        {
            _yaw = _targetYaw = transform.eulerAngles.y;
        }

        /// <param name="delta">Raw pointer/stick delta in pixels for this frame.</param>
        public void Look(Vector2 delta)
        {
            float s = sensitivity * (IsAiming ? aimSensitivityMultiplier : 1f);
            _targetYaw += delta.x * s;
            _targetPitch = Mathf.Clamp(_targetPitch - delta.y * s, minPitch, maxPitch);
        }

        private void LateUpdate()
        {
            // Frame-rate independent exponential smoothing.
            float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            _yaw = Mathf.LerpAngle(_yaw, _targetYaw, t);
            _pitch = Mathf.Lerp(_pitch, _targetPitch, t);

            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        /// <summary>Instantly face a direction, skipping smoothing. Used on respawn.</summary>
        public void SnapTo(float yaw, float pitch = 0f)
        {
            _yaw = _targetYaw = yaw;
            _pitch = _targetPitch = pitch;
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
    }
}
