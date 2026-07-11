using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    /// <summary>
    /// A touch button that reports held state, not just clicks — needed for full-auto fire.
    /// Also exposes a one-frame Pressed flag for tap actions like reload and jump.
    /// </summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private bool _pressedThisFrame;

        public bool IsHeld { get; private set; }

        /// <summary>True on the frame the button went down. Reading clears it.</summary>
        public bool ConsumePress()
        {
            bool p = _pressedThisFrame;
            _pressedThisFrame = false;
            return p;
        }

        public void OnPointerDown(PointerEventData e)
        {
            IsHeld = true;
            _pressedThisFrame = true;
        }

        public void OnPointerUp(PointerEventData e)
        {
            IsHeld = false;
        }

        private void OnDisable()
        {
            // A button hidden mid-press (death, pause) must not leave the gun stuck firing.
            IsHeld = false;
            _pressedThisFrame = false;
        }
    }
}
