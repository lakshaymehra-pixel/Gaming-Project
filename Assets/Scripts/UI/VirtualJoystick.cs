using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    /// <summary>
    /// Left-thumb movement stick. The knob is clamped inside the base and Value is
    /// reported as a normalized vector the motor can consume directly.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("References")]
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform knob;

        [Header("Behaviour")]
        [SerializeField] private float deadZone = 0.12f;

        [Tooltip("Recenter the stick under the thumb wherever the touch lands, instead of "
               + "keeping it pinned. Feels better on phones of different sizes.")]
        [SerializeField] private bool dynamicOrigin = true;

        private Canvas _canvas;
        private Camera _uiCamera;
        private Vector2 _restPosition;
        private float _radius;

        public Vector2 Value { get; private set; }
        public bool IsHeld { get; private set; }

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            _uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : _canvas.worldCamera;

            _restPosition = background.anchoredPosition;
            _radius = background.sizeDelta.x * 0.5f;
        }

        public void OnPointerDown(PointerEventData e)
        {
            IsHeld = true;

            if (dynamicOrigin &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)background.parent, e.position, _uiCamera, out Vector2 local))
            {
                background.anchoredPosition = local;
            }

            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    background, e.position, _uiCamera, out Vector2 local))
                return;

            Vector2 raw = local / _radius;
            float mag = raw.magnitude;

            if (mag < deadZone)
            {
                Value = Vector2.zero;
            }
            else
            {
                // Rescale past the dead zone so the stick still reaches full 1.0 at the edge.
                Vector2 dir = raw / mag;
                float scaled = Mathf.Clamp01((mag - deadZone) / (1f - deadZone));
                Value = dir * scaled;
            }

            knob.anchoredPosition = Vector2.ClampMagnitude(local, _radius);
        }

        public void OnPointerUp(PointerEventData e)
        {
            IsHeld = false;
            Value = Vector2.zero;
            knob.anchoredPosition = Vector2.zero;
            if (dynamicOrigin) background.anchoredPosition = _restPosition;
        }
    }
}
