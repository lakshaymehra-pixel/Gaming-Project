using Game.UI;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Single source of input for the player. Reads the on-screen controls on a touch
    /// device and falls back to keyboard + mouse in the Editor, so the same player prefab
    /// is testable on a desktop without a phone attached.
    /// </summary>
    public class PlayerInputHub : MonoBehaviour
    {
        [Header("Touch controls (optional on desktop)")]
        [SerializeField] private VirtualJoystick moveJoystick;
        [SerializeField] private TouchLookArea lookArea;
        [SerializeField] private HoldButton fireButton;
        [SerializeField] private HoldButton reloadButton;
        [SerializeField] private HoldButton aimButton;
        [SerializeField] private HoldButton jumpButton;

        [Header("Desktop fallback")]
        [SerializeField] private bool allowKeyboardMouse = true;
        [SerializeField] private float mouseSensitivity = 8f;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool FireHeld { get; private set; }
        public bool AimHeld { get; private set; }
        public bool ReloadPressed { get; private set; }
        public bool JumpPressed { get; private set; }

        private bool UseTouch => moveJoystick != null && Application.isMobilePlatform;

        private void Update()
        {
            Move = Vector2.zero;
            Look = Vector2.zero;
            FireHeld = false;
            AimHeld = false;
            ReloadPressed = false;
            JumpPressed = false;

            ReadTouch();

            if (allowKeyboardMouse && !Application.isMobilePlatform)
                ReadKeyboardMouse();
        }

        private void ReadTouch()
        {
            if (moveJoystick != null) Move += moveJoystick.Value;
            if (lookArea != null) Look += lookArea.ConsumeDelta();
            if (fireButton != null) FireHeld |= fireButton.IsHeld;
            if (aimButton != null) AimHeld |= aimButton.IsHeld;
            if (reloadButton != null) ReloadPressed |= reloadButton.ConsumePress();
            if (jumpButton != null) JumpPressed |= jumpButton.ConsumePress();
        }

        private void ReadKeyboardMouse()
        {
            Move += new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical"));

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Look += new Vector2(
                    Input.GetAxisRaw("Mouse X"),
                    Input.GetAxisRaw("Mouse Y")) * mouseSensitivity;
            }

            FireHeld |= Input.GetMouseButton(0);
            AimHeld |= Input.GetMouseButton(1);
            ReloadPressed |= Input.GetKeyDown(KeyCode.R);
            JumpPressed |= Input.GetKeyDown(KeyCode.Space);
        }
    }
}
