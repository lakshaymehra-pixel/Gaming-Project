using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Swings a humanoid's leg pivots while it moves. There is no Animator and no clips —
    /// the phase advances with distance travelled, so the stride length stays believable at
    /// any speed and a stopped character stands still instead of moon-walking.
    /// </summary>
    public class ProceduralWalker : MonoBehaviour
    {
        [Header("Limb pivots (rotated at hip/shoulder)")]
        [SerializeField] private Transform leftLeg;
        [SerializeField] private Transform rightLeg;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;

        [Header("Stride")]
        [SerializeField] private float strideDegrees = 38f;
        [Tooltip("Phase advance per metre travelled. Higher = faster steps.")]
        [SerializeField] private float stepsPerMetre = 1.6f;
        [SerializeField] private float armSwingScale = 0.4f;

        private Vector3 _lastPosition;
        private float _phase;
        private float _blend;

        private void Start()
        {
            _lastPosition = transform.position;
        }

        private void LateUpdate()
        {
            Vector3 delta = transform.position - _lastPosition;
            _lastPosition = transform.position;
            delta.y = 0f;

            float speed = delta.magnitude / Mathf.Max(Time.deltaTime, 1e-5f);

            // Advance by distance, not time: steps track the ground actually covered.
            _phase += delta.magnitude * stepsPerMetre * Mathf.PI * 2f;

            // Ease the swing in and out so starting and stopping do not snap the legs.
            _blend = Mathf.MoveTowards(_blend, speed > 0.3f ? 1f : 0f, 5f * Time.deltaTime);

            float swing = Mathf.Sin(_phase) * strideDegrees * _blend;

            if (leftLeg  != null) leftLeg.localRotation  = Quaternion.Euler( swing, 0f, 0f);
            if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);

            // Arms are optional — a rifle-carrying soldier keeps them on the gun, so the
            // factory usually leaves these unassigned.
            float armSwing = swing * armSwingScale;
            if (leftArm  != null) leftArm.localRotation  = Quaternion.Euler(-armSwing, 0f, 0f);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler( armSwing, 0f, 0f);
        }
    }
}
