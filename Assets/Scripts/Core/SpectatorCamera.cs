using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// What you become when you die: a camera loose on the island, with the round still running
    /// underneath it. The enemies keep hunting, the waves keep coming, and you can move through
    /// all of it as nothing at all.
    ///
    /// This is not spectating a teammate — there is no teammate. This game is one player against
    /// waves, and a "watch your squad" mode would need multiplayer, which would need a network
    /// layer, a host, state sync, and a different game. What this IS is honest: you are dead,
    /// the island is not, and you can look at what killed you.
    /// </summary>
    public class SpectatorCamera : MonoBehaviour
    {
        [SerializeField] private Camera cam;

        [Tooltip("The camera the player was looking through. Held by reference rather than " +
                 "found via Camera.main, which only ever returns an *enabled* camera tagged " +
                 "MainCamera — so the moment this one takes over, the search for the old one " +
                 "would start returning the wrong answer, or none.")]
        [SerializeField] private Camera playerCamera;

        [Header("Movement")]
        [SerializeField] private float speed = 12f;
        [SerializeField] private float boostMultiplier = 2.5f;
        [SerializeField] private float lookSensitivity = 2f;

        [Header("Rise")]
        [Tooltip("Metres the camera drifts upward as it takes over, so death reads as leaving " +
                 "rather than as the screen freezing where you fell.")]
        [SerializeField] private float riseHeight = 6f;
        [SerializeField] private float riseSeconds = 2.5f;

        private bool _active;
        private float _yaw;
        private float _pitch;
        private float _risen;

        private void Awake()
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam != null) cam.enabled = false;
        }

        /// <summary>
        /// Takes over from the dead player. GameLoop calls this at the moment of death, with the
        /// position the body fell at.
        /// </summary>
        public void Begin(Vector3 from)
        {
            _active = true;
            _risen = 0f;

            transform.position = from;

            // Inherit the direction they were facing. Snapping the view somewhere else at the
            // moment of death is disorienting in exactly the wrong way — you want to see what
            // killed you, and it is in front of you.
            Vector3 forward = playerCamera != null
                ? playerCamera.transform.forward
                : transform.forward;

            _yaw = Quaternion.LookRotation(forward).eulerAngles.y;
            _pitch = 10f;

            // Old view off before the new one on. The other order leaves both live for a frame,
            // and which one you see then comes down to their depths.
            if (playerCamera != null) playerCamera.enabled = false;
            if (cam != null) cam.enabled = true;

            // The ears go with the eyes. The listener lives on the player's camera, and leaving
            // it there means a ghost who flies across the island still hears it from where the
            // body fell — gunfire in front of you arriving from behind.
            TakeAudioListener();

            // Cursor stays free. The game-over panel is on screen with buttons on it, and a
            // locked cursor would make them unclickable — the player would be a ghost who cannot
            // leave. Hold a mouse button (or drag, on a phone) to look around instead.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void TakeAudioListener()
        {
            var existing = FindAnyObjectByType<AudioListener>();
            if (existing != null && existing.gameObject != gameObject)
                Destroy(existing);

            if (GetComponent<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();
        }

        private void Update()
        {
            if (!_active) return;

            Rise();
            Look();
            Move();
        }

        /// <summary>The slow lift out of the body. Once, at the start, then never again.</summary>
        private void Rise()
        {
            if (_risen >= riseHeight) return;

            float step = riseHeight / riseSeconds * Time.deltaTime;
            step = Mathf.Min(step, riseHeight - _risen);

            _risen += step;
            transform.position += Vector3.up * step;
        }

        /// <summary>
        /// Look is held, not free. The cursor has to stay usable for the game-over buttons, so
        /// the camera only turns while a mouse button is down — the same grip every 3D editor
        /// uses, and the reason nobody has to be told about it.
        /// </summary>
        private void Look()
        {
            float sens = lookSensitivity * GameSettings.SensitivityScale;

            if (Input.GetMouseButton(1) || Input.GetMouseButton(0))
            {
                _yaw += Input.GetAxisRaw("Mouse X") * sens;
                _pitch -= Input.GetAxisRaw("Mouse Y") * sens;
            }

            // Touch: drag anywhere that is not a button. Unity's raycaster already ate the touch
            // if it landed on one, but touchCount does not know that — so ignore drags that
            // started over UI.
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);

                bool overUi = UnityEngine.EventSystems.EventSystem.current != null &&
                              UnityEngine.EventSystems.EventSystem.current
                                  .IsPointerOverGameObject(touch.fingerId);

                if (touch.phase == TouchPhase.Moved && !overUi)
                {
                    _yaw += touch.deltaPosition.x * sens * 0.05f;
                    _pitch -= touch.deltaPosition.y * sens * 0.05f;
                }
            }

            _pitch = Mathf.Clamp(_pitch, -85f, 85f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void Move()
        {
            float v = Input.GetAxisRaw("Vertical");
            float h = Input.GetAxisRaw("Horizontal");

            float up = 0f;
            if (Input.GetKey(KeyCode.Space)) up += 1f;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C)) up -= 1f;

            Vector3 move = transform.forward * v + transform.right * h + Vector3.up * up;
            if (move.sqrMagnitude > 1f) move.Normalize();

            float rate = speed * (Input.GetKey(KeyCode.LeftShift) ? boostMultiplier : 1f);

            // No collider, no gravity, no NavMesh. A ghost walks through the trees, and giving it
            // physics would only mean getting it stuck inside one.
            transform.position += move * (rate * Time.deltaTime);
        }
    }
}
