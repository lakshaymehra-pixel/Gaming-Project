using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    /// <summary>
    /// Invisible full-height panel on the right of the screen. Any drag inside it is
    /// reported as a look delta. Buttons drawn on top of it still win the touch, because
    /// the EventSystem hands the pointer to the topmost raycast target.
    /// </summary>
    public class TouchLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private int _activePointerId = -1;
        private Vector2 _lastPosition;
        private Vector2 _delta;

        /// <summary>Look delta accumulated since the last read. Reading clears it.</summary>
        public Vector2 ConsumeDelta()
        {
            Vector2 d = _delta;
            _delta = Vector2.zero;
            return d;
        }

        public void OnPointerDown(PointerEventData e)
        {
            // Track only the first finger that lands here; a second thumb (e.g. fire) must
            // not hijack the camera.
            if (_activePointerId != -1) return;

            _activePointerId = e.pointerId;
            _lastPosition = e.position;
            _delta = Vector2.zero;
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != _activePointerId) return;

            _delta += e.position - _lastPosition;
            _lastPosition = e.position;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != _activePointerId) return;

            _activePointerId = -1;
            _delta = Vector2.zero;
        }
    }
}
